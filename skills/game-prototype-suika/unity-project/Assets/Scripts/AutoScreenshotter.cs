using System.Collections;
using System.IO;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Reads -delay + -screenshot CLI args, waits, captures PNG,
    /// auto-quits.  Required for agent-side qa.py self-verification.</summary>
    public class AutoScreenshotter : MonoBehaviour
    {
        private float delaySeconds = 0f;
        private string outputPath = "";

        private void Start()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-delay") float.TryParse(args[i + 1], out delaySeconds);
                else if (args[i] == "-screenshot") outputPath = args[i + 1];
            }
            if (delaySeconds > 0f && !string.IsNullOrEmpty(outputPath))
                StartCoroutine(CaptureAndQuit());
        }

        private IEnumerator CaptureAndQuit()
        {
            yield return new WaitForSeconds(delaySeconds);
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(outputPath);
            yield return new WaitForSeconds(1.5f);
            Application.Quit();
        }
    }
}
