using System.Collections;
using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.MathMode
{
    // 특수 기믹 무기 6종. 게임 시작 시 기본 Blaster를 복제해 코드로 만든다 (에디터 메뉴를 다시 실행할 필요 없음).
    //  베지에 유도탄   : 4발이 흩어졌다가 2차 베지에 곡선을 따라 조준한 적에게 휘어 들어간다
    //  프랙탈 분열탄   : 6m마다 3갈래로 갈라진다 (1 → 3 → 9발, 시에르핀스키 나무)
    //  극한 블랙홀포   : 맞은 자리에 lim 특이점을 만들어 주변 적을 빨아들인 뒤 폭발
    //  그래프 연쇄 번개: 맞힌 적에서 가까운 적으로 간선을 이어 최대 5마리까지 번개가 튄다
    //  리사주 부메랑   : 8자(리사주 곡선)를 그리며 날아갔다 돌아오며 관통
    //  홍련 초승달 참격: 붉은 초승달 검기가 앞으로 날아가며 닿는 모든 적을 벤다
    public static class MathSpecialWeapons
    {
        struct Def
        {
            public string Name;
            public SpecialProjectile.Kind Kind;
            public WeaponShootType ShootType;
            public float Delay, Reload, Damage, Speed, Range;
            public int Bullets, MaxAmmo;
            public Color Color;
        }

        static readonly Def[] k_Defs =
        {
            new Def { Name = "베지에 유도탄", Kind = SpecialProjectile.Kind.Bezier, ShootType = WeaponShootType.Manual, Delay = 0.55f, Bullets = 4, MaxAmmo = 6, Reload = 2f, Damage = 13f, Speed = 32f, Range = 90f, Color = new Color(0.3f, 0.9f, 1f) },
            new Def { Name = "프랙탈 분열탄", Kind = SpecialProjectile.Kind.Fractal, ShootType = WeaponShootType.Manual, Delay = 0.5f, Bullets = 1, MaxAmmo = 8, Reload = 2.5f, Damage = 11f, Speed = 45f, Range = 60f, Color = new Color(0.55f, 1f, 0.3f) },
            new Def { Name = "극한 블랙홀포", Kind = SpecialProjectile.Kind.Singularity, ShootType = WeaponShootType.Manual, Delay = 1.3f, Bullets = 1, MaxAmmo = 3, Reload = 0.6f, Damage = 75f, Speed = 24f, Range = 35f, Color = new Color(0.6f, 0.3f, 1f) },
            new Def { Name = "그래프 연쇄 번개", Kind = SpecialProjectile.Kind.Chain, ShootType = WeaponShootType.Automatic, Delay = 0.35f, Bullets = 1, MaxAmmo = 12, Reload = 4f, Damage = 20f, Speed = 0f, Range = 120f, Color = new Color(0.6f, 0.8f, 1f) },
            new Def { Name = "리사주 부메랑", Kind = SpecialProjectile.Kind.Lissajous, ShootType = WeaponShootType.Manual, Delay = 0.7f, Bullets = 1, MaxAmmo = 4, Reload = 1.5f, Damage = 22f, Speed = 0f, Range = 22f, Color = new Color(1f, 0.85f, 0.3f) },
            new Def { Name = "홍련 초승달 참격", Kind = SpecialProjectile.Kind.Crescent, ShootType = WeaponShootType.Manual, Delay = 0.75f, Bullets = 1, MaxAmmo = 5, Reload = 1.6f, Damage = 48f, Speed = 32f, Range = 27f, Color = new Color(1f, 0.22f, 0.18f) },
        };

        // 복제 원본(템플릿)들은 꺼진 부모 아래에 두어 스스로 움직이지 않게 한다
        public static List<WeaponController> CreateTemplates(MathModeManager manager, WeaponController baseWeapon)
        {
            var result = new List<WeaponController>();
            if (!baseWeapon) return result;

            var holder = new GameObject("SpecialWeaponTemplates");
            holder.transform.SetParent(manager.transform, false);
            holder.SetActive(false);

            foreach (Def def in k_Defs)
            {
                // 투사체 템플릿
                var proj = new GameObject($"Projectile_{def.Kind}");
                proj.transform.SetParent(holder.transform, false);
                var sp = proj.AddComponent<SpecialProjectile>();
                sp.Type = def.Kind;
                sp.Damage = def.Damage;
                sp.Speed = def.Speed;
                sp.Range = def.Range;
                sp.Color = def.Color;
                float size = def.Kind == SpecialProjectile.Kind.Singularity ? 0.6f : 0.22f;
                if (def.Kind != SpecialProjectile.Kind.Chain && def.Kind != SpecialProjectile.Kind.Crescent)
                {
                    GameObject core = manager.CreateBlock(proj.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * size, def.Color * 1.8f);
                    core.name = "Core";
                    var trail = proj.AddComponent<TrailRenderer>();
                    trail.time = def.Kind == SpecialProjectile.Kind.Lissajous ? 0.6f : 0.35f;
                    trail.minVertexDistance = 0.05f;
                    trail.widthMultiplier = size * 0.7f;
                    trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                    if (manager.LineMaterial) trail.sharedMaterial = manager.LineMaterial;
                    trail.startColor = def.Color * 1.6f;
                    trail.endColor = new Color(def.Color.r, def.Color.g, def.Color.b, 0f);
                    trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                // 무기 템플릿
                WeaponController weapon = Object.Instantiate(baseWeapon, holder.transform);
                weapon.name = $"Weapon_Special_{def.Kind}";
                weapon.WeaponName = def.Name;
                weapon.ProjectilePrefab = sp;
                weapon.ShootType = def.ShootType;
                weapon.DelayBetweenShots = def.Delay;
                weapon.BulletsPerShot = def.Bullets;
                weapon.BulletSpreadAngle = 0f;
                weapon.MaxAmmo = def.MaxAmmo;
                weapon.AmmoReloadRate = def.Reload;
                Tint(weapon, def.Color);
                Decorate(manager, weapon, def.Color);
                result.Add(weapon);
            }

            return result;
        }

        static void Tint(WeaponController weapon, Color color)
        {
            Transform root = weapon.WeaponRoot ? weapon.WeaponRoot.transform : weapon.transform;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i] || mats[i].renderQueue >= 2450 || !mats[i].HasProperty("_BaseColor")) continue;
                    var m = new Material(mats[i]);
                    m.SetColor("_BaseColor", Color.Lerp(mats[i].GetColor("_BaseColor"), color, 0.45f));
                    mats[i] = m;
                }

                r.sharedMaterials = mats;
            }
        }

        // 총구에 빛나는 고리 두 개 (특수 무기 표시)
        static void Decorate(MathModeManager manager, WeaponController weapon, Color color)
        {
            Transform root = weapon.WeaponRoot ? weapon.WeaponRoot.transform : weapon.transform;
            if (!weapon.WeaponMuzzle) return;
            Vector3 m = root.InverseTransformPoint(weapon.WeaponMuzzle.position);
            for (int i = 0; i < 2; i++)
            {
                GameObject ring = manager.CreateBlock(root, PrimitiveType.Cylinder, m - Vector3.forward * (0.04f + 0.07f * i),
                    new Vector3(0.09f, 0.006f, 0.09f), color * 1.8f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }

    // 특수 무기의 투사체. 움직임과 판정을 모두 직접 처리한다.
    public class SpecialProjectile : ProjectileBase
    {
        public enum Kind { Bezier, Fractal, Singularity, Chain, Lissajous, Crescent }

        public Kind Type;
        public float Damage = 10f;
        public float Speed = 30f;
        public float Range = 50f;
        public Color Color = Color.white;
        public int Generation;

        static int s_Frame = -1, s_Index;

        GameObject m_Owner;
        Vector3 m_Dir, m_Right, m_Up, m_Start;
        float m_Age, m_Traveled;
        bool m_Active;
        int m_IndexInShot;
        readonly HashSet<Health> m_Hit = new HashSet<Health>();

        // 베지에
        Vector3 m_P0, m_P1, m_P2;
        float m_BezierTime;
        Transform m_Target;

        // 참격
        LineRenderer m_ArcOuter, m_ArcInner;

        public string Formula => Type switch
        {
            Kind.Bezier => "B(t) = (1-t)²P0 + 2t(1-t)P1 + t²P2  (4발 유도)",
            Kind.Fractal => "1 → 3 → 9  (6m마다 3갈래, 시에르핀스키 나무)",
            Kind.Singularity => "lim x→a f(x): 주변 적을 한 점으로 끌어당긴 뒤 폭발",
            Kind.Chain => "그래프 G = (V, E): 적에서 적으로 최대 5개 꼭짓점",
            Kind.Lissajous => "(x, y) = (A sin 2t, B sin 4t) 왕복 관통",
            _ => "y = -kx² 초승달 검기, 닿는 모든 적 관통",
        };

        void OnEnable() => OnShoot += HandleShoot;
        void OnDisable() => OnShoot -= HandleShoot;

        void HandleShoot()
        {
            if (Time.frameCount != s_Frame)
            {
                s_Frame = Time.frameCount;
                s_Index = 0;
            }

            m_IndexInShot = s_Index++;
            m_Owner = Owner;
            Transform cam = AimCamera();
            Vector3 aimPoint = AimPoint(cam);
            m_Dir = (aimPoint - transform.position).normalized;
            m_Up = Vector3.up;
            m_Right = Vector3.Cross(Vector3.up, m_Dir).normalized;
            if (m_Right.sqrMagnitude < 0.01f) m_Right = cam ? cam.right : Vector3.right;
            m_Start = transform.position;
            m_Active = true;

            switch (Type)
            {
                case Kind.Bezier:
                {
                    m_Target = FindTarget(cam);
                    float a = m_IndexInShot * Mathf.PI * 0.5f + 0.4f;
                    Vector3 camRight = cam ? cam.right : m_Right, camUp = cam ? cam.up : Vector3.up;
                    m_P0 = transform.position;
                    m_P1 = m_P0 + m_Dir * 6f + (camRight * Mathf.Cos(a) + camUp * Mathf.Sin(a)) * 4.5f;
                    m_P2 = m_Target ? m_Target.position : aimPoint;
                    break;
                }
                case Kind.Chain:
                    StartCoroutine(ChainLightning(cam));
                    break;
                case Kind.Crescent:
                    m_ArcOuter = MakeArcLine(Color * 1.8f, 0.4f);
                    m_ArcInner = MakeArcLine(new Color(2f, 1.8f, 1.6f), 0.12f);
                    break;
            }
        }

        // 분열탄의 자식 탄
        public void InitChild(GameObject owner, Vector3 position, Vector3 dir, int generation)
        {
            m_Owner = owner;
            transform.position = position;
            m_Dir = dir.normalized;
            m_Right = Vector3.Cross(Vector3.up, m_Dir).normalized;
            Generation = generation;
            m_Start = position;
            m_Active = true;
            Transform core = transform.Find("Core");
            if (core) core.GetComponent<Renderer>().material.SetColor("_BaseColor", Color.Lerp(Color, Color.white, generation * 0.3f) * 1.8f);
        }

        Transform AimCamera()
        {
            var weapons = m_Owner ? m_Owner.GetComponent<PlayerWeaponsManager>() : null;
            return weapons && weapons.WeaponCamera ? weapons.WeaponCamera.transform : Camera.main ? Camera.main.transform : null;
        }

        Vector3 AimPoint(Transform cam)
        {
            if (!cam) return transform.position + transform.forward * Range;
            return Cast(cam.position, cam.forward, 300f, 0.05f, out RaycastHit hit) ? hit.point : cam.position + cam.forward * 300f;
        }

        // 화면 가운데에서 35° 안에 있는, 보이는 가장 가까운 적의 조준점
        Transform FindTarget(Transform cam)
        {
            var manager = FindAnyObjectByType<EnemyManager>();
            if (!cam || !manager) return null;
            Transform best = null;
            float bestScore = float.MaxValue;
            foreach (EnemyController e in manager.Enemies)
            {
                if (!e) continue;
                Transform aim = AimPointOf(e);
                Vector3 to = aim.position - cam.position;
                float angle = Vector3.Angle(cam.forward, to);
                if (angle > 35f || to.magnitude > Range) continue;
                float score = angle * 2f + to.magnitude * 0.3f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = aim;
                }
            }

            return best;
        }

        public static Transform AimPointOf(EnemyController e)
        {
            var actor = e.GetComponent<Actor>();
            return actor && actor.AimPoint ? actor.AimPoint : e.transform;
        }

        void Update()
        {
            if (!m_Active) return;
            m_Age += Time.deltaTime;

            switch (Type)
            {
                case Kind.Bezier: UpdateBezier(); break;
                case Kind.Fractal: UpdateFractal(); break;
                case Kind.Singularity: UpdateStraight(0.5f, true); break;
                case Kind.Lissajous: UpdateLissajous(); break;
                case Kind.Crescent: UpdateCrescent(); break;
            }
        }

        // ------------------------------------------------------------------ 이동 + 판정

        void UpdateStraight(float radius, bool singularity)
        {
            Vector3 from = transform.position;
            Vector3 step = m_Dir * Speed * Time.deltaTime;
            if (Cast(from, m_Dir, step.magnitude, radius, out RaycastHit hit))
            {
                transform.position = hit.point;
                if (singularity)
                {
                    HitDamage(hit.collider, Damage * 0.3f);
                    BeginVortex(hit.point - m_Dir * 0.5f);
                }
                else
                {
                    HitDamage(hit.collider, Damage);
                    Destroy(gameObject);
                }

                return;
            }

            transform.position = from + step;
            m_Traveled += step.magnitude;
            if (m_Traveled > Range)
            {
                if (singularity) BeginVortex(transform.position);
                else Destroy(gameObject);
            }
        }

        void UpdateBezier()
        {
            float length = Vector3.Distance(m_P0, m_P1) + Vector3.Distance(m_P1, m_P2);
            if (m_BezierTime < 1f)
            {
                if (m_Target) m_P2 = m_Target.position;
                m_BezierTime = Mathf.Min(1f, m_BezierTime + Speed * Time.deltaTime / Mathf.Max(length, 1f));
                float t = m_BezierTime, u = 1f - t;
                Vector3 next = u * u * m_P0 + 2f * u * t * m_P1 + t * t * m_P2;
                Vector3 tangent = 2f * u * (m_P1 - m_P0) + 2f * t * (m_P2 - m_P1);
                if (tangent.sqrMagnitude > 0.001f) m_Dir = tangent.normalized;
                Vector3 d = next - transform.position;
                if (Cast(transform.position, d.normalized, d.magnitude, 0.3f, out RaycastHit hit))
                {
                    HitDamage(hit.collider, Damage);
                    Destroy(gameObject);
                    return;
                }

                transform.position = next;
                if (m_Age > 4f) Destroy(gameObject);
                return;
            }

            UpdateStraight(0.3f, false);
        }

        void UpdateFractal()
        {
            if (Generation < 2 && m_Traveled >= 6f)
            {
                foreach (float angle in new[] { -24f, 24f })
                {
                    var child = Instantiate(this, transform.position, Quaternion.identity);
                    child.InitChild(m_Owner, transform.position, Quaternion.AngleAxis(angle, Vector3.up) * m_Dir, Generation + 1);
                }

                Generation++;
                m_Traveled = 0f;
                InitChild(m_Owner, transform.position, m_Dir, Generation);
            }

            UpdateStraight(0.2f, false);
            if (m_Age > 3f && this) Destroy(gameObject);
        }

        // 8자 리사주 곡선: 앞으로 갔다가(사인 반주기) 플레이어에게 돌아온다
        void UpdateLissajous()
        {
            const float duration = 1.5f, width = 3f;
            float tau = Mathf.Clamp01(m_Age / duration);
            if (tau > 0.5f && m_Traveled < 1f)
            {
                m_Traveled = 1f; // 돌아오는 길에는 같은 적을 다시 맞힐 수 있다
                m_Hit.Clear();
            }

            Vector3 home = m_Owner ? m_Owner.transform.position + Vector3.up * 1.3f : m_Start;
            Vector3 origin = Vector3.Lerp(m_Start, home, tau);
            Vector3 next = origin + m_Dir * (Range * Mathf.Sin(Mathf.PI * tau)) + m_Right * (width * Mathf.Sin(2f * Mathf.PI * tau)) +
                           Vector3.up * (0.8f * Mathf.Sin(4f * Mathf.PI * tau));
            PierceAlong(transform.position, next, 0.5f, Damage);
            if (!this) return;
            transform.position = next;
            transform.Rotate(0f, 900f * Time.deltaTime, 0f);
            if (tau >= 1f) Destroy(gameObject);
        }

        // 초승달 검기: 앞으로 날아가며 점점 넓어진다
        void UpdateCrescent()
        {
            float life = Range / Mathf.Max(Speed, 1f);
            float k = Mathf.Clamp01(m_Age / life);
            Vector3 center = m_Start + m_Dir * (Speed * m_Age);
            float half = Mathf.Lerp(2f, 4f, k);
            DrawArc(m_ArcOuter, center, half, 0.4f * (1f - k * 0.6f));
            DrawArc(m_ArcInner, center + m_Dir * 0.1f, half * 0.85f, 0.12f * (1f - k * 0.6f));

            // 활 모양 판정: 양 끝과 가운데를 잇는 캡슐 두 개
            Vector3 left = center - m_Right * half - m_Dir * 0.8f, right = center + m_Right * half - m_Dir * 0.8f;
            foreach (Collider col in Physics.OverlapCapsule(left, center, 1f, ~0, QueryTriggerInteraction.Collide))
                if (!PierceHit(col, Damage)) return;
            foreach (Collider col in Physics.OverlapCapsule(center, right, 1f, ~0, QueryTriggerInteraction.Collide))
                if (!PierceHit(col, Damage)) return;

            if (k >= 1f) Finish();
        }

        LineRenderer MakeArcLine(Color color, float width)
        {
            var go = new GameObject("Arc");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 17;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MathModeManager m = MathModeManager.Instance;
            if (m && m.LineMaterial) line.material = m.LineMaterial;
            line.startColor = line.endColor = color;
            return line;
        }

        void DrawArc(LineRenderer line, Vector3 center, float half, float width)
        {
            if (!line) return;
            line.widthMultiplier = width;
            for (int i = 0; i < line.positionCount; i++)
            {
                float x = Mathf.Lerp(-1f, 1f, i / (float)(line.positionCount - 1));
                line.SetPosition(i, center + m_Right * (x * half) - m_Dir * (x * x * 1.2f) + Vector3.up * (x * 0.3f));
            }
        }

        void Finish()
        {
            if (m_ArcOuter) Destroy(m_ArcOuter.gameObject);
            if (m_ArcInner) Destroy(m_ArcInner.gameObject);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (m_ArcOuter) Destroy(m_ArcOuter.gameObject);
            if (m_ArcInner) Destroy(m_ArcInner.gameObject);
        }

        // 관통 무기: 지나가는 길의 모든 적을 한 번씩. 관문 표적에 닿으면 그 표적만 고르고 사라진다.
        void PierceAlong(Vector3 from, Vector3 to, float radius, float damage)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 1e-6f) return;
            foreach (RaycastHit hit in Physics.SphereCastAll(from, radius, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Collide))
                if (!PierceHit(hit.collider, damage))
                    return;
        }

        // false 를 돌려주면 투사체가 사라졌다는 뜻
        bool PierceHit(Collider col, float damage)
        {
            if (!IsValid(col)) return true;
            var damageable = FindDamageable(col);
            if (!damageable || !damageable.Health) return true;
            if (col.GetComponent<MathAnswerTarget>())
            {
                damageable.InflictDamage(damage, false, m_Owner);
                Finish();
                return false;
            }

            if (m_Hit.Add(damageable.Health))
                damageable.InflictDamage(damage, false, m_Owner);
            return true;
        }

        // ------------------------------------------------------------------ 블랙홀

        void BeginVortex(Vector3 center)
        {
            m_Active = false;
            var trail = GetComponent<TrailRenderer>();
            if (trail) trail.emitting = false;
            StartCoroutine(Vortex(center));
        }

        IEnumerator Vortex(Vector3 center)
        {
            MathModeManager m = MathModeManager.Instance;
            transform.position = center;
            Transform core = transform.Find("Core");
            var rings = new List<LineRenderer>();
            if (m)
                for (int i = 0; i < 3; i++)
                {
                    LineRenderer ring = m.CreateLine(transform, Color * (1.5f + i * 0.3f), 0.08f);
                    ring.loop = true;
                    ring.positionCount = 36;
                    rings.Add(ring);
                }

            TMPro.TextMeshPro label = m ? m.CreateText(transform, Vector3.up * 2.2f, new Vector2(4f, 1f), "<b>lim x→a</b>", Color, 3f) : null;
            const float duration = 2.4f, pullRadius = 10f;
            float nextTick = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = t / duration;
                for (int i = 0; i < rings.Count; i++)
                {
                    float r = Mathf.Lerp(pullRadius, 0.3f, Mathf.Repeat(u * 2f + i / 3f, 1f));
                    for (int p = 0; p < 36; p++)
                    {
                        float a = p / 36f * Mathf.PI * 2f + t * (3f + i);
                        rings[i].SetPosition(p, center + new Vector3(Mathf.Cos(a) * r, 0.2f + i * 0.4f, Mathf.Sin(a) * r));
                    }
                }

                if (core) core.localScale = Vector3.one * (0.6f + Mathf.Sin(t * 20f) * 0.15f + u * 0.8f);
                if (label && Camera.main) label.transform.rotation = Quaternion.LookRotation(label.transform.position - Camera.main.transform.position);

                var pulled = new HashSet<EnemyController>();
                foreach (Collider col in Physics.OverlapSphere(center, pullRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    var enemy = col.GetComponentInParent<EnemyController>();
                    if (!enemy || !pulled.Add(enemy)) continue;
                    var me = enemy.GetComponent<MathEnemy>();
                    if (me && me.Kind == EnemyKind.Boss) continue; // 보스는 끌려오지 않는다
                    var agent = enemy.GetComponent<NavMeshAgent>();
                    Vector3 to = center - enemy.transform.position;
                    to.y = 0f;
                    if (agent && agent.isOnNavMesh && to.magnitude > 1f)
                        agent.Move(to.normalized * 7f * Time.deltaTime);
                }

                if (t >= nextTick)
                {
                    nextTick += 0.3f;
                    MathCombat.DamageInRadius(center, 4f, d => 5f, m_Owner);
                }

                yield return null;
            }

            MathCombat.DamageInRadius(center, 6f, d => Damage * (1f - d / 12f), m_Owner);
            if (m && m.ExplosionVfx) Destroy(Instantiate(m.ExplosionVfx, center, Quaternion.identity), 3f);
            if (m && m.CameraFX && m.Player) m.CameraFX.Shake(0.3f + 0.6f * Mathf.Clamp01(1f - Vector3.Distance(m.Player.position, center) / 30f), 0.3f);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ 연쇄 번개

        IEnumerator ChainLightning(Transform cam)
        {
            m_Active = false;
            MathModeManager m = MathModeManager.Instance;
            var points = new List<Vector3> { transform.position };
            Vector3 origin = cam ? cam.position : transform.position;
            Vector3 dir = cam ? cam.forward : transform.forward;
            float damage = Damage;

            if (Cast(origin, dir, Range, 0.25f, out RaycastHit hit))
            {
                points.Add(hit.point);
                HitDamage(hit.collider, damage);
                var enemy = hit.collider.GetComponentInParent<EnemyController>();
                var visited = new HashSet<EnemyController>();
                var manager = FindAnyObjectByType<EnemyManager>();
                while (enemy && manager && visited.Count < 5)
                {
                    visited.Add(enemy);
                    Vector3 from = AimPointOf(enemy).position;
                    EnemyController next = null;
                    float best = 11f;
                    foreach (EnemyController e in manager.Enemies)
                    {
                        if (!e || visited.Contains(e)) continue;
                        Vector3 to = AimPointOf(e).position;
                        float dist = Vector3.Distance(from, to);
                        if (dist >= best) continue;
                        bool clear = !Physics.Linecast(from, to, out RaycastHit block, ~0, QueryTriggerInteraction.Ignore) ||
                                     block.collider.GetComponentInParent<EnemyController>();
                        if (clear)
                        {
                            best = dist;
                            next = e;
                        }
                    }

                    if (!next) break;
                    damage *= 0.8f;
                    points.Add(AimPointOf(next).position);
                    var nd = next.GetComponentInChildren<Damageable>();
                    if (nd) nd.InflictDamage(damage, false, m_Owner);
                    enemy = next;
                }
            }
            else
            {
                points.Add(origin + dir * Range);
            }

            // 지그재그 번개 + 꼭짓점 표시
            if (m)
            {
                LineRenderer bolt = m.CreateLine(null, Color * 2f, 0.09f);
                var jagged = new List<Vector3>();
                for (int i = 0; i < points.Count - 1; i++)
                    for (int s = 0; s < 8; s++)
                    {
                        Vector3 p = Vector3.Lerp(points[i], points[i + 1], s / 8f);
                        if (s > 0) p += Random.insideUnitSphere * 0.25f;
                        jagged.Add(p);
                    }

                jagged.Add(points[points.Count - 1]);
                bolt.positionCount = jagged.Count;
                bolt.SetPositions(jagged.ToArray());
                var dots = new List<GameObject>();
                for (int i = 1; i < points.Count; i++)
                    dots.Add(m.CreateBlock(null, PrimitiveType.Sphere, points[i], Vector3.one * 0.35f, new Color(2f, 2f, 2f)));

                for (float t = 0f; t < 0.22f; t += Time.deltaTime)
                {
                    bolt.widthMultiplier = 0.09f * (1f - t / 0.22f) + 0.02f;
                    yield return null;
                }

                Destroy(bolt.gameObject);
                foreach (GameObject d in dots) Destroy(d);
            }

            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ 공용

        bool IsValid(Collider col)
        {
            if (!col || col.GetComponent<IgnoreHitDetection>()) return false;
            if (m_Owner && col.transform.IsChildOf(m_Owner.transform)) return false;
            if (col.isTrigger && !col.GetComponent<Damageable>()) return false;
            if (col.transform.IsChildOf(transform)) return false;
            return true;
        }

        bool Cast(Vector3 from, Vector3 dir, float distance, float radius, out RaycastHit best)
        {
            best = default;
            best.distance = float.MaxValue;
            bool found = false;
            if (distance <= 0f) return false;
            foreach (RaycastHit hit in Physics.SphereCastAll(from, radius, dir, distance, ~0, QueryTriggerInteraction.Collide))
            {
                if (!IsValid(hit.collider) || hit.distance >= best.distance) continue;
                best = hit;
                found = true;
            }

            if (found && best.distance <= 0f) best.point = from;
            return found;
        }

        static Damageable FindDamageable(Collider col)
        {
            var d = col.GetComponent<Damageable>();
            return d ? d : col.GetComponentInParent<Damageable>();
        }

        void HitDamage(Collider col, float damage)
        {
            var damageable = FindDamageable(col);
            if (damageable) damageable.InflictDamage(damage, false, m_Owner);
        }
    }
}
