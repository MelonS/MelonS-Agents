using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Bottom-center event log — shows last N AI Director events.
    /// Subscribes to AIDirector.OnEventFired.  Fades older entries.
    /// </summary>
    public class EventLogUI : MonoBehaviour
    {
        [SerializeField] private AIDirector director;
        [SerializeField] private Text logText;
        [SerializeField] private Image panelBg;
        [SerializeField] private int maxEntries = 3;
        // #ui백로그 7.1 — 시간 만료 없이는 마지막 3개 이벤트가 우상단을 영원히 가림
        //  (1188s 시점에도 초반 이벤트 잔존).  영속이 필요한 위협은 AlertStack 카드 담당.
        [SerializeField] private float entryLifetimeSec = 45f;

        private struct Entry { public string text; public float time; }
        private readonly Queue<Entry> entries = new Queue<Entry>();

        private void OnEnable()
        {
            if (director != null) director.OnEventFired += HandleEvent;
            Refresh();
        }

        private void OnDisable()
        {
            if (director != null) director.OnEventFired -= HandleEvent;
        }

        private void HandleEvent(GameEvent ev)
        {
            entries.Enqueue(new Entry { text = ev.Formatted, time = Time.unscaledTime });
            while (entries.Count > maxEntries) entries.Dequeue();
            Refresh();
        }

        private void Update()
        {
            // #7.1 — 만료 항목 제거.  큐 선두가 가장 오래된 항목이므로 선두만 검사하면 충분.
            bool changed = false;
            while (entries.Count > 0 && Time.unscaledTime - entries.Peek().time >= entryLifetimeSec)
            {
                entries.Dequeue();
                changed = true;
            }
            if (changed) Refresh();
        }

        private void Refresh()
        {
            if (logText == null) return;
            var sb = new System.Text.StringBuilder();
            foreach (var e in entries)
            {
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(e.text);
            }
            logText.text = sb.ToString();
            // Day 15: hide panel background when no events (don't leave a
            // dead black rectangle in the corner).
            if (panelBg != null) panelBg.enabled = entries.Count > 0;
        }
    }
}
