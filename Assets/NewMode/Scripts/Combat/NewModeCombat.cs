using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 새 모드 전투 (뼈대).
    //  좌클릭 평타(1·2·3단) · Q/E/X/C 스킬 · Tab 캐릭터 정보
    //  공격 방향: 카메라가 아니라 캐릭터 기준 — 이동 키를 누르고 있으면 그쪽, 아니면 보고 있는 쪽,
    //            가까운 적이 앞쪽에 있으면 그 적에게 살짝 맞춰 돈다 (총은 조준 중일 때만 화면 가운데로)
    //  직업마다 스킬 5개 (평타 + 4) — 직업별 스킬은 NewModeCombat.<무기>.cs
    //  모든 공격은 GeoFunctions의 함수로 모양이 정해지고, 그래프가 땅 위에 그려지며,
    //  에셋 이펙트(Hovl·Eric)를 그대로 소환하고 그 옆에 FxFormula가 분석한 식을 작게 띄운다.
    // 거리·크기는 모두 캐릭터 Scale(S)에 비례한다.
    public partial class NewModeCombat : MonoBehaviour
    {
        NewModePlayer m_Player;
        WeaponRig m_Rig;
        readonly List<WeaponDef> m_Weapons = new List<WeaponDef>();
        WeaponDef m_Weapon;
        Coroutine m_Current;
        int m_Combo;
        float m_ComboWindow, m_FireTimer;
        bool m_Buffered;
        bool m_CurrentIsBasic;
        bool m_Headshot;      // 이번 탄이 머리에 맞았는지 (Shoot이 정함)

        WeaponCatalog.ClassStats Stats => WeaponCatalog.All[Mathf.Clamp(m_Weapon != null ? m_Weapon.Index : 0, 0, WeaponCatalog.All.Length - 1)].Stats;
        /// <summary>HUD가 읽는 지금 평타 단계 (0이면 쉬는 중, 1·2·3)</summary>
        public int ComboStep => m_Combo;

        // 스킬 좌표계 원점·방향 (스킬을 쓴 순간의 캐릭터 기준: x 앞, y 오른쪽)
        Vector3 m_Origin;
        Quaternion m_Frame;

        // ---- WeaponRig가 읽는 값 (캐릭터 기준 방향)
        public bool SwordActive { get; private set; }
        public bool TrailOn { get; private set; }
        public Vector3 BladeDirLocal { get; private set; } = new Vector3(0.25f, -0.85f, 0.45f);
        public Vector3 BladeUpLocal { get; private set; } = Vector3.forward;
        public float Reach { get; private set; } = 0.9f;
        // 몸통 연출 (WeaponRig가 읽음)
        public float BodyTwist { get; private set; }
        public float BodyLean { get; private set; }
        public float Crouch { get; private set; }
        public float Recoil { get; private set; }
        public Vector3? LookTarget { get; private set; }  // 컷신에서 고개를 돌릴 곳
        string m_HitTag; // 다음 피해 숫자 위에 붙일 말 (기믹이 정함)

        // ---- NewModePlayer가 읽는 값
        public bool LocksMovement { get; private set; }
        public bool OverridesMotion { get; private set; }

        float S => m_Player ? m_Player.Scale : 1f;
        public WeaponDef CurrentWeapon => m_Weapon;
        public NewModePlayer PlayerRef => m_Player;
        Color C => m_Weapon != null ? m_Weapon.Color : Color.white;

        void Start()
        {
            m_Player = GetComponent<NewModePlayer>();
            PostFx.Init(); // 시작부터 후처리(톤매핑 등) 적용 — 예전엔 처음 때릴 때 켜져서 그 전엔 밝았음
            m_Font = MathModeAssets.Load() ? MathModeAssets.Load().Font : null;

            m_Weapons.Add(SwordWeapon());
            m_Weapons.Add(AxeWeapon());
            m_Weapons.Add(KnifeWeapon());
            m_Weapons.Add(PistolWeapon());
            // 전체 버프: 모든 스킬 재사용 대기 ×0.85
            foreach (var weapon in m_Weapons)
                foreach (var skill in weapon.Skills)
                    skill.Cooldown = Mathf.Round(skill.Cooldown * 0.85f * 10f) / 10f;
            for (int i = 0; i < m_Weapons.Count; i++)
            {
                WeaponCatalog.Info info = WeaponCatalog.All[i];
                m_Weapons[i].Index = i;
                m_Weapons[i].Name = info.Name;
                m_Weapons[i].Theme = info.Theme;
                m_Weapons[i].Kind = info.Kind;
                m_Weapons[i].Color = info.Color;
            }

            if (m_Player.Animator)
            {
                m_Rig = m_Player.Animator.GetComponent<WeaponRig>();
                if (!m_Rig) m_Rig = m_Player.Animator.gameObject.AddComponent<WeaponRig>();
                m_Rig.Init(transform, S, this);
            }

            TrainingDummy.Bumped += OnBumped;
            BuildHUD();
            Equip(WeaponCatalog.Selected, false);
            Fx.Tint = m_Weapon.Color; // 무기 색으로 물들일 공용 이펙트 (Fx.Neutral)
            gameObject.AddComponent<AdventureDirector>();
        }

        // ================================================================== 입력

        void Update()
        {
            foreach (var weapon in m_Weapons)
                foreach (var skill in weapon.Skills)
                    skill.Remaining = Mathf.Max(0f, skill.Remaining - Time.deltaTime);
            m_FireTimer -= Time.deltaTime;
            Recoil = Mathf.MoveTowards(Recoil, 0f, Time.deltaTime * 6f);
            if (m_Weapon != null) m_Weapon.Tick?.Invoke();
            UpdateGimmickHUD();
            UpdateHUD();
            if (m_Player)
            {
                UpdateJuice();
                m_Player.Invulnerable = m_InCutscene; // 컷신 중엔 맞지 않음
            }

            if (!m_Player || m_Player.Paused) return;
            UpdateQuiz();
            if (m_Player.InputBlocked) { m_Player.Aiming = false; return; }

            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;

            // 직업(무기)은 캐릭터 꾸미기에서 한 번 고르면 모험 중엔 바꿀 수 없다

            bool pressed = (locked && mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)
                           || Unity.FPS.Gameplay.VirtualInput.WasPressed("attack");
            bool held = (locked && mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.buttonWest.isPressed)
                        || Unity.FPS.Gameplay.VirtualInput.IsHeld("attack");

            // 총: 우클릭(게임패드 왼쪽 트리거)을 누르고 있으면 조준
            bool aimHeld = (locked && mouse != null && mouse.rightButton.isPressed) || (pad != null && pad.leftTrigger.isPressed)
                           || Unity.FPS.Gameplay.VirtualInput.IsHeld("aim");
            m_Player.Aiming = m_Weapon.Kind == WeaponKind.Gun && aimHeld && !m_InCutscene;
            UpdateCrosshair();
            if (m_Weapon.Kind == WeaponKind.Gun)
            {
                // 총: 누르고 있으면 연사
                if (held && m_Current == null && m_FireTimer <= 0f)
                {
                    m_FireTimer = m_Weapon.FireInterval / Stats.AttackSpeed;
                    StartAction(m_Weapon.Basic);
                    Punch(m_Weapon.Basic);
                }
            }
            else if (pressed)
            {
                if (m_Current == null) { StartAction(m_Weapon.Basic); Punch(m_Weapon.Basic); }
                else if (m_Combo > 0) m_Buffered = true; // 휘두르는 중에 누르면 다음 타로 이어짐
            }

            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) TryCast(0);
                if (kb.eKey.wasPressedThisFrame) TryCast(1);
                if (kb.xKey.wasPressedThisFrame) TryCast(2);
                if (kb.cKey.wasPressedThisFrame) TryCast(3);
            }
            for (int i = 0; i < 4; i++)
                if (Unity.FPS.Gameplay.VirtualInput.WasPressed("skill" + i)) TryCast(i);
            if (pad != null)
            {
                if (pad.leftShoulder.wasPressedThisFrame) TryCast(0);
                if (pad.rightShoulder.wasPressedThisFrame) TryCast(1);
                if (pad.buttonNorth.wasPressedThisFrame) TryCast(2);
                if (pad.rightTrigger.wasPressedThisFrame) TryCast(3);
            }

            if (m_Current == null && m_ComboWindow > 0f)
            {
                m_ComboWindow -= Time.deltaTime;
                if (m_ComboWindow <= 0f) m_Combo = 0;
            }

            if ((kb != null && kb.tabKey.wasPressedThisFrame) || Unity.FPS.Gameplay.VirtualInput.WasPressed("sheet")) CharacterSheet.Open(this);
        }

        void Equip(int index, bool announce)
        {
            index = Mathf.Clamp(index, 0, m_Weapons.Count - 1);
            if (m_Weapon != null && m_Weapon.Index == index && announce) return;
            m_Weapon = m_Weapons[index];
            WeaponCatalog.Selected = index;
            m_Combo = 0;
            if (m_Rig) m_Rig.Equip(index);
            RebuildSkillBar();
            if (announce)
            {
                ShowBanner($"<b>{m_Weapon.Name}</b>\n<size=45%><color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(C, Color.white, 0.4f))}>{m_Weapon.GimmickName} — {m_Weapon.GimmickInfo}</color></size>");
                if (m_Rig) Fx.Play("Sparks explode white", m_Rig.TipPosition, Quaternion.identity, 0.4f * S, 0.2f, false);
            }
        }

        void TryCast(int slot)
        {
            SkillDef skill = m_Weapon.Skills[slot];
            // 평타 도중이면 평타를 끊고 바로 스킬 (조작이 뻑뻑하지 않게)
            if (m_Current != null && m_CurrentIsBasic && skill.Remaining <= 0f) CancelAction();
            if (skill.Remaining > 0f || m_Current != null)
            {
                if (skill.Slot) skill.Slot.DOShakeAnchorPos(0.25f, 8f, 20).SetLink(skill.Slot.gameObject); // 아직 못 씀
                return;
            }
            skill.Remaining = skill.Cooldown;
            skill.WasReady = false;
            m_Combo = 0;
            Punch(skill);
            if (slot == 3)
            {
                // C = 궁극기: 컷신 → 스킬
                FaceAttack(12f);
                SetFrame();
                m_CurrentIsBasic = false;
                m_Current = StartCoroutine(Wrap(Ultimate(skill)));
                return;
            }
            StartAction(skill);
            CastFlair(skill);
            PostFx.Pulse(0.35f, C);
            ShowBanner($"<b>{skill.Name}</b>\n<size=42%><color=#BFE6FF>{(skill.Detail != null ? skill.Detail() : skill.Formula)}</color></size>");
        }

        void StartAction(SkillDef skill)
        {
            m_CurrentIsBasic = skill == m_Weapon.Basic;
            FaceAttack(m_Weapon.Kind == WeaponKind.Gun ? 14f : k_MeleeLock);
            SetFrame();
            m_Current = StartCoroutine(Wrap(skill.Run()));
        }

        void CancelAction()
        {
            if (m_Current != null) StopCoroutine(m_Current);
            m_Current = null;
            SwordActive = false;
            TrailOn = false;
            LocksMovement = false;
            OverridesMotion = false;
            BodyTwist = BodyLean = Crouch = 0f;
            m_Combo = 0;
        }

        /// <summary>
        /// 공격할 방향으로 몸을 돌린다 — 카메라가 아니라 캐릭터 기준:
        ///  타겟(앞쪽 ±100° 안, range 안, 벽에 가리지 않은 적 중 방향·거리가 가장 잘 맞는 적)이 있으면 그 적을,
        ///  없으면 캐릭터가 지금 보는 앞(이동 중이면 가는 방향)을 공격한다. 총은 우클릭 조준 중일 때만 화면 가운데.
        /// </summary>
        void FaceAttack(float range)
        {
            if (m_Weapon.Kind == WeaponKind.Gun && m_Player.Aiming)
            {
                m_Player.FaceCameraDirection();
                return;
            }
            TrainingDummy best = FindTarget(range);
            Vector3 dir = transform.forward;
            if (best)
            {
                Vector3 to = best.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f) dir = to.normalized;
            }
            else
            {
                Vector3 input = m_Player.MoveInputWorld;
                if (input.sqrMagnitude > 0.01f) dir = input.normalized; // 막 방향을 튼 순간에도 가는 쪽으로
            }
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>지금 공격하면 맞출 적 (없으면 null) — HUD 타겟 표시에도 쓴다</summary>
        TrainingDummy FindTarget(float range)
        {
            Vector3 face = transform.forward;
            Vector3 input = m_Player.MoveInputWorld;
            if (input.sqrMagnitude > 0.01f) face = input.normalized;
            face.y = 0f;

            TrainingDummy best = null;
            float bestScore = float.MaxValue;
            for (int pass = 0; pass < 2 && !best; pass++) // 적대 몹 먼저, 없으면 훈련장 허수아비
            {
                foreach (var d in Targets())
                {
                    if (pass == 0 && !d.Hostile) continue;
                    Vector3 to = d.transform.position - transform.position;
                    if (Mathf.Abs(to.y) > 2.5f * S) continue;
                    to.y = 0f;
                    float dist = Mathf.Max(0f, to.magnitude - d.Radius);
                    if (dist > range * S) continue;
                    float angle = to.sqrMagnitude > 0.0001f ? Vector3.Angle(face, to) : 0f;
                    // 아주 가까우면(1.2) 어느 쪽이든, 아니면 앞쪽 ±100°
                    if (angle > 100f && dist > 1.2f * S) continue;
                    if (Physics.Linecast(Chest, d.Center, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                    float score = dist / Mathf.Max(0.01f, range * S) + angle / 180f * 0.8f;
                    if (score < bestScore) { bestScore = score; best = d; }
                }
            }
            return best;
        }

        const float k_MeleeLock = 7f; // 근접: 이 거리 안의 적을 겨누고 파고든다

        /// <summary>평타 파고들기: 적이 평타 거리보다 멀면 최대 4.5까지 미끄러지듯 다가간다 (ease-out)</summary>
        IEnumerator StepIn(TrainingDummy t, float keep)
        {
            CharacterController cc = m_Player.Controller;
            Vector3 to = t.transform.position - transform.position;
            to.y = 0f;
            float gap = to.magnitude - t.Radius - keep;
            if (gap <= 0.05f * S || !cc || !cc.enabled) yield break;
            gap = Mathf.Min(gap, 4.5f * S);
            Vector3 dir = to.normalized;
            transform.rotation = Quaternion.LookRotation(dir);
            float time = Mathf.Lerp(0.07f, 0.17f, gap / (4.5f * S)) / Mathf.Max(0.5f, Stats.AttackSpeed);
            float done = 0f;
            TrailOn = true;
            BodyLean = 14f;
            for (float e = 0f; e < time; e += Time.deltaTime)
            {
                float want = gap * GeoFunctions.EaseOutCubic(Mathf.Clamp01(e / time));
                cc.Move(dir * (want - done) + Vector3.down * 0.03f * S);
                done = want;
                yield return null;
            }
            cc.Move(dir * (gap - done));
            BodyLean = 0f;
        }

        void SetFrame()
        {
            m_Origin = transform.position;
            m_Frame = transform.rotation;
        }

        IEnumerator Wrap(IEnumerator routine)
        {
            SwordActive = true;
            LocksMovement = true;
            yield return routine;
            SwordActive = false;
            TrailOn = false;
            LocksMovement = false;
            OverridesMotion = false;
            BodyTwist = BodyLean = Crouch = 0f;
            m_Current = null;
        }

        // 평타 1·2·3단: 휘두르는 중에 누르거나, 끝난 뒤 0.6초 안에 다시 누르면 다음 단으로. 3단 뒤엔 처음부터.
        IEnumerator Combo(int count, Func<int, IEnumerator> hit)
        {
            if (m_ComboWindow <= 0f || m_Combo >= count) m_Combo = 0;
            m_ComboWindow = 0f;
            while (true)
            {
                m_Buffered = false;
                FaceAttack(m_Weapon.Kind == WeaponKind.Gun ? 14f : k_MeleeLock);
                if (m_Weapon.Kind != WeaponKind.Gun)
                {
                    TrainingDummy target = FindTarget(k_MeleeLock);
                    if (target) yield return StepIn(target, m_Weapon.BasicRange * S * 0.55f);
                }
                SetFrame();
                int step = m_Combo % count;
                m_Combo++;
                yield return hit(step);
                if (!m_Buffered || m_Combo >= count) break;
            }
            m_ComboWindow = 0.6f;
            if (m_Combo >= count) { m_Combo = 0; m_ComboWindow = 0f; }
        }

        // ================================================================== 좌표·그리기 도구

        Vector3 World(Vector2 p, float height = 0f) => m_Origin + m_Frame * new Vector3(p.y, height, p.x);

        Vector2 Local(Vector3 world)
        {
            Vector3 d = Quaternion.Inverse(m_Frame) * (world - m_Origin);
            return new Vector2(d.z, d.x);
        }

        Vector3 OnGround(Vector2 p) { Vector3 w = World(p); w.y = GeoFx.GroundY(w, S); return w; }

        /// <summary>
        /// 몹 몸통(원기둥)에서 나(원점)에게 가장 가까운 점 — 큰 몹(거인·보스)은 몸이 두꺼워서
        /// 중심이 범위 밖이어도 몸이 닿으면 맞는다
        /// </summary>
        Vector2 LocalNear(TrainingDummy d)
        {
            Vector2 p = Local(d.transform.position);
            float m = p.magnitude;
            if (m < 0.0001f) return p;
            return p * Mathf.Max(0f, m - d.Radius) / m;
        }

        Quaternion Facing(float degrees = 0f) => m_Frame * Quaternion.Euler(0f, degrees, 0f);

        /// <summary>함수 f(u) (u: 0→1, 스킬 좌표계)의 그래프를 땅 위에 그린다</summary>
        FadingLine Draw(Func<float, Vector2> f, int samples, float width, Color color, float reveal, float life)
        {
            return GeoFx.Curve(u => World(f(u)), samples, width * S, color, reveal, life, true, S);
        }

        // ================================================================== 맞히기 도구

        static List<TrainingDummy> Targets()
        {
            var list = new List<TrainingDummy>();
            foreach (var d in TrainingDummy.All) if (d.Alive) list.Add(d);
            return list;
        }

        void Damage(TrainingDummy d, float amount, bool big, string hitFx = null, float fxSize = 0.45f)
        {
            // 피해 = 기본 × 직업 공격력 × 랜덤 × (치명타면 직업 치명 피해) × 기믹 × 헤드샷 × 궁극기 × 문제 보상
            // 치명타 확률 = 직업 치명 확률 (+15% 큰 공격), 궁극기 중엔 무조건
            m_HitTag = null;
            var st = Stats;
            float gimmick = 1f;
            if (m_Bonus > 0f) { gimmick = m_Bonus; m_HitTag = m_BonusTag; } // 연계 피해 (기믹 대신)
            else if (m_Weapon.HitMultiplier != null) gimmick = m_Weapon.HitMultiplier(d);
            float head = 1f;
            if (m_Headshot) { head = st.Headshot; m_HitTag ??= "헤드샷!"; }
            bool crit = m_Ultimate || gimmick > 1.01f || UnityEngine.Random.value < st.Crit + (big ? 0.15f : 0f);
            float dmg = amount * k_DamageScale * st.Attack * UnityEngine.Random.Range(0.9f, 1.1f) * (crit ? st.CritDamage : 1f) * gimmick * head
                        * (m_Ultimate ? 2f : 1f) * (PowerBuff ? 2f : 1f);
            d.Hit(dmg, transform.position, big, crit, m_HitTag);
            m_HitTag = null;
            if (hitFx != null) Fx.Play(hitFx, d.Center, Quaternion.identity, fxSize * S * (big ? 1.4f : 1f), 0.3f, false);
            OnImpact(d, big, crit);
        }

        // 연계 피해: 상태(빙결·균열·표식·약점)를 터뜨리는 스킬이 쓴다 — 배율과 말을 붙여 한 번 때림
        float m_Bonus;
        string m_BonusTag;
        void DamageBonus(TrainingDummy d, float amount, bool big, string hitFx, float bonus, string tag)
        {
            m_Bonus = bonus;
            m_BonusTag = tag;
            Damage(d, amount, big, hitFx, 0.6f);
            m_Bonus = 0f;
            m_BonusTag = null;
            if (bonus >= 2.5f)
            {
                PostFx.Pulse(0.6f, C);
                RingWave.Spawn(d.transform.position + Vector3.up * 0.05f * S, 1.6f * S * d.Scale, new Color(1f, 1f, 1f, 0.8f), 0.35f);
                LightFlash.Spawn(d.Center, C, 6f, 3f * S, 0.2f);
            }
        }

        /// <summary>스킬 좌표계에서 inside(p)가 참인 허수아비를 모두 때린다</summary>
        int HitWhere(Func<Vector2, bool> inside, float amount, bool big, string hitFx = null)
        {
            int count = 0;
            foreach (var d in Targets())
                if (inside(LocalNear(d))) { Damage(d, amount, big, hitFx); count++; }
            return count;
        }

        int HitNear(Vector3 world, float radius, float amount, bool big, string hitFx = null)
        {
            int count = 0;
            foreach (var d in Targets())
            {
                Vector3 diff = d.transform.position - world;
                if (Mathf.Abs(diff.y) > d.Height + 1f * S) continue;
                diff.y = 0f;
                if (diff.magnitude <= radius + d.Radius) { Damage(d, amount, big, hitFx); count++; }
            }
            return count;
        }

        /// <summary>
        /// 곡선 f를 travel초 동안 앞에서부터 그리며, 곡선 머리가 지나간 자리에서 radius 안의 허수아비를 벤다.
        /// alongFx: 곡선을 따라 일정 간격(alongEvery, u 단위)으로 소환할 이펙트.
        /// </summary>
        IEnumerator Sweep(Func<float, Vector2> f, float travel, float radius, float amount, bool big, string hitFx,
            string alongFx = null, float alongEvery = 0.25f, float alongSize = 0.5f)
        {
            const int samples = 60;
            var pts = new Vector2[samples];
            for (int i = 0; i < samples; i++) pts[i] = f(i / (samples - 1f));

            var done = new HashSet<TrainingDummy>();
            float nextFx = 0f;
            bool labeled = false;
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float head = Mathf.Clamp01(t / travel);
                int last = Mathf.Clamp(Mathf.CeilToInt(head * (samples - 1)), 0, samples - 1);

                while (alongFx != null && nextFx <= head + 0.0001f)
                {
                    Fx.Play(alongFx, OnGround(f(nextFx)), Facing(), alongSize * S, 0.3f, !labeled);
                    labeled = true;
                    nextFx += alongEvery;
                }

                foreach (var d in Targets())
                {
                    if (done.Contains(d)) continue;
                    Vector2 p = Local(d.transform.position);
                    float rr = radius + d.Radius;
                    for (int i = 0; i <= last; i++)
                    {
                        if ((pts[i] - p).sqrMagnitude <= rr * rr)
                        {
                            done.Add(d);
                            Damage(d, amount, big, hitFx);
                            break;
                        }
                    }
                }

                if (head >= 1f) break;
                yield return null;
            }
        }

        // ================================================================== 몸동작 도구 (근접)

        /// <summary>칼날 방향 함수 dir(u)를 따라 휘두르고, hitAt에 onHit을 한 번 부른다</summary>
        IEnumerator Swing(Func<float, Vector3> dir, Vector3 up, float windup, float duration, float recover, float hitAt, Action onHit, float reach = 1f, float lunge = 0.35f)
        {
            // 직업 공격 속도만큼 빠르게
            float speed = Mathf.Max(0.3f, Stats.AttackSpeed);
            windup /= speed;
            duration /= speed;
            recover /= speed;
            BladeUpLocal = up;
            Reach = reach;
            bool vertical = Mathf.Abs(up.x) > 0.5f; // 세로 휘두르기 (내려찍기)

            // 1) 예비 동작: 휘두를 방향의 반대쪽으로 조금 더 당긴다 (dir(−0.15)), 내려찍기면 몸을 젖힌다
            Vector3 from = BladeDirLocal;
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                float u = GeoFunctions.EaseOutCubic(t / windup);
                BladeDirLocal = Vector3.Slerp(from, dir(-0.15f), u).normalized;
                if (vertical) BodyLean = Mathf.Lerp(0f, -10f, u);
                yield return null;
            }

            // 2) 휘두르기: 빠르게 + 앞으로 한 걸음 (lunge)
            TrailOn = true;
            bool hit = false;
            CharacterController cc = m_Player.Controller;
            Vector3 forward = transform.forward;
            float stepped = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = GeoFunctions.EaseOutCubic(t / duration);
                BladeDirLocal = dir(u);
                if (vertical) { BodyLean = Mathf.Lerp(-10f, 24f, u); Crouch = u; }
                if (lunge > 0f && !OverridesMotion && cc && cc.enabled)
                {
                    float want = lunge * S * u;
                    cc.Move(forward * (want - stepped) + Vector3.down * 0.02f * S);
                    stepped = want;
                }
                if (!hit && t / duration >= hitAt) { hit = true; SwingSlash(dir, vertical); onHit?.Invoke(); }
                yield return null;
            }
            BladeDirLocal = dir(1f);
            if (!hit) { SwingSlash(dir, vertical); onHit?.Invoke(); }

            // 3) 마무리 동작: 조금 더 흘러갔다가(dir(1.1)) 천천히 돌아온다
            Vector3 end = dir(1.1f);
            for (float t = 0f; t < recover; t += Time.deltaTime)
            {
                float u = t / recover;
                BladeDirLocal = Vector3.Slerp(dir(1f), end, GeoFunctions.EaseOutCubic(u)).normalized;
                if (vertical) { BodyLean = Mathf.Lerp(24f, 8f, u); Crouch = 1f - u; }
                if (u > 0.4f) TrailOn = false;
                if (m_Buffered && u > 0.25f) break; // 다음 타를 눌렀으면 마무리 동작을 줄이고 바로 이어감
                yield return null;
            }
            TrailOn = false;
            if (vertical) { BodyLean = 0f; Crouch = 0f; }
        }

        // 휘두르는 순간 칼날이 지나간 자리에 베기 이펙트 (직업 색) — 휘두른 면에 맞춰 기울임
        void SwingSlash(Func<float, Vector3> dir, bool vertical)
        {
            string fx = Th.Slash;
            if (string.IsNullOrEmpty(fx) || m_Weapon.Kind == WeaponKind.Gun) return;
            Vector3 a = transform.TransformDirection(dir(0f)), b = transform.TransformDirection(dir(1f));
            Vector3 normal = Vector3.Cross(a, b);
            if (normal.sqrMagnitude < 0.0001f) normal = vertical ? transform.right : Vector3.up;
            Vector3 mid = (a + b).normalized;
            if (mid.sqrMagnitude < 0.01f) mid = transform.forward;
            Vector3 pos = Chest + mid * 0.9f * S;
            Fx.Play(fx, pos, Quaternion.LookRotation(transform.forward, normal.normalized), 0.3f * S, 0.12f, false);
        }

        // 수평 휘두르기: 칼 방향 = 부채꼴 P(θ) 방향 (θ: 앞에서 오른쪽으로 잰 각)
        static Func<float, Vector3> HorizontalArc(float fromDeg, float toDeg, float tilt = -0.12f)
        {
            return u =>
            {
                Vector2 p = GeoFunctions.Arc(1f, Mathf.LerpUnclamped(fromDeg, toDeg, u));
                return new Vector3(p.y, tilt, p.x).normalized;
            };
        }

        // 세로 휘두르기: 칼 방향 = 세로 원 P(φ) = (sinφ, cosφ)
        static Func<float, Vector3> VerticalArc(float fromDeg, float toDeg, float side = 0.12f)
        {
            return u =>
            {
                Vector2 p = GeoFunctions.VerticalArc(1f, Mathf.LerpUnclamped(fromDeg, toDeg, u));
                return new Vector3(side, p.y, p.x).normalized;
            };
        }

        // 찌르기: 뒤로 당겼다가 앞으로
        static Func<float, Vector3> Thrust(float side = 0.05f) => u => new Vector3(side, -0.05f, 1f).normalized;

        void Pose(Vector3 dirLocal, Vector3 up, float reach = 1f)
        {
            BladeDirLocal = dirLocal.normalized;
            BladeUpLocal = up;
            Reach = reach;
        }

        // ================================================================== 총 도구

        /// <summary>총을 겨눈다 (각도 degrees: 앞에서 오른쪽으로)</summary>
        IEnumerator Aim(float degrees = 0f, float time = 0.06f)
        {
            Vector2 d = GeoFunctions.LineDir(degrees);
            Pose(new Vector3(d.y, 0.03f, d.x), Vector3.up, 0.95f);
            if (time > 0f) yield return new WaitForSeconds(time);
        }

        Vector3 Muzzle => m_Rig ? m_Rig.TipPosition : transform.position + Vector3.up * 1.3f * S;

        /// <summary>
        /// 총알 하나: 총구에서 각도 degrees 방향으로 직선 y = (tanθ)x 를 따라 length만큼.
        /// pierce면 선 위의 모두를, 아니면 가장 가까운 하나를 맞힌다. 맞힌 곳(또는 끝점)을 돌려준다.
        /// </summary>
        // 총알이 맞힌 적마다: m_ShotOverride가 true를 돌려주면 기본 피해 대신 그쪽이 처리, m_ShotHook은 맞힌 뒤 불림
        Func<TrainingDummy, bool> m_ShotOverride;
        Action<TrainingDummy> m_ShotHook;

        Vector3 Shoot(float degrees, float length, float corridor, float amount, bool big, string hitFx, bool pierce, Color color, float width = 0.05f)
        {
            Vector3 from = Muzzle;
            // 조준점(화면 가운데)을 향해 쏜다: 앞쪽 탄은 위아래(피치)까지, 옆으로 많이 벌어진 탄은 수평으로
            Vector3 aimDir = AimDirection(from);
            if (Mathf.Abs(degrees) > 45f) { aimDir.y = 0f; aimDir.Normalize(); }
            Vector3 worldDir = Quaternion.AngleAxis(degrees, Vector3.up) * aimDir;
            if (m_Player.Aiming) length *= 1.5f; // 조준하면 더 멀리
            float reach = length;
            m_CrosshairKick = Mathf.Min(1f, m_CrosshairKick + 0.35f);
            Recoil = Mathf.Min(1f, Recoil + 0.6f);

            // 벽에 막히면 거기까지
            if (Physics.Raycast(from, worldDir, out RaycastHit wall, length, ~(1 << 2), QueryTriggerInteraction.Ignore))
                reach = wall.distance;

            var hits = new List<(TrainingDummy d, float along)>();
            foreach (var d in Targets())
            {
                // 몸 = 발끝부터 머리까지의 캡슐 (거인·보스는 크고 두껍다)
                if (!d.RayHitsBody(new Ray(from, worldDir), corridor, out float along)) continue;
                if (along < 0f || along > reach) continue;
                hits.Add((d, along));
            }
            hits.Sort((a, b) => a.along.CompareTo(b.along));

            Vector3 end = from + worldDir * reach;
            if (hits.Count > 0 && !pierce)
            {
                end = from + worldDir * hits[0].along;
                // 머리 명중 (조준 사격일수록 정확)
                m_Headshot = hits[0].d.RayHitsHead(new Ray(from, worldDir), m_Player.Aiming ? 0.05f * S : 0f);
                if (m_ShotOverride == null || !m_ShotOverride(hits[0].d)) Damage(hits[0].d, amount, big, hitFx);
                m_ShotHook?.Invoke(hits[0].d);
                m_Headshot = false;
            }
            else
            {
                foreach (var h in hits)
                {
                    if (m_ShotOverride == null || !m_ShotOverride(h.d)) Damage(h.d, amount, big, hitFx);
                    m_ShotHook?.Invoke(h.d);
                }
                if (hitFx != null && hits.Count == 0) Fx.Play(hitFx, end, Quaternion.identity, 0.35f * S, 0.2f, false);
            }

            GeoFx.Line(new[] { from, end }, width * S, color, 0f, 0.18f);
            return end;
        }

        void MuzzleFlash(string fx, float size = 0.35f)
        {
            Fx.Play(fx, Muzzle, Quaternion.LookRotation(m_Frame * Vector3.forward), size * S, 0.08f, false);
        }

        // ================================================================== 허수아비

        void SpawnDummies()
        {
            Vector3 origin = transform.position;
            Quaternion facing = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            float[] angles = { -50f, -25f, 0f, 25f, 50f };
            foreach (float angle in angles)
            {
                Vector2 p = GeoFunctions.Arc(6f * S, angle);
                Vector3 pos = origin + facing * new Vector3(p.y, 0f, p.x);
                pos.y = GeoFx.GroundY(pos, S);
                Vector3 look = origin - pos;
                look.y = 0f;
                MobKind kind = angle == 0f ? MobKind.Brute : (Mathf.Abs(angle) == 25f ? MobKind.Hopper : MobKind.Dummy);
                TrainingDummy.Create(pos, Quaternion.LookRotation(look), S, kind);
            }
        }

        // ================================================================== 숫자 보기 좋게

        string N(float v) => v.ToString(Mathf.Abs(v) >= 10f ? "0" : "0.##");
    }
}
