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

        // ── operator fb #1+A: RimWorld "Orders (지시)" + "Zone (구역)" categories ──
        //   In vanilla RimWorld, Mine + Deconstruct live under an ORDERS tab and
        //   Grow-zone lives under a ZONE tab — NOT as standalone screen buttons.
        //   We removed the 3 standalone toggles (Btn_채광/Btn_경작/해체) and fold
        //   them here.  These items are NOT BuildManager build-modes; each invokes
        //   the existing designation manager's public SetMode(...)/Toggle() entry
        //   point.  We model them as (label, action) so RefreshContent can render
        //   them with the SAME MakeBtn click plumbing as buildables — the fix that
        //   made buildables clickable (fillImg.raycastTarget=true) covers these too.
        //
        //   Ordered RimWorld-like: Orders, then Zone, then the build categories.
        //   "isActive" lets the row reflect the current designation mode (▣ when on).
        private struct OrderItem
        {
            public string label;
            public System.Action invoke;        // enter/toggle the designation mode
            public System.Func<bool> isActive;  // is that mode currently active?
            public OrderItem(string label, System.Action invoke, System.Func<bool> isActive)
            { this.label = label; this.invoke = invoke; this.isActive = isActive; }
        }

        private static readonly Dictionary<string, OrderItem[]> OrderCategories = new()
        {
            // Orders (지시) — RimWorld Orders tab: Chop + Mine + Deconstruct.
            ["Orders (지시)"] = new[]
            {
                // #231 RimWorld 정합 — 벌목 drag 지정(채광과 대칭).  STEP2 로 자동벌목을 끊은
                //  뒤 벌목 수단이 우클릭뿐이라 불편했던 것 해결: drag 로 나무 여러 그루 지정.
                new OrderItem("벌목 (C)",
                    () => { if (TreeChopDesignation.Instance != null) TreeChopDesignation.Instance.SetMode(true); },
                    () => TreeChopDesignation.Instance != null && TreeChopDesignation.Instance.ModeActive),
                new OrderItem("채광 (M)",
                    () => { if (MineDesignation.Instance != null) MineDesignation.Instance.SetMode(true); },
                    () => MineDesignation.Instance != null && MineDesignation.Instance.ModeActive),
                new OrderItem("해체 (X)",
                    () => { if (DeconstructDesignation.Instance != null) DeconstructDesignation.Instance.SetMode(true); },
                    () => DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive),
            },
            // Zone (구역) — RimWorld Zone tab: Grow zone + Stockpile + Dumping.
            //   ZONE wave (운영자 "영역지정 필수"): 저장존(Z1)/폐기존(Z3) 추가.  둘 다
            //   StockpileDesignation 의 drag 모드를 켠다 — 저장=일반 preset, 폐기=Low+
            //   Stone-only preset.  buildable 과 동일한 MakeBtn onClick 플러밍(저장은
            //   ▣ active 반영) — Zone 카테고리에 항목 추가만, 다른 카테고리/구조 미변경.
            ["Zone (구역)"] = new[]
            {
                new OrderItem("경작 (P)",
                    () => { if (GrowZoneDesignation.Instance != null) GrowZoneDesignation.Instance.SetMode(true); },
                    () => GrowZoneDesignation.Instance != null && GrowZoneDesignation.Instance.ModeActive),
                new OrderItem("저장 (O)",
                    () => { if (StockpileDesignation.Instance != null) StockpileDesignation.Instance.SetMode(true); },
                    () => StockpileDesignation.Instance != null
                          && StockpileDesignation.Instance.ModeActive
                          && !StockpileDesignation.Instance.DumpingMode),
                new OrderItem("폐기",
                    () => { if (StockpileDesignation.Instance != null) StockpileDesignation.Instance.SetDumpingMode(true); },
                    () => StockpileDesignation.Instance != null
                          && StockpileDesignation.Instance.ModeActive
                          && StockpileDesignation.Instance.DumpingMode),
                // ROOF wave (운영자: "RimWorld처럼 지붕영역 설정 메뉴 따로 있음") — the
                //   vanilla RimWorld "Roof Area" (지붕 영역) lives under the Zone tab.
                //   This row enters RoofDesignation's drag-rect mode (hotkey U); the
                //   same MakeBtn onClick plumbing the other Zone items use renders it
                //   (▣ active reflected).  ROOF-row addition ONLY — no other
                //   category/structure touched.
                new OrderItem("지붕 영역 (U)",
                    () => { if (RoofDesignation.Instance != null) RoofDesignation.Instance.SetMode(true); },
                    () => RoofDesignation.Instance != null && RoofDesignation.Instance.ModeActive),
            },
        };

        // 림월드 vanilla 패턴 — 카테고리별 buildable 목록
        private static readonly Dictionary<string, (BuildManager.Mode mode, string label, int cost)[]> Categories = new()
        {
            ["Structure (구조)"] = new[] {
                (BuildManager.Mode.Wall,      "벽 (목재 5)",   5),
                (BuildManager.Mode.WallStone, "벽 (석재 5)",   5),  // #127
                (BuildManager.Mode.Door,      "문 (목재 3)",   3),
                // W-M6-04 (B5) — autodoor, the Door-tab variety row (vanilla
                //   RimWorld Door tab has plain door + autodoor).  An autodoor is
                //   the SAME DoorEntity built with a HIGHER passMul (≈0.95 vs the
                //   plain door's 0.65) so a pawn crosses it FASTER, at a HIGHER
                //   wood cost (목재 6 > 문 3).  Surfaced HERE ONLY (no hotkey — the
                //   key space B/F/G/T/Y/N/R/X/M/P/K/J/H/L/E + WASD is contended),
                //   exactly like the fence-gate / barricade rows.  The existing
                //   buildable onClick wiring in RefreshContent routes this
                //   (mode,label,cost) tuple through BuildManager.Instance.SetMode,
                //   so clicking 자동문 enters Mode.Autodoor with ZERO click-plumbing
                //   change.  Cost 6 = wood, matches BuildManager.autodoorCost.
                (BuildManager.Mode.Autodoor,  "자동문 (목재 6)", 6),
                // W-M6-02 (B3) — fence + fence-gate, the cheapest Structure-tab
                //   staple (목재 1, LOW, PASSABLE).  Fence also has hotkey E;
                //   the gate is surfaced HERE ONLY (no hotkey).  The existing
                //   buildable onClick wiring in RefreshContent routes every
                //   (mode,label,cost) tuple through BuildManager.Instance.SetMode,
                //   so clicking 울타리 / 울타리 문 enters Mode.Fence / Mode.FenceGate
                //   with ZERO click-plumbing change.  Both pay 목재 1 (fenceCost).
                (BuildManager.Mode.Fence,     "울타리 (목재 1)",     1),
                (BuildManager.Mode.FenceGate, "울타리 문 (목재 1)",  1),
            },
            // W-M6-03 (B4) — Security (방어) category, the vanilla Architect
            //   Security tab.  Mirrors EXACTLY how the Fence rows were added under
            //   Structure: a (mode,label,cost) tuple whose onClick is routed by the
            //   EXISTING buildable wiring in RefreshContent through
            //   BuildManager.Instance.SetMode — so clicking 바리케이드 enters
            //   Mode.Barricade with ZERO click-plumbing change.  Cost 5 = wood,
            //   matches BuildManager.barricadeCost.  UNLIKE the passable fence, the
            //   placed BarricadeEntity blocks pathing like a low wall (pawns detour
            //   around it).  A dedicated Security category keeps defensive
            //   structures out of the Structure grouping and leaves room for future
            //   defensive types — and is the row B4 acceptance ("buildable from
            //   Architect Security category").  NO cover-math (gated).  The barricade
            //   has NO hotkey (surfaced HERE ONLY, like the fence-gate) to avoid the
            //   contended key space (B/F/G/T/Y/N/R/X/M/P/K/J/H/L/E + WASD camera).
            ["Security (방어)"] = new[] {
                (BuildManager.Mode.Barricade, "바리케이드 (목재 5)", 5),
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

        // ── v3 audit: per-buildable tooltip (자재/비용) + icon ────────────────
        //   The audit flagged the Architect rows as text-only with no hover help.
        //   For each buildable we derive (a) the material it is paid in and (b)
        //   the sprite key, so a hover shows e.g. "목재 벽 — 목재 5" and the row
        //   carries a small icon (wall=wall_wood).  These are READ-ONLY lookups
        //   keyed by BuildManager.Mode; they add NO new build modes and do NOT
        //   change any GameObject name or onClick wiring.

        // Stone-paid modes (mirrors BuildManager.PaysWithStone — kept local so we
        //   don't reach into another lane; if a mode is here it costs 석재, else 목재).
        private static bool PaysWithStone(BuildManager.Mode m) =>
            m == BuildManager.Mode.WallStone || m == BuildManager.Mode.FloorStone;

        // Sprite key under Resources/Sprites/<key> (designer-replaceable PNG).
        //   Resources.Load returns null for keys whose PNG isn't under Resources;
        //   in that case MakeIcon falls back to a flat material-colored swatch, so
        //   the row always shows SOMETHING (never magenta / never empty).
        private static string IconKey(BuildManager.Mode m) => m switch
        {
            BuildManager.Mode.Wall            => "wall_wood",
            BuildManager.Mode.WallStone       => "wall_wood",   // same silhouette; tinted grey by swatch fallback
            BuildManager.Mode.Door            => "door_wood",
            BuildManager.Mode.Autodoor        => "autodoor",
            BuildManager.Mode.Fence           => "fence",
            BuildManager.Mode.FenceGate       => "fence_gate",
            BuildManager.Mode.Barricade       => "barricade",
            BuildManager.Mode.Floor           => "floor_wood",
            BuildManager.Mode.FloorStone      => "stone_floor",
            BuildManager.Mode.Bed             => "bed_wood",
            BuildManager.Mode.BedFine         => "bed_fine",
            BuildManager.Mode.BedSleepingSpot => "bed_wood",
            BuildManager.Mode.Stove           => "stove",
            BuildManager.Mode.Lamp            => "lamp",
            _ => "",
        };

        // Plain Korean noun for the tooltip body (label already has the cost in
        //   parens; the tooltip restates "<thing> — <material> <n>" RimWorld-style).
        private static string ThingKr(BuildManager.Mode m) => m switch
        {
            BuildManager.Mode.Wall            => "목재 벽",
            BuildManager.Mode.WallStone       => "석재 벽",
            BuildManager.Mode.Door            => "문",
            BuildManager.Mode.Autodoor        => "자동문 (통과 빠름)",
            BuildManager.Mode.Fence           => "울타리 (통과 가능)",
            BuildManager.Mode.FenceGate       => "울타리 문 (통과 가능)",
            BuildManager.Mode.Barricade       => "바리케이드 (경로 차단)",
            BuildManager.Mode.Floor           => "나무 바닥",
            BuildManager.Mode.FloorStone      => "석재 바닥 (이동 보너스)",
            BuildManager.Mode.Bed             => "목재 침대",
            BuildManager.Mode.BedFine         => "고급 침대 (수면 1.4x)",
            BuildManager.Mode.BedSleepingSpot => "수면 자리 (자재 불필요)",
            BuildManager.Mode.Stove           => "화덕 (요리)",
            BuildManager.Mode.Lamp            => "램프 (조명)",
            _ => "건축물",
        };

        // Final tooltip string for a buildable row.  cost 0 == 자재 불필요.
        private static string BuildableTooltip(BuildManager.Mode m, int cost)
        {
            if (cost <= 0) return $"{ThingKr(m)} — 자재 불필요";
            string mat = PaysWithStone(m) ? "석재" : "목재";
            return $"{ThingKr(m)} — {mat} {cost}";
        }

        // Material accent for the swatch fallback (wood = warm brown, stone = grey).
        private static Color MaterialColor(BuildManager.Mode m) =>
            PaysWithStone(m)
                ? new Color(0.62f, 0.62f, 0.66f, 1f)   // stone grey
                : new Color(0.55f, 0.38f, 0.20f, 1f);  // wood brown

        // Cache loaded icon sprites (Resources.Load each frame would be wasteful;
        //   we build the row once per RefreshContent so this is small, but caching
        //   keeps repeated category-toggles allocation-free).
        private readonly Dictionary<string, Sprite> _iconCache = new();
        private Sprite LoadIcon(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_iconCache.TryGetValue(key, out var cached)) return cached;
            var s = Resources.Load<Sprite>("Sprites/" + key);
            _iconCache[key] = s;   // may be null — cached so we don't re-probe
            return s;
        }

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
            // operator fb #1 added 2 categories (Orders+Zone) → taller panel so the
            //  10 collapsed headers + one expanded category's items fit without clip.
            rt.sizeDelta = new Vector2(280, 560);
            rt.anchoredPosition = new Vector2(12, 0);

            // #UI-restyle U7 — same MakeBorderedPanel the control bar / inspector use:
            //   warm-brown fill + Divider border on all 4 edges (was a flat borderless rect).
            //   PadOuter inset so the header + buttons breathe inside the frame.
            panelContent = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(
                rt, MelonS.GameProto.Core.UITheme.BorderPx, MelonS.GameProto.Core.UITheme.PanelBg,
                MelonS.GameProto.Core.UITheme.PadOuter);

            BuildMenu();

            // QA 전용: -open-architect [-arch-cat "<카테고리>"] 면 시작부터 메뉴를 열어둔다
            //  (AutoScreenshotter 결정적 캡처용; 비활성 오브젝트에선 Start 코루틴이 안 돌아서
            //   Awake 에서 직접 처리).  게임 동작엔 영향 없음 — 평소엔 SetActive(false).
            var args = System.Environment.GetCommandLineArgs();
            bool autoOpen = false;
            string cat = "Structure (구조)";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-open-architect") autoOpen = true;
                if (args[i] == "-arch-cat" && i + 1 < args.Length) cat = args[i + 1];
            }
            if (autoOpen)
            {
                activeCategory = cat;
                isOpen = true;
                gameObject.SetActive(true);
                RefreshContent();
            }
            else
            {
                gameObject.SetActive(false);
            }
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
            // v3 audit: rows about to be destroyed can't fire PointerExit, so
            //   force-clear any lingering tooltip override before rebuilding.
            if (HoverTooltip.Instance != null) HoverTooltip.Instance.ClearUIOverride();

            // 기존 children 정리
            for (int i = contentRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.transform.GetChild(i).gameObject);

            float y = 0;

            // ── Orders (지시) + Zone (구역) FIRST (RimWorld tab order) ──────────
            //   operator fb #1: Mine/Deconstruct under Orders, Grow-zone under Zone.
            foreach (var kv in OrderCategories)
            {
                string catName = kv.Key;
                var items = kv.Value;
                var headerGo = MakeBtn(contentRoot.transform, catName,
                    new Vector2(0, -y), MelonS.GameProto.Core.UITheme.HeaderBg,
                    () => { activeCategory = (activeCategory == catName) ? "" : catName; RefreshContent(); });
                var oht = headerGo.GetComponentInChildren<Text>();
                oht.text = (activeCategory == catName ? "▼ " : "▶ ") + catName;
                oht.fontStyle = FontStyle.Bold;
                y += 36;
                if (activeCategory == catName)
                {
                    foreach (var oi in items)
                    {
                        var item = oi;  // closure capture
                        bool on = item.isActive != null && item.isActive();
                        var itemGo = MakeBtn(contentRoot.transform,
                            (on ? "▣ " : "") + item.label,
                            new Vector2(16, -y),
                            on ? MelonS.GameProto.Core.UITheme.BtnActiveBg
                               : MelonS.GameProto.Core.UITheme.BtnInactiveBg,
                            () => {
                                // Enter the designation mode, then close the menu so
                                //  the player can immediately drag-select on the map
                                //  (mirrors how a buildable enters build mode + closes).
                                item.invoke?.Invoke();
                                Close();
                            },
                            // v3 audit: orders/zone rows cost no material — the
                            //   tooltip explains the drag-designation action instead.
                            tooltip: $"{item.label} — 맵에서 드래그하여 지정");
                        y += 32;
                    }
                }
            }

            // ── then the BUILD categories (Structure/Security/Floors/...) ───────
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
                    // #241 자재 부족 항목은 RimWorld 처럼 dim 표시 (석재 0 일 때 석재 항목 회색 등).
                    //  스냅샷(메뉴 열 때 기준) — 클릭은 막지 않음(RimWorld 도 자재 없이 청사진을
                    //  깔 수 있고 pawn 이 모이면 짓는다; ghost 빨강 + placement 가 별도 처리).
                    int curWood = ResourceManager.Instance != null ? ResourceManager.Instance.wood : 0;
                    int curStone = ResourceManager.Instance != null ? ResourceManager.Instance.stone : 0;
                    foreach (var (mode, label, cost) in items)
                    {
                        var bcap = mode;  // closure capture
                        bool affordable = cost <= 0
                            || (PaysWithStone(mode) ? curStone : curWood) >= cost;
                        // v3 audit: icon + 자재/비용 tooltip per buildable row.
                        //   Authored sprite when present; stone reuses the wall
                        //   silhouette tinted grey, otherwise white (use as-authored).
                        Sprite icon = LoadIcon(IconKey(mode));
                        Color tint = icon != null
                            ? (PaysWithStone(mode) ? new Color(0.72f, 0.72f, 0.78f, 1f) : Color.white)
                            : MaterialColor(mode);   // null sprite → solid material swatch
                        // #241 자재 부족 시 row 배경/아이콘/텍스트 dim.
                        Color rowBg = affordable
                            ? MelonS.GameProto.Core.UITheme.BtnInactiveBg
                            : new Color(0.11f, 0.09f, 0.08f, 0.55f);
                        if (!affordable) tint = new Color(tint.r, tint.g, tint.b, 0.4f);
                        string tip = affordable ? BuildableTooltip(mode, cost)
                            : BuildableTooltip(mode, cost) + "  — 자재 부족";
                        var rowGo = MakeBtn(contentRoot.transform, label,
                            new Vector2(16, -y), rowBg,
                            () => {
                                if (BuildManager.Instance != null)
                                {
                                    var newMode = (BuildManager.Instance.CurrentMode == bcap)
                                        ? BuildManager.Mode.Off : bcap;
                                    BuildManager.Instance.SetMode(newMode);
                                    Close();
                                }
                            },
                            icon: icon, iconTint: tint, tooltip: tip);
                        if (!affordable)
                        {
                            var rt2 = rowGo.GetComponentInChildren<Text>();
                            if (rt2 != null) rt2.color = new Color(0.55f, 0.50f, 0.45f, 1f);  // 회색 텍스트
                        }
                        y += 32;
                    }
                }
            }
        }

        private GameObject MakeBtn(Transform parent, string label, Vector2 pos, Color col, System.Action onClick,
                                   Sprite icon = null, Color? iconTint = null, string tooltip = null)
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
            // ROOT-CAUSE FIX (operator fb #2 "하위 메뉴가 아예 안 눌러짐"):
            //   MakeBorderedPanel sets BOTH the root border Image (img) AND the
            //   PanelFill Image (fillImg) to raycastTarget=false.  The Button below
            //   targets fillImg — so with fillImg.raycastTarget=false the Button GO
            //   had NO raycastable graphic at all and the GraphicRaycaster never hit
            //   it: every category-header / buildable click passed straight through.
            //   The standalone designation toggles worked because they explicitly
            //   re-enable toggleFill.raycastTarget=true (see e.g. MineDesignation);
            //   this menu never did.  Re-enable it on the targeted fill so real
            //   pointer clicks register.
            if (fillImg != null) fillImg.raycastTarget = true;
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

            // ── v3 audit: small buildable icon at the row's left edge ─────────
            //   When an icon sprite is supplied, draw a 22px square swatch inside
            //   the fill (raycastTarget=false so it never steals the button click)
            //   and push the label right so text doesn't overlap the glyph.
            //   No icon (e.g. Orders/Zone rows) → label keeps its original inset.
            float labelLeft = 8f;
            if (icon != null || iconTint.HasValue)
            {
                const float IconBox = 22f;
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(go.transform, false);
                var iimg = iconGo.AddComponent<Image>();
                iimg.sprite = icon;                       // may be null → flat swatch
                // Caller picks the color: authored-sprite tint (grey for stone
                //   reuse) when icon!=null, else the solid material swatch color.
                iimg.color = iconTint ?? Color.white;
                iimg.preserveAspect = true;
                iimg.raycastTarget = false;
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0, 0.5f);
                irt.anchorMax = new Vector2(0, 0.5f);
                irt.pivot = new Vector2(0, 0.5f);
                irt.sizeDelta = new Vector2(IconBox, IconBox);
                irt.anchoredPosition = new Vector2(6, 0);
                labelLeft = 6 + IconBox + 6;              // icon inset + box + gap
            }

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
            trt.sizeDelta = new Vector2(-(labelLeft + 8), 0);
            trt.anchoredPosition = new Vector2(labelLeft, 0);

            // ── v3 audit: hover tooltip (자재/비용) on the row ────────────────
            //   The row IS UI, so HoverTooltip's world path Hide()s over it; we
            //   push the text through HoverTooltip.ShowText (UI override path).
            //   EventTrigger PointerEnter/Exit ONLY — does not touch the Button's
            //   onClick or the GameObject name.  No singleton OnEnable subscribe
            //   (#7): we resolve HoverTooltip.Instance lazily inside the callback.
            if (!string.IsNullOrEmpty(tooltip))
            {
                var trig = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var tipText = tooltip;   // closure capture

                var enter = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                enter.callback.AddListener((data) =>
                {
                    if (HoverTooltip.Instance != null)
                        HoverTooltip.Instance.ShowText(tipText, Input.mousePosition);
                });
                trig.triggers.Add(enter);

                var exit = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
                exit.callback.AddListener((data) =>
                {
                    if (HoverTooltip.Instance != null)
                        HoverTooltip.Instance.ClearUIOverride(tipText);
                });
                trig.triggers.Add(exit);
            }

            return go;
        }

        public void Toggle() { if (isOpen) Close(); else Open(); }
        public void Open() { isOpen = true; gameObject.SetActive(true); }
        public void Close()
        {
            isOpen = false;
            // v3 audit: clear any row tooltip so it doesn't linger after the menu hides.
            if (HoverTooltip.Instance != null) HoverTooltip.Instance.ClearUIOverride();
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // F8 림월드 Architect 단축키
            if (Input.GetKeyDown(KeyCode.F8)) Toggle();
        }
    }
}
