using System;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 스킬 연출: 함수 그래프를 땅 위에 빛나는 선으로 그리고, 데미지 숫자를 띄운다.
    public static class GeoFx
    {
        // 함수 그래프 선을 땅에 그릴지 (기본: 끔 — F1로 켜고 끈다)
        public static bool ShowGraphs;
        // 그래프를 숨겼을 때 대신 불린다 (points, width, color, reveal) — 전투 쪽에서 곡선을 따라 에셋 이펙트를 터뜨린다
        public static Action<Vector3[], float, Color, float> HiddenCurve;
        static Material s_LineMaterial;

        public static Material LineMaterial
        {
            get
            {
                if (!s_LineMaterial)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    s_LineMaterial = new Material(shader) { name = "GeoFx Line" };
                }
                return s_LineMaterial;
            }
        }

        // 땅 높이 (플레이어·허수아비는 Ignore Raycast 레이어라서 무시된다)
        public static float GroundY(Vector3 p, float scale)
        {
            Vector3 from = p + Vector3.up * 4f * scale;
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 12f * scale, ~(1 << 2), QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return p.y;
        }

        /// <summary>
        /// 함수 f(u) (u: 0→1)가 그리는 곡선을 선으로 만든다.
        /// reveal 초 동안 앞에서부터 그려지고, life 초 뒤 사라진다.
        /// </summary>
        public static FadingLine Curve(Func<float, Vector3> f, int samples, float width, Color color, float reveal, float life, bool hugGround, float scale)
        {
            var points = new Vector3[samples];
            for (int i = 0; i < samples; i++)
            {
                Vector3 p = f(i / (samples - 1f));
                if (hugGround) p.y = GroundY(p, scale) + 0.06f * scale;
                points[i] = p;
            }

            if (!ShowGraphs)
            {
                HiddenCurve?.Invoke(points, width, color, reveal);
                return null;
            }
            return Line(points, width, color, reveal, life);
        }

        public static FadingLine Line(Vector3[] points, float width, Color color, float reveal, float life)
        {
            var go = new GameObject("GeoFx Curve");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = LineMaterial;
            lr.useWorldSpace = true;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.widthMultiplier = width;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.6f));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            var line = go.AddComponent<FadingLine>();
            line.Init(points, color, reveal, life);
            return line;
        }

        /// <summary>링(원) 하나 — 중심 c, 반지름 r</summary>
        public static FadingLine Ring(Vector3 center, float radius, float width, Color color, float life, float scale)
        {
            return Curve(u =>
            {
                Vector2 p = GeoFunctions.Circle(radius, u * Mathf.PI * 2f);
                return center + new Vector3(p.x, 0f, p.y);
            }, 64, width, color, 0f, life, true, scale);
        }

        // 데미지 숫자
        public static void Popup(Vector3 position, string text, Color color, float scale, float size = 1f)
        {
            var go = new GameObject("Damage Popup");
            go.transform.position = position;
            var tmp = go.AddComponent<TextMeshPro>();
            TMP_FontAsset font = MathModeAssets.Load() ? MathModeAssets.Load().Font : null;
            if (font) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 6f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(20, 30, 60, 255);
            tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);
            var ft = go.AddComponent<FloatingText>();
            ft.Scale = scale;
            ft.Size = size;
        }
    }

    // 앞에서부터 그려지고, 끝나면 투명해지며 사라지는 선
    public class FadingLine : MonoBehaviour
    {
        LineRenderer m_Line;
        Vector3[] m_Points;
        Color m_Color;
        float m_Reveal, m_Life, m_Age;

        public void Init(Vector3[] points, Color color, float reveal, float life)
        {
            m_Line = GetComponent<LineRenderer>();
            m_Points = points;
            m_Color = color;
            m_Reveal = reveal;
            m_Life = Mathf.Max(life, reveal + 0.05f);
            Apply(m_Reveal <= 0f ? 1f : 0f, 1f);
        }

        void Update()
        {
            m_Age += Time.deltaTime;
            float shown = m_Reveal <= 0f ? 1f : Mathf.Clamp01(m_Age / m_Reveal);
            float fadeStart = m_Life * 0.6f;
            float alpha = m_Age < fadeStart ? 1f : 1f - Mathf.InverseLerp(fadeStart, m_Life, m_Age);
            Apply(shown, alpha);
            if (m_Age >= m_Life) Destroy(gameObject);
        }

        void Apply(float shown, float alpha)
        {
            int count = Mathf.Clamp(Mathf.CeilToInt(shown * (m_Points.Length - 1)) + 1, 2, m_Points.Length);
            m_Line.positionCount = count;
            for (int i = 0; i < count; i++) m_Line.SetPosition(i, m_Points[i]);

            // 그려지는 끝부분(머리)을 하얗게 빛나게
            var head = Color.Lerp(m_Color, Color.white, 0.75f);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(m_Color, 0f), new GradientColorKey(m_Color, 0.85f), new GradientColorKey(head, 1f) },
                new[] { new GradientAlphaKey(alpha * 0.35f, 0f), new GradientAlphaKey(alpha, 0.6f), new GradientAlphaKey(alpha, 1f) });
            m_Line.colorGradient = gradient;
        }
    }

    // 위로 떠오르며 사라지는 글자 (항상 카메라를 본다)
    public class FloatingText : MonoBehaviour
    {
        public float Scale = 1f;
        public float Size = 1f;   // 치명타일수록 크게
        TextMeshPro m_Text;
        float m_Age;
        Vector3 m_Drift;

        void Start()
        {
            m_Text = GetComponent<TextMeshPro>();
            m_Drift = new Vector3(UnityEngine.Random.Range(-0.9f, 0.9f), 1.6f + 0.6f * Size, 0f) * Scale;
            transform.position += new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(-0.1f, 0.3f), 0f) * Scale;
        }

        void LateUpdate()
        {
            m_Age += Time.deltaTime;
            transform.position += m_Drift * Time.deltaTime;
            m_Drift *= 1f - 2.5f * Time.deltaTime;
            Camera cam = Camera.main;
            if (cam) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            // 크게 쾅 튀어나왔다가 (OutBack) 줄어들며 사라짐
            float pop = m_Age < 0.12f ? Mathf.Lerp(2.0f, 1.2f, m_Age / 0.12f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((m_Age - 0.12f) / 0.15f));
            float shrink = Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(0.8f, 1.2f, m_Age));
            transform.localScale = Vector3.one * 0.5f * Mathf.Max(0.4f, Scale) * Size * pop * shrink;
            if (m_Text) m_Text.alpha = 1f - Mathf.InverseLerp(0.85f, 1.2f, m_Age);
            if (m_Age > 1.2f) Destroy(gameObject);
        }
    }
}
