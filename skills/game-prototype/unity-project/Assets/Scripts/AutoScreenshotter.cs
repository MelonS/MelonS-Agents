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
        public float delaySeconds = 2.5f;
        public string outputPath = "G:/ai/_screenshots/test.png";

        private void Start()
        {
            // Allow override via command-line:
            //   PawnSim.exe -screenshot G:\path\out.png -delay 3.0
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-screenshot" && i + 1 < args.Length)
                {
                    outputPath = args[i + 1];
                }
                if (args[i] == "-delay" && i + 1 < args.Length
                    && float.TryParse(args[i + 1], out float d))
                {
                    delaySeconds = d;
                }
            }
            Debug.Log($"[AutoScreenshotter] will capture in {delaySeconds}s -> {outputPath}");
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
