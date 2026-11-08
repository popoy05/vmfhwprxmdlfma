using System.Collections.Generic;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 수학 문제 하나.  보기 4개(객관식) 또는 직접 입력(주관식 — 찍어서 맞힐 수 없음)
    //  난이도(쉬움·보통·어려움)는 캐릭터 꾸미기 화면에서 고른다. 모든 답은 정수이고, 차근차근 생각하면 풀 수 있게.
    //  틀리면 힌트가 나온다.
    public class MathProblem
    {
        public string Question, Hint;
        public int[] Choices;   // 객관식 보기 (주관식이면 null)
        public int Answer;      // 객관식: 정답 보기 번호 (0~3)
        public int Value;       // 정답 값
        public bool Typed => Choices == null;

        public static readonly string[] LevelNames = { "쉬움", "보통", "어려움" };
        public static readonly string[] LevelInfo =
        {
            "덧셈·뺄셈·구구단·나눗셈 · 주관식 1문제",
            "세 자리 계산·방정식·넓이·각도·백분율 · 주관식 2문제",
            "두 자리 곱셈·양변 방정식·거듭제곱·피타고라스 · 모두 주관식",
        };
        const string k_Pref = "NewMode.QuizLevel";
        public static int Level
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(k_Pref, 1), 0, 2);
            set { PlayerPrefs.SetInt(k_Pref, Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); }
        }

        /// <summary>한 받침대에서 푸는 문제 묶음 (count개). 난이도에 따라 몇 개는 직접 입력</summary>
        public static MathProblem[] Set(System.Random rng, int count)
        {
            int level = Level;
            int typed = level == 0 ? 1 : level == 1 ? 2 : count;
            var list = new MathProblem[count];
            for (int i = 0; i < count; i++)
                list[i] = Generate(rng, level, i >= count - typed);
            return list;
        }

        public static MathProblem Generate(System.Random rng) => Generate(rng, Level, false);

        public static MathProblem Generate(System.Random rng, int level, bool typed)
        {
            var (q, ans, hint) = level == 0 ? Easy(rng) : level == 1 ? Normal(rng) : Hard(rng);
            var p = new MathProblem { Question = q, Value = ans, Hint = hint };
            if (typed) return p;

            // 헷갈리는 오답 3개 (정답 근처, 서로 다름)
            var set = new List<int> { ans };
            int[] offsets = { 1, -1, 2, -2, 10, -10, 5, -5, 3, -3, 4 };
            int guard = 0;
            while (set.Count < 4 && guard++ < 100)
            {
                int c = ans + offsets[rng.Next(offsets.Length)] * (rng.Next(3) == 0 ? 2 : 1);
                if ((c > 0 || ans <= 0) && !set.Contains(c)) set.Add(c);
            }
            for (int i = set.Count; i < 4; i++) set.Add(ans + 7 * i);
            for (int i = set.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (set[i], set[j]) = (set[j], set[i]); }
            p.Choices = set.ToArray();
            p.Answer = set.IndexOf(ans);
            return p;
        }

        static (string, int, string) Easy(System.Random rng)
        {
            switch (rng.Next(8))
            {
                case 0: { int a = rng.Next(12, 60), b = rng.Next(12, 40); return ($"{a} + {b} = ?", a + b, $"일의 자리끼리 먼저: {a % 10} + {b % 10}"); }
                case 1: { int a = rng.Next(30, 90), b = rng.Next(11, a - 5); return ($"{a} − {b} = ?", a - b, $"{a}에서 {b / 10 * 10}를 먼저 빼고, 남은 {b % 10}를 더 빼 보세요"); }
                case 2: { int a = rng.Next(2, 10), b = rng.Next(2, 10); return ($"{a} × {b} = ?", a * b, $"{a}단: {a}를 {b}번 더해요"); }
                case 3: { int b = rng.Next(2, 10), r = rng.Next(2, 10); return ($"{b * r} ÷ {b} = ?", r, $"{b} × □ = {b * r} 의 □를 찾아요"); }
                case 4: { int w = rng.Next(3, 10), h = rng.Next(2, 9); return ($"가로 {w}, 세로 {h}인\n직사각형의 둘레는?", 2 * (w + h), "둘레 = (가로 + 세로) × 2"); }
                case 5: { int s = rng.Next(1, 10), d = rng.Next(2, 6); return ($"{s}, {s + d}, {s + 2 * d}, {s + 3 * d}, ?\n<size=70%>다음에 올 수는?</size>", s + 4 * d, $"앞뒤 수의 차이가 늘 {d}이에요"); }
                case 6: { int a = rng.Next(5, 20), s = a + rng.Next(5, 20); return ($"□ + {a} = {s}\n<size=70%>□는?</size>", s - a, $"{s}에서 {a}를 빼 보세요"); }
                default: { int n = rng.Next(3, 10); return ($"한 변이 {n}인 정사각형의\n둘레는?", 4 * n, "정사각형은 네 변의 길이가 모두 같아요"); }
            }
        }

        static (string, int, string) Normal(System.Random rng)
        {
            switch (rng.Next(9))
            {
                case 0: { int a = rng.Next(120, 600), b = rng.Next(100, 400); return ($"{a} + {b} = ?", a + b, "백의 자리, 십의 자리, 일의 자리를 나눠서 더해요"); }
                case 1: { int a = rng.Next(12, 40), b = rng.Next(3, 9); return ($"{a} × {b} = ?", a * b, $"{a / 10 * 10} × {b} 와 {a % 10} × {b} 를 더해요"); }
                case 2: { int a = rng.Next(2, 6), x = rng.Next(2, 12), b = rng.Next(1, 15); return ($"{a}x + {b} = {a * x + b}\n<size=70%>x는?</size>", x, $"양쪽에서 {b}를 빼면 {a}x = {a * x}"); }
                case 3: { int w = rng.Next(4, 13), h = rng.Next(3, 12); return ($"가로 {w}, 세로 {h}인\n직사각형의 넓이는?", w * h, "넓이 = 가로 × 세로"); }
                case 4: { int a = rng.Next(30, 81), b = rng.Next(30, 81); return ($"삼각형의 두 각이 {a}°, {b}°일 때\n나머지 한 각은?", 180 - a - b, "삼각형 세 각의 합은 180°"); }
                case 5:
                {
                    int[] bases = { 40, 60, 80, 120, 200, 300 }; int[] pcts = { 10, 20, 25, 50, 75 };
                    int b, pc; do { b = bases[rng.Next(bases.Length)]; pc = pcts[rng.Next(pcts.Length)]; } while (b * pc % 100 != 0);
                    return ($"{b}의 {pc}%는?", b * pc / 100, $"{pc}% = {pc}/100 이에요. {b} × {pc} ÷ 100");
                }
                case 6: { int n = rng.Next(4, 16); return ($"{n}<sup>2</sup> = ?", n * n, $"{n} × {n}"); }
                case 7: { int a = rng.Next(2, 20), b = rng.Next(2, 9), c = rng.Next(2, 9); return ($"{a} + {b} × {c} = ?", a + b * c, "곱셈을 덧셈보다 먼저 해요"); }
                default:
                {
                    int x = rng.Next(5, 30), y = rng.Next(5, 30), z = rng.Next(5, 30);
                    int sum = x + y + z; int fix = sum % 3; z -= fix; if (z < 1) z += 3;
                    return ($"{x}, {y}, {z}의 평균은?", (x + y + z) / 3, "평균 = 모두 더한 값 ÷ 개수");
                }
            }
        }

        static (string, int, string) Hard(System.Random rng)
        {
            switch (rng.Next(10))
            {
                case 0: { int a = rng.Next(12, 50), b = rng.Next(11, 30); return ($"{a} × {b} = ?", a * b, $"{a} × {b / 10 * 10} 와 {a} × {b % 10} 를 더해요"); }
                case 1:
                {
                    int x = rng.Next(-6, 10), a = rng.Next(3, 8), c = rng.Next(1, a), b = rng.Next(-10, 15);
                    int d = a * x + b - c * x;
                    return ($"{a}x {Sign(b)} = {c}x {Sign(d)}\n<size=70%>x는?</size>", x, $"x를 한쪽으로 모으면 {a - c}x = {d - b}");
                }
                case 2: { int n = rng.Next(11, 26); return ($"√{n * n} = ?", n, $"□ × □ = {n * n} 인 □를 찾아요 (끝자리를 보면 쉬워요)"); }
                case 3: { int e = rng.Next(5, 11); return ($"2<sup>{e}</sup> = ?", 1 << e, "2를 계속 두 배: 2, 4, 8, 16, …"); }
                case 4: { int b = rng.Next(3, 15) * 2, h = rng.Next(3, 14); return ($"밑변 {b}, 높이 {h}인\n삼각형의 넓이는?", b * h / 2, "삼각형 넓이 = 밑변 × 높이 ÷ 2"); }
                case 5: { int n = rng.Next(10, 41); return ($"1 + 2 + 3 + … + {n} = ?", n * (n + 1) / 2, "(처음 + 끝) × 개수 ÷ 2"); }
                case 6: { int a = rng.Next(2, 9), b = rng.Next(2, 9); return ($"({a} + {b})<sup>2</sup> = ?", (a + b) * (a + b), "괄호 안을 먼저 계산한 뒤 제곱해요"); }
                case 7: { int a = rng.Next(5, 20), b = rng.Next(2, 9), c = rng.Next(2, 9); return ($"−{a} + {b} × (−{c}) = ?", -a - b * c, "음수 × 양수 = 음수. 곱셈 먼저!"); }
                case 8: { int k = rng.Next(1, 6); int[][] t = { new[] { 3, 4, 5 }, new[] { 5, 12, 13 }, new[] { 8, 15, 17 } }; var tr = t[rng.Next(t.Length)]; return ($"직각삼각형의 두 변이 {tr[0] * k}, {tr[1] * k}일 때\n빗변의 길이는?", tr[2] * k, "a² + b² = c² (피타고라스)"); }
                default: { int n = rng.Next(5, 11); return ($"{n}각형의 내각의 합은 몇 도?", (n - 2) * 180, "n각형은 삼각형 (n − 2)개로 나눌 수 있어요"); }
            }
        }

        static string Sign(int v) => v >= 0 ? $"+ {v}" : $"− {-v}";
    }

    // 맵 곳곳의 수학 문제 자리: 받침대 + 빛기둥·마법진(에셋 이펙트) + 떠서 도는 "?"
    public class QuizStation : MonoBehaviour
    {
        public static readonly List<QuizStation> All = new List<QuizStation>();

        public float Scale = 1f;
        public MathProblem[] Problems;   // 이 받침대에서 차례로 푸는 문제들
        public int Progress;              // 지금까지 맞힌 수
        public MathProblem Problem => Problems[Mathf.Min(Progress, Problems.Length - 1)];
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
            st.Problems = MathProblem.Set(st.m_Rng, 3);

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

        public void RefreshLabel()
        {
            if (!Solved && m_Label) m_Label.text = $"<b>수학 문제</b>  {Progress}/{Problems.Length}";
        }

        public void MarkSolved()
        {
            Solved = true;
            m_ResetAt = Time.time + 40f;
            m_Mark.text = "O";
            m_Mark.color = new Color(0.4f, 1f, 0.5f);
            m_Label.text = "<b>완료!</b>";
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
                Problems = MathProblem.Set(m_Rng, 3);
                Progress = 0;
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
