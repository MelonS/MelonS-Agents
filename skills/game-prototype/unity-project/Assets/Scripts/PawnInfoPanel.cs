using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Bottom-left UI panel showing selected pawn's needs as bars.
    /// Subscribes to ClickSelector's selection events.  Day 2 = static
    /// panel, always rendered, refreshes per-frame.
    /// </summary>
    public class PawnInfoPanel : MonoBehaviour
    {
        [SerializeField] private ClickSelector selector;
        [SerializeField] private Text titleText;
        [SerializeField] private Image foodBar;
        [SerializeField] private Image sleepBar;
        [SerializeField] private Image moodBar;
        [SerializeField] private Text emptyText;
        [SerializeField] private Image panelBg;
        // Day 55: 부위별 health 표시 — 클릭 시 RimWorld vanilla 처럼.
        [SerializeField] private Text healthText;

        // #UI-restyle U5 — runtime border frame so this panel matches the global
        //   bordered-panel system (Editor builder only made the flat fill).  Added
        //   lazily as 4 Divider-colored edge Images; toggled with the panel.
        private Image[] borderEdges;

        private void EnsureBorder()
        {
            if (borderEdges != null || panelBg == null) return;
            var prt = panelBg.GetComponent<RectTransform>();
            if (prt == null) { borderEdges = new Image[0]; return; }
            float t = MelonS.GameProto.Core.UITheme.BorderPx;
            borderEdges = new Image[4];
            // top, bottom, left, right
            borderEdges[0] = MakeEdge("BorderTop",    prt, new Vector2(0,1), new Vector2(1,1), new Vector2(0,t),  new Vector2(0, 0));
            borderEdges[1] = MakeEdge("BorderBottom", prt, new Vector2(0,0), new Vector2(1,0), new Vector2(0,t),  new Vector2(0, 0));
            borderEdges[2] = MakeEdge("BorderLeft",   prt, new Vector2(0,0), new Vector2(0,1), new Vector2(t,0),  new Vector2(0, 0));
            borderEdges[3] = MakeEdge("BorderRight",  prt, new Vector2(1,0), new Vector2(1,1), new Vector2(t,0),  new Vector2(0, 0));
        }

        private Image MakeEdge(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;   // 0 on the stretched axis, thickness on the other
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = MelonS.GameProto.Core.UITheme.Divider;
            img.raycastTarget = false;
            return img;
        }

        private void SetBorderVisible(bool v)
        {
            if (borderEdges == null) return;
            foreach (var e in borderEdges) if (e != null) e.enabled = v;
        }

        // #UI-restyle V7 — styled empty-state.  Instead of one bare cramped
        //   line on a collapsed rectangle, the empty inspector keeps its
        //   bordered/titled frame (panelBg + borderEdges) and shows a centered
        //   MUTED hint plus a small dimmed select/cursor glyph above it.
        //   Built lazily as children of emptyText so it inherits the same
        //   Korean-font path SceneSetup already wired onto emptyText.
        private Text emptyGlyph;

        private void EnsureEmptyState()
        {
            if (emptyText == null) return;
            // Center the hint inside the panel rather than the cramped corner
            //   line the Editor builder produced.
            var ert = emptyText.GetComponent<RectTransform>();
            if (ert != null)
            {
                ert.anchorMin = new Vector2(0f, 0f);
                ert.anchorMax = new Vector2(1f, 1f);
                ert.offsetMin = new Vector2(MelonS.GameProto.Core.UITheme.PadOuter, MelonS.GameProto.Core.UITheme.PadOuter);
                ert.offsetMax = new Vector2(-MelonS.GameProto.Core.UITheme.PadOuter, -MelonS.GameProto.Core.UITheme.PadOuter);
            }
            emptyText.alignment = TextAnchor.MiddleCenter;
            emptyText.color = MelonS.GameProto.Core.UITheme.TextSecondary;  // muted cream
            // ui-audit §3.4 (P5) — unified empty-state copy across both
            //   inspectors (was "오브젝트를 선택하세요"; matches EntityInspectorPanel).
            emptyText.text = "선택된 오브젝트 없음";

            if (emptyGlyph != null) return;
            // Small dimmed select/cursor glyph centered just above the hint.
            var go = new GameObject("EmptyGlyph");
            go.transform.SetParent(emptyText.transform.parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(48f, 48f);
            rt.anchoredPosition = new Vector2(0f, 22f);  // above the centered hint
            emptyGlyph = go.AddComponent<Text>();
            // Route through the same Korean-font path used for all text here;
            //   borrow emptyText's already-resolved font if present.
            emptyGlyph.font = emptyText.font != null
                ? emptyText.font
                : MelonS.GameProto.Core.UITheme.LoadKoreanFont(32);
            emptyGlyph.fontSize = 30;
            emptyGlyph.alignment = TextAnchor.MiddleCenter;
            emptyGlyph.text = "▸";  // ▸ small select/cursor caret glyph
            var dim = MelonS.GameProto.Core.UITheme.TextSecondary;
            dim.a = 0.5f;  // dimmed (alpha only — no new inline panel color)
            emptyGlyph.color = dim;
            emptyGlyph.raycastTarget = false;
        }

        private void SetEmptyStateVisible(bool v)
        {
            if (emptyGlyph != null) emptyGlyph.enabled = v;
        }

        // #23 (wiki Dim6, backlog #23) — runtime-lazy TAB STRIP.
        //   Built in code on first show with the SAME lazy-create idiom as
        //   EnsureBorder()/EnsureEmptyState() above: NO SceneSetup builder, NO
        //   new prefab, NO new SerializeField.  Splits the previously-crammed
        //   single info dump into 4 tabs (상태/건강/기분/장비).  Clicking a tab
        //   changes which content rows are visible:
        //     상태  = title + needs bars (food/sleep/mood)
        //     건강  = health parts + abilities (from existing PawnHealth/PawnAbilities)
        //     기분  = mood bar + thought breakdown (existing PawnThoughts)
        //     장비  = equipment list, or muted '장비 없음' empty-state (U5 styling)
        //   All data comes from components the panel ALREADY reads — no new
        //   runtime API is touched.  Where a tab has no data, it shows a styled
        //   muted placeholder consistent with the V7 empty-state above.
        private enum InfoTab { Status = 0, Health = 1, Mood = 2, Equip = 3 }
        private static readonly string[] TabLabels = { "상태", "건강", "기분", "장비" };
        private InfoTab activeTab = InfoTab.Status;
        private Button[] tabButtons;
        private RectTransform tabStrip;
        private Font cachedFont;

        // Content holders for the non-needs tabs (건강/기분/장비).  健康 reuses
        //   the existing healthText; the others get their own lazily-built Text
        //   so we never reparent the SceneSetup-wired serialized refs.
        private Text moodDetailText;   // 기분 tab body
        private Text equipText;        // 장비 tab body

        private Font ResolveFont()
        {
            if (cachedFont != null) return cachedFont;
            // Borrow whatever Korean font SceneSetup already resolved on an
            //   existing Text so the tabs match the rest of the panel exactly.
            if (titleText != null && titleText.font != null) cachedFont = titleText.font;
            else if (healthText != null && healthText.font != null) cachedFont = healthText.font;
            else if (emptyText != null && emptyText.font != null) cachedFont = emptyText.font;
            else cachedFont = MelonS.GameProto.Core.UITheme.LoadKoreanFont(16);
            return cachedFont;
        }

        private void EnsureTabs()
        {
            if (tabButtons != null || panelBg == null) return;
            var prt = panelBg.GetComponent<RectTransform>();
            if (prt == null) { tabButtons = new Button[0]; return; }

            float pad = MelonS.GameProto.Core.UITheme.PadOuter;
            float stripH = 22f;
            // Tab strip pinned to the TOP edge of the panel, inset by padding so
            //   it sits inside the bordered frame rhythm (same PadOuter the
            //   empty-state inset uses).
            var stripGo = new GameObject("TabStrip");
            stripGo.transform.SetParent(prt, false);
            tabStrip = stripGo.AddComponent<RectTransform>();
            tabStrip.anchorMin = new Vector2(0f, 1f);
            tabStrip.anchorMax = new Vector2(1f, 1f);
            tabStrip.pivot = new Vector2(0.5f, 1f);
            tabStrip.offsetMin = new Vector2(pad, -(pad + stripH));
            tabStrip.offsetMax = new Vector2(-pad, -pad);

            int n = TabLabels.Length;
            float gap = 4f;
            tabButtons = new Button[n];
            for (int i = 0; i < n; i++)
            {
                int idx = i;  // capture for closure
                var bgo = new GameObject("Tab_" + TabLabels[i]);
                bgo.transform.SetParent(tabStrip, false);
                var brt = bgo.AddComponent<RectTransform>();
                // Even horizontal split across the strip width.
                brt.anchorMin = new Vector2((float)i / n, 0f);
                brt.anchorMax = new Vector2((float)(i + 1) / n, 1f);
                brt.offsetMin = new Vector2(i == 0 ? 0f : gap * 0.5f, 0f);
                brt.offsetMax = new Vector2(i == n - 1 ? 0f : -gap * 0.5f, 0f);

                var img = bgo.AddComponent<Image>();
                img.color = MelonS.GameProto.Core.UITheme.BtnInactiveBg;

                var btn = bgo.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SetTab((InfoTab)idx));
                tabButtons[i] = btn;

                var lgo = new GameObject("Label");
                lgo.transform.SetParent(brt, false);
                var lrt = lgo.AddComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                var lbl = lgo.AddComponent<Text>();
                lbl.font = ResolveFont();
                lbl.fontSize = 13;
                lbl.alignment = TextAnchor.MiddleCenter;
                lbl.text = TabLabels[i];
                lbl.color = MelonS.GameProto.Core.UITheme.TextPrimary;
                lbl.raycastTarget = false;
            }

            RefreshTabButtonStyles();
        }

        private Text MakeBodyText(string name)
        {
            if (panelBg == null) return null;
            var prt = panelBg.GetComponent<RectTransform>();
            if (prt == null) return null;
            float pad = MelonS.GameProto.Core.UITheme.PadOuter;
            float stripH = 22f;
            var go = new GameObject(name);
            go.transform.SetParent(prt, false);
            var rt = go.AddComponent<RectTransform>();
            // Fill the panel below the tab strip, matching the PadOuter rhythm.
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -(pad + stripH + 6f));
            var txt = go.AddComponent<Text>();
            txt.font = ResolveFont();
            txt.fontSize = 13;
            txt.alignment = TextAnchor.UpperLeft;
            txt.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            txt.supportRichText = true;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        private void SetTab(InfoTab tab)
        {
            activeTab = tab;
            RefreshTabButtonStyles();
        }

        private void RefreshTabButtonStyles()
        {
            if (tabButtons == null) return;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                var img = tabButtons[i].targetGraphic as Image;
                if (img == null) continue;
                bool on = (int)activeTab == i;
                img.color = on
                    ? MelonS.GameProto.Core.UITheme.BtnActiveBg
                    : MelonS.GameProto.Core.UITheme.BtnInactiveBg;
                // Active tab text darkens (reads on the orange fill), inactive
                //   stays cream — same active/inactive contrast as the rest of
                //   the UI's button system.
                var lbl = tabButtons[i].GetComponentInChildren<Text>();
                if (lbl != null)
                    lbl.color = on
                        ? MelonS.GameProto.Core.UITheme.TextDark
                        : MelonS.GameProto.Core.UITheme.TextPrimary;
            }
        }

        private void SetTabStripVisible(bool v)
        {
            if (tabStrip != null) tabStrip.gameObject.SetActive(v);
        }

        private void Update()
        {
            EnsureBorder();
            EnsureEmptyState();
            EnsureTabs();
            if (selector == null)
            {
                Debug.LogWarning("[PawnInfoPanel] no selector");
                return;
            }

            PawnEntity pawn = selector.CurrentSelection;
            bool any = (pawn != null);

            // #23 — title is part of the 상태 tab now; tab strip only when a
            //   pawn is selected (empty-state keeps its own centered hint).
            bool tabStatus = activeTab == InfoTab.Status;
            bool tabHealth = activeTab == InfoTab.Health;
            bool tabMood   = activeTab == InfoTab.Mood;
            bool tabEquip  = activeTab == InfoTab.Equip;
            SetTabStripVisible(any);

            if (titleText != null) titleText.gameObject.SetActive(any && tabStatus);
            // foodBar.transform.parent = BarBg image GameObject.
            // BarBg.parent = the Row container that ALSO holds the label.
            // We toggle the row (grandparent) so the label vanishes too —
            // otherwise "Food/Sleep/Mood" labels remain visible when no
            // pawn is selected (Day 15 leftover).
            // #23 — needs bars belong to 상태 (food/sleep) ; moodBar is shown on
            //   BOTH 상태 (quick glance) and 기분 (with thought detail below).
            if (foodBar  != null) foodBar.transform.parent.parent.gameObject.SetActive(any && tabStatus);
            if (sleepBar != null) sleepBar.transform.parent.parent.gameObject.SetActive(any && tabStatus);
            if (moodBar  != null) moodBar.transform.parent.parent.gameObject.SetActive(any && (tabStatus || tabMood));
            // 건강 tab body = existing healthText (now health-parts only).
            if (healthText != null) healthText.gameObject.SetActive(any && tabHealth);
            if (moodDetailText != null) moodDetailText.gameObject.SetActive(any && tabMood);
            if (equipText != null) equipText.gameObject.SetActive(any && tabEquip);
            if (emptyText != null) emptyText.gameObject.SetActive(!any);
            SetEmptyStateVisible(!any);  // #UI-restyle V7 — dimmed glyph only when empty
            // #UI-restyle V7 — keep the bordered/titled frame drawn in the
            //   empty state too (was Day-15 collapse-to-bare-line).  The empty
            //   inspector now reads as a finished styled panel, not an
            //   unfinished cramped rectangle.  Frame is always on.
            if (panelBg != null) panelBg.enabled = true;
            SetBorderVisible(true);

            if (!any) return;

            PawnNeeds needs = pawn.GetComponent<PawnNeeds>();
            // Day 56: name + traits in title
            PawnTraits traits = pawn.GetComponent<PawnTraits>();
            if (titleText != null)
            {
                string title = pawn.PawnName;
                if (traits != null)
                {
                    string ts = traits.SummaryKr();
                    if (!string.IsNullOrEmpty(ts))
                        title += $"  <size=12><color=#c0b090>({ts})</color></size>";
                }
                titleText.text = title;
                titleText.supportRichText = true;
            }
            if (needs == null) return;

            if (foodBar  != null) foodBar.fillAmount  = needs.GetNormalized(NeedType.Food);
            if (sleepBar != null) sleepBar.fillAmount = needs.GetNormalized(NeedType.Sleep);
            if (moodBar  != null) moodBar.fillAmount  = needs.GetNormalized(NeedType.Mood);

            // #23 — populate per-tab bodies.  All three are lazily created the
            //   first time a pawn is shown so we never depend on SceneSetup
            //   wiring them (same lazy-create contract as the tab strip itself).
            //   We still build every body each frame (cheap StringBuilder) so a
            //   tab is correct the instant it is clicked.
            if (moodDetailText == null) moodDetailText = MakeBodyText("MoodDetailBody");
            if (equipText == null)      equipText      = MakeBodyText("EquipBody");

            // ---- 건강 tab: 부위별 health (Day 55) + 능력치 (#120) ----
            if (healthText != null)
            {
                PawnHealth health = pawn.GetComponent<PawnHealth>();
                if (health != null && health.parts != null)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("<color=#ddc28a>상태:</color>");
                    foreach (var part in health.parts)
                    {
                        float r = (float)part.hp / part.maxHp;
                        string color = r > 0.7f ? "#9adb86" : (r > 0.3f ? "#e8b454" : "#e85454");
                        string bleed = part.bleedRate > 0.1f ? " <color=#ff6464>출혈</color>" : "";
                        string bandage = part.bandaged ? " <color=#a0c8ff>붕대</color>" : "";
                        sb.AppendLine($"<color={color}>{part.nameKr}: {part.hp}/{part.maxHp}</color>{bleed}{bandage}");
                    }
                    if (health.IsDowned) sb.AppendLine("<color=#ff6464>의식불명</color>");
                    if (health.IsDead)   sb.AppendLine("<color=#ff0000>사망</color>");

                    // #120 - 능력치 (PawnAbilities)
                    var abil = pawn.GetComponent<PawnAbilities>();
                    if (abil != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine("<color=#ddc28a>능력치:</color>");
                        foreach (var (key, label) in PawnAbilities.DisplayMap)
                        {
                            float v = abil.GetByKey(key);
                            // 1.0 기준 색상 - 좋음 녹색, 평균 옅음, 나쁨 빨강
                            string col = v >= 1.10f ? "#9adb86" : (v >= 0.95f ? "#dddddd" : "#e88c54");
                            sb.AppendLine($"  <color={col}>{label}: {v:F2}</color>");
                        }
                    }

                    healthText.text = sb.ToString();
                }
                else
                {
                    healthText.text = "";
                }
            }

            // ---- 기분 tab: thought breakdown (#122), below the moodBar ----
            if (moodDetailText != null)
            {
                var thoughts = pawn.GetComponent<PawnThoughts>();
                if (thoughts != null && thoughts.active.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("<color=#ddc28a>기분:</color>");
                    foreach (var t in thoughts.active)
                    {
                        string col = t.offset >= 0f ? "#9adb86" : "#e88c54";
                        string sign = t.offset >= 0f ? "+" : "";
                        sb.AppendLine($"  <color={col}>{t.label} {sign}{t.offset:F0}</color>");
                    }
                    moodDetailText.text = sb.ToString();
                    moodDetailText.color = MelonS.GameProto.Core.UITheme.TextPrimary;
                    moodDetailText.alignment = TextAnchor.UpperLeft;
                }
                else
                {
                    // Styled muted placeholder (consistent with the U5 / V7
                    //   empty-state styling already in this file).
                    moodDetailText.text = "특이사항 없음";
                    moodDetailText.color = MelonS.GameProto.Core.UITheme.TextSecondary;
                    moodDetailText.alignment = TextAnchor.MiddleCenter;
                }
            }

            // ---- 장비 tab: equipment list (#123), or muted '장비 없음' ----
            if (equipText != null)
            {
                var eq = pawn.GetComponent<PawnEquipment>();
                if (eq != null && eq.equipped.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("<color=#ddc28a>장비:</color>");
                    string[] slotLabels = { "셔츠", "바지", "모자", "무기" };
                    var slots = (PawnEquipment.Slot[])System.Enum.GetValues(typeof(PawnEquipment.Slot));
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var it = eq.GetEquipped(slots[i]);
                        string val = it != null ? it.nameKr : "(없음)";
                        string col = it != null ? "#dddddd" : "#888888";
                        sb.AppendLine($"  <color={col}>{slotLabels[i]}: {val}</color>");
                    }
                    equipText.text = sb.ToString();
                    equipText.color = MelonS.GameProto.Core.UITheme.TextPrimary;
                    equipText.alignment = TextAnchor.UpperLeft;
                }
                else
                {
                    // U5/V7-consistent muted empty-state.
                    equipText.text = "장비 없음";
                    equipText.color = MelonS.GameProto.Core.UITheme.TextSecondary;
                    equipText.alignment = TextAnchor.MiddleCenter;
                }
            }
        }
    }
}
