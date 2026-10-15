using System.Collections.Generic;
using TMPro;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Unity.FPS.MathMode
{
    // 화면 오른쪽 위: 내 좌표 P(x, y), 사분면, |OP|, 방향각, 무기 탄도 수식, 좌표 탐색 안내
    // 그 아래: 좌표평면 미니맵 (플레이어 중심으로 따라 움직임). [M]으로 맵 전체 보기.
    // 아래 가운데: 스킬 바, 무기 교체 설명.
    public class CoordinatePlaneHUD : MonoBehaviour
    {
        const float k_ContentSize = 800f;   // 지도 내부 좌표계 크기 (px)
        const float k_MiniSize = 330f;
        const float k_BigSize = 900f;
        const float k_MiniRange = 10f;      // 미니맵에 보이는 반경 (칸)

        MathModeManager m_Manager;
        EnemyManager m_EnemyManager;
        TMP_FontAsset m_Font;
        TextMeshProUGUI m_InfoText;
        RectTransform m_MapPanel;
        RectTransform m_Content;
        TextMeshProUGUI m_MapTitle;
        Rect m_View;                        // 지도 전체의 좌표평면 범위
        bool m_Big;

        RectTransform m_PlayerDot;
        RectTransform m_PlayerHeading;
        RectTransform m_HuntMarker;
        readonly List<(RectTransform dot, TextMeshProUGUI label)> m_EnemyDots = new List<(RectTransform, TextMeshProUGUI)>();
        readonly List<(MathStation station, Image dot, TextMeshProUGUI label)> m_StationDots = new List<(MathStation, Image, TextMeshProUGUI)>();

        TextMeshProUGUI m_NextObjective;
        CanvasGroup m_WeaponBanner;
        TextMeshProUGUI m_WeaponBannerText;
        float m_BannerHideTime;

        public void Init(MathModeManager manager)
        {
            m_Manager = manager;
            m_Font = manager.Font;
            m_EnemyManager = FindAnyObjectByType<EnemyManager>();

            Canvas canvas = UIUtil.Canvas(transform, "MathHUD", 5);
            BuildInfoPanel(canvas.transform);
            BuildMap(canvas.transform);
            BuildWeaponBanner(canvas.transform);
            BuildNextObjective(canvas.transform);
            if (manager.Skills && !Unity.FPS.Gameplay.VirtualInput.Active) // 모바일은 화면 버튼에 쿨타임이 표시된다
                gameObject.AddComponent<MathSkillBar>().Init(manager.Skills, manager.Ultimate, canvas.transform, m_Font);
            if (manager.PlayerWeapons)
            {
                manager.PlayerWeapons.OnSwitchedToWeapon += ShowWeaponBanner;
                ShowWeaponBanner(manager.PlayerWeapons.GetActiveWeapon());
            }

            SetBig(false);
        }

        void OnDestroy()
        {
            if (m_Manager && m_Manager.PlayerWeapons)
                m_Manager.PlayerWeapons.OnSwitchedToWeapon -= ShowWeaponBanner;
        }

        // ------------------------------------------------------------------ 구성

        void BuildInfoPanel(Transform canvas)
        {
            var panel = UIUtil.Image(canvas, "CoordinateInfo", new Color(0f, 0f, 0f, 0.6f));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
            // 모바일은 오른쪽 위에 일시정지·도움말·지도 버튼이 있으므로 조금 내린다
            panel.anchoredPosition = new Vector2(-20f, Unity.FPS.Gameplay.VirtualInput.Active ? -115f : -20f);
            panel.sizeDelta = new Vector2(k_MiniSize + 40f, 330f);

            m_InfoText = UIUtil.Text(panel, m_Font, "", 20f, TextAlignmentOptions.TopLeft);
            UIUtil.Stretch(m_InfoText.rectTransform, 10f);
            m_InfoText.enableAutoSizing = true;
            m_InfoText.fontSizeMin = 12f;
            m_InfoText.fontSizeMax = 20f;
        }

        void BuildMap(Transform canvas)
        {
            MathWorld world = m_Manager.World;

            Rect b = world.Bounds;
            float size = Mathf.Max(b.width, b.height) + 2f;
            m_View = new Rect(b.center.x - size / 2f, b.center.y - size / 2f, size, size);

            m_MapPanel = UIUtil.Image(canvas, "CoordinatePlaneMap", new Color(0.02f, 0.04f, 0.08f, 0.85f));
            m_MapPanel.gameObject.AddComponent<RectMask2D>();

            m_Content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Content.SetParent(m_MapPanel, false);
            m_Content.sizeDelta = new Vector2(k_ContentSize, k_ContentSize);

            var floor = new GameObject("Floor", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            floor.rectTransform.SetParent(m_Content, false);
            floor.rectTransform.sizeDelta = new Vector2(k_ContentSize, k_ContentSize);
            floor.texture = RasterizeNavMesh(world, 256);
            floor.raycastTarget = false;

            int step = size > 80f ? 2 : 1;
            int major = size > 80f ? 10 : 5;
            for (int v = Mathf.CeilToInt(m_View.xMin); v <= m_View.xMax; v++)
            {
                if (v % step != 0) continue;
                bool isMajor = v % major == 0;
                Color c = v == 0 ? new Color(1f, 0.35f, 0.35f, 0.95f) : new Color(1f, 1f, 1f, isMajor ? 0.25f : 0.09f);
                AddLine(new Vector2(v, m_View.yMin), new Vector2(v, m_View.yMax), c, v == 0 ? 3f : 1f);
                if (isMajor && v != 0) AddTick(new Vector2(v, 0f), v.ToString(), new Vector2(0f, -14f));
            }

            for (int v = Mathf.CeilToInt(m_View.yMin); v <= m_View.yMax; v++)
            {
                if (v % step != 0) continue;
                bool isMajor = v % major == 0;
                Color c = v == 0 ? new Color(0.35f, 1f, 0.45f, 0.95f) : new Color(1f, 1f, 1f, isMajor ? 0.25f : 0.09f);
                AddLine(new Vector2(m_View.xMin, v), new Vector2(m_View.xMax, v), c, v == 0 ? 3f : 1f);
                if (isMajor && v != 0) AddTick(new Vector2(0f, v), v.ToString(), new Vector2(-18f, 0f));
            }

            AddTick(new Vector2(m_View.xMax, 0f), "x", new Vector2(-14f, 16f), 26f, new Color(1f, 0.5f, 0.5f));
            AddTick(new Vector2(0f, m_View.yMax), "y", new Vector2(16f, -16f), 26f, new Color(0.5f, 1f, 0.55f));
            AddTick(Vector2.zero, "O", new Vector2(-14f, -16f), 22f, Color.white);

            foreach (GraphLaser laser in m_Manager.Lasers)
                AddLine(world.ToPlane(laser.LeftEnd), world.ToPlane(laser.RightEnd), new Color(1f, 0.4f, 0.2f), 4f);

            foreach (MathStation station in m_Manager.Stations)
            {
                var dot = UIUtil.Image(m_Content, "Station", new Color(1f, 0.85f, 0.2f));
                dot.sizeDelta = new Vector2(20f, 20f);
                dot.anchoredPosition = PlaneToContent(station.PlanePosition);
                var label = UIUtil.Text(m_Content, m_Font, "", 20f, TextAlignmentOptions.Left);
                label.rectTransform.sizeDelta = new Vector2(200f, 30f);
                label.rectTransform.pivot = new Vector2(0f, 0.5f);
                label.rectTransform.anchoredPosition = dot.anchoredPosition + new Vector2(14f, 0f);
                m_StationDots.Add((station, dot.GetComponent<Image>(), label));
            }

            m_HuntMarker = UIUtil.Image(m_Content, "HuntTarget", new Color(0.3f, 1f, 1f));
            m_HuntMarker.sizeDelta = new Vector2(22f, 22f);
            m_HuntMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            m_HuntMarker.gameObject.SetActive(false);

            m_PlayerHeading = UIUtil.Image(m_Content, "Heading", new Color(0.4f, 0.9f, 1f));
            m_PlayerHeading.pivot = new Vector2(0f, 0.5f);
            m_PlayerHeading.sizeDelta = new Vector2(34f, 4f);
            m_PlayerDot = UIUtil.Image(m_Content, "Player", new Color(0.4f, 0.9f, 1f));
            m_PlayerDot.sizeDelta = new Vector2(16f, 16f);
            m_PlayerDot.localRotation = Quaternion.Euler(0f, 0f, 45f);

            m_MapTitle = UIUtil.Text(m_MapPanel, m_Font, "", 18f, TextAlignmentOptions.TopLeft);
            m_MapTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            m_MapTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            m_MapTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            m_MapTitle.rectTransform.sizeDelta = new Vector2(-16f, 30f);
            m_MapTitle.rectTransform.anchoredPosition = new Vector2(0f, -6f);
        }

        void BuildWeaponBanner(Transform canvas)
        {
            var panel = UIUtil.Image(canvas, "WeaponBanner", new Color(0.03f, 0.05f, 0.1f, 0.85f));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 120f);
            panel.sizeDelta = new Vector2(760f, 96f);
            m_WeaponBanner = panel.gameObject.AddComponent<CanvasGroup>();
            m_WeaponBanner.alpha = 0f;

            m_WeaponBannerText = UIUtil.Text(panel, m_Font, "", 22f, TextAlignmentOptions.Center);
            UIUtil.Stretch(m_WeaponBannerText.rectTransform, 8f);
            m_WeaponBannerText.enableAutoSizing = true;
            m_WeaponBannerText.fontSizeMin = 12f;
            m_WeaponBannerText.fontSizeMax = 22f;
        }

        void BuildNextObjective(Transform canvas)
        {
            var panel = UIUtil.Image(canvas, "NextObjective", new Color(0f, 0f, 0f, 0.6f));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -110f);
            panel.sizeDelta = new Vector2(640f, 56f);
            m_NextObjective = UIUtil.Text(panel, m_Font, "", 30f, TextAlignmentOptions.Center);
            UIUtil.Stretch(m_NextObjective.rectTransform, 4f);
            m_NextObjective.enableAutoSizing = true;
            m_NextObjective.fontSizeMin = 16f;
            m_NextObjective.fontSizeMax = 30f;
        }

        // 가장 가까운 남은 목표(관문 > 좌표 목표 > 적 순으로 우선)를 거리와 방향 화살표로 알려준다
        void UpdateNextObjective(Transform player)
        {
            string name = null;
            Vector3 target = default;
            float best = float.MaxValue;

            void Consider(string n, Vector3 pos, float priority)
            {
                float d = Vector3.Distance(player.position, pos) + priority;
                if (d < best) { best = d; name = n; target = pos; }
            }

            foreach (MathStation s in m_Manager.Stations)
                if (s && !s.IsSolved) Consider($"<color=#FFD54A>관문 {s.Letter}</color> (정답 표적 쏘기)", s.transform.position, 0f);

            CoordinateHuntObjective hunt = m_Manager.Hunt;
            if (hunt && !hunt.IsCompleted && hunt.HasTarget && hunt.ShowMarker)
                Consider($"<color=#4DFFFF>좌표 ({hunt.CurrentTarget.x}, {hunt.CurrentTarget.y})</color>로 이동", hunt.TargetWorld, 0f);

            if (name == null && m_EnemyManager && m_EnemyManager.Enemies != null)
                foreach (EnemyController e in m_EnemyManager.Enemies)
                    if (e) Consider("<color=#FF6B6B>적 처치</color>", e.transform.position, 0f);

            if (name == null)
            {
                m_NextObjective.text = hunt && !hunt.IsCompleted
                    ? $"<color=#4DFFFF>좌표 탐색</color>: 오른쪽 위 단서를 풀어 보세요"
                    : "<color=#6BFF8A>모든 목표 완료!</color>";
                return;
            }

            Vector3 to = Vector3.ProjectOnPlane(target - player.position, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up);
            float angle = Vector3.SignedAngle(forward, to, Vector3.up);
            string[] arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
            int index = ((Mathf.RoundToInt(angle / 45f) % 8) + 8) % 8;
            m_NextObjective.text = $"다음 목표: {name}  ·  {to.magnitude:0}m  <size=130%>{arrows[index]}</size>";
        }

        void ShowWeaponBanner(WeaponController weapon)
        {
            if (!weapon) return;
            int slot = m_Manager.PlayerWeapons ? m_Manager.PlayerWeapons.ActiveWeaponIndex + 1 : 0;
            m_WeaponBannerText.text =
                $"<b><color=#FFD54A>[{slot}] {weapon.WeaponName}</color></b>   <size=80%>{(Unity.FPS.Gameplay.VirtualInput.Active ? "◀ ▶ 버튼으로 교체" : "Q / E 로 교체")}</size>\n" +
                $"{MathCombat.Describe(weapon)}\n<size=85%><color=#9FEFFF>탄도: {MathCombat.Formula(weapon)}</color></size>";
            m_BannerHideTime = Time.time + 3f;
            m_WeaponBanner.alpha = 1f;
        }

        void SetBig(bool big)
        {
            m_Big = big;
            if (big)
            {
                m_MapPanel.anchorMin = m_MapPanel.anchorMax = m_MapPanel.pivot = new Vector2(0.5f, 0.5f);
                m_MapPanel.anchoredPosition = Vector2.zero;
                m_MapPanel.sizeDelta = new Vector2(k_BigSize, k_BigSize);
                m_MapPanel.gameObject.SetActive(true);
            }
            else
            {
                m_MapPanel.anchorMin = m_MapPanel.anchorMax = m_MapPanel.pivot = new Vector2(1f, 1f);
                m_MapPanel.anchoredPosition = new Vector2(-40f, -360f);
                // 모바일 화면에는 버튼이 많으므로 작은 지도는 숨기고, 지도 버튼으로 큰 지도만 연다
                m_MapPanel.gameObject.SetActive(!Unity.FPS.Gameplay.VirtualInput.Active);
                m_MapPanel.sizeDelta = new Vector2(k_MiniSize, k_MiniSize);
            }

            m_MapTitle.fontSize = big ? 22f : 16f;
            m_MapTitle.text = big
                ? $"좌표평면 지도  (1칸 = {m_Manager.UnitSize:0.#}m)   <color=#FFD54A>■ 관문</color>  <color=#FF5A5A>■ 적</color>  <color=#4DFFFF>◆ 탐색 목표</color>  <color=#FF7A40>━ 레이저</color>   [M] 닫기"
                : $"1칸 = {m_Manager.UnitSize:0.#}m   [M] 전체 지도";
        }

        // ------------------------------------------------------------------ 갱신

        void Update()
        {
            if (MathInput.Pressed(Key.M, "map") && Time.timeScale > 0f)
                SetBig(!m_Big);

            if (m_WeaponBanner.alpha > 0f && Time.time > m_BannerHideTime)
                m_WeaponBanner.alpha = Mathf.Max(0f, m_WeaponBanner.alpha - Time.deltaTime * 2f);

            Transform player = m_Manager.Player;
            if (!player) return;

            Vector2 p = m_Manager.World.ToPlane(player.position);
            Vector3 f = player.forward;
            float theta = Mathf.Repeat(Mathf.Atan2(f.z, f.x) * Mathf.Rad2Deg, 360f);

            m_PlayerDot.anchoredPosition = PlaneToContent(p);
            m_PlayerHeading.anchoredPosition = m_PlayerDot.anchoredPosition;
            m_PlayerHeading.localRotation = Quaternion.Euler(0f, 0f, theta);
            UpdateView(p);

            UpdateEnemies();
            UpdateStations();
            UpdateHuntMarker();
            UpdateInfo(p, theta);
            UpdateNextObjective(player);
        }

        // 미니맵은 플레이어를 가운데에 두고 따라 움직이고, 큰 지도는 전체를 보여준다
        void UpdateView(Vector2 p)
        {
            if (m_Big)
            {
                m_Content.localScale = Vector3.one * (k_BigSize / k_ContentSize);
                m_Content.anchoredPosition = Vector2.zero;
                return;
            }

            float pxPerUnit = k_ContentSize / m_View.width;
            float scale = k_MiniSize / (2f * k_MiniRange * pxPerUnit);
            m_Content.localScale = Vector3.one * scale;
            m_Content.anchoredPosition = -PlaneToContent(p) * scale;
        }

        void UpdateInfo(Vector2 p, float theta)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"<b><color=#66E6FF>P({p.x:0.0}, {p.y:0.0})</color></b>  {MathWorld.Quadrant(p)}\n");
            sb.Append($"|OP| = {p.magnitude:0.0}   방향각 θ = {theta:0}°\n");
            AppendWeaponInfo(sb);

            CoordinateHuntObjective hunt = m_Manager.Hunt;
            if (hunt && !hunt.IsCompleted && !string.IsNullOrEmpty(hunt.CurrentClue))
            {
                sb.Append($"\n<color=#7FFFD4><b>좌표 탐색 {hunt.Found + 1}/{hunt.Total}</b></color>\n");
                sb.Append($"<size=90%>단서: {hunt.CurrentClue}</size>\n");
                sb.Append("<size=80%><color=#AAB4C8>1) 단서를 계산해 목표 (x, y)를 구한다\n2) 위의 P(x, y)가 그 값이 되는 곳으로 간다</color></size>\n");
                string trend = hunt.Trend > 0 ? "<color=#6BFF8A>가까워지는 중 ▲</color>"
                    : hunt.Trend < 0 ? "<color=#FF7A7A>멀어지는 중 ▼</color>" : "<color=#AAB4C8>-</color>";
                sb.Append($"<size=85%>{trend}   ");
                if (hunt.ShowHint)
                    sb.Append($"<color=#FFB0B0>힌트: ({hunt.CurrentTarget.x}, {hunt.CurrentTarget.y})</color>{(hunt.ShowMarker ? " · 지도에 ◆ 표시" : "")}");
                else
                    sb.Append($"<color=#AAB4C8>힌트까지 {hunt.HintIn:0}초</color>");
                sb.Append("</size>");
            }

            m_InfoText.text = sb.ToString();
        }

        void AppendWeaponInfo(System.Text.StringBuilder sb)
        {
            PlayerMeleeMode melee = m_Manager.Melee;
            if (melee && melee.IsActive)
            {
                sb.Append($"<color=#9FEFFF>단검 [F]</color> θ = {melee.CurrentAngle:0}° → 피해 = {melee.DamagePerDegree:0.#}·θ = {melee.CurrentAngle * melee.DamagePerDegree:0}\n");
                return;
            }

            var weapon = m_Manager.PlayerWeapons ? m_Manager.PlayerWeapons.GetActiveWeapon() : null;
            if (!weapon) return;
            sb.Append($"<color=#FFD54A>{weapon.WeaponName}</color>  <size=85%>{MathCombat.Formula(weapon)}</size>\n");
        }

        void UpdateEnemies()
        {
            int count = 0;
            if (m_EnemyManager && m_EnemyManager.Enemies != null)
            {
                foreach (EnemyController enemy in m_EnemyManager.Enemies)
                {
                    if (!enemy) continue;
                    if (count >= m_EnemyDots.Count)
                    {
                        var dot = UIUtil.Image(m_Content, "Enemy", new Color(1f, 0.3f, 0.3f));
                        dot.sizeDelta = new Vector2(12f, 12f);
                        var label = UIUtil.Text(m_Content, m_Font, "", 16f, TextAlignmentOptions.Left);
                        label.rectTransform.sizeDelta = new Vector2(160f, 24f);
                        label.rectTransform.pivot = new Vector2(0f, 0.5f);
                        label.color = new Color(1f, 0.6f, 0.6f);
                        m_EnemyDots.Add((dot, label));
                    }

                    Vector2 ep = m_Manager.World.ToPlane(enemy.transform.position);
                    var (d, l) = m_EnemyDots[count];
                    d.gameObject.SetActive(true);
                    d.anchoredPosition = PlaneToContent(ep);
                    d.SetAsLastSibling();
                    l.gameObject.SetActive(m_Big);
                    if (m_Big)
                    {
                        l.rectTransform.anchoredPosition = d.anchoredPosition + new Vector2(10f, 0f);
                        l.text = $"({ep.x:0}, {ep.y:0})";
                    }

                    count++;
                }
            }

            for (int i = count; i < m_EnemyDots.Count; i++)
            {
                m_EnemyDots[i].dot.gameObject.SetActive(false);
                m_EnemyDots[i].label.gameObject.SetActive(false);
            }

            m_PlayerHeading.SetAsLastSibling();
            m_PlayerDot.SetAsLastSibling();
        }

        void UpdateStations()
        {
            foreach (var (station, dot, label) in m_StationDots)
            {
                if (!station) continue;
                dot.color = station.IsSolved ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 0.85f, 0.2f);
                Vector2 sp = station.PlanePosition;
                label.text = m_Big ? $"{station.Letter} ({sp.x:0}, {sp.y:0})" : station.Letter.ToString();
            }
        }

        void UpdateHuntMarker()
        {
            CoordinateHuntObjective hunt = m_Manager.Hunt;
            bool show = hunt && !hunt.IsCompleted && hunt.ShowMarker;
            m_HuntMarker.gameObject.SetActive(show);
            if (show)
                m_HuntMarker.anchoredPosition = PlaneToContent(m_Manager.World.ToPlane(hunt.TargetWorld));
        }

        // ------------------------------------------------------------------ 도우미

        Vector2 PlaneToContent(Vector2 p)
        {
            Vector2 n = new Vector2((p.x - m_View.xMin) / m_View.width, (p.y - m_View.yMin) / m_View.height);
            return (n - new Vector2(0.5f, 0.5f)) * k_ContentSize;
        }

        void AddLine(Vector2 a, Vector2 b, Color color, float thickness)
        {
            Vector2 pa = PlaneToContent(a), pb = PlaneToContent(b);
            var line = UIUtil.Image(m_Content, "Line", color);
            line.sizeDelta = new Vector2((pb - pa).magnitude, thickness);
            line.anchoredPosition = (pa + pb) / 2f;
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg);
        }

        void AddTick(Vector2 planePos, string text, Vector2 offset, float fontSize = 16f, Color? color = null)
        {
            var t = UIUtil.Text(m_Content, m_Font, text, fontSize, TextAlignmentOptions.Center);
            t.rectTransform.sizeDelta = new Vector2(50f, 24f);
            Vector2 pos = PlaneToContent(planePos) + offset;
            float edge = k_ContentSize / 2f - 12f;
            t.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(pos.x, -edge, edge), Mathf.Clamp(pos.y, -edge, edge));
            t.color = color ?? new Color(1f, 1f, 1f, 0.75f);
        }

        Texture2D RasterizeNavMesh(MathWorld world, int res)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[res * res];
            var fill = new Color32(90, 110, 140, 150);

            Vector2 ToPixel(Vector3 v)
            {
                Vector2 p = world.ToPlane(v);
                return new Vector2((p.x - m_View.xMin) / m_View.width * res, (p.y - m_View.yMin) / m_View.height * res);
            }

            for (int i = 0; i < world.Indices.Length; i += 3)
            {
                Vector2 a = ToPixel(world.Vertices[world.Indices[i]]);
                Vector2 b = ToPixel(world.Vertices[world.Indices[i + 1]]);
                Vector2 c = ToPixel(world.Vertices[world.Indices[i + 2]]);
                int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)));
                int maxX = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x)));
                int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)));
                int maxY = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y)));
                float area = Cross(b - a, c - a);
                if (Mathf.Abs(area) < 1e-5f) continue;

                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 q = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Cross(b - q, c - q) / area, w1 = Cross(c - q, a - q) / area, w2 = 1f - w0 - w1;
                    if (w0 >= -0.02f && w1 >= -0.02f && w2 >= -0.02f)
                        pixels[y * res + x] = fill;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
    }
}
