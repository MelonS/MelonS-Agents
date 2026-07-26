using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #5+#10 - 레퍼런스 콜로니심 Architect 메뉴 패턴.
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
        /// <summary>F2 패널 겹침 — PawnInfoPanel 이 시프트 판단에 읽는다.</summary>
        public bool IsOpen => isOpen;
        private GameObject contentRoot;
        private GameObject shelfRoot;          // #콜로니심파리티 — 우측 아이콘 셸프
        private string activeCategory = "";
        /// <summary>QA 용 — 현재 펼친 카테고리 키 (레퍼런스 콜로니심파리티 전환으로 ▶/▼ 텍스트
        ///  마커가 사라져 ArchitectClickAutoQA 가 상태를 직접 읽는다).</summary>
        public string ActiveCategory => activeCategory;

        // #콜로니심파리티 — 카테고리 표시 순서: the reference sim Architect 탭 순서(Orders→Zone→
        //  Structure→Production→Furniture→…→Security→…→Floors)의 부분집합.  2열
        //  row-major 로 채움 (the reference sim 카테고리 패널과 동일한 채움 방향).
        private static readonly string[] AllCategoryOrder =
        {
            "Orders (지시)",    "Zone (구역)",
            "Structure (구조)", "Production (생산)",
            "Furniture (가구)", "Security (방어)",
            "Floors (바닥)",    "Lighting (조명)",
        };

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

        // ── operator fb #1+A: the reference sim "Orders (지시)" + "Zone (구역)" categories ──
        //   In vanilla the reference sim, Mine + Deconstruct live under an ORDERS tab and
        //   Grow-zone lives under a ZONE tab — NOT as standalone screen buttons.
        //   We removed the 3 standalone toggles (Btn_채광/Btn_경작/해체) and fold
        //   them here.  These items are NOT BuildManager build-modes; each invokes
        //   the existing designation manager's public SetMode(...)/Toggle() entry
        //   point.  We model them as (label, action) so RefreshContent can render
        //   them with the SAME MakeBtn click plumbing as buildables — the fix that
        //   made buildables clickable (fillImg.raycastTarget=true) covers these too.
        //
        //   Ordered colony-sim-like: Orders, then Zone, then the build categories.
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
            // Orders (지시) — the reference sim Orders tab: Chop + Mine + Deconstruct.
            ["Orders (지시)"] = new[]
            {
                // #231 장르 정합 — 벌목 drag 지정(채광과 대칭).  STEP2 로 자동벌목을 끊은
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
            // Zone (구역) — the reference sim Zone tab: Grow zone + Stockpile + Dumping.
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
                // ROOF wave (운영자: "the reference sim처럼 지붕영역 설정 메뉴 따로 있음") — the
                //   vanilla the reference sim "Roof Area" (지붕 영역) lives under the Zone tab.
                //   This row enters RoofDesignation's drag-rect mode (hotkey U); the
                //   same MakeBtn onClick plumbing the other Zone items use renders it
                //   (▣ active reflected).  ROOF-row addition ONLY — no other
                //   category/structure touched.
                new OrderItem("지붕 영역 (U)",
                    () => { if (RoofDesignation.Instance != null) RoofDesignation.Instance.SetMode(true); },
                    () => RoofDesignation.Instance != null && RoofDesignation.Instance.ModeActive),
                // ERASE wave (운영자 fb "창고/다른 영역/건축물 제거 기능 필요") — the reference sim
                //   의 각 Zone 도구는 영역 추가뿐 아니라 드래그 삭제를 갖는다.  방금
                //   추가된 RoofDesignation/GrowZoneDesignation/StockpileDesignation 의
                //   SetEraseMode(true) 공개 API 로 그 삭제 모드에 진입하는 항목을
                //   노출한다.  SetEraseMode 는 내부적으로 다른 designation/빌드 모드를
                //   전부 끄므로(SetModeInternal) 별도 "다른 모드 끄기" 배선 불필요.
                //   isActive 는 해당 도구가 켜진 채 EraseMode 일 때만 ▣ 로 반영(추가
                //   모드와 구분).  buildable/기존 OrderItem 과 동일한 MakeBtn onClick
                //   플러밍 — Zone 카테고리에 항목 추가만, 다른 구조 미변경.
                new OrderItem("경작 영역 제거",
                    () => { if (GrowZoneDesignation.Instance != null) GrowZoneDesignation.Instance.SetEraseMode(true); },
                    () => GrowZoneDesignation.Instance != null
                          && GrowZoneDesignation.Instance.ModeActive
                          && GrowZoneDesignation.Instance.EraseMode),
                new OrderItem("저장/폐기 영역 제거",
                    () => { if (StockpileDesignation.Instance != null) StockpileDesignation.Instance.SetEraseMode(true); },
                    () => StockpileDesignation.Instance != null
                          && StockpileDesignation.Instance.ModeActive
                          && StockpileDesignation.Instance.EraseMode),
                new OrderItem("지붕 영역 제거",
                    () => { if (RoofDesignation.Instance != null) RoofDesignation.Instance.SetEraseMode(true); },
                    () => RoofDesignation.Instance != null
                          && RoofDesignation.Instance.ModeActive
                          && RoofDesignation.Instance.EraseMode),
            },
        };

        // 레퍼런스 콜로니심 vanilla 패턴 — 카테고리별 buildable 목록
        private static readonly Dictionary<string, (BuildManager.Mode mode, string label, int cost)[]> Categories = new()
        {
            ["Structure (구조)"] = new[] {
                (BuildManager.Mode.Wall,      "벽 (목재 5)",   5),
                (BuildManager.Mode.WallStone, "벽 (석재 5)",   5),  // #127
                (BuildManager.Mode.Door,      "문 (목재 25)",  25),
                // W-M6-04 (B5) — autodoor, the Door-tab variety row (vanilla
                //   the reference sim Door tab has plain door + autodoor).  An autodoor is
                //   the SAME DoorEntity built with a HIGHER passMul (~0.95 vs the
                //   plain door's 0.65) so a pawn crosses it FASTER, at a HIGHER
                //   wood cost (목재 6 > 문 3).  Surfaced HERE ONLY (no hotkey — the
                //   key space B/F/G/T/Y/N/R/X/M/P/K/J/H/L/E + WASD is contended),
                //   exactly like the fence-gate / barricade rows.  The existing
                //   buildable onClick wiring in RefreshContent routes this
                //   (mode,label,cost) tuple through BuildManager.Instance.SetMode,
                //   so clicking 자동문 enters Mode.Autodoor with ZERO click-plumbing
                //   change.  Cost 6 = wood, matches BuildManager.autodoorCost.
                (BuildManager.Mode.Autodoor,  "자동문 (목재 40)", 40),   // 밸런스 A3
                // W-M6-02 (B3) — fence + fence-gate, the cheapest Structure-tab
                //   staple (목재 1, LOW, PASSABLE).  Fence also has hotkey E;
                //   the gate is surfaced HERE ONLY (no hotkey).  The existing
                //   buildable onClick wiring in RefreshContent routes every
                //   (mode,label,cost) tuple through BuildManager.Instance.SetMode,
                //   so clicking 울타리 / 울타리 문 enters Mode.Fence / Mode.FenceGate
                //   with ZERO click-plumbing change.  Both pay 목재 1 (fenceCost).
                // R4 정직화(2026-07-24): 장식용 명시 — 통행 차단 기대 배신 방지
                (BuildManager.Mode.Fence,     "울타리 (장식용, 목재 1)",     1),
                (BuildManager.Mode.FenceGate, "울타리 문 (장식용, 목재 1)",  1),
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
                (BuildManager.Mode.Floor,      "나무 바닥 (목재 3)", 3),   // 밸런스 A6
                (BuildManager.Mode.FloorStone, "석재 바닥 (석재 3)", 3),   // 밸런스 A6
            },
            ["Furniture (가구)"] = new[] {
                // #154 - wiki: sleeping spot 0.8x / wood bed 1.0x / fine 1.4x
                (BuildManager.Mode.BedSleepingSpot, "수면 자리 (자재 X)",     0),
                (BuildManager.Mode.Bed,             "목재 침대 (목재 45)",   45),
                (BuildManager.Mode.BedFine,         "고급 침대 (목재 100)", 100),
                // W-M4-06 (#20) — table+chair eat/rec spot (hotkey J, 목재 6).
                //   Flagged by Lane A for QA wiring: Mode.TableChair routes through
                //   the existing SetMode/Close onClick plumbing with ZERO further change.
                (BuildManager.Mode.TableChair,      "탁자+의자 (목재 65)",   65),   // 밸런스 A6
                // GDD G3 (2026-07-24): 무덤 — 자재 0·노동 8초.  시신 안장 → 봉분+각인.
                (BuildManager.Mode.Grave,           "무덤 (자재 X·노동)",     0),
            },
            ["Production (생산)"] = new[] {
                (BuildManager.Mode.Stove, "화덕 (목재 80)", 80),
                // #229 회귀 수리 — 맨땅 시작 전환 때 스타터 연구대만 제거되고 빌드
                //  경로가 없어 연구 축(테크 트리 전체)이 신규 게임에서 죽어 있었다.
                (BuildManager.Mode.ResearchBench, "연구대 (목재 75)", 75),
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
                (BuildManager.Mode.Lamp, "램프 (목재 20)", 20),   // 밸런스 A4
            },
        };

        // #63 건축 카테고리 표시 순서(정렬) — 건축류 먼저, 방어(Security)는 맨 뒤로 모음.
        //   dict 선언 순서와 분리해 표시 순서만 여기서 제어(항목/구조 미변경).
        private static readonly string[] CategoryOrder =
        {
            "Structure (구조)",
            "Floors (바닥)",
            "Furniture (가구)",
            "Production (생산)",
            "Lighting (조명)",
            "Security (방어)",
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
            // 시각버그 fix: 석재 벽은 갈색 wall_wood 를 회색 tint 하면 muddy brown 으로
            //  보여 목재 벽과 구분이 안 됐다 → 실제 회색 석재 sprite(stone_floor)로 교체해
            //  목재(갈색 판자) vs 석재(회색 블록)가 한눈에 구분되게.
            BuildManager.Mode.WallStone       => "stone_floor",
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
            BuildManager.Mode.ResearchBench   => "struct32_research_bench",
            BuildManager.Mode.Lamp            => "lamp",
            _ => "",
        };

        // Plain Korean noun for the tooltip body (label already has the cost in
        //   parens; the tooltip restates "<thing> — <material> <n>" colony-sim-style).
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
            BuildManager.Mode.ResearchBench   => "연구대 (림이 근처에 있으면 연구 진행)",
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

        // #260 카테고리 헤더를 한글 전용으로 표시(운영자 fb '완성도/일관성' — UI 가 한글인데
        //  카테고리만 "English (한글)" 혼용이라 미완성 현지화처럼 보임).  키는 영한 혼용 그대로
        //  두되(로직 안정), 표시만 괄호 안 한글 추출.  괄호 없으면 원문 그대로.
        private static string KrCat(string cat)
        {
            int a = cat.IndexOf('('), b = cat.IndexOf(')');
            return (a >= 0 && b > a) ? cat.Substring(a + 1, b - a - 1).Trim() : cat;
        }

        // Material accent for the swatch fallback (wood = warm brown, stone = grey).
        private static Color MaterialColor(BuildManager.Mode m) =>
            PaysWithStone(m)
                ? new Color(0.62f, 0.62f, 0.66f, 1f)   // stone grey
                : new Color(0.55f, 0.38f, 0.20f, 1f);  // wood brown

        // Cache loaded icon sprites (load-once; we build the row once per
        //   RefreshContent so this is small, but caching keeps repeated
        //   category-toggles allocation-free).
        //
        // ── 시각버그 fix (회색 플레이스홀더) ──────────────────────────────────
        //   이전 버전은 Resources.Load<Sprite>("Sprites/"+key) 로 아이콘을 찾았는데,
        //   실제 건물 sprite(wall_wood/door_wood/bed_wood/stove ...)는 전부
        //   Assets/Sprites/ 아래에 있고 Resources 폴더엔 없다 → 모든 key 가 null →
        //   모든 행이 MaterialColor swatch(대부분 목재 갈색)로만 그려져 구분 불가였다.
        //   BuildManager.EnsureLampSprite 와 동일한 정공법으로, Editor/batchmode
        //   에선 AssetDatabase 로 Assets/Sprites/<key>.png 를 직접 로드한다.
        //   (이 스크립트는 Assembly-CSharp 라 #if UNITY_EDITOR 로 UnityEditor 참조
        //    가능; 런타임 빌드에선 swatch fallback 으로 자재색 구분 유지 → 헤드리스 안전.)
        private readonly Dictionary<string, Sprite> _iconCache = new();
        private Sprite LoadIcon(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_iconCache.TryGetValue(key, out var cached)) return cached;
            Sprite s = null;
