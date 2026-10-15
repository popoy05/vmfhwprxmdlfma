using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.MathMode
{
    // "수학 관문 N곳 돌파" 목표. 기존 목표 HUD와 승리 조건에 그대로 연동된다.
    public class MathStationObjective : Objective
    {
        int m_Total;
        int m_Solved;

        public void Setup(int total)
        {
            m_Total = total;
            Title = $"수학 관문 {total}곳 돌파";
            Description = $"좌표평면 지도[M]에서 노란 관문을 찾아 정답 표적을 쏘세요  0 / {total}";
        }

        public void OnStationSolved()
        {
            if (IsCompleted) return;
            m_Solved++;
            string counter = $"{m_Solved} / {m_Total}";
            if (m_Solved >= m_Total)
                CompleteObjective(string.Empty, counter, "목표 완료 : " + Title);
            else
                UpdateObjective(string.Empty, counter, $"남은 관문 {m_Total - m_Solved}곳");
        }
    }

    // "좌표 탐색" 목표. 단서(수식)가 가리키는 좌표평면 위의 점을 직접 찾아가야 한다.
    public class CoordinateHuntObjective : Objective
    {
        float k_HintDelay = 60f;            // 이후 좌표 숫자 공개 (난이도에 따라 바뀜)
        float k_MarkerDelay = 120f;         // 이후 지도에 위치 표시
        const float k_ArriveRadius = 3f;

        public string CurrentClue { get; private set; } = "";
        public Vector2Int CurrentTarget { get; private set; }
        public Vector3 TargetWorld => m_TargetWorld;
        public bool ShowHint => m_Easy || Time.time - m_TargetStartTime > k_HintDelay;
        public bool ShowMarker => m_Easy || Time.time - m_TargetStartTime > k_MarkerDelay;
        public bool HasTarget => m_Beacon;
        bool m_Easy;
        public float HintIn => Mathf.Max(0f, k_HintDelay - (Time.time - m_TargetStartTime));
        // +1: 가까워지는 중, -1: 멀어지는 중, 0: 그대로
        public int Trend { get; private set; }
        public int Found => m_Found;
        public int Total => m_Total;

        float m_LastDistance = -1f;
        float m_NextTrendTime;

        MathModeManager m_Manager;
        List<Vector3> m_Occupied;
        int m_Total;
        int m_Found;
        Vector3 m_TargetWorld;
        GameObject m_Beacon;
        float m_TargetStartTime;

        public void Setup(MathModeManager manager, int total, List<Vector3> occupied)
        {
            m_Manager = manager;
            m_Occupied = occupied;
            m_Easy = manager.Preset.DirectHuntClue;
            k_HintDelay = manager.Preset.HintDelay;
            k_MarkerDelay = manager.Preset.MarkerDelay;
            m_Total = total;
            Title = $"좌표 탐색: 신호 지점 {total}곳 찾기";
            Description = "단서를 계산 → 오른쪽 위 내 좌표 P(x, y)가 답과 같아지는 곳으로 이동";
        }

        protected override void Start()
        {
            base.Start();
            NextTarget();
        }

        void NextTarget()
        {
            if (m_Beacon) Destroy(m_Beacon);

            if (!m_Manager.World.TryFindGridSpot(m_Manager.Player.position, m_Occupied, 20f, out m_TargetWorld,
                    out Vector2Int grid))
            {
                // 격자점을 찾지 못하면 목표를 완료 처리해서 게임이 막히지 않도록 한다
                CompleteObjective(string.Empty, $"{m_Found} / {m_Total}", "좌표 탐색 종료");
                CurrentClue = "";
                return;
            }

            m_Occupied.Add(m_TargetWorld);
            CurrentTarget = grid;
            CurrentClue = m_Easy
                ? $"좌표 ({grid.x}, {grid.y}) 지점으로 가기"
                : MakeClue(grid, m_Found);
            m_TargetStartTime = Time.time;
            m_LastDistance = -1f;
            Trend = 0;

            // 신호기: 가까이 가야만 보이는 빛기둥
            m_Beacon = new GameObject("CoordinateBeacon");
            m_Beacon.transform.position = m_TargetWorld;
            m_Manager.CreateBlock(m_Beacon.transform, PrimitiveType.Cylinder, new Vector3(0f, 6f, 0f),
                new Vector3(0.35f, 6f, 0.35f), new Color(0.3f, 1f, 1f));
            m_Manager.CreateBlock(m_Beacon.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f),
                new Vector3(4f, 0.02f, 4f), new Color(0.1f, 0.6f, 0.7f));
            m_Beacon.SetActive(false);

            UpdateObjective(CurrentClue, $"{m_Found} / {m_Total}", $"새 좌표 단서: {CurrentClue}");
        }

        void Update()
        {
            if (IsCompleted || !m_Beacon || !m_Manager.Player) return;

            Vector3 d = m_Manager.Player.position - m_TargetWorld;
            float horizontal = new Vector2(d.x, d.z).magnitude;
            m_Beacon.SetActive(m_Easy || (horizontal < m_Manager.UnitSize * 4f && Mathf.Abs(d.y) < 6f));

            // 1초마다 거리 변화로 가까워짐/멀어짐 판단 (온도계 힌트)
            if (Time.time >= m_NextTrendTime)
            {
                m_NextTrendTime = Time.time + 1f;
                if (m_LastDistance >= 0f)
                    Trend = horizontal < m_LastDistance - 0.5f ? 1 : horizontal > m_LastDistance + 0.5f ? -1 : 0;
                m_LastDistance = horizontal;
            }

            if (horizontal < k_ArriveRadius && Mathf.Abs(d.y) < 4f)
            {
                m_Found++;
                m_Manager.OnBeaconFound();
                MathModeManager.ShowMessage($"신호 발견! 좌표 ({CurrentTarget.x}, {CurrentTarget.y})");
                if (m_Manager.RewardPrefab && !m_Manager.Preset.NoHeal)
                    Instantiate(m_Manager.RewardPrefab, m_TargetWorld + Vector3.up, Quaternion.identity);

                if (m_Found >= m_Total)
                {
                    Destroy(m_Beacon);
                    CurrentClue = "";
                    CompleteObjective(string.Empty, $"{m_Found} / {m_Total}", "목표 완료 : " + Title);
                }
                else
                {
                    NextTarget();
                }
            }
        }

        // 목표 좌표 (x, y)를 여러 가지 수학 표현으로 숨긴다. 첫 단서는 쉬운 편으로 낸다.
        static string MakeClue(Vector2Int t, int index)
        {
            int type = index == 0 ? Random.Range(0, 2) : Random.Range(1, 6);
            int x = t.x, y = t.y;
            switch (type)
            {
                case 0:
                {
                    int m = NonZero(-4, 4), n = NonZero(-4, 4);
                    return $"점 ({x - m}, {y - n})을 x축 방향으로 {m}, y축 방향으로 {n}만큼 평행이동한 점";
                }
                case 1:
                {
                    int dx = NonZero(-5, 5), dy = NonZero(-5, 5);
                    return $"A({x - dx}, {y - dy})와 B({x + dx}, {y + dy})의 중점";
                }
                case 2:
                {
                    int a1 = NonZero(-3, 3), a2;
                    do { a2 = NonZero(-3, 3); } while (a2 == a1);
                    return $"두 직선 {MathProblemGenerator.Line(a1, y - a1 * x)}, {MathProblemGenerator.Line(a2, y - a2 * x)}의 교점";
                }
                case 3:
                {
                    string[] axes = { "x축", "y축", "원점" };
                    int k = Random.Range(0, 3);
                    int sx = k == 0 ? x : -x, sy = k == 1 ? y : -y;
                    return $"점 ({sx}, {sy})을 {axes[k]}에 대하여 대칭이동한 점";
                }
                case 4:
                {
                    string inner = x > 0 ? $"x - {x}" : $"x + {-x}";
                    string tail = y == 0 ? "" : y > 0 ? $" + {y}" : $" - {-y}";
                    return $"포물선 y = ({inner})²{tail} 의 꼭짓점";
                }
                default:
                {
                    // 무게중심: 세 점의 좌표 평균
                    int ax = NonZero(-3, 3), ay = NonZero(-3, 3), bx = NonZero(-3, 3), by = NonZero(-3, 3);
                    return $"세 점 ({x + ax}, {y + ay}), ({x + bx}, {y + by}), ({x - ax - bx}, {y - ay - by})로 만든 삼각형의 무게중심";
                }
            }
        }

        static int NonZero(int min, int max) { int v; do { v = Random.Range(min, max + 1); } while (v == 0); return v; }
    }
}
