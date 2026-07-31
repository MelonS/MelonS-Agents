using UnityEngine;
using UnityEngine.EventSystems;
using MelonS.GameProto.AI;   // #199 C3 - PathGrid terrain walkability

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 17-25: build-mode singleton.
    ///   B: 벽 (목재 5)
    ///   F: 바닥 (목재 1, no collider, indoor marker)
    ///   G: 문 (목재 3, trigger collider)
    ///   T: 화덕 (목재 10, Day 25 cooking station)
    ///   L: 램프 (목재 4, W-M4-04 #19 standing-lamp / torch — night light pool)
    ///   K: 석재 바닥 (석재 1, W-M4-05 #21 stone/paved floor — higher move bonus than wood)
    ///   E: 울타리 (목재 1, W-M6-02 B3 fence; LOW + PASSABLE, gate via Architect)
    ///   J: 탁자+의자 (목재 6, W-M4-06 #20 table+chair — eat/rec spot)
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        public static BuildManager Instance { get; private set; }

        // #154 - bed quality 3종, #127 - stone wall.
        // W-M4-04 (#19) - Lamp: torch/standing-lamp buildable that emits a warm
        //   night light pool (drawn by LampGlowDriver).  Mirrors the Stove entry
        //   exactly (1×1 footprint, wood cost, same ghost/cooldown/place path).
        // W-M4-05 (#21) - FloorStone: a stone/paved floor variant, paid from
        //   STONE (like WallStone) and giving a HIGHER move bonus than the wood
        //   floor.  Mirrors the EXISTING Floor entry EXACTLY (1×1, no collider,
        //   sortingOrder 1 ghost, same cooldown/ghost/placement-validation path)
        //   — only the cost resource, sprite, and placed entity (StoneFloorEntity,
        //   a FloorEntity subclass with a higher MoveBonus) differ.
        // W-M4-06 (#20) - TableChair: a combined table+chair furniture piece (an
        //   eat/rec spot), paid from WOOD.  Mirrors the EXISTING Stove entry
        //   EXACTLY (1×1 footprint, wood cost, sortingOrder-5 body + 1×1
        //   collider, same ghost/cooldown/placement-validation/place path) —
        //   only the cost, sprite, and placed entity (TableEntity + ChairEntity
        //   markers, so the existing eat/rest mood path can detect an eat spot
        //   under the pawn read-only) differ.
        // W-M6-02 (B3) - Fence + FenceGate: the cheapest Structure-tab staple.
        //   1 wood, LOW, PASSABLE — placed FenceEntity carries NO collider so a
        //   pawn paths straight through (mirrors the wood Floor's "no collider,
        //   walk over" model).  Both use the same lazy procedural prefab/sprite
        //   path as Lamp/FloorStone/TableChair so the build never produces a
        //   null/magenta entity even without an asset import + ZERO SceneSetup
        //   edit.  FenceGate is the same entity (IsGate=true) with a gate sprite.
        // W-M6-03 (B4) - Barricade: the cheapest Security-tab staple (목재 5).
        //   The DELIBERATE OPPOSITE of the passable Fence: the placed
        //   BarricadeEntity BLOCKS PATHING like a low wall (it reuses the EXISTING
        //   wall pathing-block API READ-ONLY — RegisterWallCell / SetStructureBlocked
        //   — in its own Start/OnDestroy, inventing no new pathfinding), so a pawn
        //   paths AROUND it.  It still reads LOW (sandbag row in the lower half,
        //   sortingOrder 2).  Uses the same lazy procedural prefab/sprite path as
        //   Lamp/FloorStone/TableChair/Fence so the build never produces a
        //   null/magenta entity even without an asset import + ZERO SceneSetup edit.
        //   NO cover-math (gated/over-scope) — visual + pathing only.
        // ⚠ enum 순서 불변 계약: StructureTag.modeInt 가 (int) 로 저장되므로 새 모드는
        //  반드시 끝에 append (삽입 시 기존 세이브의 구조물이 전부 다른 것으로 재구성됨).
        public enum Mode { Off, Wall, Floor, Door, Stove, Bed, WallStone, BedSleepingSpot, BedFine, Lamp, FloorStone, TableChair, Fence, FenceGate, Barricade, Autodoor, ResearchBench, Grave }
        public Mode CurrentMode { get; private set; } = Mode.Off;
        public bool BuildModeActive => CurrentMode != Mode.Off;
        // #182 - placement cooldown: SetMode 후 0.15s 동안 TryPlace skip.
        //  목적: Architect 버튼 click 같은 frame race 방지 (EventSystem guard 보다 견고).
        private float setModeTime = -10f;
        private const float PlaceCooldownSec = 0.15f;

        [SerializeField] private GameObject wallPrefab, floorPrefab, doorPrefab, stovePrefab, bedPrefab;
        // #159 - BuildAutoQA 가 spriteAsset 접근 필요 (외부 prefab 인스턴스는 SR.sprite 비할당).
        public Sprite WallSpriteRef => wallSprite;
        public Sprite BedSpriteRef => bedSprite;
        public GameObject WallPrefabRef => wallPrefab;
        public GameObject BedPrefabRef => bedPrefab;
        // 운영자 피드백 #3 (2026-06-12 AM): 목재 소모량 레퍼런스 동일 — 문 25/침대 45/
        //  화덕 80 (벽 5·바닥 1 은 기존 동일).  채집량(나무 ~25/그루)과 같은 라운드 파리티.
        // 밸런스 A6(2026-07-24): 바닥 1→3 (바닐라 파리티).
        [SerializeField] private int wallCost = 5, floorCost = 3, doorCost = 25, stoveCost = 80, bedCost = 45;
        [SerializeField] private int wallStoneCost = 5;  // #127 - 석재 5
        // W-M4-04 (#19) - Lamp cost.  the reference sim torch ~ 1 wood, standing lamp needs
        //   power; this prototype lamp is a cheap always-on light at 목재 4.
        [SerializeField] private int lampCost = 20;   // 밸런스 A4(2026-07-24): 4→20 파리티
        // #229 회귀 수리 (2026-06-12) — 맨땅 시작 전환 때 스타터 연구대만 제거되고
        //  빌드 경로가 없어 연구 축이 신규 게임에서 구조적으로 죽어 있었다 (StallReason
        //  힌트 "작업대 필요 (건축 F8)"가 거짓말 — 건축 메뉴에 연구대 없음).  Lamp 의
        //  Ensure* 런타임 템플릿 패턴 복제: SetRefs/씬 재생성 없이 buildable 추가.
        //  비용 15 = 화덕(10)과 고급침대(30) 사이, 테크 트리 관문이라 접근 가능하게.
        [SerializeField] private int benchCost = 75;   // #3 파리티 — 연구대
        // W-M4-04 (#19) - Lamp prefab + sprite.  Normally wired via SetRefs from
        //   SceneSetup like the others; but the Lane A contract forbids a
        //   SceneSetup edit, so these stay null and BuildManager builds them
        //   LAZILY + PROCEDURALLY (see EnsureLampPrefab / EnsureLampSprite)
        //   exactly like NightLightPoolDriver builds its glow at runtime.  A
        //   later wave can wire real refs via SetRefs with zero code change.
        [SerializeField] private GameObject lampPrefab;
        [SerializeField] private Sprite lampSprite;
        private GameObject _lampPrefabRuntime;  // cached lazily-built template
        private Sprite     _lampSpriteRuntime;  // cached lazily-built sprite
        // W-M4-05 (#21) - Stone floor cost.  Wood floor = 목재 1; stone floor is
        //   paid from STONE (like WallStone).  석재 1 keeps it reachable / cheap.
        [SerializeField] private int floorStoneCost = 3;   // 밸런스 A6(2026-07-24): 1→3 파리티
        // W-M4-05 (#21) - Stone-floor prefab + sprite, built LAZILY + PROCEDURALLY
        //   exactly like the Lamp entry (the Lane B contract forbids a SceneSetup
        //   edit, so these stay null and BuildManager self-builds them on first
        //   use; a later wave can wire real refs via SetRefs with zero code change).
        [SerializeField] private GameObject floorStonePrefab;
        [SerializeField] private Sprite floorStoneSprite;
        private GameObject _floorStonePrefabRuntime;  // cached lazily-built template
        private Sprite     _floorStoneSpriteRuntime;  // cached lazily-built sprite
        // W-M4-06 (#20) - Table+chair cost.  A simple wood dining spot; the reference sim
        //   table ~ 25 wood + stool ~ small — 목재 6 keeps it cheap/reachable in
        //   the prototype while clearly costing more than a lamp (4).
        [SerializeField] private int tableChairCost = 65;   // 밸런스 A6(2026-07-24): 40→65 파리티
        // W-M4-06 (#20) - Table+chair prefab + sprite, built LAZILY + PROCEDURALLY
        //   exactly like the Lamp / StoneFloor entries (the Lane A contract forbids
        //   a SceneSetup edit, so these stay null and BuildManager self-builds them
        //   on first use; a later wave can wire real refs via SetRefs with zero
        //   code change).
        [SerializeField] private GameObject tableChairPrefab;
        [SerializeField] private Sprite tableChairSprite;
        private GameObject _tableChairPrefabRuntime;  // cached lazily-built template
        private Sprite     _tableChairSpriteRuntime;  // cached lazily-built sprite
        // W-M6-02 (B3) - Fence cost.  the reference sim fence ~= 1 wood/segment; this
        //   prototype keeps the cheapest Structure-tab staple at 목재 1.  The
        //   gate variant uses the same cost (a fence opening, not a costlier door).
        [SerializeField] private int fenceCost = 1;
        // W-M6-02 (B3) - Fence + FenceGate prefab + sprite, built LAZILY +
        //   PROCEDURALLY exactly like the Lamp / StoneFloor / TableChair entries
        //   (the Lane contract forbids a SceneSetup edit, so these stay null and
        //   BuildManager self-builds them on first use; a later wave can wire real
        //   refs via SetRefs with zero code change).  Fence and gate are separate
        //   sprites/templates but share the cost + place path.
        [SerializeField] private GameObject fencePrefab;
        [SerializeField] private Sprite fenceSprite;
        [SerializeField] private GameObject fenceGatePrefab;
        [SerializeField] private Sprite fenceGateSprite;
        private GameObject _fencePrefabRuntime;       // cached lazily-built fence template
        private Sprite     _fenceSpriteRuntime;       // cached lazily-built fence sprite
        private GameObject _fenceGatePrefabRuntime;   // cached lazily-built gate template
        private Sprite     _fenceGateSpriteRuntime;   // cached lazily-built gate sprite
        // W-M6-03 (B4) - Barricade cost.  the reference sim sandbags cost a few Stuff; this
        //   prototype keeps the cheapest Security-tab staple at 목재 5 (clearly
        //   costing more than a fence's 1, less than a stone wall's 5-stone).
        [SerializeField] private int barricadeCost = 5;
        // W-M6-03 (B4) - Barricade prefab + sprite, built LAZILY + PROCEDURALLY
        //   exactly like the Lamp / StoneFloor / TableChair / Fence entries (the
        //   Lane contract forbids a SceneSetup edit, so these stay null and
        //   BuildManager self-builds them on first use; a later wave can wire real
        //   refs via SetRefs with zero code change).
        [SerializeField] private GameObject barricadePrefab;
        [SerializeField] private Sprite barricadeSprite;
        private GameObject _barricadePrefabRuntime;   // cached lazily-built barricade template
        private Sprite     _barricadeSpriteRuntime;   // cached lazily-built barricade sprite
        // W-M6-04 (B5) - Autodoor cost + faster-cross multiplier.  Vanilla
        //   the reference sim's autodoor costs more Stuff than a plain door for a faster
        //   open; this prototype keeps a plain door at 목재 3 and the autodoor at
        //   목재 6 (clearly costlier).  AutodoorPassMul 0.95 ~ barely slows the
        //   crossing pawn, vs the plain door's DoorEntity.DefaultPassMul 0.65 —
        //   so a pawn crosses an autodoor FASTER (wiki B5 acceptance).  Prefab +
        //   sprite are built LAZILY + PROCEDURALLY (same as Lamp / StoneFloor /
        //   TableChair / Fence / Barricade) so the Lane needs NO SceneSetup edit;
        //   a later wave can wire real refs via SetRefs with zero code change.
        [SerializeField] private int   autodoorCost    = 40;   // 밸런스 A3(2026-07-24): 6→40 (문 25 보다 비싸야 정상)
        [SerializeField] private float autodoorPassMul = 0.95f;
        [SerializeField] private GameObject autodoorPrefab;
        [SerializeField] private Sprite     autodoorSprite;
        private GameObject _autodoorPrefabRuntime;    // cached lazily-built autodoor template
        private Sprite     _autodoorSpriteRuntime;    // cached lazily-built autodoor sprite (tinted door)
        private GameObject _gravePrefabRuntime;       // GDD G3 — cached lazily-built grave template
        private Sprite     _graveSpriteRuntime;       // GDD G3 — Resources 로드 or 절차 폴백
        // #154 - bed quality 별 cost (wiki: sleeping spot 0 / wood bed 8 / fine 30).
        //  Fine 은 wiki 가 비싸지만 (60+) 프로토타입에선 30 으로 낮춰 reachable.
        [SerializeField] private int bedSleepingSpotCost = 0;
        [SerializeField] private int bedFineCost = 100;   // #3 파리티 — 로얄급
        [SerializeField] private SpriteRenderer ghostRenderer;
        [SerializeField] private Sprite wallSprite, floorSprite, doorSprite, stoveSprite, bedSprite;

        private Camera cam;

        public void SetRefs(GameObject wall, GameObject floor, GameObject door, GameObject stove,
                            Sprite wallS, Sprite floorS, Sprite doorS, Sprite stoveS,
                            SpriteRenderer ghost,
                            GameObject bed = null, Sprite bedS = null)
        {
            wallPrefab = wall; floorPrefab = floor; doorPrefab = door; stovePrefab = stove;
            wallSprite = wallS; floorSprite = floorS; doorSprite = doorS; stoveSprite = stoveS;
            ghostRenderer = ghost;
            bedPrefab = bed; bedSprite = bedS;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            cam = Camera.main;
        }

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            if (Input.GetKeyDown(KeyCode.B)) SetMode(CurrentMode == Mode.Wall  ? Mode.Off : Mode.Wall);
            if (Input.GetKeyDown(KeyCode.F)) SetMode(CurrentMode == Mode.Floor ? Mode.Off : Mode.Floor);
            if (Input.GetKeyDown(KeyCode.G)) SetMode(CurrentMode == Mode.Door  ? Mode.Off : Mode.Door);
            if (Input.GetKeyDown(KeyCode.T)) SetMode(CurrentMode == Mode.Stove ? Mode.Off : Mode.Stove);
            // 운영자 피드백 - 침대 추가
            if (Input.GetKeyDown(KeyCode.Y)) SetMode(CurrentMode == Mode.Bed ? Mode.Off : Mode.Bed);
            // W-M4-04 (#19) - L = 램프/횃불 (free hotkey; B/F/G/T/Y/N/R/X/M/P taken).
            if (Input.GetKeyDown(KeyCode.L)) SetMode(CurrentMode == Mode.Lamp ? Mode.Off : Mode.Lamp);
            // W-M4-05 (#21) - K = 석재 바닥 (free hotkey; B/F/G/T/Y/N/R/X/M/P/L taken).
            if (Input.GetKeyDown(KeyCode.K)) SetMode(CurrentMode == Mode.FloorStone ? Mode.Off : Mode.FloorStone);
            // W-M4-06 (#20) - J = 탁자+의자 (free hotkey; B/F/G/T/Y/N/R/X/M/P/L/K taken).
            if (Input.GetKeyDown(KeyCode.J)) SetMode(CurrentMode == Mode.TableChair ? Mode.Off : Mode.TableChair);
            // W-M6-02 (B3) - E = 울타리 (free hotkey; avoids B/F/G/T/Y/N/R/X/M/P/K/J/H/L
            //   and WASD camera A/D/S/W).  The fence-GATE variant is surfaced via the
            //   Architect menu ONLY (no hotkey) to keep the gate off the contended
            //   key space while still being one-click buildable.
            if (Input.GetKeyDown(KeyCode.E)) SetMode(CurrentMode == Mode.Fence ? Mode.Off : Mode.Fence);
            if (BuildModeActive && SimInput.GetMouseButtonDown(1)) { SetMode(Mode.Off); return; }   // SimInput — 규칙 2
            // #ui백로그 4.6 — ESC 빌드 취소: 지정 모드 7종(벌목/채광/해체 등)은 전부
            //  우클릭+ESC 양쪽인데 빌드만 우클릭 전용이라 비대칭이었다 (SettingsMenu 주석이
            //  가정하던 'ESC=build-cancel' 바인딩의 실체화).  SettingsMenu 의 ESC 는 isOpen
            //  일 때만 소비하므로 충돌 없음.
            if (BuildModeActive && SimInput.GetKeyDown(KeyCode.Escape)) { SetMode(Mode.Off); return; }   // SimInput — 규칙 2
            // 운영자 fb (2026-07-27) "침대같은거 돌리는거 안되는거" — 장르 표준대로 R = 회전.
            //  R 은 평소 징집 토글(ClickSelector)이지만 건축 모드 중엔 비어 있으므로 그때만 가로챈다
            //  (ClickSelector 쪽에 !BuildModeActive 가드를 둬서 중복 발화를 막는다).
            //  1x1 은 회전해도 같으므로 무시 — 침대/고급침대/무덤(1x2)만 의미가 있다.
            if (BuildModeActive && SimInput.GetKeyDown(KeyCode.R))
            {
                Vector2Int baseSz = SizeFor(CurrentMode);
                if (baseSz.x != baseSz.y) GhostRotated = !GhostRotated;
            }
            UpdateGhost();
            // #179/#182 - click 처리.  cooldown 으로 SetMode 직후 race 방지.
            //  EventSystem guard 는 over-blocking 위험 (TopBar/GuiControlBar 가 항상 raycast target).
            //  대신 PlaceCooldownSec (0.15s) 으로 메뉴 버튼 click → SetMode → 즉시 TryPlace race 차단.
            // #QA플레이 (2026-06-11): 배치 클릭이 raw Input 이라 하네스 worldclick 이
            //  무시됐다 (휴먼라이크 플레이테스트가 건축 불가 — _playtest-first10min 발굴).
            //  ClickSelector 와 동일하게 SimInput 패스스루 — 규칙 2 (같은 레이어 검증).
            if (BuildModeActive && SimInput.GetMouseButtonDown(0))
            {
                HandleLeftClickAt(SimInput.mousePosition, checkOverUI: true);
            }
        }

        /// <summary>
        /// #191 - real Input chain 과 시뮬레이션이 동일 path 공유.
        ///   Update 의 raw input → cooldown → overUI → TryPlace 시퀀스 그대로 재현.
        ///   BuildClickAutoQA v2 가 ExecuteEvents 로 button click 시뮬 후 이 메서드로 map click 시뮬.
        /// </summary>
        // #ui백로그 4.7 — 클릭 지점의 UI 가 '상호작용 요소(Selectable 부모 체인)'인지 판정.
        //  버튼/토글 위 클릭 = 의도적 조작이므로 실패 토스트를 띄우지 않는다.
        private static readonly System.Collections.Generic.List<RaycastResult> _uiHits =
            new System.Collections.Generic.List<RaycastResult>(8);
        private static bool PointerOverSelectable(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            var ped = new PointerEventData(EventSystem.current) { position = screenPos };
            _uiHits.Clear();
            EventSystem.current.RaycastAll(ped, _uiHits);
            for (int i = 0; i < _uiHits.Count; i++)
            {
                var t = _uiHits[i].gameObject != null ? _uiHits[i].gameObject.transform : null;
                int depth = 0;
                while (t != null && depth++ < 6)
                {
                    if (t.GetComponent<UnityEngine.UI.Selectable>() != null) return true;
                    t = t.parent;
                }
            }
            return false;
        }

        public BuildClickResult HandleLeftClickAt(Vector2 screenPos, bool checkOverUI)
        {
            if (!BuildModeActive) return BuildClickResult.ModeOff;
            float since = Time.unscaledTime - setModeTime;
            if (since < PlaceCooldownSec)
            {
                Debug.Log($"[Build] CLICK skip: cooldown {since:F2}s < {PlaceCooldownSec}s (mode just set)");
                return BuildClickResult.Cooldown;
            }
            if (checkOverUI)
            {
                // 2026-07-30 — `EventSystem.IsPointerOverGameObject()` (인자 없음)은
                //  **실제 OS 마우스 위치**를 본다.  주입 입력(-repro)에서는 그 위치가
                //  클릭 지점과 무관하므로, 창 위 아무 UI에나 커서가 얹혀 있으면 모든 배치가
                //  조용히 무시된다.  `p2-first10min` 이 청사진 0개였고 로그에는 화면 정중앙
                //  (960,540) 클릭조차 `overUI=true` 로 찍혔다 — 커서가 다른 곳에 있었던 것이다.
                //  "개별 실행은 PASS, 게이트는 FAIL" 이 재현되던 이유이기도 하다(커서 위치에
                //  따라 결과가 갈렸다).
                //  프로젝트 규약(WORKFLOW-V2: 입력은 SimInput 경유)대로 SimInput 을 쓴다 —
                //  평시엔 동일하게 실제 포인터를, 주입 시엔 **주입된 좌표**를 검사한다.
                //  ClickSelector 등 다른 입력 경로는 이미 이 규약을 따르고 있었고 여기만 빠져 있었다.
                bool overUI = SimInput.IsPointerOverUI();
                if (overUI)
                {
                    Vector3 mwLog = (cam != null) ? cam.ScreenToWorldPoint(screenPos) : Vector3.zero;
                    Debug.Log($"[Build] CLICK skip: overUI=true at screen={screenPos} world=({mwLog.x:F1},{mwLog.y:F1})");
                    // #ui백로그 4.7 — 의도적 UI 조작(버튼 등 Selectable)엔 실패 토스트 생략:
                    //  #190 의 목적은 '보이지 않는 차단자' 진단이었는데 속도/탭/메뉴 버튼
                    //  클릭마다 빨간 스팸이 됐다.  Selectable 아닌 순수 그래픽에 막혔을 때만
                    //  토스트 — 진단 가치(투명 차단자 탐지)는 보존.
                    if (BuildClickToast.Instance != null && !PointerOverSelectable(screenPos))
                        BuildClickToast.Instance.ShowFail("화면 UI 위를 눌렀습니다 - 맵에 직접 클릭하세요");
                    return BuildClickResult.OverUI;
                }
            }
            if (cam == null) cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[Build] CLICK skip: camera null"); return BuildClickResult.NoCamera; }
            Vector3 mw = cam.ScreenToWorldPoint(screenPos);
            int cx = Mathf.FloorToInt(mw.x);
            int cy = Mathf.FloorToInt(mw.y);
            Debug.Log($"[Build] CLICK at screen={screenPos} world=({mw.x:F1},{mw.y:F1}) cell=({cx},{cy})");
            bool placed = DoTryPlaceAt(cx, cy);
            return placed ? BuildClickResult.Placed : BuildClickResult.PlaceFailed;
        }

        /// <summary>#191 - QA / test 용 직접 진입점.  cooldown / overUI 무시하지 않음 (진짜 path).</summary>
        public BuildClickResult SimulateMapClick(Vector2 screenPos) => HandleLeftClickAt(screenPos, checkOverUI: false);

        public void SetMode(Mode m)
        {
            CurrentMode = m;
            setModeTime = Time.unscaledTime;  // #182 cooldown reset
            if (ghostRenderer == null) return;
            if (m == Mode.Off) { ghostRenderer.enabled = false; return; }
            ghostRenderer.enabled = true;
            ghostRenderer.sprite = SpriteForMode(m);
            // 바닥류(목재/석재)는 ghost sortingOrder 1 (지면 위), 나머지 구조물은 20.
            ghostRenderer.sortingOrder = (m == Mode.Floor || m == Mode.FloorStone) ? 1 : 20;
        }

        // #255 ArchitectMenu 아이콘이 플레이어 빌드에서도 실제 sprite 를 쓰도록 런타임 접근 공개
        //  (이전 ArchitectMenu 는 AssetDatabase=에디터전용 으로 로드해 플레이어에선 회색 swatch 뿐).
        public Sprite SpriteForMode(Mode m) => m switch
        {
            Mode.Wall  => wallSprite,
            Mode.Floor => floorSprite,
            Mode.Door  => doorSprite,
            Mode.Stove => stoveSprite,
            Mode.Bed   => bedSprite,
            Mode.BedSleepingSpot => EnsureBedVariantSprite(Mode.BedSleepingSpot),   // #6
            Mode.BedFine => EnsureBedVariantSprite(Mode.BedFine),                   // #6
            Mode.Lamp  => EnsureLampSprite(),
            // #ui백로그 4.4 — WallStone 케이스 부재로 목재 벽 폴백 → '회색 틴트 목재
            //  스프라이트' 머디브라운 회귀 (#255 가 경로 바꾸며 재발).  회색 석재로.
            Mode.WallStone => EnsureFloorStoneSprite(),
            Mode.FloorStone => EnsureFloorStoneSprite(),
            Mode.TableChair => EnsureTableChairSprite(),
            Mode.Fence => EnsureFenceSprite(),
            Mode.FenceGate => EnsureFenceGateSprite(),
            Mode.Barricade => EnsureBarricadeSprite(),
            Mode.Autodoor  => EnsureAutodoorSprite(),
            Mode.ResearchBench => EnsureBenchSprite(),
            Mode.Grave => EnsureGraveSprite(),            // GDD G3
            _ => wallSprite,
        };

        /// <summary>#ui백로그 4.5 — 메뉴/툴팁이 실비용을 읽을 수 있게 public (이중 정의
        ///  드리프트 방지의 1단계.  ArchitectMenu 라벨 합성은 후속).</summary>
        public int LiveCostFor(Mode m) => CostFor(m);

        private int CostFor(Mode m) => m switch
        {
            Mode.Wall            => wallCost,
            Mode.WallStone       => wallStoneCost,
            Mode.Floor           => floorCost,
            Mode.Door            => doorCost,
            Mode.Stove           => stoveCost,
            Mode.ResearchBench   => benchCost,            // #229 회귀 수리 - 목재 15
            Mode.Bed             => bedCost,
            Mode.BedSleepingSpot => bedSleepingSpotCost,  // #154 - 0
            Mode.BedFine         => bedFineCost,          // #154 - 30
            Mode.Lamp            => lampCost,             // W-M4-04 #19 - 목재 4
            Mode.FloorStone      => floorStoneCost,       // W-M4-05 #21 - 석재 1
            Mode.TableChair      => tableChairCost,       // W-M4-06 #20 - 목재 6
            Mode.Fence           => fenceCost,            // W-M6-02 B3 - 목재 1
            Mode.FenceGate       => fenceCost,            // W-M6-02 B3 - 목재 1 (same as fence)
            Mode.Barricade       => barricadeCost,        // W-M6-03 B4 - 목재 5
            Mode.Autodoor        => autodoorCost,         // W-M6-04 B5 - 목재 6 (> 문 3)
            _ => 0,
        };

        // #버그헌트2(2026-06-04): 완성 구조물 스폰 단일 경로 — 빌드 완료(BlueprintEntity)와 save
        //  재구성(GameSaveButtons.OnLoad)이 공용으로 호출.  prefab Instantiate + 활성화 + footprint
        //  scale + 벽 재질/침대 품질(mode 기준) + StructureTag(mode) 스탬프.  두 경로가 같은 함수를
        //  타므로 '빌드 결과 == 로드 재구성 결과' 가 보장된다(드리프트 없음).
        public GameObject SpawnFinished(Mode mode, Vector3 pos, bool rotated = false)
        {
            var prefab = PrefabFor(mode);
            if (prefab == null) return null;
            // 회전 배치(R) = transform 90°.  **스케일 계산은 건드리지 않는다** —
            //  아래 localScale 은 회전 전 footprint 기준으로 로컬 크기를 맞추고, 90° 회전이
            //  월드 가로/세로를 알아서 교환한다 (로컬 (1,2) → 월드 (2,1)).
            //  콜라이더도 같은 transform 을 타므로 함께 돌아간다.
            bool rot = rotated && SizeFor(mode).x != SizeFor(mode).y;
            var spawned = Instantiate(prefab, pos, rot ? Quaternion.Euler(0, 0, 90f) : Quaternion.identity);
            spawned.SetActive(true);
            spawned.hideFlags = HideFlags.None;   // #248 런타임 템플릿의 HideAndDontSave 상속 제거
            Vector2Int fp = SizeFor(mode);
            var sr = spawned.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Vector2 sw = sr.sprite.bounds.size;
                if (sw.x > 0.01f && sw.y > 0.01f)
                    spawned.transform.localScale = new Vector3(fp.x / sw.x, fp.y / sw.y, 1f);
            }
            if (mode == Mode.Wall || mode == Mode.WallStone)
            {
                var w = spawned.GetComponent<WallEntity>();
                if (w != null) w.SetMaterial(mode == Mode.WallStone ? WallMaterial.Stone : WallMaterial.Wood);
            }
            // 벽·문이 서면 방이 닫혔을 수 있다 → 밀폐 판정을 예약한다 (레퍼런스 자동 지붕).
            //  SpawnFinished 는 건축 완료와 세이브 로드 재구성이 **함께** 타는 단일 통로라,
            //  여기 한 곳만 걸면 두 경로가 자동으로 같은 결과를 낸다.
            //  판정 자체는 RoofDesignation 이 0.4초 코얼레싱해서 돌린다(로드 시 벽 폭주 방지).
            if (mode == Mode.Wall || mode == Mode.WallStone
                || mode == Mode.Door || mode == Mode.Autodoor)
            {
                RoofDesignation.Instance?.NotifyWallBuilt(
                    new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)));
            }
            if (mode == Mode.Bed || mode == Mode.BedSleepingSpot || mode == Mode.BedFine)
            {
                var b = spawned.GetComponent<BedEntity>();
                if (b != null) b.SetQuality(mode switch
                {
                    Mode.BedSleepingSpot => BedQuality.SleepingSpot,
                    Mode.BedFine         => BedQuality.Fine,
                    _                    => BedQuality.Wood,
                });
                // 운영자 피드백 #6 — 완성품도 등급별 전용 시트 (한 시트 공유라 혼동되던 것).
                var bsr = spawned.GetComponent<SpriteRenderer>();
                var vSpr = EnsureBedVariantSprite(mode);
                if (bsr != null && vSpr != null)
                {
                    bsr.sprite = vSpr;
                    Vector2 sw2 = vSpr.bounds.size;
                    Vector2Int fp2 = SizeFor(mode);
                    if (sw2.x > 0.01f && sw2.y > 0.01f)
                        spawned.transform.localScale = new Vector3(fp2.x / sw2.x, fp2.y / sw2.y, 1f);
                }
            }
            var tag = spawned.GetComponent<StructureTag>();
            if (tag == null) tag = spawned.AddComponent<StructureTag>();
            tag.modeInt = (int)mode;
            tag.rotated = rot;
            return spawned;
        }

        private GameObject PrefabFor(Mode m) => m switch
        {
            Mode.Wall            => wallPrefab,
            Mode.WallStone       => wallPrefab,  // 같은 prefab, 다른 자원
            Mode.Floor           => floorPrefab,
            Mode.Door            => doorPrefab,
            Mode.Stove           => stovePrefab,
            Mode.ResearchBench   => EnsureBenchPrefab(),  // #229 회귀 수리
            Mode.Bed             => bedPrefab,
            Mode.BedSleepingSpot => bedPrefab,  // #154 - 같은 prefab, quality 다름
            Mode.BedFine         => bedPrefab,  // #154
            Mode.Lamp            => EnsureLampPrefab(),  // W-M4-04 #19
            Mode.FloorStone      => EnsureFloorStonePrefab(),  // W-M4-05 #21
            Mode.TableChair      => EnsureTableChairPrefab(),  // W-M4-06 #20
            Mode.Fence           => EnsureFencePrefab(),       // W-M6-02 B3
            Mode.FenceGate       => EnsureFenceGatePrefab(),   // W-M6-02 B3
            Mode.Barricade       => EnsureBarricadePrefab(),   // W-M6-03 B4
            Mode.Autodoor        => EnsureAutodoorPrefab(),    // W-M6-04 B5
            Mode.Grave           => EnsureGravePrefab(),       // GDD G3
            _ => null,
        };

        // #193 - vanilla colony-sim 침대 1x2 칸.  multi-cell entity footprint 인프라.
        //  Bed/BedFine: 1x2.  나머지: 1x1.  anchor = bottom-left cell (cx, cy).
        public static Vector2Int SizeFor(Mode m) => m switch
        {
            Mode.Bed             => new Vector2Int(1, 2),
            Mode.BedFine         => new Vector2Int(1, 2),
            Mode.Grave           => new Vector2Int(1, 2),   // GDD G3 — 침대와 동일 1×2
            _ => new Vector2Int(1, 1),
        };

        /// <summary>고스트/배치가 실제로 쓰는 크기 — 회전 상태를 반영한다 (R 키).
        ///  SizeFor 는 "이 물건의 원래 크기"라는 순수 함수로 남겨두고, 회전은 여기서만 얹는다.
        ///  이 구분이 없으면 저장/로드·비용 계산 등 SizeFor 를 쓰는 다른 경로가 회전에 오염된다.</summary>
        public static bool GhostRotated { get; private set; }

        public static Vector2Int EffectiveSizeFor(Mode m)
        {
            Vector2Int s = SizeFor(m);
            return (GhostRotated && s.x != s.y) ? new Vector2Int(s.y, s.x) : s;
        }

        private void UpdateGhost()
        {
            if (!BuildModeActive || ghostRenderer == null || cam == null) return;
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            int cx = Mathf.FloorToInt(mw.x);
            int cy = Mathf.FloorToInt(mw.y);
            // #193 - multi-cell ghost: center = (cx + w*0.5, cy + h*0.5)
            //  중심 계산엔 **회전 반영 크기**를 쓴다 — 실제로 점유할 칸 위에 얹혀야 하므로.
            Vector2Int size = EffectiveSizeFor(CurrentMode);
            ghostRenderer.transform.position = new Vector3(cx + size.x * 0.5f, cy + size.y * 0.5f, 0);
            // 회전은 transform 으로, 스케일은 **회전 전 크기**로 (90° 가 월드 가로/세로를 교환한다).
            Vector2Int baseSize = SizeFor(CurrentMode);
            bool ghostRot = GhostRotated && baseSize.x != baseSize.y;
            ghostRenderer.transform.rotation = ghostRot ? Quaternion.Euler(0, 0, 90f) : Quaternion.identity;
            if (ghostRenderer.sprite != null)
            {
                Vector2 sw = ghostRenderer.sprite.bounds.size;
                if (sw.x > 0.01f && sw.y > 0.01f)
                    ghostRenderer.transform.localScale = new Vector3(baseSize.x / sw.x, baseSize.y / sw.y, 1f);
            }
            int cost = CostFor(CurrentMode);
            bool stoneMode = PaysWithStone(CurrentMode);
            // #60 콜로니심식: 청사진은 바닥 자원(WoodPile/StoneChunk)을 hauler 가 운반해 짓는다.
            //  따라서 affordability(고스트 색)는 카운터뿐 아니라 *바닥에 깔린 자재*까지 합산해야
            //  실제 건축 가능성을 반영한다.  과거엔 카운터(stockpile)만 봐서, 시작 시 목재 300이
            //  바닥에 있어도 목재:0 → 고스트 빨강 → "못 짓는다" 오해(실제론 배치+운반 건축 가능).
            bool canAfford = AvailableForBuild(stoneMode) >= cost;
            // #199 C3 - ghost 색: terrain(물/바위) or 점유 시 빨강 (the reference sim red ghost).
            bool areaFree = ValidatePlacement(cx, cy, size.x, size.y) == PlaceReject.None;
            ghostRenderer.color = (canAfford && areaFree)
                ? new Color(1f, 1f, 1f, 0.55f)
                : new Color(1f, 0.4f, 0.4f, 0.55f);
        }

        /// <summary>
        /// 청사진이 덮는 칸의 **제거 가능한 장애물**(나무·광맥)에 자동으로 작업을 건다.
        ///
        /// 2026-07-30 운영자: "건물을 지을때 바위나 나무가 있으면 그걸 자동제거하거나
        /// 절차상 제거하는 기능이 있어야 하는데 그게 없음."
        ///
        /// 이전 동작의 두 문제:
        ///   · 광맥 → 배치 자체를 거부.  플레이어가 채광 지정 → 대기 → 다시 건축을
        ///     손으로 해야 했다.  사람의 플레이 흐름이 아니다.
        ///   · 나무 → 배치는 되지만 완성 순간 **자재 없이 조용히 사라졌다**.
        ///     사람이라면 베어서 목재를 얻는다.
        ///
        /// 이제 둘 다 그 자리에서 벌목/채광으로 지정된다 — 콜로니스트가 치우고,
        /// 자재는 정상적으로 들어오며, 건설은 그 뒤에 이어진다.
        /// </summary>
        private void AutoClearObstacles(int cx, int cy, int w, int h)
        {
            int trees = 0, veins = 0;
            for (int dx = 0; dx < w; dx++)
            {
                for (int dy = 0; dy < h; dy++)
                {
                    var center = new Vector2(cx + dx + 0.5f, cy + dy + 0.5f);
                    var hits = Physics2D.OverlapBoxAll(center, Vector2.one * 0.4f, 0f);
                    foreach (var hit in hits)
                    {
                        if (hit == null) continue;
                        if (hit.GetComponent<TreeEntity>() != null
                            && TreeChopDesignation.Instance != null)
                        {
                            if (TreeChopDesignation.Instance.MarkWorld(center) != null) trees++;
                        }
                        else if (hit.GetComponent<StoneVeinEntity>() != null
                                 && MineDesignation.Instance != null)
                        {
                            // MarkCell 은 private — 1칸짜리 드래그 사각형이 공개 진입점이다.
                            if (MineDesignation.Instance.SimulateDragRect(center, center) > 0) veins++;
                        }
                    }
                }
            }
            // ── 구역 겹침 해소 (2026-07-31 운영자: "오브젝트나 지역이나 건물이 안 겹치게,
            //    어쩔 수 없이 겹치면 이사가는 방식도 구현해야함") ──────────────────
            //  실측: 데모 영상에서 새 벽 3칸이 **시작 저장구역 위에** 그대로 세워졌다.
            //  구역(저장·경작)은 콜라이더가 없어 위 OverlapBox 에 안 걸리고, CellOccupied
            //  도 보지 않으므로 건물과 구역이 같은 칸에 태연히 공존했다 — 화면에서는
            //  노란 구역 위에 벽이 얹힌 모양이라 "겹쳐 지었다"로 읽힌다.
            //
            //  처리 방식은 **구역이 비켜난다**.  건물이 우선이고 구역은 면적이 줄어드는
            //  것으로 물러난다 — 사람이 창고 자리에 헛간을 지으면 창고를 옮기지, 헛간을
            //  포기하지 않는다.  칸 단위로 빼므로 구역 전체가 사라지지 않고 모양만 바뀐다.
            //  (나무·바위와 같은 자리에서 처리한다 — '지을 자리를 먼저 치운다'는 한 규칙.)
            int zoneCells = 0;
            for (int dx = 0; dx < w; dx++)
            {
                for (int dy = 0; dy < h; dy++)
                {
                    var cell = new Vector2Int(cx + dx, cy + dy);
                    if (StockpileDesignation.Instance != null
                        && StockpileDesignation.Instance.EraseCell(cell)) zoneCells++;
                    if (GrowZoneDesignation.Instance != null
                        && GrowZoneDesignation.Instance.EraseCell(cell)) zoneCells++;
                }
            }
            if (zoneCells > 0)
            {
                Debug.Log($"[Build] 구역 {zoneCells}칸 비켜남 @ ({cx},{cy}) {w}x{h}");
                BuildClickToast.Instance?.ShowSuccess($"구역 {zoneCells}칸을 비켜 놓았습니다");
            }

            if (trees > 0 || veins > 0)
            {
                Debug.Log($"[Build] 자동 정리 지정: 나무 {trees} · 광맥 {veins} @ ({cx},{cy}) {w}x{h}");
                if (BuildClickToast.Instance != null)
                {
                    string what = trees > 0 && veins > 0 ? $"나무 {trees}·바위 {veins}"
                                : trees > 0 ? $"나무 {trees}그루" : $"바위 {veins}개";
                    BuildClickToast.Instance.ShowSuccess($"{what} 먼저 치웁니다");
                }
            }
        }

        private bool CellOccupied(int cx, int cy)
        {
            // tile center 기준 검사 (+0.5)
            var hits = Physics2D.OverlapBoxAll(new Vector2(cx + 0.5f, cy + 0.5f), Vector2.one * 0.4f, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                if (h.GetComponent<WallEntity>() != null) return true;
                if (h.GetComponent<DoorEntity>() != null) return true;
                // #249 운영자 fb "나무 위에 건축 안됨" — the reference sim 에선 나무/식물 위에 청사진을
                //  놓을 수 있고(건설 시작 시 식물 제거), 나무가 배치를 막지 않는다.  TreeEntity 를
                //  점유물에서 제외 → 나무 위 배치 허용.  완성 시 겹친 나무는 BlueprintEntity.Complete
                //  가 제거(자재는 안 줌 — 단순 클리어).
                // (was: if (h.GetComponent<TreeEntity>() != null) return true;)
                // #199 C3 - genre fidelity: a pawn STANDING on the cell does NOT
                //  block placement.  In the reference sim you can drop a blueprint under a
                //  colonist; the pawn simply walks off (the blueprint only needs the
                //  cell clear of *structures/terrain*, not transient occupants).  So
                //  PawnEntity (and animals, which carry no structural footprint) are
                //  intentionally NOT treated as occupancy here.  Removing this also
                //  de-fragiles Build Click QA (settlement cells often have pawns).
                // 2026-07-30 운영자 — "건물을 지을때 바위나 나무가 있으면 그걸 자동제거하거나
                //  절차상 제거하는 기능이 있어야 하는데 그게 없음."  맞는 지적이다.
                //  레퍼런스 콜로니심은 **제거 가능한 것 위에는 청사진을 놓을 수 있고**, 놓는
                //  순간 그 대상에 제거 작업(벌목/채광)이 자동으로 걸린다.  건설은 치워진 뒤
                //  진행된다.  플레이어가 "먼저 채광 지정 → 기다림 → 다시 건축"을 손으로
                //  하게 만드는 것은 사람의 플레이 흐름이 아니다.
                //
                //  그래서 **제거 가능한 장애물은 점유물에서 뺀다** (아래 AutoClearObstacles 가
                //  배치 직후 작업을 건다).  치울 수 없는 것(벽·문·완성 가구·다른 청사진)만
                //  점유로 남는다.
                //  이번 회차 범위는 운영자가 지목한 **나무와 바위(광맥)** 로 한정한다.
                //  열매덤불은 작고 피하기 쉬워 점유로 남겨 둔다 (범위를 넓힐수록 회귀 위험).
                //  (was: StoneVeinEntity → return true)
                if (h.GetComponent<BerryBushEntity>() != null) return true;
                if (h.GetComponent<StoveEntity>() != null) return true;
                if (h.GetComponent<ResearchBench>() != null) return true;  // #229 회귀 수리
                if (h.GetComponent<LampEntity>() != null) return true;   // W-M4-04 #19
                if (h.GetComponent<TableEntity>() != null) return true;  // W-M4-06 #20
                if (h.GetComponent<BarricadeEntity>() != null) return true;  // W-M6-03 B4 - impassable, can't stack
                if (h.GetComponent<BedEntity>() != null) return true;
                if (h.GetComponent<BlueprintEntity>() != null) return true;  // #118
                // 운영자 (2026-06-12 오후) "돌이랑 침대가 겹쳐" — 광맥이 점유 검사에
                //  빠져 광맥 셀 위에 침대/구조물을 지을 수 있었다.
                //  2026-07-30 정정: 그때의 문제는 "겹쳐 보인다"였지 "놓을 수 있다"가 아니다.
                //  이제 광맥 위 배치를 허용하되 AutoClearObstacles 가 **채광을 자동 지정**하고,
                //  BlueprintEntity 는 광맥이 남아 있는 동안 완성되지 않는다 → 겹쳐 보이는
                //  일이 없으면서 플레이어는 손으로 채광을 먼저 시킬 필요가 없다.
            }
            return false;
        }

        /// <summary>
        /// #199 C3 - the reference sim placement rule: a blueprint may NOT sit on impassable
        /// TERRAIN (water / rock).  Deliberately terrain-ONLY (not walls): we reuse
        /// PawnMovement.IsBlockedAt — the exact raw-tilemap Water/Rock guard the pawn
        /// movement core uses — so "can a pawn step here" and "can I build here" share
        /// one terrain definition.  Walls/blueprints are handled separately by
        /// CellOccupied so each rejection keeps an accurate reason (and so a wall cell
        /// reads "영역 점유됨", not "물/바위").  A null tilemap (pure unit-test scene)
        /// → not blocked, so isolated scenes with no terrain don't false-reject.
        /// </summary>
        private bool TerrainBlocked(int cx, int cy)
            => PawnMovement.IsBlockedAt(new Vector2(cx + 0.5f, cy + 0.5f));

        /// <summary>#193 - multi-cell footprint occupy 검사.  anchor=(cx,cy), w x h 영역 전부 free 여야 false.</summary>
        private bool AreaOccupied(int cx, int cy, int w, int h)
        {
            for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++)
                    if (CellOccupied(cx + dx, cy + dy)) return true;
            return false;
        }

        // #199 C3 - placement-validation result for the footprint.  Distinguishes
        //  the rejection reasons so the toast can tell the player WHY (the reference sim
        //  shows a red ghost + reason string).
        private enum PlaceReject { None, Terrain, Occupied, OutOfBounds }

        /// <summary>
        /// #199 C3 - validate EVERY covered cell of a w×h footprint at anchor
        /// (cx,cy) BEFORE creating a blueprint, colony-sim-style:
        ///   - reject if ANY cell is impassable terrain (Water/Rock) → PlaceReject.Terrain;
        ///   - reject if ANY cell already holds a structure/blueprint → PlaceReject.Occupied.
        /// Terrain is checked first so a water cell reads "물 위엔 못 지음" rather than
        /// a generic occupied message.  All cells validated (multi-cell beds/benches).
        /// Returns PlaceReject.None when the whole footprint is buildable.
        /// </summary>
        private PlaceReject ValidatePlacement(int cx, int cy, int w, int h)
        {
            // #버그헌트3(2026-06-04): 맵 경계(±44, pawn 도달 범위) 밖 셀 거부.  이전엔 bounds 검사가
            //  없어 맵 밖 void(카메라가 ±50 까지 pan 가능)에 청사진을 놓을 수 있었고, pawn 은 ±44
            //  로 clamp 되어 자재 운반이 영영 불가 → 영구 미완성 청사진(stuck build order)이 됐다.
            for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++)
                {
                    float wx = cx + dx + 0.5f, wy = cy + dy + 0.5f;
                    if (wx < PawnMovement.WORLD_MIN.x || wx > PawnMovement.WORLD_MAX.x ||
                        wy < PawnMovement.WORLD_MIN.y || wy > PawnMovement.WORLD_MAX.y)
                        return PlaceReject.OutOfBounds;
                }
            for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++)
                    if (TerrainBlocked(cx + dx, cy + dy)) return PlaceReject.Terrain;
            for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++)
                    if (CellOccupied(cx + dx, cy + dy)) return PlaceReject.Occupied;
            return PlaceReject.None;
        }

        /// <summary>#179 - test harness 호출용 (Input.mousePosition 우회).
        ///  실제 cell (cx, cy) 에 청사진 placement 시도.  성공 시 true.</summary>
        public bool TryPlaceAt(int cx, int cy)
        {
            return DoTryPlaceAt(cx, cy);
        }

        private void TryPlace()
        {
            if (cam == null) { Debug.LogWarning("[Build] TryPlace skip: camera null"); return; }
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            int cx = Mathf.FloorToInt(mw.x);
            int cy = Mathf.FloorToInt(mw.y);
            DoTryPlaceAt(cx, cy);
        }

        // #60 건축 가용 자재 = stockpile 카운터 + 바닥 자재(WoodPile/StoneChunk).  hauler 가 바닥
        //  자재를 청사진까지 운반하므로 둘 다 실제 건축에 쓰인다.  per-frame FindObjects 비용을
        //  피하려 0.5s 캐시(고스트는 build 모드 중에만 갱신되므로 충분히 신선).
        private float _availCacheTime = -1f;
        private int _availWood, _availStone;
        private int AvailableForBuild(bool stone)
        {
            if (Time.unscaledTime - _availCacheTime >= 0.5f)
            {
                _availCacheTime = Time.unscaledTime;
                int w = ResourceManager.Instance != null ? ResourceManager.Instance.wood : 0;
                foreach (var p in Object.FindObjectsByType<WoodPileEntity>(FindObjectsSortMode.None))
                    if (p != null) w += p.Wood;
                int s = ResourceManager.Instance != null ? ResourceManager.Instance.stone : 0;
                foreach (var c in Object.FindObjectsByType<StoneChunkEntity>(FindObjectsSortMode.None))
                    if (c != null) s += c.Stone;
                _availWood = w; _availStone = s;
            }
            return stone ? _availStone : _availWood;
        }

        private bool DoTryPlaceAt(int cx, int cy)
        {
            // #179 - silent return 4개를 명시 log 로 진단 가능하게.
            // #190 - 각 실패 path 에 BuildClickToast 추가 (운영자가 화면에서 바로 원인 확인).
            var prefab = PrefabFor(CurrentMode);
            if (prefab == null)
            {
                Debug.LogWarning($"[Build] TryPlace skip: prefab null for mode={CurrentMode}");
                if (BuildClickToast.Instance != null) BuildClickToast.Instance.ShowFail("아직 지을 수 없는 건물입니다");
                return false;
            }
            int cost = CostFor(CurrentMode);
            // #193 - multi-cell entity (침대 1x2 등) footprint 검사
            // #199 C3 - the reference sim 배치 규칙: terrain(물/바위) + 구조물/청사진 중복 모두 거부.
            //  pawn 이 서 있는 cell 은 거부 X (pawn 이 비켜남).  multi-cell 은 전 cell 검사.
            //  점유 검사는 **회전 반영 크기** 기준 (가로 침대는 가로 2칸을 먹는다).
            Vector2Int size = EffectiveSizeFor(CurrentMode);
            bool placeRotated = GhostRotated && SizeFor(CurrentMode).x != SizeFor(CurrentMode).y;
            PlaceReject reject = ValidatePlacement(cx, cy, size.x, size.y);
            if (reject == PlaceReject.OutOfBounds)
            {
                Debug.Log($"[Build] TryPlace skip: out of map bounds ({cx},{cy}) {size.x}x{size.y}");
                if (BuildClickToast.Instance != null) BuildClickToast.Instance.ShowFail("지도 밖에는 지을 수 없습니다");
                return false;
            }
            if (reject == PlaceReject.Terrain)
            {
                Debug.Log($"[Build] TryPlace skip: terrain (water/rock) at ({cx},{cy}) {size.x}x{size.y} for mode={CurrentMode}");
                if (BuildClickToast.Instance != null) BuildClickToast.Instance.ShowFail("물이나 바위 위에는 지을 수 없습니다");
                return false;
            }
            if (reject == PlaceReject.Occupied)
            {
                Debug.Log($"[Build] TryPlace skip: area ({cx},{cy}) {size.x}x{size.y} occupied for mode={CurrentMode}");
                if (BuildClickToast.Instance != null) BuildClickToast.Instance.ShowFail("자리가 이미 차 있습니다 — 빈 곳에 지어 보세요");
                return false;
            }
            // #189 - 운영자 fb "건축 여전히 안 됨" root cause:
            //   이전: ResourceManager null 이면 return false → 운영자 click 시
            //   silent fail.  하지만 #142 이후 blueprint spawn 시 자원 차감 X
            //   (hauler 가 운반 후 차감), 그러니 ResourceManager 가 null 이어도
            //   BlueprintEntity 생성은 OK.  check 제거 → 청사진 정상 spawn.
            //   ResourceManager null 자체는 다른 시스템 문제로 별도 진단.
            if (ResourceManager.Instance == null)
            {
                Debug.LogWarning("[Build] WARNING: ResourceManager.Instance null - blueprint 는 spawn 진행 (hauler 가 자재 운반)");
            }
            // 운영자 fb v4 - 레퍼런스 콜로니심 정상 흐름: 청사진 spawn 시 자원 차감 X.
            //  hauler 가 자재를 청사진 위치까지 운반 후 PawnBuilder 가 건설 작업.
            // W-M4-05 (#21) - 석재로 짓는 모드(석재 벽 / 석재 바닥)는 stone 으로 결제.
            bool stoneMode = PaysWithStone(CurrentMode);
            int needWood = stoneMode ? 0 : cost;
            int needStone = stoneMode ? cost : 0;
            Debug.Log($"[Build] TryPlace OK: mode={CurrentMode} → blueprint at ({cx+0.5f}, {cy+0.5f}), need wood={needWood} stone={needStone}");

            // 2026-07-30 — 청사진이 덮는 칸의 제거 가능한 장애물(나무·광맥·덤불)에
            //  자동으로 작업을 건다.  이게 없으면 플레이어가 "채광 지정 → 대기 → 다시 건축"을
            //  손으로 반복해야 하고, 나무는 완성 순간 **자재 없이 조용히 사라졌다**
            //  (사람이라면 베어서 목재를 얻는다).
            AutoClearObstacles(cx, cy, size.x, size.y);

            Sprite ghostSpr = SpriteForCurrentMode();
            var bpGo = new GameObject($"Blueprint_{CurrentMode}");
            // #193 - multi-cell entity center = (cx + w*0.5, cy + h*0.5).  1x1 이면 (+0.5,+0.5) = 기존.
            Vector3 center = new Vector3(cx + size.x * 0.5f, cy + size.y * 0.5f, 0);
            bpGo.transform.position = center;
            var bp = bpGo.AddComponent<BlueprintEntity>();
            // GDD G3: 무덤 = 자재 0·노동 8초 (맨땅 파기).  바닥 2초, 나머지 5초.
            float secs = CurrentMode == Mode.Grave ? 8f
                       : (CurrentMode == Mode.Floor || CurrentMode == Mode.FloorStone) ? 2f : 5f;
            bp.Init(CurrentMode, prefab, ghostSpr, needWood, needStone, secs);
            // #193 - 청사진 sprite 도 1x2 비율 적용.  스케일 인자는 **회전 전 크기**여야 한다
            //  (90° 회전이 월드 가로/세로를 교환하므로) — size 는 이미 회전 반영본이라 쓰면 안 된다.
            bp.SetSize(SizeFor(CurrentMode), placeRotated);

            // 실외 침대 경고 (2026-07-24 운영자 "침대를 집 밖에" — 방 개념이 없어 게임이
            //  아무 안내도 안 주던 것).  배치는 허용하되(콜로니심式 자유) 페널티를 즉시 고지.
            if ((CurrentMode == Mode.Bed || CurrentMode == Mode.BedFine
                 || CurrentMode == Mode.BedSleepingSpot) && BuildClickToast.Instance != null)
            {
                var rd = RoofDesignation.Instance;
                if (rd != null && !rd.IsRoofed(new Vector2Int(cx, cy)))
                    BuildClickToast.Instance.ShowFail("! 실외 침대 — 지붕이 없으면 '한데서 잠 -4' 불이익");
            }

            // #자원모델 단일화(2026-06-04, 운영자 "haul-required 순수 the reference sim" 선택): 청사진은
            //  자재 없이 빈 상태로 놓이고, 림이 물리 목재/석재 더미(벌목·채광·stockpile)를 현장으로
            //  운반(PawnHauler→DepositWood)해야 PawnBuilder 가 건설한다.  이전 #242 의 '카운터 즉시
            //  결제 + funding' 블록을 제거 — 그 블록은 시작 자원이 카운터였던 시절의 임시방편으로,
            //  (a) 동일 목재가 카운터 결제 + 물리 더미 운반으로 이중 지불되던 #3 dupe 의 원인이었고
            //  (b) L518 주석의 원래 의도('청사진 spawn 시 자원 차감 X, hauler 운반 후 건설')와 모순됐다.
            //  현재 시작 자원은 물리 더미(GameManager: 목재 50 + 식량)라 haul-funding 으로 정상 건설된다.
            //  비용 0 모드(수면자리 등)는 needWood/needStone=0 이라 자동으로 즉시 완비된다.
            // #190 - 클릭 성공 토스트 + 시각 ring (운영자가 "어디에 청사진 생겼지?" 즉시 확인)
            if (BuildClickToast.Instance != null)
            {
                string kr = ModeKr(CurrentMode);
                string sizeKr = (size.x == 1 && size.y == 1) ? "" : $" {size.x}x{size.y}";
                BuildClickToast.Instance.ShowSuccess($"○ 청사진 - {kr}{sizeKr} @ ({cx},{cy})");
            }
            ClickEffect.Spawn(center, new Color(0.55f, 0.85f, 1.0f, 0.95f));
            return true;
        }

        /// <summary>ModeBanner 용 — 현재 건설 모드 한글명.</summary>
        public string CurrentModeKr() => ModeKr(CurrentMode);

        private static string ModeKr(Mode m) => m switch
        {
            Mode.Wall            => "벽(목재)",
            Mode.WallStone       => "벽(석재)",
            Mode.Floor           => "바닥",
            Mode.Door            => "문",
            Mode.Stove           => "화덕",
            Mode.ResearchBench   => "연구대",
            Mode.Bed             => "목재 침대",
            Mode.BedSleepingSpot => "수면 자리",
            Mode.BedFine         => "고급 침대",
            Mode.Lamp            => "램프",   // W-M4-04 #19
            Mode.FloorStone      => "석재 바닥",  // W-M4-05 #21
            Mode.TableChair      => "탁자+의자",  // W-M4-06 #20
            Mode.Fence           => "울타리",     // W-M6-02 B3
            Mode.FenceGate       => "울타리 문",  // W-M6-02 B3
            Mode.Barricade       => "바리케이드",  // W-M6-03 B4
            Mode.Autodoor        => "자동문",      // W-M6-04 B5
            Mode.Grave           => "무덤",        // GDD G3
            _ => m.ToString(),
        };

        // W-M4-05 (#21) - 석재로 결제하는 모드 (석재 벽 / 석재 바닥).  나머지는 목재.
        private static bool PaysWithStone(Mode m) => m == Mode.WallStone || m == Mode.FloorStone;

        private Sprite SpriteForCurrentMode() => CurrentMode switch
        {
            Mode.Wall  => wallSprite,
            Mode.Floor => floorSprite,
            Mode.Door  => doorSprite,
            Mode.Stove => stoveSprite,
            Mode.Bed   => bedSprite,
            Mode.BedSleepingSpot => EnsureBedVariantSprite(Mode.BedSleepingSpot),   // #6
            Mode.BedFine         => EnsureBedVariantSprite(Mode.BedFine),           // #6
            Mode.Lamp  => EnsureLampSprite(),   // W-M4-04 #19
            Mode.WallStone => EnsureFloorStoneSprite(),   // #4.4 — 고스트/청사진도 회색
            Mode.FloorStone => EnsureFloorStoneSprite(),  // W-M4-05 #21
            Mode.TableChair => EnsureTableChairSprite(),  // W-M4-06 #20
            Mode.Fence => EnsureFenceSprite(),            // W-M6-02 B3
            Mode.FenceGate => EnsureFenceGateSprite(),    // W-M6-02 B3
            Mode.Barricade => EnsureBarricadeSprite(),    // W-M6-03 B4
            Mode.Autodoor  => EnsureAutodoorSprite(),     // W-M6-04 B5
            Mode.ResearchBench => EnsureBenchSprite(),    // #229 회귀 수리
            Mode.Grave => EnsureGraveSprite(),            // GDD G3
            _ => wallSprite,
        };

        // ---------------------------------------------------------------- //
        //  W-M4-04 (#19) - Lamp prefab + sprite, built LAZILY in code.       //
        //                                                                    //
        //  The Lane A contract forbids a SceneSetup edit, so the lamp's      //
        //  finished prefab + sprite cannot be wired via SetRefs the way the  //
        //  other buildables are.  Instead BuildManager constructs them once  //
        //  on first use:                                                     //
        //    - the sprite is loaded from Assets/Sprites/lamp.png in the      //
        //      Editor / batchmode via AssetDatabase (reflection — no Editor  //
        //      assembly reference from a runtime script), and ALWAYS falls   //
        //      back to a procedural lamp texture so a PLAYER BUILD (where    //
        //      AssetDatabase is absent and the PNG is outside Resources/)    //
        //      still shows a real lamp instead of a magenta/null sprite —    //
        //      the exact trap Lane B is fixing for scatter.                  //
        //    - the prefab is an in-memory template GameObject carrying a     //
        //      SpriteRenderer + BoxCollider2D + LampEntity, mirroring the    //
        //      SceneSetup Stove template.  BlueprintEntity.Complete will     //
        //      Instantiate it on build completion.                           //
        //                                                                    //
        //  >>> QA FLAG: lamp uses BuildManager's lazy procedural prefab/     //
        //      sprite path — NO SceneSetup prefab wiring was added.  If a    //
        //      future wave prefers a real prefab asset, wire lampPrefab /    //
        //      lampSprite via SetRefs and these lazy builders become no-ops. //
        // ---------------------------------------------------------------- //

        private Sprite EnsureLampSprite()
        {
            if (lampSprite != null) return lampSprite;        // wired via SetRefs (future)
            if (_lampSpriteRuntime != null) return _lampSpriteRuntime;
            _lampSpriteRuntime = LoadOrBuildLampSprite();
            return _lampSpriteRuntime;
        }

        // #229 회귀 수리 — 연구대 buildable.  Lamp 의 Ensure* 패턴 복제: 에디터에선
        //  실시트(struct32_research_bench.png), 플레이어 빌드에선 Resources/struct32
        //  사본 폴백.  ResearchManager 가 1s 캐시로 신규 벤치를 자동 인지하므로
        //  배치 즉시 "연구자 없음 (작업대 근처 림)" → 림 접근 시 연구 진행.
        // 운영자 피드백 #6 (2026-06-12 AM) — 수면자리/목재/고급 침대가 한 스프라이트
        //  공유라 헷갈림.  모드별 전용 시트 (에디터 AssetDatabase / 플레이어 Resources 폴백).
        private Sprite _bedSpotSprite, _bedFineSprite;
        private Sprite EnsureBedVariantSprite(Mode m)
        {
            if (m == Mode.BedSleepingSpot)
            {
                if (_bedSpotSprite == null)
                {
#if UNITY_EDITOR
                    _bedSpotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_bed_spot.png");
#endif
                    if (_bedSpotSprite == null) _bedSpotSprite = Resources.Load<Sprite>("struct32/struct32_bed_spot");
                }
                return _bedSpotSprite != null ? _bedSpotSprite : bedSprite;
            }
            if (m == Mode.BedFine)
            {
                if (_bedFineSprite == null)
                {
#if UNITY_EDITOR
                    _bedFineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_bed_fine.png");
#endif
                    if (_bedFineSprite == null) _bedFineSprite = Resources.Load<Sprite>("struct32/struct32_bed_fine");
                }
                return _bedFineSprite != null ? _bedFineSprite : bedSprite;
            }
            return bedSprite;
        }

        private GameObject _benchPrefabRuntime;
        private Sprite _benchSpriteRuntime;

        private Sprite EnsureBenchSprite()
        {
            if (_benchSpriteRuntime != null) return _benchSpriteRuntime;
#if UNITY_EDITOR
            _benchSpriteRuntime = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Sprites/struct32_research_bench.png");
#endif
            if (_benchSpriteRuntime == null)
                _benchSpriteRuntime = Resources.Load<Sprite>("struct32/struct32_research_bench");
            return _benchSpriteRuntime;
        }

        private GameObject EnsureBenchPrefab()
        {
            if (_benchPrefabRuntime != null) return _benchPrefabRuntime;
            var bgo = new GameObject("ResearchBenchTemplate");
            bgo.hideFlags = HideFlags.HideAndDontSave;
            bgo.SetActive(false);
            var bsr = bgo.AddComponent<SpriteRenderer>();
            bsr.sprite       = EnsureBenchSprite();
            bsr.sortingOrder = 5;                              // Stove/Lamp 본체와 동일
            var bbox = bgo.AddComponent<BoxCollider2D>();
            bbox.size = Vector2.one;
            bgo.AddComponent<ResearchBench>();
            _benchPrefabRuntime = bgo;
            return _benchPrefabRuntime;
        }

        private GameObject EnsureLampPrefab()
        {
            if (lampPrefab != null) return lampPrefab;         // wired via SetRefs (future)
            if (_lampPrefabRuntime != null) return _lampPrefabRuntime;

            // In-memory template (NOT a saved .prefab asset — runtime only).
            //  Mirrors the SceneSetup Stove template: SR sortingOrder 5 +
            //  1×1 BoxCollider2D + the entity marker component.  Kept inactive
            //  and hidden so it is never seen in the scene; Instantiate copies it.
            var go = new GameObject("LampTemplate");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = EnsureLampSprite();
            sr.sortingOrder = 5;                               // same as Stove body
            var box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            go.AddComponent<LampEntity>();
            _lampPrefabRuntime = go;
            return _lampPrefabRuntime;
        }

        /// <summary>
        /// Load lamp.png via AssetDatabase (Editor/batchmode) through reflection
        /// so this runtime script needs no UnityEditor reference; ALWAYS falls
        /// back to a procedural lamp texture so a player build is never null.
        /// </summary>
        private static Sprite LoadOrBuildLampSprite()
        {
#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_lamp.png");
            if (sp != null) return sp;
#endif
            return BuildProceduralLampSprite();
        }

        /// <summary>
        /// Build a 16×16 lamp sprite in code, matching _gen_lamp.py's authored
        /// content (wood post + base, warm flame head) so it is visually
        /// interchangeable with lamp.png.  Guarantees a real lamp in a player
        /// build with zero asset-load dependency (same guard as
        /// NightLightPoolDriver's procedural glow).
        /// </summary>
        private static Sprite BuildProceduralLampSprite()
        {
            const int SIZE = 16;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = "Lamp_Proc";

            var px = new Color32[SIZE * SIZE];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            // Palette (mirrors palette.py; RGBA).
            var WOOD_DK   = new Color32(92, 60, 36, 255);
            var WOOD_MD   = new Color32(140, 92, 54, 255);
            var WOOD_LT   = new Color32(188, 138, 88, 255);
            var FIRE_OR   = new Color32(232, 120, 44, 255);
            var FIRE_LT   = new Color32(250, 196, 96, 255);
            var OUTLINE   = new Color32(40, 30, 22, 255);   // OUTLINE_OBJ

            // Texture y is bottom-up; _gen_lamp.py uses top-down PIL y.  Convert
            // each authored (gx, gyTop) to texture row: ty = SIZE-1 - gyTop.
            void Set(int gx, int gyTop, Color32 c)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return;
                px[ty * SIZE + gx] = c;
            }
            bool IsEmpty(int gx, int gyTop)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return false;
                return px[ty * SIZE + gx].a == 0;
            }
            bool IsWood(int gx, int gyTop)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return false;
                var c = px[ty * SIZE + gx];
                return (c.r == WOOD_DK.r && c.g == WOOD_DK.g && c.b == WOOD_DK.b)
                    || (c.r == WOOD_MD.r && c.g == WOOD_MD.g && c.b == WOOD_MD.b)
                    || (c.r == WOOD_LT.r && c.g == WOOD_LT.g && c.b == WOOD_LT.b);
            }

            // Flame head (rows 1-4) — un-outlined.
            foreach (var (x, y) in new (int, int)[] { (8,1),(6,2),(9,2),(6,3),(9,3),(6,4),(7,4),(8,4),(9,4) })
                Set(x, y, FIRE_OR);
            foreach (var (x, y) in new (int, int)[] { (7,1),(7,2),(8,2),(7,3),(8,3) })
                Set(x, y, FIRE_LT);

            // Wood post (rows 5-11) — alternating light/shadow read.
            for (int y = 5; y <= 11; y++)
            {
                Set(7, y, (y % 2 == 0) ? WOOD_LT : WOOD_MD);
                Set(8, y, (y % 2 == 0) ? WOOD_MD : WOOD_DK);
            }
            Set(6, 5, WOOD_MD);
            Set(9, 5, WOOD_DK);

            // Base / foot (rows 12-14).
            foreach (var (x, y) in new (int, int)[] { (6,12),(7,12),(8,12),(9,12),(6,13),(7,13),(8,13),(9,13) })
                Set(x, y, WOOD_MD);
            foreach (var (x, y) in new (int, int)[] { (5,13),(10,13),(5,14),(6,14),(7,14),(8,14),(9,14),(10,14) })
                Set(x, y, WOOD_DK);

            // 1px OUTLINE_OBJ on transparent pixels 4-adjacent to wood at y>=5
            //  (flame head left open).  Collect first, then write (no self-feed).
            var outlinePts = new System.Collections.Generic.List<(int, int)>();
            for (int y = 5; y < SIZE; y++)
            {
                for (int x = 0; x < SIZE; x++)
                {
                    if (!IsEmpty(x, y)) continue;
                    if (IsWood(x + 1, y) || IsWood(x - 1, y) || IsWood(x, y + 1) || IsWood(x, y - 1))
                        outlinePts.Add((x, y));
                }
            }
            foreach (var (x, y) in outlinePts) Set(x, y, OUTLINE);

            tex.SetPixels32(px);
            tex.Apply(updateMipmaps: false);

            return Sprite.Create(
                tex,
                new Rect(0, 0, SIZE, SIZE),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 16f);
        }

        // ---------------------------------------------------------------- //
        //  W-M4-05 (#21) - Stone-floor prefab + sprite, built LAZILY in code.//
        //                                                                    //
        //  Mirrors the Lamp lazy-build path EXACTLY (the Lane B contract     //
        //  forbids a SceneSetup edit, so the finished stone-floor prefab +   //
        //  sprite are self-built once on first use):                         //
        //    - the sprite loads from Assets/Sprites/stone_floor.png in the   //
        //      Editor/batchmode via AssetDatabase reflection, and ALWAYS     //
        //      falls back to a procedural paved-stone texture so a PLAYER    //
        //      BUILD (AssetDatabase absent, PNG outside Resources/) still    //
        //      shows a real stone floor instead of a null/magenta sprite.    //
        //    - the prefab is an in-memory template carrying a SpriteRenderer //
        //      at sortingOrder 1 (ground level, like the wood Floor) and a   //
        //      StoneFloorEntity marker — and NO collider, exactly like the   //
        //      wood FloorEntity (pawns walk over it).                        //
        //                                                                    //
        //  >>> QA FLAG: stone floor uses BuildManager's lazy procedural      //
        //      prefab/sprite path — NO SceneSetup prefab wiring was added.   //
        //      If a future wave prefers a real prefab asset, wire            //
        //      floorStonePrefab / floorStoneSprite via SetRefs and these     //
        //      lazy builders become no-ops.                                  //
        // ---------------------------------------------------------------- //

        private Sprite EnsureFloorStoneSprite()
        {
            if (floorStoneSprite != null) return floorStoneSprite;       // wired via SetRefs (future)
            if (_floorStoneSpriteRuntime != null) return _floorStoneSpriteRuntime;
            _floorStoneSpriteRuntime = LoadOrBuildFloorStoneSprite();
            return _floorStoneSpriteRuntime;
        }

        private GameObject EnsureFloorStonePrefab()
        {
            if (floorStonePrefab != null) return floorStonePrefab;        // wired via SetRefs (future)
            if (_floorStonePrefabRuntime != null) return _floorStonePrefabRuntime;

            // In-memory template (NOT a saved .prefab asset — runtime only).
            //  Mirrors the wood Floor: SR sortingOrder 1 (ground), NO collider
            //  (pawns walk over floors), StoneFloorEntity marker (a FloorEntity
            //  subclass → detected by every existing floor consumer, higher
            //  MoveBonus).  Hidden + inactive so it's never seen; Instantiate copies it.
            var go = new GameObject("StoneFloorTemplate");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = EnsureFloorStoneSprite();
            sr.sortingOrder = 1;                                // same as wood Floor ghost/tile
            go.AddComponent<StoneFloorEntity>();               // FloorEntity subclass, higher MoveBonus
            _floorStonePrefabRuntime = go;
            return _floorStonePrefabRuntime;
        }

        /// <summary>
        /// Load stone_floor.png via AssetDatabase (Editor/batchmode) through
        /// reflection-free #if so this runtime script needs no UnityEditor
        /// reference; ALWAYS falls back to a procedural paved-stone texture so a
        /// player build is never null (same guard as the lamp sprite).
        /// </summary>
        private static Sprite LoadOrBuildFloorStoneSprite()
        {
            // 아트 v2 (2026-06-11): 32px 석재 바닥 — Resources 라 플레이어 빌드에서도
            //  로드된다 (기존 에디터-only AssetDatabase 경로는 빌드에서 절차 회색 폴백).
            var v2 = Resources.Load<Sprite>("struct32/struct32_floor_stone");
            if (v2 != null) return v2;
#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/stone_floor.png");
            if (sp != null) return sp;
#endif
            return BuildProceduralStoneFloorSprite();
        }

        /// <summary>
        /// Build a 16×16 stone / paved-floor sprite in code, matching
        /// _gen_stone_floor.py's authored content (cut paving slabs on ROCK
        /// dark/mid/light with a 1px OBJ outline border, darker / harder read
        /// than the wood floor so the two are distinguishable).  Guarantees a
        /// real stone floor in a player build with zero asset-load dependency.
        /// </summary>
        private static Sprite BuildProceduralStoneFloorSprite()
        {
            const int SIZE = 16;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = "StoneFloor_Proc";

            // Palette (mirrors palette.py ROCK_* / OUTLINE_OBJ; RGBA).
            var ROCK_DK  = new Color32(66, 66, 74, 255);    // palette.py ROCK_DK
            var ROCK_MD  = new Color32(104, 104, 114, 255); // palette.py ROCK_MD
            var ROCK_LT  = new Color32(150, 150, 160, 255); // palette.py ROCK_LT
            var OUTLINE  = new Color32(40, 30, 22, 255);    // palette.py OUTLINE_OBJ

            var px = new Color32[SIZE * SIZE];

            // Texture y is bottom-up; _gen_stone_floor.py uses top-down PIL y.
            //  Convert each authored (gx, gyTop) to texture row: ty = SIZE-1-gyTop.
            void Set(int gx, int gyTop, Color32 c)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return;
                px[ty * SIZE + gx] = c;
            }

            // Fill the whole tile with mid rock (paved base).
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                    Set(x, y, ROCK_MD);

            // 2×2 cut-slab grid: a darker mortar groove at the mid lines (x=8, y=8)
            //  and at the tile border reads as four laid pavers.  Slight light
            //  bevel on the top/left of each paver for a hard, cut-stone read.
            for (int i = 0; i < SIZE; i++)
            {
                // mortar grooves (darker) — vertical x=7/8, horizontal y=7/8.
                Set(7, i, ROCK_DK); Set(8, i, ROCK_DK);
                Set(i, 7, ROCK_DK); Set(i, 8, ROCK_DK);
            }
            // light bevel (top + left edge of each of the four pavers).
            foreach (int oy in new[] { 0, 9 })
                for (int x = (oy == 0 ? 1 : 0); x < SIZE; x++)
                {
                    if (x == 7 || x == 8) continue;
                    Set(x, oy == 0 ? 1 : 9, ROCK_LT);
                }
            foreach (int ox in new[] { 0, 9 })
                for (int y = 1; y < SIZE; y++)
                {
                    if (y == 7 || y == 8) continue;
                    Set(ox == 0 ? 1 : 9, y, ROCK_LT);
                }

            // 1px OBJ outline border around the whole tile (hard edge — harder
            //  read than the wood floor, which is softer).
            for (int i = 0; i < SIZE; i++)
            {
                Set(i, 0, OUTLINE); Set(i, SIZE - 1, OUTLINE);
                Set(0, i, OUTLINE); Set(SIZE - 1, i, OUTLINE);
            }

            tex.SetPixels32(px);
            tex.Apply(updateMipmaps: false);

            return Sprite.Create(
                tex,
                new Rect(0, 0, SIZE, SIZE),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 16f);
        }

        // ---------------------------------------------------------------- //
        //  W-M4-06 (#20) - Table+chair prefab + sprite, built LAZILY in code.//
        //                                                                    //
        //  Mirrors the Lamp / StoneFloor lazy-build path EXACTLY (the Lane A //
        //  contract forbids a SceneSetup edit, so the finished table+chair    //
        //  prefab + sprite are self-built once on first use):                //
        //    - the sprite loads from Assets/Sprites/table_chair.png in the    //
        //      Editor/batchmode via AssetDatabase, and ALWAYS falls back to a //
        //      procedural table+chair texture so a PLAYER BUILD (AssetDatabase//
        //      absent, PNG outside Resources/) still shows a real piece       //
        //      instead of a null/magenta sprite — the trap Lane B fixed for   //
        //      scatter.                                                       //
        //    - the prefab is an in-memory template carrying a SpriteRenderer  //
        //      at sortingOrder 5 (furniture body, like the Stove) + a 1×1     //
        //      BoxCollider2D + BOTH a TableEntity and a ChairEntity marker so //
        //      the one build op produces a combined table+chair eat/rec spot. //
        //                                                                    //
        //  >>> QA FLAG: table+chair uses BuildManager's lazy procedural       //
        //      prefab/sprite path — NO SceneSetup prefab wiring was added.    //
        //      If a future wave prefers a real prefab asset, wire             //
        //      tableChairPrefab / tableChairSprite via SetRefs and these lazy //
        //      builders become no-ops.                                        //
        // ---------------------------------------------------------------- //

        private Sprite EnsureTableChairSprite()
        {
            if (tableChairSprite != null) return tableChairSprite;        // wired via SetRefs (future)
            if (_tableChairSpriteRuntime != null) return _tableChairSpriteRuntime;
            // 아트 B2 (2026-07-24): 절차 드로잉 128px 판 우선 (gen-ts-furniture.py,
            //  Resources 라 플레이어 빌드 포함) — 없으면 기존 절차 폴백.
            _tableChairSpriteRuntime = Resources.Load<Sprite>("Sprites/struct64_table_chair");
            if (_tableChairSpriteRuntime == null)
                _tableChairSpriteRuntime = LoadOrBuildTableChairSprite();
            return _tableChairSpriteRuntime;
        }

        private GameObject EnsureTableChairPrefab()
        {
            if (tableChairPrefab != null) return tableChairPrefab;         // wired via SetRefs (future)
            if (_tableChairPrefabRuntime != null) return _tableChairPrefabRuntime;

            // In-memory template (NOT a saved .prefab asset — runtime only).
            //  Mirrors the SceneSetup Stove template: SR sortingOrder 5 (furniture
            //  body) + 1×1 BoxCollider2D + the entity markers.  Carries BOTH a
            //  TableEntity (the eat-spot probe) and a ChairEntity (the comfort
            //  marker) so the single build op yields a combined table+chair.
            //  Hidden + inactive so it's never seen; Instantiate copies it.
            var go = new GameObject("TableChairTemplate");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = EnsureTableChairSprite();
            sr.sortingOrder = 5;                               // same as Stove body
            var box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            go.AddComponent<TableEntity>();                    // eat-spot probe + mood bonus
            go.AddComponent<ChairEntity>();                    // chair comfort marker
            _tableChairPrefabRuntime = go;
            return _tableChairPrefabRuntime;
        }

        /// <summary>
        /// Load table_chair.png via AssetDatabase (Editor/batchmode) so this
        /// runtime script needs no UnityEditor reference; ALWAYS falls back to a
        /// procedural table+chair texture so a player build is never null (same
        /// guard as the lamp / stone-floor sprites).
        /// </summary>
        private static Sprite LoadOrBuildTableChairSprite()
        {
#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/table_chair.png");
            if (sp != null) return sp;
#endif
            return BuildProceduralTableChairSprite();
        }

        /// <summary>
        /// Build a 16×16 table+chair sprite in code, matching _gen_table_chair.py's
        /// authored content (a low wood table slab on two legs with a small stool
        /// tucked to the left, 1px OBJ outline) so it is visually interchangeable
        /// with table_chair.png.  Guarantees a real piece in a player build with
        /// zero asset-load dependency (same guard as the lamp / stone-floor procs).
        /// </summary>
        private static Sprite BuildProceduralTableChairSprite()
        {
            const int SIZE = 16;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = "TableChair_Proc";

            var px = new Color32[SIZE * SIZE];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            // 밝고 대비 강한 목재 톤 — 평평한 넓은 상판 + 의자 실루엣을 또렷하게.
            //  펜스(성긴 격자)·바리케이드(흙더미)와 형태로 분리.
            var WOOD_DK = new Color32(110, 72, 42, 255);
            var WOOD_MD = new Color32(168, 116, 68, 255);
            var WOOD_LT = new Color32(212, 164, 108, 255);
            var OUTLINE = new Color32(28, 18, 12, 255);   // OUTLINE_OBJ (더 진하게)

            // Texture y is bottom-up; 권위 좌표는 top-down PIL y.
            //  Convert each authored (gx, gyTop) to texture row: ty = SIZE-1-gyTop.
            void Set(int gx, int gyTop, Color32 c)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return;
                px[ty * SIZE + gx] = c;
            }
            bool IsEmpty(int gx, int gyTop)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return false;
                return px[ty * SIZE + gx].a == 0;
            }
            bool IsWood(int gx, int gyTop)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return false;
                var c = px[ty * SIZE + gx];
                return (c.r == WOOD_DK.r && c.g == WOOD_DK.g && c.b == WOOD_DK.b)
                    || (c.r == WOOD_MD.r && c.g == WOOD_MD.g && c.b == WOOD_MD.b)
                    || (c.r == WOOD_LT.r && c.g == WOOD_LT.g && c.b == WOOD_LT.b);
            }

            // 넓고 평평한 상판(rows 3-5): 3px 두께로 두툼하게, x=3..14 가로로 길게.
            //  → "탁자"의 핵심 실루엣인 넓은 천판을 강조.
            for (int x = 3; x <= 14; x++) { Set(x, 3, WOOD_LT); Set(x, 4, WOOD_MD); Set(x, 5, WOOD_DK); }

            // 탁자 다리(rows 6-9) 좌우 끝 x=4,13 — 짧고 굵게(2px).
            for (int y = 6; y <= 9; y++)
            {
                Set(4,  y, (y % 2 == 0) ? WOOD_MD : WOOD_DK);
                Set(13, y, (y % 2 == 0) ? WOOD_MD : WOOD_DK);
            }

            // 의자 1개 — 하단에 또렷한 등받이+좌판 형태(점이 아닌 "의자 모양").
            //  좌판(rows 11-12, x=6..10) 위로 등받이(rows 8-10, x=10) 세로 한 줄.
            for (int x = 6; x <= 10; x++) { Set(x, 11, WOOD_LT); Set(x, 12, WOOD_MD); }  // 좌판
            for (int y = 8; y <= 10; y++) Set(10, y, WOOD_DK);                            // 등받이
            Set(6, 13, WOOD_DK); Set(10, 13, WOOD_DK);                                    // 의자 다리 2개

            // 1px OUTLINE_OBJ on transparent pixels 4-adjacent to wood.  Collect
            //  first, then write (no self-feed).
            var outlinePts = new System.Collections.Generic.List<(int, int)>();
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    if (!IsEmpty(x, y)) continue;
                    if (IsWood(x + 1, y) || IsWood(x - 1, y) || IsWood(x, y + 1) || IsWood(x, y - 1))
                        outlinePts.Add((x, y));
                }
            foreach (var (x, y) in outlinePts) Set(x, y, OUTLINE);

            tex.SetPixels32(px);
            tex.Apply(updateMipmaps: false);

            return Sprite.Create(
                tex,
                new Rect(0, 0, SIZE, SIZE),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 16f);
        }

        // ---------------------------------------------------------------- //
        //  W-M6-02 (B3) - Fence + FenceGate prefab + sprite, LAZILY in code. //
        //                                                                    //
        //  Mirrors the Lamp / StoneFloor / TableChair lazy-build path EXACTLY //
        //  (the Lane contract forbids a SceneSetup edit, so the finished      //
        //  fence/gate prefab + sprite are self-built once on first use):      //
        //    - the sprite loads from Assets/Sprites/fence.png /               //
        //      fence_gate.png in the Editor/batchmode via AssetDatabase, and  //
        //      ALWAYS falls back to a procedural texture so a PLAYER BUILD     //
        //      (AssetDatabase absent, PNG outside Resources/) still shows a    //
        //      real fence/gate instead of a null/magenta sprite.              //
        //    - the prefab is an in-memory template carrying a SpriteRenderer   //
        //      at sortingOrder 2 (LOW barrier — above floors @1, below         //
        //      furniture/walls @5+) and a FenceEntity marker, and CRUCIALLY    //
        //      NO collider — exactly like the wood FloorEntity — so the fence  //
        //      is PASSABLE (a pawn paths straight through; this is the B3      //
        //      acceptance: "blocks nothing").                                  //
        //                                                                    //
        //  >>> QA FLAG: fence + fence-gate use BuildManager's lazy procedural  //
        //      prefab/sprite path — NO SceneSetup prefab wiring was added.     //
        //      If a future wave prefers a real prefab asset, wire fencePrefab/ //
        //      fenceSprite (+ gate) via SetRefs and these lazy builders        //
        //      become no-ops.                                                  //
        // ---------------------------------------------------------------- //

        private Sprite EnsureFenceSprite()
        {
            if (fenceSprite != null) return fenceSprite;          // wired via SetRefs (future)
            if (_fenceSpriteRuntime != null) return _fenceSpriteRuntime;
            _fenceSpriteRuntime = LoadOrBuildFenceSprite("Assets/Sprites/struct32_fence.png", isGate: false);
            return _fenceSpriteRuntime;
        }

        private Sprite EnsureFenceGateSprite()
        {
            if (fenceGateSprite != null) return fenceGateSprite;  // wired via SetRefs (future)
            if (_fenceGateSpriteRuntime != null) return _fenceGateSpriteRuntime;
            _fenceGateSpriteRuntime = LoadOrBuildFenceSprite("Assets/Sprites/struct32_fence_gate.png", isGate: true);
            return _fenceGateSpriteRuntime;
        }

        private GameObject EnsureFencePrefab()
        {
            if (fencePrefab != null) return fencePrefab;          // wired via SetRefs (future)
            if (_fencePrefabRuntime != null) return _fencePrefabRuntime;
            _fencePrefabRuntime = BuildFenceTemplate("FenceTemplate", EnsureFenceSprite(), isGate: false);
            return _fencePrefabRuntime;
        }

        private GameObject EnsureFenceGatePrefab()
        {
            if (fenceGatePrefab != null) return fenceGatePrefab;  // wired via SetRefs (future)
            if (_fenceGatePrefabRuntime != null) return _fenceGatePrefabRuntime;
            _fenceGatePrefabRuntime = BuildFenceTemplate("FenceGateTemplate", EnsureFenceGateSprite(), isGate: true);
            return _fenceGatePrefabRuntime;
        }

        /// <summary>
        /// In-memory fence/gate template (NOT a saved .prefab — runtime only).
        /// SR sortingOrder 2 (LOW barrier — above floors, below furniture) and
        /// NO collider (like the wood Floor) so the fence is PASSABLE: a pawn
        /// walks straight through the cell.  Carries a FenceEntity marker with
        /// IsGate set.  Hidden + inactive so it's never seen; Instantiate copies it.
        /// </summary>
        private static GameObject BuildFenceTemplate(string name, Sprite spr, bool isGate)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = spr;
            sr.sortingOrder = 2;                               // LOW barrier read
            var fence = go.AddComponent<FenceEntity>();
            fence.IsGate = isGate;
            // Deliberately NO collider — fence blocks nothing (B3 acceptance).
            return go;
        }

        /// <summary>
        /// Load fence.png / fence_gate.png via AssetDatabase (Editor/batchmode)
        /// so this runtime script needs no UnityEditor reference; ALWAYS falls
        /// back to a procedural texture so a player build is never null (same
        /// guard as the lamp / stone-floor / table sprites).
        /// </summary>
        private static Sprite LoadOrBuildFenceSprite(string assetPath, bool isGate)
        {
#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sp != null) return sp;
#endif
            return BuildProceduralFenceSprite(isGate);
        }

        /// <summary>
        /// Build a 16x16 fence (or fence-gate) sprite in code, matching
        /// _gen_fence.py's authored content (a LOW horizontal wood rail on short
        /// posts, drawn in the lower half of the tile so it reads as a low
        /// barrier; the gate variant has a centre opening with two jamb posts).
        /// 1px OBJ outline.  Guarantees a real fence/gate in a player build with
        /// zero asset-load dependency.
        /// </summary>
        private static Sprite BuildProceduralFenceSprite(bool isGate)
        {
            const int SIZE = 16;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = isGate ? "FenceGate_Proc" : "Fence_Proc";

            var px = new Color32[SIZE * SIZE];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            // 밝은 목재 톤 + 대비 강한 외곽선 — 펜스는 "성긴 격자"로 읽혀야 하므로
            //  바리케이드(흙더미)·탁자(목재 덩어리)와 톤·실루엣을 모두 분리한다.
            var WOOD_DK = new Color32(120, 78, 44, 255);
            var WOOD_MD = new Color32(172, 120, 72, 255);
            var WOOD_LT = new Color32(214, 168, 112, 255);
            var METAL   = new Color32(176, 184, 196, 255);   // 게이트 경첩(철물) — 색으로 구분
            var OUTLINE = new Color32(28, 18, 12, 255);       // OUTLINE_OBJ (더 진하게)

            // Texture y is bottom-up; 권위 좌표는 top-down PIL y.
            //  Convert each authored (gx, gyTop) to texture row: ty = SIZE-1-gyTop.
            void Set(int gx, int gyTop, Color32 c)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return;
                px[ty * SIZE + gx] = c;
            }
            bool IsEmpty(int gx, int gyTop)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return false;
                return px[ty * SIZE + gx].a == 0;
            }
            bool IsWood(int gx, int gyTop)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return false;
                var c = px[ty * SIZE + gx];
                return (c.r == WOOD_DK.r && c.g == WOOD_DK.g && c.b == WOOD_DK.b)
                    || (c.r == WOOD_MD.r && c.g == WOOD_MD.g && c.b == WOOD_MD.b)
                    || (c.r == WOOD_LT.r && c.g == WOOD_LT.g && c.b == WOOD_LT.b)
                    || (c.r == METAL.r   && c.g == METAL.g   && c.b == METAL.b);
            }

            // 성긴 격자(piquet) 실루엣: 가는 1px 수직 기둥(키 큼, rows 2..14) +
            //  얇은 2개 수평 줄.  기둥 사이를 넓게 비워 "성긴 울타리"로 읽히게 하고
            //  바리케이드의 꽉 찬 더미와 확연히 다르게 한다.
            //  일반 펜스: 기둥 x=2,7,13 (3개) / 게이트: 좌우 문기둥 x=2,13만.
            int[] posts = isGate ? new[] { 2, 13 } : new[] { 2, 7, 13 };
            foreach (int px0 in posts)
            {
                // 가는 1px 수직 기둥, 위는 살짝 뾰족(밝게)하게 키 큰 말뚝 느낌.
                for (int y = 2; y <= 14; y++)
                    Set(px0, y, (y <= 4) ? WOOD_LT : ((y % 2 == 0) ? WOOD_MD : WOOD_DK));
            }
            // 수평 줄 2개(상단 row 5, 하단 row 10) — 1px 얇게, 점선 느낌으로 성기게.
            for (int x = 2; x <= 13; x++)
            {
                if (isGate && x >= 6 && x <= 9) continue;   // 게이트 중앙 개구부
                Set(x, 5,  (x % 2 == 0) ? WOOD_LT : WOOD_MD);   // 상단 줄(하이라이트)
                Set(x, 10, (x % 2 == 0) ? WOOD_MD : WOOD_DK);   // 하단 줄(그림자)
            }
            if (isGate)
            {
                // 게이트 전용 tell: 양쪽 문기둥 안쪽에 철물 경첩 점을 찍어
                //  "여닫는 문"임을 한눈에 보이게 한다(펜스에는 없음).
                Set(3,  6, METAL); Set(3,  9, METAL);   // 좌측 경첩
                Set(12, 6, METAL); Set(12, 9, METAL);   // 우측 경첩
            }

            // 1px OUTLINE_OBJ on transparent pixels 4-adjacent to wood.  Collect
            //  first, then write (no self-feed).
            var outlinePts = new System.Collections.Generic.List<(int, int)>();
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    if (!IsEmpty(x, y)) continue;
                    if (IsWood(x + 1, y) || IsWood(x - 1, y) || IsWood(x, y + 1) || IsWood(x, y - 1))
                        outlinePts.Add((x, y));
                }
            foreach (var (x, y) in outlinePts) Set(x, y, OUTLINE);

            tex.SetPixels32(px);
            tex.Apply(updateMipmaps: false);

            return Sprite.Create(
                tex,
                new Rect(0, 0, SIZE, SIZE),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 16f);
        }

        // ---------------------------------------------------------------- //
        //  W-M6-03 (B4) - Barricade prefab + sprite, built LAZILY in code.   //
        //                                                                    //
        //  Mirrors the Lamp / StoneFloor / TableChair / Fence lazy-build path //
        //  EXACTLY (the Lane contract forbids a SceneSetup edit, so the       //
        //  finished barricade prefab + sprite are self-built once on first    //
        //  use):                                                             //
        //    - the sprite loads from Assets/Sprites/barricade.png in the      //
        //      Editor/batchmode via AssetDatabase, and ALWAYS falls back to a //
        //      procedural sandbag texture so a PLAYER BUILD (AssetDatabase    //
        //      absent, PNG outside Resources/) still shows a real barricade   //
        //      instead of a null/magenta sprite.                             //
        //    - the prefab is an in-memory template carrying a SpriteRenderer   //
        //      at sortingOrder 2 (LOW barrier read — above floors @1, below   //
        //      furniture/walls @5+), a 1×1 BoxCollider2D (so CellOccupied's   //
        //      OverlapBox detects it → no stacking / no blueprint-on-top), and //
        //      a BarricadeEntity marker.  UNLIKE the passable Fence, the       //
        //      BarricadeEntity BLOCKS PATHING like a low wall: in its OWN      //
        //      Start it calls the EXISTING wall pathing-block API READ-ONLY    //
        //      (PawnMovement.RegisterWallCell) and releases it on destroy —    //
        //      so a pawn paths AROUND it (the B4 acceptance).  No new          //
        //      pathfinding is invented here.                                  //
        //                                                                    //
        //  NO cover-math (gated/over-scope): the barricade is visual + pathing //
        //  only.  A future cover wave can add accuracy math without touching   //
        //  this.                                                              //
        //                                                                    //
        //  >>> QA FLAG: barricade uses BuildManager's lazy procedural prefab/  //
        //      sprite path — NO SceneSetup prefab wiring was added.  If a      //
        //      future wave prefers a real prefab asset, wire barricadePrefab / //
        //      barricadeSprite via SetRefs and these lazy builders become      //
        //      no-ops.                                                        //
        // ---------------------------------------------------------------- //

        private Sprite EnsureBarricadeSprite()
        {
            if (barricadeSprite != null) return barricadeSprite;       // wired via SetRefs (future)
            if (_barricadeSpriteRuntime != null) return _barricadeSpriteRuntime;
            _barricadeSpriteRuntime = LoadOrBuildBarricadeSprite();
            return _barricadeSpriteRuntime;
        }

        private GameObject EnsureBarricadePrefab()
        {
            if (barricadePrefab != null) return barricadePrefab;        // wired via SetRefs (future)
            if (_barricadePrefabRuntime != null) return _barricadePrefabRuntime;

            // In-memory template (NOT a saved .prefab asset — runtime only).
            //  SR sortingOrder 2 (LOW barrier read, same as the fence) + a 1×1
            //  BoxCollider2D so CellOccupied's OverlapBox sees it (no stacking,
            //  no blueprint placed on top) + a BarricadeEntity marker.  The
            //  BarricadeEntity itself registers the PathGrid cell as blocked in
            //  Start (reusing the wall pathing-block API) so a pawn paths AROUND
            //  it.  Hidden + inactive so it's never seen; Instantiate copies it.
            var go = new GameObject("BarricadeTemplate");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = EnsureBarricadeSprite();
            sr.sortingOrder = 2;                               // LOW barrier read (same as fence)
            var box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            go.AddComponent<BarricadeEntity>();                // blocks pathing like a low wall
            _barricadePrefabRuntime = go;
            return _barricadePrefabRuntime;
        }

        // ============================ W-M6-04 (B5) Autodoor ===================== //

        private Sprite EnsureAutodoorSprite()
        {
            if (autodoorSprite != null) return autodoorSprite;          // wired via SetRefs (future)
            if (_autodoorSpriteRuntime != null) return _autodoorSpriteRuntime;
            _autodoorSpriteRuntime = LoadOrBuildAutodoorSprite();
            return _autodoorSpriteRuntime;
        }

        private GameObject EnsureAutodoorPrefab()
        {
            if (autodoorPrefab != null) return autodoorPrefab;          // wired via SetRefs (future)
            if (_autodoorPrefabRuntime != null) return _autodoorPrefabRuntime;

            // In-memory template (NOT a saved .prefab asset — runtime only), built
            //  the SAME lazy way as the Lamp / StoneFloor / TableChair / Fence /
            //  Barricade entries so the Lane needs ZERO SceneSetup edit.  An
            //  autodoor IS an AutodoorEntity : DoorEntity (so it inherits the
            //  trigger-collider pass-through, B9's NotifyPassing→PlayDoor SFX, the
            //  CellOccupied no-stack guard, and the EntityInspector/HoverTooltip/
            //  Deconstruct read paths that key off GetComponent<DoorEntity>()),
            //  and AutodoorEntity.Awake raises passMul to its higher value
            //  (~0.95 vs the plain door's DefaultPassMul 0.65) so a pawn crosses
            //  it FASTER.  Awake runs on every Instantiate'd copy, so the value
            //  needs no per-template SetPassMul here.  We also push BuildManager's
            //  serialized autodoorPassMul onto the template via SetPassMul so a
            //  designer override on the manager wins; AutodoorEntity.Awake would
            //  otherwise re-apply its own default — but since Instantiate copies
            //  the live component and Awake re-runs on the copy, the AutodoorEntity
            //  default is authoritative for placed doors (kept in sync at 0.95).
            //  SR sortingOrder 20 (structure read, above floors).  Hidden +
            //  inactive so the template is never seen; Instantiate copies it.
            var go = new GameObject("AutodoorTemplate");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = EnsureAutodoorSprite();
            sr.sortingOrder = 20;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            var door = go.AddComponent<AutodoorEntity>();
            door.SetPassMul(autodoorPassMul);   // template value; Awake re-asserts on Instantiate
            _autodoorPrefabRuntime = go;
            return _autodoorPrefabRuntime;
        }

        // ============================ GDD G3 무덤 ===================== //
        //  잉크 문법 스프라이트는 Resources/Sprites/grave64_empty.png (플레이어 빌드
        //  포함 보장 — ColorGrade 셰이더와 같은 이유).  없으면 절차 폴백.
        //  통행 가능(무덤 위 걷기 허용) — collider 는 isTrigger 로 CellOccupied
        //  no-stack 감지만 담당, PathGrid 등록 없음.

        private Sprite EnsureGraveSprite()
        {
            if (_graveSpriteRuntime != null) return _graveSpriteRuntime;
            _graveSpriteRuntime = Resources.Load<Sprite>("Sprites/grave64_empty");
            if (_graveSpriteRuntime == null) _graveSpriteRuntime = BuildGraveFallbackSprite();
            return _graveSpriteRuntime;
        }

        private static Sprite BuildGraveFallbackSprite()
        {
            const int W = 28, H = 56;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var px = new Color32[W * H];
            var soil = new Color32(96, 78, 58, 255);
            var dark = new Color32(52, 42, 32, 255);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[y * W + x] = (x == 0 || y == 0 || x == W - 1 || y == H - 1) ? dark : soil;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            var s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 32f,
                                  0, SpriteMeshType.FullRect);
            s.name = "GraveFallback";
            return s;
        }

        private GameObject EnsureGravePrefab()
        {
            if (_gravePrefabRuntime != null) return _gravePrefabRuntime;
            var go = new GameObject("GraveTemplate");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EnsureGraveSprite();
            sr.sortingOrder = 2;                    // LOW 지면 마커 (바닥 위·가구 아래)
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.9f, 1.9f);     // 1×2 풋프린트
            box.isTrigger = true;                   // no-stack 감지 전용 (물리 간섭 X)
            go.AddComponent<GraveEntity>();
            _gravePrefabRuntime = go;
            return _gravePrefabRuntime;
        }

        /// <summary>
        /// Load autodoor.png via AssetDatabase (Editor/batchmode) so this runtime
        /// script needs no UnityEditor reference; ALWAYS falls back to a procedural
        /// autodoor texture so a player build is never null/magenta (same guard as
        /// the lamp / stone-floor / table / fence / barricade sprites).
        /// </summary>
        private static Sprite LoadOrBuildAutodoorSprite()
        {
#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/autodoor.png");
            if (sp != null) return sp;
#endif
            return BuildProceduralAutodoorSprite();
        }

        /// <summary>
        /// Build a 16×16 autodoor sprite in code, matching _gen_autodoor.py's
        /// authored content: a plain wooden door panel reskinned with a teal/cyan
        /// AUTOMATION light strip down the centre split (so it reads as a powered
        /// door, distinct from the plain door) on WOOD tones with a 1px OBJ
        /// outline (flat-Kenney style).  Guarantees a real autodoor in a player
        /// build with zero asset-load dependency.
        /// </summary>
        private static Sprite BuildProceduralAutodoorSprite()
        {
            const int SIZE = 16;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = "Autodoor_Proc";

            var px = new Color32[SIZE * SIZE];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            // Palette (mirrors palette.py WOOD_* / OUTLINE_OBJ + a teal tech strip).
            var WOOD_DK   = new Color32(92, 60, 36, 255);
            var WOOD_MD   = new Color32(132, 92, 56, 255);
            var WOOD_LT   = new Color32(168, 124, 80, 255);
            var TECH_LT   = new Color32(120, 224, 220, 255);   // cyan automation glow
            var TECH_DK   = new Color32(48, 150, 152, 255);
            var OUTLINE   = new Color32(40, 30, 22, 255);       // OUTLINE_OBJ

            // Texture y is bottom-up; authored coords are top-down PIL y.
            void Set(int gx, int gyTop, Color32 c)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return;
                px[ty * SIZE + gx] = c;
            }

            // Door panel fills cols 2..13, rows 1..14 (1px transparent margin).
            for (int gy = 1; gy <= 14; gy++)
            {
                for (int gx = 2; gx <= 13; gx++)
                {
                    Color32 c;
                    if (gy == 1 || gy == 14 || gx == 2 || gx == 13) c = OUTLINE;      // 1px OBJ outline
                    else if (gx <= 4) c = WOOD_LT;                                    // left highlight
                    else if (gx >= 11) c = WOOD_DK;                                   // right shadow
                    else c = WOOD_MD;                                                 // body
                    Set(gx, gy, c);
                }
            }

            // Centre automation light strip (cols 7..8, rows 3..12) — the visual
            //  tell that this is a powered/auto door vs a plain wooden door.
            for (int gy = 3; gy <= 12; gy++)
            {
                Set(7, gy, (gy % 2 == 0) ? TECH_LT : TECH_DK);
                Set(8, gy, (gy % 2 == 0) ? TECH_DK : TECH_LT);
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            var spr = Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE),
                                    new Vector2(0.5f, 0.5f), SIZE);
            spr.name = "Autodoor_Proc";
            return spr;
        }

        /// <summary>
        /// Load barricade.png via AssetDatabase (Editor/batchmode) so this
        /// runtime script needs no UnityEditor reference; ALWAYS falls back to a
        /// procedural sandbag texture so a player build is never null (same guard
        /// as the lamp / stone-floor / table / fence sprites).
        /// </summary>
        private static Sprite LoadOrBuildBarricadeSprite()
        {
#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_barricade.png");
            if (sp != null) return sp;
#endif
            return BuildProceduralBarricadeSprite();
        }

        /// <summary>
        /// Build a 16×16 barricade sprite in code, matching _gen_barricade.py's
        /// authored content (two staggered rows of piled sandbags on muted DIRT
        /// tones with a WOOD_DK tie strap, drawn in the LOWER HALF so it reads as
        /// a low defensive line, 1px OBJ outline) so it is visually
        /// interchangeable with barricade.png.  Guarantees a real barricade in a
        /// player build with zero asset-load dependency.
        /// </summary>
        private static Sprite BuildProceduralBarricadeSprite()
        {
            const int SIZE = 16;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = "Barricade_Proc";

            var px = new Color32[SIZE * SIZE];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            // 바리케이드 팔레트: 채도 낮은 모래주머니(녹황색 흙)로 펜스의 따뜻한
            //  목재 톤과 색상부터 분리한다.  꽉 찬 비대칭 지그재그 더미 실루엣 →
            //  성긴 펜스 격자와 한눈에 구분.
            var DIRT_DK = new Color32(74, 70, 44, 255);
            var DIRT_MD = new Color32(120, 112, 70, 255);
            var DIRT_LT = new Color32(160, 152, 100, 255);
            var WOOD_DK = new Color32(70, 52, 32, 255);    // 끈/말뚝(짙은 갈색)
            var OUTLINE = new Color32(28, 24, 14, 255);     // OUTLINE_OBJ (더 진하게)

            // Texture y is bottom-up; 권위 좌표는 top-down PIL y.
            //  Convert each authored (gx, gyTop) to texture row: ty = SIZE-1-gyTop.
            void Set(int gx, int gyTop, Color32 c)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return;
                px[ty * SIZE + gx] = c;
            }
            bool IsEmpty(int gx, int gyTop)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return false;
                return px[ty * SIZE + gx].a == 0;
            }
            bool IsSolid(int gx, int gyTop)
            {
                int ty = SIZE - 1 - gyTop;
                if (gx < 0 || gx >= SIZE || ty < 0 || ty >= SIZE) return false;
                return px[ty * SIZE + gx].a != 0;
            }

            // 4-wide 통통한 모래주머니 한 덩어리(둥근 윗면 LT / 몸통 MD / 밑그림자 DK).
            void Sandbag(int cx, int yTop, int h)
            {
                for (int row = 0; row < h; row++)
                {
                    int y = yTop + row;
                    Color32 c = (row == 0) ? DIRT_LT : (row == h - 1 ? DIRT_DK : DIRT_MD);
                    for (int dx = -1; dx <= 2; dx++)
                    {
                        // 가운데 두 칸은 한 단계 밝게 → 통통한 입체감.
                        var cc = (dx == 0 || dx == 1) && row != 0 && row != h - 1 ? DIRT_LT : c;
                        Set(cx + dx, y, cc);
                    }
                }
            }

            // 지그재그 2단 더미 — 펜스의 일직선 격자와 정반대의 "비스듬히 쌓인 둔덕".
            //  뒷줄(아래·낮음)과 앞줄(살짝 위·높음)을 어긋나게 겹쳐 꽉 채운다.
            foreach (int cx in new[] { 2, 6, 10, 13 }) Sandbag(cx, 9, 4);   // 뒷줄(낮은 더미)
            foreach (int cx in new[] { 4, 8, 12 })     Sandbag(cx, 5, 5);   // 앞줄(높은 더미, 어긋남)

            // WOOD_DK 끈(tie strap) — 더미를 가로지르는 가는 2줄로 "묶음" 느낌.
            for (int x = 0; x < SIZE; x++)
            {
                if (IsSolid(x, 7))  Set(x, 7,  WOOD_DK);
                if (IsSolid(x, 11)) Set(x, 11, WOOD_DK);
            }

            // 1px OUTLINE_OBJ on transparent pixels 4-adjacent to a solid.
            //  Collect first, then write (no self-feed).
            var outlinePts = new System.Collections.Generic.List<(int, int)>();
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    if (!IsEmpty(x, y)) continue;
                    if (IsSolid(x + 1, y) || IsSolid(x - 1, y) || IsSolid(x, y + 1) || IsSolid(x, y - 1))
                        outlinePts.Add((x, y));
                }
            foreach (var (x, y) in outlinePts) Set(x, y, OUTLINE);

            tex.SetPixels32(px);
            tex.Apply(updateMipmaps: false);

            return Sprite.Create(
                tex,
                new Rect(0, 0, SIZE, SIZE),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 16f);
        }
    }

    public enum BuildClickResult { Placed, PlaceFailed, ModeOff, Cooldown, OverUI, NoCamera }
}
