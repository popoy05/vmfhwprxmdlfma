using TMPro;
using UnityEngine;

namespace Unity.FPS.MathMode
{
    // 함수 그래프 모양의 레이저 벽. 세로 평면 위에 y = f(x)를 그리고, 닿으면 피해를 입는다.
    // 그래프가 낮은 곳은 점프로 넘고, 높은 곳은 아래로 지나가야 한다.
    public class GraphLaser : MonoBehaviour
    {
        public enum FunctionType { Sine, Parabola, AbsoluteValue }

        const int k_Samples = 90;
        const float k_HitRadius = 0.35f;

        public FunctionType Type { get; private set; }
        public Vector3 LeftEnd => m_Origin - m_Right * m_HalfWidth;
        public Vector3 RightEnd => m_Origin + m_Right * m_HalfWidth;

        MathModeManager m_Manager;
        Vector3 m_Origin;
        Vector3 m_Right;
        float m_HalfWidth;
        LineRenderer m_Line;
        TextMeshPro m_Label;
        readonly Vector3[] m_Points = new Vector3[k_Samples];

        // 함수 계수
        float m_A, m_B, m_C;

        public void Init(MathModeManager manager, Vector3 origin, Vector3 right, float halfWidth, FunctionType type)
        {
            m_Manager = manager;
            m_Origin = origin;
            m_Right = right.normalized;
            m_HalfWidth = halfWidth;
            Type = type;
            transform.position = origin;

            switch (type)
            {
                case FunctionType.Sine:
                    m_A = Random.Range(2, 4) * 0.5f;       // 진폭 1.0 ~ 1.5
                    m_B = Random.Range(2, 5) * 0.25f;      // 각진동수 0.5 ~ 1.0
                    m_C = m_A + 0.3f;                      // 최저점이 바닥 위 0.3m
                    break;
                case FunctionType.Parabola:
                    m_C = 3.2f;                            // 꼭짓점 높이
                    m_A = Mathf.Round((m_C - 0.4f) / (halfWidth * halfWidth) * 100f) / 100f;
                    m_A = Mathf.Max(m_A, 0.05f);
                    break;
                case FunctionType.AbsoluteValue:
                    m_A = Mathf.Round(Mathf.Clamp(3f / halfWidth, 0.3f, 1.2f) * 10f) / 10f;
                    m_B = halfWidth * 0.5f;                // 꼭짓점이 좌우로 움직이는 폭
                    m_C = 0.3f;
                    break;
            }

            m_Line = manager.CreateLine(transform, new Color(1f, 0.25f, 0.2f), 0.09f);
            m_Line.positionCount = k_Samples;

            // x축(바닥)과 y축을 얇은 흰 선으로 그려 그래프처럼 보이게 한다
            var xAxis = manager.CreateLine(transform, new Color(1f, 1f, 1f, 0.6f), 0.03f);
            xAxis.SetPositions(new[] { LeftEnd + Vector3.up * 0.02f, RightEnd + Vector3.up * 0.02f });
            var yAxis = manager.CreateLine(transform, new Color(1f, 1f, 1f, 0.6f), 0.03f);
            yAxis.SetPositions(new[] { origin, origin + Vector3.up * 4.2f });

            var labelRoot = new GameObject("Label").transform;
            labelRoot.SetParent(transform, false);
            labelRoot.position = origin + Vector3.up * 4.8f;
            m_Label = manager.CreateText(labelRoot, Vector3.zero, new Vector2(7f, 0.9f), "", new Color(1f, 0.55f, 0.45f), 3f);
            manager.CreateText(labelRoot, new Vector3(0f, -0.55f, 0f), new Vector2(7f, 0.35f),
                "함수 그래프 레이저  |  낮은 곳은 점프, 높은 곳은 통과", new Color(1f, 1f, 1f, 0.7f), 1.5f);
        }

        float Evaluate(float x, float t)
        {
            switch (Type)
            {
                case FunctionType.Sine:
                    return m_A * Mathf.Sin(m_B * x - t) + m_C;
                case FunctionType.Parabola:
                    // 꼭짓점 높이가 천천히 오르내린다
                    return -m_A * x * x + m_C + 0.6f * Mathf.Sin(t * 0.8f);
                default:
                    // 꼭짓점이 좌우로 움직인다
                    return m_A * Mathf.Abs(x - m_B * Mathf.Sin(t * 0.6f)) + m_C;
            }
        }

        string Equation(float t)
        {
            switch (Type)
            {
                case FunctionType.Sine:
                    return $"y = {m_A:0.#} sin({m_B:0.##}x - t) + {m_C:0.#}";
                case FunctionType.Parabola:
                    float k = m_C + 0.6f * Mathf.Sin(t * 0.8f);
                    return $"y = -{m_A:0.##}x² + {k:0.0}";
                default:
                    float p = m_B * Mathf.Sin(t * 0.6f);
                    string inner = p >= 0 ? $"x - {p:0.0}" : $"x + {-p:0.0}";
                    return $"y = {m_A:0.#}|{inner}| + {m_C:0.#}";
            }
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < k_Samples; i++)
            {
                float x = Mathf.Lerp(-m_HalfWidth, m_HalfWidth, i / (float)(k_Samples - 1));
                float y = Mathf.Clamp(Evaluate(x, t), 0.05f, 6f);
                m_Points[i] = m_Origin + m_Right * x + Vector3.up * y;
            }

            m_Line.SetPositions(m_Points);
            m_Label.text = $"{Equation(t)}   <size=60%>(t = {t % 100f:0.0})</size>";

            // 라벨은 항상 플레이어 쪽을 향한다
            Transform player = m_Manager.Player;
            if (!player) return;
            Vector3 look = m_Label.transform.position - player.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                m_Label.transform.parent.rotation = Quaternion.LookRotation(look);

            DamagePlayerIfTouching(player);
        }

        void DamagePlayerIfTouching(Transform player)
        {
            if (!m_Manager.PlayerHealth) return;

            // 플레이어 몸통의 세 지점과 그래프 곡선 사이의 최단 거리를 검사
            for (int h = 0; h < 3; h++)
            {
                Vector3 body = player.position + Vector3.up * (0.4f + h * 0.6f);
                for (int i = 0; i < k_Samples - 1; i++)
                {
                    if (DistanceToSegment(body, m_Points[i], m_Points[i + 1]) < k_HitRadius)
                    {
                        m_Manager.PlayerHealth.TakeDamage(m_Manager.LaserDamagePerSecond * Time.deltaTime, gameObject);
                        return;
                    }
                }
            }
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
