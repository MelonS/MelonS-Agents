using UnityEngine;
using UnityEngine.EventSystems;
using MelonS.GameProto.AI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// ZONE wave Z1 + Z2b + Z3 (운영자 "영역지정 필수 — 저장/폐기 zone + 아이템 저장공간
    /// 설정") — STOCKPILE / DUMPING ZONE DESIGNATION.
    ///
    /// spec: G:/ai/_zone_research.md.
    ///   Z1 (저장존): drag-rect 로 floor cell 위에 StockpileZoneEntity 를 cell 마다 spawn
    ///       (priority Normal, 모든 종류 허용).  레퍼런스 콜로니심 Architect → Zone → Stockpile.
    ///   Z3 (폐기존): 같은 drag 모드의 'dumping' preset — priority Low + Stone(chunk/refuse)
    ///       만 허용 + 회색 dim tint.  레퍼런스 콜로니심 Dumping stockpile = stockpile preset.
    ///   (구 Z2b 아이템 필터 toolbar — 저장존 클릭 시 뜨던 목재/🪨석재/🍖식량 toggle 3개 —
    ///       는 운영자 fb 2026-06-01 로 제거되었다("정체불명 3버튼").  저장존은 spawn 시
    ///       StockItemKind.All(전부 수용)이 기본이므로 toolbar 없이도 모든 자원을 받고,
    ///       필터 데이터/API 는 StockpileZoneEntity.cs 에 그대로 살아 있어 hauler Z2
    ///       필터는 계속 작동한다.)
    ///
    /// 이 매니저는 StockpileZoneEntity.cs 의 데이터/필터(Z2)와 hauler(Z2)는 건드리지 않고
    /// (그건 같은 wave 의 다른 edit), 그것들이 노출한 public API(Spawn(...,priority,allowed),
    /// AllowedKinds, ToggleKind) 만 READ/CALL 한다.  ArchitectMenu 에는 항목 2개(저장/폐기)만
    /// 추가된다 — Zone(구역) 카테고리에 additive line, 다른 카테고리/플러밍 미변경.
    ///
    /// ----------------------------------------------------------------------------
    /// DISCIPLINE — mirrors MineDesignation.cs / GrowZoneDesignation.cs EXACTLY:
    ///   - [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + GameSceneGate.RunWhenGameScene
    ///     게이트로 MainMenu 누출 방지, ONE DontDestroyOnLoad manager, FindFirst 멱등.
    ///   - 라이브 싱글톤 ref 를 OnEnable 에서 캡쳐하지 않음(버그패턴 #7 방화벽): 매 프레임
    ///     read-only poll-find (BuildManager/다른 designation/Camera) + 교체 시 re-find.
    ///   - 타이밍은 Update 안의 Time.unscaledTime delta (버그패턴 #9 방화벽: WaitForSeconds
    ///     heartbeat 코루틴 없음 — 포커스 상실 시 freeze 안 됨).
    ///   - AudioBank.PlaySelect() 는 새 cell 마다 1회 / toggle 클릭 1회만 호출
    ///     (AudioBank 내부 throttle, 버그패턴 #4 방화벽 — tight-loop buzz 없음); 멱등 re-mark
    ///     은 무음.
    ///
    /// MUTUAL EXCLUSION (한 left-click 은 정확히 하나의 핸들러):
    ///   stockpile 모드는 BuildManager build / Deconstruct / Mine / Grow 모드와 상호배타.
    ///   진입 시 나머지를 끄고, 나머지가 켜지면 빠진다.  Right-click / ESC 로 취소.
    ///
    /// MODE-TOGGLE ROUTING (lane hot-file budget):
    ///   GrowZoneDesignation 처럼 standalone 스크린 버튼 없음 — ArchitectMenu Zone(구역)
    ///   카테고리에서 진입(저장/폐기).  HOTKEY O(stOrage) 는 일반 저장존 모드 토글
    ///   (B/F/G/T/Y/N/R/X/M/P/K/J/H/L/E + WASD 충돌 회피).  SceneSetup hot-file budget 0.
    ///
    ///   >>> QA FLAG (scene-wiring): 저장존 마커 = 런타임 1×1 흰 sprite(priority tint),
    ///       전부 code-generated / SceneSetup 미편집.  QA 확인: Architect→Zone→저장,
    ///       3×3 드래그 → 9 StockpileZoneEntity(Normal 노랑); 폐기 드래그 → Low(회색)
    ///       + Stone-only.  (구 Z2b 필터 toolbar 는 운영자 fb 2026-06-01 로 제거됨 —
    ///       저장존은 전부 수용(All)이 기본이라 클릭해도 UI 가 뜨지 않는다.)
    /// </summary>
    public class StockpileDesignation : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tune day-1 feel) -------
        [Header("Drag-rect")]
        [SerializeField] private float dragThreshold = 0.35f;     // world units before a click becomes a drag
        [SerializeField] private float pickRadius = 0.45f;        // cell occupancy probe half-extent

        [Header("Markers")]
        [SerializeField] private int markerSortingOrder = 2;      // below crops(3)/pawns, above ground

        // 운영자 fb (2026-06-01): "저장공간에서 셀을 누르면 뜨던 3버튼(목재/석재/식량 필터
        //  토글바, 구 Z2b)"을 제거했다 — 정체불명 UI 라 혼란만 줬다.  저장존은 spawn 시
        //  StockItemKind.All(전부 수용)로 만들어지므로, 토글바 없이도 모든 자원을 받는 게
        //  기본 동작이다(= 안전한 default).  필터 데이터(StockpileZoneEntity.AllowedKinds/
        //  ToggleKind/Accepts)는 그 파일에 그대로 살아 있어 hauler Z2 필터는 정상 작동한다.

        // ---- runtime state ---------------------------------------------------
        public static StockpileDesignation Instance { get; private set; }

        /// <summary>True while the player is in stockpile designation mode (저장 OR 폐기).</summary>
        public bool ModeActive { get; private set; }

        /// <summary>True if the active drag is laying DUMPING zones (Low + Stone-only)
        ///  rather than normal storage zones.</summary>
        public bool DumpingMode { get; private set; }

        /// <summary>운영자 fb (2026-06-01) "창고영역 삭제 기능 필요" — true 면 같은 drag-rect
        /// 가 stockpile/dumping zone 을 '지우는' ERASE 모드.  add 경로와 대칭: 같은 입력/
        /// 드래그 로직을 타되 MarkCellAt 대신 EraseCellAt 을 호출해 그 셀의 StockpileZoneEntity
        /// 를 제거한다.  the reference sim 의 Zone 'Delete/Clear' 와 동일.  ModeActive 안에서만 의미.</summary>
        public bool EraseMode { get; private set; }

        private Camera cam;

        // Drag-rect state.
        private bool dragging;
        private Vector3 dragStartWorld;

        // ============================================================
        //  Self-bootstrap — no SceneSetup edit (GrowZoneDesignation pattern).
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindExisting() != null) return;   // idempotent — never a 2nd manager
                var go = new GameObject("~StockpileDesignation");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<StockpileDesignation>();
            });
        }

        private static StockpileDesignation FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<StockpileDesignation>();
