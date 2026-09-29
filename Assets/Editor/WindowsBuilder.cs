using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Windows 64비트 실행 파일(JumpGirl.exe)을 만들고 배포용 zip으로 묶는다.
/// 결과: Builds/Windows/JumpGirl.exe, Builds/JumpGirl_Windows.zip (깃 제외)
/// 메뉴: Tools > Voxel > Build Windows
/// </summary>
public static class WindowsBuilder
{
    const string ProductName = "JumpGirl";
    const string OutputDir = "Builds/Windows";
    const string ZipPath = "Builds/JumpGirl_Windows.zip";
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Tools/Voxel/Build Windows")]
    public static void BuildMenu()
    {
        var ok = Build();
        EditorUtility.DisplayDialog("Build Windows", ok ? "Build complete: " + ZipPath : "Build failed. See Console.", "OK");
        if (ok) EditorUtility.RevealInFinder(ZipPath);
    }

    public static void ApplyPlayerSettings()
    {
        PlayerSettings.productName = ProductName;
        // 사용자 결정: 전체 화면(창 없는 전체 화면), 해상도는 모니터에 맞춤. Alt+Enter로 창 모드 전환 가능
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        AssetDatabase.SaveAssets();
    }

    public static bool Build()
    {
        ApplyPlayerSettings();

        if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);
        Directory.CreateDirectory(OutputDir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = Path.Combine(OutputDir, ProductName + ".exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[WindowsBuilder] 결과 {summary.result}, 크기 {summary.totalSize / (1024 * 1024)} MB, 경고 {summary.totalWarnings}, 에러 {summary.totalErrors}");
        if (summary.result != BuildResult.Succeeded) return false;

        // 배포에 필요 없는 디버그 폴더 제거
        foreach (var dir in Directory.GetDirectories(OutputDir).Where(d => d.EndsWith("_BurstDebugInformation_DoNotShip")))
            Directory.Delete(dir, true);

        if (File.Exists(ZipPath)) File.Delete(ZipPath);
        ZipFile.CreateFromDirectory(OutputDir, ZipPath, System.IO.Compression.CompressionLevel.Optimal, false);
        Debug.Log($"[WindowsBuilder] zip 생성: {ZipPath} ({new FileInfo(ZipPath).Length / (1024 * 1024)} MB)");
        return true;
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            EditorApplication.Exit(Build() ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
