using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M6-01 B8 -- Footstep SFX + day/night ambient variation driver.
    ///
    /// Self-bootstrapping via [RuntimeInitializeOnLoadMethod(AfterSceneLoad)].
    /// Creates a hidden DontDestroyOnLoad GO (idempotency-guarded via
    /// FindFirstObjectByType so double-init from test scenes is harmless).
    ///
    /// Two responsibilities:
    ///   1. FOOTSTEP: Poll all PawnMovement instances each frame.  For each
    ///      walking pawn (IsMoving == true) whose per-pawn 0.4s timer has
    ///      elapsed, call AudioBank.Instance?.PlayFootstep().  AudioBank has
    ///      an additional 0.2s bank-side guard as defense-in-depth (Lesson #4).
    ///
    ///   2. AMBIENT SWAP: Poll GameClock.Instance.DayProgress each frame.
    ///      On a day->night or night->day transition boundary, call
    ///      AudioBank.Instance?.SwapAmbientToNight() or SwapAmbientToDay().
    ///      The swap is idempotent -- AudioBank guards its _isNightAmbient flag.
    ///
    /// Read-only contract:
    ///   - PawnMovement: reads IsMoving (public bool) and transform.position only.
    ///     Does NOT call SetTarget, ClearTarget, or any mutating method.
    ///   - DayNightCycle / GameClock: reads GameClock.Instance.DayProgress only.
    ///     Does NOT edit DayNightCycle.cs.
    ///   - AudioBank: calls PlayFootstep(), SwapAmbientToNight(), SwapAmbientToDay()
    ///     only -- all public methods added in the AudioBank B8 lane.
    ///
    /// Day/night boundary definition (mirrors DayNightCycle.cs STOPS):
    ///   Night = DayProgress >= 0.83f (22:00) OR DayProgress &lt; 0.27f (06:30).
    ///   Day   = DayProgress in [0.27f, 0.83f).
    ///   At 0.27 DayNightCycle shows sunrise warm (06:30); at 0.83 it shows
    ///   deep night (22:00) -- these are the natural day/night boundaries.
    ///
    /// Throttle per pawn: FootstepInterval = 0.40s.
    ///   A walking pawn calls SetTarget every frame; PawnMovement.Update moves
    ///   the transform every frame.  Without throttling, PlayFootstep() would be
    ///   called at frame-rate (60+/sec per pawn).  Per-pawn throttle at 0.4s
    ///   yields ~2.5 footstep sounds per second per pawn -- a natural walking cadence.
    ///   The AudioBank adds a global 0.2s guard (5/sec max across all pawns).
    ///
    /// Rescan interval: 2.0s.
    ///   PawnMovement instances are found via FindObjectsByType at startup and
    ///   rescanned every 2s.  Handles pawn spawns/deaths mid-session without
    ///   per-frame FindObjectsByType overhead.  2s rescan is the same pattern
    ///   as TreeSwayDriver.cs (proven lightweight).
    ///
    /// Wiki B8 acceptance:
    ///   - A walking pawn plays a faint footstep ~every 0.4s.
    ///   - Ambient bed differs at 02:00 (DayProgress~0.083, night crickets)
    ///     vs 12:00 (DayProgress=0.50, day birds).
    ///
    /// QA FLAGS:
    ///   - AudioBank.sfxFootstep must be assigned in the Inspector (footstep.wav).
    ///   - AudioBank.sfxAmbientNight must be assigned in the Inspector (ambient_night.wav).
    ///   - DayNightCycle read accessor used: GameClock.Instance.DayProgress (float [0,1)).
    ///   - Ambient swap tested at DayProgress=0.083 (02:00) -> night crickets;
    ///     DayProgress=0.50 (12:00) -> day birds.
    /// </summary>
    public class FootstepDriver : MonoBehaviour
    {
        // -- Self-bootstrap --
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Idempotency guard: if a FootstepDriver already exists in the scene
            // (e.g. second scene load, test scene with manual GO), do not add another.
            if (Object.FindFirstObjectByType<FootstepDriver>() != null) return;

            var go = new GameObject("[FootstepDriver]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<FootstepDriver>();
        }

        // -- Per-pawn footstep throttle --
        // Walking cadence: 0.4s between footstep sounds per pawn (~2.5/sec).
        // Lesson #4: per-frame PawnMovement.Update() fires IsMoving every frame;
        // without per-pawn throttle = 60+ PlayFootstep/sec per pawn = audio spam.
        private const float FootstepInterval = 0.40f;

        // -- Ambient rescan interval (seconds between FindObjectsByType calls) --
        private const float RescanInterval = 2.0f;

        // -- Night boundary (mirrors DayNightCycle.cs STOPS) --
        // 0.83 = 22:00 (night start), 0.27 = 06:30 (sunrise / day start).
        // DayProgress >= 0.83 OR DayProgress < 0.27 is night.
        private const float NightStartProgress = 0.83f;  // 22:00
        private const float DayStartProgress   = 0.27f;  // 06:30

        // -- Runtime state --
        private PawnMovement[] _pawns = new PawnMovement[0];
        private float[]        _lastFootstepTimes;  // parallel array -- per-pawn timer
        private float          _nextRescan;

        // Track previous ambient state to detect transitions.
        // Start as day (AudioBank.Start() sets day ambient by default).
        private bool _wasNight = false;

        private void Start()
        {
            RescanPawns();
            _nextRescan = Time.time + RescanInterval;

            // Sync ambient state to current time-of-day on startup so we do not
            // play the wrong bed from scene start to the first Update().
            SyncAmbientImmediate();
        }

        private void Update()
        {
            // -- Rescan pawn list periodically --
            if (Time.time >= _nextRescan)
            {
                RescanPawns();
                _nextRescan = Time.time + RescanInterval;
            }

            float now = Time.time;

            // -- Footstep SFX per walking pawn --
            for (int i = 0; i < _pawns.Length; i++)
            {
                PawnMovement pm = _pawns[i];
                if (pm == null) continue;
                if (!pm.IsMoving) continue;
                if (now - _lastFootstepTimes[i] < FootstepInterval) continue;

                _lastFootstepTimes[i] = now;
                AudioBank.Instance?.PlayFootstep();
            }

            // -- Ambient day/night swap --
            if (GameClock.Instance == null) return;
            float dayProgress = GameClock.Instance.DayProgress;
            bool isNight = dayProgress >= NightStartProgress || dayProgress < DayStartProgress;

            if (isNight != _wasNight)
            {
                _wasNight = isNight;
                if (isNight)
                    AudioBank.Instance?.SwapAmbientToNight();
                else
                    AudioBank.Instance?.SwapAmbientToDay();
            }
        }

        // -- Rescans PawnMovement instances and rebuilds the per-pawn timer array --
        private void RescanPawns()
        {
            PawnMovement[] found = Object.FindObjectsByType<PawnMovement>(FindObjectsSortMode.None);

            // Build a new timer array, preserving existing per-pawn times where
            // the PawnMovement instance is the same object (handles pawn order
            // changes without resetting all timers unnecessarily).
            float[] newTimers = new float[found.Length];
            for (int i = 0; i < found.Length; i++)
            {
                // Try to find a matching existing timer
                float existingTime = -10f;
                if (_pawns != null)
                {
                    for (int j = 0; j < _pawns.Length; j++)
                    {
                        if (_pawns[j] == found[i])
                        {
                            existingTime = _lastFootstepTimes[j];
                            break;
                        }
                    }
                }
                newTimers[i] = existingTime;
            }

            _pawns             = found;
            _lastFootstepTimes = newTimers;
        }

        // -- Sync ambient immediately to current time-of-day (called at Start) --
        private void SyncAmbientImmediate()
        {
            if (GameClock.Instance == null) return;
            float dayProgress = GameClock.Instance.DayProgress;
            bool isNight = dayProgress >= NightStartProgress || dayProgress < DayStartProgress;
            _wasNight = isNight;
            if (isNight)
                AudioBank.Instance?.SwapAmbientToNight();
            else
                AudioBank.Instance?.SwapAmbientToDay();
        }
    }
}
