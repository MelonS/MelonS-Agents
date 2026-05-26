using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 9: in-game clock.  1 real-time second = some game-time fraction.
    /// Default: 1 in-game day = 240 real seconds (4 min at 1x).  At 4x
    /// speed that's 1 minute per in-game day, which feels like RimWorld.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }

        [SerializeField] private float realSecondsPerInGameDay = 240f;

        // Total in-game seconds since start.  Scales with Time.timeScale
        // since we use deltaTime (which is already scaled).
        public float GameSeconds { get; private set; }

        public int Day => 1 + (int)(GameSeconds / 86400f);
        public int Hour => (int)((GameSeconds % 86400f) / 3600f);
        public int Minute => (int)((GameSeconds % 3600f) / 60f);

        /// <summary>0..1 normalized within current day.</summary>
        public float DayProgress => (GameSeconds % 86400f) / 86400f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Start at 06:00 (sunrise) so first thing the player sees is dawn.
            GameSeconds = 6f * 3600f;
        }

        private void Update()
        {
            // 1 in-game day = 86400 in-game seconds.  realSecondsPerInGameDay
            // real seconds map to 86400 in-game seconds, so per real-second
            // we advance 86400/realSecondsPerInGameDay in-game seconds.
            float perSec = 86400f / Mathf.Max(1f, realSecondsPerInGameDay);
            GameSeconds += perSec * Time.deltaTime;
        }
    }
}
