using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #3 - 림월드 vanilla 패턴.
    ///  비-drafted pawn 으로 entity 우클릭 → 컨텍스트 메뉴 ("벌목 우선", "채집 우선" 등) 팝업.
    ///  메뉴 항목 클릭 → 그 action 강제 우선 실행.
    ///
    /// drafted pawn 은 메뉴 X, 직접 move/attack (기존 동작).
    /// 빈 곳 우클릭 (undrafted) 은 manual move 유지 (실험적 - RimWorld 는 메뉴 없음).
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
            font = LoadKoreanFont();
            panelRt = gameObject.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0, 0);
            panelRt.anchorMax = new Vector2(0, 0);
            panelRt.pivot = new Vector2(0, 1);
            panelRt.sizeDelta = new Vector2(200, 0);  // height grows by items
            bg = gameObject.AddComponent<Image>();
            bg.color = MelonS.GameProto.Core.UITheme.PanelBg;
            gameObject.SetActive(false);
        }

        private Font LoadKoreanFont()
        {
            string[] candidates = { "Malgun Gothic", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" };
            foreach (var name in candidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(name, 16);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public void Open(Vector2 screenPos, List<(string label, System.Action action)> items)
        {
            // 기존 항목 정리
            foreach (var it in currentItems) if (it != null) Destroy(it);
            currentItems.Clear();
            if (items == null || items.Count == 0) { Close(); return; }

            // 항목 생성 (위에서 아래로)
            float itemHeight = 28f;
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
                itemImg.color = new Color(0.10f, 0.12f, 0.16f, 0.0f);
                var btn = bgo.AddComponent<Button>();
                var cb = btn.colors;
                cb.normalColor = new Color(1, 1, 1, 0.95f);
                cb.highlightedColor = new Color(1.25f, 1.25f, 1.10f, 1f);
                cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                btn.colors = cb;
                btn.onClick.AddListener(() => { action?.Invoke(); Close(); });

                var txtGo = new GameObject("Label");
                txtGo.transform.SetParent(bgo.transform, false);
                var t = txtGo.AddComponent<Text>();
                t.text = label;
                t.font = font;
                t.fontSize = 16;
                t.color = new Color(0.95f, 0.92f, 0.85f, 1f);
                t.alignment = TextAnchor.MiddleLeft;
                t.raycastTarget = false;
                var trt = txtGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = new Vector2(-16, 0);
                trt.anchoredPosition = new Vector2(8, 0);
                currentItems.Add(bgo);
            }
            panelRt.sizeDelta = new Vector2(200, itemHeight * items.Count + 4);

            // 위치 (mouse 옆, screen edge 안)
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(), screenPos, null, out localPos);
            panelRt.anchoredPosition = localPos;
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
            // 메뉴 밖 click = close
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                bool overMenu = false;
                if (EventSystem.current != null)
                {
                    // 단순화 - menu 영역 안 check 안 하고 닫음 + 클릭 처리 우선
                    //  Button.onClick 이 먼저 fire 후 이 Update 가 fire (메뉴 닫힘).
                }
                if (!overMenu) Close();
            }
        }
    }
}
