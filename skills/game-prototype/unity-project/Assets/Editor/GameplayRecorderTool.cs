/*
 * GameplayRecorderTool.cs  —  Official Unity Recorder-based gameplay capture
 * Namespace: MelonS.GameProto.EditorTools
 *
 * USAGE (one command, any time):
 *   python G:/ai/MelonS-Agents/skills/game-dev-agent/scripts/record-gameplay.py
 *   python G:/ai/MelonS-Agents/skills/game-dev-agent/scripts/record-gameplay.py --seconds 30
 *   python G:/ai/MelonS-Agents/skills/game-dev-agent/scripts/record-gameplay.py --seconds 120 --out G:/ai/pawnsim_gameplay.mp4
 *
 *   OR via agent.py integrate shortcut:
 *   set MELONS_REC_SECONDS=30 && python skills/game-dev-agent/scripts/agent.py integrate --project ... --method record
 *
 * ENV VARS (all optional):
 *   MELONS_REC_SECONDS   — recording duration in seconds (default 120)
 *   MELONS_REC_OUT       — absolute output mp4 path (default G:/ai/pawnsim_gameplay.mp4)
 *   MELONS_REC_FPS       — capture frame rate (default 30)
 *
 * BATCHMODE DESIGN (key constraints overcome here):
 *
 *   (A) NO -nographics: Recorder captures the Game View via the rendering
 *       pipeline.  -nographics disables rendering entirely → black output.
 *
 *   (B) NO -quit: -quit tells Unity to exit after executeMethod returns,
 *       before any callbacks fire.  EditorApplication.Exit() is called from
 *       within this tool when done.
 *
 *   (C) Domain reload survives via EditorPrefs + [InitializeOnEnterPlayMode]:
 *       Unity reloads the scripting domain when entering play mode, wiping all
 *       static state and EditorApplication.update subscriptions.
 *       [InitializeOnEnterPlayMode] fires AFTER the reload; we re-read config
 *       from EditorPrefs (which persists across reload) and re-subscribe.
 *
 *   (D) [InitializeOnEnterPlayMode] only fires when entering play mode,
 *       so the recorder start logic is guaranteed to run at the right time.
 *
 * REQUIRES:  com.unity.recorder 5.1.0  (manifest.json dependency)
 */
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MelonS.GameProto.EditorTools
{
    public static class GameplayRecorderTool
    {
        // ----- EditorPrefs keys (persist across domain reload) ------------
        private const string PrefEnabled     = "MELONS_REC_active";
        private const string PrefSeconds     = "MELONS_REC_seconds";
        private const string PrefOut         = "MELONS_REC_out";
        private const string PrefFps         = "MELONS_REC_fps";

        // ----- defaults ---------------------------------------------------
        private const string GameScenePath  = "Assets/Scenes/Game.unity";
        private const int    DefaultSeconds = 120;
        private const int    DefaultFps     = 30;
        private const string DefaultOut     = "G:/ai/pawnsim_gameplay.mp4";

        // ----- in-domain state (after reload) -----------------------------
        private static RecorderController _controller;
        private static double             _recordingStartTime;
        private static int                _durationSec;
        private static string             _outPath;
        private static bool               _stopping;

        // -------------------------------------------------------------------
        /// <summary>
        /// Batchmode entry point.
        ///   Unity.exe -batchmode -executeMethod MelonS.GameProto.EditorTools.GameplayRecorderTool.Record
        ///              -projectPath ... -logFile ...
        /// (NO -quit, NO -nographics)
        /// </summary>
        [MenuItem("MelonS/Record Gameplay (Recorder)")]
        public static void Record()
        {
            // --- read config from env vars --------------------------------
            int durationSec = DefaultSeconds;
            string secEnv = Environment.GetEnvironmentVariable("MELONS_REC_SECONDS");
            if (!string.IsNullOrEmpty(secEnv) && int.TryParse(secEnv, out int sv))
                durationSec = sv;

            int fps = DefaultFps;
            string fpsEnv = Environment.GetEnvironmentVariable("MELONS_REC_FPS");
            if (!string.IsNullOrEmpty(fpsEnv) && int.TryParse(fpsEnv, out int fv))
                fps = fv;

            string outPath = (Environment.GetEnvironmentVariable("MELONS_REC_OUT") ?? DefaultOut)
                             .Replace('\\', '/');

            string outDir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            Debug.Log($"[GameplayRecorderTool] duration={durationSec}s  fps={fps}  out={outPath}");

            // --- persist config to EditorPrefs so it survives domain reload
            EditorPrefs.SetBool(PrefEnabled, true);
            EditorPrefs.SetInt (PrefSeconds, durationSec);
            EditorPrefs.SetString(PrefOut,  outPath);
            EditorPrefs.SetInt (PrefFps,    fps);

            // --- open Game scene ------------------------------------------
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Debug.Log($"[GameplayRecorderTool] opened: {GameScenePath}");

            // --- enter Play mode ------------------------------------------
            // Domain reload fires next.  OnEnterPlayMode() is called AFTER
            // the reload completes, at which point isPlaying is true and
            // MonoBehaviours have started.
            EditorApplication.EnterPlaymode();
            Debug.Log("[GameplayRecorderTool] EnterPlaymode() called — domain reload imminent.");
        }

        // -------------------------------------------------------------------
        // Called by Unity AFTER the domain reload that occurs when entering
        // play mode.  NOTE: isPlaying may still be false at this exact moment
        // (the transition isn't complete).  We register an update callback here;
        // OnEditorUpdate polls isPlaying and starts the recorder on the first
        // frame where play mode is confirmed active.
        // Static state survives because there are no further domain reloads
        // within a single play-mode session.
        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode(EnterPlayModeOptions options)
        {
            // Only act when OUR recording session triggered play mode
            if (!EditorPrefs.GetBool(PrefEnabled, false)) return;

            _durationSec = EditorPrefs.GetInt(PrefSeconds, DefaultSeconds);
            _outPath     = EditorPrefs.GetString(PrefOut,  DefaultOut);
            int fps      = EditorPrefs.GetInt(PrefFps,     DefaultFps);
            _stopping    = false;
            _recordingStartTime = 0;

            Debug.Log($"[GameplayRecorderTool] OnEnterPlayMode — building recorder ({_durationSec}s, {fps}fps, {_outPath})");

            // Build recorder settings — do NOT call PrepareRecording yet;
            // that requires isPlaying == true, which may not hold yet.
            var ctrlSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            ctrlSettings.SetRecordModeToManual();
            ctrlSettings.FrameRate    = fps;
            ctrlSettings.CapFrameRate = true;   // 1x real-time

            var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name    = "PawnSim Gameplay";
            movieSettings.Enabled = true;
            movieSettings.EncoderSettings = new CoreEncoderSettings
            {
                Codec           = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            movieSettings.AudioInputSettings.PreserveAudio = true;
            movieSettings.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth  = 1920,
                OutputHeight = 1080,
            };

            string outNoExt = _outPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                ? _outPath.Substring(0, _outPath.Length - 4)
                : _outPath;
            movieSettings.OutputFile = outNoExt;

            ctrlSettings.AddRecorderSettings(movieSettings);
            _controller = new RecorderController(ctrlSettings);

            // Register update callback — it will start recording on the first
            // frame where isPlaying is actually true.
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            Debug.Log("[GameplayRecorderTool] EditorApplication.update registered — waiting for isPlaying.");
        }

        // For rate-limiting the progress log (one line per 10s bracket)
        private static int _lastLoggedBracket = -1;

        // -------------------------------------------------------------------
        private static void OnEditorUpdate()
        {
            if (_stopping) return;

            // Phase A: wait until isPlaying is true, then start recording
            if (_recordingStartTime <= 0)
            {
                if (!EditorApplication.isPlaying) return;  // not in play mode yet

                // Play mode confirmed — now it's safe to call PrepareRecording
                try
                {
                    _controller.PrepareRecording();
                    _controller.StartRecording();
                    _recordingStartTime = EditorApplication.timeSinceStartup;
                    _lastLoggedBracket  = -1;
                    Debug.Log($"[GameplayRecorderTool] Recording STARTED (editor t={_recordingStartTime:F1}s, duration={_durationSec}s)");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameplayRecorderTool] StartRecording FAILED: {ex}");
                    _stopping = true;
                    EditorApplication.update -= OnEditorUpdate;
                    CleanupPrefs();
                    EditorApplication.ExitPlaymode();
                    EditorApplication.update += WaitForEditModeAndQuit;
                }
                return;
            }

            // Phase B: recording — check elapsed time
            double elapsed = EditorApplication.timeSinceStartup - _recordingStartTime;

            // Log once per 10s bracket (rate-limited to avoid log spam)
            int bracket = (int)(elapsed / 10);
            if (bracket > _lastLoggedBracket && bracket > 0)
            {
                _lastLoggedBracket = bracket;
                Debug.Log($"[GameplayRecorderTool] elapsed={bracket * 10}s / {_durationSec}s");
            }

            if (elapsed < _durationSec) return;

            // Duration reached — stop recorder and exit
            _stopping = true;
            EditorApplication.update -= OnEditorUpdate;

            Debug.Log($"[GameplayRecorderTool] Duration {_durationSec}s reached (elapsed={elapsed:F1}s). Stopping.");
            _controller.StopRecording();

            // Exit play mode; WaitForEditModeAndQuit will pick up after the transition
            EditorApplication.update += WaitForEditModeAndQuit;
            EditorApplication.ExitPlaymode();
        }

        // -------------------------------------------------------------------
        private static void WaitForEditModeAndQuit()
        {
            if (EditorApplication.isPlaying) return;  // still transitioning

            EditorApplication.update -= WaitForEditModeAndQuit;

            CleanupPrefs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Confirm output file
            string outNoExt = _outPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                ? _outPath.Substring(0, _outPath.Length - 4)
                : _outPath;

            string[] candidates = { _outPath, outNoExt + "_0001.mp4", outNoExt + ".mp4" };

            bool found = false;
            foreach (string c in candidates)
            {
                if (File.Exists(c))
                {
                    long kb = new FileInfo(c).Length / 1024;
                    Debug.Log($"[GameplayRecorderTool] OUTPUT OK: {c}  ({kb} KB)");
                    found = true;
                    break;
                }
            }
            if (!found)
                Debug.LogWarning($"[GameplayRecorderTool] WARNING: mp4 not found. Checked: {string.Join(", ", candidates)}");

            Debug.Log($"[GameplayRecorderTool] Done. Exit {(found ? 0 : 1)}");
            EditorApplication.Exit(found ? 0 : 1);
        }

        // -------------------------------------------------------------------
        private static void CleanupPrefs()
        {
            EditorPrefs.DeleteKey(PrefEnabled);
            EditorPrefs.DeleteKey(PrefSeconds);
            EditorPrefs.DeleteKey(PrefOut);
            EditorPrefs.DeleteKey(PrefFps);
        }
    }
}
