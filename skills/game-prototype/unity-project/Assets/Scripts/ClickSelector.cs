using UnityEngine;
using UnityEngine.EventSystems;

namespace MelonS.GameProto
{
    /// <summary>
    /// Listens to left-mouse clicks and selects PawnEntity instances
    /// under the cursor via 2D collider raycast.  Day 1 supports single
    /// selection only.  Click on empty ground = clear selection.
    /// </summary>
    public class ClickSelector : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;

        private PawnEntity currentSelection;
        public PawnEntity CurrentSelection => currentSelection;

        // #105 - 비-pawn 오브젝트 inspect.  EntityInspectorPanel 폴링.
        private GameObject currentInspect;
        public GameObject CurrentInspect => currentInspect;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
            // UI 위 클릭 무시 (운영자 피드백 - UI 가 가로채던 문제)
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            // 빌드 모드 활성이면 mouse click 은 BuildManager 가 처리 (place / cancel)
            bool buildActive = BuildManager.Instance != null && BuildManager.Instance.BuildModeActive;

            // Left click = select
            if (Input.GetMouseButtonDown(0) && !overUI && !buildActive)
            {
                Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;
                Collider2D hit = Physics2D.OverlapPoint(mouseWorld);
                PawnEntity pawn = (hit != null) ? hit.GetComponent<PawnEntity>() : null;
                if (pawn != null) {
                    Select(pawn);
                    currentInspect = pawn.gameObject;  // #128 - pawn 도 inspect 패널 표시
                    ClickEffect.Spawn(mouseWorld, new Color(0.3f, 1.0f, 0.5f, 0.95f));
                }
                else if (hit != null) {
                    // #105 - 비-pawn entity 좌클릭 = inspect (pawn 선택 clear)
                    currentInspect = hit.gameObject;
                    ClearSelection();
                    // #128 - 클릭 시각 피드백 (yellow ring) - 패널 안 보였던 원인 진단 도움
                    ClickEffect.Spawn(mouseWorld, new Color(1.0f, 0.85f, 0.30f, 0.95f));
                }
                else { ClearSelection(); currentInspect = null; }
            }

            // Day 48: R key toggles drafted on selected pawn
            if (Input.GetKeyDown(KeyCode.R) && currentSelection != null)
            {
                currentSelection.SetDrafted(!currentSelection.IsDrafted);
            }

