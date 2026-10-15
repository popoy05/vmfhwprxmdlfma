using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.MathMode
{
    // 투사체가 수식을 따라 움직이게 한다.
    // 발사 방향을 x축(진행 거리 s), 오른쪽을 가로 편차, 위쪽을 세로 편차로 보고 f(s)만큼 위치를 비튼다.
    // ProjectileStandard가 이동·충돌 판정을 끝낸 뒤 실행되며, 다음 프레임의 충돌 판정이 비틀린 경로를 그대로 검사한다.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(ProjectileStandard))]
    public class MathTrajectory : MonoBehaviour
    {
        public enum Function
        {
            Straight,       // 직선 (중력이 있으면 포물선)
            Sine,           // y = A sin(ks)
            Zigzag,         // 삼각파
            Helix,          // 나선 (원운동 + 직진)
            Fan,            // 부채꼴: 탄마다 기울기 tanθ 가 다른 직선
            Cone,           // 원뿔: 가운데 1발 + 원 둘레에 나머지 (단면이 원 x² + y² = (s·tanα)²)
            Damped,         // 감쇠 진동 y = A e^(-λs) sin(ks)
            Rose,           // 장미 곡선 r = A cos(nθ)
            Exponential,    // 지수 가속 v = v0 e^(kt)
        }

        public Function Type = Function.Sine;
        [Tooltip("진폭 A (m) / 부채꼴이면 중심각 (도)")]
        public float Amplitude = 0.6f;
        [Tooltip("각진동수 k (1/m) / 지수 가속이면 k (1/s)")]
        public float Frequency = 0.6f;
        [Tooltip("감쇠 계수 λ / 장미 곡선의 n")]
        public float Extra = 0.15f;
        [Tooltip("한 번에 나가는 탄 수 (나선의 위상차, 부채꼴 분할에 사용)")]
        public int PelletsPerShot = 1;

        static int s_LastShotFrame = -1;
        static int s_IndexInFrame;

        ProjectileBase m_Base;
        ProjectileStandard m_Standard;
        Vector3 m_Right, m_Up, m_Forward;
        Vector3 m_PrevOffset;
        float m_ShootTime;
        int m_Index;
        bool m_Shot;

        void OnEnable()
        {
            m_Base = GetComponent<ProjectileBase>();
            m_Standard = GetComponent<ProjectileStandard>();
            m_Base.OnShoot += OnShoot;
        }

        void OnDisable()
        {
            if (m_Base) m_Base.OnShoot -= OnShoot;
        }

        void OnShoot()
        {
            // 같은 프레임에 발사된 탄들에 0, 1, 2 ... 번호를 매긴다
            if (Time.frameCount != s_LastShotFrame)
            {
                s_LastShotFrame = Time.frameCount;
                s_IndexInFrame = 0;
            }

            m_Index = s_IndexInFrame++ % Mathf.Max(1, PelletsPerShot);
            m_Forward = transform.forward;
            m_Right = transform.right;
            m_Up = transform.up;
            m_ShootTime = Time.time;
            m_PrevOffset = Vector3.zero;
            m_Shot = true;
        }

        void Update()
        {
            if (!m_Shot) return;

            float t = Time.time - m_ShootTime;
            float s = m_Standard.Speed * t;
            Vector3 offset = Offset(s, t);
            transform.position += offset - m_PrevOffset;
            m_PrevOffset = offset;
        }

        Vector3 Offset(float s, float t)
        {
            float phase = 2f * Mathf.PI * m_Index / Mathf.Max(1, PelletsPerShot);
            float ramp = Mathf.Clamp01(s / 2f); // 총구 근처에서 갑자기 튀지 않도록
            switch (Type)
            {
                case Function.Sine:
                    return m_Right * (Amplitude * Mathf.Sin(Frequency * s + phase) - Amplitude * Mathf.Sin(phase));
                case Function.Zigzag:
                    return m_Right * (Amplitude * 2f / Mathf.PI * Mathf.Asin(Mathf.Sin(Frequency * s)));
                case Function.Helix:
                {
                    float a = Frequency * s + phase;
                    return (m_Right * Mathf.Cos(a) + m_Up * Mathf.Sin(a)) * (Amplitude * ramp);
                }
                case Function.Fan:
                {
                    float half = Amplitude * 0.5f;
                    float deg = PelletsPerShot <= 1 ? 0f : Mathf.Lerp(-half, half, m_Index / (float)(PelletsPerShot - 1));
                    return m_Right * (s * Mathf.Tan(deg * Mathf.Deg2Rad));
                }
                case Function.Cone:
                {
                    // 0번 탄은 조준점 정중앙, 나머지는 반각 α 인 원뿔의 둘레를 따라 고르게 퍼진다
                    if (m_Index == 0 || PelletsPerShot <= 1) return Vector3.zero;
                    float phi = 2f * Mathf.PI * (m_Index - 1) / (PelletsPerShot - 1);
                    float radius = s * Mathf.Tan(Amplitude * Mathf.Deg2Rad);
                    return (m_Right * Mathf.Cos(phi) + m_Up * Mathf.Sin(phi)) * radius;
                }
                case Function.Damped:
                    return m_Right * (Amplitude * Mathf.Exp(-Extra * s) * Mathf.Sin(Frequency * s));
                case Function.Rose:
                {
                    float theta = Frequency * s;
                    float r = Amplitude * ramp * Mathf.Cos(Extra * theta);
                    return (m_Right * Mathf.Cos(theta) + m_Up * Mathf.Sin(theta)) * r;
                }
                case Function.Exponential:
                {
                    // s(t) = v0 (e^(kt) - 1) / k 에서 기본 직진 v0·t 를 뺀 만큼 앞으로 더 보낸다 (최대 속도 제한)
                    float k = Mathf.Max(Frequency, 0.01f);
                    float tCap = Mathf.Min(t, Mathf.Log(12f) / k);
                    float v0 = m_Standard.Speed;
                    float extra = v0 * ((Mathf.Exp(k * tCap) - 1f) / k - tCap) + v0 * 11f * (t - tCap);
                    return m_Forward * extra;
                }
                default:
                    return Vector3.zero;
            }
        }

        // HUD에 보여줄 수식
        public string Formula
        {
            get
            {
                switch (Type)
                {
                    case Function.Sine: return $"y = {Amplitude:0.##}·sin({Frequency:0.##}x)";
                    case Function.Zigzag: return $"y = {Amplitude:0.##}·(2/π)·arcsin(sin {Frequency:0.##}x)";
                    case Function.Helix: return $"(y, z) = {Amplitude:0.##}(cos {Frequency:0.##}x, sin {Frequency:0.##}x)  ×{PelletsPerShot}";
                    case Function.Fan: return $"y = x·tanθ,  θ ∈ [-{Amplitude / 2:0}°, {Amplitude / 2:0}°]  ({PelletsPerShot}발)";
                    case Function.Cone: return $"x² + y² = (s·tan{Amplitude:0}°)²  ({PelletsPerShot}발, 조준 원과 동일)";
                    case Function.Damped: return $"y = {Amplitude:0.##}·e^(-{Extra:0.##}x)·sin({Frequency:0.##}x)";
                    case Function.Rose: return $"r = {Amplitude:0.##}·cos({Extra:0}θ),  θ = {Frequency:0.##}x";
                    case Function.Exponential: return $"v = v0·e^({Frequency:0.##}t)";
                    default:
                        return GetComponent<ProjectileStandard>().GravityDownAcceleration > 0f
                            ? "y = x·tanθ - g·x² / (2v²cos²θ)"
                            : "y = 0 (직선)";
                }
            }
        }
    }
}
