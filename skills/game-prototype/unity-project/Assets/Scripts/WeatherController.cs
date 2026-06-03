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

        [SerializeField] private AIDirector director;

        public void SetRefs(AIDirector dir) { director = dir; }

        private void Awake()
        {
            if (Services.Has<WeatherController>() && Services.Get<WeatherController>() != this)
            { Destroy(gameObject); return; }
            Services.Register<WeatherController>(this);
        }

        private void OnEnable()
        {
            if (director != null) director.OnEventFired += HandleEvent;
        }
        private void OnDisable()
        {
            if (director != null) director.OnEventFired -= HandleEvent;
        }

        private void Update()
        {
            // Late-bind in case OnEnable ran before director got assigned.
            if (director == null) return;
            // #버그헌트(2026-06-03): 폭풍 지속을 실시간(Time.time) 대신 게임 시계(GameSeconds)로
            //  측정 — 일시정지/배속을 존중(RimWorld 날씨는 게임 시간 기준).  set/check 모두 동일 출처.
            if (Current == WeatherKind.Storm && NowGameSec() > StormUntil)
            {
                Current = WeatherKind.Clear;
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
                StormUntil = NowGameSec() + 60f;  // 게임 시계 기준 60s (일시정지/배속 존중)
            }
        }

        /// <summary>Multiplier applied to camera bg color (0..1).
        /// Storm = 0.55 (darken).  Clear = 1.0.</summary>
        public float ColorMultiplier()
        {
            return Current == WeatherKind.Storm ? 0.55f : 1.0f;
        }
    }
}
