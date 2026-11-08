using DG.Tweening;
using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 캐릭터 정보 창 (Tab) — 원신의 "캐릭터" 화면처럼
    //  · 게임이 멈추고 카메라가 캐릭터 정면으로 돌아와 왼쪽에 캐릭터, 오른쪽에 정보
    //  · 탭: 속성(능력치) / 스킬(평타 + Q·E·X·C) / 조작
    //  · Tab 또는 Esc로 닫기
    public class CharacterSheet : MonoBehaviour
    {
        static CharacterSheet s_Open;

        NewModeCombat m_Combat;
        NewModePlayer m_Player;
        Canvas m_Canvas;
        RectTransform m_Body;
        Button[] m_Tabs;
        int m_Tab;
        float m_PrevScale;
        int m_OpenedFrame;

        public static bool IsOpen => s_Open;

        public static void Open(NewModeCombat combat)
        {
            if (s_Open) { s_Open.Close(); return; }
            var player = combat.PlayerRef;
            if (!player || player.Dead || player.InputBlocked || player.Paused) return;
            var sheet = new GameObject("Character Sheet").AddComponent<CharacterSheet>();
            sheet.m_Combat = combat;
            sheet.m_Player = player;
            sheet.Build();
            s_Open = sheet;
        }

        void Build()
        {
            m_OpenedFrame = Time.frameCount;
            m_PrevScale = Time.timeScale;
            SetHud(false);
            Time.timeScale = 0f;
            m_Player.InputBlocked = true;
            m_Player.CameraOverride = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            PlaceCamera();

            WeaponCatalog.Info info = WeaponCatalog.Current;
            m_Canvas = UIUtil.Canvas(transform, "Character Sheet", 38);
            var root = (RectTransform)m_Canvas.transform;

            // 왼쪽은 캐릭터가 보이도록 투명, 오른쪽으로 갈수록 어둡게
            var shade = UIUtil.Image(root, "Shade", new Color(0.05f, 0.06f, 0.1f, 0.78f));
            shade.anchorMin = new Vector2(0.48f, 0f);
            shade.anchorMax = new Vector2(1f, 1f);
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            var edge = UIUtil.Image(root, "ShadeEdge", new Color(0.05f, 0.06f, 0.1f, 0.4f));
            edge.anchorMin = new Vector2(0.4f, 0f);
            edge.anchorMax = new Vector2(0.48f, 1f);
            edge.offsetMin = edge.offsetMax = Vector2.zero;

            // 왼쪽 위: 직업 이름 · 역할
            var badge = UIUtil.RoundedImage(root, "Element", info.Color, true);
            UIUtil.Place(badge, new Vector2(0f, 1f), new Vector2(90f, -90f), new Vector2(64f, 64f));
            badge.gameObject.AddComponent<Outline>().effectColor = Gs.Gold;
            var name = Gs.Text(root, $"<b>{info.ClassName}</b>", 56f, TextAlignmentOptions.Left, Color.white, true);
            UIUtil.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(440f, -72f), new Vector2(620f, 70f));
            var role = Gs.Text(root, $"<color={Gs.Hex(Gs.GoldBright)}>{info.Role}</color>   ·   {info.Name}", 26f, TextAlignmentOptions.Left, Gs.Cream, true);
            UIUtil.Place(role.rectTransform, new Vector2(0f, 1f), new Vector2(440f, -122f), new Vector2(620f, 40f));

            // 오른쪽: 탭 + 내용
            string[] tabs = { "속성", "스킬", "조작" };
            m_Tabs = new Button[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                var b = Gs.Button(root, tabs[i], i == 0 ? info.Color : i == 1 ? Gs.Gold : Gs.InkSoft, () => ShowTab(index), new Vector2(200f, 64f), 28f);
                var rt = (RectTransform)b.transform;
                UIUtil.Place(rt, new Vector2(1f, 1f), new Vector2(-850f + i * 210f, -80f), new Vector2(190f, 64f));
                m_Tabs[i] = b;
            }
            var close = Gs.Button(root, "닫기 (Tab)", new Color(0.8f, 0.35f, 0.3f), Close, new Vector2(220f, 64f), 26f);
            UIUtil.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-140f, -80f), new Vector2(220f, 64f));

            m_Body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Body.SetParent(root, false);
            m_Body.anchorMin = new Vector2(0.5f, 0f);
            m_Body.anchorMax = new Vector2(1f, 1f);
            m_Body.offsetMin = new Vector2(40f, 60f);
            m_Body.offsetMax = new Vector2(-50f, -140f);

            UIAnim.FadeIn(UIAnim.Group(root), 0.25f);
            ShowTab(0);
        }

        // 전투·플레이어 HUD는 정보 창이 열린 동안 숨긴다 (겹치지 않게)
        void SetHud(bool visible)
        {
            if (!m_Player) return;
            foreach (var canvas in m_Player.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.name != "Combat HUD" && canvas.name != "NewMode HUD") continue;
                var group = UIAnim.Group(canvas);
                group.DOKill();
                group.DOFade(visible ? 1f : 0f, 0.2f).SetUpdate(true).SetLink(canvas.gameObject);
            }
        }

        // 카메라: 캐릭터 정면 약간 오른쪽 — 캐릭터가 화면 왼쪽에 오도록
        void PlaceCamera()
        {
            Camera cam = m_Player.Camera;
            if (!cam) return;
            float s = m_Player.Scale;
            Transform t = m_Player.transform;
            Vector3 eye = t.position + t.forward * 3.2f * s + t.right * -1.3f * s + Vector3.up * 1.25f * s;
            Vector3 look = t.position + Vector3.up * 0.95f * s + t.right * -1.2f * s;
            cam.transform.DOKill();
            cam.transform.DOMove(eye, 0.35f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
            cam.transform.DORotateQuaternion(Quaternion.LookRotation(look - eye), 0.35f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
        }

        void ShowTab(int index)
        {
            m_Tab = index;
            foreach (Transform c in m_Body) Destroy(c.gameObject);
            for (int i = 0; i < m_Tabs.Length; i++)
            {
                var img = m_Tabs[i].GetComponent<Image>();
                img.color = i == index ? new Color32(245, 222, 165, 255) : Gs.Cream;
                m_Tabs[i].transform.localScale = Vector3.one * (i == index ? 1.06f : 1f);
            }
            if (index == 0) BuildStats();
            else if (index == 1) BuildSkills();
            else BuildControls();
            UIAnim.SlideIn(m_Body, new Vector2(60f, 0f), 0f, 0.3f);
        }

        float m_Y;

        void BuildStats()
        {
            WeaponCatalog.Info info = WeaponCatalog.Current;
            var st = info.Stats;
            m_Y = 0f;
            Section("기본 능력치");
            Row("체력", $"{Mathf.CeilToInt(m_Player.HP)} / {Mathf.RoundToInt(m_Player.MaxHP)}", m_Player.HP / m_Player.MaxHP);
            Row("공격력", $"{Mathf.RoundToInt(st.Attack * 100f)}%", st.Attack / 1.3f);
            Row("공격 속도", $"{Mathf.RoundToInt(st.AttackSpeed * 100f)}%", st.AttackSpeed / 1.4f);
            Row("이동 속도", $"{Mathf.RoundToInt(st.Move * 100f)}%", st.Move / 1.2f);
            Row("방어력 (받는 피해 감소)", $"{Mathf.RoundToInt(st.Defense * 100f)}%", st.Defense / 0.3f);
            m_Y -= 10f;
            Section("고급 능력치");
            Row("치명타 확률", $"{Mathf.RoundToInt(st.Crit * 100f)}%", st.Crit / 0.35f);
            Row("치명타 피해", $"{Mathf.RoundToInt(st.CritDamage * 100f)}%", (st.CritDamage - 1f) / 1.2f);
            if (info.Kind == WeaponKind.Gun) Row("머리 명중 배율", $"×{st.Headshot:0.0}", (st.Headshot - 1f) / 1f);
            m_Y -= 10f;
            var weapon = m_Combat.CurrentWeapon;
            Section($"고유 능력 · {weapon.GimmickName}");
            var g = Gs.Text(m_Body, weapon.GimmickInfo, 26f, TextAlignmentOptions.TopLeft, Gs.Cream);
            Top(g.rectTransform, 80f);
        }

        void BuildSkills()
        {
            var weapon = m_Combat.CurrentWeapon;
            m_Y = 0f;
            Section("스킬");
            SkillRow("좌클릭", weapon.Basic, weapon.Color);
            foreach (var s in weapon.Skills) SkillRow(s.Key, s, s.Key == "C" ? Gs.GoldBright : weapon.Color);
        }

        void BuildControls()
        {
            m_Y = 0f;
            Section("조작");
            string[,] keys =
            {
                { "W A S D", "이동 (카메라 기준)" }, { "Shift", "달리기" }, { "Space", "점프 — 보스의 주황 고리를 넘을 때" },
                { "좌클릭", "평타 1·2·3단 (이동 키 방향 / 가까운 적에게)" }, { "우클릭", "조준 (총) — 조준할 때만 조준선" },
                { "Q E X", "스킬" }, { "C", "궁극기 (컷신)" }, { "F", "수학 문제 풀기" }, { "T", "허수아비 훈련장 ↔ 모험" },
                { "Tab", "캐릭터 정보" }, { "Esc", "일시정지" },
            };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                var row = RowBase(52f, i % 2 == 0);
                var k = Gs.Key(row, keys[i, 0], 34f);
                k.anchorMin = k.anchorMax = new Vector2(0f, 0.5f);
                k.pivot = new Vector2(0f, 0.5f);
                k.anchoredPosition = new Vector2(16f, 0f);
                var d = Gs.Text(row, keys[i, 1], 24f, TextAlignmentOptions.Left, Gs.Cream);
                UIUtil.Stretch(d.rectTransform, 0f);
                d.rectTransform.offsetMin = new Vector2(190f, 0f);
            }
        }

        // ------------------------------------------------------------------ 줄 만들기

        void Section(string title)
        {
            var t = Gs.Text(m_Body, $"<b>{title}</b>", 30f, TextAlignmentOptions.Left, Gs.GoldBright);
            Top(t.rectTransform, 46f);
            var line = UIUtil.Image(m_Body, "Line", new Color(Gs.Gold.r, Gs.Gold.g, Gs.Gold.b, 0.5f));
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(1f, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.sizeDelta = new Vector2(0f, 2f);
            line.anchoredPosition = new Vector2(0f, m_Y + 4f);
            m_Y -= 8f;
        }

        void Top(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, m_Y);
            m_Y -= height;
        }

        RectTransform RowBase(float height, bool shaded)
        {
            var row = UIUtil.Image(m_Body, "Row", shaded ? Gs.Row : new Color(0f, 0f, 0f, 0f));
            Top(row, height);
            return row;
        }

        void Row(string label, string value, float fill)
        {
            var row = RowBase(50f, ((int)(-m_Y / 50f)) % 2 == 0);
            var l = Gs.Text(row, label, 26f, TextAlignmentOptions.Left, Gs.Cream);
            UIUtil.Stretch(l.rectTransform, 0f);
            l.rectTransform.offsetMin = new Vector2(18f, 0f);
            var v = Gs.Text(row, $"<b>{value}</b>", 26f, TextAlignmentOptions.Right, Color.white);
            UIUtil.Stretch(v.rectTransform, 0f);
            v.rectTransform.offsetMax = new Vector2(-18f, 0f);
            // 가는 막대 (직업끼리 비교하기 쉽게)
            var bar = UIUtil.Image(row, "Bar", new Color(1f, 1f, 1f, 0.08f));
            bar.anchorMin = new Vector2(0.45f, 0.5f);
            bar.anchorMax = new Vector2(0.75f, 0.5f);
            bar.sizeDelta = new Vector2(0f, 6f);
            var f = UIUtil.Image(bar, "Fill", new Color(Gs.Gold.r, Gs.Gold.g, Gs.Gold.b, 0.85f));
            f.anchorMin = Vector2.zero;
            f.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            f.offsetMin = f.offsetMax = Vector2.zero;
        }

        void SkillRow(string key, SkillDef skill, Color color)
        {
            var row = RowBase(108f, ((int)(-m_Y / 108f)) % 2 == 0);
            var icon = UIUtil.RoundedImage(row, "Icon", new Color(0.1f, 0.11f, 0.17f, 0.95f), true);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(56f, 0f);
            icon.sizeDelta = new Vector2(78f, 78f);
            var ring = UIUtil.Image(icon, "Ring", color);
            ring.GetComponent<Image>().sprite = Gs.Ring;
            UIUtil.Stretch(ring, 0f);
            var k = Gs.Text(icon, $"<b>{key}</b>", key.Length > 2 ? 18f : 32f, TextAlignmentOptions.Center, Color.white);
            UIUtil.Stretch(k.rectTransform, 4f);

            var n = Gs.Text(row, $"<b>{skill.Name}</b>" + (skill.Cooldown > 0f ? $"   <size=75%><color={Gs.Hex(Gs.Gold)}>재사용 {skill.Cooldown:0}초</color></size>" : ""), 28f, TextAlignmentOptions.Left, Color.white);
            UIUtil.Stretch(n.rectTransform, 0f);
            n.rectTransform.offsetMin = new Vector2(112f, 50f);
            var d = Gs.Text(row, skill.Desc ?? "", 22f, TextAlignmentOptions.TopLeft, Gs.Cream);
            UIUtil.Stretch(d.rectTransform, 0f);
            d.rectTransform.offsetMin = new Vector2(112f, 6f);
            d.rectTransform.offsetMax = new Vector2(-12f, -58f);
            var f = Gs.Text(row, FxFormula.Rich(skill.Formula ?? ""), 18f, TextAlignmentOptions.TopRight, new Color(1f, 1f, 1f, 0.4f));
            UIUtil.Stretch(f.rectTransform, 0f);
            f.rectTransform.offsetMax = new Vector2(-12f, -12f);
        }

        // ------------------------------------------------------------------ 닫기

        void Update()
        {
            if (Time.frameCount == m_OpenedFrame) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) Close();
        }

        void Close()
        {
            if (s_Open == this) s_Open = null;
            SetHud(true);
            Time.timeScale = m_PrevScale <= 0f ? 1f : m_PrevScale;
            if (m_Player)
            {
                m_Player.CameraOverride = false;
                // 같은 프레임의 Esc가 일시정지로 이어지지 않게 한 박자 뒤에 입력을 풀어 준다
                var p = m_Player;
                DOVirtual.DelayedCall(0.05f, () => { if (p) p.InputBlocked = false; }, true);
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Destroy(gameObject);
        }
    }
}
