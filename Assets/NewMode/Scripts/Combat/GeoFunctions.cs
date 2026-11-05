using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 새 모드의 모든 공격은 아래 "함수"로 모양이 정해진다.
    // 좌표계(스킬을 쓴 순간의 캐릭터 기준, 땅 위 평면):
    //   x = 앞쪽 거리, y = 오른쪽 거리   (θ는 앞쪽에서 오른쪽으로 잰 각도)
    // 높이가 필요한 함수(포물선, 세로 베기)는 y를 높이로 쓴다.
    public static class GeoFunctions
    {
        // ------------------------------------------------------------ 평타: 부채꼴 (원의 일부)
        // 칼끝의 경로:  P(θ) = (R·cosθ, R·sinθ)
        // 맞는 범위:    r ≤ R  그리고  |θ| ≤ α   (부채꼴)
        public static Vector2 Arc(float radius, float thetaDeg)
        {
            float t = thetaDeg * Mathf.Deg2Rad;
            return new Vector2(radius * Mathf.Cos(t), radius * Mathf.Sin(t));
        }

        public static bool InSector(Vector2 p, float radius, float halfAngleDeg)
        {
            float r = p.magnitude;
            if (r > radius) return false;
            float theta = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            return Mathf.Abs(theta) <= halfAngleDeg;
        }

        // 세로 베기(평타 3타):  높이-앞 평면의 원  x² + y² = R²   →  P(φ) = (R·sinφ, R·cosφ)
        public static Vector2 VerticalArc(float radius, float phiDeg)
        {
            float t = phiDeg * Mathf.Deg2Rad;
            return new Vector2(radius * Mathf.Sin(t), radius * Mathf.Cos(t));
        }

        // ------------------------------------------------------------ Q: 사인 곡선
        //   y = A·sin(2πx / λ)      (A: 진폭, λ: 파장)
        public static float Sine(float x, float amplitude, float wavelength)
        {
            return amplitude * Mathf.Sin(2f * Mathf.PI * x / wavelength);
        }

        // ------------------------------------------------------------ E: 원
        //   x² + y² = r²,   r(t) = r₀ + v·t   (시간에 따라 커지는 원)
        public static float CircleRadius(float t, float r0, float speed)
        {
            return r0 + speed * t;
        }

        public static Vector2 Circle(float radius, float thetaRad)
        {
            return new Vector2(radius * Mathf.Cos(thetaRad), radius * Mathf.Sin(thetaRad));
        }

        // ------------------------------------------------------------ X: 포물선
        //   y = −(4H / D²)·(x − D/2)² + H      (x=0에서 출발, x=D에 착지, 꼭짓점 (D/2, H))
        public static float Parabola(float x, float distance, float height)
        {
            float a = 4f * height / (distance * distance);
            float h = distance * 0.5f;
            return -a * (x - h) * (x - h) + height;
        }

        // ------------------------------------------------------------ C: 장미 곡선 (극좌표)
        //   r = a·cos(kθ)      (k가 짝수면 꽃잎 2k장)
        public static float Rose(float thetaRad, float a, int k)
        {
            return a * Mathf.Cos(k * thetaRad);
        }

        public static Vector2 RosePoint(float thetaRad, float a, int k)
        {
            float r = Rose(thetaRad, a, k);
            return new Vector2(r * Mathf.Cos(thetaRad), r * Mathf.Sin(thetaRad));
        }

        // 점 p가 장미 곡선의 꽃잎 안에 있는가:  r ≤ a·|cos(kθ)|
        public static bool InRose(Vector2 p, float a, int k)
        {
            float r = p.magnitude;
            float theta = Mathf.Atan2(p.y, p.x);
            return r <= Mathf.Abs(Rose(theta, a, k)) + 0.35f; // 꽃잎 가장자리에 살짝 여유
        }

        // ------------------------------------------------------------ 직선
        //   y = (tan θ)·x   (원점을 지나고 앞쪽과 θ만큼 기운 직선) → 단위 방향 (cosθ, sinθ)
        public static Vector2 LineDir(float thetaDeg) => Arc(1f, thetaDeg);

        // 점 p와 선분 (a→b) 사이 거리
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------ 심장형 (카디오이드)   r = a(1 + cosθ)
        public static Vector2 Cardioid(float thetaRad, float a)
        {
            float r = a * (1f + Mathf.Cos(thetaRad));
            return new Vector2(r * Mathf.Cos(thetaRad), r * Mathf.Sin(thetaRad));
        }

        public static bool InCardioid(Vector2 p, float a)
        {
            float theta = Mathf.Atan2(p.y, p.x);
            return p.magnitude <= a * (1f + Mathf.Cos(theta));
        }

        // ------------------------------------------------------------ 아르키메데스 나선   r = a·θ
        public static Vector2 Spiral(float thetaRad, float a) => Circle(a * thetaRad, thetaRad);

        // ------------------------------------------------------------ 로그(등각) 나선   r = a·e^(bθ)
        public static Vector2 LogSpiral(float thetaRad, float a, float b) => Circle(a * Mathf.Exp(b * thetaRad), thetaRad);

        // ------------------------------------------------------------ 리사주 곡선   x = A·sin(p·t),  y = B·sin(q·t)
        public static Vector2 Lissajous(float t, float A, float B, int p, int q) => new Vector2(A * Mathf.Sin(p * t), B * Mathf.Sin(q * t));

        // ------------------------------------------------------------ 별 다각형 {n/k}: k번째 꼭짓점마다 잇는다   P_i = R(cos(2πki/n), sin(2πki/n))
        public static Vector2 StarVertex(int i, int n, int k, float radius) => Circle(radius, 2f * Mathf.PI * k * i / n);

        // 정n각형 꼭짓점   P_k = C + R(cos(2πk/n), sin(2πk/n))
        public static Vector2 PolygonVertex(int k, int n, float radius) => Circle(radius, 2f * Mathf.PI * k / n);

        // ------------------------------------------------------------ 수열
        //   등차수열 a_n = a₁ + (n − 1)d,   등비수열 a_n = a₁·r^(n−1)
        public static float Arithmetic(int n, float a1, float d) => a1 + (n - 1) * d;
        public static float Geometric(int n, float a1, float r) => a1 * Mathf.Pow(r, n - 1);

        // 해바라기 배열 (황금각 137.5°):  r_n = c·√n,  θ_n = n·137.5°
        public const float GoldenAngle = 137.50776f;
        public static Vector2 Sunflower(int n, float c) => Circle(c * Mathf.Sqrt(n), n * GoldenAngle * Mathf.Deg2Rad);

        // ------------------------------------------------------------ 삼각파 (반사)   y = (2A/π)·arcsin(sin(2πx/P))
        public static float TriangleWave(float x, float amplitude, float period)
        {
            return 2f * amplitude / Mathf.PI * Mathf.Asin(Mathf.Sin(2f * Mathf.PI * x / period));
        }

        // ------------------------------------------------------------ 타원   x²/a² + y²/b² = 1  →  (a·cos t, b·sin t)
        public static Vector2 Ellipse(float t, float a, float b) => new Vector2(a * Mathf.Cos(t), b * Mathf.Sin(t));

        // ------------------------------------------------------------ 단진동 (진자)   θ(t) = θ₀·cos(ωt)
        public static float Pendulum(float t, float amplitudeDeg, float omega) => amplitudeDeg * Mathf.Cos(omega * t);

        // ------------------------------------------------------------ 포물선 운동 (투사체)
        //   x = v·cosθ·t,   y = v·sinθ·t − ½gt²
        public static Vector2 Projectile(float t, float speed, float angleDeg, float g)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector2(speed * Mathf.Cos(a) * t, speed * Mathf.Sin(a) * t - 0.5f * g * t * t);
        }

        // ------------------------------------------------------------ 보조
        // 0→1 진행도를 빠르게 시작해 부드럽게 멈추도록 (칼을 휘두르는 느낌)
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }
    }
}
