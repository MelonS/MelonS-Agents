using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    public enum WeatherKind { Clear, Storm }

    /// <summary>Day 22: weather state.  AIDirector's storm_warning event
    /// triggers a 60s Storm.  Camera background darkens, outdoor pawns
    /// suffer mood penalty.  DayNightCycle.cs still drives base color;
    /// WeatherController overlays a multiplier on top.</summary>
    public class WeatherController : MonoBehaviour
    {
        public static WeatherController Instance => Services.Get<WeatherController>();  // R6

        public WeatherKind Current { get; private set; } = WeatherKind.Clear;
        public float StormUntil { get; private set; } = -1f;

        // #버그헌트(2026-06-04): 폭풍 지속 상수 회귀 fix.  2026-06-03 에 측정 단위를 실시간
        //  (Time.time) → 게임시계(GameSeconds)로 바꾸면서 상수 60f 를 그대로 둬, 폭풍이 1x 에서
        //  ~0.7 실초(60 / 86.4)만에 끝나 '폭풍이 깜빡이고 사라지는' 버그가 됐다.  게임시계는 1x
        //  에서 86.4 게임초/실초(GameClock 1.44 게임분/실초 × 60)로 흐르므로, 의도한 '약 60 실초
        //  폭풍(1x)'을 게임시계로 환산한다: 60 × 86.4 ≈ 5184 게임초.  배속(2x/4x) 시 게임시계가
        //  더 빨리 흘러 실시간 지속은 자동 비례 단축(일시정지/배속 존중).
        public const float StormDurationGameSec = 5184f;  // ≈ 60 real-sec @ 1x

        [SerializeField] private AIDirector director;

        public void SetRefs(AIDirector dir) { director = dir; }

        private void Awake()
        {
            if (Services.Has<WeatherController>() && Services.Get<WeatherController>() != this)
            { Destroy(gameObject); return; }
            Services.Register<WeatherController>(this);
        }

        // 소크 r2 채점(2026-06-12) — '폭풍 경보 2회, 폭풍 흔적 0'.  OnEnable 시점에
        //  director 가 null 이면 구독이 영구 누락되는데 기존 'Late-bind' 주석은 코드가
        //  없는 거짓 주석이었다.  실제 지연-바인드로 교체.
        private bool subscribed;

        private void OnEnable() => TrySubscribe();
        private void OnDisable()
        {
            if (subscribed && director != null) director.OnEventFired -= HandleEvent;
            subscribed = false;
        }

        private void TrySubscribe()
        {
            if (subscribed) return;
            if (director == null)
                director = UnityEngine.Object.FindFirstObjectByType<AIDirector>();
            if (director == null) return;
            director.OnEventFired += HandleEvent;
            subscribed = true;
        }

        private void Update()
        {
            TrySubscribe();
            if (director == null) return;
            // #버그헌트(2026-06-03): 폭풍 지속을 실시간(Time.time) 대신 게임 시계(GameSeconds)로
            //  측정 — 일시정지/배속을 존중(레퍼런스 날씨는 게임 시간 기준).  set/check 모두 동일 출처.
            if (Current == WeatherKind.Storm && NowGameSec() > StormUntil)
            {
                Current = WeatherKind.Clear;
                Debug.Log("[Weather] storm ends → clear");
            }
        }

        private static float NowGameSec()
            => GameClock.Instance != null ? GameClock.Instance.GameSeconds : Time.time;

        private void HandleEvent(GameEvent ev)
        {
            if (ev == null) return;
            if (ev.id == "storm_warning")
            {
                Current = WeatherKind.Storm;
                StormUntil = NowGameSec() + StormDurationGameSec;  // 게임시계 환산(≈60 실초 @1x)
                Debug.Log($"[Weather] storm begins (until gameSec={StormUntil:F0})");
            }
        }

        /// <summary>테스트 스캐폴딩 (ReproHarness setWeather) — AIDirector 이벤트를 기다리지
        ///  않고 즉시 폭풍 시작/종료.  상태 전이는 HandleEvent/Update 와 동일 경로.</summary>
        public void ForceStorm()
        {
            Current = WeatherKind.Storm;
            StormUntil = NowGameSec() + StormDurationGameSec;
        }
        public void ForceClear()
        {
            Current = WeatherKind.Clear;
            StormUntil = -1f;
        }

        /// <summary>Multiplier applied to camera bg color (0..1).
        /// Storm = 0.55 (darken).  Clear = 1.0.</summary>
        public float ColorMultiplier()
        {
            return Current == WeatherKind.Storm ? 0.55f : 1.0f;
        }
    }
}
