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
        [SerializeField] private int maxEntries = 4;

        private readonly Queue<string> entries = new Queue<string>();

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
            entries.Enqueue(ev.Formatted);
            while (entries.Count > maxEntries) entries.Dequeue();
            Refresh();
        }

        private void Refresh()
        {
            if (logText == null) return;
            logText.text = string.Join("\n\n", entries);
        }
    }
}
