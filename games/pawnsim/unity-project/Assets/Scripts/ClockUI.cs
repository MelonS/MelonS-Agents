using UnityEngine;
using UnityEngine.UI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 9 origin: 단순 "N일차 - HH:MM" 좌상단 시계.
    ///
    /// #clock-cluster (2026-06-01): the reference sim 우하단 info cluster 로 이전 + 확장.
    ///   the reference sim 는 화면 우하단(하단 명령바 오른쪽 끝 바로 위)에 시각/날짜/계절을
    ///   한 덩어리로 표시한다.  날짜/계절은 GameClock.Day(1-based) 하나에서 전부
    ///   파생 가능 (밸런스 0 영향) 하므로 데이터 소스는 GameClock 그대로 두고
    ///   표시만 바꾼다.  온도/날씨 등 생존-밸런스 시스템은 운영자 결정 전까지 표시 안 함.
    ///
    /// 이 컴포넌트는 SceneSetup 이 TopBar 좌측 Text GO 에 AddComponent 한다.
    ///   클래스명/부트스트랩(Awake) 진입점은 유지하되, Awake 에서
    ///     1) 기존 좌상단 Text 를 비워(공백) 두고 (TopBar 내 자기 슬롯 제거),
    ///     2) 메인 Canvas 루트에 우하단 bordered panel 을 새로 구성해 3줄을 그린다.
    ///   → date readout 은 화면에 정확히 하나 (the reference sim 와 동일; 둘이면 테스트 빌드처럼 보임).
    ///
    /// Headless/-batchmode 안전: Canvas/Text 없으면 null no-op (GuiControlBar 패턴 미러).
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class ClockUI : MonoBehaviour
    {
        // the reference sim 달력 상수 (위키 Time/Calendar 정합)
        //  1년 = 4분기(quadrum) × 15일 = 60일.  온대 바이옴 분기→계절:
        //  0 Aprimay→봄, 1 Jugust→여름, 2 Septober→가을, 3 Decembary→겨울.
        private const int DaysPerQuadrum = 15;
        private const int QuadrumsPerYear = 4;
        private const int DaysPerYear = DaysPerQuadrum * QuadrumsPerYear; // 60
        private const int StartYear = 5500;
        private static readonly string[] SeasonNames = { "봄", "여름", "가을", "겨울" };

        // 우하단 클러스터 텍스트들 (Canvas 루트에 생성)
        private Text timeText;
        // 우하단 클러스터는 '시각' 전담.  날짜(달력)는 좌상단 topBarDate 가 표시(중복 제거).
        private Text topBarDate;   // TopBar 좌측 gold Text — 빈 슬롯을 날짜로 채움(운영자 fb)

        // 갱신 throttle — 값이 바뀐 분(minute)/일(day) 에만 string 재구성 (GC 절감)
        private int lastShownMinute = -1;
        private int lastShownHour = -1;
        private int lastShownDay = -1;

        private void Awake()
        {
            // 1) TopBar 내 옛 좌상단 슬롯 비우기 (RequireComponent 로 Text 는 항상 존재).
            //    GO/Text 컴포넌트는 남겨두되 글자만 공백 → 우하단 클러스터가 유일한 readout.
            // 좌상단 슬롯(TopBar 가 만든 gold Text)을 비우지 않고 '날짜(달력)' readout 으로
            //  살린다 — 운영자 "탑바 좌측 빈 슬롯".  우하단 클러스터는 '시각' 전담 → 중복 없음.
            topBarDate = GetComponent<Text>();

            BuildBottomRightCluster();
        }

        private void BuildBottomRightCluster()
        {
            // GuiControlBar 와 동일하게 기존 HUD 가 쓰는 Canvas 를 재사용.
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                // headless/-batchmode: Canvas 없으면 조용히 skip (null throw 금지).
                Debug.LogWarning("[ClockUI] Canvas 없음 - 우하단 클러스터 skip");
                return;
            }

            var font = UITheme.LoadKoreanFont(22);

            // 패널 root — 우하단 앵커.  하단 명령바(GuiControlBar, y=24, 높이~72) 위에
            //  떠 있도록 bottom margin 을 명령바 위로 잡는다 (the reference sim 우하단 배치).
            const float panelW = 170f;
            const float panelH = 52f;   // 시각 단일 라인 → 컴팩트(날짜는 좌상단)
            const float marginRight = 16f;
            const float marginBottom = 120f; // #275 속도패널(높이 72, y24~96) 위 안전 간격(108→120)

            var panelGo = new GameObject("ClockCluster");
            panelGo.transform.SetParent(canvas.transform, false);
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot = new Vector2(1f, 0f);
            panelRt.sizeDelta = new Vector2(panelW, panelH);
            panelRt.anchoredPosition = new Vector2(-marginRight, marginBottom);

            // 공통 bordered panel (warm brown + 2px border) — 나머지 HUD 와 톤 통일.
            var content = UITheme.MakeBorderedPanel(panelRt, UITheme.BorderPx, UITheme.PanelBg, UITheme.PadOuter);

            // 3줄을 위→아래, 우측정렬로.  VerticalLayoutGroup 으로 간단 배치.
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleRight;
            vlg.spacing = UITheme.RowGap;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 우하단은 시각만 (날짜는 좌상단).  단일 라인이라 패널도 컴팩트.
            timeText   = MakeLine(content, "ClockTime",   font, 28, FontStyle.Normal /* BitBit 자체 볼드 — 중첩 금지 (2026-07-25) */,   UITheme.AccentGold);
        }

        private static Text MakeLine(RectTransform parent, string name, Font font,
                                     int size, FontStyle style, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = col;
            t.alignment = TextAnchor.MiddleRight;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = size + 6f;
            return t;
        }

        private void Update()
        {
            if (GameClock.Instance == null) return;
            // headless 등으로 클러스터가 안 만들어졌으면 no-op.
            if (timeText == null) return;

            int d = GameClock.Instance.Day;
            int h = GameClock.Instance.Hour;
            int m = GameClock.Instance.Minute;
            if (d == lastShownDay && h == lastShownHour && m == lastShownMinute) return;
            lastShownDay = d; lastShownHour = h; lastShownMinute = m;

            // 1) 시각 — 12시간 + AM/PM (Hour 0-23).
            timeText.text = Format12Hour(h, m);

            // 2/3) the reference sim 달력 파생 (Day 1-based → 0-based d0).
            int d0 = d - 1;
            //  (연도는 2026-08-01 표기에서 제거 — 아래 topBarDate 주석 참조.
            //   StartYear/DaysPerYear 상수는 세이브 호환을 위해 남겨 둔다.)
            int quadrumIndex = (d0 / DaysPerQuadrum) % QuadrumsPerYear; // 0..3
            int dayInQuadrum = (d0 % DaysPerQuadrum) + 1;               // 1..15
            string season = SeasonNames[quadrumIndex];                  // 봄/여름/가을/겨울

            // the reference sim 세계관 정합: 회계용어 "N분기" 대신 계절명을 날짜 전면에.
            //  예) "봄 1일, 5500년" — 계절을 첫머리에 둬 the reference sim 의 "Spring 1st, 5500"
            //  감성과 맞춘다.  연도는 뒤에 붙여 보조 정보로.
            // 좌상단 = 날짜(달력) readout.  우하단 = 시각 전담 → 중복 없이 정보 분리
            //  (운영자: 탑바 좌측 빈 슬롯 채움).
            // 2026-08-01 — 연도 표기 제거.  '봄 1일, 5500년' 의 5500 은 근거 없는 숫자이고,
            //  처음 보는 사람에게 아무 정보도 주지 않으면서 상단 제일 눈에 띄는 자리를
            //  차지했다.  대신 **정착 며칠째**를 보여준다 — 이 게임에서 실제로 의미 있는
            //  수치이고(습격 시점·작물 성장의 기준), 플레이어가 '얼마나 버텼나' 로 읽는다.
            if (topBarDate != null) topBarDate.text = $"{season} {dayInQuadrum}일  ·  정착 {d}일째";
        }

        /// <summary>24시간 Hour/Minute → "6:08 AM" / "12:00 PM" 형식.</summary>
        private static string Format12Hour(int hour24, int minute)
        {
            string ampm = hour24 < 12 ? "AM" : "PM";
            int h12 = hour24 % 12;
            if (h12 == 0) h12 = 12; // 0시/12시는 12 로 표기 (12 AM / 12 PM)
            return $"{h12}:{minute:00} {ampm}";
        }
    }
}
