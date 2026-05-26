using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MelonS.GameProto.EditorTools
{
    /// <summary>
    /// Headless build runner.  Invoked from CLI:
    ///   Unity.exe -batchmode -quit -projectPath ... -executeMethod
    ///   MelonS.GameProto.EditorTools.BuildScript.BuildWindows
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("MelonS/Build Windows (current day)")]
        public static void BuildWindows()
        {
            string buildDir = "../builds";  // sibling to unity-project
            Directory.CreateDirectory(buildDir);
            string ts = System.DateTime.Now.ToString("yyyy-MM-dd");
            // Day index derived from env var, fallback to 'X'
            string day = System.Environment.GetEnvironmentVariable("MELONS_BUILD_DAY") ?? "X";
            string buildName = $"day-{day}-{ts}";
            string outDir = Path.Combine(buildDir, buildName);
            Directory.CreateDirectory(outDir);
            string outExe = Path.Combine(outDir, "PawnSim.exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[]
                {
                    "Assets/Scenes/MainMenu.unity",
                    "Assets/Scenes/Game.unity",
                },
                locationPathName = outExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            Debug.Log($"[BuildScript] building -> {outExe}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] OK ({report.summary.totalSize / 1024 / 1024} MB)");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[BuildScript] FAIL: {report.summary.result}");
                EditorApplication.Exit(1);
            }
        }
    }
}
