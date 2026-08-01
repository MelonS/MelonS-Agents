using System;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 8 polish: 1x / 2x / 4x time scale + Pause (Spacebar).
    /// 1/2/3 keys = 1x/2x/4x.  Space = toggle pause.
    /// </summary>
    public class TimeController : MonoBehaviour
    {
        public static TimeController Instance { get; private set; }

        /// <summary>시작 배속.  일시정지 해제 시 돌아오는 값이기도 하다.</summary>
        public const float DefaultScale = 3f;

        private float lastNonPauseScale = DefaultScale;
        public bool IsPaused => Time.timeScale == 0f;
        public float CurrentScale => Time.timeScale;
        public event Action<float> OnScaleChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // 2026-08-02 — 기본 배속을 1x → 3x.
            //  실측: 1x 에서 게임 하루 = 실제 16.7분(장르 레퍼런스 정합).  그런데
            //  심사자가 보는 창은 5~15분이라 **게임 하루의 절반도 안 지나간다** —
            //  손대지 않은 10분 캡처에서 목재·가치가 9분간 완전히 고정이었고
            //  마지막 화면은 전원 취침이었다.  첫 습격(2일차)·큰 이벤트(1.5~3일)는
            //  구조적으로 창 밖이다.
            //  시뮬레이션 값은 하나도 바꾸지 않는다 — 니즈 감소·연구 속도가 전부
            //  timeScale 곱이라 **게임-하루 리듬은 그대로**고, 벽시계 속도만 바뀐다.
            //  (레퍼런스도 배속 조작을 상시 쓴다.  1x 를 '느리게' 로 남겨 둔다.)
            Time.timeScale = DefaultScale;
            lastNonPauseScale = DefaultScale;
        }

        private void Update()
        {
            // 운영자 피드백 #13 (2026-06-12 AM): 배속 1/2/4 → 레퍼런스 파리티 1/3/6.
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetScale(1f);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SetScale(3f);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SetScale(6f);
            else if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
        }

        public void SetScale(float s)
        {
            Time.timeScale = Mathf.Max(0f, s);
            // #244 운영자 fb "게임 멈췄을때 사운드 계속 들리는 버그" — 일시정지(timeScale=0)면
            //  AudioListener 도 멈춰 BGM/효과음/앰비언트 전부 정지(the reference sim 도 정지 시 게임
            //  사운드 멈춤).  재개 시 해제.
            AudioListener.pause = (Time.timeScale == 0f);
            if (s > 0f) lastNonPauseScale = s;
            // 운영자 2026-07-24 "일시정지 아닌데 안내": 재개 시 시작 시 띄운
            //  '일시정지 — 둘러보고...' 카드가 수명만큼 잔존하던 것 즉시 해소.
            if (s > 0f) AlertStackUI.Resolve("일시정지");
            OnScaleChanged?.Invoke(s);
        }

        public void TogglePause()
        {
            if (IsPaused) SetScale(lastNonPauseScale);
            else SetScale(0f);
        }
    }
}
