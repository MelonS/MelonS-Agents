using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MelonS.GameProto
{
    /// <summary>
    /// #126 - 레퍼런스 콜로니심 Schedule tab.
    ///  표: 행 = pawn, 열 = 24 시간.  cell 클릭 = 순환 (Anytime → Sleep → Work → Joy → Anytime).
    ///  F4 토글.  WorkTabUI 와 동일 패턴.
    /// </summary>
    public class ScheduleUI : MonoBehaviour
    {
        private static ScheduleUI _instance;
        public static ScheduleUI Instance => _instance;

        private RectTransform rt;
        private Image bg;
        private Font font;
        private bool isOpen = false;
        private GameObject grid;
        private const float RowHeight = 26f;
        private const float ColWidth = 22f;
        private const float NameColWidth = 100f;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("ScheduleUI");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<ScheduleUI>();
        }

        private void Awake()
        {
            font = LoadKoreanFont();
            rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float w = NameColWidth + ColWidth * 24 + 24;
            float h = 360;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            bg = gameObject.AddComponent<Image>();
            bg.color = MelonS.GameProto.Core.UITheme.PanelBg;
            BuildShell();
            gameObject.SetActive(false);
        }

        private Font LoadKoreanFont()
        {
            var bundled = Resources.Load<Font>("Fonts/GowunDodum") ?? Resources.Load<Font>("Fonts/NotoSansKR");
            if (bundled != null) return bundled;
            string[] candidates = { "Malgun Gothic", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" };
            foreach (var n in candidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(n, 16);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void BuildShell()
        {
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(transform, false);
            var t = titleGo.AddComponent<Text>();
            t.text = "일정 (F4) - 클릭하여 시간대별 행동 변경";   // UI백로그 #5.11 — 이모지 제거(tofu 위험)
            t.font = font; t.fontSize = 20; t.fontStyle = FontStyle.Normal /* BitBit 자체 볼드 — 중첩 금지 (2026-07-25) */;
            t.color = MelonS.GameProto.Core.UITheme.AccentGold;   // 감사 rank5: 제목 골드
            t.alignment = TextAnchor.UpperCenter;
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1); trt.sizeDelta = new Vector2(0, 32);
            trt.anchoredPosition = new Vector2(0, -6);

            // legend
            var legendGo = new GameObject("Legend");
            legendGo.transform.SetParent(transform, false);
            var lt = legendGo.AddComponent<Text>();
            // #ui백로그 5.9 — 범례를 정본(PawnSchedule.SlotColors/Labels)에서 파생: hex
            //  하드코딩이 이미 1색 drift('작업' #4cc070 vs 정본 #4DC073) + '자유' 색 견본
            //  누락이었다.  루프 생성으로 desync 원천 차단 (자유 포함 4종 전부 색 표시).
            var legendSb = new System.Text.StringBuilder("  ");
            for (int li = 0; li < PawnSchedule.SlotLabels.Length; li++)
            {
                if (li > 0) legendSb.Append("  |  ");
                legendSb.Append("<color=#")
                        .Append(ColorUtility.ToHtmlStringRGB(PawnSchedule.SlotColors[li]))
                        .Append(">").Append(PawnSchedule.SlotLabels[li]).Append("</color>");
            }
            lt.text = legendSb.ToString();
            lt.font = font; lt.fontSize = 14;
            lt.color = MelonS.GameProto.Core.UITheme.TextSecondary;
            lt.alignment = TextAnchor.LowerCenter;
            lt.supportRichText = true;
            var lrt = legendGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0);
            lrt.pivot = new Vector2(0.5f, 0); lrt.sizeDelta = new Vector2(0, 18);
            lrt.anchoredPosition = new Vector2(0, 4);

            grid = new GameObject("Grid");
            grid.transform.SetParent(transform, false);
            var grt = grid.AddComponent<RectTransform>();
            grt.anchorMin = new Vector2(0, 0); grt.anchorMax = new Vector2(1, 1);
            grt.sizeDelta = new Vector2(-16, -60); grt.anchoredPosition = new Vector2(0, -8);
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            for (int i = grid.transform.childCount - 1; i >= 0; i--)
                Destroy(grid.transform.GetChild(i).gameObject);

            // 헤더 - 시간 0~23
            float xCursor = NameColWidth;
            for (int h = 0; h < 24; h++)
            {
                MakeHeaderCell(grid.transform, h.ToString("00"), new Vector2(xCursor, 0));
                xCursor += ColWidth;
            }

            var pawns = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            // #ui백로그 5.12 — 이름 1차 정렬: 콜로니스트 바(#2.6)·직업 탭과 행 순서 일치
            System.Array.Sort(pawns, (a, b) =>
            {
                int byName = string.CompareOrdinal(a.PawnName, b.PawnName);
                return byName != 0 ? byName : a.GetInstanceID().CompareTo(b.GetInstanceID());
            });
            float y = -RowHeight;
            for (int r = 0; r < pawns.Length; r++)
            {
                var p = pawns[r];
                if (p == null || p.IsDead) continue;   // #ui백로그 5.2 — 시체 행 제거
                var sch = p.GetComponent<PawnSchedule>();
                if (sch == null) continue;
                // QA(2026-06-14): p.name("Pawn(Clone)") → PawnName(표시명). WorkTabUI 와 동일 수정.
                string nmS = string.IsNullOrEmpty(p.PawnName) ? p.name.Replace("(Clone)", "") : p.PawnName;
                MakeNameCell(grid.transform, nmS, new Vector2(0, y));
                xCursor = NameColWidth;
                for (int h = 0; h < 24; h++)
                {
                    var schCap = sch; int hCap = h;
                    MakeSlotCell(grid.transform, sch.slots[h], new Vector2(xCursor, y),
                        () => {
                            int cur = (int)schCap.slots[hCap];
                            schCap.SetSlot(hCap, (TimeSlot)((cur + 1) % 4));
                            RefreshGrid();
                        });
                    xCursor += ColWidth;
                }
                y -= RowHeight;
            }
        }

        private void MakeHeaderCell(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject($"H_{label}");
            go.transform.SetParent(parent, false);
            var brt = go.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 1); brt.sizeDelta = new Vector2(ColWidth, RowHeight);
            brt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = MelonS.GameProto.Core.UITheme.PanelBgLight;   // 감사 rank5: 헤더셀 warm
            var tgo = new GameObject("Lbl");
            tgo.transform.SetParent(go.transform, false);
            var t = tgo.AddComponent<Text>();
            t.text = label; t.font = font; t.fontSize = 13; t.fontStyle = FontStyle.Normal /* BitBit 자체 볼드 — 중첩 금지 (2026-07-25) */;  // #audit3 #11 가독성 11→13
            t.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero; trt.anchoredPosition = Vector2.zero;
        }

        private void MakeNameCell(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject($"N_{label}");
            go.transform.SetParent(parent, false);
            var brt = go.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 1); brt.sizeDelta = new Vector2(NameColWidth, RowHeight);
            brt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = MelonS.GameProto.Core.UITheme.PanelBg;   // 감사 rank5: 이름셀 warm
            var tgo = new GameObject("Lbl");
            tgo.transform.SetParent(go.transform, false);
            var t = tgo.AddComponent<Text>();
            t.text = label; t.font = font; t.fontSize = 13;
            t.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.sizeDelta = new Vector2(-12, 0); trt.anchoredPosition = new Vector2(8, 0);
        }

        private void MakeSlotCell(Transform parent, TimeSlot slot, Vector2 pos, System.Action onClick)
        {
            var go = new GameObject($"S_{(int)slot}");
            go.transform.SetParent(parent, false);
            var brt = go.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 1); brt.sizeDelta = new Vector2(ColWidth - 1, RowHeight - 1);
            brt.anchoredPosition = new Vector2(pos.x + 0.5f, pos.y - 0.5f);
            var img = go.AddComponent<Image>();
            img.color = PawnSchedule.SlotColors[(int)slot];
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
        }

        public void Toggle() { if (isOpen) Close(); else Open(); }
        // #275 최상단.  #ui백로그 5.3 — 중앙 팝업 3종 상호배타 (WorkTabUI 와 대칭).
        public void Open()
        {
            if (WorkTabUI.Instance != null && WorkTabUI.Instance.IsOpen) WorkTabUI.Instance.Close();
            var ru = Object.FindFirstObjectByType<ResearchUI>();
            if (ru != null && ru.PickerOpen) ru.ClosePicker();
            isOpen = true; gameObject.SetActive(true); transform.SetAsLastSibling(); RefreshGrid();
        }
        public void Close() { isOpen = false; gameObject.SetActive(false); }
        public bool IsOpen => isOpen;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F4)) Toggle();
            // #ui백로그 5.3 — ESC 닫기 (SimInput 경유 — 규칙 2)
            if (isOpen && SimInput.GetKeyDown(KeyCode.Escape)) Close();
        }
    }
}
