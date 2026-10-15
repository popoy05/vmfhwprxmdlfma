using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Unity.FPS.MathMode
{
    // 관문 문제의 단계 (난이도마다 쓰는 단계는 DifficultyPreset.StationTiers)
    //  0 기초 셈     (쉬움: 1+1 수준. 도형 개수 세기, 도형 이름, 도형 더하기·빼기)
    //  1 기초 도형   (초등학생 수준: 변의 개수, 둘레, 넓이, 삼각형의 각, 원의 지름, 직육면체)
    //  2 좌표평면   (점의 좌표 읽기, 사분면, 대칭이동, 축과 나란한 두 점 사이 거리)
    //  3 그래프     (그래프 보고 y절편·기울기·식 고르기, 포물선 꼭짓점)
    //  4 도전       (중학교 심화 전체)
    //  5 미적분 II    (극한: 지수·로그·삼각함수의 극한과 미분, 치환·부분적분, 급수)
    public static partial class MathProblemGenerator
    {
        public static readonly string[] TierNames = { "기초 셈", "기초 도형", "좌표평면", "그래프", "도전 문제", "미적분 II 심화" };
        public static readonly Color[] TierColors =
        {
            new Color(0.5f, 0.95f, 0.85f), new Color(0.35f, 0.85f, 0.5f), new Color(0.35f, 0.65f, 1f),
            new Color(0.75f, 0.5f, 1f), new Color(1f, 0.6f, 0.3f), new Color(0.95f, 0.25f, 0.45f),
        };

        static readonly Func<MathProblem>[] k_Tier0 = { PolygonSides, SquarePerimeter, RectangleArea, TriangleAngle, CircleDiameter, CuboidCount };
        static readonly Func<MathProblem>[] k_Tier1 = { ReadPoint, QuadrantWithFigure, ReflectionWithFigure, AxisDistance };
        static readonly Func<MathProblem>[] k_Tier2 = { ReadIntercept, ReadSlope, WhichLine, ReadVertex };

        public static int ClampTier(int tier) => Mathf.Clamp(tier, 0, TierNames.Length - 1);

        public static MathProblem Generate(int tier)
        {
            tier = ClampTier(tier);
            Func<MathProblem>[] pool = tier switch
            {
                0 => k_VeryEasy,
                1 => k_Tier0,
                2 => k_Tier1,
                3 => k_Tier2,
                4 => k_Generators,
                _ => k_Calculus,
            };
            MathProblem p = pool[Random.Range(0, pool.Length)]();
            p.Tier = tier;
            return p;
        }

        // ------------------------------------------------------------------ 0단계: 기초 도형

        static MathProblem PolygonSides()
        {
            int[] ns = { 3, 4, 5, 6, 8 };
            string[] names = { "", "", "", "삼각형", "사각형", "오각형", "육각형", "", "팔각형" };
            int n = ns[Random.Range(0, ns.Length)];
            var fig = new MathFigure().Line(MathFigure.Yellow, 0.035f, true, MathFigure.RegularPolygon(n, 0.75f, 90f));
            foreach (Vector2 v in MathFigure.RegularPolygon(n, 0.75f, 90f)) fig.Dot(v, MathFigure.Chalk);
            var p = Build("이 도형의 변은 몇 개일까요?", $"{n}개",
                new[] { $"{n - 1}개", $"{n + 1}개", $"{n + 2}개", $"{n * 2}개" },
                $"{names[n]}: 변 {n}개, 꼭짓점 {n}개");
            p.Figure = fig;
            return p;
        }

        static MathProblem SquarePerimeter()
        {
            int a = Random.Range(2, 10);
            var sq = new[] { new Vector2(-0.6f, -0.6f), new Vector2(0.6f, -0.6f), new Vector2(0.6f, 0.6f), new Vector2(-0.6f, 0.6f) };
            var fig = new MathFigure().Line(MathFigure.Yellow, 0.035f, true, sq).Text(new Vector2(0f, -0.82f), $"{a}cm", MathFigure.Chalk);
            var p = Build($"정사각형의 한 변이 {a}cm일 때 둘레는?", $"{4 * a}cm",
                new[] { $"{a * a}cm", $"{2 * a}cm", $"{3 * a}cm", $"{4 * a + 2}cm" },
                $"정사각형은 네 변의 길이가 같아요: {a} × 4 = {4 * a}");
            p.Figure = fig;
            return p;
        }

        static MathProblem RectangleArea()
        {
            int w = Random.Range(3, 9), h = Random.Range(2, 6);
            float fw = 0.8f, fh = 0.8f * h / w;
            fh = Mathf.Clamp(fh, 0.3f, 0.8f);
            var rect = new[] { new Vector2(-fw, -fh), new Vector2(fw, -fh), new Vector2(fw, fh), new Vector2(-fw, fh) };
            var fig = new MathFigure().Line(MathFigure.Cyan, 0.035f, true, rect)
                .Text(new Vector2(0f, -fh - 0.2f), $"{w}cm", MathFigure.Chalk)
                .Text(new Vector2(fw + 0.25f, 0f), $"{h}cm", MathFigure.Chalk);
            var p = Build("직사각형의 넓이는?", $"{w * h}cm²",
                new[] { $"{w + h}cm²", $"{2 * (w + h)}cm²", $"{w * h + w}cm²", $"{w * h - h}cm²" },
                $"넓이 = 가로 × 세로 = {w} × {h} = {w * h}");
            p.Figure = fig;
            return p;
        }

        static MathProblem TriangleAngle()
        {
            int a, b;
            do { a = Random.Range(3, 9) * 10; b = Random.Range(3, 9) * 10; } while (a + b > 150);
            int c = 180 - a - b;
            var A = new Vector2(-0.8f, -0.6f);
            var B = new Vector2(0.8f, -0.6f);
            var C = new Vector2(-0.1f, 0.7f);
            var fig = new MathFigure().Line(MathFigure.Pink, 0.035f, true, A, B, C)
                .Text(A + new Vector2(0.28f, 0.13f), $"{a}°", MathFigure.Chalk)
                .Text(B + new Vector2(-0.28f, 0.13f), $"{b}°", MathFigure.Chalk)
                .Text(C + new Vector2(0f, -0.28f), "?", MathFigure.Yellow);
            var p = Build("삼각형의 나머지 한 각 ?는 몇 도일까요?", $"{c}°",
                new[] { $"{360 - a - b}°", $"{a + b}°", $"{c + 10}°", $"{90 - c / 2}°" },
                $"삼각형 세 각의 합은 180°: 180 - {a} - {b} = {c}");
            p.Figure = fig;
            return p;
        }

        static MathProblem CircleDiameter()
        {
            int r = Random.Range(2, 10);
            var fig = new MathFigure().Line(MathFigure.Cyan, 0.035f, true, MathFigure.Circle(Vector2.zero, 0.75f))
                .Line(MathFigure.Yellow, 0.03f, false, Vector2.zero, new Vector2(0.75f, 0f))
                .Dot(Vector2.zero, MathFigure.Chalk)
                .Text(new Vector2(0.37f, 0.15f), $"{r}cm", MathFigure.Yellow);
            var p = Build($"원의 반지름이 {r}cm일 때 지름은?", $"{2 * r}cm",
                new[] { $"{r}cm", $"{r * r}cm", $"{3 * r}cm", $"{2 * r + 1}cm" },
                $"지름 = 반지름 × 2 = {r} × 2 = {2 * r}");
            p.Figure = fig;
            return p;
        }

        static MathProblem CuboidCount()
        {
            // 직육면체 겨냥도
            var f = new[] { new Vector2(-0.7f, -0.6f), new Vector2(0.3f, -0.6f), new Vector2(0.3f, 0.3f), new Vector2(-0.7f, 0.3f) };
            Vector2 d = new Vector2(0.4f, 0.3f);
            var fig = new MathFigure().Line(MathFigure.Yellow, 0.03f, true, f)
                .Line(MathFigure.Yellow, 0.03f, false, f[3], f[3] + d, f[2] + d, f[1] + d)
                .Line(MathFigure.Yellow, 0.03f, false, f[2], f[2] + d);

            int kind = Random.Range(0, 3);
            string[] what = { "꼭짓점", "면", "모서리" };
            int[] answer = { 8, 6, 12 };
            var p = Build($"직육면체의 {what[kind]}은(는) 몇 개일까요?", $"{answer[kind]}개",
                new[] { "4개", "6개", "8개", "10개", "12개" },
                "직육면체: 면 6개, 모서리 12개, 꼭짓점 8개");
            p.Figure = fig;
            return p;
        }

        // ------------------------------------------------------------------ 1단계: 좌표평면

        static MathProblem ReadPoint()
        {
            int x = NonZero(-4, 4), y = NonZero(-4, 4);
            var fig = MathFigure.Axes().Dot(MathFigure.G(x, y), MathFigure.Yellow)
                .Text(MathFigure.G(x, y) + new Vector2(0.12f, 0.12f), "A", MathFigure.Yellow);
            var p = Build("점 A의 좌표는? (한 칸 = 1)", Pt(x, y),
                new[] { Pt(y, x), Pt(-x, y), Pt(x, -y), Pt(-x, -y) },
                $"원점에서 오른쪽(+)/왼쪽(-)으로 {Math.Abs(x)}칸 → x = {x}, 위(+)/아래(-)로 {Math.Abs(y)}칸 → y = {y}");
            p.Figure = fig;
            return p;
        }

        static MathProblem QuadrantWithFigure()
        {
            MathProblem p = Quadrant();
            var fig = MathFigure.Axes()
                .Text(MathFigure.G(2.5f, 2.5f), "1", MathFigure.Faint).Text(MathFigure.G(-2.5f, 2.5f), "2", MathFigure.Faint)
                .Text(MathFigure.G(-2.5f, -2.5f), "3", MathFigure.Faint).Text(MathFigure.G(2.5f, -2.5f), "4", MathFigure.Faint);
            p.Figure = fig;
            return p;
        }

        static MathProblem ReflectionWithFigure()
        {
            int a, b;
            do { a = NonZero(-4, 4); b = NonZero(-4, 4); } while (Math.Abs(a) == Math.Abs(b));
            bool overX = Random.value < 0.5f;
            string correct = overX ? Pt(a, -b) : Pt(-a, b);
            var fig = MathFigure.Axes().Dot(MathFigure.G(a, b), MathFigure.Yellow)
                .Text(MathFigure.G(a, b) + new Vector2(0.12f, 0.12f), "P", MathFigure.Yellow);
            // 기준이 되는 축을 강조
            if (overX) fig.Line(MathFigure.Pink, 0.04f, false, MathFigure.G(-5f, 0f), MathFigure.G(5f, 0f));
            else fig.Line(MathFigure.Pink, 0.04f, false, MathFigure.G(0f, -5f), MathFigure.G(0f, 5f));

            var p = Build($"점 P를 {(overX ? "x축" : "y축")}(분홍 선)에 대하여 접으면 어디로 갈까요?", correct,
                new[] { Pt(a, -b), Pt(-a, b), Pt(-a, -b), Pt(b, a) },
                overX ? "x축 대칭: y의 부호만 바뀌어요 (x, y) → (x, -y)" : "y축 대칭: x의 부호만 바뀌어요 (x, y) → (-x, y)");
            p.Figure = fig;
            return p;
        }

        static MathProblem AxisDistance()
        {
            int x1 = Random.Range(-4, 1), y = NonZero(-3, 3), len = Random.Range(2, 6);
            int x2 = x1 + len;
            var fig = MathFigure.Axes()
                .Line(MathFigure.Cyan, 0.035f, false, MathFigure.G(x1, y), MathFigure.G(x2, y))
                .Dot(MathFigure.G(x1, y), MathFigure.Yellow).Dot(MathFigure.G(x2, y), MathFigure.Yellow)
                .Text(MathFigure.G(x1, y) + new Vector2(-0.1f, 0.15f), "A", MathFigure.Yellow)
                .Text(MathFigure.G(x2, y) + new Vector2(0.1f, 0.15f), "B", MathFigure.Yellow);
            var p = Build($"A{Pt(x1, y)}와 B{Pt(x2, y)} 사이의 거리는?", len.ToString(),
                new[] { (len + 1).ToString(), (len - 1).ToString(), (x1 + x2).ToString(), (len * 2).ToString() },
                $"y가 같으니 x만 비교: {x2} - ({x1}) = {len}");
            p.Figure = fig;
            return p;
        }

        // ------------------------------------------------------------------ 2단계: 그래프

        static (int a, int b) EasyLine()
        {
            int[] slopes = { -2, -1, 1, 2 };
            return (slopes[Random.Range(0, slopes.Length)], Random.Range(-3, 4));
        }

        static MathProblem ReadIntercept()
        {
            var (a, b) = EasyLine();
            var fig = MathFigure.Axes().Graph(x => a * x + b, MathFigure.Yellow).Dot(MathFigure.G(0, b), MathFigure.Pink);
            var p = Build("이 직선이 y축과 만나는 점의 y좌표(y절편)는?", b.ToString(),
                new[] { (-b).ToString(), (b + 1).ToString(), (b - 1).ToString(), a.ToString(), (b + 2).ToString() },
                $"그래프가 y축을 지나는 곳(분홍 점)의 높이 = {b}");
            p.Figure = fig;
            return p;
        }

        static MathProblem ReadSlope()
        {
            var (a, b) = EasyLine();
            var fig = MathFigure.Axes().Graph(x => a * x + b, MathFigure.Yellow)
                .Line(MathFigure.Pink, 0.03f, false, MathFigure.G(0, b), MathFigure.G(1, b), MathFigure.G(1, a + b));
            var p = Build("이 직선의 기울기는? (오른쪽으로 1칸 갈 때 위로 몇 칸?)", a.ToString(),
                new[] { (-a).ToString(), (a + 1).ToString(), b.ToString(), (a * 2).ToString(), "0" },
                $"오른쪽으로 1칸 → {(a > 0 ? "위로" : "아래로")} {Math.Abs(a)}칸 (분홍 계단): 기울기 = {a}");
            p.Figure = fig;
            return p;
        }

        static MathProblem WhichLine()
        {
            var (a, b) = EasyLine();
            var fig = MathFigure.Axes().Graph(x => a * x + b, MathFigure.Yellow);
            var p = Build("노란 직선의 식은?", Line(a, b),
                new[] { Line(-a, b), Line(a, -b == b ? b + 1 : -b), Line(a, b + 2), Line(-a, -b) },
                $"y절편 {b}, 기울기 {a} → {Line(a, b)}");
            p.Figure = fig;
            return p;
        }

        static MathProblem ReadVertex()
        {
            int px = NonZero(-3, 3), q = Random.Range(-3, 2);
            var fig = MathFigure.Axes().Graph(x => (x - px) * (x - px) + q, MathFigure.Yellow).Dot(MathFigure.G(px, q), MathFigure.Pink);
            var p = Build("포물선의 가장 낮은 점(꼭짓점, 분홍 점)의 좌표는?", Pt(px, q),
                new[] { Pt(-px, q), Pt(px, -q == q ? q + 1 : -q), Pt(q, px), Pt(px, q + 1) },
                $"그래프에서 가장 아래 점: x = {px}, y = {q}");
            p.Figure = fig;
            return p;
        }
    }

    // 관문 칠판에 그릴 그림. 좌표는 [-1, 1] 정사각형 안의 값.
    public class MathFigure
    {
        public static readonly Color Chalk = new Color(0.95f, 0.95f, 0.9f);
        public static readonly Color Yellow = new Color(1f, 0.88f, 0.35f);
        public static readonly Color Pink = new Color(1f, 0.55f, 0.72f);
        public static readonly Color Cyan = new Color(0.5f, 0.9f, 1f);
        public static readonly Color Faint = new Color(0.36f, 0.5f, 0.44f);

        const float k_Range = 5.6f; // 좌표평면 그림: -5 ~ 5 칸이 들어가도록

        public readonly List<(Vector2[] Points, Color Color, float Width, bool Loop)> Lines = new List<(Vector2[], Color, float, bool)>();
        public readonly List<(Vector2 Pos, string Text, Color Color)> Labels = new List<(Vector2, string, Color)>();
        public readonly List<(Vector2 Pos, Color Color)> Dots = new List<(Vector2, Color)>();

        public MathFigure Line(Color color, float width, bool loop, params Vector2[] points)
        {
            Lines.Add((points, color, width, loop));
            return this;
        }

        public MathFigure Text(Vector2 pos, string text, Color color)
        {
            Labels.Add((pos, text, color));
            return this;
        }

        public MathFigure Dot(Vector2 pos, Color color)
        {
            Dots.Add((pos, color));
            return this;
        }

        public static Vector2 G(float x, float y) => new Vector2(x, y) / k_Range;

        // 모눈 + x축 + y축 + 눈금 숫자
        public static MathFigure Axes()
        {
            var fig = new MathFigure();
            for (int i = -5; i <= 5; i++)
            {
                if (i == 0) continue;
                fig.Line(Faint, 0.01f, false, G(i, -5f), G(i, 5f));
                fig.Line(Faint, 0.01f, false, G(-5f, i), G(5f, i));
            }

            fig.Line(Chalk, 0.025f, false, G(-5.3f, 0f), G(5.3f, 0f));
            fig.Line(Chalk, 0.025f, false, G(0f, -5.3f), G(0f, 5.3f));
            fig.Text(G(5.3f, -0.6f), "x", Chalk).Text(G(0.6f, 5.3f), "y", Chalk).Text(G(-0.5f, -0.5f), "O", Chalk);
            foreach (int v in new[] { -4, -2, 2, 4 })
            {
                fig.Text(G(v, -0.6f), v.ToString(), Faint);
                fig.Text(G(-0.6f, v), v.ToString(), Faint);
            }

            return fig;
        }

        // y = f(x) 그래프 (-5 ≤ x ≤ 5, 칠판 밖으로 나가는 부분은 자른다)
        public MathFigure Graph(Func<float, float> f, Color color)
        {
            var segment = new List<Vector2>();
            for (float x = -5f; x <= 5.001f; x += 0.1f)
            {
                float y = f(x);
                if (Mathf.Abs(y) <= 5.2f)
                {
                    segment.Add(G(x, y));
                }
                else if (segment.Count > 0)
                {
                    if (segment.Count > 1) Line(color, 0.035f, false, segment.ToArray());
                    segment.Clear();
                }
            }

            if (segment.Count > 1) Line(color, 0.035f, false, segment.ToArray());
            return this;
        }

        public static Vector2[] RegularPolygon(int n, float radius, float startDeg)
        {
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = (startDeg + 360f * i / n) * Mathf.Deg2Rad;
                pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }

            return pts;
        }

        public static Vector2[] Circle(Vector2 center, float radius, int segments = 48)
        {
            var pts = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments;
                pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }

            return pts;
        }
    }
}
