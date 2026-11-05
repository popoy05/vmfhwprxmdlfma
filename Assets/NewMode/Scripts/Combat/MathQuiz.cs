using System.Collections.Generic;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 쉬운 수학 문제 하나 (보기 4개)
    public class MathProblem
    {
        public string Question;
        public int[] Choices;
        public int Answer; // Choices 안에서 정답의 번호 (0~3)

        public static MathProblem Generate(System.Random rng)
        {
            int kind = rng.Next(10);
            string q;
            int ans;
            switch (kind)
            {
                case 0:
                {
                    int a = rng.Next(12, 90), b = rng.Next(12, 90);
                    q = $"{a} + {b} = ?"; ans = a + b; break;
                }
                case 1:
                {
                    int a = rng.Next(40, 100), b = rng.Next(11, a - 5);
                    q = $"{a} − {b} = ?"; ans = a - b; break;
                }
                case 2:
                {
                    int a = rng.Next(3, 10), b = rng.Next(3, 10);
                    q = $"{a} × {b} = ?"; ans = a * b; break;
                }
                case 3:
                {
                    int a = rng.Next(2, 6), x = rng.Next(2, 10), b = rng.Next(1, 10);
                    q = $"{a}x + {b} = {a * x + b}\n<size=70%>x의 값은?</size>"; ans = x; break;
                }
                case 4:
                {
                    int w = rng.Next(3, 13), h = rng.Next(3, 13);
                    q = $"가로 {w}, 세로 {h}인\n직사각형의 넓이는?"; ans = w * h; break;
                }
                case 5:
                {
                    int a = rng.Next(30, 81), b = rng.Next(30, 81);
                    q = $"삼각형의 두 각이 {a}°, {b}°일 때\n나머지 한 각은?"; ans = 180 - a - b; break;
                }
                case 6:
                {
                    int n = rng.Next(3, 14);
                    q = $"{n}<sup>2</sup> = ?"; ans = n * n; break;
                }
                case 7:
                {
                    int[] bases = { 40, 80, 100, 200, 300, 400 };
                    int[] pcts = { 10, 20, 25, 50, 75 };
                    int b, p;
                    do { b = bases[rng.Next(bases.Length)]; p = pcts[rng.Next(pcts.Length)]; } while (b * p % 100 != 0);
                    q = $"{b}의 {p}%는?"; ans = b * p / 100; break;
                }
                case 8:
                {
                    int start = rng.Next(1, 10), d = rng.Next(2, 8);
                    q = $"{start}, {start + d}, {start + 2 * d}, {start + 3 * d}, ?\n<size=70%>다음에 올 수는?</size>"; ans = start + 4 * d; break;
                }
                default:
                {
                    int b = rng.Next(2, 10), r = rng.Next(2, 13);
                    q = $"{b * r} ÷ {b} = ?"; ans = r; break;
                }
            }

            // 헷갈리는 오답 3개 (정답 근처, 서로 다르고 0보다 큼)
            var set = new List<int> { ans };
            int[] offsets = { 1, -1, 2, -2, 10, -10, 5, -5, 3, -3, 4 };
            int guard = 0;
            while (set.Count < 4 && guard++ < 100)
            {
                int c = ans + offsets[rng.Next(offsets.Length)] * (rng.Next(3) == 0 ? 2 : 1);
                if (c > 0 && !set.Contains(c)) set.Add(c);
            }
            for (int i = set.Count; i < 4; i++) set.Add(ans + 7 * i);
            for (int i = set.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (set[i], set[j]) = (set[j], set[i]); }

            return new MathProblem { Question = q, Choices = set.ToArray(), Answer = set.IndexOf(ans) };
        }
    }

    // 맵 곳곳의 수학 문제 자리: 받침대 + 빛기둥·마법진(에셋 이펙트) + 떠서 도는 "?"
    public class QuizStation : MonoBehaviour
    {
        public static readonly List<QuizStation> All = new List<QuizStation>();

        public float Scale = 1f;
        public MathProblem Problem;
        public bool Solved { get; private set; }
        public bool KeepSolved;   // 모험의 문제: 한 번 풀면 끝 (다시 나오지 않음)
        public int Wrong;         // 틀린 횟수

        TextMeshPro m_Mark, m_Label;
        GameObject m_Effects;
        float m_ResetAt;
        System.Random m_Rng;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static QuizStation Create(Vector3 ground, float scale, int seed)
        {
            var go = new GameObject("Math Quiz Station");
            go.layer = 2;
            go.transform.position = ground;
            var st = go.AddComponent<QuizStation>();
            st.Scale = scale;
            st.m_Rng = new System.Random(seed);
            st.Problem = MathProblem.Generate(st.m_Rng);

            // 받침대 (돌기둥 + 빛나는 윗판)
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(pillar.GetComponent<Collider>());
            pillar.layer = 2;
            pillar.transform.SetParent(go.transform, false);
            pillar.transform.localPosition = new Vector3(0f, 0.45f, 0f) * scale;
            pillar.transform.localScale = new Vector3(0.7f, 0.45f, 0.7f) * scale;
            pillar.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.55f, 0.5f, 0.65f) };
            var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(top.GetComponent<Collider>());
            top.layer = 2;
            top.transform.SetParent(go.transform, false);
            top.transform.localPosition = new Vector3(0f, 0.92f, 0f) * scale;
            top.transform.localScale = new Vector3(0.85f, 0.04f, 0.85f) * scale;
            var glow = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(1f, 0.85f, 0.3f) };
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.2f) * 2f);
            top.GetComponent<Renderer>().material = glow;

            // 멀리서도 보이게: 에셋 이펙트(반복)를 계속 켜 둔다
            st.m_Effects = new GameObject("Effects");
            st.m_Effects.transform.SetParent(go.transform, false);
            Persistent("FX_LightPillar", st.m_Effects.transform, Vector3.zero, 0.8f * scale);
            Persistent("Magic circle", st.m_Effects.transform, Vector3.up * 0.03f * scale, 1.2f * scale);
            Persistent("Star aura", st.m_Effects.transform, Vector3.up * 2.3f * scale, 0.6f * scale);

            TMP_FontAsset font = MathModeAssets.Load() ? MathModeAssets.Load().Font : null;
            st.m_Mark = MakeText(go.transform, font, "?", 16f, new Color(1f, 0.85f, 0.3f), new Vector3(0f, 2.3f, 0f) * scale, scale);
            st.m_Label = MakeText(go.transform, font, "<b>수학 문제</b>", 3.2f, Color.white, new Vector3(0f, 1.45f, 0f) * scale, scale);
            return st;
        }

        public static GameObject Persistent(string name, Transform parent, Vector3 local, float size)
        {
            CombatAssets assets = CombatAssets.Load();
            GameObject prefab = assets ? assets.Effect(name) : null;
            if (!prefab) return null;
            var fx = Object.Instantiate(prefab, parent);
            fx.transform.localPosition = local;
            fx.transform.localScale = prefab.transform.localScale * size / Mathf.Max(0.0001f, parent.lossyScale.x);
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.loop = true;
            }
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true)) ps.Play(false);
            return fx;
        }

        static TextMeshPro MakeText(Transform parent, TMP_FontAsset font, string text, float size, Color color, Vector3 local, float scale)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var tmp = go.AddComponent<TextMeshPro>();
            if (font) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color32(40, 25, 0, 255);
            tmp.rectTransform.sizeDelta = new Vector2(10f, 4f);
            go.transform.localScale = Vector3.one * scale;
            return tmp;
        }

        public void MarkSolved()
        {
            Solved = true;
            m_ResetAt = Time.time + 40f;
            m_Mark.text = "O";
            m_Mark.color = new Color(0.4f, 1f, 0.5f);
            m_Label.text = "<b>정답!</b>";
            m_Effects.SetActive(false);
        }

        void Update()
        {
            // "?"가 둥실둥실 떠서 돈다 + 글자는 카메라를 본다
            float t = Time.time;
            m_Mark.transform.localPosition = new Vector3(0f, 2.3f + 0.15f * Mathf.Sin(t * 2f), 0f) * Scale;
            Camera cam = Camera.main;
            if (cam)
            {
                m_Mark.transform.rotation = Quaternion.LookRotation(m_Mark.transform.position - cam.transform.position) * Quaternion.Euler(0f, Mathf.Sin(t * 1.5f) * 25f, 0f);
                m_Label.transform.rotation = Quaternion.LookRotation(m_Label.transform.position - cam.transform.position);
            }

            // 40초 뒤 새 문제
            if (Solved && !KeepSolved && Time.time >= m_ResetAt)
            {
                Solved = false;
                Problem = MathProblem.Generate(m_Rng);
                m_Mark.text = "?";
                m_Mark.color = new Color(1f, 0.85f, 0.3f);
                m_Label.text = "<b>수학 문제</b>";
                m_Effects.SetActive(true);
            }
        }
    }

    // 맵 곳곳에 허수아비 무리와 수학 문제 자리를 놓는다 (맵마다 같은 자리 — 씬 이름으로 시드)
    public static class WorldPopulator
    {
        /// <summary>몹 무리 groups곳 (3~5마리: 허수아비·깡총이·가끔 거인), avoid 근처는 피함</summary>
        public static void PopulateMobs(Vector3 spawn, float scale, int seed, List<Vector3> avoid, int groups)
        {
            var rng = new System.Random(seed);
            var placed = new List<Vector3>(avoid);
            int made = 0;
            for (int i = 0; i < 1500 && made < groups; i++)
            {
                if (!Pick(rng, spawn, scale, 9f, 65f, 7f, placed, out Vector3 c)) continue;
                Vector3 look = spawn - c;
                look.y = 0f;
                Quaternion face = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;
                int n = 3 + rng.Next(3);
                bool brute = rng.Next(3) == 0;
                float theta = (float)rng.NextDouble() * Mathf.PI * 2f;
                // 정다각형 꼭짓점 (r cos(θ + 2πk/n), r sin(θ + 2πk/n))
                for (int k = 0; k < n; k++)
                {
                    float a = theta + k * Mathf.PI * 2f / n;
                    Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.5f * scale;
                    p.y = GeoFx.GroundY(p, scale);
                    MobKind kind = brute && k == 0 ? MobKind.Brute : (k % 2 == 1 ? MobKind.Hopper : MobKind.Dummy);
                    TrainingDummy.Create(p, face, scale, kind);
                }
                placed.Add(c);
                made++;
            }
        }

        // 평평하고, 시작 높이와 비슷하고, 물·벽이 아니고, 다른 것과 떨어진 땅 한 점
        static bool Pick(System.Random rng, Vector3 spawn, float s, float rMin, float rMax, float gap, List<Vector3> placed, out Vector3 point)
        {
            point = default;
            float r = Mathf.Lerp(rMin, rMax, Mathf.Sqrt((float)rng.NextDouble())) * s;
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector3 p = spawn + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            if (!Physics.Raycast(p + Vector3.up * 30f * s, Vector3.down, out RaycastHit hit, 80f * s, ~(1 << 2), QueryTriggerInteraction.Ignore))
                return false;
            if (hit.normal.y < 0.88f || Mathf.Abs(hit.point.y - spawn.y) > 8f * s) return false;
            string n = hit.collider.name.ToLowerInvariant();
            if (n.Contains("water") || n.Contains("sea") || n.Contains("ocean")) return false;
            if (Physics.CheckSphere(hit.point + Vector3.up * 1.3f * s, 0.6f * s, ~(1 << 2), QueryTriggerInteraction.Ignore)) return false;
            foreach (var q in placed)
                if ((q - hit.point).sqrMagnitude < gap * gap * s * s) return false;
            point = hit.point;
            return true;
        }
    }
}
