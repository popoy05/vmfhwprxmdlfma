using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.FPS.MathMode
{
    // 앱이 켜질 때 한 번 실행된다 (Windows·Android 빌드와 에디터 모두).
    //  - 항상 전체화면 (Windows: 테두리 없는 전체화면 / Android: 가로 몰입 모드)
    //  - 시작 메뉴(IntroMenu)와 결과 화면(WinScene/LoseScene)을 수학 모드용 화면으로 바꿔 끼운다
    public static class AppBootstrap
    {
        public const string MenuScene = "IntroMenu";
        public const string GameScene = "MainScene";
        public const string NewModeArenaScene = "NewModeArena";   // 새 모드: 중세 아레나 (Medieval Arena Mini)
        public const string NewModeCharacterScene = "NewModeCharacter"; // 새 모드: 캐릭터 꾸미기 (맵 선택 전)

        // (예전 맵 선택 단계용 — 지금은 맵이 하나라 쓰지 않음)
        public static bool NewModeMapSelect;

        // true면 IntroMenu에서 바로 이전 모드(수학 FPS) 메뉴를, false면 모드 선택 화면을 보여준다.
        // 이전 모드를 고른 뒤에는 결과 화면의 "메인 메뉴"가 수학 FPS 메뉴로 돌아오도록 유지된다.
        public static bool ClassicMenu;

        static bool s_MenuShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            s_MenuShown = false;
            s_HandledFirstScene = false;
            ClassicMenu = false;
            NewModeMapSelect = false;
            Application.targetFrameRate = 60;
            ApplyScreenMode();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static void ApplyScreenMode()
        {
#if UNITY_ANDROID || UNITY_IOS
            Screen.fullScreen = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
#elif UNITY_STANDALONE && !UNITY_EDITOR
            Resolution native = Screen.currentResolution;
            int width = Display.main.systemWidth > 0 ? Display.main.systemWidth : native.width;
            int height = Display.main.systemHeight > 0 ? Display.main.systemHeight : native.height;
            Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
#endif
        }

        static bool s_HandledFirstScene;

        // 처음 켜진 장면은 sceneLoaded 이벤트가 오지 않는 경우가 있다 (에디터에서 플레이를 누를 때 등).
        // 그래서 첫 장면을 다 불러온 뒤 한 번 더 확인해서, 아직 처리 안 됐으면 직접 처리한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterFirstSceneLoad()
        {
            if (!s_HandledFirstScene) OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 패키지·에디터가 겹쳐 불러오는(Additive) 장면은 무시한다 — 우리 화면 전환과 상관없음
            if (mode != LoadSceneMode.Single) return;
            s_HandledFirstScene = true;
#if UNITY_STANDALONE && !UNITY_EDITOR
            // 사용자가 창 모드로 바꿨더라도 장면이 바뀔 때마다 전체화면으로 되돌린다
            if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow) ApplyScreenMode();
#endif
            Time.timeScale = 1f;

            // 처음 켜졌을 때는 어떤 장면에서 시작했든(에디터에서 새 모드 맵·캐릭터 씬을 열고 플레이 등) 먼저 시작 화면으로 보낸다
            if (!s_MenuShown && scene.name != MenuScene)
            {
                s_MenuShown = true;
                ClassicMenu = false;
                NewModeMapSelect = false;
                SceneManager.LoadScene(MenuScene);
                return;
            }

            switch (scene.name)
            {
                case MenuScene:
                    s_MenuShown = true;
                    if (ClassicMenu) new GameObject("MathMode MainMenu").AddComponent<MainMenuScreen>();
                    else new GameObject("Mode Select").AddComponent<ModeSelectScreen>();
                    break;
                case "WinScene":
                    new GameObject("MathMode Result").AddComponent<ResultScreen>().Win = true;
                    break;
                case "LoseScene":
                    new GameObject("MathMode Result").AddComponent<ResultScreen>().Win = false;
                    break;
            }
        }
    }
}
