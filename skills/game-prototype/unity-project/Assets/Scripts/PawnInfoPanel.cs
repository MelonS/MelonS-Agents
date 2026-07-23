using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Bottom-left SINGLE selection-info panel (the reference sim convention).
    ///
    /// ── 2026-05-31 single-inspector 통합 (운영자 fb "DUAL INSPECTOR") ──
    /// 이전: pawn 전용.  비-pawn entity 는 우측 EntityInspectorPanel 이 따로 표시
    ///   → pawn 선택 시 좌/우 패널이 동시에 떠서 혼란.
    /// 지금: 이 패널 하나가 pawn AND entity 둘 다 표시하는 유일한 inspector.
    ///   - PAWN 선택 (selector.CurrentSelection != null) → 기존 탭 UI (상태/건강/기분/장비).
    ///   - 비-pawn ENTITY 선택 (selector.CurrentInspect 가 pawn 아님) →
    ///       EntityInspectorPanel.DescribeEntity() 로 받은 (제목, 본문) 을 표시.
    ///   - 둘 다 없으면 empty-state.
    ///   우측 EntityInspectorPanel 은 더 이상 패널을 그리지 않음 (logic-only).
    /// Subscribes to ClickSelector via per-frame poll (no event-subscription race).
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
        // Day 55: 부위별 health 표시 — 클릭 시 vanilla colony-sim 처럼.
        [SerializeField] private Text healthText;

        // #obj-audit: 탭 strip 높이 + body 와의 간격을 단일 상수로.  이전엔 EnsureTabs 와
        //   MakeBodyText 가 각자 `float stripH = 22f` + body 의 `stripH + 6f` 를 따로
        //   하드코딩해, 한쪽만 바꾸면 본문이 탭과 겹칠 brittleness 가 있었다(감사 #2).
        private const float TabStripH = 22f;
        private const float TabStripGap = 6f;
        // #ui백로그 3.0 — 타이틀(폰 이름) 전용 밴드 높이.  탭 스트립 바로 아래 22px.
        private const float TitleBandH = 22f;

        // single-inspector 통합 — 비-pawn entity 선택 시 설명 본문을 보여주는
        //   lazily-built Text (탭 위에 겹치지 않게 panel 본문 영역을 채움).  pawn
        //   탭 UI 와 상호 배타적으로 토글된다.
        private Text entityBodyText;
        private EntityInspectorPanel cachedEntityDesc;

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
        private bool healthRectNormalized;   // #obj-audit 탭 본문 정렬 1회 보정 가드
        private bool titleRectNormalized;    // #ui백로그 3.0 타이틀 밴드 1회 보정 가드

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
            float stripH = TabStripH;
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
                lbl.fontSize = 15;   // #UI-B 13→15 탭 라벨 가독성(짧은 라벨이라 overflow 위험 없음)
                lbl.fontStyle = FontStyle.Bold;
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
            float stripH = TabStripH;
            var go = new GameObject(name);
            go.transform.SetParent(prt, false);
            var rt = go.AddComponent<RectTransform>();
            // Fill the panel below the tab strip, matching the PadOuter rhythm.
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -(pad + stripH + TabStripGap));
            var txt = go.AddComponent<Text>();
            txt.font = ResolveFont();
            txt.fontSize = 13;
            txt.alignment = TextAnchor.UpperLeft;
            txt.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            txt.supportRichText = true;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            // 운영자 피드백 #5 (2026-06-12 AM): Overflow 라 장비 탭 긴 내용이 패널 밖으로
            //  흘러나갔다 — 영역 밖 렌더 금지(Truncate) + 장비 탭 내용은 2열 압축.
            txt.verticalOverflow = VerticalWrapMode.Truncate;
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

        // ui-audit P6/§3.7 (운영자 fb "bottom-left overlap") — enforce a SAFE
        //   bottom-left position at runtime so the single inspector never sits on
        //   top of the S/L save/load buttons or the architect menu, regardless of
        //   the Editor builder's offset.  Layout (anchor (0,0), 1920x1080 ref):
        //     S/L buttons:  x=12, y=12, 92x40   → top edge y=52.
        //     ArchitectMenu: anchor (0,0.5), x=12, 280x440 → on a 1080 screen its
        //                    bottom edge ≈ screenCenter(540) - 220 = y≈320.
        //   So a panel at x=12, y=58, height ≤ 256 lives in the clear band
        //   y=[58, 314]: above the S/L row (52) with a 6px gap, below the architect
        //   menu bottom (~320) with a small gap.  Width 380 (< architect's left x).
        private void EnsurePlacement()
        {
            var rt = (panelBg != null) ? panelBg.GetComponent<RectTransform>() : GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(380f, 248f);          // ≤256 → top edge ≤ 314
            rt.anchoredPosition = new Vector2(12f, 58f);     // above S/L (y=52) + gap
        }

        // single-inspector — body text for a non-pawn ENTITY selection.  Built
        //   lazily (same idiom as MakeBodyText) filling the panel below the title
        //   band; toggled mutually-exclusive with the pawn tab content.
        private void EnsureEntityBody()
        {
            if (entityBodyText != null || panelBg == null) return;
            var prt = panelBg.GetComponent<RectTransform>();
            if (prt == null) return;
            float pad = MelonS.GameProto.Core.UITheme.PadOuter;
            var go = new GameObject("EntityBody");
            go.transform.SetParent(prt, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(pad, pad);
            // QA F1(2026-06-14): 엔티티 선택 시 탭 스트립은 숨지만 titleText 는 폰
            //   레이아웃 기준 '스트립 아래'(y -(pad+TabStripH+gap))에 1회 정규화돼 있다.
            //   본문이 상단 34px 만 비우면(-46) 제목(-40~-62)과 겹쳐 첫 줄이 깨진다
            //   ("위치:" + "나무(참나무)" 중첩).  제목의 실제 하단(-62) 아래부터 본문 시작.
            rt.offsetMax = new Vector2(-pad, -(pad + TabStripH + TabStripGap + TitleBandH));
            entityBodyText = go.AddComponent<Text>();
            entityBodyText.font = ResolveFont();
            entityBodyText.fontSize = 14;
            entityBodyText.alignment = TextAnchor.UpperLeft;
            entityBodyText.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            entityBodyText.supportRichText = true;
            entityBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            entityBodyText.verticalOverflow = VerticalWrapMode.Overflow;
            entityBodyText.raycastTarget = false;
            entityBodyText.gameObject.SetActive(false);
        }

        // UI-B — 게이지 % 라벨 (bar fill 의 row 에 lazy 부착)
        private Text foodPct, sleepPct, moodPct;
        private static void UpdatePct(Image bar, ref Text cache, float value)
        {
            if (bar == null) return;
            if (cache == null)
            {
                // fill → Bg → Row 구조 (SceneSetup.UI CreateNeedBar)
                var row = bar.transform.parent != null ? bar.transform.parent.parent : null;
                if (row == null) return;
                var go = new GameObject("Pct");
                go.transform.SetParent(row, false);
                cache = go.AddComponent<Text>();
                cache.font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(12);
                cache.fontSize = 12;
                cache.alignment = TextAnchor.MiddleRight;
                cache.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(38, 0);
                rt.anchoredPosition = new Vector2(-4, 0);
            }
            int v = Mathf.RoundToInt(value);
            cache.text = $"{v}%";
            cache.color = v < 25
                ? new Color(0.92f, 0.40f, 0.30f, 1f)
                : MelonS.GameProto.Core.UITheme.TextSecondary;
        }

        private void Update()
        {
            EnsurePlacement();
            EnsureBorder();
            EnsureEmptyState();
            EnsureTabs();
            EnsureEntityBody();
            // #QA플레이 F2 (2026-06-12) — 건축 패널(좌하단 252px)과 같은 코너 겹침:
            //  건축 메뉴가 열려 있으면 인포패널을 그 오른쪽으로 시프트.
            if (panelBg != null)
            {
                var prt0 = panelBg.GetComponent<RectTransform>();
                var am = ArchitectMenu.Instance;
                if (prt0 != null)
                {
                    bool amOpen = am != null && am.IsOpen;
                    float wantX = amOpen ? 272f : 12f;
                    // UI겹침 P1-3/커플링 B — 셸프(상단 y58~)와의 하단 40px 띠 겹침: 건축
                    //  열림 시 y 도 셸프 실상단+10 으로 (1행=108, 2행=203).
                    float wantY = amOpen ? 108f + 95f * (am.ShelfRows - 1) : 58f;
                    if (!Mathf.Approximately(prt0.anchoredPosition.x, wantX)
                        || !Mathf.Approximately(prt0.anchoredPosition.y, wantY))
                        prt0.anchoredPosition = new Vector2(wantX, wantY);
                }
            }
            if (selector == null)
            {
                Debug.LogWarning("[PawnInfoPanel] no selector");
                return;
            }

            PawnEntity pawn = selector.CurrentSelection;

            // single-inspector — entity branch.  When NO pawn is selected but a
            //   non-pawn entity is inspected, this ONE panel shows its description
            //   (text from EntityInspectorPanel.DescribeEntity, kept as the single
            //   source of entity copy + the I25 test's Describe target).
            GameObject inspect = selector.CurrentInspect;
            bool entitySelected = false;
            string entTitle = null, entBody = null;
            if (pawn == null && inspect != null && inspect.GetComponent<PawnEntity>() == null)
            {
                if (cachedEntityDesc == null)
                    cachedEntityDesc = EntityInspectorPanel.Instance != null
                        ? EntityInspectorPanel.Instance
                        : Object.FindFirstObjectByType<EntityInspectorPanel>();
                if (cachedEntityDesc != null)
                {
                    (entTitle, entBody) = cachedEntityDesc.DescribeEntity(inspect);
                    entitySelected = entTitle != null;
                }
            }

            if (entitySelected)
            {
                // Hide pawn tab UI + empty state; show the entity description.
                SetTabStripVisible(false);
                if (titleText != null)
                {
                    titleText.gameObject.SetActive(true);
                    titleText.supportRichText = true;
                    titleText.text = entTitle;
                }
                if (foodBar  != null) foodBar.transform.parent.parent.gameObject.SetActive(false);
                if (sleepBar != null) sleepBar.transform.parent.parent.gameObject.SetActive(false);
                if (moodBar  != null) moodBar.transform.parent.parent.gameObject.SetActive(false);
                if (healthText != null) healthText.gameObject.SetActive(false);
                if (moodDetailText != null) moodDetailText.gameObject.SetActive(false);
                if (equipText != null) equipText.gameObject.SetActive(false);
                if (emptyText != null) emptyText.gameObject.SetActive(false);
                SetEmptyStateVisible(false);
                if (entityBodyText != null)
                {
                    entityBodyText.gameObject.SetActive(true);
                    entityBodyText.text = entBody;
                }
                // QA F3(2026-06-14): 인스펙터 전용 '벌목 지정' 버튼은 제거됨 — ClickSelector
                //  좌클릭 플로트 메뉴(🪓 벌목 지정, fb#1-3)가 나무/광맥/베리/철거를 포괄
                //  제공하므로 인스펙터 중복 버튼은 만들지 않는다(관련 dead code 정리됨).
                if (panelBg != null) panelBg.enabled = true;
                SetBorderVisible(true);
                return;
            }
            // not an entity selection → hide the entity body for the pawn/empty paths.
            if (entityBodyText != null) entityBodyText.gameObject.SetActive(false);

            // ── operator fb #3 (2026-05-31): "선택되면 나왔다 선택 안 되면 없어져야" ──
            //   When NOTHING is selected (no pawn AND no inspected entity), HIDE the
            //   whole panel — frame fill, border, tab strip, bars, empty hint — so no
            //   inspector chrome lingers on an empty selection.  Previously the panel
            //   kept its bordered/titled frame + "선택된 오브젝트 없음" hint always
            //   drawn (V7 styled-empty-state), which the operator flagged as wrong:
            //   the reference sim shows the inspector ONLY while something is selected.
            bool nothingSelected = (pawn == null) && (inspect == null);
            if (nothingSelected)
            {
                if (panelBg != null) panelBg.enabled = false;
                SetBorderVisible(false);
                SetTabStripVisible(false);
                if (titleText != null) titleText.gameObject.SetActive(false);
                if (foodBar  != null) foodBar.transform.parent.parent.gameObject.SetActive(false);
                if (sleepBar != null) sleepBar.transform.parent.parent.gameObject.SetActive(false);
                if (moodBar  != null) moodBar.transform.parent.parent.gameObject.SetActive(false);
                if (healthText != null) healthText.gameObject.SetActive(false);
                if (moodDetailText != null) moodDetailText.gameObject.SetActive(false);
                if (equipText != null) equipText.gameObject.SetActive(false);
                if (emptyText != null) emptyText.gameObject.SetActive(false);
                SetEmptyStateVisible(false);
                return;
            }

            bool any = (pawn != null);

            // #23 — title is part of the 상태 tab now; tab strip only when a
            //   pawn is selected (empty-state keeps its own centered hint).
            bool tabStatus = activeTab == InfoTab.Status;
            bool tabHealth = activeTab == InfoTab.Health;
            bool tabMood   = activeTab == InfoTab.Mood;
            bool tabEquip  = activeTab == InfoTab.Equip;
            SetTabStripVisible(any);

            // #ui백로그 3.6 — 타이틀(이름·활동·특성)을 전 탭 상시 표시: 건강/기분/장비 탭
            //  전환 시 '누구를 보고 있는지' 컨텍스트가 사라지던 것 해소.
            if (titleText != null) titleText.gameObject.SetActive(any);
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
                // #버그헌트4(2026-06-05): 죽은 폰은 상태탭 타이틀에 '사망' 명시.  이전엔 IsDead 가드가
                //  없어, 죽어도 Destroy 안 되는 시체(PawnEntity disable만)가 선택 유지되면 사망 직전
                //  '현재 행동'(예: 벌목)이 frozen 표시돼 시체가 일하는 것처럼 보였다(PawnNameLabel 도
                //  disable 돼 statusTm 갱신 정지 → stale).  사망 시 활동 라벨 대신 사망 배지.
                var phDeadTitle = pawn.GetComponent<PawnHealth>();
                if (phDeadTitle != null && phDeadTitle.IsDead)
                {
                    title += "  <size=13><color=#c0392b>· 사망</color></size>";
                }
                else
                {
                    // #UI-B 현재 행동을 제목에 표시(머리위 라벨과 동일 정보) — 운영자 "뭐하는지 항상".
                    var nameLabel = pawn.GetComponent<PawnNameLabel>();
                    string act = nameLabel != null ? nameLabel.CurrentActivity : "";
                    if (!string.IsNullOrEmpty(act))
                        title += $"  <size=13><color=#e8b560>· {act}</color></size>";
                }
                // G2 배경 한 줄 (2026-07-24 게임성 감사): 클릭한 림에게 최소한의 서사.
                title += $"\n<size=13><color=#c4a878>{PawnBackstory.GetLine(pawn)}</color></size>";
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
            // UI-B (2026-06-12) — 게이지 우측 % 라벨: 바만으론 '얼마나 위험한가' 판독이
            //  안 됐다 (TOP-8.3 잔여).  바 row 에 lazy 생성, 25 미만은 경고색.
            UpdatePct(foodBar,  ref foodPct,  needs.food);
            UpdatePct(sleepBar, ref sleepPct, needs.sleep);
            UpdatePct(moodBar,  ref moodPct,  needs.mood);

            // #23 — populate per-tab bodies.  All three are lazily created the
            //   first time a pawn is shown so we never depend on SceneSetup
            //   wiring them (same lazy-create contract as the tab strip itself).
            //   We still build every body each frame (cheap StringBuilder) so a
            //   tab is correct the instant it is clicked.
            if (moodDetailText == null) moodDetailText = MakeBodyText("MoodDetailBody");
            if (equipText == null)      equipText      = MakeBodyText("EquipBody");

            // #obj-audit(운영자 '탭 누르면 UI 위치 정렬 안 됨') — healthText 는 SerializeField
            //  (에디터 배치)라 MakeBodyText 로 만든 moodDetail/equip 본문과 rect 가 달라 건강
            //  탭만 위치가 어긋났다.  런타임에 동일한 본문 영역(탭 strip 아래 채움 + PadOuter)
            //  으로 1회 정규화 → 건강/기분/장비 탭 본문이 모두 같은 위치에 정렬된다.
            // #ui백로그 3.0 — titleText(에디터 배치 top -8..-34)가 런타임 탭 스트립(-12..-34)과
            //  같은 밴드를 공유, 늦은 sibling 인 불투명 탭 버튼이 폰 이름을 완전히 가렸다.
            //  본문 정규화(healthRectNormalized)와 같은 패턴으로 1회, 스트립 '아래' 밴드로 이동.
            //  타이틀은 상태 탭에서만 표시되고 상태 탭 본문(need 바)은 bottom-anchor 라 무충돌.
            if (!titleRectNormalized && titleText != null)
            {
                var trt = titleText.GetComponent<RectTransform>();
                if (trt != null)
                {
                    float tpad = MelonS.GameProto.Core.UITheme.PadOuter;
                    trt.anchorMin = new Vector2(0f, 1f);
                    trt.anchorMax = new Vector2(1f, 1f);
                    trt.pivot = new Vector2(0f, 1f);
                    trt.anchoredPosition = new Vector2(tpad, -(tpad + TabStripH + TabStripGap));
                    trt.sizeDelta = new Vector2(-2f * tpad, TitleBandH);
                    titleText.fontSize = 16;
                    titleText.verticalOverflow = VerticalWrapMode.Truncate;
                }
                titleRectNormalized = true;
            }

            if (!healthRectNormalized && healthText != null)
            {
                var hrt = healthText.GetComponent<RectTransform>();
                if (hrt != null)
                {
                    float pad = MelonS.GameProto.Core.UITheme.PadOuter;
                    hrt.anchorMin = new Vector2(0f, 0f);
                    hrt.anchorMax = new Vector2(1f, 1f);
                    hrt.offsetMin = new Vector2(pad, pad);
                    hrt.offsetMax = new Vector2(-pad, -(pad + TabStripH + TabStripGap));
                    healthText.alignment = TextAnchor.UpperLeft;
                    healthText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    healthText.verticalOverflow = VerticalWrapMode.Overflow;
                }
                healthRectNormalized = true;
            }

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
                    // 운영자 2026-06-02: 건강 탭엔 건강(부위 HP/출혈/붕대)만.  능력치는 장비 탭으로 이전.
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

            // ---- 장비 tab: 장비 목록 + 능력치(건강탭에서 이전, 운영자 fb) ----
            if (equipText != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<color=#ddc28a>장비:</color>");
                var eq = pawn.GetComponent<PawnEquipment>();
                if (eq != null && eq.equipped.Count > 0)
                {
                    string[] slotLabels = { "셔츠", "바지", "모자", "무기" };
                    var slots = (PawnEquipment.Slot[])System.Enum.GetValues(typeof(PawnEquipment.Slot));
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var it = eq.GetEquipped(slots[i]);
                        string val = it != null ? it.nameKr : "(없음)";
                        string col = it != null ? "#dddddd" : "#888888";
                        sb.AppendLine($"  <color={col}>{slotLabels[i]}: {val}</color>");
                    }
                }
                else sb.AppendLine("  <color=#888888>(장비 없음)</color>");
                // #120 능력치 — 건강탭에서 이전(건강엔 건강만, 능력치는 여기).
                var abil = pawn.GetComponent<PawnAbilities>();
                if (abil != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("<color=#ddc28a>능력치:</color>");
                    // #5 — 한 줄 2항목 압축 (세로 공간 절반).
                    int colCount = 0;
                    foreach (var (key, label) in PawnAbilities.DisplayMap)
                    {
                        float v = abil.GetByKey(key);
                        string col = v >= 1.10f ? "#9adb86" : (v >= 0.95f ? "#dddddd" : "#e88c54");
                        sb.Append($"  <color={col}>{label} {v:F2}</color>");
                        if (++colCount % 2 == 0) sb.AppendLine();
                    }
                    if (colCount % 2 != 0) sb.AppendLine();
                }
                equipText.text = sb.ToString();
                equipText.color = MelonS.GameProto.Core.UITheme.TextPrimary;
                equipText.alignment = TextAnchor.UpperLeft;
            }
        }
    }
}
