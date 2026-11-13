using TMPro;
using Unity.FPS.MathMode;
using UnityEngine;

namespace Unity.FPS.NewMode
{
    // 오른쪽 위 조작 키 안내 (PC) — 일시정지 메뉴의 "키 안내: 켜짐/꺼짐"으로 끄고 켠다 (기본: 켜짐)
    //  스킬 줄에는 지금 직업의 스킬 이름이 붙는다. 휴대폰(터치 조작)에서는 보이지 않는다.
    public class KeyGuide : MonoBehaviour
    {
        public static bool Shown
        {
            get => PlayerPrefs.GetInt("ui.keyguide", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("ui.keyguide", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        NewModePlayer m_Player;
        NewModeCombat m_Combat;
        Canvas m_Canvas;
        RectTransform m_Panel;
        WeaponDef m_BuiltFor;

        public static void Attach(NewModePlayer player)
        {
            if (NewModeTouch.Enabled || player.GetComponent<KeyGuide>()) return;
            player.gameObject.AddComponent<KeyGuide>().m_Player = player;
        }

        void Start()
        {
            m_Combat = GetComponent<NewModeCombat>();
            // 전투 HUD(9)·일시정지(10)보다 아래 — 메뉴가 열리면 어둡게 덮인다
            m_Canvas = UIUtil.Canvas(transform, "Key Guide", 7);
        }

        void Build(WeaponDef weapon)
        {
            m_BuiltFor = weapon;
            if (m_Panel) Destroy(m_Panel.gameObject);

            string Skill(int i) => weapon != null && weapon.Skills != null && i < weapon.Skills.Length ? weapon.Skills[i].Name : "스킬";
            bool gun = weapon != null && weapon.Kind == WeaponKind.Gun;
            var list = new System.Collections.Generic.List<(string key, string what)>
            {
                ("W A S D", "이동"), ("Shift", "달리기"), ("Space", "점프"),
                ("좌클릭", weapon != null && weapon.Basic != null ? $"평타 · {weapon.Basic.Name}" : "평타"),
            };
            if (gun) list.Add(("우클릭", "조준"));
            list.Add(("Q", Skill(0)));
            list.Add(("E", Skill(1)));
            list.Add(("X", Skill(2)));
            list.Add(("C", $"궁극기 · {Skill(3)}"));
            if (gun) list.Add(("R", "장전"));
            list.Add(("F", "수학 문제 풀기"));
            list.Add(("T", "훈련장 ↔ 모험"));
            list.Add(("Tab", "캐릭터 정보"));
            list.Add(("F1", "수학 보기 (그래프·식)"));
            list.Add(("Esc", "메뉴 · 설정"));
            int rows = list.Count;
            const float rowH = 27f, top = 40f, width = 330f;

            m_Panel = UIUtil.Image(m_Canvas.transform, "Keys", new Color(0.05f, 0.06f, 0.1f, 0.55f));
            m_Panel.anchorMin = m_Panel.anchorMax = new Vector2(1f, 1f);
            m_Panel.pivot = new Vector2(1f, 1f);
            m_Panel.anchoredPosition = new Vector2(-18f, -56f);
            m_Panel.sizeDelta = new Vector2(width, top + rows * rowH + 10f);
            var edge = m_Panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            edge.effectColor = new Color(Gs.Gold.r, Gs.Gold.g, Gs.Gold.b, 0.45f);

            var title = Gs.Text(m_Panel, "<b>조작 키</b>  <size=70%><color=#BBBBBB>Esc → 키 안내 끄기</color></size>", 20f, TextAlignmentOptions.Left, Gs.GoldBright);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-24f, 30f);
            title.rectTransform.anchoredPosition = new Vector2(12f, -6f);

            for (int i = 0; i < rows; i++)
            {
                float y = -top - i * rowH - rowH * 0.5f;
                var k = Gs.Key(m_Panel, list[i].key, 22f);
                k.anchorMin = k.anchorMax = new Vector2(0f, 1f);
                k.pivot = new Vector2(0f, 0.5f);
                k.anchoredPosition = new Vector2(12f, y);
                var d = Gs.Text(m_Panel, list[i].what, 17f, TextAlignmentOptions.Left, Gs.Cream, true);
                d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(0f, 1f);
                d.rectTransform.pivot = new Vector2(0f, 0.5f);
                d.rectTransform.sizeDelta = new Vector2(width - 112f, rowH);
                d.rectTransform.anchoredPosition = new Vector2(104f, y);
                d.enableAutoSizing = true;
                d.fontSizeMin = 11f;
                d.fontSizeMax = 17f;
                d.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        void Update()
        {
            if (!m_Canvas) return;
            WeaponDef weapon = m_Combat ? m_Combat.CurrentWeapon : null;
            if (!m_Panel || weapon != m_BuiltFor) Build(weapon);
            bool show = Shown && m_Player && !m_Player.Dead && !(m_Combat && m_Combat.InCutscene);
            if (m_Panel.gameObject.activeSelf != show) m_Panel.gameObject.SetActive(show);
        }
    }
}
