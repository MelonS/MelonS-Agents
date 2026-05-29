using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Wave W-M6-02 Lane B14 / wiki row B14 (M6, Dim6 UX) — the missing in-game
    /// hotkey cheat-sheet overlay.
    ///
    /// RimWorld convention: a controls / hotkey panel consolidates every binding
    /// in one place so the player isn't forced to memorize scattered keys.  Today
    /// PawnSim's hotkeys live across ~13 separate handlers (ArchitectMenu,
    /// BuildManager build modes, TimeController speed, ClickSelector draft,
    /// the designation toggles, GameSaveButtons, the panel toggles) with NO single
    /// reference surface — a legibility gap.
    ///
    /// Wiki acceptance criterion (B14, binary):
    ///   "Pressing H toggles a bordered panel listing all build/speed/draft
    ///    hotkeys; pressing H (or ESC) again hides it."
    ///
    /// Lane contract (STRICT file isolation): this wave may add ONLY this one
    /// file.  It MUST NOT edit GuiControlBar.cs, ArchitectMenu.cs, BuildManager.cs,
    /// UITheme.cs (read-only), or any SceneSetup*.cs.  So HotkeyCheatSheet is
    /// SELF-BOOTSTRAPPING, exactly like AlertStackUI:
    ///   - a [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] spawns one persistent
    ///     manager GameObject which builds its OWN centered Canvas in the shared
    ///     UITheme bordered-panel style;
    ///   - an idempotency guard (FindFirstObjectByType) prevents a second overlay
    ///     on scene/domain reload.
    ///
    /// Key choice: H.  Confirmed NOT in the taken set at authoring time
    /// (B/F/G/T/Y/L/K/J build modes, R draft, X deconstruct, M mine, P grow,
    /// N research, Alpha1-3 speed, Space pause, F1/F4/F5/F8/F9 panels/save,
    /// Escape close).  H is free — no collision, no flag needed.
    ///
    /// runInBackground note (bug-pattern #9): this introduces NO coroutine / no
    /// always-on background timer.  Toggle is an instant SetActive in Update on a
    /// single GetKeyDown — nothing here adds a background-only heartbeat that
    /// would freeze on focus loss during qa.py.
    ///
    /// Text-input firewall: H is swallowed while a UI text field (e.g. save-name
    /// entry) holds focus, detected via EventSystem.currentSelectedGameObject
    /// carrying an InputField / TMP input — so typing an "h" into a field never
    /// pops the overlay.
    ///
    /// The hotkey rows below are sourced READ-ONLY from the live handlers
    /// (verified at authoring 2026-05-30):
    ///   BuildManager.cs   B=Wall F=Floor G=Door T=Stove Y=Bed L=Lamp
    ///                     K=StoneFloor J=Table+Chair
    ///   ClickSelector.cs  R=Draft toggle (selected pawn)
    ///   DeconstructDesignation.cs X / MineDesignation.cs M / GrowZoneDesignation.cs P
    ///   TimeController.cs Alpha1=1x Alpha2=2x Alpha3=4x Space=Pause
    ///   GameSaveButtons.cs F5=Save F9=Load
    ///   ArchitectMenu F8 / WorkTabUI F1 / ScheduleUI F4 / ResearchUI N
    ///   (this overlay) H=toggle, Esc=close.
    /// </summary>
    public class HotkeyCheatSheet : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tweak without code) ----
        [Header("Toggle")]
        [SerializeField] private KeyCode toggleKey = KeyCode.H;

        [Header("Panel feel")]
        [SerializeField] private float panelWidth = 420f;
        [SerializeField] private int titleFontSize = 22;
        [SerializeField] private int sectionFontSize = 16;
        [SerializeField] private int rowFontSize = 15;
        [SerializeField] private float rowHeight = 22f;
        [SerializeField] private float sectionGap = 10f;
        [SerializeField] private float keyColWidth = 92f;

        // ---- runtime state ----
        private Canvas canvas;
        private RectTransform panelRoot;   // the bordered frame (toggled on/off)
        private Font titleFont;
        private Font bodyFont;
        private bool visible;

        // One immutable section table — built once, no per-frame allocation.
        private static readonly Section[] Sections =
        {
            new Section("건설 / Build", new[]
            {
                new Row("B", "벽 / Wall"),
                new Row("F", "바닥 / Floor"),
                new Row("G", "문 / Door"),
                new Row("T", "화덕 / Stove"),
                new Row("Y", "침대 / Bed"),
                new Row("L", "조명 / Lamp"),
                new Row("K", "돌바닥 / Stone Floor"),
                new Row("J", "탁자+의자 / Table+Chair"),
            }),
            new Section("지정 / Designation", new[]
            {
                new Row("X", "철거 / Deconstruct"),
                new Row("M", "채굴 / Mine"),
                new Row("P", "재배 구역 / Grow Zone"),
            }),
            new Section("선택 / Selection", new[]
            {
                new Row("R", "징집 토글 / Draft selected pawn"),
                new Row("LMB-Drag", "다중 선택 / Marquee select"),
                new Row("RMB", "이동 명령 / Move order"),
            }),
            new Section("속도 / Speed", new[]
            {
                new Row("1", "1배속 / Normal"),
                new Row("2", "2배속 / Fast"),
                new Row("3", "4배속 / Fastest"),
                new Row("Space", "일시정지 / Pause"),
            }),
            new Section("패널 / Panels", new[]
            {
                new Row("N", "연구 / Research"),
                new Row("F1", "작업 우선순위 / Work"),
                new Row("F4", "일정 / Schedule"),
                new Row("F8", "건축 메뉴 / Architect"),
            }),
            new Section("저장 / Save", new[]
            {
                new Row("F5", "저장 / Save"),
                new Row("F9", "불러오기 / Load"),
            }),
            new Section("도움말 / Help", new[]
            {
                new Row("H", "이 창 토글 / Toggle this panel"),
                new Row("Esc", "닫기 / Close"),
            }),
        };

        // ============================================================
        // Self-bootstrap (no SceneSetup edit) — one persistent manager.
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Idempotent: never spawn a second overlay on scene/domain reload.
            if (FindExisting() != null) return;
            var go = new GameObject("~HotkeyCheatSheet");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<HotkeyCheatSheet>();
        }

        private static HotkeyCheatSheet FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<HotkeyCheatSheet>();
