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
 * FIX LOG (operator feedback v2):
 *   Fix 1 — TRUE 1x speed: GameClock was set to 60 game-min/real-sec (= 5
 *     in-game days per 2 real minutes).  At record-start we reflect into
 *     GameClock.inGameMinutesPerRealSecond = 6f, giving 6 game-min/real-sec
 *     = ~0.5 in-game day per 2 real minutes.  TimeController is forced to 1x.
 *   Fix 2 — Zoom out: camera orthographicSize set to 12 at record-start so all
 *     three colonists + the settlement are clearly in frame.
 *   Fix 3 — Active construction: at record-start we place a small 3x3 room
 *     (8 wall blueprints + 1 floor blueprint) with needWood=0 / needStone=0
 *     so builders immediately haul-skip and start constructing — construction
 *     is actively visible within the first ~30s.
 *
 * REQUIRES:  com.unity.recorder 5.1.0  (manifest.json dependency)
 */
using System;
using System.IO;
using System.Reflection;
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

        // Recording camera orthographicSize — zoomed out enough to show all
        // colonists + settlement on a 60x60 map.  12 world-units in Y.
        private const float RecordOrthoSize = 12f;

        // True 1x speed: 6 game-minutes per real-second = ~0.5 game-day per
        // 2 real minutes.  Default was 60 = 10x too fast.
        private const float RecordGameMinPerRealSec = 6f;

        // ----- in-domain state (after reload) -----------------------------
        private static RecorderController _controller;
        private static double             _recordingStartTime;
        private static int                _durationSec;
        private static string             _outPath;
        private static bool               _stopping;
        private static bool               _sceneSetupDone;

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
            _sceneSetupDone = false;
            _recordingStartTime = 0;

            Debug.Log($"[GameplayRecorderTool] OnEnterPlayMode — building recorder ({_durationSec}s, {fps}fps, {_outPath})");

            // Build recorder settings — do NOT call PrepareRecording yet;
            // that requires isPlaying == true, which may not hold yet.
            var ctrlSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            ctrlSettings.SetRecordModeToManual();
            ctrlSettings.FrameRate    = fps;
            ctrlSettings.CapFrameRate = true;   // 1x real-time capture

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

                // One-time scene setup: fix speed, zoom, place blueprints.
                // Guarded by _sceneSetupDone so it runs exactly once even if
                // this block re-enters before recording starts.
                if (!_sceneSetupDone)
                {
                    _sceneSetupDone = true;
                    ApplyRecordingSceneSetup();
                }

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
                LogGameClockState(elapsed);
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
        /// <summary>
        /// Apply all recording-specific scene tweaks once play mode is live:
        ///   1. Force TimeController to 1x (no accidental 2x/4x).
        ///   2. Override GameClock.inGameMinutesPerRealSecond to 6 (true 1x pace).
        ///   3. Zoom camera out to orthographicSize 12.
        ///   4. Place a 3x3 room of wall blueprints with needWood=0 so
        ///      PawnBuilder starts constructing immediately.
        /// </summary>
        private static void ApplyRecordingSceneSetup()
        {
            Debug.Log("[GameplayRecorderTool] ApplyRecordingSceneSetup — enforcing 1x speed, zoom-out, blueprints.");

            // --- Fix 1: time speed 1x, GameClock rate = 6 min/real-sec ------
            var tc = UnityEngine.Object.FindObjectOfType<MelonS.GameProto.TimeController>();
            if (tc != null)
            {
                tc.SetScale(1f);
                Debug.Log("[GameplayRecorderTool] TimeController.SetScale(1) done.");
            }
            else
            {
                // Fallback: set Time.timeScale directly
                Time.timeScale = 1f;
                Debug.LogWarning("[GameplayRecorderTool] TimeController not found — Time.timeScale=1 set directly.");
            }

            var gc = MelonS.GameProto.GameClock.Instance;
            if (gc != null)
            {
                // Reflect into the private serialized field so we don't need a
                // public setter (avoids Programmer-side change).
                var fi = typeof(MelonS.GameProto.GameClock)
                    .GetField("inGameMinutesPerRealSecond",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fi != null)
                {
                    fi.SetValue(gc, RecordGameMinPerRealSec);
                    Debug.Log($"[GameplayRecorderTool] GameClock.inGameMinutesPerRealSecond set to {RecordGameMinPerRealSec} " +
                              $"(was default 60) — 6 game-min/real-sec = ~0.5 game-day per 2 real minutes.");
                }
                else
                {
                    Debug.LogWarning("[GameplayRecorderTool] Could not reflect GameClock field — game speed unchanged.");
                }
            }
            else
            {
                Debug.LogWarning("[GameplayRecorderTool] GameClock.Instance is null — game speed unchanged.");
            }

            // --- Fix 2: zoom camera out so all pawns + settlement visible ----
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographicSize = RecordOrthoSize;
                // Also ensure 2D clear (no skybox bleed)
                cam.clearFlags = CameraClearFlags.SolidColor;
                Debug.Log($"[GameplayRecorderTool] Camera.main orthographicSize set to {RecordOrthoSize}.");
            }
            else
            {
                Debug.LogWarning("[GameplayRecorderTool] Camera.main is null — zoom-out skipped.");
            }

            // --- Fix 3: place construction blueprints so building is visible -
            PlaceConstructionBlueprints();
        }

        // -------------------------------------------------------------------
        /// <summary>
        /// Place a small 3x3 room (8 wall cells + 1 door on the south side)
        /// centred at world (0, 4) — just north of the typical spawn cluster.
        /// Blueprints are pre-funded (needWood=0, needStone=0) so PawnBuilder
        /// skips hauling and starts building immediately.
        ///
        /// Uses BuildManager.TryPlaceAt() so it respects occupancy checks and
        /// reuses the same placement path as player clicks.  If BuildManager is
        /// unavailable we fall back to direct BlueprintEntity instantiation.
        /// </summary>
        private static void PlaceConstructionBlueprints()
        {
            var bm = MelonS.GameProto.BuildManager.Instance;
            if (bm == null)
            {
                Debug.LogWarning("[GameplayRecorderTool] BuildManager.Instance null — using direct blueprint spawn.");
                PlaceBlueprintsDirect();
                return;
            }

            // 3x3 exterior at anchor (-1, 3):
            //   walls along all 4 edges except one door cell (south-centre = (0,3)).
            //   Interior = (0,4) kept open.
            //   Pattern (y from bottom):
            //     y=5: W W W      (north wall)
            //     y=4: W . W      (east/west walls)
            //     y=3: W D W      (south wall + door at centre)

            // Wall placements (mode Wall, cost 5 wood normally — here 0 via direct spawn)
            var wallCells = new System.Collections.Generic.List<(int, int)>
            {
                (-1, 5), (0, 5), (1, 5),   // north wall
                (-1, 4),         (1, 4),   // east/west walls
                (-1, 3),         (1, 3),   // south corners (door in middle)
            };
            // Additional floor cell inside the room
            var floorCells = new System.Collections.Generic.List<(int, int)>
            {
                (0, 4),  // interior floor
            };

            // Door cell: south-centre
            int doorCx = 0, doorCy = 3;

            int placed = 0;
            // We use BuildManager.SetMode + TryPlaceAt.  The cooldown (0.15s)
            // applies from setModeTime — we reset once per mode switch.
            // Since we're in edit/play crossover, unscaledTime should work.
            // Workaround: reflect-zero the setModeTime field so cooldown doesn't block.
            var setModeField = typeof(MelonS.GameProto.BuildManager)
                .GetField("setModeTime", BindingFlags.Instance | BindingFlags.NonPublic);

            void ResetCooldown()
            {
                setModeField?.SetValue(bm, Time.unscaledTime - 10f);
            }

            // Walls
            bm.SetMode(MelonS.GameProto.BuildManager.Mode.Wall);
            ResetCooldown();
            foreach (var (cx, cy) in wallCells)
            {
                bool ok = bm.TryPlaceAt(cx, cy);
                if (ok) placed++;
                // After each placement, the blueprint is spawned — reset cooldown
                ResetCooldown();
            }

            // Floor inside
            bm.SetMode(MelonS.GameProto.BuildManager.Mode.Floor);
            ResetCooldown();
            foreach (var (cx, cy) in floorCells)
            {
                bool ok = bm.TryPlaceAt(cx, cy);
                if (ok) placed++;
                ResetCooldown();
            }

            // Door (south-centre)
            bm.SetMode(MelonS.GameProto.BuildManager.Mode.Door);
            ResetCooldown();
            if (bm.TryPlaceAt(doorCx, doorCy)) placed++;
            ResetCooldown();

            bm.SetMode(MelonS.GameProto.BuildManager.Mode.Off);

            // Now override all blueprints to needWood=0 / needStone=0
            // so PawnBuilder doesn't wait for hauling.
            FundAllBlueprints();

            Debug.Log($"[GameplayRecorderTool] Placed {placed} construction blueprints (pre-funded, no hauling needed).");
        }

        // -------------------------------------------------------------------
        /// <summary>
        /// Fallback: directly spawn BlueprintEntity objects without BuildManager.
        /// Uses wall prefab from a pre-existing WallEntity if found; otherwise
        /// spawns minimal blueprint GameObjects.
        /// </summary>
        private static void PlaceBlueprintsDirect()
        {
            int placed = 0;
            var positions = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(-0.5f, 5.5f, 0), new Vector3(0.5f, 5.5f, 0), new Vector3(1.5f, 5.5f, 0),
                new Vector3(-0.5f, 4.5f, 0),                               new Vector3(1.5f, 4.5f, 0),
                new Vector3(-0.5f, 3.5f, 0),                               new Vector3(1.5f, 3.5f, 0),
            };

            foreach (var pos in positions)
            {
                var go = new GameObject($"Blueprint_Wall_Rec");
                go.transform.position = pos;
                var bp = go.AddComponent<MelonS.GameProto.BlueprintEntity>();
                bp.Init(MelonS.GameProto.BuildManager.Mode.Wall, null, null, 0, 0, 5f);
                placed++;
            }
            Debug.Log($"[GameplayRecorderTool] Direct-spawned {placed} wall blueprints (pre-funded).");
        }

        // -------------------------------------------------------------------
        /// <summary>
        /// Set needWood=0 and needStone=0 on every existing BlueprintEntity so
        /// HasAllMaterials returns true immediately — PawnBuilder can start
        /// construction without waiting for a hauler to deliver materials.
        /// This is only used during recording; normal gameplay requires hauling.
        /// </summary>
        private static void FundAllBlueprints()
        {
            var blueprints = UnityEngine.Object.FindObjectsOfType<MelonS.GameProto.BlueprintEntity>();
            foreach (var bp in blueprints)
            {
                bp.needWood  = 0;
                bp.needStone = 0;
                bp.collectedWood  = 0;
                bp.collectedStone = 0;
            }
            if (blueprints.Length > 0)
                Debug.Log($"[GameplayRecorderTool] Pre-funded {blueprints.Length} blueprints (needWood=0, needStone=0).");
        }

        // -------------------------------------------------------------------
        private static void LogGameClockState(double elapsed)
        {
            var gc = MelonS.GameProto.GameClock.Instance;
            if (gc != null)
            {
                Debug.Log($"[GameplayRecorderTool] elapsed={elapsed:F0}s wall | " +
                          $"Day={gc.Day} Hour={gc.Hour:D2}:{gc.Minute:D2} " +
                          $"(game-time since start)");
            }
            else
            {
                Debug.Log($"[GameplayRecorderTool] elapsed={elapsed:F0}s / {_durationSec}s");
            }
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
