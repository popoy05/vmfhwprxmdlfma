using System.Collections;
using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 타격감 + 화려함 + 궁극기 컷신
    //  - 데미지 수치를 크게 (기본 배율 × 치명타), 숫자가 쾅 튀어나옴
    //  - 맞을 때: 무기별 타격 이펙트 · 화면 흔들림 · 히트스톱(잠깐 멈칫)
    //  - 스킬을 쓸 때: 무기별 시전 이펙트
    //  - C(궁극기): 레터박스 + 시네마틱 카메라 + 이펙트 조합 + 타이틀 카드 → 섬광 → 스킬 (모든 타격 치명타)
    //  - F1: 함수 그래프·이펙트 함수식 보기 켜고 끄기 (기본 꺼짐)
    public partial class NewModeCombat
    {
        const float k_DamageScale = 46f; // 전체 피해 (예전 37 → 46, 약 ×1.25)

        bool m_Ultimate;          // 궁극기 진행 중 (모든 타격 치명타 + 1.5배)
        bool m_InCutscene;
        public bool InCutscene => m_InCutscene;
        float m_HitstopEnd;
        bool m_HitstopOn;
        float m_SlowMo = 1f;      // 데드아이 같은 느린 시간 (히트스톱이 끝나면 이 값으로 돌아감)
        int m_LastImpactFrame = -1, m_LastImpactFxFrame = -1;
        float m_TrailWindow;
        int m_TrailSpawned;

        // 무기별 이펙트 조합 (모두 Hovl Magic effects / Eric VFX 에셋 그대로)
        struct Theme
        {
            public string Cast;    // 스킬 시전 (발밑)
            public string Impact;  // 큰 타격
            public string Circle;  // 컷신: 발밑 마법진
            public string Aura;    // 컷신: 몸을 감싸는 오라
            public string Charge;  // 컷신: 무기 끝에 모이는 빛
            public string Pillar;  // 컷신: 하늘로 솟는 기둥
            public string Release; // 컷신 끝: 터지는 순간
            public string Trail;   // 스킬 곡선을 따라 터지는 이펙트
            public string Slash;   // 평타를 휘두를 때 칼날 궤적을 따라 나오는 베기 이펙트 (총은 없음)
            public float CircleSize, AuraSize;
        }

        static readonly Theme[] k_Themes =
        {
            // 얼음검
            new Theme { Cast = "Sparks explode blue", Impact = "Sparks explode blue", Circle = "Freeze circle", Aura = "Snow AOE",
                Charge = "Sparks flashing blue", Pillar = "Crystal effect blue", Release = "Freeze circle", Trail = "Sparks explode blue", Slash = "Snow slash", CircleSize = 2.2f, AuraSize = 0.9f },
            // 대지 도끼
            new Theme { Cast = "Dust ground", Impact = "Stones hit", Circle = "FX_Magic Door_Gold", Aura = "Dust loop",
                Charge = "Sparks flashing red", Pillar = "Crystal effect red", Release = "Ground AOE explosion", Trail = "Stones hit", Slash = "Stone slash", CircleSize = 2.2f, AuraSize = 0.7f },
            // 번개 단검
            new Theme { Cast = "Plexus AoE", Impact = "Electro hit", Circle = "Magic circle 2", Aura = "Lightning aura",
                Charge = "Sparks flashing pink", Pillar = "Crystal effect blue", Release = "Electro slash", Trail = "Electro hit", Slash = "Electro slash", CircleSize = 2.2f, AuraSize = 0.9f },
            // 권총
            new Theme { Cast = "Buff", Impact = "Sparks explode yellow", Circle = "FX_Magic Door_Gold", Aura = "Magic shield yellow",
                Charge = "Sparks flashing yellow", Pillar = "FX_LightPillar", Release = "Holy hit", Trail = "Sparks explode yellow", CircleSize = 2.2f, AuraSize = 0.9f },
        };

        Theme Th => k_Themes[Mathf.Clamp(m_Weapon.Index, 0, k_Themes.Length - 1)];

        Vector3 Feet => transform.position + Vector3.up * 0.03f * S;
        Vector3 Chest => transform.position + Vector3.up * 1.1f * S;

        // ================================================================== 매 프레임

        void UpdateJuice()
        {
            // 히트스톱: 잠깐 시간을 거의 멈췄다가 되돌린다 (일시정지 중엔 건드리지 않음)
            if (m_HitstopOn && !m_Player.Paused && Time.unscaledTime >= m_HitstopEnd)
            {
                m_HitstopOn = false;
                Time.timeScale = m_SlowMo;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame && !m_Player.Paused)
            {
                bool on = !GeoFx.ShowGraphs;
                GeoFx.ShowGraphs = on;
                Fx.ShowFormula = on;
                ShowBanner(on ? "<b>수학 보기 켜짐</b>\n<size=42%>스킬 그래프 · 이펙트 함수식 표시 (F1)</size>"
                              : "<b>수학 보기 꺼짐</b>\n<size=42%>F1로 다시 켜기</size>");
            }
        }

        void Hitstop(float seconds)
        {
            if (m_Player.Paused || m_InCutscene) return;
            m_HitstopEnd = Mathf.Max(m_HitstopEnd, Time.unscaledTime + seconds);
            m_HitstopOn = true;
            Time.timeScale = 0.04f;
        }

        // ================================================================== 맞을 때

        void OnImpact(TrainingDummy d, bool big, bool crit)
        {
            // 이펙트는 하나만: 치명타/큰 타격일 때 무기별 타격 이펙트 (겹치지 않게 같은 프레임엔 한 번)
            bool gunTap = m_Weapon.Kind == WeaponKind.Gun && !big && !crit;
            if ((big || crit) && m_LastImpactFxFrame != Time.frameCount)
            {
                m_LastImpactFxFrame = Time.frameCount;
                Fx.Play(Th.Impact, d.Center, Quaternion.LookRotation(d.Center - Chest), (crit ? 0.6f : 0.45f) * S, 0.15f, false);
            }
            else if (m_LastImpactFxFrame != Time.frameCount)
            {
                // 작은 타격도 손맛: 맞은 자리에 작은 불꽃 (직업 색)
                m_LastImpactFxFrame = Time.frameCount;
                Fx.Play("Sparks explode white", d.Center, Quaternion.LookRotation(d.Center - Chest), 0.22f * S, 0.1f, false);
            }

            // 한 프레임에 여러 명을 맞혀도 흔들림·멈칫은 한 번만
            if (m_LastImpactFrame == Time.frameCount) return;
            m_LastImpactFrame = Time.frameCount;

            if (crit) { m_Player.Shake(0.16f, 0.32f); Hitstop(m_Ultimate ? 0.11f : 0.08f); PostFx.Pulse(0.45f, C); }
            else if (big) { m_Player.Shake(0.1f, 0.24f); Hitstop(0.055f); PostFx.Pulse(0.25f, C); }
            else if (!gunTap) { m_Player.Shake(0.04f, 0.12f); Hitstop(0.025f); }
            else m_Player.Shake(0.025f, 0.08f);
        }

        // 함수 그래프 대신: 곡선을 따라 무기 이펙트가 차례로 터진다 (그려지는 속도 = reveal)
        void OnHiddenCurve(Vector3[] points, float width, Color color, float reveal)
        {
            if (!this || m_Weapon == null || color.a < 0.6f || points.Length < 2) return;
            float length = 0f;
            for (int i = 1; i < points.Length; i++) length += Vector3.Distance(points[i - 1], points[i]);
            if (length < 0.3f * S) return;
            int count = Mathf.Clamp(Mathf.RoundToInt(length / (1.1f * S)), 2, 10);
            // 너무 많이 터지지 않게 (0.3초에 최대 24개)
            if (Time.time - m_TrailWindow > 0.3f) { m_TrailWindow = Time.time; m_TrailSpawned = 0; }
            count = Mathf.Min(count, 30 - m_TrailSpawned);
            if (count < 2) return;
            m_TrailSpawned += count;
            float size = Mathf.Clamp(width / S * 3.5f, 0.3f, 0.6f) * S;
            string fx = Th.Trail;
            float step = length / (count - 1), walked = 0f, next = 0f;
            int made = 0;
            for (int i = 1; i < points.Length && made < count; i++)
            {
                float seg = Vector3.Distance(points[i - 1], points[i]);
                while (made < count && next <= walked + seg + 0.001f)
                {
                    Vector3 pos = Vector3.Lerp(points[i - 1], points[i], seg > 0f ? Mathf.Clamp01((next - walked) / seg) : 0f);
                    float delay = reveal * (next / length);
                    // 곡선 위(땅) + 곡선을 감고 올라가는 3D 나선 위(공중) 두 군데:
                    //   나선 P(u) = C(u) + ŷ·(h₀ + a sin 4πu) + n̂·a cos 4πu   (n̂: 곡선의 옆 방향)
                    Vector3 tangent = (points[i] - points[i - 1]).normalized;
                    Vector3 normal = Vector3.Cross(Vector3.up, tangent).normalized;
                    float u = next / length, phi = u * Mathf.PI * 4f;
                    Vector3 air = pos + Vector3.up * (0.9f + 0.45f * Mathf.Sin(phi)) * S + normal * 0.6f * Mathf.Cos(phi) * S;
                    Quaternion rot = tangent.sqrMagnitude > 0f ? Quaternion.LookRotation(tangent, Vector3.up) : Quaternion.identity;
                    string airFx = Th.Charge;
                    if (delay <= 0.01f) { Fx.Play(fx, pos, rot, size, 0.15f, false); Fx.Play(airFx, air, rot, size * 0.7f, 0.15f, false); }
                    else DOVirtual.DelayedCall(delay, () => { Fx.Play(fx, pos, rot, size, 0.15f, false); Fx.Play(airFx, air, rot, size * 0.7f, 0.15f, false); }).SetLink(gameObject);
                    made++;
                    next += step;
                }
                walked += seg;
            }
        }

        void OnDestroy()
        {
            if (GeoFx.HiddenCurve == (System.Action<Vector3[], float, Color, float>)OnHiddenCurve) GeoFx.HiddenCurve = null;
            TrainingDummy.Bumped -= OnBumped;
        }

        // 스킬 시전 이펙트 (Q·E·X)
        void CastFlair(SkillDef skill)
        {
            Fx.Play(Th.Cast, Feet, Facing(), 0.5f * S, 0.25f, false);
        }

    }
}
