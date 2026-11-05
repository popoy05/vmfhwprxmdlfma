using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 중세 아레나 모험 (1~5분): 아래 들판에서 출발해 성 꼭대기 안마당의 보스를 쓰러뜨린다.
    //   1 몸풀기 → 2 문제 → 3 깡총이 무리 → 4 문제 → 5 거인 → 6 성문 앞 문제 → 7 보스 "성의 수호자"
    // 길: 미리 구운 내비메시(Resources/ArenaNavMesh)에서 출발점 → 보스까지 실제로 걸어갈 수 있는 길을 구해,
    //     그 길 위 일정 거리마다 단계를 놓는다. 몹·문제는 처음부터 길 위에 놓여 있다.
    // 길안내: 왼쪽 위 목표 창 · 목표 위 표시(화면 밖이면 가장자리 화살표) · 목표의 빛기둥 · 발밑부터 내비메시 길을 따라가는 빛길
    // 단계를 깰 때마다 그 자리가 체크포인트 (쓰러지면 거기서 다시). T: 허수아비 훈련장 (그동안 시간 멈춤)
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

        // 중세 아레나: 보스가 있는 성 안마당 (성벽 x 249~267, z −106~−87, 서쪽 성문)
        static readonly Vector3 k_BossPoint = new Vector3(257f, 21.7f, -95.5f);
        // 훈련장 (섬 서쪽 하늘)
        static readonly Vector3 k_TrainingCenter = new Vector3(120f, 70f, -80f);

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
        TMP_FontAsset m_Font;
        Canvas m_Canvas;
        TextMeshProUGUI m_StageTitle, m_Objective, m_Timer, m_Center, m_BossName;
        RectTransform m_Marker, m_MarkerArrow, m_BossBar;
        Image m_BossFill;
        TextMeshProUGUI m_MarkerText;
        RectTransform m_Panel;
        Image[] m_Pips;

        static readonly Color k_Gold = new Color(1f, 0.84f, 0.3f);

        void Start()
        {
            m_Player = GetComponent<NewModePlayer>();
            m_Combat = GetComponent<NewModeCombat>();
            m_Font = MathModeAssets.Load() ? MathModeAssets.Load().Font : null;
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
            BuildRoute();
            BuildUI();
            BuildGuide();
            Next();
        }

        void OnDestroy()
        {
            TrainingDummy.AnyDied -= OnAnyDied;
            if (m_HasNav) NavMesh.RemoveNavMeshData(m_Nav);
        }

        void OnAnyDied(TrainingDummy d)
        {
            if (d.Hostile) m_Kills++;
        }

        // ================================================================== 길 만들기

        void BuildRoute()
        {
            float s = S;
            Vector3 start = transform.position;
            List<Vector3> path = RoutePath(start, k_BossPoint);
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);

            var plan = new[]
            {
                (0.10f, StageKind.Fight, "몸풀기", "좌클릭과 Q·E·X로 허수아비를 쓰러뜨리세요", new[] { MobKind.Dummy, MobKind.Dummy, MobKind.Dummy }),
                (0.26f, StageKind.Quiz, "첫 번째 문제", "빛기둥 아래 받침대에서 F를 눌러 문제를 푸세요", (MobKind[])null),
                (0.42f, StageKind.Fight, "깡총이 무리", "뛰어다니는 깡총이들 — 빨갛게 깜빡이면 공격이 와요!", new[] { MobKind.Hopper, MobKind.Hopper, MobKind.Dummy, MobKind.Hopper }),
                (0.56f, StageKind.Quiz, "두 번째 문제", "정답을 맞히면 15초 동안 공격력 ×2", (MobKind[])null),
                (0.71f, StageKind.Fight, "언덕 위의 거인", "거인을 쓰러뜨리세요 — 궁극기(C)를 아끼지 마세요", new[] { MobKind.Brute, MobKind.Hopper, MobKind.Dummy }),
                (0.87f, StageKind.Quiz, "성문 앞 문제", "마지막 문제를 풀면 성문이 열려요", (MobKind[])null),
                (1.00f, StageKind.Boss, "성의 수호자", "성 꼭대기 안마당의 보스를 쓰러뜨리세요! 빨간 원은 피하고, 주황 고리는 점프!", new[] { MobKind.Boss }),
            };

            foreach (var p in plan)
            {
                Vector3 point = PointAlong(path, total * p.Item1);
                if (p.Item2 == StageKind.Quiz)
                {
                    // 문제 받침대는 길 옆으로 살짝 비켜 (길을 막지 않게)
                    Vector3 dir = DirectionAlong(path, total * p.Item1);
                    Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                    point = Snap(point + side * 1.4f * s, point);
                }
                var st = new Stage { Kind = p.Item2, Point = point, Title = p.Item3, Hint = p.Item4, Mobs = p.Item5 };
                m_Stages.Add(st);
                Spawn(st);
            }
        }

        // 출발 → 끝: 내비메시 길 (없으면 직선)
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

        // 내비메시 위 가장 가까운 곳 (같은 층에 없으면 fallback)
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
                    for (int i = 0; i < st.Mobs.Length; i++)
                    {
                        float a = i * Mathf.PI * 2f / st.Mobs.Length + 0.4f;
                        Vector3 p = st.Mobs.Length == 1 ? st.Point : Snap(st.Point + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.8f * S, st.Point);
                        Vector3 look = transform.position - p;
                        look.y = 0f;
                        var mob = TrainingDummy.Create(p, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity, S, st.Mobs[i]);
                        mob.Respawns = false;
                        mob.Hostile = true;
                        mob.MaxHealth = st.Mobs[i] switch
                        {
                            MobKind.Boss => 160000f,
                            MobKind.Brute => 30000f,
                            MobKind.Hopper => 6000f,
                            _ => 9000f,
                        };
                        mob.ResetHealth();
                        mob.Died += d => st.Alive.Remove(d);
                        st.Alive.Add(mob);
                        if (st.Mobs[i] == MobKind.Boss)
                        {
                            mob.DisplayName = "성의 수호자";
                            mob.gameObject.AddComponent<BossBrain>().Init(mob);
                            m_Boss = mob;
                        }
                    }
                    st.Total = st.Mobs.Length;
                    break;
                case StageKind.Quiz:
                    st.Station = QuizStation.Create(st.Point, S, Random.Range(0, 99999));
                    st.Station.KeepSolved = true;
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
            m_StageTitle.text = $"<color=#FFD866><b>{m_Index + 1} / {m_Stages.Count}</b></color>   <b>{st.Title}</b>";
            UIAnim.Punch(m_Panel, 0.08f, 0.3f);
            Announce(m_Index == 0 ? "<b>모험 시작!</b>\n<size=45%>성 꼭대기의 보스를 향해 — 빛길을 따라가세요</size>" : $"<b>{st.Title}</b>\n<size=45%>{st.Hint}</size>");
        }

        void Update()
        {
            if (m_Finished || !m_Player) return;
            Stage st = Current;
            if (st == null) return;

            // 컷신·훈련장에서는 길안내를 숨기고, 훈련장에 있는 동안은 시간도 멈춤
            bool hide = (m_Combat && m_Combat.InCutscene) || TrainingZone.Inside;
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, hide ? 0f : 1f, Time.unscaledDeltaTime * 6f);
            m_Path.enabled = !hide;
            if (!TrainingZone.Inside && !m_Player.Dead) m_Elapsed += Time.deltaTime;

            Vector3 flat = st.Point - transform.position;
            flat.y = 0f;
            string distText = $"{flat.magnitude / Mathf.Max(0.01f, S):0}m";

            switch (st.Kind)
            {
                case StageKind.Fight:
                    st.Alive.RemoveAll(d => !d);
                    int left = st.Alive.Count;
                    m_Objective.text = $"{st.Hint}\n<color=#FFD866>처치 {st.Total - left} / {st.Total}</color>   <size=85%><color=#FFFFFFAA>거리 {distText}</color></size>";
                    if (left == 0) Complete("전투 완료!");
                    break;
                case StageKind.Quiz:
                    m_Objective.text = $"{st.Hint}\n<size=85%><color=#FFFFFFAA>거리 {distText}</color></size>";
                    if (st.Station && st.Station.Solved)
                    {
                        m_Wrong += st.Station.Wrong;
                        Complete("정답!");
                    }
                    break;
                case StageKind.Boss:
                    st.Alive.RemoveAll(d => !d);
                    m_Objective.text = $"{st.Hint}\n<size=85%><color=#FFFFFFAA>거리 {distText}</color></size>";
                    if (st.Alive.Count == 0) Finish();
                    break;
            }
            if (m_Finished) return;

            m_Timer.text = $"{(int)(m_Elapsed / 60f):00}:{(int)(m_Elapsed % 60f):00}";
            UpdateBossBar();
            if (!hide) UpdateMarker(st);
            m_PathTimer -= Time.deltaTime;
            if (m_PathTimer <= 0f) { m_PathTimer = 0.2f; UpdatePath(st); }
        }

        void Complete(string message)
        {
            Stage st = Current;
            Fx.Play("Sparks explode yellow", st.Point + Vector3.up * 1.5f * S, Quaternion.identity, 0.7f * S, 0.2f, false);
            m_Player.SetCheckpoint(st.Point + Vector3.up * 0.05f * S, transform.eulerAngles.y);
            Announce($"<b>{message}</b>\n<size=45%>체크포인트 저장 · 다음 목표로!</size>");
            Next();
        }

        void UpdateBossBar()
        {
            bool show = m_Boss && m_Boss.Alive && (Current.Kind == StageKind.Boss || m_Boss.Engaged);
            m_BossBar.gameObject.SetActive(show);
            if (!show) return;
            m_BossFill.fillAmount = Mathf.MoveTowards(m_BossFill.fillAmount, m_Boss.Health / m_Boss.MaxHealth, Time.deltaTime * 0.8f);
            m_BossName.text = $"<b>{m_Boss.DisplayName}</b>   <size=80%>{Mathf.CeilToInt(Mathf.Max(0f, m_Boss.Health)):N0}</size>";
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

        // 발밑 → 목표: 내비메시 길을 따라가는 빛길 (앞은 진하고 멀수록 흐리게, 반짝임이 흘러감)
        void UpdatePath(Stage st)
        {
            Vector3 from = transform.position;
            List<Vector3> route = RoutePath(from, st.Point);
            float len = 0f;
            for (int i = 1; i < route.Count; i++) len += Vector3.Distance(route[i - 1], route[i]);
            if (len < 2f * S) { m_Path.positionCount = 0; return; }

            // 꺾이는 점 사이를 0.8 간격으로 채우고 땅에 붙임
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
                new[] { new GradientColorKey(k_Gold, 0f), new GradientColorKey(Color.white, phase), new GradientColorKey(k_Gold, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.05f), new GradientAlphaKey(0.55f, 0.6f), new GradientAlphaKey(0.1f, 1f) });
            m_Path.colorGradient = g;
        }

        // 목표 위 안내 표시: 화면 안이면 그 위에, 화면 밖이면 가장자리에 붙어 화살표가 방향을 가리킴
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

        // ================================================================== UI

        void BuildUI()
        {
            m_Canvas = UIUtil.Canvas(transform, "Adventure HUD", 8);
            m_Group = m_Canvas.gameObject.AddComponent<CanvasGroup>();
            m_Group.blocksRaycasts = false;

            // 왼쪽 위: 목표 창
            m_Panel = UIUtil.RoundedImage(m_Canvas.transform, "Quest", new Color(0.03f, 0.06f, 0.14f, 0.72f));
            UIUtil.Place(m_Panel, new Vector2(0f, 1f), new Vector2(290f, -150f), new Vector2(540f, 170f));
            var stripe = UIUtil.Image(m_Panel, "Stripe", k_Gold);
            stripe.anchorMin = new Vector2(0f, 0f); stripe.anchorMax = new Vector2(0f, 1f); stripe.pivot = new Vector2(0f, 0.5f);
            stripe.sizeDelta = new Vector2(6f, -20f);
            stripe.anchoredPosition = new Vector2(8f, 0f);
            m_StageTitle = UIUtil.Text(m_Panel, m_Font, "", 28f, TextAlignmentOptions.Left);
            UIUtil.Place(m_StageTitle.rectTransform, new Vector2(0f, 1f), new Vector2(275f, -30f), new Vector2(500f, 40f));
            m_Objective = UIUtil.Text(m_Panel, m_Font, "", 22f, TextAlignmentOptions.TopLeft);
            UIUtil.Place(m_Objective.rectTransform, new Vector2(0f, 1f), new Vector2(275f, -95f), new Vector2(500f, 80f));
            m_Objective.textWrappingMode = TextWrappingModes.Normal;
            m_Objective.enableAutoSizing = true;
            m_Objective.fontSizeMin = 14f;
            m_Objective.fontSizeMax = 22f;

            m_Pips = new Image[7];
            for (int i = 0; i < m_Pips.Length; i++)
            {
                var pip = UIUtil.RoundedImage(m_Panel, "Pip", new Color(1f, 1f, 1f, 0.2f), true);
                UIUtil.Place(pip, new Vector2(0f, 0f), new Vector2(34f + i * 26f, 18f), new Vector2(16f, 16f));
                m_Pips[i] = pip.GetComponent<Image>();
            }
            var warp = UIUtil.Text(m_Panel, m_Font, "<color=#9FD3FF>T</color> 훈련장", 18f, TextAlignmentOptions.Right);
            UIUtil.Place(warp.rectTransform, new Vector2(1f, 0f), new Vector2(-70f, 18f), new Vector2(120f, 26f));
            UIAnim.SlideIn(m_Panel, new Vector2(-400f, 0f), 0.2f, 0.5f);

            // 위 가운데: 시간
            m_Timer = UIUtil.Text(m_Canvas.transform, m_Font, "00:00", 34f, TextAlignmentOptions.Center);
            UIUtil.Place(m_Timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(200f, 50f));
            m_Timer.outlineWidth = 0.25f;
            m_Timer.outlineColor = new Color(0f, 0f, 0f, 0.8f);

            // 보스 체력 막대
            m_BossBar = UIUtil.RoundedImage(m_Canvas.transform, "Boss", new Color(0.05f, 0.02f, 0.05f, 0.85f));
            UIUtil.Place(m_BossBar, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 30f));
            m_BossFill = UIUtil.RoundedImage(m_BossBar, "Fill", new Color(0.85f, 0.15f, 0.2f)).GetComponent<Image>();
            UIUtil.Stretch(m_BossFill.rectTransform, 4f);
            m_BossFill.type = Image.Type.Filled;
            m_BossFill.fillMethod = Image.FillMethod.Horizontal;
            m_BossFill.fillAmount = 1f;
            m_BossName = UIUtil.Text(m_BossBar, m_Font, "", 26f, TextAlignmentOptions.Center);
            UIUtil.Place(m_BossName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 20f), new Vector2(900f, 36f));
            m_BossName.outlineWidth = 0.25f;
            m_BossName.outlineColor = new Color(0f, 0f, 0f, 0.9f);
            m_BossBar.gameObject.SetActive(false);

            // 가운데: 알림
            m_Center = UIUtil.Text(m_Canvas.transform, m_Font, "", 70f, TextAlignmentOptions.Center);
            UIUtil.Place(m_Center.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1500f, 200f));
            m_Center.outlineWidth = 0.22f;
            m_Center.outlineColor = new Color(0.25f, 0.15f, 0f);
            m_Center.color = k_Gold;
            m_Center.alpha = 0f;

            // 안내 표시
            m_Marker = new GameObject("Marker", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Marker.SetParent(m_Canvas.transform, false);
            m_Marker.anchorMin = m_Marker.anchorMax = new Vector2(0.5f, 0.5f);
            m_Marker.sizeDelta = new Vector2(120f, 120f);
            var diamond = UIUtil.RoundedImage(m_Marker, "Diamond", k_Gold);
            UIUtil.Place(diamond, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(34f, 34f));
            diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
            diamond.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            m_MarkerText = UIUtil.Text(m_Marker, m_Font, "", 22f, TextAlignmentOptions.Center);
            UIUtil.Place(m_MarkerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(160f, 60f));
            m_MarkerText.outlineWidth = 0.25f;
            m_MarkerText.outlineColor = new Color(0f, 0f, 0f, 0.85f);
            m_MarkerArrow = UIUtil.Image(m_Marker, "Arrow", k_Gold);
            m_MarkerArrow.sizeDelta = new Vector2(12f, 40f);
            m_MarkerArrow.pivot = new Vector2(0.5f, -1.2f);
            m_MarkerArrow.anchoredPosition = new Vector2(0f, 10f);
        }

        void RefreshPips()
        {
            for (int i = 0; i < m_Pips.Length && i < m_Stages.Count; i++)
            {
                Color c = i < m_Index ? k_Gold : i == m_Index ? Color.white : new Color(1f, 1f, 1f, 0.2f);
                if (i == m_Stages.Count - 1 && i != m_Index) c = i < m_Index ? k_Gold : new Color(1f, 0.35f, 0.3f, 0.5f); // 보스
                m_Pips[i].color = c;
                if (i == m_Index) UIAnim.Punch(m_Pips[i].transform, 0.6f, 0.4f);
            }
        }

        void Announce(string text)
        {
            m_Center.text = text;
            m_Center.DOKill();
            m_Center.rectTransform.DOKill();
            m_Center.alpha = 0f;
            m_Center.rectTransform.localScale = Vector3.one * 0.6f;
            DOTween.Sequence()
                .Append(m_Center.DOFade(1f, 0.2f))
                .Join(m_Center.rectTransform.DOScale(1f, 0.4f).SetEase(Ease.OutBack))
                .AppendInterval(1.8f)
                .Append(m_Center.DOFade(0f, 0.5f))
                .SetLink(m_Center.gameObject);
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
            float accuracy = m_Quizzes / (float)Mathf.Max(1, m_Quizzes + m_Wrong);
            int stars = 1 + (time < 300f ? 1 : 0) + (accuracy >= 0.99f && m_Deaths == 0 ? 1 : 0);

            var canvas = UIUtil.Canvas(transform, "Result", 40);
            var dim = UIUtil.Image(canvas.transform, "Dim", new Color(0.02f, 0.03f, 0.08f, 0.7f));
            UIUtil.Stretch(dim, 0f);
            UIAnim.FadeIn(UIAnim.Group(dim), 0.4f);
            var panel = UIUtil.RoundedImage(canvas.transform, "Panel", new Color(0.06f, 0.08f, 0.18f, 0.97f));
            UIUtil.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 760f));
            UIAnim.PopIn(panel, 0.1f, 0.45f);

            var title = UIUtil.Text(panel, m_Font, "<b>성의 수호자 처치!</b>", 76f, TextAlignmentOptions.Center);
            UIUtil.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(860f, 110f));
            title.color = k_Gold;

            var starText = UIUtil.Text(panel, m_Font, "", 90f, TextAlignmentOptions.Center);
            UIUtil.Place(starText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(860f, 110f));
            starText.text = $"<color=#FFD866>{new string('★', stars)}</color><color=#FFFFFF33>{new string('★', 3 - stars)}</color>";
            UIAnim.PopIn(starText.transform, 0.5f, 0.5f);

            WeaponCatalog.Info info = WeaponCatalog.All[WeaponCatalog.Selected];
            var stats = UIUtil.Text(panel, m_Font,
                $"직업   <b><color=#{ColorUtility.ToHtmlStringRGB(info.Color)}>{info.ClassName}</color></b>\n" +
                $"걸린 시간   <b>{(int)(time / 60f)}분 {(int)(time % 60f)}초</b>\n" +
                $"쓰러뜨린 적   <b>{m_Kills}</b>     쓰러진 횟수   <b>{m_Deaths}</b>\n" +
                $"수학 정답률   <b>{Mathf.RoundToInt(accuracy * 100f)}%</b>  <size=75%><color=#FFFFFFAA>(틀린 횟수 {m_Wrong})</color></size>",
                34f, TextAlignmentOptions.Center);
            UIUtil.Place(stats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(840f, 260f));
            stats.lineSpacing = 20f;

            var again = UIUtil.Button(panel, m_Font, "<b>다시 도전</b>", new Color(0.2f, 0.72f, 0.42f),
                () => SceneManager.LoadScene(SceneManager.GetActiveScene().name), new Vector2(260f, 90f), 34f);
            UIUtil.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(-280f, 80f), new Vector2(260f, 90f));
            var job = UIUtil.Button(panel, m_Font, "직업 다시 고르기", new Color(0.55f, 0.35f, 0.75f),
                () => SceneManager.LoadScene(AppBootstrap.NewModeCharacterScene), new Vector2(260f, 90f), 28f);
            UIUtil.Place((RectTransform)job.transform, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(260f, 90f));
            var menu = UIUtil.Button(panel, m_Font, "메뉴로", new Color(0.25f, 0.35f, 0.6f),
                () => SceneManager.LoadScene(AppBootstrap.MenuScene), new Vector2(260f, 90f), 30f);
            UIUtil.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(280f, 80f), new Vector2(260f, 90f));
        }
    }
}
