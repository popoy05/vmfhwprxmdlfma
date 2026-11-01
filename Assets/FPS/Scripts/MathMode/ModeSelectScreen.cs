using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Unity.FPS.MathMode
{
    // 앱을 켜면 가장 먼저 나오는 화면: "이전 모드"(수학 FPS)와 "새 모드" 중 하나를 고른다.
    public class ModeSelectScreen : MonoBehaviour
    {
        TMP_FontAsset m_Font;

        void Start()
        {
            ScreenCommon.HideOriginalUI(gameObject);
            UIUtil.EnsureEventSystem();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            m_Font = ScreenCommon.Font;

            // 배경: 이전 모드의 맵을 천천히 돌며 보여준다
            gameObject.AddComponent<MenuBackdrop>().Show((MapTheme)Random.Range(0, GameSettings.ThemeCount), 4242);
            BuildUI();
        }

        void BuildUI()
        {
            Canvas canvas = UIUtil.Canvas(transform, "ModeSelect", 10);
            var dim = UIUtil.Image(canvas.transform, "Dim", new Color(0.02f, 0.04f, 0.1f, 0.55f));
            UIUtil.Stretch(dim, 0f);

            var title = UIUtil.Text(canvas.transform, m_Font, "<b>모드 선택</b>", 96f, TextAlignmentOptions.Center);
            UIUtil.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1200f, 130f));
            title.color = new Color(1f, 0.84f, 0.3f);
            title.outlineWidth = 0.15f;
            title.outlineColor = new Color(0.4f, 0.25f, 0f);

            Card(canvas.transform, new Vector2(-330f, -40f), new Color(0.25f, 0.4f, 0.75f),
                "이전 모드", "수학 FPS · 좌표 전선\n지금까지 만든 게임", "1", OpenClassic);
            Card(canvas.transform, new Vector2(330f, -40f), new Color(0.2f, 0.72f, 0.42f),
                "새 모드", "넓은 자연 맵을\n자유롭게 돌아다니기", "2", OpenNewMode);

            if (!Application.isMobilePlatform)
            {
                var quit = UIUtil.Button(canvas.transform, m_Font, "종료", new Color(0.45f, 0.2f, 0.25f), Application.Quit, new Vector2(200f, 60f), 26f);
                UIUtil.Place((RectTransform)quit.transform, new Vector2(1f, 1f), new Vector2(-130f, -50f), new Vector2(200f, 60f));

                var hint = UIUtil.Text(canvas.transform, m_Font, "1: 이전 모드   2: 새 모드", 22f, TextAlignmentOptions.Center);
                UIUtil.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(800f, 40f));
                hint.color = new Color(1f, 1f, 1f, 0.6f);
            }
        }

        void Card(Transform parent, Vector2 pos, Color color, string name, string desc, string key, UnityEngine.Events.UnityAction onClick)
        {
            var button = UIUtil.Button(parent, m_Font, "", color, onClick, new Vector2(560f, 420f));
            var rt = (RectTransform)button.transform;
            UIUtil.Place(rt, new Vector2(0.5f, 0.5f), pos, new Vector2(560f, 420f));

            var label = UIUtil.Text(rt, m_Font, $"<b>{name}</b>", 72f, TextAlignmentOptions.Center);
            UIUtil.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(520f, 100f));
            var sub = UIUtil.Text(rt, m_Font, desc, 32f, TextAlignmentOptions.Center);
            UIUtil.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(520f, 120f));
            sub.color = new Color(1f, 1f, 1f, 0.85f);
        }

        void OpenClassic()
        {
            AppBootstrap.ClassicMenu = true;
            SceneManager.LoadScene(AppBootstrap.MenuScene);
        }

        void OpenNewMode()
        {
            AppBootstrap.ClassicMenu = false;
            SceneManager.LoadScene(AppBootstrap.NewModeScene);
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) OpenClassic();
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) OpenNewMode();
        }
    }
}
