using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 9: in-game clock.  Drives the "Day N - HH:MM" display.
    ///
    /// "시간이 안 흐른다" root cause (2026-05-31): the rate was authored as
    /// realSecondsPerInGameDay (default 240 = 4 real-min per in-game day).
    /// That field is SerializeField, so a stale scene-serialized value could
    /// override the code default to something huge (e.g. one in-game second
    /// per real second) which makes the MINUTE display tick only once every
    /// 60 real seconds -> reads as frozen at 1x.
    ///
    /// Fix: express the rate as in-game MINUTES advanced per real second at
    /// 1x (intuitive + matches operator's "~1 game-hour/sec" feel), and
    /// CLAMP it inside Update so no serialized override can freeze the clock.
    /// At the default 60 in-game minutes/real-second, 1x advances one in-game
    /// hour per real second (RimWorld-ish, clearly perceptible, not frantic).
    /// Time.deltaTime is already scaled by Time.timeScale, so 2x/4x/pause all
    /// follow for free.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        // R6: Instance property routes to Services.Get (caller 호환).
        //  Awake 가 Services.Register 호출.
        public static GameClock Instance => Services.Get<GameClock>();

        [Tooltip("1x 에서 실제 1초당 흘려보낼 게임 내 '분'. 60 = 게임 1시간/실초.")]
        // #219 운영자 fb "시간이 너무 빨리" — 60(하루=24초)은 과속.  6 = 하루 4분(실측
        //  자연스러운 1x, recorder 가 쓰던 값).  하단 MinRate clamp 와 동일.
        [SerializeField] private float inGameMinutesPerRealSecond = 6f;

        // Lower/upper guard so a bad serialized value cannot freeze (too low)
        // or make the clock unreadable (too fast).  Operator target sits at
        // the default 60 (1 game-hour / real-second).
        private const float MinRate = 6f;    // >= 1 game-minute / 0.1 real-sec
        private const float MaxRate = 600f;   // <= 10 game-hours / real-sec

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
            // in-game seconds advanced per real second at 1x = minutes/sec * 60.
            // Clamp guards against a stale serialized override freezing time.
            float rate = Mathf.Clamp(inGameMinutesPerRealSecond, MinRate, MaxRate);
            float inGameSecPerRealSec = rate * 60f;
            // Time.deltaTime is already multiplied by Time.timeScale, so 1x/2x/4x
            // and pause (timeScale 0 -> deltaTime 0) all flow through correctly.
            GameSeconds += inGameSecPerRealSec * Time.deltaTime;
        }
    }
}
