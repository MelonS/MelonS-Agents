using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Wave W-M6-03 Lane B2 / wiki row B2 (M6, Dim6 UX) — the gizmo command bar
    /// on selection.
    ///
    /// RimWorld convention (InspectGizmoGrid): when a thing is selected, a row of
    /// contextual command buttons appears at the bottom-center of the screen
    /// (Draft / Undraft, Cancel/deselect, etc).  PawnSim already has single-select
    /// (ClickSelector) and, since W-M6-01, multi-select (MarqueeSelector) — but the
    /// only way to act on a selected pawn is a memorized hotkey (R = draft).  This
    /// surfaces those commands as on-screen buttons, the genuine vanilla UX.
    ///
    /// Wiki acceptance criterion (B2, binary):
    ///   "Selecting a pawn shows a Draft button; clicking it drafts the pawn (same
    ///    effect as the R-key); deselecting hides the bar."
    ///
    /// Lane contract (STRICT file isolation): this wave may add ONLY this one file.
    /// It MUST NOT edit ClickSelector.cs / MarqueeSelector.cs (selection is read
    /// READ-ONLY), nor BuildManager.cs / UITheme.cs (read-only) / any SceneSetup*.cs
    /// / any test / any W-M6-01/W-M6-02 in-flight file.  So SelectionGizmoBar is
    /// SELF-BOOTSTRAPPING, exactly like AlertStackUI / HotkeyCheatSheet:
    ///   - a [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] spawns one persistent
    ///     DontDestroyOnLoad manager GameObject which builds its OWN bottom-center
    ///     Canvas in the shared UITheme bordered-panel style;
    ///   - an idempotency guard (FindFirstObjectByType) prevents a second bar on a
    ///     scene/domain reload.
    ///
    /// Selection read path (READ-ONLY, both accessors verified to exist 2026-05-30):
    ///   ClickSelector.CurrentSelection      -> PawnEntity (single-select)
    ///   MarqueeSelector.CurrentMultiSelection / .HasMultiSelection
    ///                                        -> IReadOnlyList&lt;PawnEntity&gt; (box-select)
    /// The bar prefers the marquee list when it is non-empty (marquee owns the
    /// selection in that case — see MarqueeSelector.ClearClickSelectorSingle), else
    /// falls back to the single ClickSelector pawn.  Nothing here writes to either
    /// selector; both managers are POLLED each frame (singleton-subscriber pattern,
    /// bug-pattern #7) rather than subscribed to in OnEnable.
    ///
    /// Draft command path (READ-ONLY invoke, NOT a new behavior): the Draft button
    /// calls the EXACT same public API the R-key uses —
    /// PawnEntity.SetDrafted(!IsDrafted) (ClickSelector.Update line ~59).  With a
    /// multi-selection it applies the toggle to every selected pawn (the bar's
    /// label reflects the FIRST pawn's state).  No new draft logic is introduced.
    ///
    /// runInBackground note (bug-pattern #9): this introduces NO coroutine and no
    /// always-on background timer.  Refresh is a cheap poll inside Update (which is
    /// frame-driven and shares the AutoScreenshotter runInBackground path during
    /// qa.py); the bar's only motion is SetActive + Text/label swaps.  Nothing here
    /// adds a background-only heartbeat that would freeze on OS-focus loss.
    ///
    /// Other bug-pattern firewall: no AudioBank tight loop (button click plays a
    /// single throttle-free blip on a discrete user click, not per-frame — same as
    /// GuiControlBar), no GetInstanceID collision compares, no OnCollision handlers,
    /// no justSpawned-default field.
    /// </summary>
    public class SelectionGizmoBar : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tune day-1 feel) -------
        [Header("Bar layout")]
        [SerializeField] private float buttonWidth = 96f;
        [SerializeField] private float buttonHeight = 40f;
        [SerializeField] private float buttonGap = 8f;
        [SerializeField] private float barBottomMargin = 16f;
        [SerializeField] private int buttonFontSize = 16;
        [SerializeField] private int countFontSize = 14;

        // ---- runtime state ---------------------------------------------------
        private Canvas canvas;
        private RectTransform barRoot;       // the bordered frame (toggled on/off)
        private RectTransform contentRoot;   // inside the border, holds the buttons
        private Font bodyFont;

        // Draft button + its label (built once, mutated as selection changes).
        private Image draftBtnBg;
        private Text draftBtnLabel;
        // Selection-count label ("선택 N" — shown when >1 pawn box-selected).
        private Text countLabel;

        private bool barVisible;

        // Cached selectors (polled, never subscribed) — singleton-subscriber pattern.
        private ClickSelector clickSelector;
        private MarqueeSelector marqueeSelector;

        // Reused selection snapshot — no per-frame List<> allocation (bug: GC churn).
        private readonly List<PawnEntity> selectionBuf = new List<PawnEntity>(32);

        // ============================================================
        //  Self-bootstrap (no SceneSetup edit) — one persistent manager.
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindExisting() != null) return;   // idempotent — never a 2nd bar
            var go = new GameObject("~SelectionGizmoBar");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<SelectionGizmoBar>();
        }

        private static SelectionGizmoBar FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<SelectionGizmoBar>();
