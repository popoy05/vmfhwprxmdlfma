using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.FPS.MathMode
{
    // 근접(단검) 모드 [F]
    // 좌클릭(모바일: 공격 버튼)을 누르고 있으면 단검을 뒤로 당기며 휘두를 각도 θ가 커지고(60° ~ 180°), 떼면 벤다.
    // 칼날이 실제로 지나간 각도 구간 안의 적만 맞고, 피해량 = k·θ (휘두른 각도에 비례).
    public class PlayerMeleeMode : MonoBehaviour
    {
        public float Reach = 2.6f;
        public float MinAngle = 60f;
        public float MaxAngle = 180f;
        public float ChargeTime = 0.7f;
        [Tooltip("휘두른 각도 1°당 피해")]
        public float DamagePerDegree = 0.5f;
        public float Cooldown = 0.15f;

        enum State { Idle, Charging, Swinging, Recover }

        static readonly Vector3 k_RestPos = new Vector3(0.27f, -0.27f, 0.52f);
        static readonly Vector3 k_RestEuler = new Vector3(-8f, -14f, 4f);

        public bool IsActive { get; private set; }
        public float CurrentAngle => m_State == State.Idle || m_State == State.Recover ? MinAngle : m_SwingAngle;
        public float LastDamage { get; private set; }
        public float LastArcLength => Reach * m_SwingAngle * Mathf.Deg2Rad;
        public float Charge01 { get; private set; }

        MathModeManager m_Manager;
        PlayerInputHandler m_Input;
        PlayerWeaponsManager m_Weapons;
        CharacterController m_Controller;
        Camera m_Camera;
        Transform m_Dagger;
        Renderer m_Edge;
        TrailRenderer m_Trail;

        State m_State;
        float m_StateStart;
        float m_SwingAngle;
        float m_SwingDuration;
        float m_PrevYaw;
        float m_NextAttackTime;
        float m_LastCameraYaw;
        Vector3 m_Sway;
        Vector3 m_PosVelocity;
        readonly HashSet<Health> m_HitThisSwing = new HashSet<Health>();

        public void Init(MathModeManager manager)
        {
            m_Manager = manager;
            m_Input = GetComponent<PlayerInputHandler>();
            m_Weapons = GetComponent<PlayerWeaponsManager>();
            m_Controller = GetComponent<CharacterController>();
            m_Camera = m_Weapons ? m_Weapons.WeaponCamera : Camera.main;
            if (m_Weapons) m_Weapons.OnSwitchedToWeapon += _ => SetActive(false);
            BuildDagger();
        }

        void BuildDagger()
        {
            m_Dagger = new GameObject("Dagger").transform;
            m_Dagger.SetParent(m_Camera.transform, false);
            m_Dagger.localPosition = k_RestPos;
            m_Dagger.localRotation = Quaternion.Euler(k_RestEuler);

            var steel = new Color(0.82f, 0.86f, 0.9f);
            m_Manager.CreateLitBlock(m_Dagger, new Vector3(0f, 0f, 0.2f), new Vector3(0.045f, 0.012f, 0.34f), steel, 0.95f);
            m_Manager.CreateLitBlock(m_Dagger, new Vector3(0f, 0f, 0.385f), new Vector3(0.03f, 0.011f, 0.05f), steel, 0.95f)
                .transform.localRotation = Quaternion.Euler(0f, 45f, 0f); // 뾰족한 끝
            // 칼날 가운데 홈: 충전할수록 빛난다
            var edge = m_Manager.CreateBlock(m_Dagger, PrimitiveType.Cube, new Vector3(0f, 0.007f, 0.2f), new Vector3(0.012f, 0.004f, 0.3f), new Color(0.4f, 0.8f, 1f));
            m_Edge = edge.GetComponent<Renderer>();
            m_Manager.CreateLitBlock(m_Dagger, new Vector3(0f, 0f, 0.025f), new Vector3(0.15f, 0.025f, 0.03f), new Color(0.85f, 0.68f, 0.25f), 0.8f);
            m_Manager.CreateLitBlock(m_Dagger, new Vector3(0f, 0f, -0.06f), new Vector3(0.035f, 0.035f, 0.14f), new Color(0.25f, 0.15f, 0.1f), 0.2f);
            m_Manager.CreateLitBlock(m_Dagger, new Vector3(0f, 0f, -0.135f), new Vector3(0.05f, 0.05f, 0.03f), new Color(0.85f, 0.68f, 0.25f), 0.8f, PrimitiveType.Sphere);

            // 칼끝 잔상
            var tip = new GameObject("Tip").transform;
            tip.SetParent(m_Dagger, false);
            tip.localPosition = new Vector3(0f, 0f, 0.4f);
            m_Trail = tip.gameObject.AddComponent<TrailRenderer>();
            m_Trail.time = 0.14f;
            m_Trail.minVertexDistance = 0.01f;
            m_Trail.widthCurve = AnimationCurve.Linear(0f, 0.06f, 1f, 0f);
            if (m_Manager.LineMaterial) m_Trail.sharedMaterial = m_Manager.LineMaterial;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(0.8f, 1f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.7f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            m_Trail.colorGradient = gradient;
            m_Trail.emitting = false;
            m_Trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            MathCombat.SetLayerRecursively(m_Dagger.gameObject, MathCombat.WeaponLayer(m_Weapons));
            m_Dagger.gameObject.SetActive(false);
        }

        void SetActive(bool active)
        {
            if (IsActive == active) return;
            IsActive = active;
            SetState(State.Idle);
            if (m_Input) m_Input.FireBlocked = active;
            m_Dagger.gameObject.SetActive(active);
            if (active)
            {
                // 아래에서 올라오며 꺼내기
                m_Dagger.localPosition = k_RestPos + new Vector3(0.05f, -0.35f, -0.1f);
                m_Dagger.localRotation = Quaternion.Euler(40f, -30f, 20f);
            }

            WeaponController weapon = m_Weapons ? m_Weapons.GetActiveWeapon() : null;
            if (weapon) weapon.ShowWeapon(!active);
            MathModeManager.ShowMessage(active
                ? "단검 모드: 길게 눌렀다 떼면 크게 휘두름 (F로 해제)"
                : "사격 모드");
        }

        void SetState(State state)
        {
            m_State = state;
            m_StateStart = Time.time;
        }

        void Update()
        {
            bool canInput = m_Input && m_Input.CanProcessInput();
            if (canInput && MathInput.Pressed(Key.F, "dagger"))
                SetActive(!IsActive);
            if (!IsActive) return;

            UpdateSway();
            float t = Time.time - m_StateStart;

            switch (m_State)
            {
                case State.Idle:
                {
                    Charge01 = 0f;
                    Pose(k_RestPos + Bob(), Quaternion.Euler(k_RestEuler + m_Sway), 14f);
                    SetEdgeGlow(0f);
                    if (canInput && MathInput.AttackPressed() && Time.time >= m_NextAttackTime)
                        SetState(State.Charging);
                    break;
                }

                case State.Charging:
                {
                    Charge01 = Mathf.Clamp01(t / ChargeTime);
                    m_SwingAngle = Mathf.Lerp(MinAngle, MaxAngle, Charge01);
                    // 오른쪽 위로 당기는 자세. 가득 차면 살짝 떨린다
                    Vector3 tremble = Charge01 >= 1f ? Random.insideUnitSphere * 0.004f : Vector3.zero;
                    Pose(new Vector3(0.36f, -0.16f, 0.4f) + tremble,
                        Quaternion.Euler(-30f - 10f * Charge01, 45f + 40f * Charge01, -40f * Charge01), 16f);
                    SetEdgeGlow(Charge01);
                    if (!MathInput.AttackHeld())
                        BeginSwing();
                    break;
                }

                case State.Swinging:
                    UpdateSwing(t);
                    break;

                case State.Recover:
                    Pose(k_RestPos + Bob(), Quaternion.Euler(k_RestEuler + m_Sway), 10f);
                    if (t > 0.25f) SetState(State.Idle);
                    break;
            }
        }

        // 걷는 동안 위아래·좌우로 흔들림
        Vector3 Bob()
        {
            float speed = m_Controller ? new Vector2(m_Controller.velocity.x, m_Controller.velocity.z).magnitude : 0f;
            float k = Mathf.Clamp01(speed / 8f);
            float time = Time.time * 9f;
            return new Vector3(Mathf.Cos(time * 0.5f) * 0.012f, Mathf.Abs(Mathf.Sin(time * 0.5f)) * -0.018f, 0f) * k;
        }

        // 시선을 돌리면 단검이 살짝 늦게 따라오는 느낌
        void UpdateSway()
        {
            float yaw = transform.eulerAngles.y;
            float delta = Mathf.DeltaAngle(m_LastCameraYaw, yaw);
            m_LastCameraYaw = yaw;
            Vector3 target = new Vector3(0f, -Mathf.Clamp(delta * 1.5f, -8f, 8f), Mathf.Clamp(delta * 2f, -10f, 10f));
            m_Sway = Vector3.Lerp(m_Sway, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        void Pose(Vector3 pos, Quaternion rot, float sharpness)
        {
            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            m_Dagger.localPosition = Vector3.SmoothDamp(m_Dagger.localPosition, pos, ref m_PosVelocity, 1f / sharpness);
            m_Dagger.localRotation = Quaternion.Slerp(m_Dagger.localRotation, rot, k);
        }

        void SetEdgeGlow(float charge)
        {
            Color c = Color.Lerp(new Color(0.4f, 0.6f, 0.8f), new Color(0.6f, 1.6f, 2.4f), charge);
            m_Edge.material.SetColor("_BaseColor", c);
        }

        void BeginSwing()
        {
            SetState(State.Swinging);
            m_SwingDuration = 0.09f + m_SwingAngle / 1300f;
            m_PrevYaw = m_SwingAngle / 2f;
            m_HitThisSwing.Clear();
            LastDamage = m_SwingAngle * DamagePerDegree;
            m_Trail.Clear();
            m_Trail.emitting = true;
            if (m_Manager.CameraFX) m_Manager.CameraFX.FovKick(2f + Charge01 * 4f, 0.25f);
        }

        void UpdateSwing(float t)
        {
            float u = Mathf.Clamp01(t / m_SwingDuration);
            float eased = 1f - Mathf.Pow(1f - u, 3f); // 빠르게 시작해서 부드럽게 끝나는 베기
            // 오른쪽 위(+θ/2)에서 왼쪽 아래(-θ/2)로 대각선으로 벤다
            float yaw = Mathf.Lerp(m_SwingAngle / 2f, -m_SwingAngle / 2f, eased);
            float side = yaw / Mathf.Max(m_SwingAngle / 2f, 1f); // +1 → -1
            m_Dagger.localPosition = new Vector3(0.05f + side * 0.3f, -0.2f + side * 0.1f, 0.5f - (1f - side * side) * 0.05f);
            m_Dagger.localRotation = Quaternion.Euler(8f - side * 12f, yaw, -75f);

            HitBetween(yaw, m_PrevYaw);
            m_PrevYaw = yaw;

            if (u >= 1f)
            {
                m_Trail.emitting = false;
                m_NextAttackTime = Time.time + Cooldown;
                SetState(State.Recover);
                Charge01 = 0f;
                if (m_HitThisSwing.Count > 0)
                    MathModeManager.ShowMessage($"θ = {m_SwingAngle:0}° → 피해 {LastDamage:0}  (호의 길이 s = rθ = {LastArcLength:0.0}m)");
            }
        }

        // 이번 프레임에 칼날이 쓸고 지나간 각도 [minYaw, maxYaw] 안의 적에게 피해
        void HitBetween(float yawA, float yawB)
        {
            float minYaw = Mathf.Min(yawA, yawB), maxYaw = Mathf.Max(yawA, yawB);
            Transform eye = m_Camera.transform;
            Vector3 flatForward = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;

            foreach (Collider col in Physics.OverlapSphere(eye.position, Reach, ~0, QueryTriggerInteraction.Collide))
            {
                if (col.transform.IsChildOf(transform)) continue;
                var damageable = col.GetComponent<Damageable>();
                if (!damageable || !damageable.Health || m_HitThisSwing.Contains(damageable.Health)) continue;

                Vector3 point = col.ClosestPoint(eye.position);
                Vector3 to = point - eye.position;
                Vector3 flatTo = Vector3.ProjectOnPlane(to, Vector3.up);
                float yaw = flatTo.sqrMagnitude < 0.01f ? 0f : Vector3.SignedAngle(flatForward, flatTo, Vector3.up);
                float pitch = Vector3.Angle(flatTo.sqrMagnitude < 0.01f ? flatForward : flatTo, to);
                if (yaw < minYaw - 2f || yaw > maxYaw + 2f || pitch > 65f) continue;

                m_HitThisSwing.Add(damageable.Health);
                damageable.InflictDamage(LastDamage, false, gameObject);
                OnHitFeedback(point);
            }
        }

        void OnHitFeedback(Vector3 point)
        {
            if (m_Manager.CameraFX)
            {
                m_Manager.CameraFX.HitStop(0.05f + Charge01 * 0.04f);
                m_Manager.CameraFX.Shake(0.4f + Charge01 * 0.6f, 0.18f);
            }

            StartCoroutine(Sparks(point));
            StartCoroutine(DamageNumber(point, LastDamage));
        }

        IEnumerator Sparks(Vector3 point)
        {
            var parts = new List<(Transform t, Vector3 v)>();
            for (int i = 0; i < 8; i++)
            {
                var spark = m_Manager.CreateBlock(null, PrimitiveType.Cube, point, Vector3.one * 0.07f, new Color(1.6f, 1.4f, 0.8f));
                parts.Add((spark.transform, Random.onUnitSphere * Random.Range(3f, 6f) + Vector3.up * 2f));
            }

            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                foreach (var (tr, v) in parts)
                {
                    tr.position += v * Time.unscaledDeltaTime * (1f - t / 0.35f);
                    tr.localScale = Vector3.one * 0.07f * (1f - t / 0.35f);
                }

                yield return null;
            }

            foreach (var (tr, _) in parts) Destroy(tr.gameObject);
        }

        IEnumerator DamageNumber(Vector3 point, float damage)
        {
            var root = new GameObject("DamageNumber").transform;
            root.position = point + Vector3.up * 0.5f;
            var text = m_Manager.CreateText(root, Vector3.zero, new Vector2(2f, 0.6f), $"<b>-{damage:0}</b>", new Color(1f, 0.85f, 0.3f), 5f);
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;

            for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
            {
                root.position += Vector3.up * Time.unscaledDeltaTime * 1.2f;
                root.rotation = Quaternion.LookRotation(root.position - m_Camera.transform.position);
                root.localScale = Vector3.one * (t < 0.1f ? Mathf.Lerp(0.5f, 1.3f, t / 0.1f) : Mathf.Lerp(1.3f, 1f, (t - 0.1f) / 0.2f));
                text.alpha = 1f - Mathf.Clamp01((t - 0.5f) / 0.3f);
                yield return null;
            }

            Destroy(root.gameObject);
        }
    }
}
