using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.MathMode
{
    // 수학 모드의 적 종류. 기본 호버봇에 크기·색·체력·속도와 특수 행동을 덧입혀 만든다.
    public enum EnemyKind
    {
        Drone,      // 점 드론: 기본
        Rusher,     // 벡터 돌격병: 작고 빠르게 달려와 자폭
        Tank,       // 행렬 탱커: 느리고 단단, 앞쪽 방패(부술 수 있음)
        Sniper,     // 극좌표 저격수: 멀리서 레이저로 조준 후 강한 한 발 (조준선을 보고 피하기)
        Splitter,   // 분수 분열체: 쓰러지면 ½ 크기 두 마리로 갈라짐
        Healer,     // 적분 치유체: 주변 적을 회복 (먼저 잡기)
        Blinker,    // 허수 i: 주기적으로 순간이동
        Orbiter,    // 원주율 π: 원 모양으로 탄막 발사
        Mini,       // 분열체가 나눈 ½ 조각
        Boss,       // 최종 보스
    }

    public class MathEnemy : MonoBehaviour
    {
        public EnemyKind Kind;
        public Color Color;

        static readonly HashSet<EnemyKind> s_Announced = new HashSet<EnemyKind>();
        public static void ResetAnnouncements() => s_Announced.Clear();

        public static string NameOf(EnemyKind k) => k switch
        {
            EnemyKind.Rusher => "벡터 돌격병",
            EnemyKind.Tank => "행렬 탱커",
            EnemyKind.Sniper => "극좌표 저격수",
            EnemyKind.Splitter => "분수 분열체",
            EnemyKind.Healer => "적분 치유체",
            EnemyKind.Blinker => "허수 i",
            EnemyKind.Orbiter => "원주율 π",
            EnemyKind.Mini => "½ 조각",
            EnemyKind.Boss => "∞ 무한의 군주",
            _ => "점 드론",
        };

        static string TipOf(EnemyKind k) => k switch
        {
            EnemyKind.Rusher => "빠르게 달려와 자폭! 가까이 오기 전에 쏘거나 충격파[Z]",
            EnemyKind.Tank => "앞의 격자 방패가 총알을 막는다. 방패를 부수거나 휘는 탄으로 옆을 노려라",
            EnemyKind.Sniper => "빨간 조준선이 보이면 옆으로 피하라!",
            EnemyKind.Splitter => "쓰러지면 ½ 크기 두 마리로 갈라진다",
            EnemyKind.Healer => "주변 적을 회복시킨다. 가장 먼저 처치!",
            EnemyKind.Blinker => "순간이동한다. 나타난 자리를 노려라",
            EnemyKind.Orbiter => "원 모양 탄막을 쏜다. 탄 사이로 피하라",
            _ => "",
        };

        public static Color ColorOf(EnemyKind k) => k switch
        {
            EnemyKind.Rusher => new Color(1f, 0.55f, 0.1f),
            EnemyKind.Tank => new Color(0.35f, 0.55f, 1f),
            EnemyKind.Sniper => new Color(0.75f, 0.35f, 1f),
            EnemyKind.Splitter => new Color(0.35f, 0.95f, 0.35f),
            EnemyKind.Healer => new Color(0.95f, 1f, 0.95f),
            EnemyKind.Blinker => new Color(0.2f, 1f, 1f),
            EnemyKind.Orbiter => new Color(1f, 0.35f, 0.8f),
            EnemyKind.Mini => new Color(0.6f, 1f, 0.5f),
            EnemyKind.Boss => new Color(1f, 0.8f, 0.15f),
            _ => new Color(1f, 0.25f, 0.3f),
        };

        // 크기, 체력 배율, 속도 배율
        static (Vector3 scale, float health, float speed) StatsOf(EnemyKind k) => k switch
        {
            EnemyKind.Rusher => (Vector3.one * 0.75f, 0.6f, 1.9f),
            EnemyKind.Tank => (Vector3.one * 1.45f, 2.8f, 0.6f),
            EnemyKind.Sniper => (new Vector3(0.85f, 1.25f, 0.85f), 0.8f, 0.75f),
            EnemyKind.Splitter => (Vector3.one * 1.2f, 1.3f, 0.9f),
            EnemyKind.Healer => (Vector3.one * 0.95f, 0.9f, 0.9f),
            EnemyKind.Blinker => (Vector3.one * 0.9f, 0.8f, 1.1f),
            EnemyKind.Orbiter => (Vector3.one * 1.1f, 1.3f, 0.8f),
            EnemyKind.Mini => (Vector3.one * 0.6f, 0.35f, 1.5f),
            EnemyKind.Boss => (Vector3.one * 2.6f, 1f, 0.7f),
            _ => (Vector3.one, 1f, 1f),
        };

        // 적 하나를 해당 종류로 바꾼다. 막 생성한 적(Start 전)과 이미 움직이는 적 모두에 쓸 수 있다.
        public static MathEnemy Apply(EnemyController enemy, EnemyKind kind, MathModeManager manager)
        {
            var me = enemy.gameObject.AddComponent<MathEnemy>();
            me.Kind = kind;
            me.Color = ColorOf(kind);

            Bounds bounds = LocalBounds(enemy.transform);
            var (scale, health, speed) = StatsOf(kind);
            enemy.transform.localScale = Vector3.Scale(enemy.transform.localScale, scale);

            var h = enemy.GetComponent<Health>();
            if (h && kind != EnemyKind.Boss)
            {
                h.MaxHealth *= health;
                h.CurrentHealth = h.MaxHealth;
            }

            ScaleSpeed(enemy, speed);

            var mobile = enemy.GetComponent<EnemyMobile>();
            var detection = enemy.GetComponentInChildren<DetectionModule>();
            switch (kind)
            {
                case EnemyKind.Rusher:
                    DisableGun(enemy);
                    if (mobile) mobile.AttackStopDistanceRatio = 0f;
                    if (detection) detection.DetectionRange = Mathf.Max(detection.DetectionRange, 30f);
                    enemy.gameObject.AddComponent<RusherBehaviour>().Init(manager, enemy);
                    break;
                case EnemyKind.Tank:
                    enemy.gameObject.AddComponent<TankShield>().Init(manager, enemy, bounds, me.Color);
                    break;
                case EnemyKind.Sniper:
                    DisableGun(enemy);
                    if (mobile) mobile.AttackStopDistanceRatio = 1f;
                    if (detection)
                    {
                        detection.DetectionRange = 50f;
                        detection.AttackRange = 38f;
                    }
                    enemy.gameObject.AddComponent<SniperBehaviour>().Init(manager, enemy);
                    break;
                case EnemyKind.Splitter:
                    enemy.gameObject.AddComponent<SplitterBehaviour>().Init(manager, enemy);
                    break;
                case EnemyKind.Healer:
                    foreach (WeaponController w in enemy.GetComponentsInChildren<WeaponController>(true)) w.DelayBetweenShots *= 2f;
                    enemy.gameObject.AddComponent<HealerBehaviour>().Init(manager, enemy);
                    break;
                case EnemyKind.Blinker:
                    enemy.gameObject.AddComponent<BlinkerBehaviour>().Init(manager, enemy);
                    break;
                case EnemyKind.Orbiter:
                    DisableGun(enemy);
                    enemy.gameObject.AddComponent<OrbiterBehaviour>().Init(manager, enemy);
                    break;
                case EnemyKind.Boss:
                    // 플레이어에게 붙지 않고 거리를 두고 탄막을 쏜다
                    if (mobile) mobile.AttackStopDistanceRatio = 1f;
                    if (detection)
                    {
                        detection.DetectionRange = 60f;
                        detection.AttackRange = 22f;
                    }
                    break;
            }

            Decorate(manager, enemy, kind, bounds, me.Color);
            return me;
        }

        public static void ScaleSpeed(EnemyController enemy, float k)
        {
            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent) agent.speed *= k;
            // 막 생성된 적은 EnemyController.Start 에서 NavigationModule 값으로 속도를 덮어쓰므로 함께 바꾼다
            var nav = enemy.GetComponentInChildren<NavigationModule>();
            if (nav) nav.MoveSpeed *= k;
        }

        static void DisableGun(EnemyController enemy)
        {
            foreach (WeaponController w in enemy.GetComponentsInChildren<WeaponController>(true))
                w.DelayBetweenShots = 1e6f;
        }

        static Bounds LocalBounds(Transform root)
        {
            var b = new Bounds(Vector3.up, Vector3.one);
            bool first = true;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Vector3 min = root.InverseTransformPoint(r.bounds.min), max = root.InverseTransformPoint(r.bounds.max);
                if (first) { b = new Bounds((min + max) * 0.5f, Vector3.zero); first = false; }
                b.Encapsulate(min);
                b.Encapsulate(max);
            }

            return b;
        }

        // 종류마다 다른 실루엣이 되도록 장식을 붙인다 (충돌체 없음)
        static void Decorate(MathModeManager m, EnemyController enemy, EnemyKind kind, Bounds b, Color c)
        {
            Transform t = enemy.transform;
            Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z);
            Vector3 front = new Vector3(b.center.x, b.center.y, b.max.z);
            Color glow = c * 1.5f;
            string symbol = null;

            switch (kind)
            {
                case EnemyKind.Rusher: // 앞으로 뻗은 화살표
                    m.CreateBlock(t, PrimitiveType.Cube, front + Vector3.forward * 0.3f, new Vector3(0.12f, 0.12f, 0.8f), glow);
                    var tipA = m.CreateBlock(t, PrimitiveType.Cube, front + new Vector3(0.18f, 0f, 0.55f), new Vector3(0.1f, 0.1f, 0.45f), glow);
                    tipA.transform.localRotation = Quaternion.Euler(0f, -40f, 0f);
                    var tipB = m.CreateBlock(t, PrimitiveType.Cube, front + new Vector3(-0.18f, 0f, 0.55f), new Vector3(0.1f, 0.1f, 0.45f), glow);
                    tipB.transform.localRotation = Quaternion.Euler(0f, 40f, 0f);
                    symbol = "→v";
                    break;
                case EnemyKind.Tank:
                    symbol = "[A]";
                    break;
                case EnemyKind.Sniper: // 긴 총열
                    var barrel = m.CreateBlock(t, PrimitiveType.Cylinder, front + new Vector3(0f, 0.1f, 0.5f), new Vector3(0.08f, 0.6f, 0.08f), glow);
                    barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    symbol = "r∠θ";
                    break;
                case EnemyKind.Splitter:
                    symbol = "½+½";
                    break;
                case EnemyKind.Healer: // 초록 십자
                    m.CreateBlock(t, PrimitiveType.Cube, top + Vector3.up * 0.45f, new Vector3(0.18f, 0.6f, 0.18f), new Color(0.4f, 1.6f, 0.6f));
                    m.CreateBlock(t, PrimitiveType.Cube, top + Vector3.up * 0.5f, new Vector3(0.6f, 0.18f, 0.18f), new Color(0.4f, 1.6f, 0.6f));
                    symbol = "∫";
                    break;
                case EnemyKind.Blinker:
                    symbol = "i";
                    break;
                case EnemyKind.Orbiter: // 주위를 도는 구슬 3개
                {
                    var pivot = new GameObject("Orbit").transform;
                    pivot.SetParent(t, false);
                    pivot.localPosition = b.center;
                    pivot.gameObject.AddComponent<ConstantRotation>().RotatingSpeed = 180f;
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 120f * Mathf.Deg2Rad;
                        m.CreateBlock(pivot, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (b.extents.x + 0.6f), Vector3.one * 0.3f, glow);
                    }
                    symbol = "π";
                    break;
                }
                case EnemyKind.Mini:
                    symbol = "½";
                    break;
                case EnemyKind.Boss: // 왕관 가시
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad;
                        var spike = m.CreateBlock(t, PrimitiveType.Cube, top + new Vector3(Mathf.Cos(a) * 0.3f, 0.1f, Mathf.Sin(a) * 0.3f), new Vector3(0.06f, 0.22f, 0.06f), glow);
                        spike.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 20f, 0f, -Mathf.Cos(a) * 20f);
                    }
                    symbol = "∞";
                    break;
            }

            // 머리 위 이름표 (카메라를 바라봄)
            if (kind != EnemyKind.Drone)
            {
                var tagRoot = new GameObject("NameTag").transform;
                tagRoot.SetParent(t, false);
                tagRoot.localPosition = top + Vector3.up * (kind == EnemyKind.Healer ? 1.1f : kind == EnemyKind.Boss ? 0.55f : 0.6f);
                var text = m.CreateText(tagRoot, Vector3.zero, new Vector2(4f, 0.8f),
                    symbol != null ? $"<b>{symbol}</b>\n<size=55%>{NameOf(kind)}</size>" : $"<size=55%>{NameOf(kind)}</size>", c, 3f);
                text.outlineWidth = 0.25f;
                text.outlineColor = Color.black;
                tagRoot.gameObject.AddComponent<FaceCamera>();
                tagRoot.localScale = new Vector3(1f / t.localScale.x, 1f / t.localScale.y, 1f / t.localScale.z) * (kind == EnemyKind.Boss ? 2.2f : 1f);
            }
        }

        void Update()
        {
            // 새로운 종류의 적을 처음 만나면 특징을 알려 준다
            if (Kind == EnemyKind.Drone || Kind == EnemyKind.Mini || Kind == EnemyKind.Boss || s_Announced.Contains(Kind)) return;
            MathModeManager m = MathModeManager.Instance;
            if (!m || !m.Player || Vector3.Distance(m.Player.position, transform.position) > 28f) return;
            s_Announced.Add(Kind);
            MathModeManager.ShowMessage($"새로운 적 '{NameOf(Kind)}': {TipOf(Kind)}");
        }
    }

    // 항상 카메라를 바라보는 이름표
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }

    // ================================================================== 적 공용 탄

    // 플레이어만 맞히는 느린 탄 (벽에 닿으면 사라짐). 피할 수 있도록 눈에 잘 띄게 크고 밝다.
    public class EnemyBullet : MonoBehaviour
    {
        Vector3 m_Velocity;
        float m_Damage, m_Radius, m_Life;
        GameObject m_Owner;

        public static EnemyBullet Fire(MathModeManager m, GameObject owner, Vector3 position, Vector3 velocity, float damage, Color color, float size = 0.45f)
        {
            GameObject go = m.CreateBlock(null, PrimitiveType.Sphere, position, Vector3.one * size, color * 1.8f);
            go.name = "EnemyBullet";
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.widthMultiplier = size * 0.8f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            if (m.LineMaterial) trail.sharedMaterial = m.LineMaterial;
            trail.startColor = color * 1.5f;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var bullet = go.AddComponent<EnemyBullet>();
            bullet.m_Velocity = velocity;
            bullet.m_Damage = damage;
            bullet.m_Radius = size * 0.5f;
            bullet.m_Owner = owner;
            bullet.m_Life = 7f;
            return bullet;
        }

        void Update()
        {
            m_Life -= Time.deltaTime;
            if (m_Life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 step = m_Velocity * Time.deltaTime;
            foreach (RaycastHit hit in Physics.SphereCastAll(transform.position, m_Radius, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<EnemyController>()) continue;
                if (m_Owner && hit.collider.transform.IsChildOf(m_Owner.transform)) continue;
                if (hit.collider.GetComponentInParent<PlayerCharacterController>())
                {
                    var damageable = hit.collider.GetComponentInParent<Damageable>();
                    if (damageable) damageable.InflictDamage(m_Damage, false, m_Owner);
                }

                Destroy(gameObject);
                return;
            }

            transform.position += step;
        }
    }

    // ================================================================== 종류별 행동

    public abstract class EnemyBehaviour : MonoBehaviour
    {
        protected MathModeManager m_Manager;
        protected EnemyController m_Enemy;
        protected Health m_Health;

        public void Init(MathModeManager manager, EnemyController enemy)
        {
            m_Manager = manager;
            m_Enemy = enemy;
            m_Health = enemy.GetComponent<Health>();
            OnInit();
        }

        protected virtual void OnInit() { }

        protected Transform Player => m_Manager ? m_Manager.Player : null;
        protected bool Alive => m_Health && m_Health.CurrentHealth > 0f;

        // 적의 눈에서 플레이어 몸통까지 막힘이 없는지
        protected bool CanSee(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            foreach (RaycastHit hit in Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<PlayerCharacterController>()) continue;
                if (hit.collider.GetComponentInParent<EnemyController>()) continue;
                return false;
            }

            return true;
        }

        protected Vector3 PlayerChest => Player.position + Vector3.up * 1.1f;
        protected Vector3 Eye => transform.position + Vector3.up * 1.2f * transform.localScale.y;
    }

    // 벡터 돌격병: 가까이 오면 0.6초 깜빡이다 자폭
    public class RusherBehaviour : EnemyBehaviour
    {
        bool m_Armed;

        void Update()
        {
            if (m_Armed || !Alive || !Player) return;
            if (Vector3.Distance(transform.position, Player.position) < 3.2f)
                StartCoroutine(Explode());
        }

        IEnumerator Explode()
        {
            m_Armed = true;
            var warn = m_Manager.CreateBlock(transform, PrimitiveType.Sphere, Vector3.up, Vector3.one * 1.5f, new Color(2f, 0.6f, 0.1f));
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                if (!Alive) { Destroy(warn); yield break; }
                warn.SetActive(Mathf.Repeat(t * 12f, 1f) < 0.5f);
                yield return null;
            }

            if (!Alive) yield break;
            Vector3 c = transform.position + Vector3.up;
            if (m_Manager.ExplosionVfx) Destroy(Instantiate(m_Manager.ExplosionVfx, c, Quaternion.identity), 3f);
            if (Player && Vector3.Distance(Player.position, transform.position) < 4.5f)
            {
                var damageable = Player.GetComponent<Damageable>();
                if (damageable) damageable.InflictDamage(28f, false, gameObject);
                if (m_Manager.CameraFX) m_Manager.CameraFX.Shake(0.7f, 0.3f);
            }

            m_Health.Kill();
        }
    }

    // 행렬 탱커: 앞쪽의 방패. 판정은 보이지 않는 상자, 겉모습은 행렬 모양 격자 선이라 탱커 몸이 비쳐 보인다.
    // 방패에도 체력이 있어 부술 수 있다.
    public class TankShield : MonoBehaviour
    {
        public void Init(MathModeManager m, EnemyController enemy, Bounds b, Color color)
        {
            const float width = 1.9f, height = 1.6f;
            var shield = new GameObject("Shield");
            shield.transform.SetParent(enemy.transform, false);
            shield.transform.localPosition = new Vector3(0f, 1.0f, Mathf.Max(b.max.z, 0.6f) + 0.35f);
            shield.AddComponent<OutlineIgnore>();
            var box = shield.AddComponent<BoxCollider>();
            box.size = new Vector3(width, height, 0.15f);
            box.isTrigger = true; // 적의 시야·이동은 막지 않고 플레이어 탄만 막는다

            // 격자 3×3 (행렬 모양)
            var lines = new List<LineRenderer>();
            Color c = new Color(0.4f, 0.9f, 1.8f);
            for (int i = 0; i <= 3; i++)
            {
                float x = Mathf.Lerp(-width / 2f, width / 2f, i / 3f), y = Mathf.Lerp(-height / 2f, height / 2f, i / 3f);
                float w = i == 0 || i == 3 ? 0.07f : 0.03f;
                lines.Add(ShieldLine(m, shield.transform, c, w, new Vector3(x, -height / 2f, 0f), new Vector3(x, height / 2f, 0f)));
                lines.Add(ShieldLine(m, shield.transform, c, w, new Vector3(-width / 2f, y, 0f), new Vector3(width / 2f, y, 0f)));
            }

            var health = shield.AddComponent<Health>();
            health.MaxHealth = 140f * (m.Preset != null ? m.Preset.EnemyHealth : 1f);
            shield.AddComponent<Damageable>();
            health.OnDie += () =>
            {
                MathModeManager.ShowMessage("행렬 탱커의 방패 파괴!");
                if (m.ExplosionVfx) Destroy(Instantiate(m.ExplosionVfx, shield.transform.position, Quaternion.identity), 3f);
                Destroy(shield);
            };
            // 맞을수록 파랑 → 빨강
            health.OnDamaged += (d, s) =>
            {
                float k = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 0f;
                Color now = Color.Lerp(new Color(1.8f, 0.3f, 0.2f), c, k);
                foreach (LineRenderer l in lines)
                    if (l) l.startColor = l.endColor = now;
            };
        }

        static LineRenderer ShieldLine(MathModeManager m, Transform parent, Color color, float width, Vector3 a, Vector3 b)
        {
            LineRenderer line = m.CreateLine(parent, color, width);
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.material.SetColor("_BaseColor", Color.white);
            return line;
        }
    }

    // 극좌표 저격수: 1.4초 동안 빨간 조준선으로 플레이어를 따라가다, 마지막 0.35초에 고정하고 발사
    public class SniperBehaviour : EnemyBehaviour
    {
        float m_Next;

        protected override void OnInit() => m_Next = Time.time + Random.Range(2f, 4f);

        void Update()
        {
            if (!Alive || !Player || Time.time < m_Next) return;
            if (Vector3.Distance(transform.position, Player.position) > 45f || !CanSee(Eye, PlayerChest)) return;
            m_Next = Time.time + 5.5f;
            StartCoroutine(Shoot());
        }

        IEnumerator Shoot()
        {
            LineRenderer line = m_Manager.CreateLine(null, new Color(1.5f, 0.1f, 0.2f), 0.04f);
            line.positionCount = 2;
            Vector3 dir = (PlayerChest - Eye).normalized;
            const float aim = 1.4f, lockTime = 0.35f;
            for (float t = 0f; t < aim; t += Time.deltaTime)
            {
                if (!Alive || !Player) { Destroy(line.gameObject); yield break; }
                if (t < aim - lockTime) dir = (PlayerChest - Eye).normalized;
                else line.startColor = line.endColor = Mathf.Repeat(t * 16f, 1f) < 0.5f ? new Color(2f, 1.5f, 0.3f) : new Color(1.5f, 0.1f, 0.2f);
                line.widthMultiplier = Mathf.Lerp(0.02f, 0.06f, t / aim);
                // 플레이어 얼굴 바로 앞에서 끊어 화면을 가리지 않게 한다
                float reach = Mathf.Max(1f, Vector3.Distance(Eye, PlayerChest) - 2.5f);
                line.SetPosition(0, Eye);
                line.SetPosition(1, Eye + dir * reach);
                yield return null;
            }

            // 발사: 고정된 방향으로 한 줄 판정
            Vector3 end = Eye + dir * 60f;
            foreach (RaycastHit hit in SortedHits(Eye, dir, 60f))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<EnemyController>()) continue;
                end = hit.point;
                var damageable = hit.collider.GetComponentInParent<Damageable>();
                if (damageable && hit.collider.GetComponentInParent<PlayerCharacterController>())
                {
                    damageable.InflictDamage(32f, false, gameObject);
                    if (m_Manager.CameraFX) m_Manager.CameraFX.Flash(new Color(1f, 0.1f, 0.1f, 0.3f), 0.3f);
                }

                break;
            }

            line.startColor = line.endColor = new Color(2f, 1.8f, 1.6f);
            line.SetPosition(1, end);
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                line.widthMultiplier = 0.15f * (1f - t / 0.2f);
                yield return null;
            }

            Destroy(line.gameObject);
        }

        static RaycastHit[] SortedHits(Vector3 from, Vector3 dir, float length)
        {
            RaycastHit[] hits = Physics.RaycastAll(from, dir, length, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return hits;
        }
    }

    // 분수 분열체: 쓰러지면 ½ 조각 두 마리
    public class SplitterBehaviour : EnemyBehaviour
    {
        protected override void OnInit()
        {
            m_Health.OnDie += () =>
            {
                if (!m_Manager) return;
                MathModeManager.ShowMessage("1 = ½ + ½  분열!");
                for (int i = 0; i < 2; i++)
                    m_Manager.SpawnEnemy(EnemyKind.Mini, transform.position + transform.right * (i == 0 ? 1.5f : -1.5f), 2f);
            };
        }
    }

    // 적분 치유체: 2.5초마다 반지름 12m 안의 다친 적을 최대 체력의 20%만큼 회복
    public class HealerBehaviour : EnemyBehaviour
    {
        float m_Next;
        EnemyManager m_EnemyManager;

        protected override void OnInit()
        {
            m_EnemyManager = FindAnyObjectByType<EnemyManager>();
            m_Next = Time.time + 2.5f;
        }

        void Update()
        {
            if (!Alive || Time.time < m_Next || !m_EnemyManager) return;
            m_Next = Time.time + 2.5f;
            var targets = new List<Health>();
            foreach (EnemyController e in m_EnemyManager.Enemies)
            {
                if (!e || e == m_Enemy || Vector3.Distance(e.transform.position, transform.position) > 12f) continue;
                var h = e.GetComponent<Health>();
                if (h && h.CurrentHealth < h.MaxHealth && h.CurrentHealth > 0f) targets.Add(h);
            }

            if (targets.Count > 0) StartCoroutine(HealBeams(targets));
        }

        IEnumerator HealBeams(List<Health> targets)
        {
            var lines = new List<(LineRenderer line, Transform target)>();
            foreach (Health h in targets)
            {
                h.Heal(h.MaxHealth * 0.2f);
                LineRenderer l = m_Manager.CreateLine(null, new Color(0.4f, 1.8f, 0.6f), 0.12f);
                l.positionCount = 2;
                lines.Add((l, h.transform));
            }

            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                foreach (var (l, target) in lines)
                {
                    if (!l) continue;
                    l.SetPosition(0, transform.position + Vector3.up * 1.5f);
                    l.SetPosition(1, target ? target.position + Vector3.up : l.GetPosition(1));
                    l.widthMultiplier = 0.15f * (1f - t / 0.5f);
                }

                yield return null;
            }

            foreach (var (l, _) in lines) if (l) Destroy(l.gameObject);
        }
    }

    // 허수 i: 플레이어를 발견하면 4.5초마다 플레이어 주변 7~12m 의 다른 곳으로 순간이동
    public class BlinkerBehaviour : EnemyBehaviour
    {
        float m_Next;

        protected override void OnInit() => m_Next = Time.time + 4.5f;

        void Update()
        {
            if (!Alive || !Player || Time.time < m_Next || !m_Enemy.KnownDetectedTarget) return;
            m_Next = Time.time + 4.5f;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(7f, 12f);
                Vector3 candidate = Player.position + new Vector3(r.x, 0f, r.y);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
                if (m_Manager.World != null && !m_Manager.World.IsReachable(Player.position, hit.position)) continue;
                StartCoroutine(Ring(transform.position));
                var agent = m_Enemy.NavMeshAgent;
                if (agent && agent.isOnNavMesh) agent.Warp(hit.position);
                else transform.position = hit.position;
                StartCoroutine(Ring(hit.position));
                break;
            }
        }

        IEnumerator Ring(Vector3 at)
        {
            LineRenderer ring = m_Manager.CreateLine(null, new Color(0.3f, 1.8f, 1.8f), 0.12f);
            ring.loop = true;
            ring.positionCount = 32;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                float r = Mathf.Lerp(0.3f, 2.5f, t / 0.4f);
                for (int i = 0; i < 32; i++)
                {
                    float a = i / 32f * Mathf.PI * 2f;
                    ring.SetPosition(i, at + new Vector3(Mathf.Cos(a) * r, 1f, Mathf.Sin(a) * r));
                }

                ring.widthMultiplier = 0.15f * (1f - t / 0.4f);
                yield return null;
            }

            Destroy(ring.gameObject);
        }
    }

    // 원주율 π: 3초마다 몸 둘레로 10발의 원형 탄막 (발사할 때마다 각도가 조금씩 돈다)
    public class OrbiterBehaviour : EnemyBehaviour
    {
        float m_Next, m_Phase;

        protected override void OnInit() => m_Next = Time.time + Random.Range(1.5f, 3f);

        void Update()
        {
            if (!Alive || !Player || Time.time < m_Next) return;
            if (Vector3.Distance(transform.position, Player.position) > 30f || !CanSee(Eye, PlayerChest)) return;
            m_Next = Time.time + 3f;
            m_Phase += 18f;
            Vector3 origin = transform.position + Vector3.up * 1.1f;
            const int count = 10;
            for (int i = 0; i < count; i++)
            {
                float a = (m_Phase + 360f * i / count) * Mathf.Deg2Rad;
                EnemyBullet.Fire(m_Manager, gameObject, origin, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 9f, 9f, new Color(1f, 0.35f, 0.8f));
            }
        }
    }

    // 테두리를 붙이지 않을 오브젝트 표시 (방패 등)
    public class OutlineIgnore : MonoBehaviour { }

    // 적 테두리: 메시마다 테두리 셰이더를 쓰는 복제 렌더러를 자식으로 붙인다
    public static class EnemyOutline
    {
        static Shader s_Shader;
        static readonly Dictionary<Color, Material> s_Materials = new Dictionary<Color, Material>();

        public static bool Apply(GameObject root, Color color, float width = 3f)
        {
            if (!s_Shader) s_Shader = Resources.Load<Shader>("MathMode_Outline");
            if (!s_Shader || !s_Shader.isSupported) return false;
            if (!s_Materials.TryGetValue(color, out Material mat) || !mat)
            {
                mat = new Material(s_Shader) { name = "EnemyOutline" };
                mat.SetColor("_OutlineColor", color);
                mat.SetColor("_XRayColor", new Color(color.r, color.g, color.b, 0.35f));
                mat.SetFloat("_OutlineWidth", width);
                s_Materials[color] = mat;
            }

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (r.name == "Outline" || r.name.StartsWith("Outline_") || r.GetComponentInParent<OutlineIgnore>()) continue;
                if (r.GetComponent<TMP_SubMesh>() || r.GetComponentInParent<TMP_Text>()) continue;
                if (r.transform.Find("Outline") || (r.transform.parent && r.transform.parent.Find("Outline_" + r.name))) continue;

                Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null;
                if (!mesh) continue;
                var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;

                Renderer outline;
                if (r is SkinnedMeshRenderer skinned)
                {
                    // 뼈대 애니메이션을 그대로 따라가도록 같은 뼈를 쓰는 복제본
                    var go = new GameObject("Outline_" + r.name) { layer = r.gameObject.layer };
                    go.transform.SetParent(r.transform.parent, false);
                    go.transform.localPosition = r.transform.localPosition;
                    go.transform.localRotation = r.transform.localRotation;
                    go.transform.localScale = r.transform.localScale;
                    var copy = go.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = mesh;
                    copy.bones = skinned.bones;
                    copy.rootBone = skinned.rootBone;
                    copy.localBounds = skinned.localBounds;
                    copy.updateWhenOffscreen = skinned.updateWhenOffscreen;
                    outline = copy;
                }
                else
                {
                    var go = new GameObject("Outline") { layer = r.gameObject.layer };
                    go.transform.SetParent(r.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    outline = go.AddComponent<MeshRenderer>();
                }

                outline.sharedMaterials = mats;
                outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outline.receiveShadows = false;
                outline.enabled = r.enabled;
            }

            return true;
        }
    }
}
