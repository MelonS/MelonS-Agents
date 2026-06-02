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
        private bool openSettings = false;  // 진단용: 캡처 직전 설정 메뉴를 연다 (-opensettings)
        private bool testSave = false;      // 진단용: 캡처 직전 저장을 트리거 (-testsave, #44 검증)
        private bool inspectPawn = false;   // 진단용: 첫 림 선택해 인스펙트 패널 표시 (-inspectpawn)

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
                if (args[i] == "-opensettings") openSettings = true;
                if (args[i] == "-testsave") testSave = true;
                if (args[i] == "-inspectpawn") inspectPawn = true;
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

            // 진단(-opensettings): 캡처 직전 설정 메뉴를 열어 레이아웃을 확인.
            if (openSettings)
            {
                MelonS.GameProto.SettingsMenu.Open();
                yield return new WaitForSeconds(0.4f);
            }

            // 진단(-inspectpawn): 첫 림을 선택해 인스펙트 패널을 띄운다(UI 검증).
            if (inspectPawn)
            {
                var sel = Object.FindFirstObjectByType<MelonS.GameProto.ClickSelector>();
                if (sel != null) sel.DiagnosticInspectFirstPawn();
                yield return new WaitForSeconds(0.5f);
            }

            // 진단(-testsave, #44): GameSaveButtons 호스트를 찾아 저장을 트리거 → save.json 작성 검증.
            if (testSave)
            {
                var gsb = Object.FindFirstObjectByType<MelonS.GameProto.GameSaveButtons>(FindObjectsInactive.Include);
                if (gsb != null) { gsb.OnSave(); Debug.Log("[AutoScreenshotter] -testsave: GameSaveButtons.OnSave() 호출"); }
                else Debug.LogError("[AutoScreenshotter] -testsave: GameSaveButtons 호스트 없음!");
                yield return new WaitForSeconds(0.5f);
            }

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
