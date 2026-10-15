using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Unity.FPS.MathMode
{
    public enum MapTheme { GridCity, Desert, Snow, NeonSpace, Original }

    // 테마별 맵을 게임 시작 시 코드로 만든다.
    // 바닥 = 좌표평면(격자·x축·y축·원점 O), 3×3 구역마다 서로 다른 구조물, 테마 소품, 조명·안개·하늘.
    public partial class MapGenerator
    {
        public const float HalfSize = 70f;

        class Style
        {
            public Color Ground, GridMinor, GridMajor, Wall, AccentA, AccentB, Prop;
            public bool GlowGrid, GlowAccent;
            public Color Sun, Ambient, Fog;
            public float SunIntensity, FogDensity;
            public Vector3 SunEuler;
        }

        public static string DisplayName(MapTheme theme) => theme switch
        {
            MapTheme.GridCity => "좌표 도시",
            MapTheme.Desert => "기하 사막",
            MapTheme.Snow => "설원 요새",
            MapTheme.NeonSpace => "네온 우주정거장",
            _ => "기본 맵",
        };

        public static string Summary(MapTheme theme) => theme switch
        {
            MapTheme.GridCity => "빌딩 숲과 공원, 낮의 도시",
            MapTheme.Desert => "피라미드와 오아시스, 노을 사막",
            MapTheme.Snow => "얼음 성채와 침엽수림, 눈보라",
            MapTheme.NeonSpace => "빛나는 반응로와 홀로그램 아치",
            _ => "원래 튜토리얼 레벨 (맵 넓히기 적용분)",
        };

        static Style GetStyle(MapTheme theme) => theme switch
        {
            MapTheme.Desert => new Style
            {
                Ground = new Color(0.85f, 0.72f, 0.5f), GridMinor = new Color(0.78f, 0.63f, 0.42f), GridMajor = new Color(0.62f, 0.47f, 0.3f),
                Wall = new Color(0.8f, 0.62f, 0.4f), AccentA = new Color(0.78f, 0.52f, 0.3f), AccentB = new Color(0.35f, 0.6f, 0.35f), Prop = new Color(0.6f, 0.45f, 0.3f),
                Sun = new Color(1f, 0.85f, 0.65f), SunIntensity = 1.4f, SunEuler = new Vector3(45f, 60f, 0f),
                Ambient = new Color(0.75f, 0.68f, 0.6f), Fog = new Color(0.95f, 0.85f, 0.7f), FogDensity = 0.0015f,
            },
            MapTheme.Snow => new Style
            {
                Ground = new Color(0.82f, 0.86f, 0.92f), GridMinor = new Color(0.8f, 0.85f, 0.92f), GridMajor = new Color(0.58f, 0.68f, 0.85f),
                Wall = new Color(0.75f, 0.82f, 0.9f), AccentA = new Color(0.55f, 0.78f, 0.95f), AccentB = new Color(0.18f, 0.38f, 0.26f), Prop = new Color(0.5f, 0.5f, 0.56f),
                Sun = new Color(0.95f, 0.97f, 1f), SunIntensity = 1.2f, SunEuler = new Vector3(45f, 150f, 0f),
                Ambient = new Color(0.7f, 0.74f, 0.82f), Fog = new Color(0.88f, 0.92f, 0.96f), FogDensity = 0.002f,
            },
            MapTheme.NeonSpace => new Style
            {
                Ground = new Color(0.16f, 0.16f, 0.24f), GridMinor = new Color(0.12f, 0.2f, 0.45f), GridMajor = new Color(0.2f, 0.6f, 1f),
                Wall = new Color(0.3f, 0.3f, 0.4f), AccentA = new Color(0f, 1f, 0.9f), AccentB = new Color(1f, 0.2f, 0.8f), Prop = new Color(0.38f, 0.38f, 0.48f),
                GlowGrid = true, GlowAccent = true,
                Sun = new Color(0.85f, 0.8f, 1f), SunIntensity = 1.0f, SunEuler = new Vector3(55f, 30f, 0f),
                Ambient = new Color(0.45f, 0.42f, 0.6f), Fog = new Color(0.12f, 0.08f, 0.22f), FogDensity = 0.002f,
            },
            _ => new Style
            {
                Ground = new Color(0.42f, 0.45f, 0.48f), GridMinor = new Color(0.5f, 0.54f, 0.58f), GridMajor = new Color(0.78f, 0.8f, 0.84f),
                Wall = new Color(0.55f, 0.58f, 0.63f), AccentA = new Color(0.25f, 0.45f, 0.8f), AccentB = new Color(0.3f, 0.6f, 0.3f), Prop = new Color(0.6f, 0.4f, 0.25f),
                Sun = new Color(1f, 0.96f, 0.9f), SunIntensity = 1.3f, SunEuler = new Vector3(50f, -30f, 0f),
                Ambient = new Color(0.68f, 0.72f, 0.8f), Fog = new Color(0.75f, 0.82f, 0.9f), FogDensity = 0.0012f,
            },
        };

        readonly MathModeManager m_Manager;
        readonly MapTheme m_Theme;
        readonly Style m_Style;
        readonly System.Random m_Rng;
        readonly Dictionary<(Color, bool, float), Material> m_Materials = new Dictionary<(Color, bool, float), Material>();
        readonly List<(Vector3 pos, float radius)> m_Occupied = new List<(Vector3, float)>();
        Transform m_Root;
        Transform m_Animated;

        public Vector3 SpawnPoint => new Vector3(0f, 0.1f, -HalfSize + 10f);

        MapGenerator(MathModeManager manager, MapTheme theme, int seed)
        {
            m_Manager = manager;
            m_Theme = theme;
            m_Style = GetStyle(theme);
            m_Rng = new System.Random(seed);
        }

        // seed: 같은 테마라도 시드가 다르면 배치가 달라진다 (랜덤 맵). buildNavMesh=false 는 메뉴 배경용
        public static MapGenerator Build(MathModeManager manager, MapTheme theme, int seed, bool buildNavMesh)
        {
            var generator = new MapGenerator(manager, theme, seed);
            generator.DisableOldLevel();
            generator.Generate(buildNavMesh);
            return generator;
        }

        // ------------------------------------------------------------------ 기존 레벨 끄기

        void DisableOldLevel()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NavMeshSurface surface in root.GetComponentsInChildren<NavMeshSurface>(true))
                    surface.enabled = false;
                DisableGeometry(root.transform);
            }
        }

        static bool IsKept(Transform t) =>
            t.GetComponent<EnemyController>() || t.GetComponent<Pickup>() || t.GetComponent<PlayerCharacterController>() ||
            t.GetComponent<Objective>() || t.GetComponent<Canvas>() || t.GetComponent<Camera>() ||
            t.GetComponent<EventSystem>() || t.GetComponent<MathModeManager>() || t.GetComponent<PatrolPath>() ||
            (t.GetComponent<Light>() && t.GetComponent<Light>().type == LightType.Directional);

        static void DisableGeometry(Transform t)
        {
            if (IsKept(t)) return;

            bool containsKept = false;
            foreach (Transform d in t.GetComponentsInChildren<Transform>(true))
                if (d != t && IsKept(d)) { containsKept = true; break; }

            if (containsKept)
            {
                foreach (Transform child in t)
                    DisableGeometry(child);
                return;
            }

            if (t.GetComponentInChildren<Renderer>(true) || t.GetComponentInChildren<Collider>(true) ||
                t.GetComponentInChildren<Light>(true) || t.GetComponentInChildren<ReflectionProbe>(true))
                t.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ 생성

        // 만들어진 루트들 (메뉴에서 맵을 바꿀 때 지우기 위해)
        public readonly List<GameObject> CreatedObjects = new List<GameObject>();

        void Generate(bool buildNavMesh)
        {
            m_Root = new GameObject("===== GENERATED MAP : " + DisplayName(m_Theme) + " =====").transform;
            m_Animated = new GameObject("===== GENERATED MAP (ANIMATED) =====").transform;

            BuildGround();
            BuildPaths();
            BuildBoundary();
            BuildBoundaryPillars();
            BuildPlaza();
            BuildOriginMonument();
            BuildZones();
            BuildHills();
            ScatterProps(45);
            BuildBackdrop();
            BuildSky();

            CreatedObjects.Add(m_Root.gameObject);
            CreatedObjects.Add(m_Animated.gameObject);
            if (buildNavMesh)
            {
                var surface = m_Root.gameObject.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
            }

            StaticBatchingUtility.Combine(m_Root.gameObject);
            ApplyLighting();
            ApplyPostProcessing();
        }

        void BuildGround()
        {
            float size = HalfSize * 2f;
            Block(m_Root, new Vector3(0f, -0.5f, 0f), new Vector3(size + 6f, 1f, size + 6f), m_Style.Ground);

            float u = m_Manager.UnitSize;
            int n = Mathf.FloorToInt(HalfSize / u);
            for (int i = -n; i <= n; i++)
            {
                float v = i * u;
                bool major = i % 5 == 0;
                Color c = i == 0 ? Color.clear : major ? m_Style.GridMajor : m_Style.GridMinor;
                float w = major ? 0.14f : 0.05f;
                if (i != 0)
                {
                    Deco(m_Root, new Vector3(v, 0.012f, 0f), new Vector3(w, 0.02f, size), c, m_Style.GlowGrid);
                    Deco(m_Root, new Vector3(0f, 0.012f, v), new Vector3(size, 0.02f, w), c, m_Style.GlowGrid);
                }

                if (major && i != 0)
                {
                    FlatLabel(new Vector3(v, 0.03f, -1.2f), i.ToString(), m_Style.GridMajor);
                    FlatLabel(new Vector3(-1.2f, 0.03f, v), i.ToString(), m_Style.GridMajor);
                }
            }

            // x축(빨강) = 월드 X, y축(초록) = 월드 Z
            Deco(m_Root, new Vector3(0f, 0.02f, 0f), new Vector3(size, 0.03f, 0.35f), new Color(1f, 0.3f, 0.3f), true);
            Deco(m_Root, new Vector3(0f, 0.02f, 0f), new Vector3(0.35f, 0.03f, size), new Color(0.3f, 1f, 0.4f), true);
            FlatLabel(new Vector3(HalfSize - 3f, 0.03f, 2.5f), "x", new Color(1f, 0.4f, 0.4f), 3f);
            FlatLabel(new Vector3(2.5f, 0.03f, HalfSize - 3f), "y", new Color(0.4f, 1f, 0.5f), 3f);
        }

        void BuildBoundary()
        {
            float h = 7f, s = HalfSize;
            Block(m_Root, new Vector3(0f, h / 2f, s + 1f), new Vector3(s * 2f + 4f, h, 2f), m_Style.Wall);
            Block(m_Root, new Vector3(0f, h / 2f, -s - 1f), new Vector3(s * 2f + 4f, h, 2f), m_Style.Wall);
            Block(m_Root, new Vector3(s + 1f, h / 2f, 0f), new Vector3(2f, h, s * 2f + 4f), m_Style.Wall);
            Block(m_Root, new Vector3(-s - 1f, h / 2f, 0f), new Vector3(2f, h, s * 2f + 4f), m_Style.Wall);

            // 벽 윗부분 띠 장식
            Color trim = m_Style.AccentA;
            Deco(m_Root, new Vector3(0f, h, s), new Vector3(s * 2f, 0.3f, 0.3f), trim, m_Style.GlowAccent);
            Deco(m_Root, new Vector3(0f, h, -s), new Vector3(s * 2f, 0.3f, 0.3f), trim, m_Style.GlowAccent);
            Deco(m_Root, new Vector3(s, h, 0f), new Vector3(0.3f, 0.3f, s * 2f), trim, m_Style.GlowAccent);
            Deco(m_Root, new Vector3(-s, h, 0f), new Vector3(0.3f, 0.3f, s * 2f), trim, m_Style.GlowAccent);
        }

        // 원점 O 기념물: 받침대 + 축 막대 + "O"
        void BuildOriginMonument()
        {
            Occupy(Vector3.zero, 10f);
            Block(m_Root, new Vector3(0f, 0.25f, 0f), new Vector3(6f, 0.5f, 6f), m_Style.Wall);
            Block(m_Root, new Vector3(0f, 2.5f, 0f), new Vector3(1f, 4f, 1f), m_Style.AccentA, glow: m_Style.GlowAccent);
            Deco(m_Root, new Vector3(1.8f, 4.2f, 0f), new Vector3(3.6f, 0.25f, 0.25f), new Color(1f, 0.3f, 0.3f), true);
            Deco(m_Root, new Vector3(0f, 4.2f, 1.8f), new Vector3(0.25f, 0.25f, 3.6f), new Color(0.3f, 1f, 0.4f), true);
            var label = m_Manager.CreateText(m_Animated, new Vector3(0f, 5.6f, 0f), new Vector2(3f, 1.5f), "O (0, 0)", Color.white, 6f);
            m_Animated.gameObject.AddComponent<Billboard>().Targets.Add(label.transform);

            // 네 귀퉁이 낮은 엄폐물
            foreach (Vector2 d in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                Block(m_Root, new Vector3(d.x * 8f, 0.6f, d.y * 8f), new Vector3(3f, 1.2f, 1f), m_Style.Prop, rotY: 45f * d.x * d.y);
        }

        void BuildZones()
        {
            float cell = HalfSize * 2f / 3f;
            var kinds = new List<int> { 0, 1, 2, 3, 4, 5, 0 }; // 0 고지대, 1 엄폐벽, 2 기둥숲, 3 관문, 4/5 테마 랜드마크
            Shuffle(kinds);

            int k = 0;
            for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue;     // 원점 광장
                if (i == 0 && j == -1) continue;    // 플레이어 시작 지점
                Vector3 center = new Vector3(i * cell + Range(-4f, 4f), 0f, j * cell + Range(-4f, 4f));
                var zone = new GameObject("Zone").transform;
                zone.SetParent(m_Root, false);
                zone.position = center;
                zone.rotation = Quaternion.Euler(0f, 90f * m_Rng.Next(4), 0f);
                Occupy(center, 15f);
                ZonePatch(center);

                switch (kinds[k++ % kinds.Count])
                {
                    case 0: Platform(zone); break;
                    case 1: CoverWalls(zone); break;
                    case 2: PillarGrove(zone); break;
                    case 3: Gate(zone); break;
                    case 4: LandmarkA(zone); break;
                    default: LandmarkB(zone); break;
                }
            }

            // 시작 지점 주변은 낮은 상자 몇 개만
            for (int i = 0; i < 4; i++)
                Block(m_Root, SpawnPoint + new Vector3(Range(-14f, 14f), 0.6f, Range(6f, 16f)), new Vector3(1.4f, 1.2f, 1.4f), m_Style.Prop, rotY: Range(0f, 90f));
            Occupy(SpawnPoint, 8f);
        }

        // 고지대: 3m 높이 단상 + 양쪽 경사로 (경사각 = arctan(3/10) ≈ 16.7°)
        void Platform(Transform z)
        {
            Block(z, new Vector3(0f, 1.5f, 0f), new Vector3(14f, 3f, 14f), m_Style.Wall);
            Deco(z, new Vector3(0f, 3.02f, 0f), new Vector3(12f, 0.04f, 12f), m_Style.AccentA, m_Style.GlowAccent);
            Ramp(z, new Vector3(0f, 0f, 7f), Vector3.forward, 3f, 10f, 4f);
            Ramp(z, new Vector3(0f, 0f, -7f), Vector3.back, 3f, 10f, 4f);
            Block(z, new Vector3(4f, 3.6f, 3f), new Vector3(1.2f, 1.2f, 1.2f), m_Style.Prop);
            Block(z, new Vector3(-4f, 3.6f, -2f), new Vector3(2.5f, 1.2f, 1f), m_Style.Prop);
            Deco(z, new Vector3(0f, 5f, 0f), new Vector3(0.2f, 4f, 0.2f), m_Style.AccentB, m_Style.GlowAccent); // 깃대
        }

        void CoverWalls(Transform z)
        {
            for (int i = 0; i < 6; i++)
            {
                float len = Range(5f, 9f);
                Vector3 p = new Vector3(Range(-12f, 12f), 1.2f, Range(-12f, 12f));
                Block(z, p, new Vector3(len, 2.4f, 0.7f), i % 2 == 0 ? m_Style.Wall : m_Style.AccentA, rotY: m_Rng.Next(2) * 90f);
            }

            Block(z, new Vector3(0f, 0.5f, 0f), new Vector3(1.5f, 1f, 1.5f), m_Style.Prop);
        }

        void PillarGrove(Transform z)
        {
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                float h = Range(4f, 8f);
                Block(z, new Vector3(x * 7f, h / 2f, y * 7f), new Vector3(1.6f, h, 1.6f), (x + y) % 2 == 0 ? m_Style.Wall : m_Style.AccentA,
                    glow: m_Style.GlowAccent && (x + y) % 2 != 0);
                Deco(z, new Vector3(x * 7f, h + 0.15f, y * 7f), new Vector3(2f, 0.3f, 2f), m_Style.AccentB, m_Style.GlowAccent);
            }
        }

        void Gate(Transform z)
        {
            Block(z, new Vector3(-6f, 4.5f, 0f), new Vector3(3f, 9f, 3f), m_Style.Wall);
            Block(z, new Vector3(6f, 4.5f, 0f), new Vector3(3f, 9f, 3f), m_Style.Wall);
            Block(z, new Vector3(0f, 9.5f, 0f), new Vector3(15f, 2f, 3f), m_Style.AccentA, glow: m_Style.GlowAccent);
            Block(z, new Vector3(-12f, 1.1f, 0f), new Vector3(9f, 2.2f, 0.8f), m_Style.Wall);
            Block(z, new Vector3(12f, 1.1f, 0f), new Vector3(9f, 2.2f, 0.8f), m_Style.Wall);
        }

        void LandmarkA(Transform z)
        {
            switch (m_Theme)
            {
                case MapTheme.GridCity:
                    // 빌딩 4동 + 창문 띠
                    for (int i = 0; i < 4; i++)
                    {
                        float x = i % 2 == 0 ? -8f : 8f, y = i < 2 ? -8f : 8f, h = Range(12f, 24f);
                        Color c = Color.Lerp(m_Style.Wall, m_Style.AccentA, Range(0f, 0.6f));
                        Block(z, new Vector3(x, h / 2f, y), new Vector3(9f, h, 9f), c);
                        for (float band = 3f; band < h - 1f; band += 3f)
                            Deco(z, new Vector3(x, band, y), new Vector3(9.1f, 0.5f, 9.1f), new Color(1f, 0.92f, 0.6f), true);
                    }
                    break;
                case MapTheme.Desert:
                    // 계단식 피라미드 + 오벨리스크
                    for (int layer = 0; layer < 6; layer++)
                    {
                        float s = 18f - layer * 3f;
                        Block(z, new Vector3(0f, 0.6f + layer * 1.2f, 0f), new Vector3(s, 1.2f, s), Color.Lerp(m_Style.Wall, m_Style.AccentA, layer / 6f));
                    }
                    Block(z, new Vector3(12f, 4f, 10f), new Vector3(1.4f, 8f, 1.4f), m_Style.AccentA);
                    Block(z, new Vector3(-12f, 4f, 10f), new Vector3(1.4f, 8f, 1.4f), m_Style.AccentA);
                    break;
                case MapTheme.Snow:
                    // 팔각형 얼음 성채 (입구 2곳)
                    for (int i = 0; i < 8; i++)
                    {
                        if (i == 0 || i == 4) continue;
                        float a = i * 45f;
                        Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 10f;
                        Block(z, p + Vector3.up * 1.75f, new Vector3(8.2f, 3.5f, 1.2f), m_Style.AccentA, rotY: a);
                    }
                    Block(z, new Vector3(0f, 3f, 0f), new Vector3(3f, 6f, 3f), m_Style.Wall);
                    Deco(z, new Vector3(0f, 6.5f, 0f), new Vector3(3.6f, 1f, 3.6f), m_Style.AccentA, false);
                    break;
                default:
                    // 반응로 + 회전하는 빛 고리
                    Block(z, new Vector3(0f, 3f, 0f), new Vector3(3f, 6f, 3f), m_Style.Wall);
                    Deco(z, new Vector3(0f, 3f, 0f), new Vector3(3.3f, 5f, 3.3f), m_Style.AccentA, true);
                    var ring = new GameObject("ReactorRing").transform;
                    ring.SetParent(m_Animated, false);
                    ring.position = z.position + Vector3.up * 7f;
                    ring.gameObject.AddComponent<ConstantRotation>().RotatingSpeed = 40f;
                    for (int i = 0; i < 16; i++)
                    {
                        Vector3 p = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * 7f;
                        Deco(ring, p, new Vector3(0.8f, 0.8f, 0.8f), i % 2 == 0 ? m_Style.AccentA : m_Style.AccentB, true);
                    }
                    for (int i = 0; i < 4; i++)
                        Block(z, Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward * 11f + Vector3.up * 0.75f, new Vector3(4f, 1.5f, 1f), m_Style.Prop, rotY: i * 90f + 45f);
                    break;
            }
        }

        void LandmarkB(Transform z)
        {
            switch (m_Theme)
            {
                case MapTheme.GridCity:
                    // 공원: 나무 원형 배치 + 벤치
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 p = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 10f;
                        Tree(z, p, 1f);
                    }
                    Block(z, new Vector3(0f, 0.4f, 3f), new Vector3(4f, 0.8f, 1f), m_Style.Prop);
                    Block(z, new Vector3(0f, 0.4f, -3f), new Vector3(4f, 0.8f, 1f), m_Style.Prop);
                    break;
                case MapTheme.Desert:
                    // 오아시스: 물 + 야자수
                    Deco(z, new Vector3(0f, 0.03f, 0f), new Vector3(12f, 0.04f, 9f), new Color(0.2f, 0.55f, 0.75f), true);
                    for (int i = 0; i < 5; i++)
                        Palm(z, Quaternion.Euler(0f, i * 72f, 0f) * Vector3.forward * 9f);
                    break;
                case MapTheme.Snow:
                    // 얼어붙은 호수 + 얼음 가시
                    Deco(z, new Vector3(0f, 0.03f, 0f), new Vector3(16f, 0.04f, 12f), new Color(0.7f, 0.85f, 0.95f), false);
                    for (int i = 0; i < 7; i++)
                    {
                        Vector3 p = new Vector3(Range(-9f, 9f), 0f, Range(-7f, 7f));
                        float h = Range(2f, 5f);
                        var spike = Block(z, p + Vector3.up * h / 2f, new Vector3(0.9f, h, 0.9f), m_Style.AccentA);
                        spike.transform.localRotation = Quaternion.Euler(Range(-10f, 10f), Range(0f, 90f), Range(-10f, 10f));
                    }
                    break;
                default:
                    // 홀로그램 아치 3개가 길을 이룬다
                    for (int i = -1; i <= 1; i++)
                    {
                        float zPos = i * 7f;
                        Block(z, new Vector3(-4f, 3f, zPos), new Vector3(0.8f, 6f, 0.8f), m_Style.Wall);
                        Block(z, new Vector3(4f, 3f, zPos), new Vector3(0.8f, 6f, 0.8f), m_Style.Wall);
                        Deco(z, new Vector3(0f, 6.2f, zPos), new Vector3(8.8f, 0.4f, 0.4f), i == 0 ? m_Style.AccentB : m_Style.AccentA, true);
                        Deco(z, new Vector3(-4f, 3f, zPos), new Vector3(0.9f, 6f, 0.2f), m_Style.AccentA, true);
                        Deco(z, new Vector3(4f, 3f, zPos), new Vector3(0.9f, 6f, 0.2f), m_Style.AccentA, true);
                    }
                    Block(z, new Vector3(-9f, 0.75f, 0f), new Vector3(1.5f, 1.5f, 10f), m_Style.Prop);
                    Block(z, new Vector3(9f, 0.75f, 0f), new Vector3(1.5f, 1.5f, 10f), m_Style.Prop);
                    break;
            }
        }

        // 완만한 언덕 몇 개 (평지가 단조롭지 않도록)
        void BuildHills()
        {
            for (int i = 0; i < 4; i++)
            {
                if (!TryFreeSpot(12f, out Vector3 p)) continue;
                Occupy(p, 9f);
                var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hill.transform.SetParent(m_Root, false);
                hill.transform.position = p + Vector3.down * 0.9f;
                hill.transform.localScale = new Vector3(Range(12f, 18f), 3.2f, Range(12f, 18f));
                // 납작한 구는 SphereCollider로 표현할 수 없으므로 메시 충돌체로 교체
                Object.DestroyImmediate(hill.GetComponent<Collider>());
                hill.AddComponent<MeshCollider>();
                SetMaterial(hill.GetComponent<Renderer>(), Color.Lerp(m_Style.Ground, m_Style.GridMinor, 0.5f), false, 0.1f);
            }
        }

        void ScatterProps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryFreeSpot(3f, out Vector3 p)) continue;
                Occupy(p, 3.5f);
                var prop = new GameObject("Prop").transform;
                prop.SetParent(m_Root, false);
                prop.position = p;
                prop.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                int kind = m_Rng.Next(3);

                switch (m_Theme)
                {
                    case MapTheme.GridCity:
                        if (kind == 0) Car(prop);
                        else if (kind == 1) Lamp(prop);
                        else Crates(prop);
                        break;
                    case MapTheme.Desert:
                        if (kind == 0) Cactus(prop);
                        else if (kind == 1) Rock(prop);
                        else Crates(prop);
                        break;
                    case MapTheme.Snow:
                        if (kind == 0) Tree(prop, Vector3.zero, Range(0.8f, 1.3f));
                        else if (kind == 1) Rock(prop);
                        else Tree(prop, Vector3.zero, 1f);
                        break;
                    default:
                        if (kind == 0) Crystal(prop);
                        else if (kind == 1) Lamp(prop);
                        else Crates(prop);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ 소품

        void Crates(Transform p)
        {
            Block(p, new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 1.2f, 1.2f), m_Style.Prop);
            if (m_Rng.Next(2) == 0) Block(p, new Vector3(0.9f, 0.5f, 0.6f), new Vector3(1f, 1f, 1f), m_Style.Prop, rotY: 20f);
            if (m_Rng.Next(3) == 0) Block(p, new Vector3(0.1f, 1.6f, 0f), new Vector3(0.9f, 0.8f, 0.9f), m_Style.Prop, rotY: 35f);
        }

        void Car(Transform p)
        {
            Color c = Color.HSVToRGB((float)m_Rng.NextDouble(), 0.6f, 0.8f);
            Block(p, new Vector3(0f, 0.6f, 0f), new Vector3(2f, 0.9f, 4.2f), c);
            Block(p, new Vector3(0f, 1.35f, -0.3f), new Vector3(1.8f, 0.7f, 2.2f), c * 0.8f);
            Deco(p, new Vector3(0f, 1.35f, 0.82f), new Vector3(1.7f, 0.55f, 0.05f), new Color(0.6f, 0.8f, 0.95f), false);
        }

        void Lamp(Transform p)
        {
            Block(p, new Vector3(0f, 2.5f, 0f), new Vector3(0.2f, 5f, 0.2f), m_Style.Wall);
            Deco(p, new Vector3(0f, 5.1f, 0f), new Vector3(0.6f, 0.3f, 0.6f), m_Theme == MapTheme.NeonSpace ? m_Style.AccentB : new Color(1f, 0.95f, 0.7f), true);
        }

        void Tree(Transform p, Vector3 local, float scale)
        {
            Block(p, local + new Vector3(0f, 1.2f * scale, 0f), new Vector3(0.4f, 2.4f * scale, 0.4f) , new Color(0.4f, 0.27f, 0.15f));
            if (m_Theme == MapTheme.Snow)
            {
                // 침엽수: 층층이 쌓인 잎 + 하얀 눈 모자
                for (int i = 0; i < 3; i++)
                {
                    float s = (2.6f - i * 0.7f) * scale;
                    var leaf = Deco(p, local + new Vector3(0f, (2.4f + i * 1.1f) * scale, 0f), new Vector3(s, 1.3f * scale, s), m_Style.AccentB, false);
                    leaf.transform.localRotation = Quaternion.Euler(0f, 45f * i, 0f);
                }
                Ball(p, local + new Vector3(0f, 5.2f * scale, 0f), new Vector3(0.9f, 0.6f, 0.9f) * scale, Color.white);
                return;
            }

            // 둥근 활엽수: 공 모양 잎 3~4덩이
            Color leafColor = m_Style.AccentB;
            Ball(p, local + new Vector3(0f, 3.2f * scale, 0f), new Vector3(2.8f, 2.4f, 2.8f) * scale, leafColor);
            Ball(p, local + new Vector3(0.8f, 2.7f * scale, 0.3f), new Vector3(1.8f, 1.6f, 1.8f) * scale, leafColor * 1.12f);
            Ball(p, local + new Vector3(-0.7f, 2.8f * scale, -0.4f), new Vector3(1.7f, 1.5f, 1.7f) * scale, leafColor * 0.9f);
            if (m_Rng.Next(2) == 0)
                Ball(p, local + new Vector3(0.1f, 4.1f * scale, 0.2f), new Vector3(1.4f, 1.2f, 1.4f) * scale, leafColor * 1.2f);
        }

        void Palm(Transform p, Vector3 local)
        {
            var trunk = Block(p, local + new Vector3(0f, 2.5f, 0f), new Vector3(0.35f, 5f, 0.35f), new Color(0.55f, 0.4f, 0.25f));
            trunk.transform.localRotation = Quaternion.Euler(8f, 0f, 6f);
            for (int i = 0; i < 5; i++)
            {
                var leaf = Deco(p, local + new Vector3(0f, 5f, 0f), new Vector3(0.6f, 0.1f, 3.2f), m_Style.AccentB, false);
                leaf.transform.localRotation = Quaternion.Euler(20f, i * 72f, 0f);
            }
        }

        void Cactus(Transform p)
        {
            Block(p, new Vector3(0f, 1.5f, 0f), new Vector3(0.6f, 3f, 0.6f), m_Style.AccentB);
            Deco(p, new Vector3(0.6f, 1.8f, 0f), new Vector3(0.9f, 0.4f, 0.4f), m_Style.AccentB, false);
            Deco(p, new Vector3(0.9f, 2.3f, 0f), new Vector3(0.4f, 1f, 0.4f), m_Style.AccentB, false);
        }

        void Rock(Transform p)
        {
            var rock = Block(p, new Vector3(0f, 0.6f, 0f), new Vector3(Range(1.5f, 3f), Range(1f, 1.8f), Range(1.5f, 3f)), m_Style.Prop);
            rock.transform.localRotation = Quaternion.Euler(Range(-15f, 15f), Range(0f, 90f), Range(-15f, 15f));
        }

        void Crystal(Transform p)
        {
            Color c = m_Rng.Next(2) == 0 ? m_Style.AccentA : m_Style.AccentB;
            var crystal = Block(p, new Vector3(0f, 1.2f, 0f), new Vector3(1f, 2.4f, 1f), c, glow: true);
            crystal.transform.localRotation = Quaternion.Euler(0f, 45f, 12f);
        }

        void Ramp(Transform parent, Vector3 edge, Vector3 outward, float rise, float run, float width)
        {
            float length = Mathf.Sqrt(run * run + rise * rise);
            Vector3 uphill = (-outward * run + Vector3.up * rise).normalized;
            var ramp = Block(parent, edge + outward * (run / 2f) + Vector3.up * (rise / 2f - 0.2f), new Vector3(width, 0.4f, length), m_Style.AccentA,
                glow: false);
            ramp.transform.localRotation = Quaternion.LookRotation(uphill, Vector3.up);
        }

        // ------------------------------------------------------------------ 조명

        void ApplyLighting()
        {
            Light sun = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
                if (l.type == LightType.Directional) { sun = l; break; }

            if (!sun)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.gameObject.SetActive(true);
            sun.transform.rotation = Quaternion.Euler(m_Style.SunEuler);
            sun.color = m_Style.Sun;
            sun.intensity = m_Style.SunIntensity;
            sun.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = m_Style.Ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = m_Style.Fog;
            RenderSettings.fogDensity = m_Style.FogDensity;

            int index = (int)m_Theme;
            if (m_Manager.ThemeSkyboxes != null && index < m_Manager.ThemeSkyboxes.Length && m_Manager.ThemeSkyboxes[index])
                RenderSettings.skybox = m_Manager.ThemeSkyboxes[index];
            RenderSettings.sun = sun;
            DynamicGI.UpdateEnvironment();
        }

        // ------------------------------------------------------------------ 도우미

        GameObject Block(Transform parent, Vector3 localPos, Vector3 scale, Color color, float rotY = 0f, bool glow = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            go.transform.localScale = scale;
            // 큰 블록은 밝기를 살짝씩 다르게 해서 단조롭지 않게 (3단계로만 나눠 머티리얼 수를 제한)
            if (!glow && scale.x * scale.y * scale.z > 0.5f)
            {
                float f = 0.93f + 0.06f * m_Rng.Next(3);
                color = new Color(color.r * f, color.g * f, color.b * f, color.a);
            }
            SetMaterial(go.GetComponent<Renderer>(), color, glow, 0.2f);
            return go;
        }

        // 충돌체 없는 장식
        GameObject Deco(Transform parent, Vector3 localPos, Vector3 scale, Color color, bool glow)
        {
            GameObject go = Block(parent, localPos, scale, color, 0f, glow);
            Object.DestroyImmediate(go.GetComponent<Collider>()); // NavMesh 굽기 전에 즉시 제거
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        void FlatLabel(Vector3 pos, string text, Color color, float size = 1.6f)
        {
            var t = m_Manager.CreateText(m_Animated, pos, new Vector2(size * 1.5f, size), text, color, size * 3f); // 글씨는 정적 배칭 제외
            t.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void SetMaterial(Renderer renderer, Color color, bool glow, float smoothness)
        {
            var key = (color, glow, smoothness);
            if (!m_Materials.TryGetValue(key, out Material mat))
            {
                Material source = glow || !m_Manager.LitMaterial ? m_Manager.UnlitMaterial : m_Manager.LitMaterial;
                mat = source ? new Material(source) : new Material(renderer.sharedMaterial);
                // 빛나는 색은 1보다 밝게(HDR) 넣어 후처리 빛 번짐(Bloom)이 걸리게 한다
                Color c = glow ? new Color(color.r * 1.6f, color.g * 1.6f, color.b * 1.6f, color.a) : color;
                mat.SetColor("_BaseColor", c);
                mat.SetColor("_Color", c);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                m_Materials[key] = mat;
            }

            renderer.sharedMaterial = mat;
        }

        void Occupy(Vector3 p, float radius) => m_Occupied.Add((p, radius));

        bool TryFreeSpot(float radius, out Vector3 spot)
        {
            float limit = HalfSize - 5f;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                Vector3 p = new Vector3(Range(-limit, limit), 0f, Range(-limit, limit));
                bool free = true;
                foreach (var (pos, r) in m_Occupied)
                    if (Vector3.Distance(pos, p) < r + radius) { free = false; break; }
                // 축 위(좌표 탐색 목표가 자주 생기는 선)는 비워 둔다
                if (Mathf.Abs(p.x) < 2f || Mathf.Abs(p.z) < 2f) free = false;
                if (NearPath(p, radius + 1.5f)) free = false; // 길 위에는 물건을 두지 않는다
                if (!free) continue;
                spot = p;
                return true;
            }

            spot = default;
            return false;
        }

        float Range(float min, float max) => min + (float)m_Rng.NextDouble() * (max - min);

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = m_Rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    // 여러 글씨가 항상 플레이어(카메라)를 바라보게 한다
    public class Billboard : MonoBehaviour
    {
        public readonly List<Transform> Targets = new List<Transform>();

        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (!cam) return;
            foreach (Transform t in Targets)
            {
                if (!t) continue;
                Vector3 look = t.position - cam.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(look);
            }
        }
    }
}
