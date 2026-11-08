using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.FPS.NewMode
{
    // 화면 후처리 연출
    //  · Pulse : 큰 타격·스킬 때 화면이 순간 휘었다가(렌즈 왜곡) 색이 갈라지며(색수차) 돌아온다.  a(t) = a₀·e^(−6t)
    //  · Cutscene : 궁극기 컷신 동안 배경을 어둡고 탁하게(노출↓·채도↓) + 원소 색 비네트 → 캐릭터만 빛나 보이게
    public static class PostFx
    {
        static Volume s_Volume;
        static LensDistortion s_Lens;
        static ChromaticAberration s_Chroma;
        static ColorAdjustments s_Color;
        static Vignette s_Vignette;
        static float s_Amount, s_Cut, s_CutTarget;
        static Color s_CutTint = Color.white;

        /// <summary>장면이 시작될 때 바로 후처리를 켠다 (톤매핑·블룸 등 씬 볼륨이 처음부터 적용되게)</summary>
        public static void Init() => Ensure();

        public static void Pulse(float strength, Color tint)
        {
            if (!Ensure()) return;
            s_Amount = Mathf.Clamp01(Mathf.Max(s_Amount, strength));
        }

        /// <summary>궁극기 컷신 화면 (on: 어둡게, off: 원래대로) — 부드럽게 바뀐다</summary>
        public static void Cutscene(bool on, Color tint)
        {
            if (!Ensure()) return;
            s_CutTarget = on ? 1f : 0f;
            s_CutTint = tint;
        }

        static bool Ensure()
        {
            if (s_Volume) return true;
            var go = new GameObject("PostFx");
            s_Volume = go.AddComponent<Volume>();
            s_Volume.isGlobal = true;
            s_Volume.priority = 50f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            s_Lens = profile.Add<LensDistortion>(true);
            s_Chroma = profile.Add<ChromaticAberration>(true);
            s_Color = profile.Add<ColorAdjustments>(true);
            s_Vignette = profile.Add<Vignette>(true);
            s_Lens.intensity.Override(0f);
            s_Lens.scale.Override(1f);
            s_Chroma.intensity.Override(0f);
            s_Color.postExposure.Override(0f);
            s_Color.saturation.Override(0f);
            s_Color.contrast.Override(0f);
            s_Vignette.intensity.Override(0f);
            s_Vignette.smoothness.Override(0.6f);
            s_Vignette.color.Override(Color.black);
            s_Volume.profile = profile;
            go.AddComponent<PostFxRunner>();
            s_Amount = s_Cut = s_CutTarget = 0f;

            foreach (var cam in Camera.allCameras)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data && cam.targetTexture == null) data.renderPostProcessing = true;
            }
            return true;
        }

        // 새로 생긴 카메라(플레이어 카메라 등)에도 후처리를 켜 둔다
        static float s_CamCheck;
        static void EnsureCameras(float dt)
        {
            s_CamCheck -= dt;
            if (s_CamCheck > 0f) return;
            s_CamCheck = 0.5f;
            foreach (var cam in Camera.allCameras)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data && cam.targetTexture == null && !data.renderPostProcessing) data.renderPostProcessing = true;
            }
        }

        public static void Step(float dt)
        {
            if (!s_Volume) return;
            EnsureCameras(dt);
            s_Amount = Mathf.Max(0f, s_Amount * Mathf.Exp(-6f * dt) - 0.02f * dt);
            s_Lens.intensity.value = -0.45f * s_Amount;
            s_Lens.scale.value = 1f + 0.08f * s_Amount;
            s_Chroma.intensity.value = Mathf.Clamp01(0.9f * s_Amount + 0.25f * s_Cut);

            // 컷신 화면: 0.15초 만에 어두워지고, 끝나면 0.3초에 걸쳐 돌아옴
            s_Cut = Mathf.MoveTowards(s_Cut, s_CutTarget, dt * (s_CutTarget > s_Cut ? 7f : 3.5f));
            s_Color.postExposure.value = -1.1f * s_Cut;
            s_Color.saturation.value = -45f * s_Cut;
            s_Color.contrast.value = 18f * s_Cut;
            s_Vignette.intensity.value = 0.5f * s_Cut;
            s_Vignette.color.value = Color.Lerp(Color.black, s_CutTint * 0.35f, 0.6f);
        }
    }

    public class PostFxRunner : MonoBehaviour
    {
        void Update() => PostFx.Step(Time.unscaledDeltaTime);
    }

    // 잠깐 번쩍이는 빛 (원소 색) — 세기가 지수적으로 줄어든다
    public class LightFlash : MonoBehaviour
    {
        Light m_Light;
        float m_Start, m_Time, m_Age;
        bool m_Unscaled;

        public static Light Spawn(Vector3 pos, Color color, float intensity, float range, float time, bool unscaled = false, Transform follow = null)
        {
            var go = new GameObject("Light Flash");
            go.transform.position = pos;
            if (follow) go.transform.SetParent(follow, true);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            var f = go.AddComponent<LightFlash>();
            f.m_Light = l;
            f.m_Start = intensity;
            f.m_Time = time;
            f.m_Unscaled = unscaled;
            return l;
        }

        void Update()
        {
            m_Age += m_Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = m_Age / Mathf.Max(0.01f, m_Time);
            m_Light.intensity = m_Start * Mathf.Exp(-4f * u);
            if (u >= 1f) Destroy(gameObject);
        }
    }

    // 땅에 퍼지는 충격파 고리: 반지름 r(t) = R·(1 − (1 − t)³), 점점 투명해짐
    public class RingWave : MonoBehaviour
    {
        float m_Radius, m_Time, m_Age;
        Material m_Mat;
        Color m_Color;
        bool m_Unscaled;

        public static void Spawn(Vector3 center, float radius, Color color, float time = 0.55f, bool unscaled = false)
        {
            var ring = Telegraph.Ring(center, color);
            var w = ring.AddComponent<RingWave>();
            w.m_Radius = radius;
            w.m_Time = time;
            w.m_Color = color;
            w.m_Mat = ring.GetComponent<Renderer>().material;
            w.m_Unscaled = unscaled;
            ring.transform.localScale = Vector3.zero;
        }

        void Update()
        {
            m_Age += m_Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(m_Age / m_Time);
            float e = 1f - Mathf.Pow(1f - u, 3f);
            transform.localScale = new Vector3(m_Radius * 2f * e, m_Radius * 2f * e, 1f);
            m_Mat.color = new Color(m_Color.r, m_Color.g, m_Color.b, m_Color.a * (1f - u));
            if (u >= 1f) Destroy(gameObject);
        }
    }
}
