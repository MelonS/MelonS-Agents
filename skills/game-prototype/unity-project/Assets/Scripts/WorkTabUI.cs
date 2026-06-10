using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #114 + #8 - 레퍼런스 콜로니심 Work tab 패턴.
    ///  표: 행 = pawn, 열 = work type (벌목/채집/사냥/요리/연구).
    ///  cell 좌클릭 = priority +1 (0→1→2→3→4→0 순환).  우클릭 = 즉시 0 (disable).
    ///  PawnUtilityAI 가 priority 1 부터 순서대로 시도. 0 = skip.
    ///
    /// F1 레퍼런스 콜로니심 Work tab 단축키 토글.  GuiControlBar 에 "직업" 버튼도 추가.
    /// Self-bootstrap via EnsureInScene() (GameManager 가 호출).
    ///
    /// 이전 (#114 전): 모든 pawn 이 same hardcoded action 순서.
    /// 이후: 운영자가 per-pawn 으로 사냥꾼/벌목꾼/요리사 specialize 가능.
    /// </summary>
    public class WorkTabUI : MonoBehaviour
    {
        private static WorkTabUI _instance;
        public static WorkTabUI Instance => _instance;

        private RectTransform rt;
        private Image bg;
        private Font font;
        private bool isOpen = false;
        private GameObject grid;
        private const float RowHeight = 30f;
        private const float ColWidth = 56f;
        private const float NameColWidth = 110f;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("WorkTabUI");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<WorkTabUI>();
        }

        private void Awake()
        {
            font = LoadKoreanFont();
            rt = gameObject.AddComponent<RectTransform>();
            // 화면 중앙 hover - 레퍼런스 콜로니심 Work tab 도 화면 중앙쯤.
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // size: name col + 5 work cols + padding.
            float w = NameColWidth + ColWidth * PawnWorkSettings.AllKinds.Length + 24;
            float h = 380;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            bg = gameObject.AddComponent<Image>();
            bg.color = MelonS.GameProto.Core.UITheme.PanelBg;

            BuildShell();
            gameObject.SetActive(false);
        }

        private Font LoadKoreanFont()
        {
            string[] candidates = { "Malgun Gothic", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" };
            foreach (var n in candidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(n, 18);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void BuildShell()
        {
            // 제목
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(transform, false);
            var t = titleGo.AddComponent<Text>();
            t.text = "📋 직업 우선순위 (F1)";
            t.font = font;
            t.fontSize = 22;
            t.fontStyle = FontStyle.Bold;
            t.color = MelonS.GameProto.Core.UITheme.AccentGold;   // 감사 rank5: 제목 골드
            t.alignment = TextAnchor.UpperCenter;
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(0, 36);
            trt.anchoredPosition = new Vector2(0, -8);

            // 안내 텍스트 (자판 아래)
            var hintGo = new GameObject("Hint");
            hintGo.transform.SetParent(transform, false);
            var ht = hintGo.AddComponent<Text>();
            ht.text = "좌클릭: 우선순위 변경 (0→1→2→3→4)   우클릭: 끔(0)";
            ht.font = font;
            ht.fontSize = 13;
            ht.color = MelonS.GameProto.Core.UITheme.TextSecondary;
            ht.alignment = TextAnchor.LowerCenter;
            var hrt = hintGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0, 0);
            hrt.anchorMax = new Vector2(1, 0);
            hrt.pivot = new Vector2(0.5f, 0);
            hrt.sizeDelta = new Vector2(0, 20);
            hrt.anchoredPosition = new Vector2(0, 6);

            // grid container
            grid = new GameObject("Grid");
            grid.transform.SetParent(transform, false);
            var grt = grid.AddComponent<RectTransform>();
            grt.anchorMin = new Vector2(0, 0);
            grt.anchorMax = new Vector2(1, 1);
            grt.sizeDelta = new Vector2(-16, -72);
            grt.anchoredPosition = new Vector2(0, -6);
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            for (int i = grid.transform.childCount - 1; i >= 0; i--)
                Destroy(grid.transform.GetChild(i).gameObject);

            // 헤더 row (work type 한글 이름).
            float xCursor = NameColWidth;
            for (int c = 0; c < PawnWorkSettings.AllKinds.Length; c++)
            {
                MakeHeaderCell(grid.transform, PawnWorkSettings.KoreanNames[c],
                    new Vector2(xCursor, 0));
                xCursor += ColWidth;
            }

            // pawn rows
            var pawns = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            // 안정적 순서 - GetInstanceID
            System.Array.Sort(pawns, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            float y = -RowHeight;
            for (int r = 0; r < pawns.Length; r++)
            {
                var p = pawns[r];
                // #ui백로그 5.2 — 시체 행 제거: 죽은 폰이 행으로 남아 우선순위 편집까지
                //  가능했다(무효 조작 혼란).  SkillUI 의 기존 !IsDead 패턴과 통일.
                if (p == null || p.IsDead) continue;
                var settings = p.GetComponent<PawnWorkSettings>();
                if (settings == null) continue;

                // name cell
                MakeNameCell(grid.transform, p.name, new Vector2(0, y));

                // priority cells
                xCursor = NameColWidth;
                for (int c = 0; c < PawnWorkSettings.AllKinds.Length; c++)
                {
                    var kind = PawnWorkSettings.AllKinds[c];
                    int pr = settings.GetPriority(kind);
                    var sCap = settings; var kCap = kind;
                    MakePriorityCell(grid.transform, pr, new Vector2(xCursor, y),
                        leftClick: () => {
                            int cur = sCap.GetPriority(kCap);
                            int next = (cur + 1) % 5;  // 0→1→2→3→4→0
                            sCap.SetPriority(kCap, next);
                            RefreshGrid();
                        },
                        rightClick: () => {
                            sCap.SetPriority(kCap, 0);
                            RefreshGrid();
                        });
                    xCursor += ColWidth;
                }
                y -= RowHeight;
            }

            if (pawns.Length == 0)
            {
                var empty = new GameObject("Empty");
                empty.transform.SetParent(grid.transform, false);
                var t = empty.AddComponent<Text>();
                t.text = "(pawn 없음)";
                t.font = font;
                t.fontSize = 16;
                t.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                t.alignment = TextAnchor.MiddleCenter;
                var ert = empty.GetComponent<RectTransform>();
                ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one;
                ert.sizeDelta = Vector2.zero; ert.anchoredPosition = Vector2.zero;
            }

            // #obj-audit #0 — 패널 높이를 pawn 수에 맞춰 동적 조정.  이전엔 380px 고정이라
            //  grid 가용 308px(=380-72) < (헤더1 + pawn N)×30 이면(N≥10) 아래 행이 잘렸다.
            //  필요 높이 = (행 수)×RowHeight + 72(타이틀+힌트 inset) + 하단여백; 화면 안 clamp.
            int rows = (pawns != null ? pawns.Length : 0) + 1;  // +1 헤더 행
            float needed = rows * RowHeight + 72f + 16f;
            float maxH = Screen.height > 0 ? Screen.height - 80f : 900f;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Clamp(needed, 200f, maxH));
        }

        private void MakeHeaderCell(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject($"H_{label}");
            go.transform.SetParent(parent, false);
            var brt = go.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1);
            brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 1);
            brt.sizeDelta = new Vector2(ColWidth, RowHeight);
            brt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = MelonS.GameProto.Core.UITheme.PanelBgLight;   // 감사 rank5: 헤더셀 warm

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var t = txtGo.AddComponent<Text>();
            t.text = label;
            t.font = font;
            t.fontSize = 14;
            t.fontStyle = FontStyle.Bold;
            t.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero; trt.anchoredPosition = Vector2.zero;
        }

        private void MakeNameCell(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject($"N_{label}");
            go.transform.SetParent(parent, false);
            var brt = go.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1);
            brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 1);
            brt.sizeDelta = new Vector2(NameColWidth, RowHeight);
            brt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = MelonS.GameProto.Core.UITheme.PanelBg;   // 감사 rank5: 이름셀 warm

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var t = txtGo.AddComponent<Text>();
            t.text = label;
            t.font = font;
            t.fontSize = 14;
            t.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.sizeDelta = new Vector2(-12, 0); trt.anchoredPosition = new Vector2(8, 0);
        }

        private void MakePriorityCell(Transform parent, int priority, Vector2 pos,
            System.Action leftClick, System.Action rightClick)
        {
            var go = new GameObject($"P_{priority}");
            go.transform.SetParent(parent, false);
            var brt = go.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1);
            brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 1);
            brt.sizeDelta = new Vector2(ColWidth - 2, RowHeight - 2);
            brt.anchoredPosition = new Vector2(pos.x + 1, pos.y - 1);
            var img = go.AddComponent<Image>();
            // 색상 by priority - 레퍼런스 콜로니심 Work tab 의 노랑→빨강 그라데이션 모방.
            //   0 disabled = dark grey
            //   1 highest   = bright yellow
            //   2           = lime
            //   3           = mid green
            //   4 lowest    = teal/blue
            img.color = priority switch
            {
                0 => new Color(0.15f, 0.15f, 0.15f, 1f),
                1 => new Color(0.95f, 0.85f, 0.25f, 1f),
                2 => new Color(0.70f, 0.90f, 0.35f, 1f),
                3 => new Color(0.40f, 0.75f, 0.45f, 1f),
                4 => new Color(0.35f, 0.55f, 0.85f, 1f),
                _ => new Color(0.15f, 0.15f, 0.15f, 1f),
            };
            var click = go.AddComponent<WorkTabCellClick>();
            click.left = leftClick;
            click.right = rightClick;

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var t = txtGo.AddComponent<Text>();
            t.text = priority == 0 ? "·" : priority.ToString();
            t.font = font;
            t.fontSize = 18;
            t.fontStyle = FontStyle.Bold;
            t.color = priority == 0
                ? new Color(0.76f, 0.76f, 0.76f, 1f)   // #audit3 #10 대비 ↑ (0.55→0.76, WCAG AA)
                : new Color(0.10f, 0.10f, 0.10f, 1f);
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero; trt.anchoredPosition = Vector2.zero;
        }

        public void Toggle()
        {
            if (isOpen) Close(); else Open();
        }
        // #275 최상단.  #ui백로그 5.3 — 중앙 팝업 3종(직업/일정/연구) 상호배타: 같은
        //  중앙 자리에 겹쳐 쌓이던 것 해소 (레퍼런스 하단 메뉴는 한 번에 하나).
        public void Open()
        {
            if (ScheduleUI.Instance != null && ScheduleUI.Instance.IsOpen) ScheduleUI.Instance.Close();
            var ru = Object.FindFirstObjectByType<ResearchUI>();
            if (ru != null && ru.PickerOpen) ru.ClosePicker();
            isOpen = true; gameObject.SetActive(true); transform.SetAsLastSibling(); RefreshGrid();
        }
        public void Close() { isOpen = false; gameObject.SetActive(false); }
        public bool IsOpen => isOpen;

        private void Update()
        {
            // F1 레퍼런스 콜로니심 Work tab 단축키
            if (Input.GetKeyDown(KeyCode.F1)) Toggle();
            // #ui백로그 5.3 — ESC 닫기 (designation 모드 7곳의 'ESC=취소' 관례 정합)
            if (isOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }
    }

    /// <summary>
    /// 좌/우클릭 모두 받는 cell click 핸들러.  Button 은 left 만 지원해서 직접 PointerEventData 처리.
    /// </summary>
    public class WorkTabCellClick : MonoBehaviour,
        UnityEngine.EventSystems.IPointerClickHandler
    {
        public System.Action left;
        public System.Action right;

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
        {
            if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left) left?.Invoke();
            else if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right) right?.Invoke();
        }
    }
}
