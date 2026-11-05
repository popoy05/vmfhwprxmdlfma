using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 2. 대지 도끼 — 기믹 "모아 찍기"
    //  좌클릭을 누르고 있으면 힘을 모은다 (3단계). 놓으면 단계만큼 멀리 뛰어 내려찍고 충격파로 적을 띄운다.
    //  평타  대지 가르기 2연타   (톡 누르면: 내려찍기 → 가로 휩쓸기)
    //  Q     회전 투척           부메랑처럼 날아갔다 되돌아오는 도끼 (가고 오며 두 번 맞힘)
    //  E     대지 회오리         2초 동안 도끼를 돌리며 이동 (주변을 계속 벰)
    //  X     대지 융기           앞으로 바위 기둥이 차례로 솟아 적을 하늘로 띄운다
    //  C     운석 낙하           높이 뛰어올라 운석을 쏟아붓고, 한가운데로 내리꽂는다
    public partial class NewModeCombat
    {
        static readonly Color k_Earth = new Color(1f, 0.6f, 0.25f);
        float m_AxeCharge;

        WeaponDef AxeWeapon() => new WeaponDef
        {
            GimmickName = "모아 찍기",
            GimmickInfo = "좌클릭을 누르고 있으면 힘을 모아 내려찍는다 (3단계)",
            Gimmick = () => m_AxeCharge > 0f
                ? $"{Bar(m_AxeCharge / 1.2f, 12)}  <b>{Mathf.Clamp(Mathf.FloorToInt(m_AxeCharge / 0.4f), 0, 3)}단계</b>"
                : "<size=80%><color=#FFFFFFAA>좌클릭 길게 누르기 → 놓으면 내려찍기</color></size>",
            Basic = new SkillDef { Key = "좌클릭", Name = "대지 가르기", Formula = "x² + y² = R²", Run = AxeBasic },
            Skills = new[]
            {
                new SkillDef { Key = "Q", Name = "회전 투척", Formula = "x = a sin πu, y = b sin 2πu", Cooldown = 4f, Run = AxeBoomerang,
                    Detail = () => $"x = {N(8f * S)} sin πu,   y = {N(1.6f * S)} sin 2πu" },
                new SkillDef { Key = "E", Name = "대지 회오리", Formula = "θ(t) = ωt", Cooldown = 8f, Run = AxeWhirlwind,
                    Detail = () => "θ(t) = 4π·t,   0 ≤ t ≤ 2" },
                new SkillDef { Key = "X", Name = "대지 융기", Formula = "aₙ = a₁ + (n−1)d", Cooldown = 7f, Run = AxePillars,
                    Detail = () => $"aₙ = {N(1.5f * S)} + (n − 1)·{N(1.5f * S)},   n = 1…6" },
                new SkillDef { Key = "C", Name = "운석 낙하", Formula = "rₙ = c√n, θₙ = 137.5°n", Cooldown = 15f, Run = AxeMeteors,
                    Detail = () => $"rₙ = {N(1.1f * S)}√n,   θₙ = n × 137.5°" },
            },
        };

        // 톡 → 2연타, 길게 → 모아 찍기
        IEnumerator AxeBasic()
        {
            if (m_Combo > 0) { yield return AxeCombo(); yield break; }
            float held = 0f;
            Pose(new Vector3(0.2f, 0.6f, -0.3f), Vector3.forward, 0.95f);
            while (LmbHeld() && held < 0.2f) { held += Time.deltaTime; yield return null; }
            if (held < 0.2f) { yield return AxeCombo(); yield break; }
            yield return AxeChargeSlam();
        }

        IEnumerator AxeCombo() => Combo(2, step =>
        {
            float R = 2.3f * S;
            if (step == 0)
            {
                return Swing(VerticalArc(-35f, 125f), Vector3.right, 0.16f, 0.15f, 0.22f, 0.7f, () =>
                {
                    Fx.Play("Stone slash", World(new Vector2(R * 0.6f, 0f), 0.3f * S), Facing(), 0.3f * S, 0.15f, false);
                    foreach (var d in Targets())
                        if (GeoFunctions.InSector(Local(d.transform.position), R, 35f)) { Damage(d, 34f, true, "Stones hit"); }
                    m_Player.Shake(0.1f, 0.2f);
                }, 1f, 0.45f);
            }
            return Swing(HorizontalArc(100f, -95f), Vector3.up, 0.12f, 0.18f, 0.18f, 0.5f, () =>
            {
                foreach (var d in Targets())
                {
                    Vector2 p = Local(d.transform.position);
                    if (!GeoFunctions.InSector(p, R * 1.1f, 85f)) continue;
                    Damage(d, 30f, false, "Stones hit");
                    d.Knock((d.transform.position - transform.position).normalized * 6f * S);
                }
            }, 1f, 0.35f);
        });

        IEnumerator AxeChargeSlam()
        {
            m_AxeCharge = 0f;
            int level = 0;
            Crouch = 0.6f;
            TrailOn = false;
            while (LmbHeld() && m_AxeCharge < 1.2f)
            {
                m_AxeCharge += Time.deltaTime;
                // 도끼를 머리 위로 들고 부르르 떤다
                float shake = Mathf.Sin(Time.time * 60f) * 0.04f * m_AxeCharge;
                Pose(new Vector3(0.15f + shake, 1f, -0.35f), Vector3.forward, 0.95f);
                BodyLean = -8f - 6f * m_AxeCharge;
                int now = Mathf.Min(3, Mathf.FloorToInt(m_AxeCharge / 0.4f));
                if (now > level)
                {
                    level = now;
                    Fx.Play("Sparks flashing red", m_Rig ? m_Rig.TipPosition : Chest, Quaternion.identity, (0.25f + 0.1f * level) * S, 0.15f, false);
                    m_Player.Shake(0.03f * level, 0.15f);
                    UIAnimPunchGimmick();
                }
                yield return null;
            }
            while (LmbHeld()) { yield return null; } // 다 모았으면 놓을 때까지 대기
            level = Mathf.Max(1, Mathf.Min(3, Mathf.FloorToInt(m_AxeCharge / 0.4f)));
            m_AxeCharge = 0f;
            Crouch = 0f;

            m_Player.FaceCameraDirection();
            SetFrame();
            float D = (1.5f + 1.5f * level) * S, H = (0.8f + 0.5f * level) * S;
            yield return Leap(D, H, 0.3f + 0.08f * level, VerticalArc(-20f, 130f));

            Vector3 land = transform.position;
            float radius = (1.4f + 0.9f * level) * S;
            Crouch = 1f;
            Fx.Play(level == 3 ? "Ground AOE explosion" : "Stones hit", land, Quaternion.identity, (0.25f + 0.12f * level) * S, 0.2f, false);
            Fx.Play("Dust ground", land, Quaternion.identity, (0.35f + 0.15f * level) * S, 0.2f, false);
            m_Player.Shake(0.06f + 0.05f * level, 0.3f);
            foreach (var d in Nearest(land, radius, 99))
            {
                Damage(d, 30f + 25f * level, level >= 2, "Stones hit");
                d.Launch(Vector3.up * (2f + 1.5f * level) * S + (d.transform.position - land).normalized * 1.5f * S, 18f * S);
            }
            yield return new WaitForSeconds(0.25f);
            Crouch = 0f;
        }

        void UIAnimPunchGimmick()
        {
            if (m_GimmickLabel) Unity.FPS.MathMode.UIAnim.Punch(m_GimmickLabel.transform, 0.25f, 0.25f);
        }

        // Q: 부메랑 — 앞으로 날아갔다가 내게 돌아온다.  P(u) = (b sin 2πu, a sin πu) (돌아올 땐 내 위치로)
        IEnumerator AxeBoomerang()
        {
            float a = 8f * S, b = 1.6f * S, time = 1.2f;
            yield return Swing(HorizontalArc(110f, -40f, 0.05f), Vector3.up, 0.12f, 0.1f, 0f, 1f, null, 1f, 0.3f);
            SwordActive = false;
            LocksMovement = false;
            if (m_Rig && m_Rig.Weapon) m_Rig.Weapon.SetActive(false);

            GameObject axe = WeaponVisual.Create(1, S * 1.4f, out _);
            GameObject fx = Fx.Play("Smoke vortex", axe.transform.position, Quaternion.identity, 0.25f * S, time, false, axe.transform);
            Vector3 origin = m_Origin + Vector3.up * 1.1f * S;
            Quaternion frame = m_Frame;
            var lastHit = new Dictionary<TrainingDummy, float>();
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float u = t / time;
                Vector3 local = new Vector3(b * Mathf.Sin(2f * Mathf.PI * u), 0f, a * Mathf.Sin(Mathf.PI * u));
                Vector3 outPos = origin + frame * local;
                Vector3 home = transform.position + Vector3.up * 1.1f * S;
                // 돌아오는 동안 내가 움직였어도 내 손으로 오도록, 후반부엔 출발점을 내 위치 쪽으로 옮긴다
                axe.transform.position = outPos + (home - origin) * Mathf.InverseLerp(0.5f, 1f, u);
                axe.transform.rotation = frame * Quaternion.Euler(0f, t * 1440f, 90f);
                foreach (var d in Targets())
                {
                    if (lastHit.TryGetValue(d, out float when) && t - when < 0.35f) continue;
                    if ((d.Center - axe.transform.position).magnitude > 1f * S) continue;
                    lastHit[d] = t;
                    Damage(d, 34f, true, "Stones hit");
                    d.Knock((d.transform.position - axe.transform.position).normalized * 4f * S);
                }
                yield return null;
            }
            if (fx) fx.transform.SetParent(null, true);
            Destroy(axe);
            if (m_Rig && m_Rig.Weapon) m_Rig.Weapon.SetActive(true);
            Fx.Play("Stones hit", Chest, Quaternion.identity, 0.3f * S, 0.1f, false);
        }

        // E: 2초 동안 도끼를 원으로 돌리며 이동 — 0.25초마다 주변을 벤다
        IEnumerator AxeWhirlwind()
        {
            const float time = 2f;
            LocksMovement = false;
            TrailOn = true;
            GameObject dust = Fx.Play("Dust loop", transform.position, Quaternion.identity, 0.45f * S, time, false, transform);
            float nextHit = 0.1f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                // 도끼 방향: 수평 원 (cos ωt, sin ωt)
                float th = t * Mathf.PI * 4f;
                Pose(new Vector3(Mathf.Sin(th), -0.15f, Mathf.Cos(th)), Vector3.up, 1f);
                BodyTwist = Mathf.Sin(th) * 35f;
                if (t >= nextHit)
                {
                    nextHit += 0.25f;
                    foreach (var d in Nearest(transform.position, 2.4f * S, 99))
                    {
                        Damage(d, 12f, false, "Stones hit");
                        d.Knock((d.transform.position - transform.position).normalized * 2.5f * S);
                    }
                }
                yield return null;
            }
            BodyTwist = 0f;
            TrailOn = false;
            if (dust) dust.transform.SetParent(null, true);
        }

        // X: 내려찍으면 등차수열 간격으로 바위 기둥이 솟아 적을 띄운다
        IEnumerator AxePillars()
        {
            float a1 = 1.5f * S, d = 1.5f * S;
            const int count = 6;
            yield return Swing(VerticalArc(-40f, 140f), Vector3.right, 0.18f, 0.13f, 0f, 1f, null, 1f, 0.3f);
            m_Player.Shake(0.1f, 0.2f);
            Draw(u => new Vector2(u * GeoFunctions.Arithmetic(count, a1, d), 0f), 2, 0.05f, k_Earth, 0.5f, 1.4f);
            LocksMovement = false;
            Crouch = 1f;
            for (int n = 1; n <= count; n++)
            {
                Vector3 w = OnGround(new Vector2(GeoFunctions.Arithmetic(n, a1, d), 0f));
                Fx.Play("Stones hit", w, Quaternion.LookRotation(Vector3.up), 0.4f * S, 0.15f, false);
                if (n % 2 == 1) Fx.Play("Dust puff", w, Quaternion.identity, 0.3f * S, 0.1f, false);
                foreach (var t in Nearest(w, 1.2f * S, 99))
                {
                    Damage(t, 26f, n == count, "Stones hit");
                    t.Launch(Vector3.up * 6f * S, 16f * S);
                }
                yield return new WaitForSeconds(0.09f);
            }
            Crouch = 0f;
        }

        // C: 높이 뛰어올라 운석을 쏟아붓고(황금각 배열), 마지막에 한가운데로 내리꽂는다
        IEnumerator AxeMeteors()
        {
            float c = 1.1f * S;
            const int count = 14;
            Vector2 center = new Vector2(5f * S, 0f);
            Vector3 centerW = SafeGround(OnGround(center));
            CharacterController cc = m_Player.Controller;
            OverridesMotion = true;

            // 1) 하늘로 (y = H sin(πt/2T))
            Vector3 start = transform.position;
            Pose(new Vector3(0.1f, 1f, -0.2f), Vector3.forward, 1f);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                cc.Move(start + Vector3.up * 4.5f * S * Mathf.Sin(t / 0.35f * Mathf.PI * 0.5f) - transform.position);
                yield return null;
            }
            Fx.Play("Meteors AOE", centerW, Quaternion.identity, 0.5f * S, 1.3f, false);

            // 2) 운석 비
            for (int n = 1; n <= count; n++)
            {
                Vector3 w = OnGround(center + GeoFunctions.Sunflower(n, c));
                Fx.Play(n % 3 == 0 ? "Explosion" : "Stones hit", w, Quaternion.identity, (n % 3 == 0 ? 0.22f : 0.35f) * S, 0.1f, false);
                foreach (var d in Nearest(w, 1f * S, 99)) Damage(d, 22f, false, null);
                if (n % 3 == 0) m_Player.Shake(0.06f, 0.12f);
                yield return new WaitForSeconds(0.06f);
            }

            // 3) 한가운데로 내리꽂기
            Vector3 air = transform.position;
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                float u = t / 0.22f;
                Pose(Vector3.Slerp(new Vector3(0.1f, 1f, -0.2f), new Vector3(0.05f, -0.7f, 0.7f), u), Vector3.right, 1f);
                BodyLean = 30f * u;
                cc.Move(Vector3.Lerp(air, centerW + Vector3.up * 0.05f * S, u * u) - transform.position);
                yield return null;
            }
            Blink(centerW);
            Crouch = 1f;
            Fx.Play("Ground AOE explosion", centerW, Quaternion.identity, 0.7f * S, 0.3f, false);
            Fx.Play("Explosion", centerW + Vector3.up * 0.5f * S, Quaternion.identity, 0.5f * S, 0.2f, false);
            m_Player.Shake(0.3f, 0.6f);
            foreach (var d in Nearest(centerW, 4.5f * S, 99))
            {
                Damage(d, 90f, true, "Stones hit");
                d.Launch(Vector3.up * 7f * S + (d.transform.position - centerW).normalized * 3f * S, 18f * S);
            }
            OverridesMotion = false;
            yield return new WaitForSeconds(0.35f);
            Crouch = 0f;
            BodyLean = 0f;
        }
    }
}
