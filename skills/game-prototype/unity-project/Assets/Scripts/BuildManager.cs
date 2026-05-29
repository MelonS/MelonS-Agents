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
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        public static BuildManager Instance { get; private set; }

        // #154 - bed quality 3종, #127 - stone wall.
        // W-M4-04 (#19) - Lamp: torch/standing-lamp buildable that emits a warm
        //   night light pool (drawn by LampGlowDriver).  Mirrors the Stove entry
        //   exactly (1×1 footprint, wood cost, same ghost/cooldown/place path).
        public enum Mode { Off, Wall, Floor, Door, Stove, Bed, WallStone, BedSleepingSpot, BedFine, Lamp }
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
        [SerializeField] private int wallCost = 5, floorCost = 1, doorCost = 3, stoveCost = 10, bedCost = 8;
        [SerializeField] private int wallStoneCost = 5;  // #127 - 석재 5
        // W-M4-04 (#19) - Lamp cost.  RimWorld torch ≈ 1 wood, standing lamp needs
        //   power; this prototype lamp is a cheap always-on light at 목재 4.
        [SerializeField] private int lampCost = 4;
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
        // #154 - bed quality 별 cost (wiki: sleeping spot 0 / wood bed 8 / fine 30).
        //  Fine 은 wiki 가 비싸지만 (60+) 프로토타입에선 30 으로 낮춰 reachable.
        [SerializeField] private int bedSleepingSpotCost = 0;
        [SerializeField] private int bedFineCost = 30;
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
            if (BuildModeActive && Input.GetMouseButtonDown(1)) { SetMode(Mode.Off); return; }
            UpdateGhost();
            // #179/#182 - click 처리.  cooldown 으로 SetMode 직후 race 방지.
            //  EventSystem guard 는 over-blocking 위험 (TopBar/GuiControlBar 가 항상 raycast target).
            //  대신 PlaceCooldownSec (0.15s) 으로 메뉴 버튼 click → SetMode → 즉시 TryPlace race 차단.
            if (BuildModeActive && Input.GetMouseButtonDown(0))
            {
                HandleLeftClickAt(Input.mousePosition, checkOverUI: true);
            }
        }

        /// <summary>
        /// #191 - real Input chain 과 시뮬레이션이 동일 path 공유.
        ///   Update 의 raw input → cooldown → overUI → TryPlace 시퀀스 그대로 재현.
        ///   BuildClickAutoQA v2 가 ExecuteEvents 로 button click 시뮬 후 이 메서드로 map click 시뮬.
        /// </summary>
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
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (overUI)
                {
                    Vector3 mwLog = (cam != null) ? cam.ScreenToWorldPoint(screenPos) : Vector3.zero;
                    Debug.Log($"[Build] CLICK skip: overUI=true at screen={screenPos} world=({mwLog.x:F1},{mwLog.y:F1})");
                    if (BuildClickToast.Instance != null) BuildClickToast.Instance.ShowFail("✗ UI 위 클릭 - 맵에 직접 클릭하세요");
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
            ghostRenderer.sprite = m switch
            {
                Mode.Wall  => wallSprite,
                Mode.Floor => floorSprite,
                Mode.Door  => doorSprite,
                Mode.Stove => stoveSprite,
                Mode.Bed   => bedSprite,
                Mode.BedSleepingSpot => bedSprite,  // #154
                Mode.BedFine => bedSprite,  // #154
                Mode.Lamp  => EnsureLampSprite(),   // W-M4-04 #19
                _ => wallSprite,
            };
            ghostRenderer.sortingOrder = m == Mode.Floor ? 1 : 20;
        }

        private int CostFor(Mode m) => m switch
        {
            Mode.Wall            => wallCost,
            Mode.WallStone       => wallStoneCost,
            Mode.Floor           => floorCost,
            Mode.Door            => doorCost,
            Mode.Stove           => stoveCost,
            Mode.Bed             => bedCost,
            Mode.BedSleepingSpot => bedSleepingSpotCost,  // #154 - 0
            Mode.BedFine         => bedFineCost,          // #154 - 30
            Mode.Lamp            => lampCost,             // W-M4-04 #19 - 목재 4
            _ => 0,
        };

        private GameObject PrefabFor(Mode m) => m switch
        {
            Mode.Wall            => wallPrefab,
            Mode.WallStone       => wallPrefab,  // 같은 prefab, 다른 자원
            Mode.Floor           => floorPrefab,
            Mode.Door            => doorPrefab,
            Mode.Stove           => stovePrefab,
            Mode.Bed             => bedPrefab,
            Mode.BedSleepingSpot => bedPrefab,  // #154 - 같은 prefab, quality 다름
            Mode.BedFine         => bedPrefab,  // #154
            Mode.Lamp            => EnsureLampPrefab(),  // W-M4-04 #19
            _ => null,
        };

        // #193 - RimWorld vanilla 침대 1x2 칸.  multi-cell entity footprint 인프라.
        //  Bed/BedFine: 1x2.  나머지: 1x1.  anchor = bottom-left cell (cx, cy).
        public static Vector2Int SizeFor(Mode m) => m switch
        {
            Mode.Bed             => new Vector2Int(1, 2),
            Mode.BedFine         => new Vector2Int(1, 2),
            _ => new Vector2Int(1, 1),
        };

        private void UpdateGhost()
        {
            if (!BuildModeActive || ghostRenderer == null || cam == null) return;
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            int cx = Mathf.FloorToInt(mw.x);
            int cy = Mathf.FloorToInt(mw.y);
            // #193 - multi-cell ghost: center = (cx + w*0.5, cy + h*0.5)
            Vector2Int size = SizeFor(CurrentMode);
            ghostRenderer.transform.position = new Vector3(cx + size.x * 0.5f, cy + size.y * 0.5f, 0);
            // ghost scale 도 size 적용 (sprite world bound 보정)
            if (ghostRenderer.sprite != null)
            {
                Vector2 sw = ghostRenderer.sprite.bounds.size;
                if (sw.x > 0.01f && sw.y > 0.01f)
                    ghostRenderer.transform.localScale = new Vector3(size.x / sw.x, size.y / sw.y, 1f);
            }
            int cost = CostFor(CurrentMode);
            bool stoneMode = CurrentMode == Mode.WallStone;
            bool canAfford = ResourceManager.Instance != null
                && (stoneMode ? ResourceManager.Instance.stone : ResourceManager.Instance.wood) >= cost;
            // #199 C3 - ghost 색: terrain(물/바위) or 점유 시 빨강 (RimWorld red ghost).
            bool areaFree = ValidatePlacement(cx, cy, size.x, size.y) == PlaceReject.None;
            ghostRenderer.color = (canAfford && areaFree)
                ? new Color(1f, 1f, 1f, 0.55f)
                : new Color(1f, 0.4f, 0.4f, 0.55f);
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
                if (h.GetComponent<TreeEntity>() != null) return true;
                // #199 C3 - RimWorld fidelity: a pawn STANDING on the cell does NOT
                //  block placement.  In RimWorld you can drop a blueprint under a
                //  colonist; the pawn simply walks off (the blueprint only needs the
                //  cell clear of *structures/terrain*, not transient occupants).  So
                //  PawnEntity (and animals, which carry no structural footprint) are
                //  intentionally NOT treated as occupancy here.  Removing this also
                //  de-fragiles Build Click QA (settlement cells often have pawns).
                if (h.GetComponent<BerryBushEntity>() != null) return true;
                if (h.GetComponent<StoveEntity>() != null) return true;
                if (h.GetComponent<LampEntity>() != null) return true;   // W-M4-04 #19
                if (h.GetComponent<BedEntity>() != null) return true;
                if (h.GetComponent<BlueprintEntity>() != null) return true;  // #118
            }
            return false;
        }

        /// <summary>
        /// #199 C3 - RimWorld placement rule: a blueprint may NOT sit on impassable
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
        //  the rejection reasons so the toast can tell the player WHY (RimWorld
        //  shows a red ghost + reason string).
        private enum PlaceReject { None, Terrain, Occupied }

        /// <summary>
        /// #199 C3 - validate EVERY covered cell of a w×h footprint at anchor
        /// (cx,cy) BEFORE creating a blueprint, RimWorld-style:
        ///   - reject if ANY cell is impassable terrain (Water/Rock) → PlaceReject.Terrain;
        ///   - reject if ANY cell already holds a structure/blueprint → PlaceReject.Occupied.
        /// Terrain is checked first so a water cell reads "물 위엔 못 지음" rather than
        /// a generic occupied message.  All cells validated (multi-cell beds/benches).
        /// Returns PlaceReject.None when the whole footprint is buildable.
        /// </summary>
        private PlaceReject ValidatePlacement(int cx, int cy, int w, int h)
        {
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

        private bool DoTryPlaceAt(int cx, int cy)
        {
            // #179 - silent return 4개를 명시 log 로 진단 가능하게.
            // #190 - 각 실패 path 에 BuildClickToast 추가 (운영자가 화면에서 바로 원인 확인).
            var prefab = PrefabFor(CurrentMode);
            if (prefab == null)
            {
                Debug.LogWarning($"[Build] TryPlace skip: prefab null for mode={CurrentMode}");
                if (BuildClickToast.Instance != null) BuildClickToast.Instance.ShowFail($"✗ prefab 미설정 ({CurrentMode})");
                return false;
            }
            int cost = CostFor(CurrentMode);
            // #193 - multi-cell entity (침대 1x2 등) footprint 검사
            // #199 C3 - RimWorld 배치 규칙: terrain(물/바위) + 구조물/청사진 중복 모두 거부.
            //  pawn 이 서 있는 cell 은 거부 X (pawn 이 비켜남).  multi-cell 은 전 cell 검사.
            Vector2Int size = SizeFor(CurrentMode);
            PlaceReject reject = ValidatePlacement(cx, cy, size.x, size.y);
            if (reject == PlaceReject.Terrain)
            {
                Debug.Log($"[Build] TryPlace skip: terrain (water/rock) at ({cx},{cy}) {size.x}x{size.y} for mode={CurrentMode}");
                if (BuildClickToast.Instance != null) BuildClickToast.Instance.ShowFail($"✗ 물/바위 위엔 못 지음 ({cx},{cy})");
                return false;
            }
            if (reject == PlaceReject.Occupied)
            {
                Debug.Log($"[Build] TryPlace skip: area ({cx},{cy}) {size.x}x{size.y} occupied for mode={CurrentMode}");
                if (BuildClickToast.Instance != null) BuildClickToast.Instance.ShowFail($"✗ {size.x}x{size.y} 영역 점유됨 ({cx},{cy}) - 다른 곳 시도");
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
            Debug.Log($"[Build] TryPlace OK: mode={CurrentMode} → blueprint at ({cx+0.5f}, {cy+0.5f}), need wood={(CurrentMode == Mode.WallStone ? 0 : cost)} stone={(CurrentMode == Mode.WallStone ? cost : 0)}");
            // 운영자 fb v4 - 림월드 정상 흐름: 청사진 spawn 시 자원 차감 X.
            //  hauler 가 자재를 청사진 위치까지 운반 후 PawnBuilder 가 건설 작업.
            bool stoneMode = CurrentMode == Mode.WallStone;
            int needWood = stoneMode ? 0 : cost;
            int needStone = stoneMode ? cost : 0;

            Sprite ghostSpr = SpriteForCurrentMode();
            var bpGo = new GameObject($"Blueprint_{CurrentMode}");
            // #193 - multi-cell entity center = (cx + w*0.5, cy + h*0.5).  1x1 이면 (+0.5,+0.5) = 기존.
            Vector3 center = new Vector3(cx + size.x * 0.5f, cy + size.y * 0.5f, 0);
            bpGo.transform.position = center;
            var bp = bpGo.AddComponent<BlueprintEntity>();
            float secs = CurrentMode == Mode.Floor ? 2f : 5f;
            bp.Init(CurrentMode, prefab, ghostSpr, needWood, needStone, secs);
            bp.SetSize(size);  // #193 - 청사진 sprite 도 1x2 비율 적용
            // #190 - 클릭 성공 토스트 + 시각 ring (운영자가 "어디에 청사진 생겼지?" 즉시 확인)
            if (BuildClickToast.Instance != null)
            {
                string kr = ModeKr(CurrentMode);
                string sizeKr = (size.x == 1 && size.y == 1) ? "" : $" {size.x}x{size.y}";
                BuildClickToast.Instance.ShowSuccess($"✓ 청사진 - {kr}{sizeKr} @ ({cx},{cy})");
            }
            ClickEffect.Spawn(center, new Color(0.55f, 0.85f, 1.0f, 0.95f));
            return true;
        }

        private static string ModeKr(Mode m) => m switch
        {
            Mode.Wall            => "벽(목재)",
            Mode.WallStone       => "벽(석재)",
            Mode.Floor           => "바닥",
            Mode.Door            => "문",
            Mode.Stove           => "화덕",
            Mode.Bed             => "목재 침대",
            Mode.BedSleepingSpot => "수면 자리",
            Mode.BedFine         => "고급 침대",
            Mode.Lamp            => "램프",   // W-M4-04 #19
            _ => m.ToString(),
        };

        private Sprite SpriteForCurrentMode() => CurrentMode switch
        {
            Mode.Wall  => wallSprite,
            Mode.Floor => floorSprite,
            Mode.Door  => doorSprite,
            Mode.Stove => stoveSprite,
            Mode.Bed   => bedSprite,
            Mode.BedSleepingSpot => bedSprite,
            Mode.BedFine         => bedSprite,
            Mode.Lamp  => EnsureLampSprite(),   // W-M4-04 #19
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
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/lamp.png");
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
    }

    public enum BuildClickResult { Placed, PlaceFailed, ModeOff, Cooldown, OverUI, NoCamera }
}
