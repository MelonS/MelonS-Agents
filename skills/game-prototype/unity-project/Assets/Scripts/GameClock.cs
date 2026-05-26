using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 9: in-game clock.  1 real-time second = some game-time fraction.
    /// Default: 1 in-game day = 240 real seconds (4 min at 1x).  At 4x
    /// speed that's 1 minute per in-game day, which feels like RimWorld.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        // R6: Instance property routes to Services.Get (caller 호환).
        //  Awake 가 Services.Register 호출.
        public static GameClock Instance => Services.Get<GameClock>();

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
            // R6: ServiceLocator register (singleton 중복 방지)
            if (Services.Has<GameClock>() && Services.Get<GameClock>() != this)
            { Destroy(gameObject); return; }
            Services.Register<GameClock>(this);
            // P6: CLI -starthour N 으로 시작 시간 강제 (없으면 default 06:00 새벽)
            int startHour = 6;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-starthour" && int.TryParse(args[i+1], out int h))
                { startHour = Mathf.Clamp(h, 0, 23); break; }
            }
            GameSeconds = startHour * 3600f;
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
