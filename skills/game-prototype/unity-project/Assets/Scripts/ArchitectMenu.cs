using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #5+#10 - 림월드 Architect 메뉴 패턴.
    ///  좌측 collapsible 카테고리 패널: Structure / Furniture / Production / Misc.
    ///  카테고리 클릭 → 하위 buildable 펼침.  Buildable 클릭 → BuildManager.SetMode.
    ///
    /// F8 키 토글.  GuiControlBar 의 build 5버튼 (벽/바닥/문/화덕/침대) 대체.
    /// Self-bootstrap (GameManager.EnsureInScene).
    /// </summary>
    public class ArchitectMenu : MonoBehaviour
    {
        private static ArchitectMenu _instance;
        public static ArchitectMenu Instance => _instance;

        private RectTransform rt;
        private Image bg;
        private Font font;
        private bool isOpen = false;
        private GameObject contentRoot;
        private string activeCategory = "";

        // 림월드 vanilla 패턴 — 카테고리별 buildable 목록
        private static readonly Dictionary<string, (BuildManager.Mode mode, string label, int cost)[]> Categories = new()
        {
            ["Structure (구조)"] = new[] {
                (BuildManager.Mode.Wall,      "벽 (목재 5)",   5),
                (BuildManager.Mode.WallStone, "벽 (석재 5)",   5),  // #127
                (BuildManager.Mode.Door,      "문 (목재 3)",   3),
            },
            ["Floors (바닥)"] = new[] {
                (BuildManager.Mode.Floor, "나무 바닥 (목재 1)", 1),
            },
            ["Furniture (가구)"] = new[] {
                (BuildManager.Mode.Bed,   "침대 (목재 8)",  8),
            },
            ["Production (생산)"] = new[] {
                (BuildManager.Mode.Stove, "화덕 (목재 10)", 10),
            },
        };

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("ArchitectMenu");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<ArchitectMenu>();
        }

        private void Awake()
        {
            font = LoadKoreanFont();
            rt = gameObject.AddComponent<RectTransform>();
            // 좌측 stack - TopBar 아래 + GuiControlBar 위.  size 280x440.
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(280, 440);
            rt.anchoredPosition = new Vector2(12, 0);
            bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.08f, 0.10f, 0.93f);

            BuildMenu();
            gameObject.SetActive(false);
        }

        private Font LoadKoreanFont()
        {
            string[] candidates = { "Malgun Gothic", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" };
            foreach (var name in candidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(name, 18);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void BuildMenu()
        {
            // 제목
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(transform, false);
            var t = titleGo.AddComponent<Text>();
            t.text = "🏛 건축 (F8)";
            t.font = font;
            t.fontSize = 22;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(0.95f, 0.92f, 0.85f, 1f);
            t.alignment = TextAnchor.UpperCenter;
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(0, 36);
            trt.anchoredPosition = new Vector2(0, -8);

            // content 영역 (카테고리 list + 펼친 buildables)
            contentRoot = new GameObject("Content");
            contentRoot.transform.SetParent(transform, false);
            var crt = contentRoot.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0);
            crt.anchorMax = new Vector2(1, 1);
            crt.sizeDelta = new Vector2(-16, -56);
            crt.anchoredPosition = new Vector2(0, -16);
            RefreshContent();
        }

        private void RefreshContent()
        {
            // 기존 children 정리
            for (int i = contentRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.transform.GetChild(i).gameObject);

            float y = 0;
            foreach (var kv in Categories)
            {
                string catName = kv.Key;
                var items = kv.Value;
                // 카테고리 헤더 (toggle)
                var headerGo = MakeBtn(contentRoot.transform, catName,
                    new Vector2(0, -y), new Color(0.20f, 0.22f, 0.25f, 0.95f),
                    () => { activeCategory = (activeCategory == catName) ? "" : catName; RefreshContent(); });
                var ht = headerGo.GetComponentInChildren<Text>();
                ht.text = (activeCategory == catName ? "▼ " : "▶ ") + catName;
                ht.fontStyle = FontStyle.Bold;
                y += 36;
                // 펼친 buildables (active 카테고리만)
                if (activeCategory == catName)
                {
                    foreach (var (mode, label, cost) in items)
                    {
                        var bcap = mode;  // closure capture
                        MakeBtn(contentRoot.transform, label,
                            new Vector2(16, -y), new Color(0.13f, 0.15f, 0.18f, 0.95f),
                            () => {
                                if (BuildManager.Instance != null)
                                {
                                    var newMode = (BuildManager.Instance.CurrentMode == bcap)
                                        ? BuildManager.Mode.Off : bcap;
                                    BuildManager.Instance.SetMode(newMode);
                                    Close();
                                }
                            });
                        y += 32;
                    }
                }
            }
        }

        private GameObject MakeBtn(Transform parent, string label, Vector2 pos, Color col, System.Action onClick)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var brt = go.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1);
            brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(0, 1);
            brt.sizeDelta = new Vector2(-pos.x, 30);
            brt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = col;
            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            cb.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick?.Invoke());
            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
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
            return go;
        }

        public void Toggle() { if (isOpen) Close(); else Open(); }
        public void Open() { isOpen = true; gameObject.SetActive(true); }
        public void Close() { isOpen = false; gameObject.SetActive(false); }

        private void Update()
        {
            // F8 림월드 Architect 단축키
            if (Input.GetKeyDown(KeyCode.F8)) Toggle();
        }
    }
}
