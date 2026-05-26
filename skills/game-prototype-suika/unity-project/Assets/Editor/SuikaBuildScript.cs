using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MelonS.GameProto.EditorTools
{
    public static class SuikaBuildScript
    {
        public static void BuildWindows()
        {
            string buildDir = "../builds";
            Directory.CreateDirectory(buildDir);
            string ts = System.DateTime.Now.ToString("yyyy-MM-dd");
            string day = System.Environment.GetEnvironmentVariable("MELONS_BUILD_DAY") ?? "X";
            string buildName = $"day-{day}-{ts}";
            string outDir = Path.Combine(buildDir, buildName);
            Directory.CreateDirectory(outDir);
            string outExe = Path.Combine(outDir, "SuikaLite.exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity" },
                locationPathName = outExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            Debug.Log($"[SuikaBuildScript] -> {outExe}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[SuikaBuildScript] OK ({report.summary.totalSize / 1024 / 1024} MB)");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[SuikaBuildScript] FAIL: {report.summary.result}");
                EditorApplication.Exit(1);
            }
        }

        public static void BuildVerify()
        {
            string buildDir = "../builds";
            Directory.CreateDirectory(buildDir);
            string outDir = Path.Combine(buildDir, "verify");
            Directory.CreateDirectory(outDir);
            string outExe = Path.Combine(outDir, "SuikaLite.exe");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity" },
                locationPathName = outExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            Debug.Log($"[SuikaBuildScript] verify -> {outExe}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