            // Right click = move OR chop OR attack (drafted) for selected pawn
            //   buildActive 면 BuildManager 가 우클릭 = cancel 처리 (overlap 방지)
            //   #113 - undrafted + entity hit = RimWorld 스타일 "Prioritize" 컨텍스트 메뉴
            if (Input.GetMouseButtonDown(1) && !overUI && !buildActive && currentSelection != null
                && !currentSelection.IsDrafted)
            {
                Vector3 mw = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mw.z = 0f;
                Collider2D ehit = Physics2D.OverlapPoint(mw);
                if (ehit != null && ContextMenuUI.Instance != null)
                {
                    var items = BuildContextMenu(ehit, mw);
                    if (items != null && items.Count > 0)
                    {
                        ContextMenuUI.Instance.Open(Input.mousePosition, items);
                        return;  // 메뉴 떴음 - 기존 직접 action skip
                    }
                }
                // entity 없거나 메뉴 없으면 기존 동작 (manual move)
            }
            if (Input.GetMouseButtonDown(1) && !overUI && !buildActive && currentSelection != null)
            {
                Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;
                // 시각 피드백 - 사용자에게 클릭 위치 보여줌
                ClickEffect.Spawn(mouseWorld, new Color(1f, 0.9f, 0.3f, 0.95f));
                Collider2D rhit = Physics2D.OverlapPoint(mouseWorld);

                // Day 48: drafted pawn — right-click on enemy/animal = attack/hunt
                if (currentSelection.IsDrafted)
                {
                    if (rhit != null)
                    {
                        BanditEnemy bandit = rhit.GetComponent<BanditEnemy>();
                        AnimalEntity animal = rhit.GetComponent<AnimalEntity>();
                        WolfEnemy wolf = rhit.GetComponent<WolfEnemy>();
                        if (bandit != null)
                        {
                            currentSelection.DraftedAttackTarget = bandit;
                            currentSelection.DraftedHuntTarget   = null;
                            currentSelection.DraftedWolfTarget   = null;
                            Debug.Log($"[Draft] {currentSelection.PawnName} → 적 공격");
                            return;
                        }
                        if (wolf != null)
                        {
                            currentSelection.DraftedWolfTarget   = wolf;
                            currentSelection.DraftedAttackTarget = null;
                            currentSelection.DraftedHuntTarget   = null;
                            Debug.Log($"[Draft] {currentSelection.PawnName} → 늑대 공격");
                            return;
                        }
                        if (animal != null)
                        {
                            currentSelection.DraftedHuntTarget   = animal;
                            currentSelection.DraftedAttackTarget = null;
                            currentSelection.DraftedWolfTarget   = null;
                            Debug.Log($"[Draft] {currentSelection.PawnName} → 동물 사냥");
                            return;
                        }
                    }
                    // Otherwise: manual movement (no chop while drafted)
                    PawnMovement mvD = currentSelection.GetComponent<PawnMovement>();
                    if (mvD != null) mvD.SetTarget(new Vector2(mouseWorld.x, mouseWorld.y));
                    currentSelection.ManualMoveUntil = Time.time + 15f;
                    return;
                }

                // Non-drafted: existing chop/move + Day 68 crop harvest + Stretch Trade/Tame
                TreeEntity tree = (rhit != null) ? rhit.GetComponent<TreeEntity>() : null;
                CropEntity crop = (rhit != null) ? rhit.GetComponent<CropEntity>() : null;
                TraderEntity trader = (rhit != null) ? rhit.GetComponent<TraderEntity>() : null;
                AnimalEntity animalC = (rhit != null) ? rhit.GetComponent<AnimalEntity>() : null;
                if (trader != null)
                {
                    bool ok = trader.TryTrade();
                    Debug.Log($"[Trade] success={ok}");
                    return;
                }
                if (animalC != null)  // 비-drafted 시 동물 우클릭 = 길들이기 시도
                {
                    bool ok = animalC.TryTame();
                    Debug.Log($"[Tame] success={ok}");
                    return;
                }
                if (crop != null && crop.IsRipe)
                {
                    int food = crop.Harvest();
                    Debug.Log($"[Harvest] +{food} 식량");
                    return;
                }
                if (tree != null)
                {
                    PawnChopper chopper = currentSelection.GetComponent<PawnChopper>();
                    if (chopper != null) chopper.SetTreeTarget(tree);
                }
                else
                {
                    // 모든 task ClearTask (chopper/gatherer/hunter/cook) - 잔여 task override 방지
                    PawnChopper chopper = currentSelection.GetComponent<PawnChopper>();
                    if (chopper != null) chopper.ClearTask();
                    var gather = currentSelection.GetComponent<PawnGatherer>();
                    if (gather != null) gather.ClearTask();
                    var hunter = currentSelection.GetComponent<PawnHunter>();
                    if (hunter != null) hunter.ClearTask();
                    var cook = currentSelection.GetComponent<PawnCook>();
                    if (cook != null) cook.ClearTask();
                    PawnMovement mv = currentSelection.GetComponent<PawnMovement>();
                    if (mv != null) mv.SetTarget(new Vector2(mouseWorld.x, mouseWorld.y));
                    // 수동 이동 명령 → AI 5초 skip (즉시 override 방지)
                    currentSelection.ManualMoveUntil = Time.time + 15f;
                }
            }
        }

        // #113 - 림월드 우클릭 prioritize 메뉴 아이템 build (undrafted 만)
        private System.Collections.Generic.List<(string, System.Action)> BuildContextMenu(
            Collider2D hit, Vector3 worldPos)
        {
            var list = new System.Collections.Generic.List<(string, System.Action)>();
            if (currentSelection == null) return list;
            var pawn = currentSelection;
            var tree = hit.GetComponent<TreeEntity>();
            var bush = hit.GetComponent<BerryBushEntity>();
            var crop = hit.GetComponent<CropEntity>();
            var animal = hit.GetComponent<AnimalEntity>();
            var trader = hit.GetComponent<TraderEntity>();
            var stove = hit.GetComponent<StoveEntity>();
            var bench = hit.GetComponent<ResearchBench>();
            var vein = hit.GetComponent<StoneVeinEntity>();  // #119
            var bandit = hit.GetComponent<BanditEnemy>();    // #135
            var stockpile = hit.GetComponent<StockpileZoneEntity>();  // #155
            if (tree != null)
            {
                list.Add(("⛏ 벌목 우선", () => {
                    var ch = pawn.GetComponent<PawnChopper>();
                    if (ch != null) { ch.SetTreeTarget(tree); pawn.ManualMoveUntil = Time.time + 8f; }
                }));
            }
            if (bandit != null && bandit.IsDowned)
            {
                var bcap = bandit;
                list.Add(("🔒 포섭 시도 (50%)", () => bcap.TryCapture()));
            }
            if (vein != null && !vein.IsDestroyed)
            {
                list.Add(("⛏ 채광 우선", () => {
                    var m = pawn.GetComponent<PawnMiner>();
                    if (m != null) { m.SetVeinTarget(vein); pawn.ManualMoveUntil = Time.time + 8f; }
                }));
            }
            if (bush != null && !bush.IsDepleted)
            {
                list.Add(("🍇 채집 우선", () => {
                    var g = pawn.GetComponent<PawnGatherer>();
                    if (g != null) { g.SetBushTarget(bush); pawn.ManualMoveUntil = Time.time + 8f; }
                }));
            }
            if (crop != null && crop.IsRipe)
            {
                list.Add(("🌾 수확", () => crop.Harvest()));
            }
            if (animal != null && !animal.IsDead)
            {
                list.Add(("🎯 길들이기 시도", () => animal.TryTame()));
                list.Add(("🏹 사냥 (드래프트 필요)", () => {
                    pawn.SetDrafted(true);
                    pawn.DraftedHuntTarget = animal;
                }));
            }
            if (trader != null)
            {
                // #133 - 6 거래 옵션
                var traderCap = trader;
                for (int i = 0; i < TraderEntity.TradeOptions.Length; i++)
                {
                    int idx = i;
                    list.Add(($"🛒 {TraderEntity.TradeOptions[i].label}",
                        () => traderCap.TryTrade(idx)));
                }
            }
            if (stove != null)
            {
                list.Add(("🍳 요리 우선", () => {
                    var c = pawn.GetComponent<PawnCook>();
                    if (c != null) { c.SetStoveTarget(stove); pawn.ManualMoveUntil = Time.time + 8f; }
                }));
            }
            if (bench != null)
            {
                list.Add(("📚 연구 (옆에 가서 대기)", () => {
                    var m = pawn.GetComponent<PawnMovement>();
                    if (m != null) m.SetTarget(bench.transform.position);
                    pawn.ManualMoveUntil = Time.time + 8f;
                }));
            }
            if (stockpile != null)
            {
                // #155 - stockpile priority 순환 (Low → Normal → Preferred → Important → Critical → Low ...)
                var spCap = stockpile;
                list.Add(($"📦 우선순위: {spCap.PriorityKr} → 다음 단계",
                    () => { if (spCap != null) spCap.CyclePriority(); }));
            }
            return list;
        }

        // 통합 검증용 - 실제 mouse input 시뮬레이션 (IntegrationTestRunner 호출)
        public void SimulateSelect(PawnEntity pawn) { Select(pawn); }

        /// <summary>#192 - context menu item 진짜 호출 (vein 채광 / bandit 포섭 등 context-menu-only action).
        ///  worldPos 에서 entity hit + 같은 menu 생성 → label 포함 item 찾아서 action 실행.
        ///  미발견 시 false.</summary>
        public bool SimulateContextMenuAction(Vector2 worldPos, string itemLabelContains)
        {
            if (currentSelection == null) return false;
            Vector3 mouseWorld = new Vector3(worldPos.x, worldPos.y, 0f);
            Collider2D hit = Physics2D.OverlapPoint(mouseWorld);
            if (hit == null) return false;
            var items = BuildContextMenu(hit, mouseWorld);
            if (items == null) return false;
            foreach (var (label, action) in items)
            {
                if (label.Contains(itemLabelContains))
                {
                    action?.Invoke();
                    return true;
                }
            }
            return false;
        }

        public void SimulateRightClick(Vector2 worldPos)
        {
            if (currentSelection == null) return;
            Vector3 mouseWorld = new Vector3(worldPos.x, worldPos.y, 0f);
            ClickEffect.Spawn(mouseWorld, new Color(0.3f, 0.9f, 1f, 0.95f));  // 통합 검증 - 파란 X
            Collider2D rhit = Physics2D.OverlapPoint(mouseWorld);
            if (currentSelection.IsDrafted)
            {
                // drafted 분기: 적/늑대/동물 우선
                if (rhit != null)
                {
                    BanditEnemy bandit = rhit.GetComponent<BanditEnemy>();
                    WolfEnemy wolf = rhit.GetComponent<WolfEnemy>();
                    AnimalEntity animal = rhit.GetComponent<AnimalEntity>();
                    if (bandit != null) { currentSelection.DraftedAttackTarget = bandit; return; }
                    if (wolf != null) { currentSelection.DraftedWolfTarget = wolf; return; }
                    if (animal != null) { currentSelection.DraftedHuntTarget = animal; return; }
                }
                var mvD = currentSelection.GetComponent<PawnMovement>();
                if (mvD != null) mvD.SetTarget(worldPos);
                currentSelection.ManualMoveUntil = Time.time + 15f;
                return;
            }
            // 비-drafted: trader/animal/crop/tree/empty
            TraderEntity trader = (rhit != null) ? rhit.GetComponent<TraderEntity>() : null;
            AnimalEntity animalC = (rhit != null) ? rhit.GetComponent<AnimalEntity>() : null;
            CropEntity crop = (rhit != null) ? rhit.GetComponent<CropEntity>() : null;
            TreeEntity tree = (rhit != null) ? rhit.GetComponent<TreeEntity>() : null;
            if (trader != null) { trader.TryTrade(); return; }
            if (animalC != null) { animalC.TryTame(); return; }
            if (crop != null && crop.IsRipe) { crop.Harvest(); return; }
            if (tree != null)
            {
                var chopper = currentSelection.GetComponent<PawnChopper>();
                if (chopper != null) chopper.SetTreeTarget(tree);
                return;
            }
            // 통합 검증 I2 결과 — chopper 만 ClearTask 했더니 잔여 gatherer/hunter/cook task 가
            //  movement.SetTarget(자기 target) 호출해서 사용자 target 무시됨.  전부 ClearTask.
            var chopperE = currentSelection.GetComponent<PawnChopper>();
            if (chopperE != null) chopperE.ClearTask();
            var gatherE = currentSelection.GetComponent<PawnGatherer>();
            if (gatherE != null) gatherE.ClearTask();
            var hunterE = currentSelection.GetComponent<PawnHunter>();
            if (hunterE != null) hunterE.ClearTask();
            var cookE = currentSelection.GetComponent<PawnCook>();
            if (cookE != null) cookE.ClearTask();
            var mv = currentSelection.GetComponent<PawnMovement>();
            if (mv != null) mv.SetTarget(worldPos);
            currentSelection.ManualMoveUntil = Time.time + 15f;
        }

        private void Select(PawnEntity pawn)
        {
            // 같은 pawn 다시 클릭하면 camera 재 focus 만 (안 보일 때 다시 찾기)
            bool sameAsPrev = (currentSelection == pawn);
            if (!sameAsPrev)
            {
                if (currentSelection != null) currentSelection.SetSelected(false);
                currentSelection = pawn;
                currentSelection.SetSelected(true);
            }
            // 운영자 피드백 - pawn 이 AI wander 로 화면 밖 → 선택 시 부드럽게 camera focus
            //  same pawn 재선택도 focus 트리거 (재중심)
            var camForFocus = mainCamera != null ? mainCamera : Camera.main;
            var cc = camForFocus != null ? camForFocus.GetComponent<CameraController>() : null;
            if (cc == null) cc = Object.FindFirstObjectByType<CameraController>();
            if (cc != null) cc.RequestFocus(pawn);
        }

        private void ClearSelection()
        {
            if (currentSelection == null) return;
            currentSelection.SetSelected(false);
            currentSelection = null;
        }
    }
}
