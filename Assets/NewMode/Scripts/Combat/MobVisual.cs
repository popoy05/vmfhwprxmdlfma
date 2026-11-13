using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 몹 모델 (CombatAssets.Mobs)
    //  - 일반 몹: Little Ghost — 허수아비(노랑) · 깡총이(검정, 작게) · 거인(빨강, 크게). 땅 위에 살짝 떠서 둥실둥실
    //  - 보스: 고래 — 아주 크게, 땅 위에 떠서 헤엄친다
    // 모델은 프리팹에 들어 있는 충돌체 크기로 맞춰 넣는다 (맞는 판정은 TrainingDummy의 캡슐이 그대로 담당)
    public static class MobVisual
    {
        public static string ModelName(MobKind kind) => kind switch
        {
            MobKind.Boss => "Whale",
            MobKind.Hopper => "GhostBlack",
            MobKind.Brute => "GhostRed",
            _ => "GhostYellow",
        };

        // 애니메이션 상태 이름 (Little_Ghost2.controller · Whale.controller)
        public static string IdleState(MobKind kind) => kind == MobKind.Boss ? "Swimming" : "idle";
        public static string MoveState(MobKind kind) => kind == MobKind.Boss ? "Swimming Speedy" : "fly";
        public static string AttackState(MobKind kind) => kind == MobKind.Boss ? "Attack" : "attack2";
        public static string HitState(MobKind kind) => kind == MobKind.Boss ? null : "hit";
        public static string DeadState(MobKind kind) => kind == MobKind.Boss ? "dead" : null;

        /// <summary>보스 고래: 몸 길이·몸통 높이 (BodyScale 배)</summary>
        public const float WhaleLength = 3.6f, WhaleCenter = 0.95f;
        /// <summary>유령: 키 · 땅에서 떠 있는 높이 (BodyScale 배)</summary>
        public const float GhostHeight = 1.55f, GhostHover = 0.22f;

        /// <summary>body 아래에 모델을 만든다. 모델이 없으면 null (그때는 예전 도형 허수아비)</summary>
        public static Transform Build(Transform body, MobKind kind, float b, out Animator animator)
        {
            animator = null;
            CombatAssets assets = CombatAssets.Load();
            GameObject prefab = assets ? assets.Mob(ModelName(kind)) : null;
            if (!prefab) return null;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(body, false);
            GameObject model = Object.Instantiate(prefab, visual, false);
            model.name = "Model";

            // 크기는 프리팹 충돌체로 잰다 (모델 기준 좌표). 잰 다음 충돌체·물리는 모두 뗀다 (땅 높이 재기·공격 판정에 걸리지 않게)
            Bounds local = ColliderBounds(model.transform);
            foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true; // 몸이 커서 가장자리에서 사라지지 않게
            }

            Transform m = model.transform;
            if (kind == MobKind.Boss)
            {
                // 고래: 머리가 −Z 쪽이라 뒤로 돌려 앞(+Z)을 보게. 몸 길이를 맞추고 몸통 가운데를 WhaleCenter 높이에
                float k = WhaleLength * b / Mathf.Max(0.01f, local.size.z);
                m.localRotation = Quaternion.Euler(0f, 180f, 0f);
                m.localScale = Vector3.one * k;
                m.localPosition = new Vector3(0f, WhaleCenter * b, 0f) - m.localRotation * (local.center * k);
            }
            else
            {
                // 유령: 키를 맞추고 발(아래 끝)을 GhostHover 높이에
                float k = GhostHeight * b / Mathf.Max(0.01f, local.size.y);
                m.localScale = Vector3.one * k;
                Vector3 bottom = new Vector3(local.center.x, local.min.y, local.center.z);
                m.localPosition = new Vector3(0f, GhostHover * b, 0f) - bottom * k;
            }

            animator = model.GetComponentInChildren<Animator>();
            if (animator)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Play(IdleState(kind), 0, Random.value);
            }
            return visual;
        }

        // 프리팹의 Box·Capsule 충돌체 → 모델 기준 경계 상자
        static Bounds ColliderBounds(Transform root)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                Bounds b;
                if (c is BoxCollider box) b = new Bounds(box.center, box.size);
                else if (c is CapsuleCollider cap)
                {
                    float r = cap.radius, h = Mathf.Max(cap.height, 2f * r);
                    Vector3 size = cap.direction == 0 ? new Vector3(h, 2f * r, 2f * r)
                                 : cap.direction == 1 ? new Vector3(2f * r, h, 2f * r)
                                 : new Vector3(2f * r, 2f * r, h);
                    b = new Bounds(cap.center, size);
                }
                else continue;
                if (c.transform == root) return b;
                // 자식 충돌체면 모델 기준으로 옮긴다 (크기만 대충)
                Vector3 center = root.InverseTransformPoint(c.transform.TransformPoint(b.center));
                Vector3 scale = c.transform.lossyScale;
                Vector3 rs = root.lossyScale;
                return new Bounds(center, Vector3.Scale(b.size, new Vector3(scale.x / rs.x, scale.y / rs.y, scale.z / rs.z)));
            }
            // 충돌체가 없으면 렌더러 경계
            Bounds rb = new Bounds(Vector3.up * 0.5f, Vector3.one);
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds w = r.bounds;
                Vector3 lc = root.InverseTransformPoint(w.center);
                Bounds lb = new Bounds(lc, w.size);
                if (!any) { rb = lb; any = true; } else rb.Encapsulate(lb);
            }
            return rb;
        }
    }
}
