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
        private Text dateText;
        private Text seasonText;

        // 갱신 throttle — 값이 바뀐 분(minute)/일(day) 에만 string 재구성 (GC 절감)
        private int lastShownMinute = -1;
        private int lastShownHour = -1;
        private int lastShownDay = -1;

        private void Awake()
        {
            // 1) TopBar 내 옛 좌상단 슬롯 비우기 (RequireComponent 로 Text 는 항상 존재).
            //    GO/Text 컴포넌트는 남겨두되 글자만 공백 → 우하단 클러스터가 유일한 readout.
            var oldTxt = GetComponent<Text>();
            if (oldTxt != null) oldTxt.text = string.Empty;

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

            // 패널 root — 우하단 앵커.  하단 명령바(GuiControlBar, y=24, 높이≈72) 위에
            //  떠 있도록 bottom margin 을 명령바 위로 잡는다 (the reference sim 우하단 배치).
            const float panelW = 200f;
            const float panelH = 96f;
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

            timeText   = MakeLine(content, "ClockTime",   font, 26, FontStyle.Bold,   UITheme.AccentGold);
            dateText   = MakeLine(content, "ClockDate",   font, 20, FontStyle.Normal, UITheme.TextPrimary);
            seasonText = MakeLine(content, "ClockSeason", font, 18, FontStyle.Normal, UITheme.TextSecondary);
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
            if (timeText == null || dateText == null || seasonText == null) return;

            int d = GameClock.Instance.Day;
            int h = GameClock.Instance.Hour;
            int m = GameClock.Instance.Minute;
            if (d == lastShownDay && h == lastShownHour && m == lastShownMinute) return;
            lastShownDay = d; lastShownHour = h; lastShownMinute = m;

            // 1) 시각 — 12시간 + AM/PM (Hour 0-23).
            timeText.text = Format12Hour(h, m);

            // 2/3) the reference sim 달력 파생 (Day 1-based → 0-based d0).
            int d0 = d - 1;
            int year = StartYear + d0 / DaysPerYear;
            int quadrumIndex = (d0 / DaysPerQuadrum) % QuadrumsPerYear; // 0..3
            int dayInQuadrum = (d0 % DaysPerQuadrum) + 1;               // 1..15
            string season = SeasonNames[quadrumIndex];                  // 봄/여름/가을/겨울

            // the reference sim 세계관 정합: 회계용어 "N분기" 대신 계절명을 날짜 전면에.
            //  예) "봄 1일, 5500년" — 계절을 첫머리에 둬 the reference sim 의 "Spring 1st, 5500"
            //  감성과 맞춘다.  연도는 뒤에 붙여 보조 정보로.
            dateText.text = $"{season} {dayInQuadrum}일, {year}년";
            // 보조줄: 계절을 라벨로 단독 표기 (날짜줄과 톤 분리; 시각/계절 표시 유지).
            seasonText.text = $"{season}철";
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
