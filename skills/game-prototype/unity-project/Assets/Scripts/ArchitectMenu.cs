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
        private RectTransform panelContent;   // #UI-restyle U7 — inner content RT (inside border+fill)
        private Font font;
        private bool isOpen = false;
        private GameObject contentRoot;
        private string activeCategory = "";

        // #UI-restyle U7 (Round 5) — share the control bar's exact button palette.
        private static readonly Color InactiveBg = MelonS.GameProto.Core.UITheme.BtnInactiveBg;
        private static readonly Color HeaderBg   = MelonS.GameProto.Core.UITheme.HeaderBg;

        // wiki Dim2 acceptance #5: clicking any UI button plays the blip.
        //  Route category/buildable button onClick through the EXISTING
        //  AudioBank.PlaySelect() (same soft-blip pawn-select uses). Find-once +
        //  cache (ClickSelector cached-reference pattern); null no-op if absent.
        private AudioBank cachedAudio;
        private bool audioResolved;

        private void PlayClickBlip()
        {
            if (!audioResolved)
            {
                cachedAudio = AudioBank.Instance != null
                    ? AudioBank.Instance
                    : Object.FindFirstObjectByType<AudioBank>();
                audioResolved = true;
            }
            if (cachedAudio != null) cachedAudio.PlaySelect();
        }

        // 림월드 vanilla 패턴 — 카테고리별 buildable 목록
        private static readonly Dictionary<string, (BuildManager.Mode mode, string label, int cost)[]> Categories = new()
        {
            ["Structure (구조)"] = new[] {
                (BuildManager.Mode.Wall,      "벽 (목재 5)",   5),
                (BuildManager.Mode.WallStone, "벽 (석재 5)",   5),  // #127
                (BuildManager.Mode.Door,      "문 (목재 3)",   3),
            },
            // W-M4-06 (#21 / W-M4-05 QA flag) — surface the stone/paved floor in the
            //   Architect catalogue.  The stone floor (BuildManager.Mode.FloorStone,
            //   hotkey K, paid 석재 1, V59b move-bonus 1.50x > wood 1.30x) already
            //   EXISTS and is fully functional via the K hotkey; it was only missing
            //   from this menu (qa.json flags_for_next_wave).  It sits as a SECOND
            //   buildable inside the EXISTING "Floors (바닥)" flooring category —
            //   mirroring how WallStone sits alongside Wall under Structure — so both
            //   floor variants are grouped and NO new category is introduced (no
            //   collision with Lane A's Furniture entries).  The existing buildable
            //   onClick wiring in RefreshContent routes every (mode,label,cost) tuple
            //   through BuildManager.Instance.SetMode, so clicking 석재 바닥 enters
            //   Mode.FloorStone (CurrentMode == Mode.FloorStone) with ZERO
            //   click-plumbing change.  Cost 1 = stone, matches BuildManager
            //   .floorStoneCost.  Mode.FloorStone is used READ-ONLY (not added/edited).
            ["Floors (바닥)"] = new[] {
                (BuildManager.Mode.Floor,      "나무 바닥 (목재 1)", 1),
                (BuildManager.Mode.FloorStone, "석재 바닥 (석재 1)", 1),
            },
            ["Furniture (가구)"] = new[] {
                // #154 - wiki: sleeping spot 0.8x / wood bed 1.0x / fine 1.4x
                (BuildManager.Mode.BedSleepingSpot, "수면 자리 (자재 X)",     0),
                (BuildManager.Mode.Bed,             "목재 침대 (목재 8)",     8),
                (BuildManager.Mode.BedFine,         "고급 침대 (목재 30)",   30),
                // W-M4-06 (#20) — table+chair eat/rec spot (hotkey J, 목재 6).
                //   Flagged by Lane A for QA wiring: Mode.TableChair routes through
                //   the existing SetMode/Close onClick plumbing with ZERO further change.
                (BuildManager.Mode.TableChair,      "탁자+의자 (목재 6)",     6),
            },
            ["Production (생산)"] = new[] {
                (BuildManager.Mode.Stove, "화덕 (목재 10)", 10),
            },
            // W-M4-05 (#42) — surface the W-M4-04 Lamp buildable in the Architect
            //   catalogue (was L-hotkey-only, per qa.json flags_for_next_wave #42).
            //   Catalogue line ONLY: the existing buildable onClick wiring in
            //   RefreshContent routes every (mode,label,cost) tuple through
            //   BuildManager.Instance.SetMode — so clicking 램프 enters Lamp build
            //   mode (CurrentMode == Mode.Lamp) with ZERO click-plumbing change.
            //   Cost 4 = wood, matches BuildManager.lampCost.  Mode.Lamp already
            //   exists in BuildManager (not added/edited here).  A dedicated
            //   "Lighting (조명)" category keeps the lamp out of Production's
            //   crafting-station grouping and leaves room for future light types.
            ["Lighting (조명)"] = new[] {
                (BuildManager.Mode.Lamp, "램프 (목재 4)", 4),
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
            // #UI-restyle U9 — route through UITheme (no per-script font fallback drift).
            font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(18);
            rt = gameObject.AddComponent<RectTransform>();
            // 좌측 stack - TopBar 아래 + GuiControlBar 위.  size 280x440.
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(280, 440);
            rt.anchoredPosition = new Vector2(12, 0);

            // #UI-restyle U7 — same MakeBorderedPanel the control bar / inspector use:
            //   warm-brown fill + Divider border on all 4 edges (was a flat borderless rect).
            //   PadOuter inset so the header + buttons breathe inside the frame.
            panelContent = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(
                rt, MelonS.GameProto.Core.UITheme.BorderPx, MelonS.GameProto.Core.UITheme.PanelBg,
                MelonS.GameProto.Core.UITheme.PadOuter);

            BuildMenu();
            gameObject.SetActive(false);
        }

        private void BuildMenu()
        {
            // 헤더 strip — HeaderBg 띠 + AccentGold 제목 (모든 패널 헤더와 동일 톤).
            var headerStripGo = new GameObject("HeaderStrip");
            headerStripGo.transform.SetParent(panelContent, false);
            var hsImg = headerStripGo.AddComponent<Image>();
            hsImg.color = HeaderBg;
            hsImg.raycastTarget = false;
            var hsrt = headerStripGo.GetComponent<RectTransform>();
            hsrt.anchorMin = new Vector2(0, 1);
            hsrt.anchorMax = new Vector2(1, 1);
            hsrt.pivot = new Vector2(0.5f, 1);
            hsrt.sizeDelta = new Vector2(0, 36);
            hsrt.anchoredPosition = new Vector2(0, 0);

            // 제목 (gold, 헤더 띠 위)
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerStripGo.transform, false);
            var t = titleGo.AddComponent<Text>();
            t.text = "🏛 건축 (F8)";
            t.font = font;
            t.fontSize = 20;
            t.fontStyle = FontStyle.Bold;
            t.color = MelonS.GameProto.Core.UITheme.AccentGold;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            trt.anchoredPosition = Vector2.zero;

            // content 영역 (카테고리 list + 펼친 buildables) — 헤더 strip 아래.
            contentRoot = new GameObject("Content");
            contentRoot.transform.SetParent(panelContent, false);
            var crt = contentRoot.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0);
            crt.anchorMax = new Vector2(1, 1);
            crt.offsetMin = new Vector2(0, 0);
            crt.offsetMax = new Vector2(0, -44);   // leave room for the 36px header + 8 gap
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
                    new Vector2(0, -y), MelonS.GameProto.Core.UITheme.HeaderBg,
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
                            new Vector2(16, -y), MelonS.GameProto.Core.UITheme.BtnInactiveBg,
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

            // #UI-restyle U7 — identical treatment to GuiControlBar.MakeBtn:
            //   root Image = Divider border; inset PanelFill = the body color; Button
            //   targets the FILL so hover/pressed tint the body, border stays consistent.
            var img = go.AddComponent<Image>();
            img.color = MelonS.GameProto.Core.UITheme.Divider;
            var fillRt = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(brt, 2f, col);
            var fillImg = fillRt.parent.GetComponent<Image>();   // PanelFill image
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = fillImg;
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor     = new Color(0.72f, 0.72f, 0.72f, 1f);
            cb.selectedColor    = Color.white;
            cb.fadeDuration     = 0.06f;
            btn.colors = cb;
            // wiki #5 — central chokepoint: every architect button blips on click.
            btn.onClick.AddListener(() => { PlayClickBlip(); onClick?.Invoke(); });

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var t = txtGo.AddComponent<Text>();
            t.text = label;
            t.font = font;
            t.fontSize = 16;
            t.fontStyle = FontStyle.Bold;
            t.color = MelonS.GameProto.Core.UITheme.TextPrimary;
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
