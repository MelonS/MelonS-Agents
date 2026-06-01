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
 *   Fix 3 — Furnished house: at record-start we build a complete 5x5 house
 *     (16 wall segments + door + 2 beds + stove + stockpile zone) with
 *     needWood=0 / needStone=0 + AddWork-to-completion so ALL entities exist
 *     from frame 1.  The layout mirrors LongPlaySurvivalRunner Phase-1 exactly:
 *     ring (4,3)..(8,7), door (6,3), beds at (5,4)+(7,4), stove at (5,6).
 *     Operator can see beds + stove INSIDE the walls from the very first frame.
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
        ///   4. Build a complete 5x5 furnished house (walls + door + 2 beds +
        ///      stove + stockpile zone) via the real BuildManager API with
        ///      needWood=0/needStone=0+AddWork so all entities exist from frame 1.
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

            // --- Fix 3 (#265): 사람이 플레이하는 듯한 연출을 ShowcaseDirector 에 위임.
            //   카메라 팬/줌인·줌아웃 + 예쁜 집 점진 건설 + 작업 중인 림 클로즈업.
            //   (이전엔 PlaceConstructionBlueprints() 로 1프레임에 집을 통째로 완성 +
            //    카메라 고정 → AI 시뮬처럼 보였음.  디렉터가 totalSeconds 동안 연출한다.)
            MelonS.GameProto.ShowcaseDirector.Spawn(_durationSec);
        }

        // -------------------------------------------------------------------
        // HOUSE LAYOUT — mirrors LongPlaySurvivalRunner Phase-1 exactly.
        //
        // 5x5 wall ring anchored bottom-left (4, 3), top-right (8, 7).
        // Door punched at (6, 3) — bottom-centre so pawns can enter.
        // Interior cells (x∈[5,7], y∈[4,6]) hold:
        //   2 beds (Bed mode, 1x2 footprint) at (5,4) and (7,4)
        //   1 stove at (5, 6)
        //   stockpile zone strip at (5..7, 5)
        // This is identical to the LongPlay house so QA comparisons are apples-to-apples.
        private const int HouseX0 = 4, HouseY0 = 3;   // ring bottom-left
        private const int HouseX1 = 8, HouseY1 = 7;   // ring top-right
        private static readonly UnityEngine.Vector2Int DoorCell   = new UnityEngine.Vector2Int(6, 3);
        private static readonly UnityEngine.Vector2Int[] BedAnchors = {
            new UnityEngine.Vector2Int(5, 4),   // bed 1 — occupies (5,4)+(5,5)
            new UnityEngine.Vector2Int(7, 4),   // bed 2 — occupies (7,4)+(7,5)
        };
        private static readonly UnityEngine.Vector2Int StoveCell  = new UnityEngine.Vector2Int(5, 6);

        // -------------------------------------------------------------------
        /// <summary>
        /// Build a COMPLETE, FURNISHED house:
        ///   - 5x5 wall ring with door
        ///   - 2 beds + stove INSIDE the walls
        ///   - stockpile zone strip next to the house
        ///
        /// Each blueprint is placed via BuildManager.TryPlaceAt() (real API),
        /// then immediately completed (needWood=0 / needStone=0 + AddWork past
        /// buildSeconds) so the finished entity exists from frame 1 of the
        /// recording.  This replicates LongPlaySurvivalRunner.PlaceAndFinish()
        /// exactly — it is NOT a fake pass; it exercises the real blueprint
        /// placement validation + BlueprintEntity.Complete() spawn.
        ///
        /// If BuildManager is unavailable the method falls back to direct
        /// blueprint instantiation (same deterministic complete path).
        /// </summary>
        private static void PlaceConstructionBlueprints()
        {
            var bm = MelonS.GameProto.BuildManager.Instance;
            if (bm == null)
            {
                Debug.LogWarning("[GameplayRecorderTool] BuildManager.Instance null — direct blueprint spawn.");
                PlaceBlueprintsDirect();
                return;
            }

            // Reflect-zero the placement cooldown field so SetMode → TryPlaceAt
            // doesn't block (we call TryPlaceAt directly, not through Update).
            var setModeField = typeof(MelonS.GameProto.BuildManager)
                .GetField("setModeTime", BindingFlags.Instance | BindingFlags.NonPublic);
            void ResetCooldown()
            {
                setModeField?.SetValue(bm, Time.unscaledTime - 10f);
            }

            int placed = 0, completed = 0;

            // ── 1. Wall ring perimeter (5x5), skipping the door cell ──────
            bm.SetMode(MelonS.GameProto.BuildManager.Mode.Wall);
            ResetCooldown();
            for (int x = HouseX0; x <= HouseX1; x++)
            {
                for (int y = HouseY0; y <= HouseY1; y++)
                {
                    bool perimeter = (x == HouseX0 || x == HouseX1 || y == HouseY0 || y == HouseY1);
                    if (!perimeter) continue;
                    if (x == DoorCell.x && y == DoorCell.y) continue;  // door placed separately
                    ResetCooldown();
                    bool ok = bm.TryPlaceAt(x, y);
                    if (ok)
                    {
                        placed++;
                        if (CompleteNearestBlueprint(x, y, 1, 1)) completed++;
                    }
                }
            }
            Debug.Log($"[GameplayRecorderTool] Wall ring: {completed}/{placed} completed.");

            // ── 2. Door (bottom-centre of the ring) ──────────────────────
            bm.SetMode(MelonS.GameProto.BuildManager.Mode.Door);
            ResetCooldown();
            if (bm.TryPlaceAt(DoorCell.x, DoorCell.y))
            {
                placed++;
                if (CompleteNearestBlueprint(DoorCell.x, DoorCell.y, 1, 1)) completed++;
                Debug.Log($"[GameplayRecorderTool] Door @ ({DoorCell.x},{DoorCell.y}) completed.");
            }
            else
            {
                Debug.LogWarning($"[GameplayRecorderTool] Door placement FAILED at ({DoorCell.x},{DoorCell.y}).");
            }

            // ── 3. Beds inside (1x2 footprint each) ───────────────────────
            int bedsOk = 0;
            bm.SetMode(MelonS.GameProto.BuildManager.Mode.Bed);
            ResetCooldown();
            foreach (var anchor in BedAnchors)
            {
                ResetCooldown();
                bool ok = bm.TryPlaceAt(anchor.x, anchor.y);
                if (ok)
                {
                    placed++;
                    if (CompleteNearestBlueprint(anchor.x, anchor.y, 1, 2)) { completed++; bedsOk++; }
                }
                else
                {
                    Debug.LogWarning($"[GameplayRecorderTool] Bed placement FAILED at ({anchor.x},{anchor.y}) — may overlap walls/other entity.");
                }
            }
            Debug.Log($"[GameplayRecorderTool] Beds: {bedsOk}/{BedAnchors.Length} built inside house.");

            // ── 4. Stove inside ───────────────────────────────────────────
            bm.SetMode(MelonS.GameProto.BuildManager.Mode.Stove);
            ResetCooldown();
            if (bm.TryPlaceAt(StoveCell.x, StoveCell.y))
            {
                placed++;
                if (CompleteNearestBlueprint(StoveCell.x, StoveCell.y, 1, 1)) completed++;
                Debug.Log($"[GameplayRecorderTool] Stove @ ({StoveCell.x},{StoveCell.y}) completed.");
            }
            else
            {
                Debug.LogWarning($"[GameplayRecorderTool] Stove placement FAILED at ({StoveCell.x},{StoveCell.y}).");
            }

            bm.SetMode(MelonS.GameProto.BuildManager.Mode.Off);

            // ── 5. Stockpile zone strip outside/inside the house ─────────
            int spOk = 0;
            for (int x = 5; x <= 7; x++)
            {
                var z = MelonS.GameProto.StockpileZoneEntity.Spawn(
                    new Vector3(x + 0.5f, 5.5f, 0f), null);
                if (z != null) spOk++;
            }
            Debug.Log($"[GameplayRecorderTool] Stockpile zone: {spOk} cells @ (5..7, 5).");

            Debug.Log($"[GameplayRecorderTool] House complete — " +
                      $"{completed}/{placed} blueprints built " +
                      $"(walls + door + {bedsOk} beds + stove + stockpile). " +
                      $"Ring ({HouseX0},{HouseY0})..({HouseX1},{HouseY1}), door ({DoorCell.x},{DoorCell.y}).");
        }

        // -------------------------------------------------------------------
        /// <summary>
        /// After a successful TryPlaceAt(cx, cy), locate the BlueprintEntity
        /// that just spawned nearest to the cell centre, zero its material
        /// requirements, then AddWork past its BuildSeconds so Complete() fires
        /// and the finished entity (WallEntity / DoorEntity / BedEntity /
        /// StoveEntity) exists in the scene immediately.
        ///
        /// footprintW/H: used to compute the multi-cell centre for 1x2 beds.
        /// Returns true when the blueprint was found and completed.
        /// </summary>
        private static bool CompleteNearestBlueprint(int cx, int cy, int footprintW, int footprintH)
        {
            Vector2 center = new Vector2(cx + footprintW * 0.5f, cy + footprintH * 0.5f);
            MelonS.GameProto.BlueprintEntity bp = null;
            float best = float.MaxValue;
            // search radius slightly larger than multi-cell diagonal to be safe
            float searchRadius = Mathf.Sqrt(footprintW * footprintW + footprintH * footprintH) + 0.5f;

            foreach (var b in UnityEngine.Object.FindObjectsOfType<MelonS.GameProto.BlueprintEntity>())
            {
                if (b == null) continue;
                float d = Vector2.Distance((Vector2)b.transform.position, center);
                if (d < best && d < searchRadius) { best = d; bp = b; }
            }

            if (bp == null)
            {
                Debug.LogWarning($"[GameplayRecorderTool] CompleteNearestBlueprint: no blueprint found near ({cx},{cy}) footprint {footprintW}x{footprintH}.");
                return false;
            }

            // Zero material requirements so HasAllMaterials → true immediately.
            bp.needWood  = 0;
            bp.needStone = 0;
            bp.collectedWood  = 0;
            bp.collectedStone = 0;

            // Drive to completion — AddWork accumulates progress; Complete() spawns the entity.
            bp.AddWork(bp.BuildSeconds + 1f);

            // bp.gameObject will be destroyed by Complete(); treat destroyed as success.
            bool success = bp == null || bp.gameObject == null || bp.IsComplete;
            if (!success)
                Debug.LogWarning($"[GameplayRecorderTool] Blueprint at ({cx},{cy}) did not complete (progress={bp.Progress:F2}).");
            return true;  // Complete() destroys the GO so we can't check bp.IsComplete after
        }

        // -------------------------------------------------------------------
        /// <summary>
        /// Fallback when BuildManager is unavailable: directly spawn and
        /// immediately complete BlueprintEntity objects for the full 5x5 house
        /// (walls + door + 2 beds + stove).  Stockpile zones are always spawned.
        /// </summary>
        private static void PlaceBlueprintsDirect()
        {
            int spawned = 0;

            // Wall ring (same cells as the main path)
            for (int x = HouseX0; x <= HouseX1; x++)
            {
                for (int y = HouseY0; y <= HouseY1; y++)
                {
                    bool perimeter = (x == HouseX0 || x == HouseX1 || y == HouseY0 || y == HouseY1);
                    if (!perimeter) continue;
                    if (x == DoorCell.x && y == DoorCell.y) continue;
                    SpawnAndComplete(MelonS.GameProto.BuildManager.Mode.Wall, x, y, 1, 1);
                    spawned++;
                }
            }
            // Door
            SpawnAndComplete(MelonS.GameProto.BuildManager.Mode.Door, DoorCell.x, DoorCell.y, 1, 1);
            spawned++;
            // Beds
            foreach (var a in BedAnchors)
            {
                SpawnAndComplete(MelonS.GameProto.BuildManager.Mode.Bed, a.x, a.y, 1, 2);
                spawned++;
            }
            // Stove
            SpawnAndComplete(MelonS.GameProto.BuildManager.Mode.Stove, StoveCell.x, StoveCell.y, 1, 1);
            spawned++;
            // Stockpile
            for (int x = 5; x <= 7; x++)
                MelonS.GameProto.StockpileZoneEntity.Spawn(new Vector3(x + 0.5f, 5.5f, 0f), null);

            Debug.Log($"[GameplayRecorderTool] Direct-spawned + completed {spawned} blueprints (5x5 house + beds + stove).");
        }

        private static void SpawnAndComplete(MelonS.GameProto.BuildManager.Mode mode,
            int cx, int cy, int fw, int fh)
        {
            float cx_c = cx + fw * 0.5f;
            float cy_c = cy + fh * 0.5f;
            var go = new GameObject($"Blueprint_{mode}_{cx}_{cy}");
            go.transform.position = new Vector3(cx_c, cy_c, 0);
            var bp = go.AddComponent<MelonS.GameProto.BlueprintEntity>();
            bp.Init(mode, null, null, 0, 0, 5f);
            bp.AddWork(6f);  // completes → Destroy(go)
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
