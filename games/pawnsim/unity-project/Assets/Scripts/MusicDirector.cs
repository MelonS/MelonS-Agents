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

        // Cached AIDirector reference -- re-polled each frame until found.
        // AIDirector does not use the Services locator (it is a scene MonoBehaviour
        // placed by SceneSetup); the correct search pattern is FindFirstObjectByType,
        // matching AlertStackUI.cs which resolves AIDirector the same way.
        private AIDirector _director;

        // #게임필 배치4(2026-06-10 자율) — 위험 음악을 '날짜'가 아니라 실제 위협에 연동.
        //  이전엔 CurrentThreatTier(순수 날짜 함수) 폴링이라 7일차부터 아무 일 없어도
        //  긴장 트랙 영구 재생 — 음악이 상황 신호이길 멈췄다.  살아있는 산적/늑대 존재를
        //  0.5s 폴링(ThreatAlertUI.CheckThreats 패턴)으로 전환.
        private float _lastThreatPoll = -10f;
        private bool _dangerOn;

        private void Update()
        {
            if (Time.unscaledTime - _lastThreatPoll < 0.5f) return;
            _lastThreatPoll = Time.unscaledTime;

            bool threatAlive;
#if UNITY_2023_1_OR_NEWER
            threatAlive = Object.FindFirstObjectByType<BanditEnemy>() != null
                       || Object.FindFirstObjectByType<WolfEnemy>() != null;
#else
            threatAlive = Object.FindObjectOfType<BanditEnemy>() != null
                       || Object.FindObjectOfType<WolfEnemy>() != null;
#endif
            if (threatAlive == _dangerOn) return;  // no transition -- skip
            _dangerOn = threatAlive;

            var bank = AudioBank.Instance;
            if (bank == null) return;

            if (threatAlive)
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
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindDriver() != null) return;

                var go = new GameObject("~MusicDirector");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                go.AddComponent<MusicDirector>();
                    });
        }

        private static MusicDirector FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<MusicDirector>();
#else
            return Object.FindObjectOfType<MusicDirector>();
#endif
        }

        // AIDirector is a scene MonoBehaviour; it does NOT expose a Services-based
        // Instance property.  Search via FindFirstObjectByType (lesson #7 pattern).
        private static AIDirector FindAIDirector()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<AIDirector>();
#else
            return Object.FindObjectOfType<AIDirector>();
#endif
        }
    }
}
