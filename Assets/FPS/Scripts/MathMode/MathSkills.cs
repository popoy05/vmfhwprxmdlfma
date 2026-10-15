using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Unity.FPS.MathMode
{
    // 수학 스킬 4종 (Z / X / C / V, 모바일은 화면 버튼). 쿨타임은 난이도에 따라 줄어든다.
    //  Z 원형 충격파  x² + y² = r²   : 주변 반지름 8m 원 안의 적에게 피해 + 밀쳐내기
    //  X 벡터 대시    v = (a, b)      : 이동 방향으로 8m 순간 돌진, 돌진 중 무적
    //  C 좌표 포격    P(x, y)         : 조준한 지점에 1초 뒤 빛기둥 폭격 (반지름 5m, 거리에 따라 선형 감소)
    //  V 적분 회복    ∫10dt [0,3]=30  : 3초 동안 초당 10 회복
    public class MathSkills : MonoBehaviour
    {
        public class Skill
        {
            public string Key, VirtualId, Name, Formula;
            public UnityEngine.InputSystem.Key KeyCode;
            public Color Color;
            public float Cooldown;
            public float ReadyTime;
            public float Remaining => Mathf.Max(0f, ReadyTime - Time.time);
            public bool Ready => Time.time >= ReadyTime;
        }

        public readonly Skill Shockwave = new Skill { Key = "Z", KeyCode = UnityEngine.InputSystem.Key.Z, VirtualId = "skillZ", Name = "원형 충격파", Formula = "x²+y²=8²", Cooldown = 12f, Color = new Color(0.4f, 0.9f, 1f) };
        public readonly Skill Dash = new Skill { Key = "X", KeyCode = UnityEngine.InputSystem.Key.X, VirtualId = "skillX", Name = "벡터 대시", Formula = "|v| = 8m", Cooldown = 7f, Color = new Color(1f, 0.85f, 0.3f) };
        public readonly Skill Strike = new Skill { Key = "C", KeyCode = UnityEngine.InputSystem.Key.C, VirtualId = "skillC", Name = "좌표 포격", Formula = "P(x, y)", Cooldown = 18f, Color = new Color(1f, 0.45f, 0.35f) };
        public readonly Skill Heal = new Skill { Key = "V", KeyCode = UnityEngine.InputSystem.Key.V, VirtualId = "skillV", Name = "적분 회복", Formula = "∫10dt [0,3] = 30", Cooldown = 25f, Color = new Color(0.4f, 1f, 0.5f) };
        public bool HealDisabled; // 극한 난이도: 회복 스킬 없음
        public Skill[] All => HealDisabled ? new[] { Shockwave, Dash, Strike } : new[] { Shockwave, Dash, Strike, Heal };

        MathModeManager m_Manager;
        PlayerInputHandler m_Input;
        PlayerCharacterController m_Controller;
        CharacterController m_CharacterController;
        Health m_Health;
        Camera m_Camera;

        public void Init(MathModeManager manager)
        {
            m_Manager = manager;
            m_Input = GetComponent<PlayerInputHandler>();
            m_Controller = GetComponent<PlayerCharacterController>();
            m_CharacterController = GetComponent<CharacterController>();
            m_Health = GetComponent<Health>();
            m_Camera = m_Controller ? m_Controller.PlayerCamera : Camera.main;
        }

        void Update()
        {
            if (!m_Input || !m_Input.CanProcessInput()) return;

            foreach (Skill skill in All)
                if (MathInput.Pressed(skill.KeyCode, skill.VirtualId))
                    Cast(skill, false);
        }

        // 스킬 사용. auto = 자동 스킬(MathAssist)이 쓴 경우 (실패해도 메시지를 띄우지 않는다)
        public bool Cast(Skill skill, bool auto)
        {
            if (skill == Heal && HealDisabled)
            {
                if (!auto) MathModeManager.ShowMessage("극한 난이도에서는 회복할 수 없습니다");
                return false;
            }

            if (skill == Heal && m_Health && m_Health.CurrentHealth >= m_Health.MaxHealth)
            {
                if (!auto) MathModeManager.ShowMessage("체력이 가득 차 있어 적분 회복을 쓸 필요가 없습니다");
                return false;
            }

            if (!skill.Ready)
            {
                if (!auto) MathModeManager.ShowMessage($"{skill.Name} 재사용 대기 {skill.Remaining:0.0}초");
                return false;
            }

            skill.ReadyTime = Time.time + skill.Cooldown;
            if (skill == Shockwave) StartCoroutine(ShockwaveRoutine());
            else if (skill == Dash) StartCoroutine(DashRoutine());
            else if (skill == Strike) StartCoroutine(StrikeRoutine());
            else StartCoroutine(HealRoutine());
            return true;
        }

        CameraFX FX => m_Manager.CameraFX;

        // ------------------------------------------------------------------ Z: 원형 충격파

        IEnumerator ShockwaveRoutine()
        {
            const float radius = 8f, maxDamage = 45f, minDamage = 15f;
            Vector3 center = transform.position + Vector3.up * 0.15f;

            // 피해: 중심 45 → 가장자리 15 (거리에 대한 일차함수) + 바깥쪽으로 밀쳐내기
            int hits = MathCombat.DamageInRadius(center + Vector3.up, radius,
                d => Mathf.Lerp(maxDamage, minDamage, d / radius), gameObject);
            KnockBack(center, radius, 6f);
            MathModeManager.ShowMessage($"원형 충격파 x² + y² = {radius * radius:0}  명중 {hits}");
            if (FX)
            {
                FX.Shake(0.8f, 0.35f);
                FX.Flash(new Color(0.4f, 0.9f, 1f, 0.25f), 0.25f);
            }

            // 바닥 번쩍임 + 두 겹의 고리 + 위로 튀는 빛 조각
            var flash = m_Manager.CreateBlock(null, PrimitiveType.Cylinder, center, new Vector3(1f, 0.01f, 1f), new Color(0.5f, 1.4f, 2f));
            LineRenderer ring1 = Ring(new Color(0.5f, 1.2f, 2f), 0.45f);
            LineRenderer ring2 = Ring(new Color(1.6f, 1.6f, 1.6f), 0.15f);
            var shards = new List<(Transform t, Vector3 v)>();
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var shard = m_Manager.CreateBlock(null, PrimitiveType.Cube, center + dir * 0.5f, Vector3.one * 0.18f, new Color(0.6f, 1.4f, 2f));
                shards.Add((shard.transform, dir * 14f + Vector3.up * Random.Range(3f, 6f)));
            }

            const float duration = 0.5f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = t / duration;
                float r1 = radius * (1f - Mathf.Pow(1f - u, 3f));
                float r2 = radius * Mathf.Clamp01((u - 0.15f) / 0.85f);
                SetCircle(ring1, center, r1);
                SetCircle(ring2, center + Vector3.up * 0.05f, r2);
                ring1.widthMultiplier = 0.45f * (1f - u) + 0.05f;
                flash.transform.localScale = new Vector3(r1 * 2f, 0.01f, r1 * 2f);
                flash.GetComponent<Renderer>().material.SetColor("_BaseColor", new Color(0.5f, 1.4f, 2f) * (1f - u) * 0.6f);
                foreach (var (tr, v) in shards)
                {
                    tr.position += (v + Vector3.down * 20f * t) * Time.deltaTime;
                    tr.Rotate(400f * Time.deltaTime, 300f * Time.deltaTime, 0f);
                    tr.localScale = Vector3.one * 0.18f * (1f - u);
                }

                yield return null;
            }

            Destroy(ring1.gameObject);
            Destroy(ring2.gameObject);
            Destroy(flash);
            foreach (var (tr, _) in shards) Destroy(tr.gameObject);
        }

        // 적을 원의 중심에서 바깥쪽으로 밀어낸다 (NavMesh 위에서만 움직이도록 agent.Move 사용)
        void KnockBack(Vector3 center, float radius, float distance)
        {
            foreach (Collider col in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var enemy = col.GetComponentInParent<EnemyController>();
                if (!enemy) continue;
                var agent = enemy.GetComponent<NavMeshAgent>();
                if (!agent || !agent.isOnNavMesh) continue;
                Vector3 dir = Vector3.ProjectOnPlane(enemy.transform.position - center, Vector3.up).normalized;
                StartCoroutine(Push(agent, dir * distance, 0.25f));
            }
        }

        static IEnumerator Push(NavMeshAgent agent, Vector3 offset, float duration)
        {
            for (float t = 0f; t < duration && agent && agent.isOnNavMesh; t += Time.deltaTime)
            {
                agent.Move(offset * (Time.deltaTime / duration) * (1.5f - t / duration));
                yield return null;
            }
        }

        // ------------------------------------------------------------------ X: 벡터 대시

        IEnumerator DashRoutine()
        {
            const float distance = 8f, duration = 0.18f;
            Vector3 move = m_Input.GetMoveInput();
            Vector3 dir = transform.TransformDirection(move);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            dir.Normalize();

            Vector2 v = new Vector2(dir.x, dir.z) * (distance / m_Manager.UnitSize);
            MathModeManager.ShowMessage($"벡터 대시 v = ({v.x:0.0}, {v.y:0.0})  |v| = {v.magnitude:0.0}칸");
            if (FX)
            {
                FX.FovKick(14f, 0.4f);
                FX.Flash(new Color(1f, 0.9f, 0.5f, 0.15f), 0.2f);
            }

            // 바닥에 이동 벡터 화살표
            Vector3 start = transform.position + Vector3.up * 0.1f;
            Vector3 end = start + dir * distance;
            var arrow = m_Manager.CreateLine(null, new Color(1.6f, 1.3f, 0.4f), 0.2f);
            Vector3 side = Vector3.Cross(Vector3.up, dir) * 0.8f;
            arrow.positionCount = 5;
            arrow.SetPositions(new[] { start, end, end - dir * 1.2f + side, end, end - dir * 1.2f - side });

            var ghosts = new List<GameObject>();
            if (m_Health) m_Health.Invincible = true;
            int ghostIndex = 0;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (m_CharacterController && m_CharacterController.enabled)
                    m_CharacterController.Move(dir * (distance / duration) * Time.deltaTime);
                // 잔상
                if (t > ghostIndex * duration / 4f)
                {
                    ghostIndex++;
                    ghosts.Add(m_Manager.CreateBlock(null, PrimitiveType.Capsule, transform.position + Vector3.up, new Vector3(0.7f, 0.9f, 0.7f), new Color(1.2f, 1f, 0.4f)));
                }

                yield return null;
            }

            if (m_Health) m_Health.Invincible = false;

            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                float k = 1f - t / 0.4f;
                foreach (GameObject g in ghosts) g.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f) * k;
                arrow.widthMultiplier = 0.2f * k;
                yield return null;
            }

            foreach (GameObject g in ghosts) Destroy(g);
            Destroy(arrow.gameObject);
        }

        // ------------------------------------------------------------------ C: 좌표 포격

        IEnumerator StrikeRoutine()
        {
            const float radius = 5f, maxDamage = 90f, delay = 1f;
            Transform eye = m_Camera.transform;
            Vector3 target = Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : eye.position + eye.forward * 80f;

            Vector2 p = m_Manager.World.ToPlane(target);
            MathModeManager.ShowMessage($"좌표 포격 지정: ({p.x:0.0}, {p.y:0.0})");

            // 표적: 회전하는 원 + 십자선 + 좌표/카운트다운 글씨
            var marker = new GameObject("StrikeMarker").transform;
            marker.position = target;
            LineRenderer ring = m_Manager.CreateLine(marker, new Color(1.6f, 0.5f, 0.4f), 0.12f);
            ring.loop = true;
            SetCircle(ring, target + Vector3.up * 0.1f, radius);
            LineRenderer inner = m_Manager.CreateLine(marker, new Color(1.6f, 0.8f, 0.4f), 0.08f);
            inner.loop = true;
            var cross = m_Manager.CreateLine(marker, new Color(1.6f, 0.6f, 0.4f), 0.06f);
            cross.positionCount = 5;
            var label = m_Manager.CreateText(marker, Vector3.up * 2.5f, new Vector2(6f, 1f), "", new Color(1f, 0.6f, 0.45f), 3f);
            label.outlineWidth = 0.2f;

            for (float t = 0f; t < delay; t += Time.deltaTime)
            {
                float u = t / delay;
                SetCircle(inner, target + Vector3.up * 0.12f, radius * (1f - u));
                float a = t * 3f;
                Vector3 dx = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius, dz = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                Vector3 c = target + Vector3.up * 0.12f;
                cross.SetPositions(new[] { c - dx, c + dx, c, c - dz, c + dz });
                label.text = $"({p.x:0.0}, {p.y:0.0})\n<size=140%>{delay - t:0.0}</size>";
                label.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(target - eye.position, Vector3.up));
                yield return null;
            }

            // 하늘에서 내려꽂히는 빛기둥
            var beam = m_Manager.CreateBlock(null, PrimitiveType.Cylinder, target + Vector3.up * 30f, new Vector3(1.5f, 30f, 1.5f), new Color(2f, 1.2f, 0.8f));
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                beam.transform.localScale = new Vector3(1.5f + t * 20f, 30f, 1.5f + t * 20f);
                yield return null;
            }

            // 피해: 중심 90 → 가장자리 0 (선형 감소 f(d) = 90(1 - d/r))
            int hits = MathCombat.DamageInRadius(target, radius, d => maxDamage * (1f - d / radius), gameObject);
            if (m_Manager.ExplosionVfx)
                Destroy(Instantiate(m_Manager.ExplosionVfx, target, Quaternion.identity), 3f);
            KnockBack(target, radius, 4f);
            float near = Mathf.Clamp01(1f - Vector3.Distance(transform.position, target) / 40f);
            if (FX) FX.Shake(0.4f + near * 0.8f, 0.4f);
            MathModeManager.ShowMessage($"포격 명중 {hits}");
            Destroy(marker.gameObject);

            LineRenderer wave = Ring(new Color(2f, 1.2f, 0.8f), 0.3f);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                float u = t / 0.4f;
                SetCircle(wave, target + Vector3.up * 0.2f, radius * 1.4f * u);
                beam.transform.localScale = new Vector3(4.5f * (1f - u), 30f, 4.5f * (1f - u));
                yield return null;
            }

            Destroy(beam);
            Destroy(wave.gameObject);
        }

        // ------------------------------------------------------------------ V: 적분 회복

        IEnumerator HealRoutine()
        {
            MathModeManager.ShowMessage("적분 회복: 0초~3초 동안 ∫10 dt = 30 회복");
            if (FX) FX.Flash(new Color(0.3f, 1f, 0.4f, 0.2f), 0.5f);

            // 몸을 감싸며 올라가는 초록 이중 나선
            LineRenderer helixA = m_Manager.CreateLine(null, new Color(0.5f, 1.8f, 0.7f), 0.06f);
            LineRenderer helixB = m_Manager.CreateLine(null, new Color(1.2f, 1.8f, 0.6f), 0.06f);
            helixA.positionCount = helixB.positionCount = 40;
            float nextNumber = 0f;

            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                if (m_Health) m_Health.Heal(10f * Time.deltaTime);

                Vector3 basePos = transform.position;
                for (int i = 0; i < 40; i++)
                {
                    float h = i / 39f * 2.2f;
                    float a = h * 5f + t * 6f;
                    helixA.SetPosition(i, basePos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.8f + Vector3.up * h);
                    helixB.SetPosition(i, basePos + new Vector3(Mathf.Cos(a + Mathf.PI), 0f, Mathf.Sin(a + Mathf.PI)) * 0.8f + Vector3.up * h);
                }

                float fade = t > 2.6f ? (3f - t) / 0.4f : 1f;
                helixA.widthMultiplier = helixB.widthMultiplier = 0.06f * fade;

                if (t >= nextNumber)
                {
                    nextNumber += 1f;
                    StartCoroutine(FloatingText(basePos + Vector3.up * 2.2f + Random.insideUnitSphere * 0.4f, "+10", new Color(0.5f, 1f, 0.6f)));
                }

                yield return null;
            }

            Destroy(helixA.gameObject);
            Destroy(helixB.gameObject);
        }

        IEnumerator FloatingText(Vector3 position, string text, Color color)
        {
            var root = new GameObject("FloatingText").transform;
            root.position = position;
            var tmp = m_Manager.CreateText(root, Vector3.zero, new Vector2(2f, 0.6f), $"<b>{text}</b>", color, 4f);
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                root.position += Vector3.up * Time.deltaTime;
                root.rotation = Quaternion.LookRotation(root.position - m_Camera.transform.position);
                tmp.alpha = 1f - t;
                yield return null;
            }

            Destroy(root.gameObject);
        }

        // ------------------------------------------------------------------ 도우미

        LineRenderer Ring(Color color, float width)
        {
            LineRenderer ring = m_Manager.CreateLine(null, color, width);
            ring.loop = true;
            ring.positionCount = 48;
            return ring;
        }

        static void SetCircle(LineRenderer ring, Vector3 center, float r)
        {
            if (ring.positionCount != 48) ring.positionCount = 48;
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
        }
    }

    // 화면 아래 가운데의 스킬 바 (PC). 모바일은 화면 버튼에 쿨타임이 표시된다.
    public class MathSkillBar : MonoBehaviour
    {
        readonly List<(MathSkills.Skill skill, RectTransform cover, TextMeshProUGUI timer)> m_Slots =
            new List<(MathSkills.Skill, RectTransform, TextMeshProUGUI)>();
        MathUltimate m_Ultimate;
        RectTransform m_UltFill;
        TextMeshProUGUI m_UltText;
        Image m_UltFrame;

        public void Init(MathSkills skills, MathUltimate ultimate, Transform canvas, TMP_FontAsset font)
        {
            m_Ultimate = ultimate;
            var bar = new GameObject("SkillBar", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(canvas, false);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 20f);
            bar.sizeDelta = new Vector2(5 * 150f, 84f);

            MathSkills.Skill[] all = skills.All;
            for (int i = 0; i < all.Length; i++)
            {
                var slot = UIUtil.Image(bar, "Skill", new Color(0.05f, 0.08f, 0.14f, 0.85f));
                slot.anchorMin = slot.anchorMax = new Vector2(0f, 0.5f);
                slot.pivot = new Vector2(0f, 0.5f);
                slot.sizeDelta = new Vector2(142f, 80f);
                slot.anchoredPosition = new Vector2(i * 150f + 4f, 0f);
                var strip = UIUtil.Image(slot, "Color", all[i].Color);
                strip.anchorMin = new Vector2(0f, 1f);
                strip.anchorMax = new Vector2(1f, 1f);
                strip.pivot = new Vector2(0.5f, 1f);
                strip.sizeDelta = new Vector2(0f, 4f);

                var text = UIUtil.Text(slot, font, $"<b><color=#FFD54A>[{all[i].Key}]</color> {all[i].Name}</b>\n<size=75%>{all[i].Formula}  ·  {all[i].Cooldown:0}s</size>", 18f, TextAlignmentOptions.Center);
                UIUtil.Stretch(text.rectTransform, 4f);

                var cover = UIUtil.Image(slot, "Cooldown", new Color(0f, 0f, 0f, 0.65f));
                cover.anchorMin = new Vector2(0f, 0f);
                cover.anchorMax = new Vector2(1f, 0f);
                cover.pivot = new Vector2(0.5f, 0f);
                cover.sizeDelta = new Vector2(0f, 0f);

                var timer = UIUtil.Text(slot, font, "", 30f, TextAlignmentOptions.Center);
                UIUtil.Stretch(timer.rectTransform, 0f);
                timer.color = Color.white;
                m_Slots.Add((all[i], cover, timer));
            }

            if (GameSettings.AutoSkills)
            {
                var auto = UIUtil.Text(bar, font, "<b>AUTO</b> 스킬 자동 사용 중", 18f, TextAlignmentOptions.Center);
                UIUtil.Place(auto.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 16f), new Vector2(400f, 26f));
                auto.color = new Color(0.6f, 1f, 0.7f, 0.85f);
            }

            // 궁극기 게이지
            var ult = UIUtil.Image(bar, "Ultimate", new Color(0.12f, 0.08f, 0.02f, 0.9f));
            m_UltFrame = ult.GetComponent<Image>();
            ult.anchorMin = ult.anchorMax = new Vector2(0f, 0.5f);
            ult.pivot = new Vector2(0f, 0.5f);
            ult.sizeDelta = new Vector2(142f, 80f);
            ult.anchoredPosition = new Vector2(all.Length * 150f + 4f, 0f);
            m_UltFill = UIUtil.Image(ult, "Fill", new Color(1f, 0.75f, 0.2f, 0.55f));
            m_UltFill.anchorMin = Vector2.zero;
            m_UltFill.anchorMax = new Vector2(0f, 1f);
            m_UltFill.pivot = new Vector2(0f, 0.5f);
            m_UltText = UIUtil.Text(ult, font, "", 18f, TextAlignmentOptions.Center);
            UIUtil.Stretch(m_UltText.rectTransform, 4f);
        }

        void Update()
        {
            foreach (var (skill, cover, timer) in m_Slots)
            {
                float ratio = skill.Cooldown > 0f ? skill.Remaining / skill.Cooldown : 0f;
                cover.sizeDelta = new Vector2(0f, 80f * ratio);
                timer.text = skill.Ready ? "" : $"{skill.Remaining:0}";
            }

            if (!m_Ultimate) return;
            float charge = m_Ultimate.Charge01;
            m_UltFill.anchorMax = new Vector2(charge, 1f);
            m_UltFill.sizeDelta = Vector2.zero;
            bool ready = m_Ultimate.IsReady;
            m_UltText.text = ready
                ? "<b><color=#FFE680>[G] 궁극기!</color></b>\n<size=75%>Σ 무한급수 폭격</size>"
                : $"<b>궁극기</b> {charge * 100f:0}%\n<size=75%>적 처치·정답으로 충전</size>";
            float pulse = ready ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
            m_UltFrame.color = Color.Lerp(new Color(0.12f, 0.08f, 0.02f, 0.9f), new Color(0.9f, 0.6f, 0.1f, 0.95f), pulse);
        }
    }
}
