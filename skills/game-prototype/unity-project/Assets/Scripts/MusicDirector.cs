using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M3-03 Lane A -- wiki acceptance #8:
    ///   "Music swaps to the tension track during a raid (threatTier>=2) and back when clear."
    ///
    /// Self-attaching driver that polls AIDirector.Instance.CurrentThreatTier
    /// each frame and routes tier transitions to AudioBank.PlayDangerMusic() /
    /// AudioBank.StopDangerMusic().
    ///
    /// Pattern: mirrors RainSoundDriver.cs -- a hidden persistent GameObject with
    /// [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] so no SceneSetup.cs /
    /// SceneSetup.*.cs edit is required this lane.
    ///
    /// Lane contract (W-M3-03):
    ///   - This file is owned SOLELY by Lane A.
    ///   - AIDirector.cs is NOT edited -- this driver reads only
    ///     AIDirector.Instance.CurrentThreatTier (public read-only int property).
    ///   - AudioBank.cs PlayDangerMusic()/StopDangerMusic() calls are the only
    ///     cross-file dependency; AudioBank.cs is also owned by Lane A.
    ///   - WeatherController.cs, RainSoundDriver.cs and all entity/SceneSetup
    ///     files are NOT touched by this lane.
    ///
    /// Throttle note: PlayDangerMusic/StopDangerMusic are idempotent looping-bed
    /// calls (same pattern as PlayRain/StopRain in RainSoundDriver). No per-call
    /// throttle is needed here -- the _lastTier state guard already suppresses
    /// redundant calls every frame while the tier is stable.
    ///
    /// Null-safety:
    ///   - If AIDirector.Instance is null (scene without a director), the driver
    ///     idles silently each frame with no error.
    ///   - If AudioBank.Instance is null, the driver idles silently.
    ///   - Both are checked per-frame so the driver recovers automatically if
    ///     either singleton appears late (race-condition safe).
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        // Sentinel value: -1 means "not yet initialized" -- distinct from tier 0
        // so the driver issues an initial command on the very first frame even if
        // the tier has not changed since bootstrap (handles load-into-raid edge case).
        private int _lastTier = -1;

        private void Update()
        {
            var director = AIDirector.Instance;
            if (director == null) return;

            int tier = director.CurrentThreatTier;

            if (tier == _lastTier) return;  // no transition -- skip
            _lastTier = tier;

            var bank = AudioBank.Instance;
            if (bank == null) return;

            if (tier >= 2)
                bank.PlayDangerMusic();
            else
                bank.StopDangerMusic();
        }

        // ================================================================
        //  Self-attach bootstrap -- no SceneSetup edit needed.
        // ================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindDriver() != null) return;

            var go = new GameObject("~MusicDirector");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<MusicDirector>();
        }

        private static MusicDirector FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<MusicDirector>();
#else
            return Object.FindObjectOfType<MusicDirector>();
#endif
        }
    }
}
