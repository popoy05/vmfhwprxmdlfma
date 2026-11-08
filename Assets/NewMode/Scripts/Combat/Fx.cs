using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 에셋 이펙트(Hovl Magic effects / Eric VFX)를 그대로 소환한다.
    //  - 크기 배율을 실제로 적용하고 (파티클 Scaling Mode = Hierarchy)
    //  - 반복(loop) 이펙트는 hold초 뒤에 멈추고, 다 사라지면 지운다
    //  - 옆에 FxFormula가 분석한 함수식을 작게 띄운다
    public static class Fx
    {
        // 모든 이펙트를 키우는 배율 (화려하게)
        public static float SizeBoost = 1.45f;
        // 이펙트 옆 함수식 표시 (기본: 끔 — F1로 켜고 끈다)
        public static bool ShowFormula;

        // 무기 색으로 물들이는 공용 이펙트 (색이 정해지지 않은 하양·파랑 계열 범용 이펙트들)
        public static Color? Tint;
        static readonly System.Collections.Generic.HashSet<string> s_Neutral = new System.Collections.Generic.HashSet<string>
        {
            "Explosion", "Sparks explode white", "Sparks flashing white", "Sparks white", "Teleport", "Portal blue",
            "FX_LightPillar", "Magic circle", "Magic circle 2", "Laser AOE", "Holy hit", "Plexus AoE", "Plexus",
            "Buff", "Star aura", "Ground AOE explosion", "Healing", "Healing circle", "Debuff",
        };

        public static GameObject Play(string name, Vector3 position, Quaternion rotation, float size,
            float hold = 1.2f, bool label = true, Transform follow = null)
        {
            size *= SizeBoost;
            CombatAssets assets = CombatAssets.Load();
            GameObject prefab = assets ? assets.Effect(name) : null;
            if (!prefab)
            {
                Debug.LogWarning($"[NewMode] 이펙트를 찾을 수 없음: {name}");
                return null;
            }

            GameObject fx = Object.Instantiate(prefab, position, rotation, follow);
            fx.transform.localScale = prefab.transform.localScale * size;
            if (Tint.HasValue && s_Neutral.Contains(name)) Colorize(fx, Tint.Value);

            float life = 0f;
            bool looping = false;
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                looping |= main.loop;
                life = Mathf.Max(life, (main.loop ? hold : main.duration) + main.startLifetime.constantMax);
            }

            var animator = fx.GetComponentInChildren<Animator>();
            if (animator && animator.runtimeAnimatorController)
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    life = Mathf.Max(life, clip.length);

            if (life <= 0f) life = 2f;
            life = Mathf.Min(life, 6f);
            fx.AddComponent<FxLifetime>().Init(looping ? hold : life, life);

            if (label && ShowFormula) FxFormula.Label(fx, FxFormula.Analyze(fx), Mathf.Min(life, 1.8f), size, new Color(1f, 0.95f, 0.75f));
            return fx;
        }

        /// <summary>시간 배율과 상관없이 재생 (컷신용)</summary>
        public static GameObject Unscaled(GameObject fx)
        {
            if (!fx) return fx;
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.useUnscaledTime = true;
            }
            var life = fx.GetComponent<FxLifetime>();
            if (life) life.Unscaled = true;
            return fx;
        }

        /// <summary>
        /// 파티클 색을 tint 쪽으로 (밝기는 유지): c' = lerp(c, tint·max(r,g,b), 0.8), 알파는 그대로
        /// </summary>
        public static void Colorize(GameObject fx, Color tint)
        {
            Color Shift(Color c)
            {
                float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                Color t = new Color(tint.r * v, tint.g * v, tint.b * v, c.a);
                return Color.Lerp(c, t, 0.8f);
            }
            Gradient ShiftGradient(Gradient g)
            {
                if (g == null) return null;
                var keys = g.colorKeys;
                for (int i = 0; i < keys.Length; i++) keys[i].color = Shift(keys[i].color);
                var result = new Gradient { mode = g.mode };
                result.SetKeys(keys, g.alphaKeys);
                return result;
            }

            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var sc = main.startColor;
                switch (sc.mode)
                {
                    case ParticleSystemGradientMode.Color: main.startColor = Shift(sc.color); break;
                    case ParticleSystemGradientMode.TwoColors: main.startColor = new ParticleSystem.MinMaxGradient(Shift(sc.colorMin), Shift(sc.colorMax)); break;
                    case ParticleSystemGradientMode.Gradient: main.startColor = new ParticleSystem.MinMaxGradient(ShiftGradient(sc.gradient)); break;
                    case ParticleSystemGradientMode.TwoGradients: main.startColor = new ParticleSystem.MinMaxGradient(ShiftGradient(sc.gradientMin), ShiftGradient(sc.gradientMax)); break;
                    default: main.startColor = Shift(sc.Evaluate(0.5f)); break;
                }
                var col = ps.colorOverLifetime;
                if (col.enabled && col.color.mode == ParticleSystemGradientMode.Gradient)
                    col.color = new ParticleSystem.MinMaxGradient(ShiftGradient(col.color.gradient));
            }
            foreach (var light in fx.GetComponentsInChildren<Light>(true)) light.color = tint;
            // 재질 색도 (주황 빛기둥 같은 것)
            foreach (var r in fx.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.sharedMaterial) continue;
                var m = r.material;
                foreach (var prop in new[] { "_BaseColor", "_TintColor", "_Color" })
                    if (m.HasProperty(prop)) m.SetColor(prop, Shift(m.GetColor(prop)));
            }
        }
    }

    // stopAt초에 파티클 방출을 멈추고 destroyAt초에 지운다
    public class FxLifetime : MonoBehaviour
    {
        float m_StopAt, m_DestroyAt, m_Age;
        bool m_Stopped;
        public bool Unscaled; // 궁극기 컷신처럼 시간이 거의 멈춘 동안에도 재생

        public void Init(float stopAt, float destroyAt)
        {
            m_StopAt = stopAt;
            m_DestroyAt = Mathf.Max(destroyAt, stopAt + 0.1f);
        }

        void Update()
        {
            m_Age += Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (!m_Stopped && m_Age >= m_StopAt)
            {
                m_Stopped = true;
                foreach (var ps in GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (m_Age >= m_DestroyAt) Destroy(gameObject);
        }
    }
}
