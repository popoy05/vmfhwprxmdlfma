using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace Unity.FPS.MathMode
{
    public class MathProblem
    {
        public string Question;
        public string[] Choices;
        public int CorrectIndex;
        public string Explanation;
        public MathFigure Figure;   // 칠판에 함께 그릴 그림 (없으면 null)
        public int Tier;            // MathProblemGenerator.TierNames 의 번호
    }

    // 좌표평면·그래프·기하 문제를 무작위로 만든다. 모든 답은 정수(또는 간단한 분수)가 되도록 역으로 설계한다.
    public static partial class MathProblemGenerator
    {
        static readonly Func<MathProblem>[] k_Generators =
        {
            PointOnLine, Distance, Midpoint, Slope, CircleEquation, ParabolaVertex, Intercept,
            Reflection, Quadrant, LineIntersection, TriangleArea, InteriorAngle, LineThroughTwoPoints,
        };

        // 쉬운 모드: 한눈에 풀 수 있는 유형만 (사분면, 대칭이동, y절편, 기본 도형의 내각)
        public static bool Easy;
        static readonly Func<MathProblem>[] k_EasyGenerators = { Quadrant, Reflection, Intercept, InteriorAngle };

        public static MathProblem Generate()
        {
            var pool = Easy ? k_EasyGenerators : k_Generators;
            return pool[Random.Range(0, pool.Length)]();
        }

        // ------------------------------------------------------------------ 문제 유형

        static MathProblem PointOnLine()
        {
            int a = NonZero(-3, 3), b = Random.Range(-5, 6), x0 = Random.Range(-4, 5);
            string correct = Pt(x0, a * x0 + b);
            var wrong = new List<string>();
            while (wrong.Count < 6)
            {
                int x = x0 + Random.Range(-2, 3), y = a * x + b + NonZero(-3, 3);
                wrong.Add(Pt(x, y));
            }

            return Build($"직선 {Line(a, b)} 위에 있는 점은?", correct, wrong,
                $"x = {x0}를 대입하면 y = {a}×({x0}) + ({b}) = {a * x0 + b}");
        }

        static MathProblem Distance()
        {
            var triples = new[] { (3, 4, 5), (6, 8, 10), (5, 12, 13), (8, 6, 10), (4, 3, 5), (9, 12, 15) };
            var (dx, dy, d) = triples[Random.Range(0, triples.Length)];
            int x1 = Random.Range(-5, 6), y1 = Random.Range(-5, 6);
            int x2 = x1 + dx * Sign(), y2 = y1 + dy * Sign();
            return Build($"두 점 A{Pt(x1, y1)}, B{Pt(x2, y2)} 사이의 거리는?", d.ToString(),
                new[] { dx + dy, d + 1, d - 1, d + 2, dx * dy / 2 }.Select(v => v.ToString()),
                $"√({dx}² + {dy}²) = √{dx * dx + dy * dy} = {d}  (피타고라스 정리)");
        }

        static MathProblem Midpoint()
        {
            int x1 = Random.Range(-6, 7), y1 = Random.Range(-6, 7);
            int x2 = x1 + 2 * NonZero(-4, 4), y2 = y1 + 2 * NonZero(-4, 4);
            int mx = (x1 + x2) / 2, my = (y1 + y2) / 2;
            return Build($"A{Pt(x1, y1)}, B{Pt(x2, y2)}의 중점 M의 좌표는?", Pt(mx, my),
                new[] { Pt(x1 + x2, y1 + y2), Pt(my, mx), Pt(mx + 1, my), Pt(mx, my - 1), Pt((x2 - x1) / 2, (y2 - y1) / 2) },
                $"M = (({x1}+{x2})/2, ({y1}+{y2})/2) = {Pt(mx, my)}");
        }

        static MathProblem Slope()
        {
            int x1 = Random.Range(-5, 6), y1 = Random.Range(-5, 6);
            int run = NonZero(-3, 3), rise = NonZero(-6, 6);
            int x2 = x1 + run, y2 = y1 + rise;
            string correct = Frac(rise, run);
            return Build($"두 점 {Pt(x1, y1)}, {Pt(x2, y2)}를 지나는 직선의 기울기는?", correct,
                new[] { Frac(run, rise), Frac(-rise, run), Frac(rise + 1, run), Frac(rise, run + 1), (rise + run).ToString() },
                $"기울기 = (y의 증가량)/(x의 증가량) = {rise}/{run} = {correct}");
        }

        static MathProblem CircleEquation()
        {
            var circles = new[] { (5, new[] { (3, 4), (4, 3), (5, 0), (0, 5) }), (10, new[] { (6, 8), (8, 6), (10, 0) }), (13, new[] { (5, 12), (12, 5) }) };
            var (r, pts) = circles[Random.Range(0, circles.Length)];
            var (px, py) = pts[Random.Range(0, pts.Length)];
            string correct = Pt(px * Sign(), py * Sign());
            var wrong = new List<string>();
            while (wrong.Count < 6)
            {
                int x = Random.Range(-r, r + 1), y = Random.Range(-r, r + 1);
                if (x * x + y * y != r * r) wrong.Add(Pt(x, y));
            }

            return Build($"원 x² + y² = {r * r} 위의 점은?", correct, wrong,
                $"원점에서 거리가 {r}인 점: {px}² + {py}² = {r * r}");
        }

        static MathProblem ParabolaVertex()
        {
            int a = NonZero(-2, 2), p = NonZero(-4, 4), q = Random.Range(-5, 6);
            string aStr = a == 1 ? "" : a == -1 ? "-" : a.ToString();
            string inner = p > 0 ? $"x - {p}" : $"x + {-p}";
            string eq = $"y = {aStr}({inner})²{SignedTerm(q)}";
            return Build($"포물선 {eq} 의 꼭짓점은?", Pt(p, q),
                new[] { Pt(-p, q), Pt(p, -q), Pt(q, p), Pt(-p, -q), Pt(a, q) },
                $"y = a(x - p)² + q 의 꼭짓점은 (p, q) = {Pt(p, q)}");
        }

        static MathProblem Intercept()
        {
            int a = NonZero(-4, 4), x0 = NonZero(-5, 5);
            int b = -a * x0;
            if (!Easy && Random.value < 0.5f) // 쉬운 모드는 y절편만
                return Build($"직선 {Line(a, b)} 의 x절편은?", x0.ToString(),
                    new[] { -x0, b, -b, a, x0 + 1 }.Select(v => v.ToString()),
                    $"y = 0을 대입: 0 = {a}x + ({b}) → x = {x0}");

            return Build($"직선 {Line(a, b)} 의 y절편은?", b.ToString(),
                new[] { -b, a, x0, -a, b + 1 }.Select(v => v.ToString()),
                $"x = 0을 대입하면 y = {b}");
        }

        static MathProblem Reflection()
        {
            int a, b;
            do { a = NonZero(-7, 7); b = NonZero(-7, 7); } while (Math.Abs(a) == Math.Abs(b));

            var options = new (string name, string result)[]
            {
                ("x축", Pt(a, -b)), ("y축", Pt(-a, b)), ("원점", Pt(-a, -b)), ("직선 y = x", Pt(b, a)),
            };
            var (name, correct) = options[Random.Range(0, options.Length)];
            return Build($"점 {Pt(a, b)}를 {name}에 대하여 대칭이동한 점은?", correct,
                options.Select(o => o.result).Append(Pt(a, b)),
                name switch
                {
                    "x축" => "x축 대칭: (x, y) → (x, -y)",
                    "y축" => "y축 대칭: (x, y) → (-x, y)",
                    "원점" => "원점 대칭: (x, y) → (-x, -y)",
                    _ => "y = x 대칭: (x, y) → (y, x)",
                });
        }

        static MathProblem Quadrant()
        {
            int a = NonZero(-9, 9), b = NonZero(-9, 9);
            int q = a > 0 ? (b > 0 ? 1 : 4) : (b > 0 ? 2 : 3);
            string[] names = { "제1사분면", "제2사분면", "제3사분면", "제4사분면" };
            return Build($"점 {Pt(a, b)}는 어느 사분면 위에 있을까?", names[q - 1], names,
                $"x {(a > 0 ? "> 0" : "< 0")}, y {(b > 0 ? "> 0" : "< 0")} 이므로 {names[q - 1]}");
        }

        static MathProblem LineIntersection()
        {
            int x0 = Random.Range(-4, 5), y0 = Random.Range(-4, 5);
            int a1 = NonZero(-3, 3), a2;
            do { a2 = NonZero(-3, 3); } while (a2 == a1);
            int b1 = y0 - a1 * x0, b2 = y0 - a2 * x0;
            return Build($"두 직선 {Line(a1, b1)}, {Line(a2, b2)} 의 교점은?", Pt(x0, y0),
                new[] { Pt(y0, x0), Pt(-x0, y0), Pt(x0, -y0), Pt(x0 + 1, y0 + a1), Pt(0, b1), Pt(0, b2) },
                $"{a1}x + ({b1}) = {a2}x + ({b2}) → x = {x0}, y = {y0}");
        }

        static MathProblem TriangleArea()
        {
            int x1 = Random.Range(-5, 3), y1 = Random.Range(-5, 3);
            int w = Random.Range(2, 9), h = Random.Range(2, 9);
            if (w * h % 2 != 0) w++;
            int s = Random.Range(0, w + 1);
            int area = w * h / 2;
            return Build($"세 점 A{Pt(x1, y1)}, B{Pt(x1 + w, y1)}, C{Pt(x1 + s, y1 + h)} 로 만든 삼각형의 넓이는?", area.ToString(),
                new[] { w * h, w + h, area + 2, area - 1, (w + h) * 2 }.Select(v => v.ToString()),
                $"밑변 AB = {w}, 높이 = {h} → ½ × {w} × {h} = {area}");
        }

        static MathProblem InteriorAngle()
        {
            int[] ns = Easy ? new[] { 3, 4, 6 } : new[] { 3, 4, 5, 6, 8, 9, 10, 12 };
            int n = ns[Random.Range(0, ns.Length)];
            int angle = 180 * (n - 2) / n;
            string[] names = { "", "", "", "정삼각형", "정사각형", "정오각형", "정육각형", "", "정팔각형", "정구각형", "정십각형", "", "정십이각형" };
            if (Easy || Random.value < 0.5f) // 쉬운 모드는 내각만
                return Build($"{names[n]}의 한 내각의 크기는?", $"{angle}°",
                    new[] { 180 * (n - 2), 360 / n, angle + 10, angle - 12, 180 - 360 / n + 6 }.Select(v => $"{v}°"),
                    $"내각의 합 180°×({n}-2) = {180 * (n - 2)}°, ÷ {n} = {angle}°");

            return Build($"{names[n]}의 한 외각의 크기는?", $"{360 / n}°",
                new[] { angle, 180 * (n - 2), 360 / n + 10, 360 / n * 2, 180 / n }.Select(v => $"{v}°"),
                $"외각의 합은 항상 360° → 360° ÷ {n} = {360 / n}°");
        }

        static MathProblem LineThroughTwoPoints()
        {
            int a = NonZero(-3, 3), b = Random.Range(-5, 6);
            int x1 = Random.Range(-3, 2), x2 = x1 + Random.Range(1, 4);
            return Build($"두 점 {Pt(x1, a * x1 + b)}, {Pt(x2, a * x2 + b)}를 지나는 직선은?", Line(a, b),
                new[] { Line(-a, b), Line(a, -b), Line(a, b + 1), Line(b == 0 ? a + 1 : b, a), Line(a + 1, b) },
                $"기울기 = {a}, x = {x1} 대입해서 y절편 = {b}");
        }

        // ------------------------------------------------------------------ 보조

        static MathProblem Build(string question, string correct, IEnumerable<string> wrongPool, string explanation)
        {
            var wrong = wrongPool.Where(w => w != correct).Distinct().OrderBy(_ => Random.value).Take(3).ToList();
            // 보기가 부족하면 정답을 살짝 변형해서 채운다
            for (int i = 1; wrong.Count < 3; i++)
            {
                string extra = correct + new string('\'', i);
                if (!wrong.Contains(extra)) wrong.Add(extra);
            }

            var choices = wrong.Append(correct).OrderBy(_ => Random.value).ToArray();
            return new MathProblem
            {
                Question = question,
                Choices = choices,
                CorrectIndex = Array.IndexOf(choices, correct),
                Explanation = explanation,
            };
        }

        public static string Pt(int x, int y) => $"({x}, {y})";
        public static string Pt(float x, float y) => $"({x:0.#}, {y:0.#})";

        public static string Line(int a, int b)
        {
            string ax = a switch { 0 => "", 1 => "x", -1 => "-x", _ => $"{a}x" };
            if (a == 0) return $"y = {b}";
            return $"y = {ax}{SignedTerm(b)}";
        }

        static string SignedTerm(int v) => v == 0 ? "" : v > 0 ? $" + {v}" : $" - {-v}";

        static string Frac(int num, int den)
        {
            if (den == 0) return "없음";
            int g = Gcd(Math.Abs(num), Math.Abs(den));
            num /= g;
            den /= g;
            if (den < 0) { num = -num; den = -den; }
            return den == 1 ? num.ToString() : $"{num}/{den}";
        }

        static int Gcd(int a, int b) => b == 0 ? Math.Max(a, 1) : Gcd(b, a % b);
        static int NonZero(int min, int max) { int v; do { v = Random.Range(min, max + 1); } while (v == 0); return v; }
        static int Sign() => Random.value < 0.5f ? -1 : 1;
    }
}
