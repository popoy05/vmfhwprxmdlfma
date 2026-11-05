using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    public enum MobKind { Dummy, Hopper, Brute, Boss }

    // 연습용 몹
    //  - 허수아비(Dummy): 제자리
    //  - 깡총이(Hopper): 집 근처를 포물선으로 깡총깡총 뛰어다님 (작고 빠름)
    //  - 거인(Brute): 크고 단단함 (체력 3배)
    // 맞으면 흔들리고 숫자가 뜬다. 밀려나면 미끄러지다 다른 몹과 부딪쳐 함께 다친다(Bumped 이벤트).
    // 무기 기믹용 상태: 서리(Frost 0~3, 3이면 얼어붙음) · 번개 표식(Marked)
    public class TrainingDummy : MonoBehaviour
    {
        public static readonly List<TrainingDummy> All = new List<TrainingDummy>();
        /// <summary>밀려난 몹(a)이 다른 몹(b)과 부딪쳤다 (속도)</summary>
        public static event Action<TrainingDummy, TrainingDummy, float> Bumped;
        /// <summary>쓰러졌다 (모험 목표·처치 수)</summary>
        public static event Action<TrainingDummy> AnyDied;
        public event Action<TrainingDummy> Died;
        /// <summary>false면 쓰러진 뒤 다시 일어나지 않고 사라진다 (모험 목표 몹)</summary>
        public bool Respawns = true;
        /// <summary>true면 플레이어가 가까이 오면 쫓아와 공격한다 (모험 몹). 훈련장 허수아비는 false</summary>
        public bool Hostile;
        public string DisplayName;
        public float Health => m_Health;

        public float MaxHealth = 60000f;
        public float Scale = 1f;
        public MobKind Kind;

        float m_Health;
        Transform m_Body;
        Renderer[] m_Renderers;
        Color[] m_BaseColors;
        Vector3 m_Wobble, m_WobbleVel;
        float m_Flash;
        float m_DownTimer;
        Collider m_Collider;
        Vector3 m_Home;

        // 밀려남 (수평 속도, 마찰로 줄어든다)
        Vector3 m_Slide;
        float m_BumpCooldown;
        // 깡총이
        float m_HopTimer, m_HopTime = -1f, m_HopDuration;
        Vector3 m_HopFrom, m_HopTo;

        // 기믹 상태
        public int Frost { get; private set; }
        public bool Frozen => Time.time < m_FrozenUntil;
        float m_FrozenUntil;
        GameObject m_IceFx;
        public bool Marked => Time.time < m_MarkedUntil;
        float m_MarkedUntil;
        GameObject m_MarkFx;

        public bool Alive => m_DownTimer <= 0f;
        float BodyScale => Scale * (Kind == MobKind.Boss ? 2.6f : Kind == MobKind.Brute ? 1.7f : Kind == MobKind.Hopper ? 0.75f : 1f);
        public Vector3 Center => transform.position + Vector3.up * 0.9f * BodyScale;
        public Vector3 Head => transform.position + Vector3.up * 1.62f * BodyScale;
        public float Radius => 0.35f * BodyScale;
        public float Height => 1.9f * BodyScale;
        public bool Airborne => m_LaunchTime >= 0f || m_HopTime >= 0f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static TrainingDummy Create(Vector3 position, Quaternion rotation, float scale, MobKind kind = MobKind.Dummy)
        {
            var root = new GameObject(kind.ToString());
            root.layer = 2;
            root.transform.SetPositionAndRotation(position, rotation);
            var dummy = root.AddComponent<TrainingDummy>();
            dummy.Scale = scale;
            dummy.Kind = kind;
            dummy.m_Home = position;
            dummy.m_HopTimer = UnityEngine.Random.Range(0.3f, 2f);
            if (kind == MobKind.Brute) dummy.MaxHealth *= 3f;
            if (kind == MobKind.Boss) dummy.MaxHealth *= 6f;
            if (kind == MobKind.Hopper) dummy.MaxHealth *= 0.6f;

            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            dummy.m_Body = body;
            float b = dummy.BodyScale;

            var wood = new Color(0.55f, 0.36f, 0.2f);
            bool heavy = kind == MobKind.Brute || kind == MobKind.Boss;
            var straw = kind == MobKind.Boss ? new Color(0.4f, 0.32f, 0.45f) : kind == MobKind.Brute ? new Color(0.62f, 0.55f, 0.5f) : kind == MobKind.Hopper ? new Color(0.6f, 0.9f, 0.55f) : new Color(0.93f, 0.78f, 0.38f);
            var cloth = kind == MobKind.Boss ? new Color(0.75f, 0.15f, 0.2f) : kind == MobKind.Brute ? new Color(0.35f, 0.3f, 0.55f) : kind == MobKind.Hopper ? new Color(0.3f, 0.6f, 0.95f) : new Color(0.85f, 0.3f, 0.3f);
            if (kind != MobKind.Hopper)
                Part(body, PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 0.45f, 0.12f), wood, b);
            else
            {
                Part(body, PrimitiveType.Sphere, new Vector3(-0.15f, 0.12f, 0f), new Vector3(0.2f, 0.24f, 0.32f), wood, b); // 발
                Part(body, PrimitiveType.Sphere, new Vector3(0.15f, 0.12f, 0f), new Vector3(0.2f, 0.24f, 0.32f), wood, b);
            }
            Part(body, PrimitiveType.Capsule, new Vector3(0f, 1.05f, 0f), new Vector3(0.5f, 0.42f, 0.4f), straw, b);
            Part(body, PrimitiveType.Cylinder, new Vector3(0f, 1.2f, 0f), new Vector3(0.09f, 0.55f, 0.09f), wood, b).localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(body, PrimitiveType.Sphere, new Vector3(0f, 1.62f, 0f), new Vector3(0.38f, 0.38f, 0.38f), straw, b);
            Part(body, PrimitiveType.Cube, new Vector3(0f, 1.05f, 0.2f), new Vector3(0.36f, 0.36f, 0.04f), cloth, b);
            if (heavy)
            {
                // 뿔 두 개 + 어깨 갑옷
                Part(body, PrimitiveType.Capsule, new Vector3(-0.14f, 1.85f, 0f), new Vector3(0.07f, 0.14f, 0.07f), Color.white, b).localRotation = Quaternion.Euler(0f, 0f, 25f);
                Part(body, PrimitiveType.Capsule, new Vector3(0.14f, 1.85f, 0f), new Vector3(0.07f, 0.14f, 0.07f), Color.white, b).localRotation = Quaternion.Euler(0f, 0f, -25f);
                Part(body, PrimitiveType.Sphere, new Vector3(-0.32f, 1.3f, 0f), new Vector3(0.26f, 0.2f, 0.26f), cloth, b);
                Part(body, PrimitiveType.Sphere, new Vector3(0.32f, 1.3f, 0f), new Vector3(0.26f, 0.2f, 0.26f), cloth, b);
            }
            if (kind == MobKind.Boss)
            {
                // 왕관
                var gold = new Color(1f, 0.8f, 0.25f);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2f / 5f;
                    Part(body, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.13f, 1.86f, Mathf.Sin(a) * 0.13f), new Vector3(0.06f, 0.12f, 0.06f), gold, b);
                }
                Part(body, PrimitiveType.Cylinder, new Vector3(0f, 1.8f, 0f), new Vector3(0.3f, 0.03f, 0.3f), gold, b);
            }
            if (kind == MobKind.Hopper)
            {
                // 귀 두 개
                Part(body, PrimitiveType.Capsule, new Vector3(-0.1f, 1.95f, 0f), new Vector3(0.08f, 0.2f, 0.05f), straw, b);
                Part(body, PrimitiveType.Capsule, new Vector3(0.1f, 1.95f, 0f), new Vector3(0.08f, 0.2f, 0.05f), straw, b);
            }

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f) * b;
            col.height = 1.8f * b;
            col.radius = 0.3f * b;
            dummy.m_Collider = col;

            dummy.m_Renderers = body.GetComponentsInChildren<Renderer>();
            dummy.m_BaseColors = new Color[dummy.m_Renderers.Length];
            for (int i = 0; i < dummy.m_Renderers.Length; i++) dummy.m_BaseColors[i] = dummy.m_Renderers[i].material.color;
            dummy.m_Health = dummy.MaxHealth;
            return dummy;
        }

        static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 size, Color color, float scale)
        {
            var go = GameObject.CreatePrimitive(type);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos * scale;
            go.transform.localScale = size * scale;
            var r = go.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            return go.transform;
        }

        public void ResetHealth() => m_Health = MaxHealth;

        /// <summary>맞았다. from: 때린 쪽 위치 (흔들리는 방향), tag: 숫자 위에 붙는 말 (예: "헤드샷!")</summary>
        public void Hit(float damage, Vector3 from, bool big, bool crit = false, string tag = null)
        {
            if (!Alive) return;
            m_Health -= damage;
            m_Flash = 1f;

            Vector3 away = transform.position - from;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
            Vector3 axis = Vector3.Cross(Vector3.up, away);
            if (!Frozen) m_WobbleVel += axis * (crit ? 420f : big ? 300f : 170f);

            Color color = crit ? new Color(1f, 0.35f, 0.2f) : big ? new Color(1f, 0.85f, 0.3f) : new Color(0.85f, 0.97f, 1f);
            string text = Mathf.RoundToInt(damage).ToString("N0");
            if (tag != null) text = $"<size=55%>{tag}</size>\n" + text;
            else if (crit) text = "<size=55%>치명타!</size>\n" + text;
            GeoFx.Popup(Center + Vector3.up * 0.6f * BodyScale, text, color, Scale, crit || tag != null ? 1.9f : big ? 1.45f : 1.15f);

            if (m_Health <= 0f)
            {
                m_DownTimer = Respawns ? 3f : 1.2f;
                Died?.Invoke(this);
                AnyDied?.Invoke(this);
                m_Collider.enabled = false;
                m_Slide = Vector3.zero;
                ClearFrost();
                m_MarkedUntil = 0f;
                if (m_MarkFx) Destroy(m_MarkFx);
            }
        }

        // ---------------------------------------------------------------- 기믹 상태

        /// <summary>서리 쌓기. 3이 되면 얼어붙는다 (seconds 동안). 얼어붙었으면 true</summary>
        public bool AddFrost(int amount, float seconds = 3.5f)
        {
            if (!Alive) return false;
            Frost = Mathf.Min(3, Frost + amount);
            if (Frost >= 3 && !Frozen)
            {
                m_FrozenUntil = Time.time + seconds;
                m_Wobble = m_WobbleVel = Vector3.zero;
                m_Slide = Vector3.zero;
                if (m_IceFx) Destroy(m_IceFx);
                m_IceFx = Fx.Play("Crystal effect blue", transform.position, Quaternion.identity, 0.55f * BodyScale, seconds, false, transform);
                return true;
            }
            return Frozen;
        }

        /// <summary>얼음을 깬다 (얼어 있었으면 true)</summary>
        public bool Shatter()
        {
            bool was = Frozen;
            ClearFrost();
            return was;
        }

        void ClearFrost()
        {
            Frost = 0;
            m_FrozenUntil = 0f;
            if (m_IceFx) Destroy(m_IceFx);
        }

        public void Mark(float seconds)
        {
            if (!Alive) return;
            m_MarkedUntil = Time.time + seconds;
            if (m_MarkFx) Destroy(m_MarkFx);
            m_MarkFx = Fx.Play("Sparks flashing pink", Head + Vector3.up * 0.35f * BodyScale, Quaternion.identity, 0.3f * BodyScale, seconds, false, transform);
        }

        public void ClearMark()
        {
            m_MarkedUntil = 0f;
            if (m_MarkFx) Destroy(m_MarkFx);
        }

        // ---------------------------------------------------------------- 움직임

        /// <summary>수평으로 밀어낸다 (거인은 덜 밀림)</summary>
        public void Knock(Vector3 velocity)
        {
            if (!Alive || Frozen) return;
            velocity.y = 0f;
            m_Slide += velocity / (Kind == MobKind.Brute ? 2.5f : 1f);
            m_BumpCooldown = 0f;
        }

        // 포물선으로 날아갔다가 떨어진다:  p(t) = v·t + ½·g·t²  (g는 아래쪽)
        Vector3 m_LaunchVelocity;
        float m_LaunchGravity, m_LaunchTime = -1f;
        public event Action<TrainingDummy> Landed;

        public void Launch(Vector3 velocity, float gravity)
        {
            if (!Alive || Frozen) return;
            if (Kind == MobKind.Brute) velocity *= 0.6f;
            m_LaunchVelocity = velocity;
            m_LaunchGravity = gravity;
            m_LaunchTime = 0f;
        }

        /// <summary>강제로 옮긴다 (끌어당기기 등)</summary>
        public void MoveTo(Vector3 position)
        {
            position.y = GeoFx.GroundY(position, Scale);
            if (Mathf.Abs(position.y - transform.position.y) > 0.8f * Scale) return; // 다른 층으로는 끌려가지 않음
            transform.position = position;
        }

        // 집에서 너무 멀어졌나 (모험 몹은 자기 자리 근처에서만)
        bool Leashed(Vector3 p)
        {
            if (!Hostile) return false;
            Vector3 d = p - m_Home;
            d.y = 0f;
            return d.magnitude > (Kind == MobKind.Boss ? 18f : 10f) * Scale;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // 날아가는 중 (포물선, 몸만 움직이고 끝나면 떨어진 자리로 옮겨 감)
            if (m_LaunchTime >= 0f)
            {
                m_LaunchTime += dt;
                float t = m_LaunchTime;
                Vector3 p = m_LaunchVelocity * t + Vector3.down * 0.5f * m_LaunchGravity * t * t;
                if (p.y <= 0f && t > 0.1f)
                {
                    m_LaunchTime = -1f;
                    Vector3 land = transform.position + new Vector3(p.x, 0f, p.z);
                    land.y = GeoFx.GroundY(land, Scale);
                    // 다른 층(아래 낭떠러지)으로 떨어질 자리면 원래 자리로 내려온다
                    if (Mathf.Abs(land.y - transform.position.y) < 0.6f * Scale && !Leashed(land)) transform.position = land;
                    m_Body.localPosition = Vector3.zero;
                    m_WobbleVel += transform.right * 200f;
                    Landed?.Invoke(this);
                }
                else
                {
                    m_Body.position = transform.position + p;
                    m_Body.localRotation = Quaternion.Euler(t * 720f, 0f, 0f);
                    return;
                }
            }

            if (m_DownTimer > 0f)
            {
                // 쓰러졌다가 3초 뒤 집에서 다시 일어남
                m_DownTimer -= dt;
                float down = m_DownTimer > 0.5f ? 1f : m_DownTimer / 0.5f;
                m_Body.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(-85f, 0f, 0f), GeoFunctions.EaseOutCubic(down));
                if (m_DownTimer <= 0f && !Respawns)
                {
                    Fx.Play("Sparks explode white", Center, Quaternion.identity, 0.4f * Scale, 0.1f, false);
                    Destroy(gameObject);
                    return;
                }
                if (m_DownTimer <= 0f)
                {
                    m_Health = MaxHealth;
                    m_Collider.enabled = true;
                    m_Wobble = m_WobbleVel = Vector3.zero;
                    transform.position = m_Home;
                }
                Tint();
                return;
            }

            Slide(dt);
            if (Hostile && !Frozen) Think(dt);
            if (Kind == MobKind.Hopper && !Frozen) Hop(dt);

            if (!Frozen)
            {
                // 스프링: 흔들렸다가 제자리로
                m_WobbleVel += (-m_Wobble * 120f - m_WobbleVel * 9f) * dt;
                m_Wobble += m_WobbleVel * dt;
                m_Wobble = Vector3.ClampMagnitude(m_Wobble, 40f);
                Quaternion wobble = Quaternion.Euler(transform.InverseTransformDirection(m_Wobble));
                if (m_HopTime < 0f && m_Windup < 0f) m_Body.localRotation = wobble * Quaternion.Euler(0f, 0f, m_Engaged && Kind != MobKind.Hopper ? Mathf.Sin(Time.time * 12f) * 5f : 0f);
            }
            if (m_Flash > 0f) m_Flash = Mathf.Max(0f, m_Flash - dt * 6f);
            Tint();
        }

        void Slide(float dt)
        {
            if (m_Slide.sqrMagnitude < 0.01f * Scale * Scale) { m_Slide = Vector3.zero; return; }
            Vector3 next = transform.position + m_Slide * dt;
            float ground = GeoFx.GroundY(next, Scale);
            if (Mathf.Abs(ground - transform.position.y) > 0.45f * Scale || Leashed(next)) { m_Slide = Vector3.zero; return; } // 낭떠러지·벽에서 멈춤 (떨어지지 않게)
            next.y = ground;
            transform.position = next;
            m_Slide *= Mathf.Exp(-3.2f * dt); // 마찰: v(t) = v₀e^(−kt)

            // 핀볼: 빠르게 밀려가다 다른 몹과 부딪치면 둘 다 다친다
            m_BumpCooldown -= dt;
            float speed = m_Slide.magnitude;
            if (speed < 2.5f * Scale || m_BumpCooldown > 0f) return;
            foreach (var other in All)
            {
                if (other == this || !other.Alive) continue;
                Vector3 d = other.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude > Radius + other.Radius + 0.15f * Scale) continue;
                m_BumpCooldown = 0.25f;
                other.Knock(m_Slide * 0.7f + d.normalized * speed * 0.3f);
                m_Slide *= 0.35f;
                Bumped?.Invoke(this, other, speed);
                break;
            }
        }

        void Hop(float dt)
        {
            if (m_HopTime >= 0f)
            {
                m_HopTime += dt;
                float u = Mathf.Clamp01(m_HopTime / m_HopDuration);
                Vector3 p = Vector3.Lerp(m_HopFrom, m_HopTo, u);
                p.y = Mathf.Lerp(m_HopFrom.y, m_HopTo.y, u);
                transform.position = p;
                // 포물선 높이 h(u) = 4H·u(1 − u)
                m_Body.localPosition = Vector3.up * 4f * 0.6f * Scale * u * (1f - u);
                m_Body.localRotation = Quaternion.Euler(Mathf.Sin(u * Mathf.PI) * -15f, 0f, 0f);
                if (u >= 1f) { m_HopTime = -1f; m_Body.localPosition = Vector3.zero; }
                return;
            }
            if (m_Slide.sqrMagnitude > 0.1f) return;
            m_HopTimer -= dt;
            if (m_HopTimer > 0f) return;
            m_HopTimer = UnityEngine.Random.Range(0.5f, 1.4f);

            // 싸우는 중이면 플레이어 쪽으로, 아니면 집에서 3m 안의 다음 자리로
            Vector2 r = UnityEngine.Random.insideUnitCircle * 3f * Scale;
            Vector3 to = m_Engaged && NewModePlayer.Instance ? NewModePlayer.Instance.transform.position : m_Home + new Vector3(r.x, 0f, r.y);
            if (m_Engaged) m_HopTimer = UnityEngine.Random.Range(0.25f, 0.6f);
            Vector3 step = to - transform.position;
            step.y = 0f;
            step = Vector3.ClampMagnitude(step, 1.6f * Scale);
            to = transform.position + step;
            to.y = GeoFx.GroundY(to, Scale);
            if (Mathf.Abs(to.y - transform.position.y) > 0.45f * Scale || Leashed(to)) return;
            m_HopFrom = transform.position;
            m_HopTo = to;
            m_HopDuration = 0.45f;
            m_HopTime = 0f;
            if (step.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(step);
        }

        // ---------------------------------------------------------------- 적 AI (모험 몹)
        //  가까이 오면(7m) 쫓아오고, 닿으면 잠깐 빨갛게 깜빡인 뒤(예고) 때린다 → 그 사이에 피할 수 있음

        bool m_Engaged;
        float m_AttackCd = 1f, m_Windup = -1f;

        float Reach => (Kind == MobKind.Boss ? 2.4f : Kind == MobKind.Brute ? 1.5f : 1f) * Scale;
        float WindupTime => Kind == MobKind.Boss ? 0.9f : Kind == MobKind.Brute ? 0.75f : 0.55f;
        float HitDamage => Kind == MobKind.Boss ? 12f : Kind == MobKind.Brute ? 8f : Kind == MobKind.Hopper ? 3f : 5f;
        float WalkSpeed => (Kind == MobKind.Boss ? 1.3f : Kind == MobKind.Brute ? 0.9f : 1.1f) * Scale;
        public bool Busy => m_Windup >= 0f;
        public bool Engaged => m_Engaged;

        void Think(float dt)
        {
            NewModePlayer p = NewModePlayer.Instance;
            if (!p || p.Dead || Airborne || m_Slide.sqrMagnitude > 0.25f * Scale * Scale) { m_Engaged = false; m_Windup = -1f; return; }
            Vector3 to = p.transform.position - transform.position;
            float dy = Mathf.Abs(to.y);
            to.y = 0f;
            float d = to.magnitude;
            float aggro = (Kind == MobKind.Boss ? 11f : 7f) * Scale;
            m_Engaged = d < aggro && dy < 2.5f * Scale && !Leashed(p.transform.position);

            // 공격 예고 → 때리기
            if (m_Windup >= 0f)
            {
                m_Windup += dt;
                m_Body.localRotation = Quaternion.Euler(-18f * Mathf.Min(1f, m_Windup / WindupTime), 0f, 0f);
                if (m_Windup >= WindupTime)
                {
                    m_Windup = -1f;
                    m_AttackCd = Kind == MobKind.Boss ? 1.4f : 1.8f;
                    m_WobbleVel += transform.right * 300f;
                    Fx.Play(Kind == MobKind.Boss ? "Stones hit" : "Dust puff", transform.position + transform.forward * Reach * 0.6f, Quaternion.identity, 0.4f * BodyScale, 0.1f, false);
                    if (d < Reach + 0.5f * Scale && dy < 1.2f * BodyScale) p.TakeDamage(HitDamage, transform.position, Kind == MobKind.Boss ? 6f : 3f);
                }
                return;
            }
            m_AttackCd -= dt;

            if (!m_Engaged)
            {
                // 집으로 천천히 돌아감
                Vector3 home = m_Home - transform.position;
                home.y = 0f;
                if (Kind != MobKind.Hopper && home.magnitude > 0.3f * Scale) Walk(home.normalized, dt);
                return;
            }

            if (to.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * dt);
            if (d > Reach)
            {
                if (Kind != MobKind.Hopper) Walk(to.normalized, dt);
            }
            else if (m_AttackCd <= 0f)
            {
                m_Windup = 0f;
            }
        }

        void Walk(Vector3 dir, float dt)
        {
            Vector3 next = transform.position + dir * WalkSpeed * dt;
            float ground = GeoFx.GroundY(next, Scale);
            if (Mathf.Abs(ground - transform.position.y) > 0.35f * Scale || Leashed(next)) return;
            next.y = ground;
            transform.position = next;
            // 뒤뚱뒤뚱
            m_Body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 12f) * 6f);
        }

        void Tint()
        {
            // 하얗게 번쩍(맞음) · 푸르게(서리 단계) · 얼음(얼어붙음)
            Color ice = new Color(0.55f, 0.85f, 1f);
            float frost = Frozen ? 0.75f : Frost * 0.15f;
            float warn = m_Windup >= 0f ? 0.25f + 0.5f * Mathf.PingPong(m_Windup * 10f, 1f) : 0f; // 공격 직전 빨갛게 깜빡
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                Color c = Color.Lerp(m_BaseColors[i], ice, frost);
                c = Color.Lerp(c, new Color(1f, 0.15f, 0.1f), warn);
                m_Renderers[i].material.color = Color.Lerp(c, Color.white, m_Flash);
            }
        }
    }
}
