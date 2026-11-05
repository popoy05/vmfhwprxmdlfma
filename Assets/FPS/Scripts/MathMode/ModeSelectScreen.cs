using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
                var quit = UIUtil.Button(m_Canvas.transform, m_Font, "종료", new Color(0.45f, 0.2f, 0.25f), Application.Quit, new Vector2(200f, 60f), 26f);
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

            var title = UIUtil.Text(page, m_Font, mapPage ? "<b>맵 선택</b>" : "<b>모드 선택</b>", 96f, TextAlignmentOptions.Center);
            UIUtil.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1200f, 130f));
            title.color = new Color(1f, 0.84f, 0.3f);
            title.outlineWidth = 0.15f;
            title.outlineColor = new Color(0.4f, 0.25f, 0f);
            UIAnim.PopIn(title.transform, 0f, 0.45f); // DOTween: 제목이 통 튀어나옴

            Card(page, new Vector2(-330f, -40f), new Color(0.25f, 0.4f, 0.75f),
                "이전 모드", "수학 FPS · 좌표 전선\n지금까지 만든 게임", OpenClassic);
            Card(page, new Vector2(330f, -40f), new Color(0.2f, 0.72f, 0.42f),
                "새 모드", "직업을 골라\n성 꼭대기의 보스를 향해", OpenCharacter);
            string hintText = "1: 이전 모드   2: 새 모드";

            if (!Application.isMobilePlatform)
            {
                var hint = UIUtil.Text(page, m_Font, hintText, 22f, TextAlignmentOptions.Center);
                UIUtil.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(800f, 40f));
                hint.color = new Color(1f, 1f, 1f, 0.6f);
                UIAnim.FadeIn(UIAnim.Group(hint), 0.8f);
            }
        }

        void Card(Transform parent, Vector2 pos, Color color, string name, string desc, UnityEngine.Events.UnityAction onClick)
        {
            var button = UIUtil.Button(parent, m_Font, "", color, onClick, new Vector2(560f, 420f));
            var rt = (RectTransform)button.transform;
            UIUtil.Place(rt, new Vector2(0.5f, 0.5f), pos, new Vector2(560f, 420f));

            var label = UIUtil.Text(rt, m_Font, $"<b>{name}</b>", 72f, TextAlignmentOptions.Center);
            UIUtil.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(520f, 100f));
            var sub = UIUtil.Text(rt, m_Font, desc, 32f, TextAlignmentOptions.Center);
            UIUtil.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(520f, 120f));
            sub.color = new Color(1f, 1f, 1f, 0.85f);

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
