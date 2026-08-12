using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 8: top-right speed indicator: "▶ 1x" / "▶▶ 2x" / "▶▶▶ 4x" / "|| PAUSED".
    /// Polls TimeController via Update (singleton-subscriber pattern).
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class TimeUI : MonoBehaviour
    {
        // 감사 rank2: 하드코딩 팔레트 → UITheme 토큰 직접 참조(탑바 색 불일치 영구 해소).
        // ⚠ `static readonly … = UITheme.X` 로 두지 않는다.  UITheme 의 색은
        //  `PlayerPrefs.GetInt("ui_palette")` 를 타는데, Unity 는 **필드 초기화
        //  시점의 PlayerPrefs 접근을 금지**한다:
        //    UnityException: GetInt is not allowed to be called from a MonoBehaviour
        //    constructor (or instance field initializer)
        //  → TypeInitializationException 으로 번져 이 UI 가 통째로 죽는다.
        //  (2026-08-07 제출 영상 녹화 로그에서 발견 — 화면엔 티가 안 나서
        //   예외를 읽기 전까지 몰랐다.)  프로퍼티로 두면 **쓰는 시점**에 평가된다.
        private static Color TextPrimary => MelonS.GameProto.Core.UITheme.TextPrimary;
        private static Color AccentWarn => MelonS.GameProto.Core.UITheme.TextDanger;

        private Text txt;
        private float lastShown = -1f;

        private void Awake()
        {
            txt = GetComponent<Text>();
            Refresh(1f);
        }

        private void Update()
        {
            float cur = TimeController.Instance != null ? TimeController.Instance.CurrentScale : 1f;
            if (Mathf.Approximately(cur, lastShown)) return;
            Refresh(cur);
        }

        private void Refresh(float s)
        {
            if (txt == null) return;
            if (s <= 0f)         { txt.text = "|| 멈춤"; txt.color = AccentWarn; }   // #ui백로그 2.7 — 전한글 HUD 톤 통일 (하단 버튼 '멈춤'과 일치)
            else if (s <= 1.01f) { txt.text = "▶ 1x";       txt.color = TextPrimary; }
            else if (s <= 2.01f) { txt.text = "▶▶ 2x";      txt.color = TextPrimary; }
            else                 { txt.text = $"▶▶▶ {s:0}x"; txt.color = TextPrimary; }
            lastShown = s;
        }
    }
}
