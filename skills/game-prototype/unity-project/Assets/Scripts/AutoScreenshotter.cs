using System.Collections;
using System.IO;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Self-validation harness — when present in a scene, automatically
    /// captures a screenshot N seconds after start and quits the app.
    /// Used by autonomous agent verification loop:
    ///   1. add to scene
    ///   2. launch .exe
    ///   3. .exe self-screenshots + self-quits
    ///   4. agent reads the screenshot file + verifies content
    /// </summary>
    public class AutoScreenshotter : MonoBehaviour
    {
        public float delaySeconds = 0f;
        public string outputPath = "";

        private void Start()
        {
            // Only fire when CLI args explicitly request a screenshot.
            // Without args (double-click play), do NOTHING — game runs normally.
            // Args:  PawnSim.exe -screenshot G:\path\out.png -delay 3.0
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
            // CRITICAL: when QA runs in headless/background mode (window loses
            // focus to the python launcher's console), Unity by default pauses
            // Update entirely (ProjectSettings runInBackground=0).  This caused
            // qa.py --delay 30s+ runs to silently never produce a screenshot
            // because the WaitForSeconds coroutine froze.  Force runInBackground
            // ON for any CLI-driven QA session so the timer always elapses.
            Application.runInBackground = true;
            Debug.Log($"[AutoScreenshotter] will capture in {delaySeconds}s -> {outputPath} (runInBackground=ON)");
            StartCoroutine(CaptureAndQuit());
        }

        private IEnumerator CaptureAndQuit()
        {
            yield return new WaitForSeconds(delaySeconds);

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            ScreenCapture.CaptureScreenshot(outputPath);
            Debug.Log($"[AutoScreenshotter] captured -> {outputPath}");

            // Wait a few frames for the screenshot file to flush
            yield return new WaitForSeconds(1.5f);

            Debug.Log("[AutoScreenshotter] quitting...");
            Application.Quit();
        }
    }
}
