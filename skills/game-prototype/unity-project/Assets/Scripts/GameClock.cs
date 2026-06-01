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
        // #234 RimWorld 정합 (위키 Time) — RimWorld 1x: 하루=60000틱=1000초(16.7분), 60틱/초.
        //  하루 1440 게임분 / 1000 실초 = 1.44 게임분/실초.  (니즈 decay 도 #234 에서 비례
        //  축소해 게임-하루 리듬은 동일하게 유지 — 둘 다 timeScale 곱이라 2x/4x 도 자동 정합.)
        [SerializeField] private float inGameMinutesPerRealSecond = 1.44f;

        // Lower/upper guard so a bad serialized value cannot freeze (too low)
        // or make the clock unreadable (too fast).  Operator target sits at
        // the default 60 (1 game-hour / real-second).
        private const float MinRate = 1f;    // #234 RimWorld 1x(1.44) 허용 위해 6→1 하향
        private const float MaxRate = 600f;   // <= 10 game-hours / real-sec

        // Total in-game seconds since start.  Scales with Time.timeScale
        // since we use deltaTime (which is already scaled).
        public float GameSeconds { get; private set; }

        /// <summary>#276 세이브 로드 시 게임 시계 복원(레이드/이벤트 스케줄 정합).</summary>
        public void SetGameSeconds(float s) => GameSeconds = Mathf.Max(0f, s);

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
