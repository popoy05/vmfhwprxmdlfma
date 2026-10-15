using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Tools > Math Mode > 빌드
// Windows(.exe)와 Android(.apk)를 각각 한 번에 빌드한다. 둘 다 항상 전체화면, Android는 가로 화면 고정.
public static class MathModeBuild
{
    const string k_ProductName = "수학 FPS 좌표 전선";
    const string k_AndroidId = "com.mathfps.coordinatefront";

    [MenuItem("Tools/Math Mode/빌드/Windows 빌드 (.exe)", false, 100)]
    static void BuildWindows()
    {
        if (!CheckModule(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, "Windows Build Support")) return;
        ApplyCommon();
        ApplyWindows();
        Build(BuildTarget.StandaloneWindows64, "Builds/Windows/MathFPS.exe");
    }

    [MenuItem("Tools/Math Mode/빌드/Android 빌드 (.apk)", false, 101)]
    static void BuildAndroid()
    {
        if (!CheckModule(BuildTargetGroup.Android, BuildTarget.Android, "Android Build Support (OpenJDK, SDK & NDK 포함)")) return;
        ApplyCommon();
        ApplyAndroid();
        Build(BuildTarget.Android, "Builds/Android/MathFPS.apk");
    }

    [MenuItem("Tools/Math Mode/빌드/설정만 적용 (전체화면·가로화면·장면 목록)", false, 120)]
    static void ApplyAll()
    {
        ApplyCommon();
        ApplyWindows();
        ApplyAndroid();
        Debug.Log("[MathMode] 빌드 설정 적용 완료: Windows 전체화면, Android 가로 전체화면, 장면 목록 정리");
    }

    static bool CheckModule(BuildTargetGroup group, BuildTarget target, string moduleName)
    {
        if (BuildPipeline.IsBuildTargetSupported(group, target)) return true;
        EditorUtility.DisplayDialog("빌드 모듈 없음",
            $"이 Unity에는 '{moduleName}' 모듈이 설치되어 있지 않습니다.\n\nUnity Hub > 설치 > 6000.5.2f1 의 톱니바퀴 > 모듈 추가 에서 설치한 뒤 다시 시도하세요.",
            "확인");
        return false;
    }

    static void ApplyCommon()
    {
        PlayerSettings.productName = k_ProductName;

        // 장면 순서: 시작 메뉴 → 게임 → 결과
        string[] order =
        {
            "Assets/FPS/Scenes/IntroMenu.unity", "Assets/FPS/Scenes/MainScene.unity",
            "Assets/FPS/Scenes/WinScene.unity", "Assets/FPS/Scenes/LoseScene.unity",
        };
        EditorBuildSettings.scenes = order.Where(File.Exists).Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
    }

    static void ApplyWindows()
    {
        // 항상 전체화면 (테두리 없는 창 모드 전체화면, 모니터 해상도 그대로)
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.resizableWindow = false;
        PlayerSettings.runInBackground = false;
    }

    static void ApplyAndroid()
    {
        // 가로 화면만, 상태 표시줄 없는 전체화면
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
#pragma warning disable CS0618 // 구식 API지만 상태 표시줄 없는 전체화면 시작에 여전히 쓰인다
        PlayerSettings.Android.startInFullscreen = true;
#pragma warning restore CS0618
        PlayerSettings.Android.renderOutsideSafeArea = true;

        string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        if (string.IsNullOrEmpty(id) || id.Contains("DefaultCompany"))
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, k_AndroidId);

        // 요즘 안드로이드 기기(64비트)에서 돌도록
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = false; // .apk 로 바로 설치할 수 있게
    }

    static void Build(BuildTarget target, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = path,
            target = target,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[MathMode] 빌드 성공: {path} ({report.summary.totalSize / (1024 * 1024)} MB)");
            EditorUtility.RevealInFinder(path);
        }
        else
        {
            Debug.LogError($"[MathMode] 빌드 실패 ({report.summary.result}). Console 의 빨간 오류를 확인하세요.");
        }
    }
}
