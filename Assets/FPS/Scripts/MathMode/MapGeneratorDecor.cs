using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.FPS.MathMode
{
    // 맵을 예쁘게 꾸미는 부분: 길·가로등, 구역 바닥, 분수 광장, 벽 기둥, 맵 밖 배경, 구름, 후처리
    public partial class MapGenerator
    {
        readonly List<(Vector3 a, Vector3 b)> m_Paths = new List<(Vector3, Vector3)>();

        Color PathColor => m_Theme switch
        {
            MapTheme.Desert => new Color(0.95f, 0.86f, 0.66f),
            MapTheme.Snow => new Color(0.66f, 0.74f, 0.86f),
            MapTheme.NeonSpace => new Color(0.24f, 0.24f, 0.34f),
            _ => new Color(0.74f, 0.72f, 0.68f),
        };

        Color PatchColor => m_Theme switch
        {
            MapTheme.Desert => new Color(0.78f, 0.6f, 0.4f),
            MapTheme.Snow => new Color(0.74f, 0.87f, 0.98f),
            MapTheme.NeonSpace => new Color(0.1f, 0.1f, 0.18f),
            _ => new Color(0.4f, 0.64f, 0.34f), // 잔디
        };

        // ------------------------------------------------------------------ 길

        void BuildPaths()
        {
            float cell = HalfSize * 2f / 3f;

            // 시작 지점 → 광장
            Path(SpawnPoint, new Vector3(0f, 0f, -16f), 4f);

            // 광장을 감싸는 원형 산책로 (반지름 16)
            const int segments = 28;
            for (int i = 0; i < segments; i++)
            {
                float a0 = 2f * Mathf.PI * i / segments, a1 = 2f * Mathf.PI * (i + 1) / segments;
                Path(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 16f, new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 16f, 3f, decorate: false);
            }

            // 광장에서 각 구역으로 뻗는 길
            for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
            {
                if ((i == 0 && j == 0) || (i == 0 && j == -1)) continue;
                Vector3 dir = new Vector3(i, 0f, j).normalized;
                Path(dir * 16f, new Vector3(i * cell, 0f, j * cell) - dir * 14f, 3f);
            }
        }

        void Path(Vector3 a, Vector3 b, float width, bool decorate = true)
        {
            m_Paths.Add((a, b));
            Vector3 d = b - a;
            float length = d.magnitude;
            if (length < 0.1f) return;
            float rotY = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Vector3 mid = (a + b) / 2f;

            var strip = Deco(m_Root, mid + Vector3.up * 0.008f, new Vector3(width, 0.02f, length + 0.4f), PathColor, false);
            strip.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);

            // 양쪽 테두리 (네온은 빛나는 선)
            Vector3 side = Quaternion.Euler(0f, rotY, 0f) * Vector3.right * (width / 2f);
            Color edge = m_Theme == MapTheme.NeonSpace ? m_Style.AccentA : PathColor * 0.8f;
            foreach (float s in new[] { -1f, 1f })
            {
                var e = Deco(m_Root, mid + side * s + Vector3.up * 0.014f, new Vector3(0.18f, 0.03f, length + 0.4f), edge, m_Theme == MapTheme.NeonSpace);
                e.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            }

            if (!decorate) return;

            // 길가 장식: 가로등과 덤불을 번갈아
            Vector3 dir = d / length;
            int n = Mathf.FloorToInt(length / 9f);
            for (int i = 1; i <= n; i++)
            {
                Vector3 p = a + dir * (i * 9f);
                Vector3 offset = side.normalized * (width / 2f + 1.2f) * (i % 2 == 0 ? 1f : -1f);
                if (i % 2 == 0) LampAt(p + offset);
                else Shrub(p + offset);
            }
        }

        bool NearPath(Vector3 p, float distance)
        {
            foreach (var (a, b) in m_Paths)
            {
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.001f));
                if (Vector3.Distance(p, a + ab * t) < distance) return true;
            }

            return false;
        }

        void LampAt(Vector3 p)
        {
            var root = new GameObject("Lamp").transform;
            root.SetParent(m_Root, false);
            root.position = p;
            if (m_Theme == MapTheme.Desert)
            {
                // 횃불
                Block(root, new Vector3(0f, 1.2f, 0f), new Vector3(0.18f, 2.4f, 0.18f), new Color(0.45f, 0.3f, 0.18f));
                Ball(root, new Vector3(0f, 2.55f, 0f), new Vector3(0.45f, 0.6f, 0.45f), new Color(1f, 0.6f, 0.2f), glow: true);
                return;
            }

            Block(root, new Vector3(0f, 1.8f, 0f), new Vector3(0.16f, 3.6f, 0.16f), m_Theme == MapTheme.NeonSpace ? m_Style.Wall : new Color(0.2f, 0.22f, 0.26f));
            Deco(root, new Vector3(0.35f, 3.55f, 0f), new Vector3(0.8f, 0.08f, 0.12f), new Color(0.2f, 0.22f, 0.26f), false);
            Ball(root, new Vector3(0.7f, 3.4f, 0f), Vector3.one * 0.45f,
                m_Theme == MapTheme.NeonSpace ? m_Style.AccentB : new Color(1f, 0.93f, 0.7f), glow: true);
        }

        void Shrub(Vector3 p)
        {
            var root = new GameObject("Shrub").transform;
            root.SetParent(m_Root, false);
            root.position = p;
            switch (m_Theme)
            {
                case MapTheme.Desert:
                    Ball(root, new Vector3(0f, 0.3f, 0f), new Vector3(1.2f, 0.6f, 1f), m_Style.Prop);
                    break;
                case MapTheme.Snow:
                    Ball(root, new Vector3(0f, 0.35f, 0f), new Vector3(1.4f, 0.8f, 1.4f), Color.white);
                    break;
                case MapTheme.NeonSpace:
                    Deco(root, new Vector3(0f, 0.5f, 0f), new Vector3(0.35f, 1f, 0.35f), m_Style.AccentA, true);
                    break;
                default:
                    Ball(root, new Vector3(0f, 0.55f, 0f), new Vector3(1.4f, 1.1f, 1.4f), m_Style.AccentB);
                    Flowers(root, Vector3.zero, 1.2f, 5);
                    break;
            }
        }

        void Flowers(Transform parent, Vector3 center, float radius, int count)
        {
            Color[] colors = { new Color(1f, 0.45f, 0.6f), new Color(1f, 0.85f, 0.3f), new Color(0.75f, 0.55f, 1f), Color.white };
            for (int i = 0; i < count; i++)
            {
                Vector2 o = Random2() * radius;
                Ball(parent, center + new Vector3(o.x, 0.25f, o.y), Vector3.one * 0.3f, colors[m_Rng.Next(colors.Length)]);
            }
        }

        // ------------------------------------------------------------------ 구역 바닥

        void ZonePatch(Vector3 center)
        {
            var rim = Deco(m_Root, center + Vector3.up * 0.004f, new Vector3(32f, 0.01f, 32f), m_Theme == MapTheme.NeonSpace ? m_Style.AccentB : PatchColor * 0.85f,
                m_Theme == MapTheme.NeonSpace);
            Cylinderize(rim);
            var patch = Deco(m_Root, center + Vector3.up * 0.006f, new Vector3(30.5f, 0.01f, 30.5f), PatchColor, false);
            Cylinderize(patch);

            // 테마별 바닥 장식
            for (int i = 0; i < 10; i++)
            {
                Vector2 o = Random2() * 14f;
                Vector3 p = center + new Vector3(o.x, 0f, o.y);
                switch (m_Theme)
                {
                    case MapTheme.GridCity: Flowers(m_Root, p, 0.8f, 3); break;
                    case MapTheme.Desert: Deco(m_Root, p + Vector3.up * 0.15f, new Vector3(0.6f, 0.3f, 0.5f), m_Style.Prop, false); break;
                    case MapTheme.Snow: Deco(m_Root, p + Vector3.up * 0.2f, new Vector3(0.3f, 0.4f, 0.3f), m_Style.AccentA, true).transform.localRotation = Quaternion.Euler(0f, 45f, 20f); break;
                    default: Ball(m_Root, p + Vector3.up * 0.1f, Vector3.one * 0.25f, i % 2 == 0 ? m_Style.AccentA : m_Style.AccentB, glow: true); break;
                }
            }
        }

        // 납작한 정육면체 대신 원판 메시를 쓴다
        void Cylinderize(GameObject go)
        {
            go.GetComponent<MeshFilter>().sharedMesh = PrimitiveMesh(PrimitiveType.Cylinder);
            Vector3 s = go.transform.localScale;
            go.transform.localScale = new Vector3(s.x, s.y * 0.5f, s.z);
        }

        static readonly Dictionary<PrimitiveType, Mesh> s_Meshes = new Dictionary<PrimitiveType, Mesh>();

        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            if (s_Meshes.TryGetValue(type, out Mesh mesh) && mesh) return mesh;
            var tmp = GameObject.CreatePrimitive(type);
            mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            s_Meshes[type] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------ 광장

        void BuildPlaza()
        {
            // 동심원 바닥 (과녁 / 원의 방정식 느낌)
            Color[] rings = { PathColor * 0.9f, PathColor, PathColor * 0.82f, PathColor };
            for (int i = 0; i < rings.Length; i++)
            {
                float d = 26f - i * 5f;
                var disc = Deco(m_Root, Vector3.up * (0.005f + i * 0.002f), new Vector3(d, 0.01f, d), rings[i], false);
                Cylinderize(disc);
            }

            // 분수: 돌 수조 + 물(테마별: 물/얼음/빛)
            var pool = Block(m_Root, new Vector3(0f, 0.3f, 0f), new Vector3(11f, 0.3f, 11f), m_Style.Wall);
            pool.GetComponent<MeshFilter>().sharedMesh = PrimitiveMesh(PrimitiveType.Cylinder);
            Object.DestroyImmediate(pool.GetComponent<Collider>());
            pool.AddComponent<MeshCollider>(); // 원기둥 메시 그대로 충돌

            Color water = m_Theme switch
            {
                MapTheme.Snow => new Color(0.8f, 0.92f, 1f),
                MapTheme.NeonSpace => m_Style.AccentA,
                _ => new Color(0.3f, 0.65f, 0.9f),
            };
            var surface = Deco(m_Root, new Vector3(0f, 0.61f, 0f), new Vector3(10f, 0.01f, 10f), water, m_Theme != MapTheme.Snow);
            Cylinderize(surface);

            // 광장 둘레 벤치와 화분
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 30f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 12.5f;
                if (Mathf.Abs(p.x) < 3f && p.z < 0f) continue; // 시작 지점 쪽 길은 비운다
                var bench = Block(m_Root, p + Vector3.up * 0.25f, new Vector3(3f, 0.5f, 0.8f), new Color(0.55f, 0.36f, 0.2f), rotY: a);
                bench.transform.localRotation = Quaternion.Euler(0f, a + 90f, 0f);
                Vector3 pot = Quaternion.Euler(0f, a + 30f, 0f) * Vector3.forward * 12.5f;
                Block(m_Root, pot + Vector3.up * 0.4f, new Vector3(1f, 0.8f, 1f), m_Style.Prop);
                if (m_Theme == MapTheme.GridCity) Flowers(m_Root, pot + Vector3.up * 0.6f, 0.35f, 4);
                else Ball(m_Root, pot + Vector3.up * 1.1f, Vector3.one * 0.9f, m_Theme == MapTheme.Snow ? Color.white : m_Style.AccentB, glow: m_Theme == MapTheme.NeonSpace);
            }
        }

        // ------------------------------------------------------------------ 경계벽 기둥

        void BuildBoundaryPillars()
        {
            float s = HalfSize + 1f;
            for (float t = -HalfSize; t <= HalfSize + 0.1f; t += 14f)
            {
                foreach (Vector3 p in new[] { new Vector3(t, 0f, s), new Vector3(t, 0f, -s), new Vector3(s, 0f, t), new Vector3(-s, 0f, t) })
                {
                    Block(m_Root, p + Vector3.up * 4.2f, new Vector3(2.6f, 8.4f, 2.6f), m_Style.Wall * 0.9f);
                    Deco(m_Root, p + Vector3.up * 8.6f, new Vector3(3f, 0.4f, 3f), m_Style.AccentA, m_Style.GlowAccent);
                    Ball(m_Root, p + Vector3.up * 9.2f, Vector3.one * 0.9f, m_Style.AccentB, glow: true);
                }
            }
        }

        // ------------------------------------------------------------------ 맵 밖 배경

        void BuildBackdrop()
        {
            var root = new GameObject("Backdrop").transform;
            root.SetParent(m_Root, false);
            for (int i = 0; i < 36; i++)
            {
                float a = i * 10f + Range(-3f, 3f);
                float r = Range(95f, 135f);
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * r;
                switch (m_Theme)
                {
                    case MapTheme.GridCity:
                    {
                        float h = Range(20f, 60f), w = Range(10f, 20f);
                        Color c = Color.Lerp(new Color(0.55f, 0.6f, 0.7f), new Color(0.75f, 0.78f, 0.85f), (float)m_Rng.NextDouble());
                        Deco(root, p + Vector3.up * h / 2f, new Vector3(w, h, w), c, false).transform.localRotation = Quaternion.Euler(0f, a, 0f);
                        for (float band = 6f; band < h - 2f; band += 6f)
                            Deco(root, p + Vector3.up * band, new Vector3(w + 0.2f, 0.6f, w + 0.2f), new Color(1f, 0.92f, 0.65f), true).transform.localRotation = Quaternion.Euler(0f, a, 0f);
                        break;
                    }
                    case MapTheme.Desert:
                        Ball(root, p + Vector3.down * 4f, new Vector3(Range(40f, 70f), Range(14f, 26f), Range(25f, 40f)), Color.Lerp(m_Style.Ground, m_Style.AccentA, 0.3f));
                        if (i % 9 == 0)
                            for (int layer = 0; layer < 8; layer++)
                                Deco(root, p * 1.15f + Vector3.up * (3f + layer * 4f), new Vector3(40f - layer * 5f, 4f, 40f - layer * 5f), m_Style.Wall, false);
                        break;
                    case MapTheme.Snow:
                    {
                        float h = Range(30f, 70f);
                        Ball(root, p + Vector3.up * (h * 0.2f), new Vector3(Range(40f, 60f), h, Range(40f, 60f)), new Color(0.55f, 0.6f, 0.7f));
                        Ball(root, p + Vector3.up * (h * 0.58f), new Vector3(Range(18f, 26f), h * 0.3f, Range(18f, 26f)), Color.white);
                        break;
                    }
                    default:
                        if (i % 3 == 0)
                        {
                            float h = Range(20f, 50f);
                            Deco(root, p + Vector3.up * h / 2f, new Vector3(6f, h, 6f), m_Style.Wall, false);
                            Deco(root, p + Vector3.up * h / 2f, new Vector3(6.3f, h * 0.9f, 1f), i % 2 == 0 ? m_Style.AccentA : m_Style.AccentB, true);
                        }
                        break;
                }
            }

            if (m_Theme == MapTheme.NeonSpace)
            {
                // 하늘에 뜬 행성과 고리
                Ball(root, new Vector3(-120f, 90f, 160f), Vector3.one * 50f, new Color(0.6f, 0.35f, 1f), glow: true);
                Ball(root, new Vector3(140f, 60f, 120f), Vector3.one * 22f, new Color(1f, 0.5f, 0.75f), glow: true);
                var ring = Deco(root, new Vector3(-120f, 90f, 160f), new Vector3(90f, 0.5f, 90f), m_Style.AccentA, true);
                Cylinderize(ring);
                ring.transform.localRotation = Quaternion.Euler(20f, 0f, 15f);
            }
        }

        // ------------------------------------------------------------------ 구름 / 떠다니는 빛

        void BuildSky()
        {
            var pivot = new GameObject("Clouds").transform;
            pivot.SetParent(m_Animated, false);
            pivot.gameObject.AddComponent<ConstantRotation>().RotatingSpeed = 1.2f;

            for (int i = 0; i < 16; i++)
            {
                Vector3 c = Quaternion.Euler(0f, i * 22.5f + Range(-8f, 8f), 0f) * Vector3.forward * Range(30f, 110f) + Vector3.up * Range(42f, 60f);
                if (m_Theme == MapTheme.NeonSpace)
                {
                    Ball(pivot, c, Vector3.one * Range(1f, 2.5f), i % 2 == 0 ? m_Style.AccentA : m_Style.AccentB, glow: true);
                    continue;
                }

                Color cloud = m_Theme == MapTheme.Desert ? new Color(1f, 0.93f, 0.85f) : Color.white;
                int puffs = 3 + m_Rng.Next(3);
                for (int k = 0; k < puffs; k++)
                    Ball(pivot, c + new Vector3(Range(-7f, 7f), Range(-1f, 1.5f), Range(-4f, 4f)),
                        new Vector3(Range(8f, 14f), Range(3f, 5f), Range(6f, 10f)), cloud);
            }
        }

        // ------------------------------------------------------------------ 후처리

        void ApplyPostProcessing()
        {
            var go = new GameObject("MathMode PostFX");
            CreatedObjects.Add(go);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            // 모바일 기기는 성능을 위해 빛 번짐을 약하게
            float bloomScale = GameSettings.IsMobileDevice ? 0.5f : 1f;
            bloom.intensity.Override((m_Theme == MapTheme.NeonSpace ? 1.1f : 0.5f) * bloomScale);
            bloom.scatter.Override(0.65f);

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(14f);
            color.contrast.Override(8f);
            color.postExposure.Override(0.1f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            volume.sharedProfile = profile;

            Camera main = Camera.main;
            if (main) main.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        // ------------------------------------------------------------------ 도우미

        GameObject Ball(Transform parent, Vector3 localPos, Vector3 scale, Color color, bool glow = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            SetMaterial(go.GetComponent<Renderer>(), color, glow, 0.15f);
            go.GetComponent<Renderer>().shadowCastingMode = scale.y > 5f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        Vector2 Random2()
        {
            float a = (float)m_Rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt((float)m_Rng.NextDouble());
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
    }
}