#else
            return Object.FindObjectOfType<StockpileDesignation>();
#endif
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            cam = Camera.main;
        }

        // ---- mode control ----------------------------------------------------

        /// <summary>Enter/leave STORAGE (저장) mode.  Mutually exclusive with build /
        /// deconstruct / mine / grow.  ArchitectMenu Zone → 저장 calls SetMode(true).</summary>
        public void SetMode(bool on) => SetModeInternal(on, dumping: false, erase: false);

        /// <summary>Enter DUMPING (폐기) mode — same drag, Low+Stone-only preset.
        /// ArchitectMenu Zone → 폐기 calls SetDumpingMode(true).</summary>
        public void SetDumpingMode(bool on) => SetModeInternal(on, dumping: true, erase: false);

        /// <summary>운영자 fb (2026-06-01): enter ERASE (삭제) mode — the same drag-rect
        /// removes any stockpile/dumping zone under each swept cell.  ArchitectMenu Zone
        /// → 창고 삭제 row (or Shift+O hotkey) calls SetEraseMode(true).</summary>
        public void SetEraseMode(bool on) => SetModeInternal(on, dumping: false, erase: true);

        private void SetModeInternal(bool on, bool dumping, bool erase)
        {
            ModeActive = on;
            DumpingMode = on && dumping;
            EraseMode = on && erase;
            if (on)
            {
                // Cancel the other four designation modes so one click fires one handler.
                if (BuildManager.Instance != null && BuildManager.Instance.BuildModeActive)
                    BuildManager.Instance.SetMode(BuildManager.Mode.Off);
                if (DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive)
                    DeconstructDesignation.Instance.SetMode(false);
                if (MineDesignation.Instance != null && MineDesignation.Instance.ModeActive)
                    MineDesignation.Instance.SetMode(false);
                if (GrowZoneDesignation.Instance != null && GrowZoneDesignation.Instance.ModeActive)
                    GrowZoneDesignation.Instance.SetMode(false);
            }
            if (!on) { dragging = false; }
        }

        /// <summary>O hotkey toggles plain STORAGE mode.</summary>
        public void Toggle() => SetMode(!ModeActive);

        private void Update()
        {
            if (cam == null) cam = Camera.main;

            // Hotkey O (stOrage) — free key (build B/F/G/T/Y, N/R, X decon, M mine,
            //  P plant, K/J/H/L/E builds are taken).  Plain O toggles storage mode;
            //  Shift+O toggles the ERASE submode (운영자 fb: 창고영역 삭제).
            if (Input.GetKeyDown(KeyCode.O))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shift) { if (ModeActive && EraseMode) SetEraseMode(false); else SetEraseMode(true); }
                else       { if (ModeActive && !EraseMode && !DumpingMode) SetMode(false); else SetMode(true); }
            }

            // If a build / deconstruct / mine / grow mode was entered elsewhere, leave
            //  stockpile mode so they never fight over the same left-click.
            if (ModeActive)
            {
                bool other =
                    (BuildManager.Instance != null && BuildManager.Instance.BuildModeActive) ||
                    (DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive) ||
                    (MineDesignation.Instance != null && MineDesignation.Instance.ModeActive) ||
                    (GrowZoneDesignation.Instance != null && GrowZoneDesignation.Instance.ModeActive);
                if (other) SetMode(false);
            }

            if (ModeActive)
            {
                // Right-click / ESC cancels the mode (same convention as build).
                if (SimInput.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                    SetMode(false);
                else
                    HandleDragInput();
            }
            // 구 Z2b 필터 토글바 제거(운영자 fb 2026-06-01): 저장존을 클릭해도 더 이상
            //  아무 UI 도 뜨지 않는다 — 저장존은 spawn 시 전부 수용(All)이 기본.
        }

        // ---- left-press / drag / release → lay stockpile cell(s) -------------

        // 첫사이클 T12 — 드래그 중 라이브 사각형 미리보기 (release 일괄 Mark 라
        //  깜깜이 드래그이던 것).  순수 시각, 하네스 1프레임 클릭에는 무표시(무해).
        private void OnRenderObject()
        {
            if (!dragging || !ModeActive || cam == null || Camera.current != cam) return;
            Vector3 curW = cam.ScreenToWorldPoint(SimInput.mousePosition);
            bool destr = EraseMode || DumpingMode;
            var fillC = destr ? new Color(0.85f, 0.30f, 0.25f, 0.13f) : new Color(0.95f, 0.85f, 0.40f, 0.13f);
            var borderC = destr ? new Color(0.90f, 0.35f, 0.30f, 0.9f) : new Color(0.95f, 0.85f, 0.45f, 0.9f);
            DragRectGL.Draw(cam, dragStartWorld, curW, fillC, borderC);
        }

        private void HandleDragInput()
        {
            if (cam == null) return;

            if (SimInput.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;   // press began over UI — ignore
                dragging = true;
                dragStartWorld = cam.ScreenToWorldPoint(SimInput.mousePosition);
            }
            else if (SimInput.GetMouseButtonUp(0) && dragging)
            {
                dragging = false;
                Vector3 endWorld = cam.ScreenToWorldPoint(SimInput.mousePosition);
                if (Vector3.Distance(endWorld, dragStartWorld) < dragThreshold)
                    MarkCell(endWorld);                 // a tap → single cell
                else
                    MarkRect(dragStartWorld, endWorld); // a sweep → rectangle of cells
            }
        }

        // ---- public test / QA entry points (no real pointer needed) ----------

        /// <summary>QA / test entry: lay one stockpile cell under a screen position
        /// (no overUI guard).  Returns the StockpileZoneEntity, or null if rejected.</summary>
        public StockpileZoneEntity SimulateClick(Vector2 screenPos)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return null;
            return MarkCell(cam.ScreenToWorldPoint(screenPos));
        }

        /// <summary>QA / test entry: drag-select a world rectangle, lay a stockpile on
        /// every valid floor cell inside it.  Returns the count laid.</summary>
        public int SimulateDragRect(Vector2 worldA, Vector2 worldB) => MarkRect(worldA, worldB);

        /// <summary>QA / test entry: lay a stockpile at a specific cell (current preset).</summary>
        public StockpileZoneEntity DesignateCell(Vector2Int cell) => MarkCellAt(cell.x, cell.y);

        // ---- marking ---------------------------------------------------------

        private StockpileZoneEntity MarkCell(Vector3 world)
        {
            int cx = Mathf.FloorToInt(world.x), cy = Mathf.FloorToInt(world.y);
            if (EraseMode) { EraseCellAt(cx, cy); return null; }
            return MarkCellAt(cx, cy);
        }

        private int MarkRect(Vector3 a, Vector3 b)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x));
            int x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y));
            int y1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y));
            int n = 0;
            for (int cx = x0; cx <= x1; cx++)
                for (int cy = y0; cy <= y1; cy++)
                    if (EraseMode ? EraseCellAt(cx, cy) : (MarkCellAt(cx, cy, playBlip: false, fx: false) != null)) n++;
            // #버그헌트3(2026-06-04): 드래그 전체에 blip 1회 + FX 1회(rect 중심)만 — 이전엔 per-cell 로
            //  PlaySelect/ClickEffect 를 한 프레임에 수십~수백 회 발화해 오디오 버퍼 collapse 버즈
            //  ('이상한 사운드') + 마커 flood 였다(PlaySelect 는 throttle 없음 — 기존 주석 거짓).
            //  RoofDesignation 과 동일 배치 패턴.
            if (n > 0 && !EraseMode)
            {
                AudioBank.Instance?.PlaySelect();
                ClickEffect.Spawn(new Vector3((x0 + x1) * 0.5f + 0.5f, (y0 + y1) * 0.5f + 0.5f, 0f),
                    DumpingMode ? new Color(0.6f, 0.6f, 0.62f, 0.95f) : new Color(0.95f, 0.85f, 0.30f, 0.95f));
            }
            return n;
        }

        /// <summary>운영자 fb (2026-06-01) "창고영역 삭제 기능 필요": remove the stockpile/
        /// dumping zone occupying cell (cx,cy).  Symmetric to MarkCellAt — finds the live
        /// StockpileZoneEntity at the cell centre and Destroys it (the zone holds no
        /// PathGrid blocker, so a plain Unity Destroy frees the cell; haulers holding it
        /// as a target self-release on their next null-check poll, the same way an erased
        /// crop/marker is dropped).
        /// Idempotent: erasing an empty cell is a silent no-op.  Returns true if a zone
        /// was removed.</summary>
        public bool EraseCellAt(int cx, int cy)
        {
            Vector2 center = new Vector2(cx + 0.5f, cy + 0.5f);
            var z = ExistingStockpileAt(center);
            if (z == null) return false;   // nothing here — silent no-op

            // 첫사이클 T8 (2026-06-12) — 존만 지우면 그 위 더미가 InStockpile=true 로
            //  영구 좌초(재운반 불가·부패 정지·창고 이사 불가).  '미저장'으로 되돌리고
            //  카운터도 차감('카운터 = Σ InStockpile 더미' 불변식 — 픽업 차감과 대칭).
            //  기존 운반 AI 가 남은 구역으로 자동 이사한다.
            var rmRef = ResourceManager.Instance;
            foreach (var hit in Physics2D.OverlapBoxAll(center, Vector2.one * 0.9f, 0f))
            {
                if (hit == null) continue;
                var wp = hit.GetComponent<WoodPileEntity>();
                if (wp != null && wp.InStockpile) { wp.InStockpile = false; rmRef?.AddWood(-wp.Wood); continue; }
                var sc = hit.GetComponent<StoneChunkEntity>();
                if (sc != null && sc.InStockpile) { sc.InStockpile = false; rmRef?.AddStone(-sc.Stone); continue; }
                var mp = hit.GetComponent<MeatPileEntity>();
                if (mp != null && mp.InStockpile) { mp.InStockpile = false; rmRef?.AddFood(-mp.Food); }
            }
            Destroy(z.gameObject);

            AudioBank.Instance?.PlaySelect();
            ClickEffect.Spawn(new Vector3(center.x, center.y, 0f),
                new Color(0.85f, 0.40f, 0.36f, 0.95f)); // DANGER_RED-ish = removed
            Debug.Log($"[Stockpile] 영역 삭제 cell ({cx},{cy})");
            return true;
        }

        /// <summary>QA / test entry point: erase the stockpile zone at a specific cell.</summary>
        public bool EraseCell(Vector2Int cell) => EraseCellAt(cell.x, cell.y);

        /// <summary>Lay a StockpileZoneEntity on cell (cx,cy) if it is open floor.
        /// Idempotent: a cell that already holds a stockpile is skipped (returns the
        /// existing one, silently — no audio).  Rejects non-floor (water/rock) and
        /// cells occupied by a structure/blueprint/vein the way GrowZoneDesignation
        /// rejects non-plantable cells.  Preset = storage(Normal/All) or dumping
        /// (Low/Stone-only) per the active mode.</summary>
        private StockpileZoneEntity MarkCellAt(int cx, int cy, bool playBlip = true, bool fx = true)
        {
            Vector2 center = new Vector2(cx + 0.5f, cy + 0.5f);

            // Idempotent re-mark: a stockpile already here → return it (silent).
            var existing = ExistingStockpileAt(center);
            if (existing != null) return existing;

            if (!IsFloorCell(cx, cy)) return null;   // reject water/rock/occupied

            var preset = DumpingMode
                ? (StockpilePriority.Low,    StockItemKind.Stone)   // Z3 폐기존
                : (StockpilePriority.Normal, StockItemKind.All);    // Z1 저장존

            var z = StockpileZoneEntity.Spawn(new Vector3(center.x, center.y, 0f),
                ZoneSprite(), preset.Item1, preset.Item2);
            // dumping zone 은 priority tint(Low=회색) 가 이미 dim 이라 별도 처리 불필요 —
            //  Low tint(회색 0.55 a.45) 가 곧 "refuse" 시각.  zone marker sprite 의
            //  sortingOrder 를 맞춘다.
            var sr = z.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = markerSortingOrder;

            // #버그헌트3(2026-06-04): 단일 클릭만 즉시 피드백; 드래그(MarkRect)는 playBlip/fx=false 로
            //  넘겨 per-cell 버즈/flood 를 막고 rect 당 1회만 발화(PlaySelect 는 throttle 없음).
            if (playBlip) AudioBank.Instance?.PlaySelect();
            if (fx) ClickEffect.Spawn(new Vector3(center.x, center.y, 0f),
                DumpingMode ? new Color(0.6f, 0.6f, 0.62f, 0.95f)   // 폐기 = 회색
                            : new Color(0.95f, 0.85f, 0.30f, 0.95f)); // 저장 = 노랑
            Debug.Log($"[Stockpile] {(DumpingMode ? "폐기존" : "저장존")} 지정 cell ({cx},{cy})");
            return z;
        }

        /// <summary>An existing live stockpile occupying the cell centre, or null.</summary>
        private StockpileZoneEntity ExistingStockpileAt(Vector2 center)
        {
            var hits = Physics2D.OverlapBoxAll(center, Vector2.one * pickRadius, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var z = h.GetComponent<StockpileZoneEntity>();
                if (z != null) return z;
            }
            return null;
        }

        /// <summary>True if (cx,cy) is open floor a stockpile can sit on — NOT
        /// impassable terrain (water/rock), walkable on the PathGrid, and free of
        /// structures/blueprints/veins/crops.  Mirrors GrowZoneDesignation
        /// .IsPlantableCell's reject philosophy (null tilemap/grid → don't false-reject
        /// in headless/unit-test scenes).</summary>
        public bool IsFloorCell(int cx, int cy)
        {
            Vector2 center = new Vector2(cx + 0.5f, cy + 0.5f);

            if (PawnMovement.IsBlockedAt(center)) return false;   // water/rock
            if (PawnMovement.Grid != null && !PawnMovement.Grid.IsWalkable(new Vector2Int(cx, cy)))
                return false;

            var hits = Physics2D.OverlapBoxAll(center, Vector2.one * pickRadius, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var go = h.gameObject;
                if (go.GetComponent<StockpileZoneEntity>() != null) return false; // already a zone
                if (go.GetComponent<WallEntity>() != null) return false;
                if (go.GetComponent<DoorEntity>() != null) return false;
                if (go.GetComponent<StoveEntity>() != null) return false;
                if (go.GetComponent<BedEntity>() != null) return false;
                if (go.GetComponent<BlueprintEntity>() != null) return false;
                if (go.GetComponent<StoneVeinEntity>() != null) return false;
                if (go.GetComponent<CropEntity>() != null) return false;
            }
            return true;
        }

        // A shared 1×1 white sprite for stockpile markers (priority tint per-renderer,
        //  applied by StockpileZoneEntity.ApplyTint).  Runtime — no prefab / PNG.
        private static Sprite sharedZoneSprite;
        private static Sprite ZoneSprite()
        {
            if (sharedZoneSprite != null) return sharedZoneSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            sharedZoneSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1f);   // 1 px-per-unit → 1 world-unit quad
            return sharedZoneSprite;
        }

        // ====================================================================
        //  (구 Z2b 필터 토글바 제거 — 운영자 fb 2026-06-01)
        //  저장존을 클릭하면 뜨던 목재/🪨석재/🍖식량 3버튼 toolbar 와 그 selection/
        //  follow/EnsureToolbar 로직 전체를 삭제했다.  저장존은 StockpileZoneEntity.Spawn
        //  시점에 StockItemKind.All(전부 수용)로 초기화되므로 UI 없이도 모든 자원을 받는
        //  것이 기본 동작이고, 필터 데이터/API(AllowedKinds·ToggleKind·Accepts)는
        //  StockpileZoneEntity.cs 에 그대로 남아 hauler Z2 필터가 계속 정상 작동한다.
        // ====================================================================
    }
}
