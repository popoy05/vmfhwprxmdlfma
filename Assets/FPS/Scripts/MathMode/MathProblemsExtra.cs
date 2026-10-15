using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Unity.FPS.MathMode
{
    // 0단계 '기초 셈'(쉬움, 1+1 수준)과 5단계 '미적분 II 심화'(극한) 문제.
    public static partial class MathProblemGenerator
    {
        static readonly Func<MathProblem>[] k_VeryEasy = { ShapeAddition, CountShapes, ShapeName, CountCorners, ShapeTakeAway };

        static readonly Func<MathProblem>[] k_Calculus =
        {
            LimitExpLog, LimitTrig, ProductRuleExp, LnIntegral, SubstitutionSinCos, ImplicitCircle, ParametricSlope,
            LimitE, InverseDerivative, AreaParabolaLine, LogDerivative, MaxValueExp, GeometricSeries,
        };

        // ------------------------------------------------------------------ 0단계: 기초 셈 (그림을 보고 세기만 하면 된다)

        static readonly string[] k_ShapeWords = { "동그라미(○)", "세모(△)", "네모(□)" };

        // kind: 0 원, 1 세모, 2 네모
        static void SmallShape(MathFigure fig, int kind, Vector2 center, float r, Color color)
        {
            Vector2[] pts = kind switch
            {
                0 => MathFigure.Circle(center, r, 24),
                1 => Offset(MathFigure.RegularPolygon(3, r * 1.15f, 90f), center),
                _ => Offset(MathFigure.RegularPolygon(4, r * 1.2f, 45f), center),
            };
            fig.Line(color, 0.03f, true, pts);
        }

        static Vector2[] Offset(Vector2[] pts, Vector2 by)
        {
            for (int i = 0; i < pts.Length; i++) pts[i] += by;
            return pts;
        }

        static MathProblem ShapeAddition()
        {
            int kind = Random.Range(0, 3);
            int a = Random.Range(1, 4), b = Random.Range(1, 4);
            var fig = new MathFigure().Text(Vector2.zero, "+", MathFigure.Chalk);
            for (int i = 0; i < a; i++) SmallShape(fig, kind, new Vector2(-0.55f, 0.5f - i * 0.5f), 0.17f, MathFigure.Yellow);
            for (int i = 0; i < b; i++) SmallShape(fig, kind, new Vector2(0.55f, 0.5f - i * 0.5f), 0.17f, MathFigure.Cyan);
            int sum = a + b;
            var p = Build($"{k_ShapeWords[kind]} {a}개와 {b}개를 합치면 모두 몇 개?", $"{sum}개",
                new[] { $"{sum - 1}개", $"{sum + 1}개", $"{sum + 2}개", $"{a}개" },
                $"하나씩 세어 보면 {a} + {b} = {sum}");
            p.Figure = fig;
            return p;
        }

        static MathProblem CountShapes()
        {
            int kind = Random.Range(0, 3), other = (kind + Random.Range(1, 3)) % 3;
            int n = Random.Range(1, 6);
            int others = Random.Range(0, 7 - n);
            var cells = new System.Collections.Generic.List<Vector2>();
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                cells.Add(new Vector2(-0.6f + col * 0.6f, 0.6f - row * 0.6f));
            for (int i = 0; i < cells.Count; i++)
            {
                int j = Random.Range(i, cells.Count);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }

            var fig = new MathFigure();
            for (int i = 0; i < n; i++) SmallShape(fig, kind, cells[i], 0.18f, MathFigure.Yellow);
            for (int i = 0; i < others; i++) SmallShape(fig, other, cells[n + i], 0.18f, MathFigure.Pink);
            var p = Build($"칠판에 {k_ShapeWords[kind]}는 몇 개 있을까요?", $"{n}개",
                new[] { $"{n + 1}개", $"{n + others}개", $"{Mathf.Max(0, n - 1)}개", $"{n + 2}개" },
                $"{k_ShapeWords[kind]}만 세면 {n}개");
            p.Figure = fig;
            return p;
        }

        static MathProblem ShapeName()
        {
            string[] names = { "원", "삼각형", "사각형" };
            int kind = Random.Range(0, 3);
            var fig = new MathFigure();
            SmallShape(fig, kind, Vector2.zero, 0.7f, MathFigure.Yellow);
            var p = Build("칠판에 그려진 도형의 이름은?", names[kind], new[] { "원", "삼각형", "사각형", "오각형" },
                kind == 0 ? "둥글고 뾰족한 곳이 없으면 원" : kind == 1 ? "변이 3개면 삼각형" : "변이 4개면 사각형");
            p.Figure = fig;
            return p;
        }

        static MathProblem CountCorners()
        {
            bool triangle = Random.value < 0.5f;
            int n = triangle ? 3 : 4;
            var fig = new MathFigure();
            Vector2[] pts = MathFigure.RegularPolygon(n, 0.7f, triangle ? 90f : 45f);
            fig.Line(MathFigure.Yellow, 0.035f, true, pts);
            foreach (Vector2 v in pts) fig.Dot(v, MathFigure.Pink);
            var p = Build($"{(triangle ? "세모" : "네모")}의 뾰족한 꼭짓점(분홍 점)은 몇 개?", $"{n}개",
                new[] { "2개", "3개", "4개", "5개" },
                $"분홍 점을 세면 {n}개");
            p.Figure = fig;
            return p;
        }

        static MathProblem ShapeTakeAway()
        {
            int kind = Random.Range(0, 3);
            int a = Random.Range(2, 6);
            var fig = new MathFigure();
            for (int i = 0; i < a; i++)
            {
                Vector2 c = new Vector2(-0.7f + (i % 3) * 0.7f, i < 3 ? 0.35f : -0.35f);
                SmallShape(fig, kind, c, 0.2f, MathFigure.Yellow);
                if (i == a - 1) // 마지막 하나에 X 표시
                {
                    fig.Line(MathFigure.Pink, 0.04f, false, c + new Vector2(-0.25f, -0.25f), c + new Vector2(0.25f, 0.25f));
                    fig.Line(MathFigure.Pink, 0.04f, false, c + new Vector2(-0.25f, 0.25f), c + new Vector2(0.25f, -0.25f));
                }
            }

            var p = Build($"{k_ShapeWords[kind]} {a}개 중 1개를 지우면(X) 몇 개가 남을까요?", $"{a - 1}개",
                new[] { $"{a}개", $"{a + 1}개", $"{Mathf.Max(0, a - 2)}개", "1개" },
                $"{a} - 1 = {a - 1}");
            p.Figure = fig;
            return p;
        }

        // ------------------------------------------------------------------ 5단계: 미적분 II 심화

        const string k_Lim0 = "lim<sub>x→0</sub>";
        const string k_LimInf = "lim<sub>n→∞</sub>";

        // c·e^k 표기 (c = 1 이면 생략, k = 1 이면 e)
        static string ExpTerm(int c, int k)
        {
            if (c == 0) return "0";
            string e = k == 0 ? "1" : k == 1 ? "e" : $"e<sup>{k}</sup>";
            if (k == 0) return c.ToString();
            return c == 1 ? e : c == -1 ? "-" + e : $"{c}{e}";
        }

        static MathProblem LimitExpLog()
        {
            int a = Random.Range(1, 7), b;
            do { b = Random.Range(1, 7); } while (b == a);
            return Build($"{k_Lim0} (e<sup>{a}x</sup> - 1) / ln(1 + {b}x) 의 값은?", Frac(a, b),
                new[] { Frac(b, a), Frac(a * a, b), Frac(a, b * b), "1", Frac(a + 1, b), "0" },
                $"(e<sup>{a}x</sup>-1)/({a}x) → 1, ln(1+{b}x)/({b}x) → 1 이므로 {a}/{b} = {Frac(a, b)}");
        }

        static MathProblem LimitTrig()
        {
            int a = Random.Range(2, 7);
            if (Random.value < 0.5f)
            {
                int b;
                do { b = Random.Range(1, 7); } while (b == a);
                return Build($"{k_Lim0} sin {a}x / tan {b}x 의 값은?", Frac(a, b),
                    new[] { Frac(b, a), Frac(a * a, b * b), "1", Frac(a, 2 * b), "0" },
                    $"sin {a}x ≈ {a}x, tan {b}x ≈ {b}x → {Frac(a, b)}");
            }

            return Build($"{k_Lim0} (1 - cos {a}x) / x² 의 값은?", Frac(a * a, 2),
                new[] { Frac(a, 2), (a * a).ToString(), Frac(a * a, 4), a.ToString(), "0" },
                $"1 - cos {a}x = 2sin²({a}x/2) ≈ ({a}x)²/2 → {Frac(a * a, 2)}");
        }

        static MathProblem ProductRuleExp()
        {
            int a;
            do { a = Random.Range(-2, 4); } while (a == 0 || a == -2);
            string f = $"x²e<sup>{(a == 1 ? "" : a == -1 ? "-" : a.ToString())}x</sup>";
            return Build($"f(x) = {f} 일 때 f'(1)의 값은?", ExpTerm(2 + a, a),
                new[] { ExpTerm(a, a), ExpTerm(2, a), ExpTerm(2 + a, a - 1), ExpTerm(1 + a, a), ExpTerm(2 * a, a) },
                $"f'(x) = 2xe<sup>{a}x</sup> + {a}x²e<sup>{a}x</sup> → f'(1) = (2 + {a})e<sup>{a}</sup>");
        }

        static MathProblem LnIntegral()
        {
            int k = Random.Range(1, 4);
            string upper = k == 1 ? "e" : $"e<sup>{k}</sup>";
            string correct = k == 1 ? "1" : $"{ExpTerm(k - 1, k)} + 1";
            return Build($"∫<sub>1</sub><sup>{upper}</sup> ln x dx 의 값은?", correct,
                new[] { $"{ExpTerm(k, k)} + 1", $"{ExpTerm(k, k)} - 1", k == 1 ? "e" : $"{ExpTerm(k - 1, k)} - 1", $"{ExpTerm(k + 1, k)} + 1", k.ToString() },
                $"부분적분: ∫ln x dx = x ln x - x → [{k}·{upper} - {upper}] - [0 - 1] = {correct}");
        }

        static MathProblem SubstitutionSinCos()
        {
            int n = Random.Range(1, 7);
            return Build($"∫<sub>0</sub><sup>π/2</sup> sin<sup>{n}</sup>x · cos x dx 의 값은?", Frac(1, n + 1),
                new[] { Frac(1, n), Frac(1, n + 2), Frac(n, n + 1), Frac(2, n + 1), "1" },
                $"t = sin x 로 치환: ∫<sub>0</sub><sup>1</sup> t<sup>{n}</sup> dt = 1/{n + 1}");
        }

        static MathProblem ImplicitCircle()
        {
            var triples = new[] { (3, 4, 5), (4, 3, 5), (6, 8, 10), (8, 6, 10), (5, 12, 13), (12, 5, 13) };
            var (a, b, r) = triples[Random.Range(0, triples.Length)];
            a *= Sign();
            b *= Sign();
            return Build($"곡선 x² + y² = {r * r} 위의 점 ({a}, {b})에서의 접선의 기울기 dy/dx 는?", Frac(-a, b),
                new[] { Frac(a, b), Frac(-b, a), Frac(b, a), Frac(-a, 2 * b), "0" },
                $"음함수 미분: 2x + 2y·y' = 0 → y' = -x/y = {Frac(-a, b)}");
        }

        static MathProblem ParametricSlope()
        {
            int t0, p, q, num, den;
            do
            {
                t0 = Random.Range(1, 4);
                p = Random.Range(0, 4);
                q = Random.Range(1, 6);
                num = 3 * t0 * t0 - q;
                den = 2 * t0 + p;
            } while (num == 0 || den == 0);

            string x = p == 0 ? "t²" : $"t² + {p}t";
            return Build($"x = {x}, y = t³ - {q}t 일 때, t = {t0} 에서 dy/dx 의 값은?", Frac(num, den),
                new[] { Frac(den, num), Frac(3 * t0 * t0 + q, den), Frac(num, 2 * t0), Frac(3 * t0 - q, den), num.ToString() },
                $"dy/dx = (dy/dt)/(dx/dt) = (3t² - {q})/(2t + {p}) = {num}/{den}");
        }

        static MathProblem LimitE()
        {
            int a = Random.Range(1, 4), b = Random.Range(1, 4);
            string inner = a == 1 ? "1/n" : $"{a}/n";
            string pow = b == 1 ? "n" : $"{b}n";
            return Build($"{k_LimInf} (1 + {inner})<sup>{pow}</sup> 의 값은?", ExpTerm(1, a * b),
                new[] { ExpTerm(1, a + b), ExpTerm(1, a), ExpTerm(1, b), (a * b).ToString(), ExpTerm(1, a * b + 1), "1" },
                $"(1 + {inner})<sup>n</sup> → e<sup>{a}</sup> 이므로 (e<sup>{a}</sup>)<sup>{b}</sup> = {ExpTerm(1, a * b)}");
        }

        static MathProblem InverseDerivative()
        {
            int a = Random.Range(1, 7), b = Random.Range(-3, 4);
            string bTerm = b == 0 ? "" : b > 0 ? $" + {b}" : $" - {-b}";
            int y = 1 + a + b;
            return Build($"f(x) = x³ + {a}x{bTerm} 의 역함수를 g(x)라 할 때, g'({y}) 의 값은?", Frac(1, 3 + a),
                new[] { (3 + a).ToString(), Frac(1, 3 * a), Frac(1, 1 + a), Frac(1, 3 + a + Mathf.Abs(b) + 1), Frac(3, a) },
                $"f(1) = {y} 이므로 g'({y}) = 1/f'(1) = 1/(3 + {a}) = {Frac(1, 3 + a)}");
        }

        static MathProblem AreaParabolaLine()
        {
            int k = Random.Range(1, 7);
            string line = k == 1 ? "y = x" : $"y = {k}x";
            return Build($"곡선 y = x² 과 직선 {line} 로 둘러싸인 부분의 넓이는?", Frac(k * k * k, 6),
                new[] { Frac(k * k * k, 3), Frac(k * k, 2), Frac(k * k * k, 2), Frac(k * k * k, 12), Frac(k * k, 6) },
                $"∫<sub>0</sub><sup>{k}</sup> ({k}x - x²) dx = {k}³/2 - {k}³/3 = {Frac(k * k * k, 6)}");
        }

        static MathProblem LogDerivative()
        {
            int c = Random.Range(1, 4), a = Random.Range(1, 6);
            return Build($"f(x) = ln(x² + {a}) 일 때 f'({c}) 의 값은?", Frac(2 * c, c * c + a),
                new[] { Frac(1, c * c + a), Frac(2 * c, c * c), Frac(2 * c, a), Frac(c, c * c + a), Frac(2, c * c + a) },
                $"f'(x) = 2x/(x² + {a}) → {2 * c}/{c * c + a}");
        }

        static MathProblem MaxValueExp()
        {
            int a = Random.Range(1, 5);
            string correct = a == 1 ? "1/e" : $"1/({a}e)";
            string fx = a == 1 ? "xe<sup>-x</sup>" : $"xe<sup>-{a}x</sup>";
            return Build($"x > 0 에서 f(x) = {fx} 의 최댓값은?", correct,
                new[] { a == 1 ? "e" : $"{a}/e", a == 1 ? "1/e²" : $"1/({a}e²)", a == 1 ? "1" : $"1/{a}", $"1/({a + 1}e)", "2/e" },
                $"f'(x) = e<sup>-{a}x</sup>(1 - {a}x) = 0 → x = 1/{a}, f(1/{a}) = {correct}");
        }

        static MathProblem GeometricSeries()
        {
            int q = Random.Range(2, 8), p = Random.Range(1, q);
            return Build($"Σ<sub>n=1</sub><sup>∞</sup> ({p}/{q})<sup>n</sup> 의 값은?", Frac(p, q - p),
                new[] { Frac(q, q - p), Frac(p, q), Frac(p, q + p), Frac(q - p, p), Frac(1, q - p) },
                $"첫째항 {p}/{q}, 공비 {p}/{q} → ({p}/{q}) / (1 - {p}/{q}) = {Frac(p, q - p)}");
        }
    }
}