#else
            return Object.FindObjectOfType<HotkeyCheatSheet>();
#endif
        }

        private void Awake()
        {
            titleFont = UITheme.LoadKoreanFont(titleFontSize);
            bodyFont = UITheme.LoadKoreanFont(rowFontSize);
            BuildCanvas();
            SetVisible(false);
        }

        private void Update()
        {
            // Toggle on H.  Esc only closes (never opens), so it doesn't fight
            // the designation/menu Esc-cancel handlers when nothing is open.
            if (Input.GetKeyDown(toggleKey) && !IsTypingInTextField())
            {
                SetVisible(!visible);
                return;
            }
            if (visible && Input.GetKeyDown(KeyCode.Escape))
            {
                SetVisible(false);
            }
        }

        /// <summary>
        /// True when a UI text field currently holds focus, so a literal "h"
        /// keystroke is text input rather than a toggle request.
        /// </summary>
        private static bool IsTypingInTextField()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var sel = es.currentSelectedGameObject;
            if (sel == null) return false;
            // Legacy uGUI InputField (and any TMP input, found by component name
            // without taking a hard TMP dependency).
            if (sel.GetComponent<InputField>() != null) return true;
            var comps = sel.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null) continue;
                if (comps[i].GetType().Name.Contains("InputField")) return true;
            }
            return false;
        }

        private void SetVisible(bool on)
        {
            visible = on;
            if (panelRoot != null) panelRoot.gameObject.SetActive(on);
        }

        // ============================================================
        // Canvas + panel construction (built once in Awake).
        // ============================================================
        private void BuildCanvas()
        {
            // Own Canvas (separate object) so we never touch any existing UI
            // hierarchy.  Screen-space overlay, high sort order so the cheat
            // sheet sits above world + other panels.
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Center-anchored bordered panel.  Height computed from the section
            // table so the frame always fits its contents.
            float contentHeight = MeasureContentHeight();
            float panelHeight = contentHeight + UITheme.PadOuter * 2f
                                + titleFontSize + UITheme.RowGap * 2f;

            var rootGo = new GameObject("HotkeyPanel");
            rootGo.transform.SetParent(transform, false);
            panelRoot = rootGo.AddComponent<RectTransform>();
            panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            panelRoot.pivot = new Vector2(0.5f, 0.5f);
            panelRoot.anchoredPosition = Vector2.zero;
            panelRoot.sizeDelta = new Vector2(panelWidth, panelHeight);

            // Shared bordered-panel treatment: Divider border on all 4 edges +
            // PanelBg fill + PadOuter inner padding rhythm.
            var content = UITheme.MakeBorderedPanel(panelRoot, border: UITheme.BorderPx,
                                                    bg: UITheme.PanelBg, pad: UITheme.PadOuter);

            BuildContent(content, panelHeight);
        }

        private float MeasureContentHeight()
        {
            float h = 0f;
            for (int s = 0; s < Sections.Length; s++)
            {
                h += sectionFontSize + UITheme.RowGap;       // section header
                h += Sections[s].rows.Length * rowHeight;     // rows
                h += sectionGap;                              // gap after section
            }
            return h;
        }

        private void BuildContent(RectTransform content, float panelHeight)
        {
            // --- header strip (HeaderBg) with AccentGold title ---
            float headerH = titleFontSize + UITheme.RowGap * 2f;
            var headerGo = new GameObject("HeaderStrip");
            headerGo.transform.SetParent(content, false);
            var headerRt = headerGo.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0f, headerH);
            var headerImg = headerGo.AddComponent<Image>();
            headerImg.color = UITheme.HeaderBg;
            headerImg.raycastTarget = false;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerRt, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = new Vector2(8f, 0f);
            titleRt.offsetMax = new Vector2(-8f, 0f);
            var titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = titleFont;
            titleTxt.fontSize = titleFontSize;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = UITheme.AccentGold;
            titleTxt.raycastTarget = false;
            titleTxt.text = "단축키 / Hotkeys  (H)";

            // --- a Divider line just under the header ---
            var divGo = new GameObject("HeaderDivider");
            divGo.transform.SetParent(content, false);
            var divRt = divGo.AddComponent<RectTransform>();
            divRt.anchorMin = new Vector2(0f, 1f);
            divRt.anchorMax = new Vector2(1f, 1f);
            divRt.pivot = new Vector2(0.5f, 1f);
            divRt.anchoredPosition = new Vector2(0f, -headerH);
            divRt.sizeDelta = new Vector2(0f, UITheme.BorderPx);
            var divImg = divGo.AddComponent<Image>();
            divImg.color = UITheme.Divider;
            divImg.raycastTarget = false;

            // --- rows, laid out top-down from below the header divider ---
            float y = -(headerH + UITheme.RowGap);
            for (int s = 0; s < Sections.Length; s++)
            {
                var sec = Sections[s];

                AddSectionHeader(content, sec.title, y);
                y -= (sectionFontSize + UITheme.RowGap);

                for (int r = 0; r < sec.rows.Length; r++)
                {
                    AddRow(content, sec.rows[r], y);
                    y -= rowHeight;
                }
                y -= sectionGap;
            }
        }

        private void AddSectionHeader(RectTransform content, string text, float y)
        {
            var go = new GameObject("Section");
            go.transform.SetParent(content, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(panelWidth, sectionFontSize + UITheme.RowGap);
            var txt = go.AddComponent<Text>();
            txt.font = titleFont;
            txt.fontSize = sectionFontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.LowerLeft;
            txt.color = UITheme.AccentOrange;
            txt.raycastTarget = false;
            txt.text = text;
        }

        private void AddRow(RectTransform content, Row row, float y)
        {
            // key cell (boxed, tan accent text) + label cell (cream body text).
            var rowGo = new GameObject("Row");
            rowGo.transform.SetParent(content, false);
            var rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, y);
            rowRt.sizeDelta = new Vector2(panelWidth, rowHeight);

            // key chip background (PanelBgLight) so the key reads as a "button".
            var chipGo = new GameObject("KeyChip");
            chipGo.transform.SetParent(rowRt, false);
            var chipRt = chipGo.AddComponent<RectTransform>();
            chipRt.anchorMin = new Vector2(0f, 0.5f);
            chipRt.anchorMax = new Vector2(0f, 0.5f);
            chipRt.pivot = new Vector2(0f, 0.5f);
            chipRt.sizeDelta = new Vector2(keyColWidth, rowHeight - 2f);
            chipRt.anchoredPosition = new Vector2(4f, 0f);
            var chipImg = chipGo.AddComponent<Image>();
            chipImg.color = UITheme.PanelBgLight;
            chipImg.raycastTarget = false;

            // Key text must live in a CHILD GO — Unity forbids Image + Text on same GO.
            var keyTextGo = new GameObject("KeyText");
            keyTextGo.transform.SetParent(chipRt, false);
            var keyTextRt = keyTextGo.AddComponent<RectTransform>();
            keyTextRt.anchorMin = Vector2.zero;
            keyTextRt.anchorMax = Vector2.one;
            keyTextRt.offsetMin = Vector2.zero;
            keyTextRt.offsetMax = Vector2.zero;
            var keyTxt = keyTextGo.AddComponent<Text>();
            keyTxt.font = bodyFont;
            keyTxt.fontSize = rowFontSize;
            keyTxt.fontStyle = FontStyle.Bold;
            keyTxt.alignment = TextAnchor.MiddleCenter;
            keyTxt.color = UITheme.AccentTan;
            keyTxt.raycastTarget = false;
            keyTxt.text = row.key;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowRt, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(keyColWidth + 14f, 0f);
            labelRt.offsetMax = new Vector2(-4f, 0f);
            var labelTxt = labelGo.AddComponent<Text>();
            labelTxt.font = bodyFont;
            labelTxt.fontSize = rowFontSize;
            labelTxt.alignment = TextAnchor.MiddleLeft;
            labelTxt.color = UITheme.TextPrimary;
            labelTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelTxt.verticalOverflow = VerticalWrapMode.Truncate;
            labelTxt.raycastTarget = false;
            labelTxt.text = row.label;
        }

        // ---- immutable row/section data (no per-frame allocation) ------------
        private readonly struct Row
        {
            public readonly string key;
            public readonly string label;
            public Row(string key, string label) { this.key = key; this.label = label; }
        }

        private readonly struct Section
        {
            public readonly string title;
            public readonly Row[] rows;
            public Section(string title, Row[] rows) { this.title = title; this.rows = rows; }
        }
    }
}
