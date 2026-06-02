using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 8: top-right speed indicator: "▶ 1x" / "▶▶ 2x" / "▶▶▶ 4x" / "❚❚ PAUSED".
    /// Polls TimeController via Update (singleton-subscriber pattern).
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class TimeUI : MonoBehaviour
    {
        // 감사 rank2: 하드코딩 팔레트 → UITheme 토큰 직접 참조(탑바 색 불일치 영구 해소).
        private static readonly Color TextPrimary = MelonS.GameProto.Core.UITheme.TextPrimary;
        private static readonly Color AccentWarn  = MelonS.GameProto.Core.UITheme.TextDanger;

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
            if (s <= 0f)         { txt.text = "❚❚ PAUSED"; txt.color = AccentWarn; }
            else if (s <= 1.01f) { txt.text = "▶ 1x";       txt.color = TextPrimary; }
            else if (s <= 2.01f) { txt.text = "▶▶ 2x";      txt.color = TextPrimary; }
            else                 { txt.text = $"▶▶▶ {s:0}x"; txt.color = TextPrimary; }
            lastShown = s;
        }
    }
}
