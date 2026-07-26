using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #3 - 레퍼런스 콜로니심 vanilla 패턴.
    ///  비-drafted pawn 으로 entity 우클릭 → 컨텍스트 메뉴 ("벌목 우선", "채집 우선" 등) 팝업.
    ///  메뉴 항목 클릭 → 그 action 강제 우선 실행.
    ///
    /// drafted pawn 은 메뉴 X, 직접 move/attack (기존 동작).
    /// 빈 곳 우클릭 (undrafted) 은 manual move 유지 (실험적 - the reference sim 는 메뉴 없음).
    /// </summary>
    public class ContextMenuUI : MonoBehaviour
    {
        private static ContextMenuUI _instance;
        public static ContextMenuUI Instance => _instance;

        private Canvas canvas;
        private RectTransform panelRt;
        private Image bg;
        private Font font;
        private List<GameObject> currentItems = new List<GameObject>();
        private float openTime = -10f;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var c = Object.FindFirstObjectByType<Canvas>();
            if (c == null) return;
            var go = new GameObject("ContextMenuUI");
            go.transform.SetParent(c.transform, false);
            _instance = go.AddComponent<ContextMenuUI>();
            _instance.canvas = c;
        }

        private void Awake()
        {
            font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(16);   // #7.7 — 사본 제거
            panelRt = gameObject.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0, 0);
            panelRt.anchorMax = new Vector2(0, 0);
            panelRt.pivot = new Vector2(0, 1);
            panelRt.sizeDelta = new Vector2(200, 0);  // height grows by items
            bg = gameObject.AddComponent<Image>();
            bg.color = MelonS.GameProto.Core.UITheme.PanelBg;
            // #ui백로그 7.5 — bordered-panel 계약: 무테두리 raw 패널은 어두운 지형에서
            //  경계가 흐릿했다.  자식 재배치 없는 최소 준수 = Divider 색 2px Outline.
            var outline = gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = MelonS.GameProto.Core.UITheme.Divider;
            outline.effectDistance = new Vector2(2f, -2f);
            gameObject.SetActive(false);
        }

        public void Open(Vector2 screenPos, List<(string label, System.Action action)> items)
        {
            // 기존 항목 정리
            foreach (var it in currentItems) if (it != null) Destroy(it);
            currentItems.Clear();
            if (items == null || items.Count == 0) { Close(); return; }

            // 항목 생성 (위에서 아래로)
            float itemHeight = 28f;
            var itemLabels = new System.Collections.Generic.List<Text>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                int idx = i;
                var label = items[i].label;
                var action = items[i].action;
                var bgo = new GameObject($"Item_{i}");
                bgo.transform.SetParent(transform, false);
                var brt = bgo.AddComponent<RectTransform>();
                brt.anchorMin = new Vector2(0, 1);
                brt.anchorMax = new Vector2(1, 1);
                brt.pivot = new Vector2(0, 1);
                brt.sizeDelta = new Vector2(0, itemHeight);
                brt.anchoredPosition = new Vector2(0, -itemHeight * idx);
                var itemImg = bgo.AddComponent<Image>();
                // 감사 rank1: base 가 alpha=0 이라 Button tint(곱연산) 가 항상 0 → hover 피드백
                //  전무했음.  base=white 로 두고 state 색을 직접 지정(평소 투명 → hover/press 시
                //  warm 톤 표시).  곱연산이라도 base white 면 state 색이 그대로 나온다.
                itemImg.color = Color.white;
                var btn = bgo.AddComponent<Button>();
                var cb = btn.colors;
                cb.normalColor      = new Color(1, 1, 1, 0f);                       // 평소 투명
                cb.highlightedColor = MelonS.GameProto.Core.UITheme.BtnHover;       // hover = warm 강조
                cb.pressedColor     = MelonS.GameProto.Core.UITheme.AccentOrange;   // press = orange
                cb.fadeDuration     = 0.08f;
                btn.colors = cb;
                // #게임필2 — 명령 ACK: 메뉴 항목 실행이 들린다 (클릭→명령 접수 확인)
                btn.onClick.AddListener(() => { AudioBank.Instance?.PlaySelect(); action?.Invoke(); Close(); });

                var txtGo = new GameObject("Label");
                txtGo.transform.SetParent(bgo.transform, false);
                var t = txtGo.AddComponent<Text>();
                t.text = label;
                t.font = font;
                t.fontSize = 16;
                t.color = MelonS.GameProto.Core.UITheme.TextPrimary;   // #7.5 — 하드코딩 제거
                t.alignment = TextAnchor.MiddleLeft;
                t.raycastTarget = false;
                var trt = txtGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = new Vector2(-16, 0);
                trt.anchoredPosition = new Vector2(8, 0);
                itemLabels.Add(t);
                currentItems.Add(bgo);
            }
            // UI가독성 (운영자 2026-06-11 "글자가 ui밖을 벗어남") — 고정 200px 가
            //  긴 라벨('울타리 문 건설' 등)을 잘랐다.  라벨 preferredWidth 실측으로
            //  content-fit (200~340 클램프).  item rect 는 stretch 라 폭을 따라온다.
            float maxLabelW = 0f;
            foreach (var lbl in itemLabels)
                if (lbl != null) maxLabelW = Mathf.Max(maxLabelW, lbl.preferredWidth);
            float fitW = Mathf.Clamp(maxLabelW + 32f, 200f, 340f);
            panelRt.sizeDelta = new Vector2(fitW, itemHeight * items.Count + 4);

            // 위치 (mouse 옆) — #275 화면 가장자리 클램프: pivot(0,1)이라 우/하로 확장하므로
            //  메뉴 우측·하단이 캔버스 밖으로 잘리지 않게 보정.
            Vector2 localPos;
            var canvasRt = canvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRt, screenPos, null, out localPos);
            Rect cr = canvasRt.rect;
            float pw = panelRt.sizeDelta.x, ph = panelRt.sizeDelta.y;

            // 좌표계 정합 (2026-07-27 운영자 "취소버튼이 이상한곳에 있음"의 원인):
            //  ScreenPointToLocalPointInRectangle 은 캔버스 **pivot(중앙) 원점** 좌표를 준다.
            //  반면 이 패널은 anchorMin=anchorMax=(0,0) 이라 anchoredPosition 이
            //  **캔버스 좌하단 원점**으로 해석된다.  변환 없이 대입하면 메뉴가 클릭 지점에서
            //  화면 절반(W/2, H/2)만큼 어긋나 항상 좌하단 근처에 떴다.
            localPos.x -= cr.xMin;
            localPos.y -= cr.yMin;

            // pivot(0,1) = 좌상단 기준이라 우측·아래로 펼쳐진다 → 우/하단 잘림 방지 클램프.
            localPos.x = Mathf.Clamp(localPos.x, 0f, Mathf.Max(0f, cr.width - pw));
            localPos.y = Mathf.Clamp(localPos.y, Mathf.Min(cr.height, ph), cr.height);
            panelRt.anchoredPosition = localPos;
            // 운영자 #34 "나무 벌목 서브메뉴 안 뜸": 메뉴는 GameManager init 때 캔버스에 일찍
            //  추가돼 이후 생성되는 HUD 패널들(topbar/inspect 등)이 더 높은 sibling index 로
            //  위에 그려져 메뉴를 가렸음(z-order 관리 부재).  열 때마다 최상단 형제로 올려
            //  항상 모든 UI 위에 보이게 한다.
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            openTime = Time.unscaledTime;
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public bool IsOpen => gameObject.activeSelf;

        private void Update()
        {
            // open 직후 같은 frame 의 left-click 으로 닫지 않게 0.1s grace
            if (!IsOpen) return;
            if (Time.unscaledTime - openTime < 0.1f) return;
            // 메뉴 밖 click = close.  ★#226 운영자 fb "오브젝트 우클릭 기능 전혀 동작 안 함"
            //  root cause: 항목 버튼 onClick 은 마우스 UP 에 발동하는데, 과거 코드는 overMenu 를
            //  항상 false 로 둬 마우스 DOWN 에 무조건 Close → 버튼이 비활성화되어 UP 의 onClick 이
            //  영영 안 발동 = 메뉴 항목 전혀 실행 안 됨.  FIX: 클릭이 메뉴 패널 위면 닫지 않는다
            //  (버튼이 down→up 을 받아 onClick 실행 후 스스로 Close).  패널 밖 클릭만 닫는다.
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                bool overMenu = RectTransformUtility.RectangleContainsScreenPoint(
                    panelRt, Input.mousePosition, null);
                if (!overMenu) Close();
            }
        }
    }
}
