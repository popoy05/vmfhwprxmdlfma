using System.Collections.Generic;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.AI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Tools > Map Expander
// 1) 레벨 지오메트리를 수평(XZ)으로 확대하고 플레이어/적/픽업/순찰 지점 위치도 같은 비율로 옮긴다.
// 2) NavMesh를 다시 굽는다.
// 3) 넓어진 NavMesh 위에 HoverBot/Turret을 추가 배치하고 HoverBot에는 순찰 경로를 만들어 준다.
public class MapExpander : EditorWindow
{
    const string k_ScaleRootName = "===== LEVEL (SCALED) =====";
    const string k_ExtraEnemiesRootName = "===== EXTRA ENEMIES =====";
    const string k_HoverBotPath = "Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab";
    const string k_TurretPath = "Assets/FPS/Prefabs/Enemies/Enemy_Turret.prefab";

    float m_ScaleFactor = 1.6f;
    int m_HoverBotCount = 12;
    int m_TurretCount = 4;
    float m_MinDistanceFromPlayer = 15f;
    float m_MinSpacing = 6f;
    int m_Seed = 1234;

    [MenuItem("Tools/Map Expander")]
    static void Open() => GetWindow<MapExpander>("Map Expander");

    void OnGUI()
    {
        EditorGUILayout.LabelField("맵 확장", EditorStyles.boldLabel);
        m_ScaleFactor = EditorGUILayout.Slider("수평 확대 배율", m_ScaleFactor, 1f, 3f);
        if (GUILayout.Button("1) 맵 넓히기 + NavMesh 재생성"))
            ExpandMap();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("몹 추가", EditorStyles.boldLabel);
        m_HoverBotCount = EditorGUILayout.IntSlider("HoverBot 추가 수", m_HoverBotCount, 0, 50);
        m_TurretCount = EditorGUILayout.IntSlider("Turret 추가 수", m_TurretCount, 0, 30);
        m_MinDistanceFromPlayer = EditorGUILayout.FloatField("플레이어와 최소 거리", m_MinDistanceFromPlayer);
        m_MinSpacing = EditorGUILayout.FloatField("적끼리 최소 간격", m_MinSpacing);
        m_Seed = EditorGUILayout.IntField("랜덤 시드", m_Seed);
        if (GUILayout.Button("2) 적 추가 배치"))
            SpawnEnemies();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "순서대로 1 → 2를 누른 뒤 씬을 저장(Ctrl+S)하세요.\n" +
            "배치는 Ctrl+Z로 되돌릴 수 있지만 NavMesh는 다시 구워야 합니다.",
            MessageType.Info);
    }

    // ------------------------------------------------------------------ 맵 확대

    void ExpandMap()
    {
        Scene scene = SceneManager.GetActiveScene();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Expand Map");

        var geometry = new List<Transform>();
        var movers = new List<Transform>();
        Transform scaleRoot = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == k_ScaleRootName)
            {
                scaleRoot = root.transform;
                continue;
            }

            Classify(root.transform, geometry, movers);
        }

        if (scaleRoot == null)
        {
            var go = new GameObject(k_ScaleRootName);
            Undo.RegisterCreatedObjectUndo(go, "Create scale root");
            scaleRoot = go.transform;
        }

        // 이동 대상은 원래 월드 위치를 먼저 기록해 둔다 (지오메트리 재배치 중 부모가 바뀔 수 있으므로)
        var targets = movers
            .Distinct()
            .Select(t => (t, pos: t.position))
            .OrderBy(e => Depth(e.t))
            .ToList();

        foreach (Transform t in geometry)
            Undo.SetTransformParent(t, scaleRoot, "Reparent level geometry");

        Undo.RecordObject(scaleRoot, "Scale level");
        scaleRoot.position = Vector3.zero;
        scaleRoot.rotation = Quaternion.identity;
        scaleRoot.localScale = Vector3.Scale(scaleRoot.localScale, new Vector3(m_ScaleFactor, 1f, m_ScaleFactor));

        foreach (var (t, pos) in targets)
        {
            Undo.RecordObject(t, "Move gameplay object");
            t.position = new Vector3(pos.x * m_ScaleFactor, pos.y, pos.z * m_ScaleFactor);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);

        RebakeNavMesh(scene);
        Debug.Log($"[MapExpander] 지오메트리 {geometry.Count}개를 x{m_ScaleFactor:0.##} 확대, 게임플레이 오브젝트 {targets.Count}개 이동, NavMesh 재생성 완료.");
    }

    static bool IsGameplay(Transform t) =>
        t.GetComponent<EnemyController>() || t.GetComponent<Pickup>() ||
        t.GetComponent<PlayerCharacterController>() || t.GetComponent<Objective>() ||
        t.GetComponent<PatrolPath>() || t.GetComponent<Camera>() || t.GetComponent<Canvas>();

    static bool SubtreeHasGameplay(Transform t) =>
        t.GetComponentsInChildren<Transform>(true).Any(IsGameplay);

    static bool SubtreeHasGeometry(Transform t) =>
        t.GetComponentInChildren<Renderer>(true) || t.GetComponentInChildren<Collider>(true) ||
        t.GetComponentInChildren<Light>(true) || t.GetComponentInChildren<ReflectionProbe>(true);

    static void Classify(Transform t, List<Transform> geometry, List<Transform> movers)
    {
        if (t.GetComponent<NavMeshSurface>())
            return;

        if (IsGameplay(t))
        {
            var path = t.GetComponent<PatrolPath>();
            if (path)
                movers.AddRange(path.PathNodes.Where(n => n));
            else
                movers.Add(t);
            return;
        }

        // 프리팹 인스턴스 내부 자식은 부모를 바꿀 수 없으므로 통째로 지오메트리로 취급한다
        bool canRestructure = !PrefabUtility.IsPartOfPrefabInstance(t.gameObject);
        if (SubtreeHasGameplay(t) && canRestructure)
        {
            foreach (Transform child in t.Cast<Transform>().ToList())
                Classify(child, geometry, movers);
            return;
        }

        if (SubtreeHasGeometry(t))
            geometry.Add(t);
    }

    static int Depth(Transform t)
    {
        int d = 0;
        for (; t.parent; t = t.parent) d++;
        return d;
    }

    static void RebakeNavMesh(Scene scene)
    {
        var surfaces = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<NavMeshSurface>(true))
            .ToArray();

        // 적 배치를 위해 즉시 동기 빌드한 뒤, 에셋 저장까지 되도록 에디터 베이크도 걸어 둔다
        foreach (NavMeshSurface s in surfaces)
            s.BuildNavMesh();
        NavMeshAssetManager.instance.StartBakingSurfaces(surfaces);
    }

    // ------------------------------------------------------------------ 적 추가

    void SpawnEnemies()
    {
        Scene scene = SceneManager.GetActiveScene();
        var hoverBotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_HoverBotPath);
        var turretPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_TurretPath);
        if (!hoverBotPrefab || !turretPrefab)
        {
            Debug.LogError("[MapExpander] 적 프리팹을 찾을 수 없습니다.");
            return;
        }

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.indices.Length == 0)
        {
            Debug.LogError("[MapExpander] NavMesh가 없습니다. 먼저 1) 맵 넓히기를 실행하거나 NavMesh를 구워 주세요.");
            return;
        }

        var player = scene.GetRootGameObjects()
            .Select(r => r.GetComponentInChildren<PlayerCharacterController>(true))
            .FirstOrDefault(p => p);
        Vector3 playerPos = player ? player.transform.position : Vector3.zero;
        NavMesh.SamplePosition(playerPos, out NavMeshHit playerHit, 10f, NavMesh.AllAreas);

        var occupied = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<EnemyController>(true))
            .Select(e => e.transform.position)
            .ToList();

        Random.InitState(m_Seed);
        var sampler = new TriangleSampler(tri);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Spawn Extra Enemies");

        GameObject rootGo = GameObject.Find(k_ExtraEnemiesRootName);
        if (!rootGo)
        {
            rootGo = new GameObject(k_ExtraEnemiesRootName);
            Undo.RegisterCreatedObjectUndo(rootGo, "Create extra enemies root");
        }

        int spawned = 0;
        var path = new NavMeshPath();

        bool TryFindSpot(out Vector3 spot)
        {
            for (int attempt = 0; attempt < 500; attempt++)
            {
                Vector3 candidate = sampler.RandomPoint();
                if ((candidate - playerPos).sqrMagnitude < m_MinDistanceFromPlayer * m_MinDistanceFromPlayer)
                    continue;
                if (occupied.Any(o => (o - candidate).sqrMagnitude < m_MinSpacing * m_MinSpacing))
                    continue;
                // 몸통 높이(0.3m~2.3m)가 벽 등과 겹치면 제외 (벽 끼임 방지)
                if (Physics.CheckCapsule(candidate + Vector3.up * 0.9f, candidate + Vector3.up * 1.7f, 0.6f, ~0,
                        QueryTriggerInteraction.Ignore))
                    continue;
                // 플레이어가 걸어서 갈 수 있는 영역만 (고립된 지붕 위 등 제외)
                if (playerHit.hit && (!NavMesh.CalculatePath(playerHit.position, candidate, NavMesh.AllAreas, path) ||
                                      path.status != NavMeshPathStatus.PathComplete))
                    continue;
                spot = candidate;
                return true;
            }

            spot = default;
            return false;
        }

        for (int i = 0; i < m_HoverBotCount + m_TurretCount; i++)
        {
            bool isHoverBot = i < m_HoverBotCount;
            if (!TryFindSpot(out Vector3 spot))
                break;

            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(isHoverBot ? hoverBotPrefab : turretPrefab, scene);
            Undo.RegisterCreatedObjectUndo(enemy, "Spawn enemy");
            enemy.name = $"{(isHoverBot ? "Enemy_HoverBot" : "Enemy_Turret")}_Extra_{i + 1:00}";
            enemy.transform.SetParent(rootGo.transform, true);
            enemy.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            occupied.Add(spot);
            spawned++;

            if (isHoverBot)
                CreatePatrolPath(enemy.GetComponent<EnemyController>(), spot, rootGo.transform, i + 1);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);

        int requested = m_HoverBotCount + m_TurretCount;
        if (spawned < requested)
            Debug.LogWarning($"[MapExpander] 자리가 부족해 {requested}마리 중 {spawned}마리만 배치했습니다. 최소 간격/거리를 줄여 보세요.");
        else
            Debug.Log($"[MapExpander] 적 {spawned}마리 추가 배치 완료.");
    }

    static void CreatePatrolPath(EnemyController enemy, Vector3 start, Transform parent, int index)
    {
        var pathGo = new GameObject($"Patrol_Extra_{index:00}");
        Undo.RegisterCreatedObjectUndo(pathGo, "Create patrol path");
        pathGo.transform.SetParent(parent, false);
        pathGo.transform.position = start;
        var patrol = pathGo.AddComponent<PatrolPath>();

        var nodes = new List<Vector3> { start };
        var navPath = new NavMeshPath();
        for (int attempt = 0; attempt < 60 && nodes.Count < 3; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(6f, 14f);
            Vector3 candidate = start + new Vector3(dir.x, 0f, dir.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                continue;
            if (!NavMesh.CalculatePath(start, hit.position, NavMesh.AllAreas, navPath) ||
                navPath.status != NavMeshPathStatus.PathComplete)
                continue;
            nodes.Add(hit.position);
        }

        for (int n = 0; n < nodes.Count; n++)
        {
            var node = new GameObject($"Patrol_Point ({n})").transform;
            node.SetParent(pathGo.transform, false);
            node.position = nodes[n] + Vector3.up;
            patrol.PathNodes.Add(node);
        }

        patrol.EnemiesToAssign.Add(enemy);
    }

    class TriangleSampler
    {
        readonly Vector3[] m_Vertices;
        readonly int[] m_Indices;
        readonly float[] m_CumulativeArea;

        public TriangleSampler(NavMeshTriangulation tri)
        {
            m_Vertices = tri.vertices;
            m_Indices = tri.indices;
            int count = m_Indices.Length / 3;
            m_CumulativeArea = new float[count];
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = m_Vertices[m_Indices[i * 3]];
                Vector3 b = m_Vertices[m_Indices[i * 3 + 1]];
                Vector3 c = m_Vertices[m_Indices[i * 3 + 2]];
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                m_CumulativeArea[i] = total;
            }
        }

        public Vector3 RandomPoint()
        {
            float r = Random.value * m_CumulativeArea[m_CumulativeArea.Length - 1];
            int i = System.Array.BinarySearch(m_CumulativeArea, r);
            if (i < 0) i = ~i;
            i = Mathf.Min(i, m_CumulativeArea.Length - 1);

            Vector3 a = m_Vertices[m_Indices[i * 3]];
            Vector3 b = m_Vertices[m_Indices[i * 3 + 1]];
            Vector3 c = m_Vertices[m_Indices[i * 3 + 2]];
            float u = Random.value, v = Random.value;
            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
            return a + (b - a) * u + (c - a) * v;
        }
    }
}
