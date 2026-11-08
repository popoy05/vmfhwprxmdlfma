using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.FPS.NewMode;

namespace Unity.FPS.MathMode
{
    // 앱을 켜면 가장 먼저 나오는 화면.
    //  1단계: "이전 모드"(수학 FPS) / "새 모드" 선택
    //  새 모드: 캐릭터 꾸미기(직업 고르기) → 중세 아레나 모험
    public class ModeSelectScreen : MonoBehaviour
    {
        TMP_FontAsset m_Font;
        Canvas m_Canvas;
        GameObject m_Page;
        bool m_MapPage;

        void Start()
        {
            ScreenCommon.HideOriginalUI(gameObject);
            UIUtil.EnsureEventSystem();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            m_Font = ScreenCommon.Font;

            // 배경: 이전 모드의 맵을 천천히 돌며 보여준다
            gameObject.AddComponent<MenuBackdrop>().Show((MapTheme)Random.Range(0, GameSettings.ThemeCount), 4242);

            m_Canvas = UIUtil.Canvas(transform, "ModeSelect", 10);
            var dim = UIUtil.Image(m_Canvas.transform, "Dim", new Color(0.02f, 0.04f, 0.1f, 0.55f));
            UIUtil.Stretch(dim, 0f);

            if (!Application.isMobilePlatform)
            {
                var quit = Gs.Button(m_Canvas.transform, "종료", new Color(0.85f, 0.4f, 0.35f), Application.Quit, new Vector2(200f, 60f), 24f);
                UIUtil.Place((RectTransform)quit.transform, new Vector2(1f, 1f), new Vector2(-130f, -50f), new Vector2(200f, 60f));
            }

            ShowPage(false);
            AppBootstrap.NewModeMapSelect = false;
        }

        void ShowPage(bool mapPage)
        {
            m_MapPage = mapPage;
            if (m_Page) Destroy(m_Page);
            m_Page = new GameObject(mapPage ? "MapPage" : "ModePage", typeof(RectTransform));
            var page = (RectTransform)m_Page.transform;
            page.SetParent(m_Canvas.transform, false);
            UIUtil.Stretch(page, 0f);

            // 원신 풍 제목: 양옆 금색 장식선
            var title = Gs.Header(page, "모드 선택", 88f, new Vector2(0f, -170f), 1200f, Gs.GoldBright);
            title.outlineWidth = 0.12f;
            title.outlineColor = new Color32(40, 30, 10, 255);
            UIAnim.PopIn(title.transform.parent, 0f, 0.45f); // DOTween: 제목이 통 튀어나옴

            Card(page, new Vector2(-330f, -40f), new Color(0.25f, 0.4f, 0.75f),
                "이전 모드", "수학 FPS · 좌표 전선\n지금까지 만든 게임", OpenClassic);
            Card(page, new Vector2(330f, -40f), new Color(0.2f, 0.72f, 0.42f),
                "새 모드", "직업을 골라\n성 꼭대기의 보스를 향해", OpenCharacter);
            string hintText = "1: 이전 모드   2: 새 모드";

            if (!Application.isMobilePlatform)
            {
                var hint = Gs.Text(page, hintText, 22f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f), true);
                UIUtil.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(800f, 40f));
                UIAnim.FadeIn(UIAnim.Group(hint), 0.8f);
            }
        }

        void Card(Transform parent, Vector2 pos, Color color, string name, string desc, UnityEngine.Events.UnityAction onClick)
        {
            // 원신 풍 카드: 크림색 바탕 + 금색 테두리 + 위쪽 색 띠 + 마름모
            var button = UIUtil.Button(parent, m_Font, "", Gs.Cream, onClick, new Vector2(560f, 420f));
            var rt = (RectTransform)button.transform;
            UIUtil.Place(rt, new Vector2(0.5f, 0.5f), pos, new Vector2(560f, 420f));
            var outline = button.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Gs.Gold;
            outline.effectDistance = new Vector2(3f, -3f);
            var band = UIUtil.RoundedImage(rt, "Band", color);
            band.anchorMin = new Vector2(0f, 1f); band.anchorMax = new Vector2(1f, 1f); band.pivot = new Vector2(0.5f, 1f);
            band.sizeDelta = new Vector2(-24f, 110f);
            band.anchoredPosition = new Vector2(0f, -12f);
            var icon = Gs.Diamond(rt, Gs.GoldBright, 34f);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1f);
            icon.anchoredPosition = new Vector2(0f, -122f);
            icon.gameObject.AddComponent<UnityEngine.UI.Outline>().effectColor = new Color(0.3f, 0.2f, 0f);

            var label = Gs.Text(rt, $"<b>{name}</b>", 70f, TextAlignmentOptions.Center, Color.white, true);
            UIUtil.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(520f, 100f));
            var sub = Gs.Text(rt, desc, 32f, TextAlignmentOptions.Center, Gs.Ink);
            UIUtil.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(500f, 160f));

            // DOTween: 카드가 아래에서 차례로 솟아오르고, 마우스를 올리면 살짝 커진다
            UIAnim.SlideIn(rt, new Vector2(0f, -260f), pos.x < 0f ? 0.1f : 0.2f, 0.5f);
            button.GetComponent<ButtonJuice>().HoverScale = 1.05f;
        }

        void OpenClassic()
        {
            AppBootstrap.ClassicMenu = true;
            SceneManager.LoadScene(AppBootstrap.MenuScene);
        }

        // 새 모드: 캐릭터 꾸미기(직업 고르기) → "모험 시작 ▶" → 중세 아레나
        static void OpenCharacter() => LoadNewMode(AppBootstrap.NewModeCharacterScene);

        static void LoadNewMode(string scene)
        {
            AppBootstrap.ClassicMenu = false;
            SceneManager.LoadScene(scene);
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            bool one = kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            bool two = kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;

            if (one) OpenClassic();
            else if (two) OpenCharacter();
        }
    }
}
