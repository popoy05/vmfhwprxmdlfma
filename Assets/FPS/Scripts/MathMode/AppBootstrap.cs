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
        public const string NewModeScene = "NewModeScene";

        // true면 IntroMenu에서 바로 이전 모드(수학 FPS) 메뉴를, false면 모드 선택 화면을 보여준다.
        // 이전 모드를 고른 뒤에는 결과 화면의 "메인 메뉴"가 수학 FPS 메뉴로 돌아오도록 유지된다.
        public static bool ClassicMenu;

        static bool s_MenuShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            s_MenuShown = false;
            ClassicMenu = false;
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

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            // 사용자가 창 모드로 바꿨더라도 장면이 바뀔 때마다 전체화면으로 되돌린다
            if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow) ApplyScreenMode();
#endif
            Time.timeScale = 1f;

            // 게임 장면에서 바로 켜졌다면(에디터에서 MainScene을 열고 플레이 등) 먼저 메인 메뉴로 보낸다
            if (scene.name == GameScene && !s_MenuShown)
            {
                s_MenuShown = true;
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
