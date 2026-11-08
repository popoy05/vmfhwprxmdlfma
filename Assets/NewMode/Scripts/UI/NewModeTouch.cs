using TMPro;
using Unity.FPS.Gameplay;
using Unity.FPS.MathMode;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.NewMode
{
    // 스마트폰(터치) 조작 — 새 모드 전용
    //  왼쪽 아래: 조이스틱 (왼쪽 화면 어디든 누르면 그 자리에 생김, 끝까지 밀면 달리기)
    //  오른쪽 화면: 끌어서 시점 돌리기
    //  오른쪽 아래: 공격(큰 버튼, 누른 채 끌면 시점도 돌아감) + 스킬 Q·E·X·C (전투 HUD의 원형 버튼 위에 겹쳐 놓은 투명 터치 영역)
    //               점프 · (권총) 조준·장전
    //  왼쪽 위: 일시정지 · 캐릭터 정보
    //  수학 문제 안내([F] 수학 문제 풀기)를 누르면 문제 창이 열린다
    // 모든 입력은 VirtualInput으로 들어가 키보드·마우스 입력과 똑같이 처리된다.
    public class NewModeTouch : MonoBehaviour
    {
        /// <summary>터치 조작을 쓰는지 (휴대폰, 마우스 없는 터치스크린, 또는 Force)</summary>
        public static bool Enabled => Force || GameSettings.UseMobileControls;
        /// <summary>테스트용: PC에서도 터치 조작 화면을 켠다</summary>
        public static bool Force;

        // 오른쪽 아래 배치 (전투 HUD와 같은 기준: 오른쪽 아래 모서리에서의 위치, 1920×1080)
        public static readonly Vector2 AttackPos = new Vector2(-175f, 175f);
        public const float AttackSize = 170f, SkillSize = 112f, UltSize = 130f;
        public static Vector2 SkillPos(int i)
        {
            float[] ang = { 185f, 148f, 111f, 72f };
            float r = i == 3 ? 255f : 235f;
            float a = ang[i] * Mathf.Deg2Rad;
            return AttackPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        NewModePlayer m_Player;
        NewModeCombat m_Combat;
        Canvas m_Canvas;
        RectTransform m_Aim, m_Reload, m_Interact;
        bool m_Shown = true;

        public static void Attach(NewModePlayer player)
        {
            if (!Enabled || player.GetComponent<NewModeTouch>()) return;
            var t = player.gameObject.AddComponent<NewModeTouch>();
            t.m_Player = player;
        }

        void Start()
        {
            m_Combat = GetComponent<NewModeCombat>();
            VirtualInput.Active = true;
            VirtualInput.ResetAll();
            UIUtil.EnsureEventSystem();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Build();
        }

        void OnDestroy()
        {
            VirtualInput.ResetAll();
            VirtualInput.Active = false;
        }

        void Build()
        {
            // 전투 HUD(9)보다 위 — 버튼 그림은 HUD가 그리고, 여기는 투명한 터치 영역 + 몇 개의 보조 버튼
            m_Canvas = UIUtil.Canvas(transform, "Touch Controls", 12);
            Transform c = m_Canvas.transform;

            // 시점 영역: 화면 전체 (버튼·조이스틱이 아닌 빈 곳 어디든 끌면 시점이 돈다) — 맨 뒤에 깔림
            // UIUtil.Image는 기본으로 터치를 받지 않으므로(raycastTarget = false) 반드시 켜야 한다
            var lookArea = UIUtil.Image(c, "LookArea", new Color(0f, 0f, 0f, 0.001f));
            lookArea.anchorMin = Vector2.zero;
            lookArea.anchorMax = Vector2.one;
            lookArea.offsetMin = lookArea.offsetMax = Vector2.zero;
            lookArea.GetComponent<Image>().raycastTarget = true;
            lookArea.gameObject.AddComponent<TouchLookArea>();

            // 이동 조이스틱 (왼쪽 아래 45% × 62%, 위쪽 미니맵은 피함) — 시점 영역 위에
            var moveArea = UIUtil.Image(c, "MoveArea", new Color(0f, 0f, 0f, 0.001f));
            moveArea.anchorMin = Vector2.zero;
            moveArea.anchorMax = new Vector2(0.45f, 0.62f);
            moveArea.offsetMin = moveArea.offsetMax = Vector2.zero;
            moveArea.GetComponent<Image>().raycastTarget = true;
            var joyBase = UIUtil.RoundedImage(moveArea, "Base", new Color(1f, 1f, 1f, 0.16f), true);
            joyBase.GetComponent<Image>().sprite = Gs.Ring;
            joyBase.GetComponent<Image>().raycastTarget = false;
            var joyKnob = UIUtil.RoundedImage(joyBase, "Knob", new Color(Gs.Cream.r, Gs.Cream.g, Gs.Cream.b, 0.6f), true);
            joyKnob.GetComponent<Image>().raycastTarget = false;
            joyBase.sizeDelta = new Vector2(250f, 250f);
            joyKnob.sizeDelta = new Vector2(110f, 110f);
            moveArea.gameObject.AddComponent<TouchJoystick>().Init(joyBase, joyKnob);

            Vector2 br = new Vector2(1f, 0f);
            // 공격 (누른 채 끌면 시점도)
            var attack = Hit(c, br, AttackPos, AttackSize, "attack", true);
            attack.gameObject.AddComponent<TouchLookArea>().PassThrough = true;
            // 스킬 Q E X C
            for (int i = 0; i < 4; i++) Hit(c, br, SkillPos(i), i == 3 ? UltSize : SkillSize, "skill" + i, false);

            // 점프 · 조준 · 장전
            Visible(c, br, new Vector2(-575f, 90f), 108f, "점프", new Color(0.25f, 0.45f, 0.8f, 0.7f), "jump", true);
            m_Aim = Visible(c, br, new Vector2(-590f, 245f), 96f, "조준", new Color(0.3f, 0.35f, 0.5f, 0.7f), "aim", true);
            m_Reload = Visible(c, br, new Vector2(-525f, 390f), 84f, "장전", new Color(0.3f, 0.35f, 0.5f, 0.7f), "reload", false);

            // 왼쪽 위: 일시정지 · 캐릭터 정보
            Vector2 tl = new Vector2(0f, 1f);
            Visible(c, tl, new Vector2(56f, -56f), 84f, "<b>| |</b>", new Color(0.12f, 0.13f, 0.2f, 0.8f), "pause", false);
            Visible(c, tl, new Vector2(150f, -56f), 84f, "정보", new Color(0.12f, 0.13f, 0.2f, 0.8f), "sheet", false);

            // 수학 문제 안내를 누르면 열기 (위치는 매 프레임 안내 상자에 맞춤)
            m_Interact = Hit(c, new Vector2(0.5f, 0.5f), Vector2.zero, 60f, "interact", false);
        }

        // 투명 터치 영역 (그림은 전투 HUD가 그림)
        RectTransform Hit(Transform parent, Vector2 anchor, Vector2 pos, float size, string id, bool hold)
        {
            RectTransform rt = UIUtil.RoundedImage(parent, id, new Color(1f, 1f, 1f, 0.001f), true);
            UIUtil.Place(rt, anchor, pos, new Vector2(size, size) * 1.08f);
            rt.GetComponent<Image>().raycastTarget = true;
            var t = rt.gameObject.AddComponent<TouchButton>();
            t.VirtualId = id;
            t.Hold = hold;
            return rt;
        }

        // 보이는 원형 버튼 (원신 풍: 어두운 원 + 금빛 고리 + 글자)
        RectTransform Visible(Transform parent, Vector2 anchor, Vector2 pos, float size, string label, Color color, string id, bool hold)
        {
            RectTransform rt = UIUtil.RoundedImage(parent, id, color, true);
            UIUtil.Place(rt, anchor, pos, new Vector2(size, size));
            rt.GetComponent<Image>().raycastTarget = true;
            var ring = UIUtil.Image(rt, "Ring", new Color(Gs.Gold.r, Gs.Gold.g, Gs.Gold.b, 0.8f));
            ring.GetComponent<Image>().sprite = Gs.Ring;
            ring.GetComponent<Image>().raycastTarget = false;
            UIUtil.Stretch(ring, 0f);
            var text = Gs.Text(rt, label, size * 0.3f, TextAlignmentOptions.Center, Color.white, true);
            text.raycastTarget = false;
            UIUtil.Stretch(text.rectTransform, 4f);
            var t = rt.gameObject.AddComponent<TouchButton>();
            t.VirtualId = id;
            t.Hold = hold;
            return rt;
        }

        void Update()
        {
            if (!m_Player) m_Player = GetComponent<NewModePlayer>();
            // 일시정지·문제 창·캐릭터 정보·컷신 중에는 숨겨서 그 화면의 버튼이 눌리게
            bool show = m_Player && !m_Player.Paused && !m_Player.InputBlocked && !m_Player.Dead
                        && Time.timeScale > 0.001f && !(m_Combat && m_Combat.InCutscene);
            if (show != m_Shown)
            {
                m_Shown = show;
                m_Canvas.gameObject.SetActive(show);
                if (!show) VirtualInput.ResetAll();
            }
            if (!show || !m_Combat || m_Combat.CurrentWeapon == null) return;

            bool gun = m_Combat.CurrentWeapon.Kind == WeaponKind.Gun;
            if (m_Aim.gameObject.activeSelf != gun) { m_Aim.gameObject.SetActive(gun); m_Reload.gameObject.SetActive(gun); }

            // 수학 문제 안내 위에 터치 영역
            RectTransform prompt = m_Combat.QuizPrompt;
            bool promptOn = prompt && prompt.gameObject.activeInHierarchy;
            if (m_Interact.gameObject.activeSelf != promptOn) m_Interact.gameObject.SetActive(promptOn);
            if (promptOn)
            {
                m_Interact.anchorMin = prompt.anchorMin;
                m_Interact.anchorMax = prompt.anchorMax;
                m_Interact.pivot = prompt.pivot;
                m_Interact.anchoredPosition = prompt.anchoredPosition;
                m_Interact.sizeDelta = prompt.sizeDelta + new Vector2(30f, 30f);
            }
        }
    }
}
