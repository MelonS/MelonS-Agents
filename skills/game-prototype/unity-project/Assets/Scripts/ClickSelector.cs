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

        // #227 운영자 fb "조작 제대로 안됨" — 단일 OverlapPoint 는 한 점에 여러 콜라이더가
        //  겹칠 때 actionable 하지 않은 것(바닥 데코 등)을 반환할 수 있어 우클릭 메뉴/작업이
        //  안 잡히는 사례(예: 채광).  OverlapPointAll 로 '작업 가능한 엔티티'를 우선 선택.
        private static readonly System.Type[] _actionableTypes = {
            typeof(PawnEntity), typeof(TreeEntity), typeof(StoneVeinEntity), typeof(CropEntity),
            typeof(BerryBushEntity), typeof(AnimalEntity), typeof(BanditEnemy), typeof(WolfEnemy),
            typeof(BedEntity), typeof(BlueprintEntity), typeof(WoodPileEntity), typeof(StoneChunkEntity),
            typeof(MeatPileEntity), typeof(StoveEntity), typeof(ResearchBench), typeof(TraderEntity),
            typeof(StockpileZoneEntity)
        };
        private static bool IsActionable(Collider2D c)
        {
            foreach (var t in _actionableTypes)
                if (c.GetComponent(t) != null) return true;
            return false;
        }
        private Collider2D PickEntityAt(Vector2 world)
        {
            var hits = Physics2D.OverlapPointAll(world);
            if (hits == null || hits.Length == 0) return null;
            if (hits.Length == 1) return hits[0];
            // #227 - 여러 콜라이더 겹침: actionable 엔티티 우선 + 그 중 클릭 지점에 중심이
            //  가장 가까운 것 선택(클릭한 바로 그 오브젝트가 의도일 확률 최고).  단일 actionable
            //  우선 로직이 '첫 번째'를 골라 밴딧 공격 등이 엉뚱한 엔티티를 잡던 회귀 fix.
            Collider2D best = null; float bestSq = float.MaxValue; bool bestAct = false;
            foreach (var h in hits)
            {
                if (h == null) continue;
                bool act = IsActionable(h);
                float sq = ((Vector2)h.transform.position - world).sqrMagnitude;
                if (best == null || (act && !bestAct) || (act == bestAct && sq < bestSq))
                { best = h; bestSq = sq; bestAct = act; }
            }
            return best;
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
                Collider2D hit = PickEntityAt(mouseWorld);
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
                Collider2D ehit = PickEntityAt(mw);
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
                Collider2D rhit = PickEntityAt(mouseWorld);

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
                // rcfix - 건설중 벽(blueprint) / 목재침대(bed) 직접 우클릭 단일 행동
                BlueprintEntity bp = (rhit != null) ? rhit.GetComponent<BlueprintEntity>() : null;
                BedEntity bed = (rhit != null) ? rhit.GetComponent<BedEntity>() : null;
                BerryBushEntity bushC = (rhit != null) ? rhit.GetComponent<BerryBushEntity>() : null;
                WoodPileEntity pileC = (rhit != null) ? rhit.GetComponent<WoodPileEntity>() : null;
                StoneVeinEntity veinC = (rhit != null) ? rhit.GetComponent<StoneVeinEntity>() : null;  // #219 우클릭 채광
                if (bp != null && !bp.IsComplete)
                {
                    // 건설중 벽 우클릭 = 그 blueprint 를 짓도록 명령 (PawnBuilder).
                    var b = currentSelection.GetComponent<PawnBuilder>();
                    if (b != null)
                    {
                        var nr = currentSelection.GetComponent<PawnNeeds>();
                        if (nr != null) nr.ClearRestTarget();  // 휴식 명령 취소
                        b.SetBlueprintTarget(bp);
                        currentSelection.ManualMoveUntil = Time.time + 12f;
                        Debug.Log($"[Build] {currentSelection.PawnName} → 건설 ({bp.Mode})");
                    }
                    return;
                }
                if (bed != null)
                {
                    // 목재침대 우클릭 = 그 침대로 가서 수면 (PawnNeeds.SetRestTarget).
                    var nr = currentSelection.GetComponent<PawnNeeds>();
                    if (nr != null)
                    {
                        ClearAllWorkTasks(currentSelection);  // 잔여 작업 override 방지
                        nr.SetRestTarget(bed);
                        var mv2 = currentSelection.GetComponent<PawnMovement>();
                        if (mv2 != null) mv2.SetTarget(bed.transform.position);
                        currentSelection.ManualMoveUntil = Time.time + 30f;  // 침대 도착까지 AI skip
                        Debug.Log($"[Rest] {currentSelection.PawnName} → 수면 ({bed.QualityKr})");
                    }
                    return;
                }
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
                    // #219 운영자 fb "우클릭 강제지정 잘 안됨" — 과거엔 crop.Harvest() 를 즉시
                    //  호출해 선택 림이 가지도 않고 수확됐다(텔레포트 수확).  이제 PawnHarvester
                    //  에 명령 → 림이 작물로 걸어가 수확(물리).  ManualMoveUntil 로 AI override 방지.
                    var hv = currentSelection.GetComponent<PawnHarvester>();
                    if (hv != null)
                    {
                        ClearAllWorkTasks(currentSelection);
                        hv.SetCropTarget(crop);
                        currentSelection.ManualMoveUntil = Time.time + 10f;
                        Debug.Log($"[Harvest] {currentSelection.PawnName} → 수확 명령");
                    }
                    return;
                }
                if (bushC != null && !bushC.IsDepleted)
                {
                    var g = currentSelection.GetComponent<PawnGatherer>();
                    if (g != null) { g.SetBushTarget(bushC); currentSelection.ManualMoveUntil = Time.time + 8f; }
                    Debug.Log($"[Gather] {currentSelection.PawnName} → 채집");
                    return;
                }
                if (pileC != null)
                {
                    var h = currentSelection.GetComponent<PawnHauler>();
                    if (h != null) { h.SetPileTarget(pileC); currentSelection.ManualMoveUntil = Time.time + 8f; }
                    Debug.Log($"[Haul] {currentSelection.PawnName} → 운반");
                    return;
                }
                if (veinC != null)
                {
                    // #219 운영자 fb — 광맥 우클릭 = 그 림이 채광하러 가도록 명령 (과거엔
                    //  직접 핸들러에 없어 이동 명령으로 빠졌음 = 채광 강제지정 불가).
                    var mn = currentSelection.GetComponent<PawnMiner>();
                    if (mn != null)
                    {
                        ClearAllWorkTasks(currentSelection);
                        mn.SetVeinTarget(veinC);
                        currentSelection.ManualMoveUntil = Time.time + 12f;
                        Debug.Log($"[Mine] {currentSelection.PawnName} → 채광 명령");
                    }
                    return;
                }
                if (tree != null)
                {
                    // #219 - 벌목 강제지정에도 ManualMoveUntil 부여 (AI 가 즉시 다른 일로
                    //  override 하지 않도록 — 다른 우클릭 명령과 동일하게 존중).
                    PawnChopper chopper = currentSelection.GetComponent<PawnChopper>();
                    if (chopper != null)
                    {
                        ClearAllWorkTasks(currentSelection);
                        chopper.SetTreeTarget(tree);
                        currentSelection.ManualMoveUntil = Time.time + 12f;
                        Debug.Log($"[Chop] {currentSelection.PawnName} → 벌목 명령");
                    }
                }
                else
                {
                    // 모든 task ClearTask - 잔여 task override 방지 (rcfix: rest 명령도 취소)
                    ClearAllWorkTasks(currentSelection);
                    PawnMovement mv = currentSelection.GetComponent<PawnMovement>();
                    if (mv != null) mv.SetTarget(new Vector2(mouseWorld.x, mouseWorld.y));
                    // 수동 이동 명령 → AI 5초 skip (즉시 override 방지)
                    currentSelection.ManualMoveUntil = Time.time + 15f;
                }
            }
        }

        // rcfix - 모든 작업/휴식 task 취소 (사용자 명령이 잔여 AI task 에 override 되는 것 방지).
        //  chopper/gatherer/hunter/cook/builder.ClearTask + needs.ClearRestTarget.
        private void ClearAllWorkTasks(PawnEntity pawn)
        {
            if (pawn == null) return;
            var chopper = pawn.GetComponent<PawnChopper>();
            if (chopper != null) chopper.ClearTask();
            var gather = pawn.GetComponent<PawnGatherer>();
            if (gather != null) gather.ClearTask();
            var hunter = pawn.GetComponent<PawnHunter>();
            if (hunter != null) hunter.ClearTask();
            var cook = pawn.GetComponent<PawnCook>();
            if (cook != null) cook.ClearTask();
            var builder = pawn.GetComponent<PawnBuilder>();
            if (builder != null) builder.ClearTask();
            var needs = pawn.GetComponent<PawnNeeds>();
            if (needs != null) needs.ClearRestTarget();
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
            var bp = hit.GetComponent<BlueprintEntity>();    // rcfix - 건설중
            var bed = hit.GetComponent<BedEntity>();         // rcfix - 수면
            var pile = hit.GetComponent<WoodPileEntity>();   // rcfix - 운반
            if (bp != null && !bp.IsComplete)
            {
                var bpCap = bp;
                list.Add(("🔨 건설 우선", () => {
                    var b = pawn.GetComponent<PawnBuilder>();
                    if (b != null)
                    {
                        var nr = pawn.GetComponent<PawnNeeds>();
                        if (nr != null) nr.ClearRestTarget();
                        b.SetBlueprintTarget(bpCap);
                        pawn.ManualMoveUntil = Time.time + 12f;
                    }
                }));
            }
            if (bed != null)
            {
                var bedCap = bed;
                list.Add(($"🛏 수면 ({bed.QualityKr})", () => {
                    var nr = pawn.GetComponent<PawnNeeds>();
                    if (nr != null)
                    {
                        ClearAllWorkTasks(pawn);
                        nr.SetRestTarget(bedCap);
                        var mv = pawn.GetComponent<PawnMovement>();
                        if (mv != null) mv.SetTarget(bedCap.transform.position);
                        pawn.ManualMoveUntil = Time.time + 30f;
                    }
                }));
            }
            if (pile != null)
            {
                var pileCap = pile;
                list.Add(("📦 운반 우선", () => {
                    var h = pawn.GetComponent<PawnHauler>();
                    if (h != null) { h.SetPileTarget(pileCap); pawn.ManualMoveUntil = Time.time + 8f; }
                }));
            }
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
                var cropCap = crop;
                list.Add(("🌾 수확 우선", () => {
                    // #226 일관성 — 다른 메뉴 항목처럼 림이 걸어가서 수확 (과거엔 즉시 Harvest).
                    var hv = pawn.GetComponent<PawnHarvester>();
                    if (hv != null) { ClearAllWorkTasks(pawn); hv.SetCropTarget(cropCap); pawn.ManualMoveUntil = Time.time + 10f; }
                }));
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
            Collider2D hit = PickEntityAt(mouseWorld);
            if (hit == null) { Debug.Log($"[CtxQA] PickEntityAt null at {mouseWorld}"); return false; }
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
            Debug.Log($"[CtxQA] '{itemLabelContains}' 미발견. hit={hit.gameObject.name} items=[{string.Join(",", items.ConvertAll(i => i.Item1))}]");
            return false;
        }

        public void SimulateRightClick(Vector2 worldPos)
        {
            if (currentSelection == null) return;
            Vector3 mouseWorld = new Vector3(worldPos.x, worldPos.y, 0f);
            ClickEffect.Spawn(mouseWorld, new Color(0.3f, 0.9f, 1f, 0.95f));  // 통합 검증 - 파란 X
            Collider2D rhit = PickEntityAt(mouseWorld);
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
            // 비-drafted: blueprint/bed/trader/animal/crop/bush/pile/tree/empty
            TraderEntity trader = (rhit != null) ? rhit.GetComponent<TraderEntity>() : null;
            AnimalEntity animalC = (rhit != null) ? rhit.GetComponent<AnimalEntity>() : null;
            CropEntity crop = (rhit != null) ? rhit.GetComponent<CropEntity>() : null;
            TreeEntity tree = (rhit != null) ? rhit.GetComponent<TreeEntity>() : null;
            BlueprintEntity bp = (rhit != null) ? rhit.GetComponent<BlueprintEntity>() : null;
            BedEntity bed = (rhit != null) ? rhit.GetComponent<BedEntity>() : null;
            BerryBushEntity bushC = (rhit != null) ? rhit.GetComponent<BerryBushEntity>() : null;
            WoodPileEntity pileC = (rhit != null) ? rhit.GetComponent<WoodPileEntity>() : null;
            if (bp != null && !bp.IsComplete)
            {
                var b = currentSelection.GetComponent<PawnBuilder>();
                if (b != null)
                {
                    var nr = currentSelection.GetComponent<PawnNeeds>();
                    if (nr != null) nr.ClearRestTarget();
                    b.SetBlueprintTarget(bp);
                    currentSelection.ManualMoveUntil = Time.time + 12f;
                }
                return;
            }
            if (bed != null)
            {
                var nr = currentSelection.GetComponent<PawnNeeds>();
                if (nr != null)
                {
                    ClearAllWorkTasks(currentSelection);
                    nr.SetRestTarget(bed);
                    var mvB = currentSelection.GetComponent<PawnMovement>();
                    if (mvB != null) mvB.SetTarget(bed.transform.position);
                    currentSelection.ManualMoveUntil = Time.time + 30f;
                }
                return;
            }
            if (trader != null) { trader.TryTrade(); return; }
            if (animalC != null) { animalC.TryTame(); return; }
            if (crop != null && crop.IsRipe)
            {
                // #227 - 실제 Update 핸들러와 동일하게 PawnHarvester 명령(물리수확).
                var hv = currentSelection.GetComponent<PawnHarvester>();
                if (hv != null) { ClearAllWorkTasks(currentSelection); hv.SetCropTarget(crop); currentSelection.ManualMoveUntil = Time.time + 10f; }
                return;
            }
            if (bushC != null && !bushC.IsDepleted)
            {
                var g = currentSelection.GetComponent<PawnGatherer>();
                if (g != null) { g.SetBushTarget(bushC); currentSelection.ManualMoveUntil = Time.time + 8f; }
                return;
            }
            if (pileC != null)
            {
                var h = currentSelection.GetComponent<PawnHauler>();
                if (h != null) { h.SetPileTarget(pileC); currentSelection.ManualMoveUntil = Time.time + 8f; }
                return;
            }
            if (tree != null)
            {
                var chopper = currentSelection.GetComponent<PawnChopper>();
                if (chopper != null) chopper.SetTreeTarget(tree);
                return;
            }
            // 통합 검증 I2 결과 — chopper 만 ClearTask 했더니 잔여 gatherer/hunter/cook task 가
            //  movement.SetTarget(자기 target) 호출해서 사용자 target 무시됨.  전부 ClearTask.
            ClearAllWorkTasks(currentSelection);
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
