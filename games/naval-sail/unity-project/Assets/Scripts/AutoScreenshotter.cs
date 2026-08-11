using System.Collections;
using System.IO;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Self-validation harness — captures a screenshot N seconds after start
    /// and quits. Minimal standalone version for this project (no PawnSim
    /// diagnostic flags — those lived in a different, PawnSim-specific copy
    /// under skills/game-prototype/unity-project/, which this project does
    /// not depend on now that the two are separate Unity projects).
    ///   .exe -screenshot <path> -delay <seconds>
    /// </summary>
    public class AutoScreenshotter : MonoBehaviour
    {
        public float delaySeconds = 0f;
        public string outputPath = "";

        private void Start()
        {
            bool requested = false;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-screenshot" && i + 1 < args.Length)
                {
                    outputPath = args[i + 1];
                    requested = true;
                }
                if (args[i] == "-delay" && i + 1 < args.Length
                    && float.TryParse(args[i + 1], out float d))
                {
                    delaySeconds = d;
                    requested = true;
                }
            }
            if (!requested || delaySeconds <= 0f || string.IsNullOrEmpty(outputPath))
            {
                Debug.Log("[AutoScreenshotter] no CLI args -> play mode (no auto-quit)");
                return;
            }
            Application.runInBackground = true;
            Debug.Log($"[AutoScreenshotter] will capture in {delaySeconds}s -> {outputPath}");
            StartCoroutine(CaptureAndQuit());
        }

        private IEnumerator CaptureAndQuit()
        {
            yield return new WaitForSecondsRealtime(delaySeconds);

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            ScreenCapture.CaptureScreenshot(outputPath);
            Debug.Log($"[AutoScreenshotter] captured -> {outputPath}");

            yield return new WaitForSecondsRealtime(1.5f);

            Debug.Log("[AutoScreenshotter] quitting...");
            Application.Quit();
        }
    }
}
