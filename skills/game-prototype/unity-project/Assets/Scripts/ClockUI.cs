using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>Day 9: top-right clock display "Day 1 - 06:00".</summary>
    [RequireComponent(typeof(Text))]
    public class ClockUI : MonoBehaviour
    {
        private Text txt;
        private int lastShownMinute = -1;
        private int lastShownHour = -1;
        private int lastShownDay = -1;

        private void Awake()
        {
            txt = GetComponent<Text>();
            if (txt != null) txt.text = "1일차 - 06:00";
        }

        private void Update()
        {
            if (GameClock.Instance == null || txt == null) return;
            int d = GameClock.Instance.Day;
            int h = GameClock.Instance.Hour;
            int m = GameClock.Instance.Minute;
            if (d != lastShownDay || h != lastShownHour || m != lastShownMinute)
            {
                txt.text = $"{d}일차 - {h:00}:{m:00}";
                lastShownDay = d; lastShownHour = h; lastShownMinute = m;
            }
        }
    }
}
