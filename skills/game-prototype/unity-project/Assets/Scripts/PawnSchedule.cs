using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #126 - 레퍼런스 콜로니심 Schedule tab (단순화).
    ///  24 시간 grid × per-pawn × (Anytime/Sleep/Work/Joy).
    ///  AI Decide 가 현재 시간대 slot 따름 - Sleep 시간엔 작업 skip.
    ///
    /// 기본값: 0~6 Sleep, 7~21 Work, 22 Joy, 23 Sleep.
    /// </summary>
    public enum TimeSlot { Anytime, Sleep, Work, Joy }

    public class PawnSchedule : MonoBehaviour
    {
        public TimeSlot[] slots = new TimeSlot[24];

        public static readonly string[] SlotLabels = { "자유", "수면", "작업", "여가" };
        public static readonly Color[] SlotColors = {
            new Color(0.55f, 0.55f, 0.60f, 1f),  // Anytime grey
            new Color(0.30f, 0.40f, 0.85f, 1f),  // Sleep blue
            new Color(0.30f, 0.75f, 0.45f, 1f),  // Work green
            new Color(0.95f, 0.60f, 0.25f, 1f),  // Joy orange
        };

        private void Awake()
        {
            for (int h = 0; h < 24; h++)
            {
                if (h <= 6 || h >= 23) slots[h] = TimeSlot.Sleep;
                else if (h == 22) slots[h] = TimeSlot.Joy;
                else slots[h] = TimeSlot.Work;
            }
        }

        public TimeSlot GetCurrentSlot()
        {
            int hour = 12;  // fallback noon
            if (GameClock.Instance != null) hour = Mathf.Clamp(GameClock.Instance.Hour, 0, 23);
            return slots[hour];
        }

        public void SetSlot(int hour, TimeSlot s)
        {
            if (hour < 0 || hour > 23) return;
            slots[hour] = s;
        }
    }
}
