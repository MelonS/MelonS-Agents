using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MelonS.GameProto.EditorTools
{
    /// <summary>
    /// 헤드리스 빌드. batchmode -executeMethod 로 호출.
    ///
    /// Invoked from CLI:
    ///   Unity.exe -batchmode -quit -projectPath ... -executeMethod
    ///   MelonS.GameProto.EditorTools.NavalBuildScript.BuildVerify
    /// </summary>
    public static class NavalBuildScript
    {
        [MenuItem("MelonS/Naval/Build Verify (Windows)")]
        public static void BuildVerify()
        {
            string buildDir = "../builds/verify";
            Directory.CreateDirectory(buildDir);
            string outExe = Path.Combine(buildDir, "NavalProto.exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/OceanPrototype.unity" },
                locationPathName = outExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            Debug.Log($"[NavalBuildScript] building -> {outExe}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[NavalBuildScript] OK ({report.summary.totalSize / 1024 / 1024} MB)");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[NavalBuildScript] FAIL: {report.summary.result}");
                EditorApplication.Exit(1);
            }
        }
    }
}