#if UNITY_EDITOR
            // Editor/batchmode(헤드리스 QA 포함): 실제 authored PNG 를 그대로 사용.
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/" + key + ".png");
#endif
            // 런타임 빌드 보강(폴백): 혹시 Resources 로 옮겨둔 sprite 가 있으면 사용.
            if (s == null) s = Resources.Load<Sprite>("Sprites/" + key);
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
            // #콜로니심파리티(운영자 2026-06-11 "건축 UI 거의 똑같이") — 좌하단 모서리 고정:
            //  the reference sim User_interface 위키 'Architect menu — Bottom left corner'.
            //  카테고리 2열 패널 + 우측 아이콘 셸프 구조 (RefreshContent/RefreshShelf).
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(252, 220);   // 높이는 RefreshContent 가 행 수에 맞춤
            rt.anchoredPosition = new Vector2(8, PanelBottomY);

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
            t.text = "건축 (F8)";
            t.font = font;
            t.fontSize = 20;
            t.fontStyle = FontStyle.Normal /* BitBit 자체 볼드 — 중첩 금지 (2026-07-25) */;
            t.color = MelonS.GameProto.Core.UITheme.AccentGold;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            trt.anchoredPosition = Vector2.zero;

            // content 영역 (카테고리 2열 버튼) — 헤더 strip 아래.
            contentRoot = new GameObject("Content");
            contentRoot.transform.SetParent(panelContent, false);
            var crt = contentRoot.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0);
            crt.anchorMax = new Vector2(1, 1);
            crt.offsetMin = new Vector2(0, 0);
            crt.offsetMax = new Vector2(0, -44);   // leave room for the 36px header + 8 gap

            // #콜로니심파리티 — 아이콘 셸프: 패널 오른쪽에 가로로 앉는 75px급 셀 줄
            //  (the reference sim 는 카테고리 패널 우측, 하단 탭바 위에 designator 셸프가 펼쳐짐).
            //  마스크가 없으므로 패널 rect 밖이어도 정상 렌더.
            shelfRoot = new GameObject("Shelf");
            shelfRoot.transform.SetParent(rt, false);
            var srt = shelfRoot.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(1f, 0f);
            srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(0f, 0f);
            srt.anchoredPosition = new Vector2(6f, 0f);
            srt.sizeDelta = new Vector2(10f, 92f);
            shelfRoot.SetActive(false);

            RefreshContent();
        }

        private void RefreshContent()
        {
            // v3 audit: rows about to be destroyed can't fire PointerExit, so
            //   force-clear any lingering tooltip override before rebuilding.
            if (HoverTooltip.Instance != null) HoverTooltip.Instance.ClearUIOverride();

            for (int i = contentRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.transform.GetChild(i).gameObject);

            // ── #콜로니심파리티 (운영자 2026-06-11 "건축 UI 거의 똑같이") ────────────
            //  카테고리 = 좌하단 2열 텍스트 버튼 (the reference sim 카테고리 패널).  클릭하면
            //  우측 셸프에 해당 카테고리의 아이콘 셀들이 펼쳐진다 (RefreshShelf).
            //  셀 클릭 = 모드 진입 + 메뉴 유지 (연속 배치 — the reference sim 동일), 해제는
            //  우클릭/ESC (BuildManager/designation 들이 이미 처리).
            const float colW = 113f, rowH = 34f, gap = 4f;
            int idx = 0;
            foreach (var catName in AllCategoryOrder)
            {
                if (!OrderCategories.ContainsKey(catName) && !Categories.ContainsKey(catName))
                    continue;
                int col = idx % 2, row = idx / 2;
                string cap = catName;   // closure capture
                bool isOn = activeCategory == catName;
                MakeCatBtn(contentRoot.transform, KrCat(catName),
                    new Vector2(col * (colW + gap), -row * (rowH + gap)), colW, rowH,
                    isOn ? MelonS.GameProto.Core.UITheme.BtnActiveBg : InactiveBg,
                    isOn,
                    () => { activeCategory = (activeCategory == cap) ? "" : cap; RefreshContent(); });
                idx++;
            }

            // 패널 높이 = 헤더(36+8) + 카테고리 행들 + 하단 여백
            int rows = (idx + 1) / 2;
            rt.sizeDelta = new Vector2(252f, rows * (rowH + gap) + 44f + 12f);

            RefreshShelf();
        }

        /// <summary>#콜로니심파리티 — 카테고리 버튼 (2열 그리드용 고정폭).</summary>
        private void MakeCatBtn(Transform parent, string label, Vector2 pos, float w, float h,
                                Color col, bool active, System.Action onClick)
        {
            var go = new GameObject($"Cat_{label}");
            go.transform.SetParent(parent, false);
            var brt = go.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1);
            brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 1);
            brt.sizeDelta = new Vector2(w, h);
            brt.anchoredPosition = pos;

            var img = go.AddComponent<Image>();
            img.color = MelonS.GameProto.Core.UITheme.Divider;
            var fillRt = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(brt, 2f, col);
            var fillImg = fillRt.parent.GetComponent<Image>();
            if (fillImg != null) fillImg.raycastTarget = true;   // 클릭 가능 (MakeBtn 과 동일 fix)
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = fillImg;
            btn.onClick.AddListener(() => { PlayClickBlip(); onClick?.Invoke(); });

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var t = txtGo.AddComponent<Text>();
            t.text = label;
            t.font = font;
            t.fontSize = 16;
            t.fontStyle = FontStyle.Normal /* BitBit 자체 볼드 — 중첩 금지 (2026-07-25) */;
            t.color = active ? MelonS.GameProto.Core.UITheme.AccentGold
                             : MelonS.GameProto.Core.UITheme.TextPrimary;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt2 = txtGo.GetComponent<RectTransform>();
            trt2.anchorMin = Vector2.zero;
            trt2.anchorMax = Vector2.one;
            trt2.sizeDelta = Vector2.zero;
        }

        /// <summary>#콜로니심파리티 — 우측 아이콘 셸프 재구성.  활성 카테고리의 designator
        ///  들을 68px 셀(아이콘 + 하단 이름 띠 + 핫키 글자)로 가로 나열.  비용은 셀이
        ///  아니라 호버 툴팁(the reference sim 인포박스 자리) — LiveCostFor 실비용 (#4.5 2단계).</summary>
        private void RefreshShelf()
        {
            if (shelfRoot == null) return;
            for (int i = shelfRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(shelfRoot.transform.GetChild(i).gameObject);

            float x = 0f;
            const float cellW = 68f, cgap = 5f;
            bool any = false;

            if (OrderCategories.TryGetValue(activeCategory, out var orderItems))
            {
                int ci = 0;   // UI겹침 P1-2 — 5셀 줄바꿈 (1행 우단이 징집 버튼(x680~)을 안 덮게)
                foreach (var oi in orderItems)
                {
                    var item = oi;   // closure capture
                    bool on = item.isActive != null && item.isActive();
                    string nm = NameOf(item.label);
                    bool isErase = nm.Contains("제거");
                    // 디자인폴리시(2026-06-14): 제거 항목 셀명 축약 — "저장/폐기 영역 제거"
                    //  (9자)는 68px 셀폭을 넘쳐 라벨이 잔디(월드) 위로 흘러나왔다.
                    //  셀엔 축약명("저장 제거"), 툴팁엔 전체 설명 유지.
                    string cellNm = isErase
                        ? nm.Replace("/폐기", "").Replace(" 영역 제거", " 제거").Replace("영역 제거", "제거")
                        : nm;
                    float cx2 = (ci % 5) * (cellW + cgap);
                    float cy2 = (ci / 5) * 95f;
                    MakeShelfCell(cx2, cy2, cellNm, HotkeyOf(item.label), null,
                        isErase ? "×" : nm.Substring(0, 1), on, true,
                        nm + " — 맵에서 드래그하여 지정",
                        () => { item.invoke?.Invoke(); RefreshContent(); });
                    ci++; x = Mathf.Max(x, cx2 + cellW + cgap); any = true;
                }
                ShelfRows = (ci + 4) / 5;
            }
            else if (Categories.TryGetValue(activeCategory, out var buildItems))
            {
                int bi = 0;   // UI겹침 P1-2 — 5셀 줄바꿈
                int curWood = ResourceManager.Instance != null ? ResourceManager.Instance.wood : 0;
                int curStone = ResourceManager.Instance != null ? ResourceManager.Instance.stone : 0;
                foreach (var (mode, label, cost) in buildItems)
                {
                    var bcap = mode;   // closure capture
                    int liveCost = BuildManager.Instance != null
                        ? BuildManager.Instance.LiveCostFor(mode) : cost;   // #4.5 — 실비용
                    bool affordable = liveCost <= 0
                        || (PaysWithStone(mode) ? curStone : curWood) >= liveCost;
                    Sprite icon = BuildManager.Instance != null
                        ? BuildManager.Instance.SpriteForMode(mode)
                        : LoadIcon(IconKey(mode));
                    bool on = BuildManager.Instance != null
                              && BuildManager.Instance.CurrentMode == bcap;
                    string nm = NameOf(ThingKr(mode));   // "목재 벽"/"석재 벽" — 셀명 충돌 방지
                    string tip = BuildableTooltip(mode, liveCost)
                                 + (affordable ? "" : "  — 자재 부족");
                    float cx2 = (bi % 5) * (cellW + cgap);
                    float cy2 = (bi / 5) * 95f;
                    MakeShelfCell(cx2, cy2, nm, HotkeyOf(label), icon, "", on, affordable, tip,
                        () =>
                        {
                            if (BuildManager.Instance == null) return;
                            var newMode = (BuildManager.Instance.CurrentMode == bcap)
                                ? BuildManager.Mode.Off : bcap;
                            BuildManager.Instance.SetMode(newMode);
                            RefreshContent();   // 활성 하이라이트 갱신 (메뉴는 유지)
                        });
                    bi++; x = Mathf.Max(x, cx2 + cellW + cgap); any = true;
                }
                ShelfRows = (bi + 4) / 5;
            }

            var srt2 = shelfRoot.GetComponent<RectTransform>();
            if (srt2 != null) srt2.sizeDelta = new Vector2(Mathf.Max(10f, x), 92f + (ShelfRows - 1) * 95f);
            shelfRoot.SetActive(any && isOpen);
        }

        /// <summary>#콜로니심파리티 — 셸프 셀: 68px 정사각, 아이콘(또는 글리프) + 하단 이름
        ///  띠 + 좌상단 핫키 글자.  활성 셀은 금색 테두리 (the reference sim 선택 designator).</summary>
        /// <summary>UI겹침 P1-2/커플링 B — 현재 셸프 행 수 (PawnInfoPanel y 시프트 연동).</summary>
        public int ShelfRows { get; private set; } = 1;

        /// <summary>패널·셸프의 화면 바닥 기준 y.
        ///
        /// 운영자 피드백(2026-07-27) "건축메뉴 떠 있을때 다른 메뉴 위치 좀 이상해":
        /// 기존 값 8 은 하단 컨트롤바(바닥 y24~96)와 **같은 높이 띠**여서, 좌측 셸프와
        /// 중앙 컨트롤바가 가로로 이어진 하나의 띠처럼 읽혔다.  컨트롤바 위(96+8)로
        /// 올려 층을 분리한다.</summary>
        public const float PanelBottomY = 104f;

        private const float ShelfCellH = 92f;
        private const float ShelfRowStep = 95f;

        /// <summary>셸프 실제 상단 y (화면 바닥 기준).  PawnInfoPanel 이 이 값을 읽어
        /// 자기 위치를 정한다 — 예전처럼 108/95 매직넘버를 양쪽에 복제하면 패널을 옮길 때마다
        /// 한쪽만 고쳐져 겹친다.</summary>
        public float ShelfTopY => PanelBottomY + ShelfCellH + (ShelfRows - 1) * ShelfRowStep;

        /// <summary>카테고리 패널 자체의 상단 y (화면 바닥 기준).
        ///
        /// 2026-07-27 회귀: 인포패널을 좌측에 고정하면서 y 기준으로 `ShelfTopY` 를 썼는데,
        /// **패널이 셸프보다 높다**(셸프는 우측에 붙은 아이콘 줄, 패널은 2열 카테고리 목록).
        /// 그 결과 인포패널 하단이 건축 패널 상단을 26% 덮어 수면·기분 바가 가려졌다.
        /// 위로 피할 기준은 둘 중 **더 높은 쪽**이어야 한다 → BlockTopY.</summary>
        public float PanelTopY => rt != null ? PanelBottomY + rt.sizeDelta.y : PanelBottomY + 220f;

        /// <summary>건축 UI 전체(패널 + 셸프)가 차지하는 최상단 y.  다른 패널이 이 위로 피한다.</summary>
        public float BlockTopY => Mathf.Max(ShelfTopY, PanelTopY);

        private void MakeShelfCell(float x, float yRow, string name, string hotkey, Sprite icon, string glyph,
                                   bool active, bool affordable, string tooltip, System.Action onClick)
        {
            const float Cell = 68f;
            var go = new GameObject($"Cell_{name}");
            go.transform.SetParent(shelfRoot.transform, false);
            var brt = go.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(0, 0);
            brt.pivot = new Vector2(0, 0);
            brt.sizeDelta = new Vector2(Cell, Cell + 22f);   // 셀 + 이름 띠 포함 높이
            brt.anchoredPosition = new Vector2(x, yRow);

            var img = go.AddComponent<Image>();
            img.color = active ? MelonS.GameProto.Core.UITheme.AccentGold
                               : MelonS.GameProto.Core.UITheme.Divider;
            var fillRt = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(
                brt, 2f, affordable ? InactiveBg : new Color(0.11f, 0.09f, 0.08f, 0.75f));
            var fillImg = fillRt.parent.GetComponent<Image>();
            if (fillImg != null) fillImg.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = fillImg;
            btn.onClick.AddListener(() => { PlayClickBlip(); onClick?.Invoke(); });

            // 아이콘 (스프라이트) 또는 글리프 (지시/구역 첫 글자, 제거류는 ×)
            if (icon != null)
            {
                var iGo = new GameObject("Icon");
                iGo.transform.SetParent(go.transform, false);
                var ii = iGo.AddComponent<Image>();
                ii.sprite = icon;
                ii.preserveAspect = true;
                ii.raycastTarget = false;
                ii.color = affordable ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                var irt = iGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.5f, 1f);
                irt.anchorMax = new Vector2(0.5f, 1f);
                irt.pivot = new Vector2(0.5f, 1f);
                irt.sizeDelta = new Vector2(46f, 46f);
                irt.anchoredPosition = new Vector2(0f, -10f);
            }
            else if (!string.IsNullOrEmpty(glyph))
            {
                var gGo = new GameObject("Glyph");
                gGo.transform.SetParent(go.transform, false);
                var gt = gGo.AddComponent<Text>();
                gt.text = glyph;
                gt.font = font;
                gt.fontSize = 30;
                gt.fontStyle = FontStyle.Normal /* BitBit 자체 볼드 — 중첩 금지 (2026-07-25) */;
                // 디자인폴리시(2026-06-14): 제거 도구 글리프 — 살몬레드(0.92,0.40,0.35)는
                //  "오류/삭제실패"로 읽혔다.  차분한 테라코타로 톤다운 = 경고가 아닌 도구.
                gt.color = glyph == "×"
                    ? new Color(0.78f, 0.52f, 0.46f, 1f)
                    : MelonS.GameProto.Core.UITheme.AccentOrange;
                gt.alignment = TextAnchor.MiddleCenter;
                gt.raycastTarget = false;
                var grt = gGo.GetComponent<RectTransform>();
                grt.anchorMin = new Vector2(0f, 0.25f);
                grt.anchorMax = new Vector2(1f, 1f);
                grt.sizeDelta = Vector2.zero;
            }

            // 하단 이름 띠 (the reference sim 셀 하단 검은 띠 + 흰 소형 텍스트)
            var stripGo = new GameObject("NameStrip");
            stripGo.transform.SetParent(go.transform, false);
            var sImg = stripGo.AddComponent<Image>();
            sImg.color = new Color(0f, 0f, 0f, 0.55f);
            sImg.raycastTarget = false;
            var sRt = stripGo.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0f);
            sRt.anchorMax = new Vector2(1f, 0f);
            sRt.pivot = new Vector2(0.5f, 0f);
            sRt.sizeDelta = new Vector2(0f, 22f);
            sRt.anchoredPosition = Vector2.zero;
            var nGo = new GameObject("Name");
            nGo.transform.SetParent(stripGo.transform, false);
            var nt = nGo.AddComponent<Text>();
            nt.text = name;
            nt.font = font;
            nt.fontSize = 12;
            nt.color = affordable ? Color.white : new Color(0.7f, 0.65f, 0.6f, 1f);
            nt.alignment = TextAnchor.MiddleCenter;
            nt.raycastTarget = false;
            // 디자인폴리시(2026-06-14): 긴 셀명이 옆으로 흘러 월드(잔디) 위에 떠버리던 것
            //  차단 — 가로 Wrap + 세로 Overflow(아래로 줄바꿈).  셀폭(68px) 밖으로 절대
            //  안 넘친다.  (대부분 명사는 1줄로 맞으므로 시각 변화 없음.)
            nt.horizontalOverflow = HorizontalWrapMode.Wrap;
            nt.verticalOverflow = VerticalWrapMode.Overflow;
            var nRt = nGo.GetComponent<RectTransform>();
            nRt.anchorMin = Vector2.zero;
            nRt.anchorMax = Vector2.one;
            nRt.sizeDelta = Vector2.zero;

            // 좌상단 핫키 글자 (the reference sim 셀 좌상단 키 표기)
            if (!string.IsNullOrEmpty(hotkey))
            {
                var hGo = new GameObject("Hotkey");
                hGo.transform.SetParent(go.transform, false);
                var htx = hGo.AddComponent<Text>();
                htx.text = hotkey;
                htx.font = font;
                htx.fontSize = 11;
                htx.fontStyle = FontStyle.Normal /* BitBit 자체 볼드 — 중첩 금지 (2026-07-25) */;
                htx.color = MelonS.GameProto.Core.UITheme.AccentGold;
                htx.alignment = TextAnchor.UpperLeft;
                htx.raycastTarget = false;
                var hRt = hGo.GetComponent<RectTransform>();
                hRt.anchorMin = new Vector2(0f, 1f);
                hRt.anchorMax = new Vector2(0f, 1f);
                hRt.pivot = new Vector2(0f, 1f);
                hRt.sizeDelta = new Vector2(20f, 16f);
                hRt.anchoredPosition = new Vector2(4f, -3f);
            }

            // 호버 툴팁 (비용/설명) — MakeBtn 과 동일한 HoverTooltip UI override 경로
            if (!string.IsNullOrEmpty(tooltip))
            {
                var trig = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var tipText = tooltip;
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
        }

        /// <summary>"벽 (목재 5)" → "벽" / "벌목 (C)" → "벌목" — 셀 이름은 명사만 (비용은 툴팁).</summary>
        private static string NameOf(string label)
        {
            int p = label.IndexOf(" (");
            return p > 0 ? label.Substring(0, p) : label;
        }

        /// <summary>괄호 내용이 영문 1글자면 핫키로 해석 ("벌목 (C)" → "C").</summary>
        private static string HotkeyOf(string label)
        {
            int a = label.IndexOf('('), b = label.IndexOf(')');
            if (a >= 0 && b == a + 2)
            {
                char c = label[a + 1];
                if (c < (char)128) return c.ToString();
            }
            return "";
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
                // 시각버그 fix: 아이콘을 22→26px 로 키워 실제 sprite 가 또렷이 보이게.
                const float IconBox = 26f;
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
            // 시각버그 fix: 행 텍스트 가독성 — 14→16 은 이미 적용돼 있었으나 운영자가
            //  여전히 작다 보고 → 17px 로 한 단계 더 키워 어떤 건물인지 명확히 읽히게.
            t.fontSize = 17;
            t.fontStyle = FontStyle.Normal /* BitBit 자체 볼드 — 중첩 금지 (2026-07-25) */;
            t.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
            // 시각버그 fix (운영자: 항목 텍스트 오른쪽 잘림) — 라벨은 한 줄짜리이므로
            //  가로 overflow 를 Overflow 로 둬 rect 폭이 살짝 모자라도 글자가 잘리거나
            //  엉뚱하게 줄바꿈되지 않게(폭 확장과 병행).  세로는 한 줄 고정.
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            // 텍스트 영역을 아이콘 우측부터 패널 오른쪽 끝까지 충분히 — 우측 여백 6px.
            trt.sizeDelta = new Vector2(-(labelLeft + 6), 0);
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
        // #275 최상단.  #ui백로그 4.2 — 재오픈 시 ▣ 활성표시/자재 dim stale 해소:
        //  RefreshContent 는 idempotent(전체 재구성)이고 열 때 1회라 비용 무시 가능.
        public void Open() { isOpen = true; gameObject.SetActive(true); transform.SetAsLastSibling(); RefreshContent(); }
        public void Close()
        {
            isOpen = false;
            // v3 audit: clear any row tooltip so it doesn't linger after the menu hides.
            if (HoverTooltip.Instance != null) HoverTooltip.Instance.ClearUIOverride();
            gameObject.SetActive(false);
        }

        // #241b 자재 부족 dim 을 live 로 — 메뉴 열린 동안 wood/stone 이 바뀌면(채집/소비)
        //  RefreshContent 로 affordability 재평가.  매 frame 아니라 값 변화 시에만 rebuild
        //  (자원은 자주 안 바뀌어 flicker/비용 무시 가능).  the reference sim 도 live 로 회색 갱신.
        private int _lastWood = int.MinValue;
        private int _lastStone = int.MinValue;

        private void Update()
        {
            // F8 레퍼런스 콜로니심 Architect 단축키
            if (Input.GetKeyDown(KeyCode.F8)) Toggle();

            if (isOpen && ResourceManager.Instance != null)
            {
                int w = ResourceManager.Instance.wood;
                int s = ResourceManager.Instance.stone;
                if (w != _lastWood || s != _lastStone)
                {
                    _lastWood = w; _lastStone = s;
                    RefreshContent();
                }
            }
        }
    }
}