#else
            return Object.FindObjectOfType<SelectionGizmoBar>();
#endif
        }

        private void Awake()
        {
            bodyFont = UITheme.LoadKoreanFont(buttonFontSize);
            BuildCanvas();
            SetBarVisible(false);
        }

        private void Update()
        {
            // Poll the current selection (read-only) and rebuild the bar's state.
            //  Polling (not OnEnable subscription) avoids the singleton-subscription
            //  race (bug-pattern #7): the selectors may bootstrap after this manager.
            CollectSelection(selectionBuf);

            if (selectionBuf.Count == 0)
            {
                if (barVisible) SetBarVisible(false);
                return;
            }

            if (!barVisible) SetBarVisible(true);
            RefreshButtons(selectionBuf);
        }

        // ============================================================
        //  Selection read path (READ-ONLY) — marquee list first, else single.
        // ============================================================
        private void CollectSelection(List<PawnEntity> outList)
        {
            outList.Clear();

            EnsureSelectors();

            // Marquee owns selection whenever its box-list is non-empty.
            if (marqueeSelector != null && marqueeSelector.HasMultiSelection)
            {
                var ms = marqueeSelector.CurrentMultiSelection;
                for (int i = 0; i < ms.Count; i++)
                {
                    var p = ms[i];
                    if (p != null && !p.IsDead) outList.Add(p);
                }
                if (outList.Count > 0) return;   // marquee selection wins
            }

            // Else the single ClickSelector pawn (if any, still alive).
            if (clickSelector != null)
            {
                var single = clickSelector.CurrentSelection;
                if (single != null && !single.IsDead) outList.Add(single);
            }
        }

        private void EnsureSelectors()
        {
            if (clickSelector == null)
            {
#if UNITY_2023_1_OR_NEWER
                clickSelector = Object.FindFirstObjectByType<ClickSelector>();
#else
                clickSelector = Object.FindObjectOfType<ClickSelector>();
#endif
            }
            if (marqueeSelector == null)
            {
#if UNITY_2023_1_OR_NEWER
                marqueeSelector = Object.FindFirstObjectByType<MarqueeSelector>();
#else
                marqueeSelector = Object.FindObjectOfType<MarqueeSelector>();
#endif
            }
        }

        // ============================================================
        //  Button refresh — reflects the FIRST pawn's draft state + count.
        // ============================================================
        private void RefreshButtons(List<PawnEntity> sel)
        {
            int n = sel.Count;
            PawnEntity first = sel[0];

            // Draft button label/state mirrors the first pawn — clicking toggles
            //  EVERY selected pawn via the same SetDrafted API the R-key uses.
            bool drafted = first != null && first.IsDrafted;
            if (draftBtnLabel != null)
                draftBtnLabel.text = drafted ? "징집 해제\nUndraft" : "징집\nDraft";
            if (draftBtnBg != null)
                draftBtnBg.color = drafted ? UITheme.BtnActiveBg : UITheme.BtnInactiveBg;
            if (draftBtnLabel != null)
                draftBtnLabel.color = drafted ? UITheme.TextDark : UITheme.TextPrimary;

            // Count line — only meaningful when more than one pawn is selected.
            if (countLabel != null)
            {
                if (n > 1) countLabel.text = $"선택 {n} / Selected {n}";
                else countLabel.text = first != null ? first.PawnName : string.Empty;
            }
        }

        // ============================================================
        //  Command handlers — READ-ONLY invokes of existing public APIs.
        // ============================================================
        private void OnDraftClicked()
        {
            CollectSelection(selectionBuf);
            if (selectionBuf.Count == 0) return;

            // Toggle relative to the FIRST pawn (matches the on-screen label), then
            //  apply that SAME target state to every selected pawn.  SetDrafted is
            //  the EXACT public API ClickSelector's R-key uses — no new behavior.
            bool target = !selectionBuf[0].IsDrafted;
            for (int i = 0; i < selectionBuf.Count; i++)
            {
                var p = selectionBuf[i];
                if (p != null && !p.IsDead) p.SetDrafted(target);
            }
            RefreshButtons(selectionBuf);
        }

        private void OnCancelClicked()
        {
            // "Cancel / Deselect" — drop selection rings so the bar hides next frame.
            //  Read-only on the selectors: we only call PawnEntity.SetSelected(false)
            //  on the currently-selected pawns (the same minimal read-only clear
            //  MarqueeSelector itself uses), never editing ClickSelector/Marquee.
            CollectSelection(selectionBuf);
            for (int i = 0; i < selectionBuf.Count; i++)
            {
                var p = selectionBuf[i];
                if (p != null) p.SetSelected(false);
            }
            // Hide immediately; Update's poll will keep it hidden while nothing is
            //  selected.  (The selectors re-evaluate their own pointers on next click.)
            SetBarVisible(false);
        }

        // ============================================================
        //  Canvas + bar construction (built once in Awake).
        // ============================================================
        private void BuildCanvas()
        {
            // Own Canvas (separate object) so we never touch any existing UI
            //  hierarchy.  Screen-space overlay, sort order below the cheat-sheet
            //  (250) but above world/HUD so the commands sit on top of the map.
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Two command buttons (Draft, Cancel) + count line above them.  Width is
            //  computed from the two buttons; the count label sits inside the frame.
            int buttonCount = 2;
            float innerW = buttonCount * buttonWidth + (buttonCount - 1) * buttonGap;
            float barW = innerW + UITheme.PadOuter * 2f;
            float barH = buttonHeight + countFontSize + UITheme.RowGap
                         + UITheme.PadOuter * 2f;

            // Bottom-center anchored bordered frame.
            var rootGo = new GameObject("GizmoBar");
            rootGo.transform.SetParent(transform, false);
            barRoot = rootGo.AddComponent<RectTransform>();
            barRoot.anchorMin = new Vector2(0.5f, 0f);
            barRoot.anchorMax = new Vector2(0.5f, 0f);
            barRoot.pivot = new Vector2(0.5f, 0f);
            barRoot.anchoredPosition = new Vector2(0f, barBottomMargin);
            barRoot.sizeDelta = new Vector2(barW, barH);

            // Shared bordered-panel treatment: Divider border + PanelBg fill.
            contentRoot = UITheme.MakeBorderedPanel(barRoot, border: UITheme.BorderPx,
                                                    bg: UITheme.PanelBg, pad: UITheme.PadOuter);

            BuildCountLabel();
            BuildButtons(innerW);
        }

        private void BuildCountLabel()
        {
            var go = new GameObject("CountLabel");
            go.transform.SetParent(contentRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, countFontSize + UITheme.RowGap);
            countLabel = go.AddComponent<Text>();
            countLabel.font = bodyFont;
            countLabel.fontSize = countFontSize;
            countLabel.fontStyle = FontStyle.Bold;
            countLabel.alignment = TextAnchor.MiddleCenter;
            countLabel.color = UITheme.AccentGold;
            countLabel.raycastTarget = false;
            countLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            countLabel.verticalOverflow = VerticalWrapMode.Overflow;
            countLabel.text = string.Empty;
        }

        private void BuildButtons(float innerW)
        {
            // Buttons laid out left-to-right along the bottom of the content area.
            float startX = -innerW * 0.5f + buttonWidth * 0.5f;

            // Draft / Undraft — the load-bearing command (B2 acceptance).
            draftBtnBg = MakeButton("DraftBtn", startX, "징집\nDraft",
                                    out draftBtnLabel, OnDraftClicked);

            // Cancel / Deselect.
            float cancelX = startX + buttonWidth + buttonGap;
            MakeButton("CancelBtn", cancelX, "취소\nCancel", out _, OnCancelClicked);
        }

        /// <summary>Builds one themed command button at anchoredX along the bottom
        ///  of the content area.  Returns the button's background Image; out-param
        ///  gives the label Text.  Image + Text live on SEPARATE GameObjects (Unity
        ///  forbids both on one GO).</summary>
        private Image MakeButton(string name, float anchoredX, string label,
                                 out Text labelOut, System.Action onClick)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(contentRoot, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0f);
            btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(anchoredX, 0f);
            btnRt.sizeDelta = new Vector2(buttonWidth, buttonHeight);

            var bg = btnGo.AddComponent<Image>();
            bg.color = UITheme.BtnInactiveBg;
            bg.raycastTarget = true;   // the button must catch clicks

            var btn = btnGo.AddComponent<Button>();
            // Color-tint transition so the AccentOrange/hover states read on press.
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;          // multiplies bg.color
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.onClick.AddListener(() => { PlayClickBlip(); onClick?.Invoke(); });

            // 1px Divider border via an inset child fill is overkill for a button;
            //  the bordered frame already carries the panel border.  Add a thin
            //  Divider outline so each button reads as a discrete chip.
            var outline = btnGo.AddComponent<Outline>();
            outline.effectColor = UITheme.Divider;
            outline.effectDistance = new Vector2(UITheme.BorderPx, -UITheme.BorderPx);

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(btnRt, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(2f, 2f);
            txtRt.offsetMax = new Vector2(-2f, -2f);
            var txt = txtGo.AddComponent<Text>();
            txt.font = bodyFont;
            txt.fontSize = buttonFontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = UITheme.TextPrimary;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.text = label;

            labelOut = txt;
            return bg;
        }

        /// <summary>Single UI-click blip on a discrete user click (NOT per-frame —
        ///  no tight-loop audio, bug-pattern #4).  Routes through the EXISTING
        ///  AudioBank.PlaySelect() — the same UI-click sound GuiControlBar uses for
        ///  every button (read-only; if AudioBank is absent the call is skipped).</summary>
        private AudioBank cachedAudio;
        private void PlayClickBlip()
        {
            if (cachedAudio == null)
            {
#if UNITY_2023_1_OR_NEWER
                cachedAudio = AudioBank.Instance != null ? AudioBank.Instance
                            : Object.FindFirstObjectByType<AudioBank>();
#else
                cachedAudio = AudioBank.Instance != null ? AudioBank.Instance
                            : Object.FindObjectOfType<AudioBank>();
#endif
            }
            if (cachedAudio != null) cachedAudio.PlaySelect();
        }

        private void SetBarVisible(bool on)
        {
            barVisible = on;
            if (barRoot != null) barRoot.gameObject.SetActive(on);
        }
    }
}
