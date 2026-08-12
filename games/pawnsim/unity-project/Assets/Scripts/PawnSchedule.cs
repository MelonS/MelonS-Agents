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
        // TOP-9 (visual-polish-backlog 2026-06-11): 채도 100% 원색이 월드 웜톤 팔레트와
        //  충돌 — 같은 의미를 유지한 채 머티드 톤으로 (수면 슬레이트블루/작업 세이지/
        //  여가 앰버/자유 웜그레이).
        public static readonly Color[] SlotColors = {
            new Color(0.50f, 0.47f, 0.43f, 1f),  // Anytime warm grey
            new Color(0.24f, 0.35f, 0.55f, 1f),  // Sleep slate blue
            new Color(0.31f, 0.48f, 0.31f, 1f),  // Work sage green
            new Color(0.55f, 0.42f, 0.24f, 1f),  // Joy amber
        };

        private void Awake()
        {
            for (int h = 0; h < 24; h++)
            {
                // 2026-07-30 — `h <= 6` 이었다.  그런데 **게임은 6:00 AM 에 시작한다**.
                //  즉 시작하자마자 전원이 '일정상 취침' 이었다.  침대가 없던 동안에는
                //  GoSleepAction 이 실패해 증상이 안 보였는데, 시작 캠프에 침대를 넣자
                //  전원이 개시 직후 침대로 걸어가고 **플레이어의 직접 명령(우클릭 벌목)까지
                //  밀려났다** — `p1-chop-selected-only` 가 "지훈의 실제 활동: 취침 이동"으로
                //  잡아냈다.  기상 시각을 게임 시작 시각과 맞춘다: 23:00~05:59 수면, 6시 기상.
                if (h < 6 || h >= 23) slots[h] = TimeSlot.Sleep;
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
