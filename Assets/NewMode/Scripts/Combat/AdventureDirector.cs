using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 중세 아레나 모험 (1~5분): 아래 들판에서 출발해 성 꼭대기 안마당의 보스를 쓰러뜨린다.
    //   1 몸풀기 → 2 문제 → 3 깡총이 무리 → 4 문제 → 5 거인 → 6 문제 → 7 성문 앞 문제 → 8 보스 "하늘 고래"
    //   문제 받침대 4곳 × 3문제 = 12문제 (난이도는 캐릭터 꾸미기에서 고름)
    // 길: 미리 구운 내비메시(Resources/ArenaNavMesh)에서 출발점 → 보스까지 실제로 걸어갈 수 있는 길을 구해 그 위에 단계를 놓는다.
    // 원신 풍 길안내: 왼쪽 위 둥근 미니맵(목표 ◆·적 ●) + 그 아래 임무 글씨 · 목표 위 표시 · 발밑부터 이어지는 빛길
    // 단계를 깰 때마다 체크포인트. T: 허수아비 훈련장 (그동안 시간 멈춤)
    public class AdventureDirector : MonoBehaviour
    {
        enum StageKind { Fight, Quiz, Boss }

        class Stage
        {
            public StageKind Kind;
            public Vector3 Point;
            public string Title, Hint;
            public MobKind[] Mobs;
            public readonly List<TrainingDummy> Alive = new List<TrainingDummy>();
            public int Total;
            public QuizStation Station;
        }

        static readonly Vector3 k_BossPoint = new Vector3(257f, 21.7f, -95.5f);       // 성 안마당
        static readonly Vector3 k_TrainingCenter = new Vector3(120f, 70f, -80f);       // 섬 서쪽 하늘

        NewModePlayer m_Player;
        NewModeCombat m_Combat;
        CanvasGroup m_Group;
        float S => m_Player ? m_Player.Scale : 1f;
        readonly List<Stage> m_Stages = new List<Stage>();
        int m_Index = -1;
        float m_Elapsed;
        int m_Kills, m_Wrong, m_Quizzes, m_Deaths;
        bool m_Finished;
        TrainingDummy m_Boss;
        NavMeshDataInstance m_Nav;
        bool m_HasNav;

        // 길안내
        GameObject m_Beacon;
        LineRenderer m_Path;
        float m_PathTimer;
        NavMeshPath m_NavPath;

        // UI
        Canvas m_Canvas;
        TextMeshProUGUI m_QuestTitle, m_Objective, m_Timer, m_BossName, m_MarkerText;
        RectTransform m_Marker, m_MarkerArrow, m_BossBar, m_Quest, m_BannerRoot;
        TextMeshProUGUI m_Banner;
        Image m_BossFill, m_BossLag;
        RectTransform[] m_Pips;

        // 미니맵
        Camera m_MapCam;
        RenderTexture m_MapTex;
        RectTransform m_MapRoot, m_MapGoal;
        readonly List<RectTransform> m_MapDots = new List<RectTransform>();
        const float k_MapSize = 220f, k_MapMeters = 16f;

        void Start()
        {
            m_Player = GetComponent<NewModePlayer>();
            m_Combat = GetComponent<NewModeCombat>();
            TrainingDummy.AnyDied += OnAnyDied;
            m_Player.Revived += () => m_Deaths++;

            var navData = Resources.Load<NavMeshData>("ArenaNavMesh");
            if (navData)
            {
                m_Nav = NavMesh.AddNavMeshData(navData);
                m_HasNav = true;
            }
            m_NavPath = new NavMeshPath();

            TrainingZone.Create(m_Player, k_TrainingCenter);
            DisableDecorColliders();
            BuildRoute();
            BuildUI();
            BuildMinimap();
            BuildGuide();
            Next();
        }

        void OnDestroy()
        {
            TrainingDummy.AnyDied -= OnAnyDied;
            if (m_HasNav) NavMesh.RemoveNavMeshData(m_Nav);
            if (m_MapTex) m_MapTex.Release();
        }

        // 풀·덤불에도 충돌체가 있어서 캐릭터가 그 위에 떠 있거나 몹·이펙트가 풀 꼭대기에 놓이던 문제 → 장식의 메시 충돌체는 끈다.
        // 대신 풀이 아닌 장식(덤불·검·깃발·방패)은 캐릭터가 뚫고 지나가지 않게 단순한 상자 충돌체를 둔다.
        //  상자는 Ignore Raycast 층 → 땅 높이 재기·몹·이펙트·카메라는 무시하고, 캐릭터(같은 층끼리 충돌)만 막는다.
        void DisableDecorColliders()
        {
            var props = GameObject.Find("Props");
            if (!props) return;
            string[] decor = { "Grass 1", "Bush_1", "Bush_2", "Bush_3", "Bush_4", "Sword_1", "Sztandar", "Archer Shield 1" };
            Transform blockers = null;
            foreach (Transform t in props.transform)
            {
                string name = System.Text.RegularExpressions.Regex.Replace(t.name, @"\s*\(\d+\)$", "");
                if (System.Array.IndexOf(decor, name) < 0) continue;
                foreach (var c in t.GetComponentsInChildren<Collider>()) c.enabled = false;
                if (name == "Grass 1") continue; // 풀은 그대로 지나갈 수 있게
                if (!blockers) blockers = new GameObject("Decor Blockers").transform;
                AddBlocker(t, blockers, S);
            }
        }

        /// <summary>장식 모양을 감싸는 보이지 않는 상자 (캐릭터만 막음)</summary>
        public static void AddBlocker(Transform target, Transform parent, float s)
        {
            Bounds b = default;
            bool any = false;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return;
            var go = new GameObject(target.name + " Block") { layer = 2 };
            go.transform.SetParent(parent, false);
            go.transform.position = b.center;
            var box = go.AddComponent<BoxCollider>();
            // 얇은 깃발·검도 확실히 막히게 최소 두께, 가장자리 잎사귀는 조금 덜 (85%)
            box.size = new Vector3(Mathf.Max(b.size.x * 0.85f, 0.25f * s), Mathf.Max(b.size.y, 0.4f * s), Mathf.Max(b.size.z * 0.85f, 0.25f * s));
        }

        // 진짜 땅 높이 (내비메시 꺾인 점 사이를 직선으로 이으면 볼록한 언덕 속으로 파묻히므로, 위에서 아래로 쏴서 잰다)
        Vector3 OnGround(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 4f * S, Vector3.down, out RaycastHit hit, 10f * S, ~(1 << 2), QueryTriggerInteraction.Ignore))
                p.y = hit.point.y;
            return p;
        }

        void OnAnyDied(TrainingDummy d)
        {
            if (d.Hostile) m_Kills++;
        }

        // ================================================================== 길 만들기

        void BuildRoute()
        {
            float s = S;
            List<Vector3> path = RoutePath(transform.position, k_BossPoint);
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);

            var plan = new[]
            {
                (0.09f, StageKind.Fight, "몸풀기", NewModeTouch.Enabled ? "공격 1·2·3단과 스킬로 유령들을 쓰러뜨리세요" : "좌클릭 1·2·3단과 Q·E·X로 유령들을 쓰러뜨리세요", new[] { MobKind.Dummy, MobKind.Dummy, MobKind.Dummy }),
                (0.21f, StageKind.Quiz, "들판의 문제", "빛기둥 아래 받침대에서 F — 3문제를 풀어요", (MobKind[])null),
                (0.36f, StageKind.Fight, "꼬마 유령 무리", "유령이 빨갛게 깜빡이면 공격이 와요 — 피하세요!", new[] { MobKind.Hopper, MobKind.Hopper, MobKind.Dummy, MobKind.Hopper }),
                (0.49f, StageKind.Quiz, "골짜기의 문제", "3문제를 모두 맞히면 공격력 ×2 + 스킬 재사용", (MobKind[])null),
                (0.66f, StageKind.Fight, "언덕 위의 붉은 유령", "붉은 유령을 쓰러뜨리세요 — 궁극기(C)를 아끼지 마세요", new[] { MobKind.Brute, MobKind.Hopper, MobKind.Dummy }),
                (0.76f, StageKind.Quiz, "언덕의 문제", "Tab으로 스킬과 능력치를 확인할 수 있어요", (MobKind[])null),
                (0.88f, StageKind.Quiz, "성문 앞 문제", "마지막 문제를 풀면 성문이 열려요", (MobKind[])null),
                (1.00f, StageKind.Boss, "하늘 고래", "보스 하늘 고래! 빨간 원·길은 피하고, 주황 고리는 점프! 파란 빛이면 공격이 안 들어가요", new[] { MobKind.Boss }),
            };

            foreach (var p in plan)
            {
                Vector3 point = OnGround(PointAlong(path, total * p.Item1));
                if (p.Item2 == StageKind.Quiz)
                {
                    // 문제 받침대는 길 옆으로 살짝 비켜 (길을 막지 않게)
                    Vector3 dir = DirectionAlong(path, total * p.Item1);
                    Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                    point = OnGround(Snap(point + side * 1.4f * s, point));
                }
                var st = new Stage { Kind = p.Item2, Point = point, Title = p.Item3, Hint = p.Item4, Mobs = p.Item5 };
                m_Stages.Add(st);
                Spawn(st);
            }
        }

        List<Vector3> RoutePath(Vector3 from, Vector3 to)
        {
            var list = new List<Vector3>();
            if (m_HasNav && NavMesh.SamplePosition(from, out NavMeshHit a, 3f * S, NavMesh.AllAreas)
                         && NavMesh.SamplePosition(to, out NavMeshHit b, 3f * S, NavMesh.AllAreas)
                         && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, m_NavPath)
                         && m_NavPath.status == NavMeshPathStatus.PathComplete)
            {
                list.AddRange(m_NavPath.corners);
                return list;
            }
            list.Add(from);
            list.Add(to);
            return list;
        }

        static Vector3 PointAlong(List<Vector3> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (distance <= seg) return Vector3.Lerp(path[i - 1], path[i], seg > 0f ? distance / seg : 0f);
                distance -= seg;
            }
            return path[path.Count - 1];
        }

        static Vector3 DirectionAlong(List<Vector3> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (distance <= seg || i == path.Count - 1)
                {
                    Vector3 d = path[i] - path[i - 1];
                    d.y = 0f;
                    return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
                }
                distance -= seg;
            }
            return Vector3.forward;
        }

        Vector3 Snap(Vector3 p, Vector3 fallback)
        {
            if (m_HasNav && NavMesh.SamplePosition(p, out NavMeshHit hit, 1.2f * S, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - fallback.y) < 0.6f * S)
                return hit.position;
            return fallback;
        }

        void Spawn(Stage st)
        {
            switch (st.Kind)
            {
                case StageKind.Fight:
                case StageKind.Boss:
                    // 어려움: 일반 전투마다 유령 둘이 더
                    if (st.Kind == StageKind.Fight && CombatDifficulty.Hard)
                    {
                        var more = new List<MobKind>(st.Mobs) { MobKind.Hopper, MobKind.Dummy };
                        st.Mobs = more.ToArray();
                    }
                    for (int i = 0; i < st.Mobs.Length; i++)
                    {
                        float a = i * Mathf.PI * 2f / st.Mobs.Length + 0.4f;
                        Vector3 p = st.Mobs.Length == 1 ? st.Point : Snap(st.Point + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.8f * S, st.Point);
                        p = OnGround(p);
                        Vector3 look = transform.position - p;
                        look.y = 0f;
                        var mob = TrainingDummy.Create(p, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity, S, st.Mobs[i]);
                        mob.Respawns = false;
                        mob.Hostile = true;
                        mob.MaxHealth = st.Mobs[i] switch
                        {
                            MobKind.Boss => 220000f, // 하늘 고래 (패턴이 많고 단단함)
                            MobKind.Brute => 28000f,
                            MobKind.Hopper => 6000f,
                            _ => 9000f,
                        };
                        mob.ResetHealth();
                        mob.Died += d => st.Alive.Remove(d);
                        st.Alive.Add(mob);
                        if (st.Mobs[i] == MobKind.Boss)
                        {
                            mob.DisplayName = CombatDifficulty.Hard ? "하늘 고래 <color=#FF6A5A>(어려움)</color>" : "하늘 고래";
                            mob.gameObject.AddComponent<BossBrain>().Init(mob);
                            m_Boss = mob;
                        }
                    }
                    st.Total = st.Mobs.Length;
                    break;
                case StageKind.Quiz:
                    st.Station = QuizStation.Create(st.Point, S, Random.Range(0, 99999));
                    st.Station.KeepSolved = true;
                    st.Station.RefreshLabel();
                    m_Quizzes++;
                    break;
            }
        }

        // ================================================================== 단계 진행

        Stage Current => m_Index >= 0 && m_Index < m_Stages.Count ? m_Stages[m_Index] : null;

        void Next()
        {
            m_Index++;
            Stage st = Current;
            if (st == null) return;

            if (m_Beacon) Destroy(m_Beacon);
            m_Beacon = new GameObject("Objective Beacon");
            m_Beacon.transform.position = st.Point;
            QuizStation.Persistent("FX_LightPillar", m_Beacon.transform, Vector3.zero, (st.Kind == StageKind.Boss ? 1.4f : 1f) * S);

            RefreshPips();
            m_QuestTitle.text = $"<b>{st.Title}</b>  <size=75%><color={Gs.Hex(Gs.Cream)}>{m_Index + 1} / {m_Stages.Count}</color></size>";
            UIAnim.Punch(m_Quest, 0.06f, 0.3f);
            Announce(m_Index == 0 ? "<b>모험 시작</b>\n<size=45%>성 꼭대기의 보스를 향해 — 빛길을 따라가세요</size>" : $"<b>{st.Title}</b>\n<size=45%>{st.Hint}</size>");
        }

        void Update()
        {
            if (m_Finished || !m_Player) return;
            Stage st = Current;
            if (st == null) return;

            bool hide = (m_Combat && m_Combat.InCutscene) || TrainingZone.Inside || CharacterSheet.IsOpen;
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, hide ? 0f : 1f, Time.unscaledDeltaTime * 6f);
            m_Path.enabled = !hide;
            if (!TrainingZone.Inside && !m_Player.Dead) m_Elapsed += Time.deltaTime;

            Vector3 flat = st.Point - transform.position;
            flat.y = 0f;
            string distText = $"<size=85%><color=#FFFFFFAA>{flat.magnitude / Mathf.Max(0.01f, S):0}m</color></size>";

            switch (st.Kind)
            {
                case StageKind.Fight:
                    st.Alive.RemoveAll(d => !d);
                    int left = st.Alive.Count;
                    m_Objective.text = $"{st.Hint}\n<color={Gs.Hex(Gs.GoldBright)}>처치 {st.Total - left} / {st.Total}</color>   {distText}";
                    if (left == 0) Complete("전투 완료");
                    break;
                case StageKind.Quiz:
                    m_Objective.text = st.Station
                        ? $"{st.Hint}\n<color={Gs.Hex(Gs.GoldBright)}>문제 {st.Station.Progress} / {st.Station.Problems.Length}</color>   {distText}"
                        : st.Hint;
                    if (st.Station && st.Station.Solved)
                    {
                        m_Wrong += st.Station.Wrong;
                        Complete("문제 해결");
                    }
                    break;
                case StageKind.Boss:
                    st.Alive.RemoveAll(d => !d);
                    m_Objective.text = $"{st.Hint}\n{distText}";
                    if (st.Alive.Count == 0) Finish();
                    break;
            }
            if (m_Finished) return;

            m_Timer.text = $"{(int)(m_Elapsed / 60f):00}:{(int)(m_Elapsed % 60f):00}";
            UpdateBossBar();
            if (!hide) UpdateMarker(st);
            UpdateMinimap(st);
            m_PathTimer -= Time.deltaTime;
            if (m_PathTimer <= 0f) { m_PathTimer = 0.2f; UpdatePath(st); }
        }

        void Complete(string message)
        {
            Stage st = Current;
            Fx.Play("Sparks explode yellow", st.Point + Vector3.up * 1.5f * S, Quaternion.identity, 0.7f * S, 0.2f, false);
            m_Player.SetCheckpoint(st.Point + Vector3.up * 0.05f * S, transform.eulerAngles.y);
            Announce($"<b>{message}</b>\n<size=45%>체크포인트 저장 · 다음 목표로</size>");
            Next();
        }

        void UpdateBossBar()
        {
            bool show = m_Boss && m_Boss.Alive && (Current.Kind == StageKind.Boss || m_Boss.Engaged);
            if (m_BossBar.gameObject.activeSelf != show)
            {
                m_BossBar.gameObject.SetActive(show);
                if (show) UIAnim.FadeIn(UIAnim.Group(m_BossBar), 0.4f);
            }
            if (!show) return;
            float f = m_Boss.Health / m_Boss.MaxHealth;
            m_BossFill.fillAmount = f;
            m_BossLag.fillAmount = Mathf.MoveTowards(Mathf.Max(m_BossLag.fillAmount, f), f, Time.deltaTime * 0.35f);
            m_BossName.text = $"<b>{m_Boss.DisplayName}</b>   <size=75%><color={Gs.Hex(Gs.Gold)}>{Mathf.CeilToInt(Mathf.Max(0f, m_Boss.Health)):N0}</color></size>";
        }

        // ================================================================== 길안내

        void BuildGuide()
        {
            var go = new GameObject("Guide Path");
            m_Path = go.AddComponent<LineRenderer>();
            m_Path.sharedMaterial = GeoFx.LineMaterial;
            m_Path.widthMultiplier = 0.12f * S;
            m_Path.numCapVertices = 2;
            m_Path.numCornerVertices = 2;
            m_Path.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Path.receiveShadows = false;
        }

        void UpdatePath(Stage st)
        {
            List<Vector3> route = RoutePath(transform.position, st.Point);
            float len = 0f;
            for (int i = 1; i < route.Count; i++) len += Vector3.Distance(route[i - 1], route[i]);
            if (len < 2f * S) { m_Path.positionCount = 0; return; }

            var pts = new List<Vector3>();
            for (int i = 1; i < route.Count && pts.Count < 80; i++)
            {
                Vector3 a = route[i - 1], b = route[i];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / (0.8f * S)));
                for (int k = 0; k < n && pts.Count < 80; k++)
                {
                    Vector3 p = Vector3.Lerp(a, b, k / (float)n);
                    if (m_HasNav && NavMesh.SamplePosition(p, out NavMeshHit h, 0.6f * S, NavMesh.AllAreas)) p = h.position;
                    pts.Add(p + Vector3.up * 0.08f * S);
                }
            }
            pts.Add(route[route.Count - 1] + Vector3.up * 0.08f * S);
            m_Path.positionCount = pts.Count;
            m_Path.SetPositions(pts.ToArray());

            float phase = Mathf.Repeat(Time.time * 0.8f, 1f);
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Gs.GoldBright, 0f), new GradientColorKey(Color.white, phase), new GradientColorKey(Gs.GoldBright, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.05f), new GradientAlphaKey(0.55f, 0.6f), new GradientAlphaKey(0.1f, 1f) });
            m_Path.colorGradient = g;
        }

        void UpdateMarker(Stage st)
        {
            Camera cam = m_Player.Camera;
            if (!cam) return;
            Vector3 world = st.Point + Vector3.up * (st.Kind == StageKind.Boss ? 5f : 2.6f) * S;
            Vector3 sp = cam.WorldToScreenPoint(world);
            var canvasRect = (RectTransform)m_Canvas.transform;
            float scale = canvasRect.rect.width / Screen.width;
            Vector2 half = canvasRect.rect.size * 0.5f;
            Vector2 pos = new Vector2(sp.x * scale, sp.y * scale) - half;
            bool behind = sp.z < 0f;
            if (behind) pos = -pos;
            Vector2 margin = half - new Vector2(70f, 90f);
            bool off = behind || Mathf.Abs(pos.x) > margin.x || Mathf.Abs(pos.y) > margin.y;
            if (off)
            {
                Vector2 d = pos.sqrMagnitude > 0.01f ? pos.normalized : Vector2.up;
                float k = Mathf.Min(margin.x / Mathf.Max(0.001f, Mathf.Abs(d.x)), margin.y / Mathf.Max(0.001f, Mathf.Abs(d.y)));
                pos = d * k;
                m_MarkerArrow.gameObject.SetActive(true);
                m_MarkerArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
            }
            else m_MarkerArrow.gameObject.SetActive(false);
            m_Marker.anchoredPosition = pos + new Vector2(0f, Mathf.Sin(Time.time * 4f) * 6f);

            Vector3 flat = st.Point - transform.position;
            flat.y = 0f;
            string icon = st.Kind == StageKind.Fight ? "전투" : st.Kind == StageKind.Quiz ? "문제" : "보스";
            m_MarkerText.text = $"<b>{icon}</b>\n<size=80%>{flat.magnitude / Mathf.Max(0.01f, S):0}m</size>";
        }

        // ================================================================== 미니맵 (원신처럼 왼쪽 위 둥근 지도)

        void BuildMinimap()
        {
            m_MapTex = new RenderTexture(256, 256, 16) { name = "Minimap" };
            var camGo = new GameObject("Minimap Camera");
            m_MapCam = camGo.AddComponent<Camera>();
            m_MapCam.orthographic = true;
            m_MapCam.orthographicSize = k_MapMeters * S;
            m_MapCam.targetTexture = m_MapTex;
            m_MapCam.clearFlags = CameraClearFlags.SolidColor;
            m_MapCam.backgroundColor = new Color(0.12f, 0.16f, 0.24f);
            m_MapCam.cullingMask = ~((1 << 2) | (1 << 5)); // 플레이어·몹·UI는 빼고 지형만 (몹은 점으로 따로 그림)
            m_MapCam.nearClipPlane = 0.1f;
            m_MapCam.farClipPlane = 200f;
            m_MapCam.depth = -10f; // 본 화면보다 먼저, 텍스처로만 그림
            var data = m_MapCam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;

            m_MapRoot = UIUtil.RoundedImage(m_Canvas.transform, "Minimap", new Color(0f, 0f, 0f, 1f), true);
            // 휴대폰: 왼쪽 위 모서리에 일시정지·정보 버튼이 있으니 지도를 조금 비켜 놓는다
            Vector2 mapAt = NewModeTouch.Enabled ? new Vector2(235f, -205f) : new Vector2(150f, -150f);
            UIUtil.Place(m_MapRoot, new Vector2(0f, 1f), mapAt, new Vector2(k_MapSize, k_MapSize));
            m_MapRoot.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var raw = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            raw.transform.SetParent(m_MapRoot, false);
            UIUtil.Stretch(raw.rectTransform, 0f);
            raw.texture = m_MapTex;
            raw.color = new Color(1f, 1f, 1f, 0.9f);
            raw.raycastTarget = false;

            var ring = UIUtil.Image(m_Canvas.transform, "MapRing", Gs.Gold);
            ring.GetComponent<Image>().sprite = Gs.Ring;
            UIUtil.Place(ring, new Vector2(0f, 1f), mapAt, new Vector2(k_MapSize + 14f, k_MapSize + 14f));

            // 가운데: 나 (흰 화살촉)
            var me = Gs.Diamond(m_MapRoot, Color.white, 16f);
            me.anchorMin = me.anchorMax = new Vector2(0.5f, 0.5f);
            me.anchoredPosition = Vector2.zero;
            me.localScale = new Vector3(0.7f, 1.3f, 1f);
            me.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);

            m_MapGoal = Gs.Diamond(m_MapRoot, Gs.GoldBright, 18f);
            m_MapGoal.anchorMin = m_MapGoal.anchorMax = new Vector2(0.5f, 0.5f);
            m_MapGoal.gameObject.AddComponent<Outline>().effectColor = new Color(0.3f, 0.2f, 0f, 0.9f);

            var north = Gs.Text(m_Canvas.transform, "<b>중세 아레나</b>", 18f, TextAlignmentOptions.Center, Gs.Cream, true);
            UIUtil.Place(north.rectTransform, new Vector2(0f, 1f), mapAt + new Vector2(0f, -125f), new Vector2(220f, 26f));
        }

        void UpdateMinimap(Stage st)
        {
            if (!m_MapCam) return;
            // 지도는 카메라가 보는 방향이 위 (돌아가는 지도)
            float yaw = m_Player.Yaw;
            m_MapCam.transform.SetPositionAndRotation(transform.position + Vector3.up * 40f * S, Quaternion.Euler(90f, yaw, 0f));

            float pxPerM = (k_MapSize * 0.5f) / (k_MapMeters * S);
            Vector2 ToMap(Vector3 world)
            {
                Vector3 d = world - transform.position;
                Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * d;
                return new Vector2(local.x, local.z) * pxPerM;
            }

            // 목표 ◆ (밖이면 가장자리에)
            Vector2 g = ToMap(st.Point);
            float edge = k_MapSize * 0.5f - 14f;
            if (g.magnitude > edge) g = g.normalized * edge;
            m_MapGoal.anchoredPosition = g;
            m_MapGoal.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f));

            // 적 ● (가까운 것만, 빨강 / 보스는 크게)
            int n = 0;
            foreach (var d in TrainingDummy.All)
            {
                if (!d.Alive || !d.Hostile) continue;
                Vector2 p = ToMap(d.transform.position);
                if (p.magnitude > edge) continue;
                if (n >= m_MapDots.Count)
                {
                    var dot = UIUtil.RoundedImage(m_MapRoot, "Enemy", new Color(1f, 0.35f, 0.3f), true);
                    dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                    m_MapDots.Add(dot);
                }
                var r = m_MapDots[n++];
                r.gameObject.SetActive(true);
                r.anchoredPosition = p;
                r.sizeDelta = Vector2.one * (d.Kind == MobKind.Boss ? 16f : d.Kind == MobKind.Brute ? 11f : 8f);
            }
            for (int i = n; i < m_MapDots.Count; i++) m_MapDots[i].gameObject.SetActive(false);
            m_MapGoal.SetAsLastSibling();
        }

        // ================================================================== UI

        void BuildUI()
        {
            m_Canvas = UIUtil.Canvas(transform, "Adventure HUD", 8);
            m_Group = m_Canvas.gameObject.AddComponent<CanvasGroup>();
            m_Group.blocksRaycasts = false;

            // 미니맵 아래: 임무 (원신의 임무 추적처럼 판 없이 금색 제목 + 흰 설명)
            m_Quest = new GameObject("Quest", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Quest.SetParent(m_Canvas.transform, false);
            m_Quest.anchorMin = m_Quest.anchorMax = new Vector2(0f, 1f);
            m_Quest.pivot = new Vector2(0f, 1f);
            m_Quest.anchoredPosition = new Vector2(36f, -300f);
            m_Quest.sizeDelta = new Vector2(520f, 160f);
            var shade = UIUtil.Image(m_Quest, "Shade", new Color(0.05f, 0.06f, 0.1f, 0.35f));
            UIUtil.Stretch(shade, 0f);
            var icon = Gs.Diamond(m_Quest, Gs.GoldBright, 16f);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 1f);
            icon.anchoredPosition = new Vector2(16f, -22f);
            m_QuestTitle = Gs.Text(m_Quest, "", 28f, TextAlignmentOptions.Left, Gs.GoldBright, true);
            m_QuestTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            m_QuestTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            m_QuestTitle.rectTransform.pivot = new Vector2(0f, 1f);
            m_QuestTitle.rectTransform.sizeDelta = new Vector2(-40f, 40f);
            m_QuestTitle.rectTransform.anchoredPosition = new Vector2(36f, -4f);
            m_Objective = Gs.Text(m_Quest, "", 22f, TextAlignmentOptions.TopLeft, Color.white, true);
            m_Objective.rectTransform.anchorMin = new Vector2(0f, 0f);
            m_Objective.rectTransform.anchorMax = new Vector2(1f, 1f);
            m_Objective.rectTransform.offsetMin = new Vector2(36f, 30f);
            m_Objective.rectTransform.offsetMax = new Vector2(-10f, -46f);
            m_Objective.enableAutoSizing = true;
            m_Objective.fontSizeMin = 14f;
            m_Objective.fontSizeMax = 22f;

            m_Pips = new RectTransform[m_Stages.Count];
            for (int i = 0; i < m_Pips.Length; i++)
            {
                var pip = Gs.Diamond(m_Quest, new Color(1f, 1f, 1f, 0.25f), 11f);
                pip.anchorMin = pip.anchorMax = new Vector2(0f, 0f);
                pip.anchoredPosition = new Vector2(42f + i * 24f, 14f);
                m_Pips[i] = pip;
            }
            UIAnim.SlideIn(m_Quest, new Vector2(-400f, 0f), 0.2f, 0.5f);

            // 오른쪽 위: 시간
            m_Timer = Gs.Text(m_Canvas.transform, "00:00", 26f, TextAlignmentOptions.Right, Gs.Cream, true);
            UIUtil.Place(m_Timer.rectTransform, new Vector2(1f, 1f), new Vector2(-90f, -26f), new Vector2(160f, 36f));

            // 보스 체력 (원신처럼 위 가운데 얇은 막대 + 이름)
            m_BossBar = UIUtil.RoundedImage(m_Canvas.transform, "Boss", new Color(0.05f, 0.05f, 0.08f, 0.85f));
            m_BossBar.GetComponent<Image>().sprite = Gs.Pill;
            UIUtil.Place(m_BossBar, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(820f, 20f));
            m_BossBar.gameObject.AddComponent<Outline>().effectColor = new Color(Gs.Gold.r, Gs.Gold.g, Gs.Gold.b, 0.7f);
            m_BossLag = UIUtil.RoundedImage(m_BossBar, "Lag", new Color(1f, 0.85f, 0.7f, 0.8f)).GetComponent<Image>();
            UIUtil.Stretch(m_BossLag.rectTransform, 3f);
            m_BossLag.sprite = Gs.Pill;
            m_BossLag.type = Image.Type.Filled;
            m_BossLag.fillMethod = Image.FillMethod.Horizontal;
            m_BossFill = UIUtil.RoundedImage(m_BossBar, "Fill", new Color32(220, 70, 70, 255)).GetComponent<Image>();
            UIUtil.Stretch(m_BossFill.rectTransform, 3f);
            m_BossFill.sprite = Gs.Pill;
            m_BossFill.type = Image.Type.Filled;
            m_BossFill.fillMethod = Image.FillMethod.Horizontal;
            m_BossName = Gs.Text(m_BossBar, "", 26f, TextAlignmentOptions.Center, Color.white, true);
            UIUtil.Place(m_BossName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 22f), new Vector2(820f, 34f));
            m_BossBar.gameObject.SetActive(false);

            // 가운데: 알림 띠
            m_BannerRoot = Gs.Banner(m_Canvas.transform, out m_Banner, 250f, 130f);
            m_BannerRoot.GetComponent<CanvasGroup>().alpha = 0f;
            m_Banner.color = Gs.GoldBright;

            // 목표 위 표시
            m_Marker = new GameObject("Marker", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Marker.SetParent(m_Canvas.transform, false);
            m_Marker.anchorMin = m_Marker.anchorMax = new Vector2(0.5f, 0.5f);
            m_Marker.sizeDelta = new Vector2(120f, 120f);
            var diamond = Gs.Diamond(m_Marker, Gs.GoldBright, 26f);
            diamond.anchorMin = diamond.anchorMax = new Vector2(0.5f, 0.5f);
            diamond.anchoredPosition = new Vector2(0f, 10f);
            diamond.gameObject.AddComponent<Outline>().effectColor = new Color(0.25f, 0.18f, 0f, 0.8f);
            m_MarkerText = Gs.Text(m_Marker, "", 22f, TextAlignmentOptions.Center, Color.white, true);
            UIUtil.Place(m_MarkerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(160f, 60f));
            m_MarkerArrow = UIUtil.Image(m_Marker, "Arrow", Gs.GoldBright);
            m_MarkerArrow.sizeDelta = new Vector2(10f, 34f);
            m_MarkerArrow.pivot = new Vector2(0.5f, -1.3f);
            m_MarkerArrow.anchoredPosition = new Vector2(0f, 10f);
        }

        void RefreshPips()
        {
            for (int i = 0; i < m_Pips.Length; i++)
            {
                Color c = i < m_Index ? Gs.GoldBright : i == m_Index ? Color.white : new Color(1f, 1f, 1f, 0.25f);
                if (i == m_Pips.Length - 1 && i > m_Index) c = new Color(1f, 0.4f, 0.35f, 0.7f); // 보스
                m_Pips[i].GetComponent<Image>().color = c;
                if (i == m_Index) UIAnim.Punch(m_Pips[i], 0.6f, 0.4f);
            }
        }

        void Announce(string text)
        {
            m_Banner.text = text;
            Gs.Show(m_BannerRoot, 1.8f);
        }

        // ================================================================== 결과

        void Finish()
        {
            m_Finished = true;
            if (m_Beacon) Destroy(m_Beacon);
            m_Path.positionCount = 0;
            m_Marker.gameObject.SetActive(false);
            m_BossBar.gameObject.SetActive(false);
            Fx.Play("Sparks explode yellow", transform.position + Vector3.up * 1.2f * S, Quaternion.identity, 1f * S, 0.3f, false);
            m_Player.InputBlocked = true;
            m_Player.Invulnerable = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            float time = m_Elapsed;
            int problems = m_Quizzes * 3;
            float accuracy = problems / (float)Mathf.Max(1, problems + m_Wrong);
            int stars = 1 + (time < 300f ? 1 : 0) + (accuracy >= 0.85f && m_Deaths == 0 ? 1 : 0);

            var canvas = UIUtil.Canvas(transform, "Result", 40);
            UIAnim.FadeIn(UIAnim.Group(Gs.Dim(canvas.transform, 0.6f)), 0.4f);
            var panel = Gs.Panel(canvas.transform, "Panel", false);
            UIUtil.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 780f));
            UIAnim.PopIn(panel, 0.1f, 0.45f);

            Gs.Header(panel, "하늘 고래 처치!", 60f, new Vector2(0f, -70f), 820f, Gs.Ink);
            var starText = Gs.Text(panel, "", 84f, TextAlignmentOptions.Center, Gs.Gold);
            UIUtil.Place(starText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(860f, 100f));
            starText.text = $"<color={Gs.Hex(new Color32(230, 170, 60, 255))}>{new string('★', stars)}</color><color=#00000022>{new string('★', 3 - stars)}</color>";
            UIAnim.PopIn(starText.transform, 0.5f, 0.5f);

            WeaponCatalog.Info info = WeaponCatalog.Current;
            string[,] rows =
            {
                { "직업", $"<color={Gs.Hex(Color.Lerp(info.Color, Gs.Ink, 0.45f))}>{info.ClassName}</color>" },
                { "걸린 시간", $"{(int)(time / 60f)}분 {(int)(time % 60f)}초" },
                { "쓰러뜨린 적", $"{m_Kills}" },
                { "쓰러진 횟수", $"{m_Deaths}" },
                { "수학 정답률", $"{Mathf.RoundToInt(accuracy * 100f)}%  <size=75%><color={Gs.Hex(Gs.InkSoft)}>({problems}문제 · 난이도 {MathProblem.LevelNames[MathProblem.Level]} · 틀린 횟수 {m_Wrong})</color></size>" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                var row = UIUtil.Image(panel, "Row", i % 2 == 0 ? new Color(0f, 0f, 0f, 0.04f) : new Color(0f, 0f, 0f, 0f));
                UIUtil.Place(row, new Vector2(0.5f, 1f), new Vector2(0f, -280f - i * 58f), new Vector2(780f, 54f));
                var l = Gs.Text(row, rows[i, 0], 28f, TextAlignmentOptions.Left, Gs.InkSoft);
                UIUtil.Stretch(l.rectTransform, 0f);
                l.rectTransform.offsetMin = new Vector2(24f, 0f);
                var v = Gs.Text(row, $"<b>{rows[i, 1]}</b>", 28f, TextAlignmentOptions.Right, Gs.Ink);
                UIUtil.Stretch(v.rectTransform, 0f);
                v.rectTransform.offsetMax = new Vector2(-24f, 0f);
            }

            var again = Gs.Button(panel, "<b>다시 도전</b>", new Color(0.35f, 0.75f, 0.5f),
                () => SceneManager.LoadScene(SceneManager.GetActiveScene().name), new Vector2(270f, 86f), 32f, true);
            UIUtil.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(-290f, 70f), new Vector2(270f, 86f));
            var job = Gs.Button(panel, "직업 다시 고르기", new Color(0.7f, 0.5f, 0.9f),
                () => SceneManager.LoadScene(AppBootstrap.NewModeCharacterScene), new Vector2(270f, 86f), 26f);
            UIUtil.Place((RectTransform)job.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(270f, 86f));
            var menu = Gs.Button(panel, "메인 메뉴", Gs.InkSoft,
                () => SceneManager.LoadScene(AppBootstrap.NewModeCharacterScene), new Vector2(270f, 86f), 30f);
            UIUtil.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(290f, 70f), new Vector2(270f, 86f));
        }
    }
}
