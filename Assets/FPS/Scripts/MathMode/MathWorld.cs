using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.MathMode
{
    // 월드 좌표 ↔ 좌표평면 변환과 NavMesh 위 위치 선정을 담당한다.
    // 좌표평면의 x축 = 월드 X, y축 = 월드 Z. 1칸 = UnitSize 미터.
    public class MathWorld
    {
        public readonly float UnitSize;
        public readonly Rect Bounds;           // 좌표평면 단위의 걸을 수 있는 영역 범위
        public readonly Vector3[] Vertices;    // NavMesh 삼각형 (미니맵 렌더링용)
        public readonly int[] Indices;

        readonly float[] m_CumulativeArea;
        readonly NavMeshPath m_Path = new NavMeshPath();

        public MathWorld(float unitSize)
        {
            UnitSize = unitSize;
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            Vertices = tri.vertices;
            Indices = tri.indices;

            int count = Indices.Length / 3;
            m_CumulativeArea = new float[count];
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = Vertices[Indices[i * 3]], b = Vertices[Indices[i * 3 + 1]], c = Vertices[Indices[i * 3 + 2]];
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                m_CumulativeArea[i] = total;
            }

            if (Vertices.Length == 0)
            {
                Bounds = new Rect(-10, -10, 20, 20);
                return;
            }

            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (Vector3 v in Vertices)
            {
                Vector2 p = ToPlane(v);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public bool HasNavMesh => m_CumulativeArea.Length > 0;

        // 도달 가능 여부를 판단하는 기준점 (시작 지점). 없으면 호출자가 준 위치를 쓴다.
        public Vector3? Hub { get; set; }

        // 쉬운 모드: 목표·적을 플레이어 근처(이 거리 안)에만 배치
        public float MaxFromPlayer { get; set; } = float.MaxValue;

        public Vector2 ToPlane(Vector3 world) => new Vector2(world.x / UnitSize, world.z / UnitSize);

        public static string Quadrant(Vector2 p)
        {
            if (Mathf.Abs(p.x) < 0.05f || Mathf.Abs(p.y) < 0.05f) return "축 위";
            return p.x > 0 ? (p.y > 0 ? "제1사분면" : "제4사분면") : (p.y > 0 ? "제2사분면" : "제3사분면");
        }

        public Vector3 RandomPoint()
        {
            float r = Random.value * m_CumulativeArea[m_CumulativeArea.Length - 1];
            int i = System.Array.BinarySearch(m_CumulativeArea, r);
            if (i < 0) i = ~i;
            i = Mathf.Min(i, m_CumulativeArea.Length - 1);

            Vector3 a = Vertices[Indices[i * 3]], b = Vertices[Indices[i * 3 + 1]], c = Vertices[Indices[i * 3 + 2]];
            float u = Random.value, v = Random.value;
            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
            return a + (b - a) * u + (c - a) * v;
        }

        public bool IsReachable(Vector3 from, Vector3 to)
        {
            if (Hub.HasValue) from = Hub.Value;
            if (!NavMesh.SamplePosition(from, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                return true; // 기준점이 NavMesh 밖이면 검사를 생략한다
            return NavMesh.CalculatePath(hit.position, to, NavMesh.AllAreas, m_Path) &&
                   m_Path.status == NavMeshPathStatus.PathComplete;
        }

        // 수평 8방향으로 radius 만큼 벽이 없는지 검사
        public static bool HasClearance(Vector3 point, float radius, float height = 1.5f)
        {
            Vector3 origin = point + Vector3.up * height;
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                if (Physics.Raycast(origin, dir, radius, ~0, QueryTriggerInteraction.Ignore))
                    return false;
            }

            return true;
        }

        // 조건을 만족하는 위치를 찾는다. 실패하면 조건을 조금씩 완화한다.
        public bool TryFindSpot(Vector3 playerPos, List<Vector3> occupied, float minFromPlayer, float minSpacing,
            float clearance, out Vector3 spot)
        {
            for (int relax = 0; relax < 3; relax++)
            {
                float scale = 1f - relax * 0.3f;
                for (int attempt = 0; attempt < 300; attempt++)
                {
                    Vector3 p = RandomPoint();
                    float distance = Vector3.Distance(p, playerPos);
                    if (distance < minFromPlayer * scale || distance > MaxFromPlayer / scale) continue;
                    if (occupied.Exists(o => Vector3.Distance(o, p) < minSpacing * scale)) continue;
                    if (clearance > 0f && !HasClearance(p, clearance * scale)) continue;
                    if (!IsReachable(playerPos, p)) continue;
                    spot = p;
                    return true;
                }
            }

            spot = default;
            return false;
        }

        // 좌표평면의 정수 격자점 위에 있는 위치를 찾는다 (좌표 탐색 미션용)
        public bool TryFindGridSpot(Vector3 playerPos, List<Vector3> occupied, float minFromPlayer, out Vector3 spot,
            out Vector2Int grid)
        {
            for (int attempt = 0; attempt < 600; attempt++)
            {
                Vector3 p = RandomPoint();
                grid = new Vector2Int(Mathf.RoundToInt(p.x / UnitSize), Mathf.RoundToInt(p.z / UnitSize));
                if (grid.x == 0 || grid.y == 0) continue;
                Vector3 snapped = new Vector3(grid.x * UnitSize, p.y, grid.y * UnitSize);
                if (!NavMesh.SamplePosition(snapped, out NavMeshHit hit, 1.0f, NavMesh.AllAreas)) continue;
                if (new Vector2(hit.position.x - snapped.x, hit.position.z - snapped.z).magnitude > 0.3f) continue;
                float d = Vector3.Distance(hit.position, playerPos);
                if (d < minFromPlayer || d > MaxFromPlayer) continue;
                if (occupied.Exists(o => Vector3.Distance(o, hit.position) < 6f)) continue;
                if (!HasClearance(hit.position, 1.5f)) continue; // 구조물 속이나 벽에 붙은 곳 제외
                if (!IsReachable(playerPos, hit.position)) continue;
                spot = hit.position;
                return true;
            }

            spot = default;
            grid = default;
            return false;
        }
    }
}
