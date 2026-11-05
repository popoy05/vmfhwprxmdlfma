using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 에셋 이펙트(파티클)를 "분석"해서 그 모양을 함수식으로 나타낸다.
    //  - 어디서 생기나: Shape 모듈 (구·반구·원뿔·원·도넛·상자·선분·메시)
    //  - 어떻게 움직이나: 시작 속도, 중력, 궤도/방사 속도, 회전
    // 실제로 소환된 이펙트(크기 배율이 적용된 상태)의 값을 읽기 때문에 숫자도 그 순간의 실제 크기다.
    public static class FxFormula
    {
        public static string Analyze(GameObject fx)
        {
            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0)
            {
                var mesh = fx.GetComponentInChildren<MeshFilter>();
                return mesh ? $"메시: {Clean(mesh.sharedMesh ? mesh.sharedMesh.name : fx.name)}" : "";
            }

            // 화면에서 가장 크게 보이는 파티클을 대표로 고른다 (크기 × √개수)
            ParticleSystem main = systems[0];
            float best = -1f;
            foreach (var ps in systems)
            {
                float score = VisualScore(ps);
                if (score > best) { best = score; main = ps; }
            }

            float scale = main.transform.lossyScale.x;
            var lines = new List<string>();
            bool point = IsPoint(main, scale);
            if (!point) Add(lines, ShapeFormula(main, scale));
            Add(lines, MotionFormula(main, scale, point));

            // 대표 파티클이 움직이지 않으면, 가장 빠른 파티클의 움직임도 함께 보여 준다
            if (lines.Count < 2)
            {
                ParticleSystem fast = null;
                float speed = 0.01f;
                foreach (var ps in systems)
                    if (ps.main.startSpeed.constantMax > speed) { speed = ps.main.startSpeed.constantMax; fast = ps; }
                if (fast && fast != main) Add(lines, MotionFormula(fast, scale, IsPoint(fast, scale)));
            }

            if (lines.Count < 2) Add(lines, SizeOrRotation(main, scale));
            if (lines.Count > 2) lines.RemoveRange(2, lines.Count - 2);
            return string.Join("\n", lines);
        }

        static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line) && !lines.Contains(line)) lines.Add(line);
        }

        static float VisualScore(ParticleSystem ps)
        {
            var emission = ps.emission;
            float count = emission.rateOverTime.constantMax * ps.main.duration;
            for (int i = 0; i < emission.burstCount; i++) count += emission.GetBurst(i).count.constantMax;
            return ps.main.startSize.constantMax * Mathf.Sqrt(Mathf.Max(1f, count));
        }

        // 반지름이 0에 가까운 모양 = 한 점에서 나온다
        static bool IsPoint(ParticleSystem ps, float scale)
        {
            var shape = ps.shape;
            if (!shape.enabled) return true;
            switch (shape.shapeType)
            {
                case ParticleSystemShapeType.Box:
                case ParticleSystemShapeType.BoxShell:
                case ParticleSystemShapeType.BoxEdge:
                case ParticleSystemShapeType.Rectangle:
                    return shape.scale.magnitude * scale < 0.02f;
                case ParticleSystemShapeType.Mesh:
                case ParticleSystemShapeType.MeshRenderer:
                case ParticleSystemShapeType.SkinnedMeshRenderer:
                    return false;
                default:
                    return shape.radius * Mathf.Max(shape.scale.x, shape.scale.z) * scale < 0.02f;
            }
        }

        static string ShapeFormula(ParticleSystem ps, float scale)
        {
            var shape = ps.shape;
            Vector3 s = shape.scale * scale;                                   // 상자·사각형 크기 (실제 크기)
            float r = shape.radius * Mathf.Max(shape.scale.x, shape.scale.z) * scale; // 반지름 (실제 크기)
            bool surface = shape.radiusThickness <= 0.01f;
            string eq = surface ? "=" : "≤";
            string arc = shape.arc < 359.5f ? $",  0 ≤ θ ≤ {shape.arc:0}°" : "";

            switch (shape.shapeType)
            {
                case ParticleSystemShapeType.Sphere:
                case ParticleSystemShapeType.SphereShell:
                    return $"x² + y² + z² {eq} {N(r)}²";
                case ParticleSystemShapeType.Hemisphere:
                case ParticleSystemShapeType.HemisphereShell:
                    return $"x² + y² + z² {eq} {N(r)}²,  y ≥ 0";
                case ParticleSystemShapeType.Cone:
                case ParticleSystemShapeType.ConeShell:
                case ParticleSystemShapeType.ConeVolume:
                case ParticleSystemShapeType.ConeVolumeShell:
                    if (shape.angle < 1f) return $"x² + z² = {N(r)}²  (원기둥)" + arc;
                    if (shape.angle > 89f) return $"x² + z² ≤ (r₀ + h)²,  y = 0  (평면)" + arc;
                    return $"r(h) = {N(r)} + h·tan{shape.angle:0}°" + arc;
                case ParticleSystemShapeType.Circle:
                case ParticleSystemShapeType.CircleEdge:
                    return $"x² + z² {eq} {N(r)}²" + arc;
                case ParticleSystemShapeType.Donut:
                    return $"(√(x² + z²) − {N(r)})² + y² = {N(shape.donutRadius * scale)}²";
                case ParticleSystemShapeType.Box:
                case ParticleSystemShapeType.BoxShell:
                case ParticleSystemShapeType.BoxEdge:
                    return $"|x| ≤ {N(s.x / 2)},  |y| ≤ {N(s.y / 2)},  |z| ≤ {N(s.z / 2)}";
                case ParticleSystemShapeType.Rectangle:
                    return $"|x| ≤ {N(s.x / 2)},  |y| ≤ {N(s.y / 2)}";
                case ParticleSystemShapeType.SingleSidedEdge:
                    return $"y = 0,  |x| ≤ {N(r)}";
                case ParticleSystemShapeType.Mesh:
                case ParticleSystemShapeType.MeshRenderer:
                case ParticleSystemShapeType.SkinnedMeshRenderer:
                    return $"메시 표면 ({Clean(shape.mesh ? shape.mesh.name : "mesh")})";
                default:
                    return "";
            }
        }

        static string MotionFormula(ParticleSystem ps, float scale, bool point)
        {
            var main = ps.main;
            float v = main.startSpeed.constantMax * scale;
            float g = main.gravityModifier.constantMax * 9.81f;

            var vel = ps.velocityOverLifetime;
            if (vel.enabled)
            {
                float orbital = Mathf.Max(Mathf.Abs(vel.orbitalX.constantMax), Mathf.Abs(vel.orbitalY.constantMax), Mathf.Abs(vel.orbitalZ.constantMax));
                if (orbital > 0.01f) return $"θ(t) = {N(orbital)}t  (회전)";
                float radial = vel.radial.constantMax * scale;
                if (Mathf.Abs(radial) > 0.01f) return $"r(t) = r₀ {Sign(radial)} {N(Mathf.Abs(radial))}t";
            }

            // 중력이 있으면 포물선 운동
            if (Mathf.Abs(g) > 0.01f)
                return v > 0.01f ? $"y = {N(v)}t − ½·{N(g)}t²" : $"y = −½·{N(g)}t²";

            if (v > 0.01f)
            {
                if (!point) return $"r(t) = r₀ + {N(v)}t";
                // 한 점에서 속력 v로 사방에 날아가면, 시간 t일 때 반지름 vt인 구면 위에 있다
                var shape = ps.shape;
                bool hemi = shape.enabled && (shape.shapeType == ParticleSystemShapeType.Hemisphere || shape.shapeType == ParticleSystemShapeType.HemisphereShell);
                bool cone = shape.enabled && (shape.shapeType == ParticleSystemShapeType.Cone || shape.shapeType == ParticleSystemShapeType.ConeShell);
                if (cone && shape.angle < 1f) return $"y = {N(v)}t  (직선)";
                if (cone) return $"r = {N(v)}t,  φ ≤ {shape.angle:0}°  (원뿔)";
                return hemi ? $"x² + y² + z² = ({N(v)}t)²,  y ≥ 0" : $"x² + y² + z² = ({N(v)}t)²";
            }

            return SizeOrRotation(ps, scale);
        }

        // 제자리에서 커지거나 도는 파티클: s(t) = s₀ + kt,  θ(t) = θ₀ + ωt   (t: 0 → 수명 T)
        static string SizeOrRotation(ParticleSystem ps, float scale)
        {
            var main = ps.main;
            var rot = ps.rotationOverLifetime;
            if (rot.enabled && Mathf.Abs(rot.z.constantMax) > 0.05f)
                return $"θ(t) = {N(Mathf.Abs(rot.z.constantMax) * Mathf.Rad2Deg)}°·t  (회전)";

            var size = ps.sizeOverLifetime;
            float s = main.startSize.constantMax * scale;
            if (size.enabled)
            {
                float a = s * Evaluate(size.size, 0f), b = s * Evaluate(size.size, 1f);
                float T = Mathf.Max(0.01f, main.startLifetime.constantMax);
                float k = (b - a) / T;
                if (Mathf.Abs(k) > 0.01f) return $"s(t) = {N(a)} {Sign(k)} {N(Mathf.Abs(k))}t  (크기)";
            }
            return s > 0.01f ? $"x² + y² = {N(s * 0.5f)}²  (빛 원판)" : "";
        }

        static float Evaluate(ParticleSystem.MinMaxCurve curve, float t)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Curve: return curve.curve.Evaluate(t) * curve.curveMultiplier;
                case ParticleSystemCurveMode.TwoCurves: return curve.curveMax.Evaluate(t) * curve.curveMultiplier;
                case ParticleSystemCurveMode.TwoConstants: return curve.constantMax;
                default: return curve.constant;
            }
        }

        // 폰트(나눔고딕)에 없는 위·아래 첨자 문자(ₙ ₖ ₀ ⁿ ⁻ …)를 TMP의 <sub>/<sup> 태그로 바꾼다
        const string k_Sub = "₀₁₂₃₄₅₆₇₈₉ₐₑₒₓₔₕₖₗₘₙₚₛₜᵢ";
        const string k_SubPlain = "0123456789aeoxəhklmnpsti";
        const string k_Sup = "⁰ⁱ⁴⁵⁶⁷⁸⁹⁺⁻⁼⁽⁾ⁿ";
        const string k_SupPlain = "0i456789+−=()n";

        public static string Rich(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder(text.Length + 16);
            int mode = 0; // 0 보통, 1 아래첨자, 2 위첨자
            foreach (char ch in text)
            {
                int sub = k_Sub.IndexOf(ch), sup = k_Sup.IndexOf(ch);
                int want = sub >= 0 ? 1 : sup >= 0 ? 2 : 0;
                if (want != mode)
                {
                    if (mode == 1) sb.Append("</sub>");
                    if (mode == 2) sb.Append("</sup>");
                    if (want == 1) sb.Append("<sub>");
                    if (want == 2) sb.Append("<sup>");
                    mode = want;
                }
                sb.Append(sub >= 0 ? k_SubPlain[sub] : sup >= 0 ? k_SupPlain[sup] : ch);
            }
            if (mode == 1) sb.Append("</sub>");
            if (mode == 2) sb.Append("</sup>");
            return sb.ToString();
        }

        static string N(float value) => value.ToString(value >= 10f ? "0" : "0.##");
        static string Sign(float value) => value >= 0f ? "+" : "−";
        static string Clean(string name) => name.Replace("(Clone)", "").Trim();

        // ------------------------------------------------------------ 화면 표시

        // 이펙트 옆에 작은 함수식 글자를 띄운다 (카메라를 바라보고, 이펙트가 끝날 때쯤 사라짐)
        public static void Label(GameObject fx, string text, float life, float scale, Color color)
        {
            if (string.IsNullOrEmpty(text) || !fx) return;
            var go = new GameObject("Fx Formula");
            Bounds b = Bounds(fx);
            go.transform.position = b.center + Vector3.up * (b.extents.y + 0.35f * scale);
            var tmp = go.AddComponent<TextMeshPro>();
            TMP_FontAsset font = MathModeAssets.Load() ? MathModeAssets.Load().Font : null;
            if (font) tmp.font = font;
            tmp.text = Rich(text);
            tmp.fontSize = 2.6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color32(10, 20, 45, 230);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);
            go.AddComponent<FormulaLabel>().Init(life, scale);
        }

        static Bounds Bounds(GameObject fx)
        {
            var b = new Bounds(fx.transform.position, Vector3.zero);
            foreach (var r in fx.GetComponentsInChildren<Renderer>())
                if (r.bounds.size.sqrMagnitude < 400f) b.Encapsulate(r.bounds);
            return b;
        }
    }

    public class FormulaLabel : MonoBehaviour
    {
        TextMeshPro m_Text;
        float m_Life, m_Age, m_Scale;

        public void Init(float life, float scale)
        {
            m_Text = GetComponent<TextMeshPro>();
            m_Life = Mathf.Clamp(life, 0.8f, 2.2f);
            m_Scale = Mathf.Max(0.45f, scale);
            transform.localScale = Vector3.one * 0.9f * m_Scale; // 글자 높이 ≈ 0.23m × Scale (작지만 읽을 수 있게)
        }

        void LateUpdate()
        {
            m_Age += Time.deltaTime;
            Camera cam = Camera.main;
            if (cam) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            transform.position += Vector3.up * 0.15f * m_Scale * Time.deltaTime;
            float a = Mathf.Clamp01(m_Age / 0.15f) * (1f - Mathf.InverseLerp(m_Life * 0.6f, m_Life, m_Age));
            m_Text.alpha = a;
            if (m_Age >= m_Life) Destroy(gameObject);
        }
    }
}
