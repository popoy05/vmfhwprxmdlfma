using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.MathMode
{
    // 메뉴·결과 화면 배경: 실제 게임 맵을 만들고 카메라가 천천히 돌며 보여준다 (화면이 맵과 꼭 맞도록)
    public class MenuBackdrop : MonoBehaviour
    {
        MathModeManager m_Host;
        MapGenerator m_Map;
        Camera m_Camera;
        float m_Angle;
        public float Speed = 5f;
        public float Radius = 78f;
        public float Height = 30f;

        public MathModeManager Host => m_Host;

        public void Show(MapTheme theme, int seed)
        {
            if (m_Map != null)
                foreach (GameObject go in m_Map.CreatedObjects)
                    if (go) Destroy(go);

            if (!m_Host) m_Host = MathModeManager.CreatePreviewHost();
            m_Map = MapGenerator.Build(m_Host, theme, seed, buildNavMesh: false);

            m_Camera = Camera.main;
            if (!m_Camera) m_Camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            m_Camera.clearFlags = CameraClearFlags.Skybox;
            m_Camera.farClipPlane = 500f;
            m_Camera.fieldOfView = 50f;
            m_Camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        void Update()
        {
            if (!m_Camera) return;
            m_Angle += Speed * Time.unscaledDeltaTime;
            float a = m_Angle * Mathf.Deg2Rad;
            m_Camera.transform.position = new Vector3(Mathf.Sin(a) * Radius, Height, Mathf.Cos(a) * Radius);
            m_Camera.transform.LookAt(new Vector3(0f, 4f, 0f));
        }
    }

    // 화면에 둥둥 떠오르는 수식들 (장식)
    public class FloatingSymbols : MonoBehaviour
    {
        static readonly string[] k_Symbols = { "y = sin x", "x² + y² = r²", "π", "Σ", "∫", "θ", "y = ax + b", "√2", "∞", "(x, y)", "y = x²", "180°", "△", "□", "○" };
        readonly List<(RectTransform rt, TextMeshProUGUI text, float speed, float life)> m_Items = new List<(RectTransform, TextMeshProUGUI, float, float)>();
        TMP_FontAsset m_Font;
        RectTransform m_Area;
        float m_Next;

        public void Init(RectTransform area, TMP_FontAsset font)
        {
            m_Area = area;
            m_Font = font;
        }

        void Update()
        {
            if (Time.unscaledTime > m_Next)
            {
                m_Next = Time.unscaledTime + 0.45f;
                var text = UIUtil.Text(m_Area, m_Font, k_Symbols[Random.Range(0, k_Symbols.Length)], Random.Range(26f, 54f), TextAlignmentOptions.Center);
                text.rectTransform.sizeDelta = new Vector2(400f, 80f);
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(Random.Range(0.05f, 0.95f), 0f);
                text.rectTransform.anchoredPosition = new Vector2(0f, -40f);
                text.color = new Color(1f, 1f, 1f, 0f);
                m_Items.Add((text.rectTransform, text, Random.Range(40f, 90f), 0f));
            }

            for (int i = m_Items.Count - 1; i >= 0; i--)
            {
                var (rt, text, speed, life) = m_Items[i];
                life += Time.unscaledDeltaTime;
                rt.anchoredPosition += Vector2.up * speed * Time.unscaledDeltaTime;
                text.alpha = Mathf.Clamp01(life / 1.5f) * Mathf.Clamp01((9f - life) / 2f) * 0.55f;
                if (life > 9f)
                {
                    Destroy(rt.gameObject);
                    m_Items.RemoveAt(i);
                }
                else
                {
                    m_Items[i] = (rt, text, speed, life);
                }
            }
        }
    }

    static class ScreenCommon
    {
        // 원래 씬의 UI(튜토리얼 이미지·조작키 그림)를 숨긴다
        public static void HideOriginalUI(GameObject keep)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root == keep) continue;
                if (root.GetComponentInChildren<Canvas>(true)) root.SetActive(false);
            }
        }

        public static TMP_FontAsset Font => MathModeAssets.Load() ? MathModeAssets.Load().Font : null;

        public static void Panel(RectTransform rt, Color color)
        {
            var image = rt.GetComponent<Image>();
            image.sprite = UIUtil.Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
        }
    }

    // ================================================================== 시작 메뉴

    public class MainMenuScreen : MonoBehaviour
    {
        MenuBackdrop m_Backdrop;
        TMP_FontAsset m_Font;
        TextMeshProUGUI m_Difficulty, m_Map, m_Controls, m_Sensitivity, m_AutoWeapon, m_AutoSkills, m_AimAssist, m_Info;
        Button m_StartButton;
        int m_ShownTheme = -1;

        void Start()
        {
            ScreenCommon.HideOriginalUI(gameObject);
            UIUtil.EnsureEventSystem();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            m_Font = ScreenCommon.Font;

            m_Backdrop = gameObject.AddComponent<MenuBackdrop>();
            ShowBackdrop();
            BuildUI();
            Refresh();
        }

        void ShowBackdrop()
        {
            int map = GameSettings.Map;
            int theme = map == GameSettings.RandomMap ? Random.Range(0, GameSettings.ThemeCount) : map;
            if (theme == m_ShownTheme && map != GameSettings.RandomMap) return;
            m_ShownTheme = theme;
            m_Backdrop.Show((MapTheme)theme, 1000 + theme * 77);
        }

        void BuildUI()
        {
            Canvas canvas = UIUtil.Canvas(transform, "MainMenu", 10);
            var symbols = new GameObject("Symbols", typeof(RectTransform)).GetComponent<RectTransform>();
            symbols.SetParent(canvas.transform, false);
            UIUtil.Stretch(symbols, 0f);
            gameObject.AddComponent<FloatingSymbols>().Init(symbols, m_Font);

            // 왼쪽 어두운 판
            var side = UIUtil.Image(canvas.transform, "Side", new Color(0.02f, 0.04f, 0.1f, 0.78f));
            side.anchorMin = new Vector2(0f, 0f);
            side.anchorMax = new Vector2(0f, 1f);
            side.pivot = new Vector2(0f, 0.5f);
            side.sizeDelta = new Vector2(720f, 0f);

            var title = UIUtil.Text(side, m_Font, "<b>수학 FPS</b>", 104f, TextAlignmentOptions.Left);
            UIUtil.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(370f, -95f), new Vector2(640f, 120f));
            title.color = new Color(1f, 0.84f, 0.3f);
            title.outlineWidth = 0.15f;
            title.outlineColor = new Color(0.4f, 0.25f, 0f);
            var subtitle = UIUtil.Text(side, m_Font, "<b>좌표 전선</b>", 64f, TextAlignmentOptions.Left);
            UIUtil.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(370f, -185f), new Vector2(640f, 80f));
            var tagline = UIUtil.Text(side, m_Font, "도형 · 좌표평면 · 그래프로 싸우는 수학 슈팅", 26f, TextAlignmentOptions.Left);
            UIUtil.Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(370f, -240f), new Vector2(640f, 40f));
            tagline.color = new Color(0.75f, 0.85f, 1f);

            m_StartButton = UIUtil.Button(side, m_Font, "<b>▶  게임 시작</b>", new Color(0.2f, 0.75f, 0.4f), StartGame, new Vector2(560f, 90f), 42f);
            UIUtil.Place((RectTransform)m_StartButton.transform, new Vector2(0f, 1f), new Vector2(370f, -325f), new Vector2(560f, 90f));

            m_Difficulty = Selector(side, -425f, "난이도", () => CycleDifficulty(-1), () => CycleDifficulty(1));
            m_Map = Selector(side, -499f, "맵", () => CycleMap(-1), () => CycleMap(1));
            m_Controls = Selector(side, -573f, "조작", () => CycleControls(-1), () => CycleControls(1));
            m_Sensitivity = Selector(side, -647f, "감도", () => ChangeSensitivity(-0.1f), () => ChangeSensitivity(0.1f));
            m_AutoWeapon = Selector(side, -721f, "자동 무기", ToggleAutoWeapon, ToggleAutoWeapon);
            m_AutoSkills = Selector(side, -795f, "자동 스킬", ToggleAutoSkills, ToggleAutoSkills);
            m_AimAssist = Selector(side, -869f, "조준 보정", ToggleAimAssist, ToggleAimAssist);

            var infoBox = UIUtil.RoundedImage(side, "Info", new Color(1f, 1f, 1f, 0.07f));
            UIUtil.Place(infoBox, new Vector2(0f, 1f), new Vector2(370f, -980f), new Vector2(560f, 130f));
            m_Info = UIUtil.Text(infoBox, m_Font, "", 22f, TextAlignmentOptions.Left);
            UIUtil.Stretch(m_Info.rectTransform, 16f);
            m_Info.enableAutoSizing = true;
            m_Info.fontSizeMin = 14f;
            m_Info.fontSizeMax = 22f;

            var back = UIUtil.Button(canvas.transform, m_Font, "◀ 모드 선택", new Color(0.25f, 0.35f, 0.55f), BackToModeSelect, new Vector2(240f, 60f), 26f);
            UIUtil.Place((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(-150f, -50f), new Vector2(240f, 60f));

            if (!Application.isMobilePlatform)
            {
                var quit = UIUtil.Button(side, m_Font, "종료", new Color(0.45f, 0.2f, 0.25f), Application.Quit, new Vector2(160f, 54f), 24f);
                UIUtil.Place((RectTransform)quit.transform, new Vector2(1f, 1f), new Vector2(-130f, -50f), new Vector2(200f, 54f));
            }

            var version = UIUtil.Text(canvas.transform, m_Font, "Enter / Space: 시작", 20f, TextAlignmentOptions.Right);
            UIUtil.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-180f, 30f), new Vector2(340f, 30f));
            version.color = new Color(1f, 1f, 1f, 0.6f);
        }

        TextMeshProUGUI Selector(RectTransform parent, float y, string label, UnityEngine.Events.UnityAction prev, UnityEngine.Events.UnityAction next)
        {
            var row = UIUtil.RoundedImage(parent, label, new Color(1f, 1f, 1f, 0.08f));
            UIUtil.Place(row, new Vector2(0f, 1f), new Vector2(370f, y), new Vector2(560f, 66f));

            var name = UIUtil.Text(row, m_Font, label, 26f, TextAlignmentOptions.Left);
            UIUtil.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(130f, 60f));
            name.color = new Color(0.7f, 0.8f, 0.95f);

            var left = UIUtil.Button(row, m_Font, "◀", new Color(0.25f, 0.35f, 0.55f), prev, new Vector2(64f, 60f), 28f);
            UIUtil.Place((RectTransform)left.transform, new Vector2(0f, 0.5f), new Vector2(190f, 0f), new Vector2(64f, 60f));
            var right = UIUtil.Button(row, m_Font, "▶", new Color(0.25f, 0.35f, 0.55f), next, new Vector2(64f, 60f), 28f);
            UIUtil.Place((RectTransform)right.transform, new Vector2(1f, 0.5f), new Vector2(-45f, 0f), new Vector2(64f, 60f));

            var value = UIUtil.Text(row, m_Font, "", 30f, TextAlignmentOptions.Center);
            UIUtil.Place(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(70f, 0f), new Vector2(330f, 60f));
            value.enableAutoSizing = true;
            value.fontSizeMin = 16f;
            value.fontSizeMax = 30f;
            return value;
        }

        void CycleDifficulty(int d)
        {
            GameSettings.Difficulty = (Difficulty)(((int)GameSettings.Difficulty + d + 4) % 4);
            Refresh();
        }

        void CycleMap(int d)
        {
            int count = GameSettings.ThemeCount + 1; // 테마 + 랜덤
            GameSettings.Map = (GameSettings.Map + d + count) % count;
            ShowBackdrop();
            Refresh();
        }

        void CycleControls(int d)
        {
            GameSettings.Controls = (ControlScheme)(((int)GameSettings.Controls + d + 3) % 3);
            Refresh();
        }

        void ToggleAutoWeapon()
        {
            GameSettings.AutoWeapon = !GameSettings.AutoWeapon;
            Refresh();
        }

        void ToggleAutoSkills()
        {
            GameSettings.AutoSkills = !GameSettings.AutoSkills;
            Refresh();
        }

        void ToggleAimAssist()
        {
            GameSettings.AimAssist = !GameSettings.AimAssist;
            Refresh();
        }

        void ChangeSensitivity(float d)
        {
            GameSettings.Sensitivity = Mathf.Round((GameSettings.Sensitivity + d) * 10f) / 10f;
            Refresh();
        }

        void Refresh()
        {
            Difficulty diff = GameSettings.Difficulty;
            string color = GameSettings.DifficultyColor(diff);
            m_Difficulty.text = $"<color={color}><b>{GameSettings.DifficultyName(diff)}</b></color>";
            m_Map.text = $"<b>{GameSettings.MapName(GameSettings.Map)}</b>";
            m_Controls.text = GameSettings.ControlsName(GameSettings.Controls);
            m_Sensitivity.text = $"{GameSettings.Sensitivity:0.0}";
            m_AutoWeapon.text = GameSettings.OnOff(GameSettings.AutoWeapon);
            m_AutoSkills.text = GameSettings.OnOff(GameSettings.AutoSkills);
            m_AimAssist.text = GameSettings.OnOff(GameSettings.AimAssist);
            string mapInfo = GameSettings.Map == GameSettings.RandomMap
                ? "테마와 배치가 매번 달라져요"
                : MapGenerator.Summary((MapTheme)GameSettings.Map);
            m_Info.text = $"{GameSettings.DifficultyDescription(diff)}\n<color=#AAB4C8>맵: {mapInfo}</color>";
        }

        void StartGame()
        {
            SceneManager.LoadScene(AppBootstrap.GameScene);
        }

        void BackToModeSelect()
        {
            AppBootstrap.ClassicMenu = false;
            SceneManager.LoadScene(AppBootstrap.MenuScene);
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                StartGame();
            else if (kb != null && kb.escapeKey.wasPressedThisFrame)
                BackToModeSelect();
        }
    }

    // ================================================================== 결과 화면

    public class ResultScreen : MonoBehaviour
    {
        public bool Win;

        void Start()
        {
            ScreenCommon.HideOriginalUI(gameObject);
            UIUtil.EnsureEventSystem();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            TMP_FontAsset font = ScreenCommon.Font;

            var backdrop = gameObject.AddComponent<MenuBackdrop>();
            backdrop.Speed = 3f;
            backdrop.Height = Win ? 26f : 40f;
            backdrop.Show(GameStats.Theme, GameStats.Seed == 0 ? 1000 + (int)GameStats.Theme * 77 : GameStats.Seed);

            Canvas canvas = UIUtil.Canvas(transform, "Result", 10);
            var dim = UIUtil.Image(canvas.transform, "Dim", Win ? new Color(0f, 0.02f, 0.06f, 0.35f) : new Color(0.12f, 0f, 0.02f, 0.5f));
            UIUtil.Stretch(dim, 0f);
            if (Win) gameObject.AddComponent<Confetti>().Init(canvas.transform);

            var panel = UIUtil.RoundedImage(canvas.transform, "Panel", new Color(0.03f, 0.05f, 0.12f, 0.9f));
            UIUtil.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 760f));

            var title = UIUtil.Text(panel, font, Win ? "<b>임무 완료!</b>" : "<b>아쉽게 실패...</b>", 86f, TextAlignmentOptions.Center);
            UIUtil.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(860f, 110f));
            title.color = Win ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.55f);
            title.outlineWidth = 0.15f;
            title.outlineColor = Color.black;

            var sub = UIUtil.Text(panel, font, Win ? "모든 수학 임무를 해결했어요" : "괜찮아요! 다시 도전하면 더 잘할 수 있어요", 28f, TextAlignmentOptions.Center);
            UIUtil.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(860f, 40f));
            sub.color = new Color(0.8f, 0.88f, 1f);

            bool hasStats = GameStats.StartTime > 0f || GameStats.EndTime > 0f;
            float duration = GameStats.Duration;
            string time = hasStats ? $"{(int)(duration / 60f)}분 {(int)(duration % 60f):00}초" : "-";
            string rows =
                $"맵<pos=52%><b>{MapGenerator.DisplayName(GameStats.Theme)}</b>\n" +
                $"난이도<pos=52%><b><color={GameSettings.DifficultyColor(GameStats.Difficulty)}>{GameSettings.DifficultyName(GameStats.Difficulty)}</color></b>\n" +
                $"걸린 시간<pos=52%><b>{time}</b>\n" +
                $"처치한 적<pos=52%><b>{GameStats.Kills}</b>\n" +
                $"관문 정답 / 오답<pos=52%><b><color=#6BFF8A>{GameStats.Correct}</color> / <color=#FF8A8A>{GameStats.Wrong}</color></b>\n" +
                $"좌표 탐색<pos=52%><b>{GameStats.BeaconsFound}</b>\n" +
                $"궁극기 사용<pos=52%><b>{GameStats.Ultimates}</b>";
            var stats = UIUtil.Text(panel, font, rows, 30f, TextAlignmentOptions.TopLeft);
            UIUtil.Place(stats.rectTransform, new Vector2(0.5f, 1f), new Vector2(-90f, -400f), new Vector2(560f, 330f));
            stats.lineSpacing = 12f;
            stats.enableAutoSizing = true;
            stats.fontSizeMin = 16f;
            stats.fontSizeMax = 30f;

            if (Win)
            {
                string grade = GameStats.Grade(true);
                var gradeCircle = UIUtil.RoundedImage(panel, "Grade", new Color(1f, 0.8f, 0.25f, 0.95f), circle: true);
                UIUtil.Place(gradeCircle, new Vector2(0.5f, 1f), new Vector2(300f, -330f), new Vector2(180f, 180f));
                var gradeText = UIUtil.Text(gradeCircle, font, $"<b>{grade}</b>", 110f, TextAlignmentOptions.Center);
                UIUtil.Stretch(gradeText.rectTransform, 0f);
                gradeText.color = new Color(0.25f, 0.12f, 0f);
                var gradeLabel = UIUtil.Text(panel, font, "등급", 24f, TextAlignmentOptions.Center);
                UIUtil.Place(gradeLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, -440f), new Vector2(200f, 30f));
            }

            float y = 70f;
            var retry = UIUtil.Button(panel, font, "<b>다시 하기</b>", new Color(0.2f, 0.7f, 0.4f),
                () => SceneManager.LoadScene(AppBootstrap.GameScene), new Vector2(260f, 84f), 32f);
            UIUtil.Place((RectTransform)retry.transform, new Vector2(0.5f, 0f), new Vector2(-145f, y), new Vector2(260f, 84f));
            var menu = UIUtil.Button(panel, font, "메인 메뉴", new Color(0.25f, 0.35f, 0.6f),
                () => SceneManager.LoadScene(AppBootstrap.MenuScene), new Vector2(260f, 84f), 32f);
            UIUtil.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(145f, y), new Vector2(260f, 84f));

            if (!Win && GameSettings.Difficulty != Difficulty.Easy)
            {
                var easier = UIUtil.Button(panel, font, "한 단계 쉽게 다시", new Color(0.7f, 0.5f, 0.15f), () =>
                {
                    GameSettings.Difficulty = GameSettings.Difficulty - 1;
                    SceneManager.LoadScene(AppBootstrap.GameScene);
                }, new Vector2(560f, 64f), 26f);
                UIUtil.Place((RectTransform)easier.transform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(560f, 64f));
            }
        }
    }

    // 승리 화면의 색종이
    public class Confetti : MonoBehaviour
    {
        readonly List<(RectTransform rt, Vector2 v, float spin)> m_Pieces = new List<(RectTransform, Vector2, float)>();
        Transform m_Canvas;
        float m_Next;

        public void Init(Transform canvas) => m_Canvas = canvas;

        void Update()
        {
            if (Time.unscaledTime > m_Next && m_Pieces.Count < 120)
            {
                m_Next = Time.unscaledTime + 0.04f;
                Color c = Color.HSVToRGB(Random.value, 0.7f, 1f);
                var rt = UIUtil.Image(m_Canvas, "Confetti", c);
                rt.anchorMin = rt.anchorMax = new Vector2(Random.value, 1f);
                rt.anchoredPosition = new Vector2(0f, 20f);
                rt.sizeDelta = new Vector2(Random.Range(10f, 18f), Random.Range(16f, 26f));
                rt.SetAsFirstSibling();
                m_Pieces.Add((rt, new Vector2(Random.Range(-60f, 60f), -Random.Range(150f, 300f)), Random.Range(-360f, 360f)));
            }

            for (int i = m_Pieces.Count - 1; i >= 0; i--)
            {
                var (rt, v, spin) = m_Pieces[i];
                rt.anchoredPosition += v * Time.unscaledDeltaTime;
                rt.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
                if (rt.anchoredPosition.y < -1200f)
                {
                    Destroy(rt.gameObject);
                    m_Pieces.RemoveAt(i);
                }
            }
        }
    }
}
