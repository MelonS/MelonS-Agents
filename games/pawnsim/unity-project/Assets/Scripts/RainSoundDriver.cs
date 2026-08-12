using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M3-02 Lane D — wiki acceptance #9:
    ///   "A storm plays a rain loop; clear weather is silent of rain."
    ///
    /// Self-attaching driver that polls WeatherController.Instance.Current
    /// each frame and routes weather transitions to AudioBank.PlayRain() /
    /// AudioBank.StopRain().
    ///
    /// Pattern: mirrors FlickerLight.cs Bootstrap+Driver approach — a hidden
    /// persistent GameObject with [RuntimeInitializeOnLoadMethod(AfterSceneLoad)]
    /// so no SceneSetup.cs / SceneSetup.*.cs edit is required this lane.
    ///
    /// Lane contract (W-M3-02):
    ///   - This file is owned SOLELY by Lane D.
    ///   - WeatherController.cs is NOT edited — this driver reads only
    ///     WeatherController.Instance.Current (public property, read-only poll).
    ///   - AudioBank.cs PlayRain()/StopRain() calls are the only cross-file
    ///     dependency; AudioBank.cs is also owned by Lane D.
    ///
    /// Throttle note: rain is a looping bed, not a one-shot SFX. PlayRain()
    /// is idempotent (no-op if already playing); StopRain() is idempotent
    /// (no-op if already stopped). No per-call throttle needed here — the
    /// driver state machine (lastKind) already suppresses redundant calls.
    /// </summary>
    public class RainSoundDriver : MonoBehaviour
    {
        // Track the last observed weather kind to avoid redundant Play/Stop
        // calls every frame while the weather is stable.
        private WeatherKind _lastKind = WeatherKind.Clear;

        // True after the first valid poll — ensures we issue an initial
        // Stop/Play command on the first frame even if weather hasn't changed
        // since the driver spawned (handles scene-load-into-storm edge case).
        private bool _initialized = false;

        private void Update()
        {
            var wc = WeatherController.Instance;
            if (wc == null) return;

            WeatherKind current = wc.Current;

            if (!_initialized || current != _lastKind)
            {
                _initialized = true;
                _lastKind = current;

                var bank = AudioBank.Instance;
                if (bank == null) return;

                if (current == WeatherKind.Storm)
                    bank.PlayRain();
                else
                    bank.StopRain();
            }
        }

        // ================================================================
        //  Self-attach bootstrap — no SceneSetup edit needed.
        // ================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindDriver() != null) return;

                var go = new GameObject("~RainSoundDriver");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                go.AddComponent<RainSoundDriver>();
                    });
        }

        private static RainSoundDriver FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<RainSoundDriver>();
#else
            return Object.FindObjectOfType<RainSoundDriver>();
#endif
        }
    }
}
