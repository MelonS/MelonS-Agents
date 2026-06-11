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
        private CameraController _cachedCameraController;   // #audit4 #4 — focus 폴백 캐시

        private PawnEntity currentSelection;
        public PawnEntity CurrentSelection => currentSelection;

        // #105 - 비-pawn 오브젝트 inspect.  EntityInspectorPanel 폴링.
        private GameObject currentInspect;
        public GameObject CurrentInspect => currentInspect;
        // #버그헌트(2026-06-03): 마퀴 드래그 시작 시 비-pawn inspect 패널도 해제하도록 공개 API.
        public void ClearInspect() => currentInspect = null;

        // 진단용(-inspectpawn): 첫 PawnEntity 를 선택해 인스펙트 패널을 띄운다 → 스크린샷 검증.
        public void DiagnosticInspectFirstPawn()
        {
            var p = Object.FindFirstObjectByType<PawnEntity>();
            if (p != null) { Select(p); currentInspect = p.gameObject; }
        }

        public PawnEntity DiagnosticSelected => currentSelection;

        // 진단용(-pawndiag): 선택된 림에게 우클릭 수동이동과 *동일한 실제 경로*로 이동 명령.
        //  (ClickSelector 우클릭 핸들러 L317-323 과 같은 코드: 잔여작업 clear + SetTarget +
        //   ManualMoveUntil → AI override 방지).  선택-림 처리 검증용.
        public bool DiagnosticCommandMove(Vector2 worldTarget)
        {
            if (currentSelection == null) return false;
            ClearAllWorkTasks(currentSelection);
            var mv = currentSelection.GetComponent<PawnMovement>();
            if (mv != null) mv.SetTarget(worldTarget);
            currentSelection.ManualMoveUntil = Time.time + 15f;
            return true;
        }

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
                // 거리 동률(겹친 동일위치)일 때 InstanceID 로 결정적 tie-break — 같은 클릭이
                //  호출마다 같은 엔티티를 반환하게 해 메뉴/선택 깜빡임을 막는다(운영자 fb).
                bool better = best == null || (act && !bestAct)
                    || (act == bestAct && (sq < bestSq - 1e-4f
                        || (Mathf.Abs(sq - bestSq) < 1e-4f && h.GetInstanceID() < best.GetInstanceID())));
                if (better) { best = h; bestSq = sq; bestAct = act; }
            }
            return best;
        }

        private void Update()
        {
            // UI 위 클릭 무시 (운영자 피드백 - UI 가 가로채던 문제)
            // WORKFLOW-V2: 입력은 SimInput 경유 — 평시 Input 패스스루, -repro 시뮬 주입.
            bool overUI = SimInput.IsPointerOverUI();
            // 빌드 모드 활성이면 mouse click 은 BuildManager 가 처리 (place / cancel)
            bool buildActive = BuildManager.Instance != null && BuildManager.Instance.BuildModeActive;
            // r2 #2 (2026-06-12) — 지정 모드 7종 중 활성이 있으면 월드 클릭은 그 매니저
            //  소유 (해제 우클릭이 같은 프레임 이동/공격 명령으로 새던 레이스 차단).
            bool designationActive =
                (TreeChopDesignation.Instance != null && TreeChopDesignation.Instance.ModeActive)
                || (MineDesignation.Instance != null && MineDesignation.Instance.ModeActive)
                || (DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive)
                || (GrowZoneDesignation.Instance != null && GrowZoneDesignation.Instance.ModeActive)
                || (StockpileDesignation.Instance != null && StockpileDesignation.Instance.ModeActive)
                || (RoofDesignation.Instance != null && RoofDesignation.Instance.ModeActive);
            buildActive = buildActive || designationActive;
            // 우클릭은 컨텍스트 메뉴 열림 중에도 월드로 새지 않게 (메뉴가 소비).
            bool ctxOpen = ContextMenuUI.Instance != null && ContextMenuUI.Instance.IsOpen;

            // Left click = select
            if (SimInput.GetMouseButtonDown(0) && !overUI && !buildActive)
            {
                Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(SimInput.mousePosition);
                mouseWorld.z = 0f;
                Collider2D hit = PickEntityAt(mouseWorld);
                PawnEntity pawn = (hit != null) ? hit.GetComponent<PawnEntity>() : null;
                // r2 #7 — 시체는 선택 대상이 아니라 inspect 대상 (죽은 림이 클릭 1순위로
                //  생존 림/아이템을 가로채던 것).
                if (pawn != null && pawn.IsDead) pawn = null;
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
                    // 운영자 fb — 나무/베리덤불/광맥/청사진 등 상호작용 가능한 엔티티는 좌클릭 시
                    //  inspect 와 함께 '플로트 액션 메뉴'(벌목/채집/채광/취소 등)를 띄운다.
                    //  the reference sim 처럼 선택만으로 작업 지정에 접근 가능.  지정 액션은 림 선택 불필요
                    //  (designation manager 가 idle 림 dispatch).  메뉴 항목이 없으면 inspect 만.
                    if (ContextMenuUI.Instance != null)
                    {
                        var litems = BuildLeftClickMenu(hit, mouseWorld);
                        if (litems != null && litems.Count > 0)
                            ContextMenuUI.Instance.Open(SimInput.mousePosition, litems);
                        else if (ContextMenuUI.Instance.IsOpen)
                            ContextMenuUI.Instance.Close();
                    }
                }
                else {
                    // #34 보강: PickEntityAt 가 작은 콜라이더(나무 16x16 등)를 tie-break 로
                    //  놓쳐 hit==null 이 돼도, 클릭점에 actionable 엔티티가 겹쳐 있으면
                    //  좌클릭 메뉴(벌목/채광/채집)를 띄운다.  BuildLeftClickMenu 는 내부에서
                    //  OverlapPointAll 로 다시 훑으므로 hit=null 로 호출해도 동작.
                    var litems = (ContextMenuUI.Instance != null)
                        ? BuildLeftClickMenu(null, mouseWorld) : null;
                    if (litems != null && litems.Count > 0)
                        ContextMenuUI.Instance.Open(SimInput.mousePosition, litems);
                    else {
                        ClearSelection(); currentInspect = null;
                        if (ContextMenuUI.Instance != null && ContextMenuUI.Instance.IsOpen)
                            ContextMenuUI.Instance.Close();
                    }
                }
            }

            // Day 48: R key toggles drafted on selected pawn.
            // 운영자 fb "징집은 여러 림 한꺼번에 선택해 쓰는 기능인데 여러 마리 선택 시 안 됨" —
            //  MarqueeSelector 로 다중 선택돼 있으면 R 키 징집을 *선택된 전원*에게 적용한다.
            //  단일 선택(currentSelection) 은 기존대로.  GizmoBar 의 OnDraftClicked 와 동일하게
            //  첫 림 상태 기준으로 토글 target 을 정해 전원 일괄 적용(혼재 상태 → 일괄 ON).
            if (SimInput.GetKeyDown(KeyCode.R))
            {
                ToggleDraftOnSelection();
            }

            // #233 ★진짜 원인(진단 로그 sel=False): 우클릭 벌목/채광이 'currentSelection!=null'
            //  게이트에 막혀 림이 선택 안 돼 있으면 안 됐다.  the reference sim 에선 벌목/채광 *지정*은
            //  림 선택이 불필요(Architect>Orders 처럼).  → 선택 무관하게 우클릭 나무/광맥 = 바로
            //  지정(🪓/⛏ 마커 + idle 림 dispatch).  이 블록을 selection 게이트 밖에 둔다.
            if (SimInput.GetMouseButtonDown(1) && !overUI && !buildActive && !ctxOpen)
            {
                Vector3 dmw = mainCamera.ScreenToWorldPoint(SimInput.mousePosition); dmw.z = 0f;
                Collider2D dhit = PickEntityAt(dmw);
                if (dhit != null)
                {
                    // #38 재발(2026-06-10 실플레이 Player.log + marquee 재현): #233 이 이 블록을
                    //  '지정-only + return' 으로 만들면서 아래 tree/vein 직접명령 분기(L345/L331)가
                    //  도달 불가 사문화 — 지정만 깔리고 임의 림의 자율 AI 가 집어갔다.  마키 선택
                    //  시엔 move-order 가 선택 림 AI 를 15s 봉인해 100% 타 림이 가져감.  지정은
                    //  유지하되(마커/세이브/자율경로 호환) 선택 림이 있으면 즉시 직접 배정한다.
                    //  선택 없음 = #233 그대로 (지정 → 자율 ChopTreeAction).
                    if (dhit.GetComponent<TreeEntity>() != null && TreeChopDesignation.Instance != null
                        && TreeChopDesignation.Instance.TryMark(dhit.gameObject) != null)
                    {
                        Debug.Log("[Chop] 우클릭 벌목 지정");
                        TryOrderSelectedChop(dhit.GetComponent<TreeEntity>());
                        return;
                    }
                    if (dhit.GetComponent<StoneVeinEntity>() != null && MineDesignation.Instance != null
                        && MineDesignation.Instance.TryMark(dhit.gameObject) != null)
                    {
                        Debug.Log("[Mine] 우클릭 채광 지정");
                        TryOrderSelectedMine(dhit.GetComponent<StoneVeinEntity>());
                        return;
                    }
                }
            }
            // Right click = move OR chop OR attack (drafted) for selected pawn
            //   buildActive 면 BuildManager 가 우클릭 = cancel 처리 (overlap 방지)
            //   #113 - undrafted + entity hit = the reference sim 스타일 "Prioritize" 컨텍스트 메뉴
            if (SimInput.GetMouseButtonDown(1) && !overUI && !buildActive && !ctxOpen && currentSelection != null
                && !currentSelection.IsDrafted && !MarqueeOwnsCommand())   // #60 다중선택 양보
            {
                Vector3 mw = mainCamera.ScreenToWorldPoint(SimInput.mousePosition);
                mw.z = 0f;
                Collider2D ehit = PickEntityAt(mw);
                // #275 나무/광맥 우클릭 벌목·채광 지정은 위 선택-무관 블록(line 118-131)이 이미
                //  모든 케이스를 처리하고 return 하므로, 여기 중복 TryMark 는 제거(dead).  이
                //  블록은 '나무/광맥이 아닌' 엔티티의 컨텍스트 메뉴만 담당한다.
                if (ehit != null && ContextMenuUI.Instance != null)
                {
                    var items = BuildContextMenu(ehit, mw);
                    if (items != null && items.Count > 0)
                    {
                        // WORKFLOW-V2 진단 — 우클릭이 이동 대신 메뉴로 빠진 케이스 가시화
                        Debug.Log($"[RClick] {currentSelection.PawnName} 우클릭({mw.x:F1},{mw.y:F1}) hit={ehit.name} → 컨텍스트메뉴 ({items.Count}항목, 이동 아님)");
                        ContextMenuUI.Instance.Open(SimInput.mousePosition, items);
                        return;  // 메뉴 떴음 - 기존 직접 action skip
                    }
                }
                // entity 없거나 메뉴 없으면 기존 동작 (manual move)
            }
            if (SimInput.GetMouseButtonDown(1) && !overUI && !buildActive && !ctxOpen && currentSelection != null
                && !MarqueeOwnsCommand())   // #60 다중선택 중엔 MarqueeSelector 가 그룹 move 소유
            {
                Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(SimInput.mousePosition);
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
                if (animalC != null && !animalC.IsDead)  // r2 #7 — 시체 길들이기 차단
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
                    // WORKFLOW-V2 진단 — P0 '림 이동 안 됨': 명령 등록 여부를 로그로 확정
                    Debug.Log($"[Move] {currentSelection.PawnName} 수동이동 명령 ({mouseWorld.x:F1},{mouseWorld.y:F1}) mv={(mv != null)}");
                }
            }
        }

        // ── 다중 선택 징집 ────────────────────────────────────────────────────
        //  MarqueeSelector(있고 다중 선택 중)이면 그 전원에게, 아니면 currentSelection 단일에게
        //  Draft 토글을 적용.  토글 target 은 '첫 림이 현재 drafted 인가'의 반대(전원 일괄) —
        //  SelectionGizmoBar.OnDraftClicked 와 동일 규약.  PawnEntity.SetDrafted 는 R 키가
        //  쓰던 바로 그 공개 API(새 징집 로직 없음).
        private MarqueeSelector cachedMarquee;
        private MarqueeSelector FindMarquee()
        {
            if (cachedMarquee != null) return cachedMarquee;
#if UNITY_2023_1_OR_NEWER
            cachedMarquee = Object.FindFirstObjectByType<MarqueeSelector>();
#else
            cachedMarquee = Object.FindObjectOfType<MarqueeSelector>();
#endif
            return cachedMarquee;
        }

        // #60 다중선택 충돌 방지: marquee 가 2+ 림을 선택 중이면 우클릭 그룹 move 는 MarqueeSelector
        //  (formation fan-out)가 소유한다.  ClickSelector 의 단일-currentSelection 우클릭(이동/공격/
        //  컨텍스트)이 동시에 발화하면 currentSelection 이 formation 셀 + raw 클릭점 이중명령을 받아
        //  엉뚱/겹침("여러 림 명령 시 일부 엉뚱") → 다중선택 중엔 단일 경로를 양보(skip).
        private bool MarqueeOwnsCommand()
        {
            var m = FindMarquee();
            return m != null && m.HasMultiSelection;
        }

        /// <summary>#38 — 우클릭 지정 시 명령 받을 선택 림.  마키 다중선택이 있으면 그중
        ///  대상에서 가장 가까운 비징집·생존 림, 아니면 단일 currentSelection.  없으면 null
        ///  (지정만 → 자율 경로).</summary>
        private PawnEntity NearestCommandablePawn(Vector3 targetPos)
        {
            var m = FindMarquee();
            if (m != null && m.HasMultiSelection)
            {
                PawnEntity best = null; float bestSq = float.MaxValue;
                for (int i = 0; i < m.CurrentMultiSelection.Count; i++)
                {
                    var p = m.CurrentMultiSelection[i];
                    if (p == null || p.IsDead || p.IsDrafted) continue;
                    float sq = (p.transform.position - targetPos).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = p; }
                }
                return best;
            }
            if (currentSelection != null && !currentSelection.IsDead && !currentSelection.IsDrafted)
                return currentSelection;
            return null;
        }

        /// <summary>#38 — 선택 림에게 벌목 직접 배정.  SetTreeTarget 이 예약을 걸어
        ///  (f29b10f 키 일치) 타 림 자율 합류를 차단한다.  성공 시 true.</summary>
        private bool TryOrderSelectedChop(TreeEntity tree)
        {
            if (tree == null) return false;
            var p = NearestCommandablePawn(tree.transform.position);
            var ch = p != null ? p.GetComponent<PawnChopper>() : null;
            if (ch == null) return false;
            ClearAllWorkTasks(p);
            ch.SetTreeTarget(tree);
            p.ManualMoveUntil = Time.time + 12f;
            Debug.Log($"[Chop] {p.PawnName} → 벌목 명령 (우클릭 직접 배정)");
            return true;
        }

        /// <summary>#38 — 선택 림에게 채광 직접 배정 (벌목과 대칭).</summary>
        private bool TryOrderSelectedMine(StoneVeinEntity vein)
        {
            if (vein == null) return false;
            var p = NearestCommandablePawn(vein.transform.position);
            var mn = p != null ? p.GetComponent<PawnMiner>() : null;
            if (mn == null) return false;
            ClearAllWorkTasks(p);
            mn.SetVeinTarget(vein);
            p.ManualMoveUntil = Time.time + 12f;
            Debug.Log($"[Mine] {p.PawnName} → 채광 명령 (우클릭 직접 배정)");
            return true;
        }

        /// <summary>#ui백로그 0.0 — 하단바 [징집] 버튼이 재사용할 수 있게 public.
        ///  R 핫키와 GUI 버튼이 같은 경로(다중선택 전원 일괄)를 타야 동작 불일치가 없다.</summary>
        public void ToggleDraftOnSelection()
        {
            var marquee = FindMarquee();
            if (marquee != null && marquee.HasMultiSelection)
            {
                var sel = marquee.CurrentMultiSelection;
                // 첫 생존 림 기준으로 target 상태 결정 후 전원 일괄 적용.
                PawnEntity firstAlive = null;
                for (int i = 0; i < sel.Count; i++)
                    if (sel[i] != null && !sel[i].IsDead) { firstAlive = sel[i]; break; }
                if (firstAlive == null) return;
                bool target = !firstAlive.IsDrafted;
                int n = 0;
                for (int i = 0; i < sel.Count; i++)
                {
                    var p = sel[i];
                    if (p != null && !p.IsDead) { p.SetDrafted(target); n++; }
                }
                Debug.Log($"[Draft] 다중 선택 {n}명 → drafted={target}");
                return;
            }
            if (currentSelection != null)
                currentSelection.SetDrafted(!currentSelection.IsDrafted);
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
            // #회귀가드(2026-06-03): miner/harvester 누락 → 우클릭 재명령 시 이전 채광/수확
            //  task 가 안 풀려 예약 점유·작업 충돌(PawnEntity.ClearAllWorkTasks 엔 있는데
            //  여기만 빠졌던 불일치).  지정-구동 단일화로 채광 task 가 흔해져 영향 커짐.
            pawn.GetComponent<PawnMiner>()?.ClearTask();
            pawn.GetComponent<PawnHarvester>()?.ClearTask();
            var needs = pawn.GetComponent<PawnNeeds>();
            if (needs != null) needs.ClearRestTarget();
        }

        // #113 - 레퍼런스 콜로니심 우클릭 prioritize 메뉴 아이템 build (undrafted 만)
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

        // 운영자 fb #1~3 — 엔티티 *좌클릭 선택* 시 뜨는 플로트 액션 메뉴.
        //  BuildContextMenu(우클릭, 선택된 림에게 '우선' 명령)와 달리, 이쪽은 선택된 림이
        //  없어도 동작하는 *지정(designation)* 액션을 노출한다(the reference sim Orders 동등):
        //   - 나무   → 🪓 벌목 지정 (TreeChopDesignation.TryMark)  [fb #1]
        //   - 베리덤불 → 🍇 채집 지정 (PawnGatherer dispatch; 가까운 idle 림에게)  [fb #2]
        //   - 광맥   → ⛏ 채광 지정 (MineDesignation.TryMark)  [fb #3]
        //   - 청사진 → 🔨 건설 / ✕ 청사진 취소  [fb #3]
        //   - 구조물(벽/문/난로/침대) → ✕ 철거 지정 (DeconstructDesignation.TryMark)  [fb #3]
        //  저장공간(StockpileZoneEntity) 은 StockpileDesignation 의 전용 토글 toolbar 가
        //  좌클릭을 이미 처리하므로 여기서 메뉴를 만들지 않는다(중복/충돌 방지 — fb #4 참고).
        private System.Collections.Generic.List<(string, System.Action)> BuildLeftClickMenu(
            Collider2D hit, Vector3 worldPos)
        {
            var list = new System.Collections.Generic.List<(string, System.Action)>();
            // 운영자 2026-06-02 "나무 벌목 버튼이 깜빡": PickEntityAt 가 겹친 콜라이더 중
            //  단일 승자만 돌려줘(비결정 tie-break) 나무+광맥 겹친 칸에서 어떤 땐 나무, 어떤 땐
            //  광맥이 잡혀 항목이 나타났다 사라졌다 했다.  메뉴는 클릭점에 겹친 *모든* actionable
            //  을 훑어 각 액션을 넣는다 → 나무/광맥/철거가 동시에 떠 깜빡임 제거.
            var cols = Physics2D.OverlapPointAll(worldPos);
            if ((cols == null || cols.Length == 0) && hit != null) cols = new[] { hit };
            if (cols == null || cols.Length == 0) return list;

            var tree = FindIn<TreeEntity>(cols);
            if (tree != null && !tree.IsDestroyed && TreeChopDesignation.Instance != null)
            {
                var go = tree.gameObject;
                list.Add(("🪓 벌목 지정", () => {
                    if (TreeChopDesignation.Instance != null) TreeChopDesignation.Instance.TryMark(go);
                }));
            }

            var vein = FindIn<StoneVeinEntity>(cols);
            if (vein != null && !vein.IsDestroyed && MineDesignation.Instance != null)
            {
                var go = vein.gameObject;
                list.Add(("⛏ 채광 지정", () => {
                    if (MineDesignation.Instance != null) MineDesignation.Instance.TryMark(go);
                }));
            }

            var bush = FindIn<BerryBushEntity>(cols);
            // 운영자 "베리 채집 버튼이 없음": AI 가 다 따먹어 60s 재생창 동안 IsDepleted 라
            //  버튼이 통째로 사라졌다.  고갈/재생 중이어도 채집 지정 노출(레퍼런스 정합) →
            //  지정한 림이 덤불로 걸어가 재생되면 채집(DispatchGatherToIdle 가드도 완화).
            if (bush != null)
            {
                var bushCap = bush;
                list.Add(("🍇 채집 지정", () => DispatchGatherToIdle(bushCap)));
            }

            var bp = FindIn<BlueprintEntity>(cols);
            if (bp != null && !bp.IsComplete)
            {
                var bpCap = bp;
                list.Add(("🔨 건설 우선", () => PrioritizeBuild(bpCap)));
                list.Add(("✕ 청사진 취소", () =>
                {
                    if (bpCap == null) return;
                    // #버그헌트4(2026-06-05): 환불 경로 통일 — 채워진 자재를 물리 더미로 떨어뜨려 보존.
                    //  이전엔 raw Destroy 라 collected 자재가 (pickup 때 카운터 -차감됨 + 물리 미드롭)
                    //  양쪽에서 영구 증발했다(드래그-철거 취소는 환불되는데 컨텍스트 메뉴만 누수).
                    if (DeconstructDesignation.Instance != null)
                        DeconstructDesignation.Instance.CancelBlueprint(bpCap);
                    else
                        Destroy(bpCap.gameObject);
                }));
            }

            // 완성된 구조물(벽/문/난로/침대) → 철거 지정.  겹친 콜라이더 중 deconstructable 한
            //  첫 오브젝트(청사진 제외).
            if (DeconstructDesignation.Instance != null && bp == null)
            {
                foreach (var c in cols)
                {
                    if (c == null) continue;
                    var dgo = c.gameObject;
                    if (DeconstructTarget.IsDeconstructable(dgo))
                    {
                        list.Add(("✕ 철거 지정", () => {
                            if (DeconstructDesignation.Instance != null) DeconstructDesignation.Instance.TryMark(dgo);
                        }));
                        break;
                    }
                }
            }

            return list;
        }

        // 겹친 콜라이더들 중 컴포넌트 T 를 가진 첫 번째를 반환(없으면 null).  PickEntityAt 의
        //  단일-승자 비결정성을 메뉴 단계에서 무력화하기 위한 멀티-콜라이더 룩업.
        private static T FindIn<T>(Collider2D[] cols) where T : Component
        {
            if (cols == null) return null;
            foreach (var c in cols)
            {
                if (c == null) continue;
                var t = c.GetComponent<T>();
                if (t != null) return t;
            }
            return null;
        }

        // 베리덤불 채집 지정 — 가장 가까운 'idle' PawnGatherer 에게 직접 배정(designation manager 가
        //  없는 채집을 위한 dispatch).  drafted/manual/sleeping/breaking 림은 제외.  TreeChop/
        //  Mine designation 의 idle-dispatch 와 동일한 가용성 규약.
        private void DispatchGatherToIdle(BerryBushEntity bush)
        {
            // 운영자 fb: 고갈(재생 중) 덤불에도 채집 지정 가능 — 림이 걸어가 60s 내 재생되면
            //  채집한다.  완전 null 만 차단(IsDepleted 가드 제거).  PawnGatherer 의 give-up 이
            //  도달 불가/장기 정체를 처리하므로 무한 대기는 없음.
            if (bush == null) return;
#if UNITY_2023_1_OR_NEWER
            var gatherers = Object.FindObjectsByType<PawnGatherer>(FindObjectsSortMode.None);
#else
            var gatherers = Object.FindObjectsOfType<PawnGatherer>();
#endif
            if (gatherers == null || gatherers.Length == 0) return;
            PawnGatherer best = null; float bestSq = float.MaxValue;
            Vector2 tp = bush.transform.position;
            foreach (var g in gatherers)
            {
                if (g == null) continue;
                var e = g.GetComponent<PawnEntity>();
                if (e != null && (e.IsDrafted || e.IsUnderManualControl)) continue;
                var nd = g.GetComponent<PawnNeeds>();
                if (nd != null && (nd.IsSleeping || nd.IsBreaking)) continue;
                float sq = ((Vector2)g.transform.position - tp).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = g; }
            }
            if (best != null)
            {
                best.SetBushTarget(bush);
                var e = best.GetComponent<PawnEntity>();
                if (e != null) e.ManualMoveUntil = Time.time + 8f;
                Debug.Log("[Gather] 채집 지정 → idle 림 배정");
            }
        }

        // 청사진 건설 우선 — 가장 가까운 idle PawnBuilder 에게 직접 배정.
        private void PrioritizeBuild(BlueprintEntity bp)
        {
            if (bp == null || bp.IsComplete) return;
#if UNITY_2023_1_OR_NEWER
            var builders = Object.FindObjectsByType<PawnBuilder>(FindObjectsSortMode.None);
#else
            var builders = Object.FindObjectsOfType<PawnBuilder>();
#endif
            if (builders == null || builders.Length == 0) return;
            PawnBuilder best = null; float bestSq = float.MaxValue;
            Vector2 tp = bp.transform.position;
            foreach (var b in builders)
            {
                if (b == null) continue;
                var e = b.GetComponent<PawnEntity>();
                if (e != null && (e.IsDrafted || e.IsUnderManualControl)) continue;
                var nd = b.GetComponent<PawnNeeds>();
                if (nd != null && (nd.IsSleeping || nd.IsBreaking)) continue;
                float sq = ((Vector2)b.transform.position - tp).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            if (best != null)
            {
                var nr = best.GetComponent<PawnNeeds>();
                if (nr != null) nr.ClearRestTarget();
                best.SetBlueprintTarget(bp);
                var e = best.GetComponent<PawnEntity>();
                if (e != null) e.ManualMoveUntil = Time.time + 12f;
                Debug.Log("[Build] 건설 우선 → idle 림 배정");
            }
        }

        // 통합 검증용 - 실제 mouse input 시뮬레이션 (IntegrationTestRunner 호출)
        public void SimulateSelect(PawnEntity pawn) { Select(pawn); }

        /// <summary>QA — 좌클릭 액션 메뉴 항목을 직접 호출(벌목/채집/채광 지정 등).
        ///  worldPos 의 entity hit + 같은 좌클릭 메뉴 생성 → label 포함 item 실행.  미발견 시 false.</summary>
        /// <summary>#34 회귀가드 — 실제 Update 좌클릭 핸들러(L98~127)와 동일하게 PickEntityAt
        /// → BuildLeftClickMenu → ContextMenuUI.Open 을 수행해, '나무 좌클릭 시 벌목 메뉴가
        /// 실제로 열리는가'를 테스트가 검증할 수 있게 한다(이 상호작용은 그동안 테스트가 없어
        /// 반복 회귀했다).  메뉴가 열렸으면 true.</summary>
        public bool SimulateLeftClickOpenMenu(Vector2 worldPos)
        {
            Vector3 mouseWorld = new Vector3(worldPos.x, worldPos.y, 0f);
            Collider2D hit = PickEntityAt(mouseWorld);
            var pawn = (hit != null) ? hit.GetComponent<PawnEntity>() : null;
            if (pawn != null) { Select(pawn); currentInspect = pawn.gameObject; return false; }
            // 실제 핸들러와 동일: entity hit 이면 inspect + 좌클릭 메뉴, hit=null 이면 OverlapPointAll 재훑기.
            if (hit != null) { currentInspect = hit.gameObject; ClearSelection(); }
            if (ContextMenuUI.Instance == null) return false;
            var litems = BuildLeftClickMenu(hit, mouseWorld);
            if (litems != null && litems.Count > 0)
            {
                ContextMenuUI.Instance.Open(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), litems);
                return ContextMenuUI.Instance.IsOpen;
            }
            return false;
        }

        public bool SimulateLeftClickMenuAction(Vector2 worldPos, string itemLabelContains)
        {
            Vector3 mouseWorld = new Vector3(worldPos.x, worldPos.y, 0f);
            Collider2D hit = PickEntityAt(mouseWorld);
            if (hit == null) { Debug.Log($"[LeftMenuQA] PickEntityAt null at {mouseWorld}"); return false; }
            var items = BuildLeftClickMenu(hit, mouseWorld);
            if (items == null) return false;
            foreach (var (label, action) in items)
            {
                if (label.Contains(itemLabelContains)) { action?.Invoke(); return true; }
            }
            Debug.Log($"[LeftMenuQA] '{itemLabelContains}' 미발견. hit={hit.gameObject.name} items=[{string.Join(",", items.ConvertAll(i => i.Item1))}]");
            return false;
        }

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
            // #38진단(2026-06-10) — 우클릭 무동작 간헐 재현: 셀렉션 소실인지 pick 오집인지
            //  로그로 구분 (real 경로의 [RClick] 과 대칭).  지정 0건/배정 0건 FAIL 의 추적 장치.
            if (currentSelection == null)
            {
                Debug.Log($"[RClickSim] no-op: currentSelection=null at {worldPos}");
                return;
            }
            Vector3 mouseWorld = new Vector3(worldPos.x, worldPos.y, 0f);
            ClickEffect.Spawn(mouseWorld, new Color(0.3f, 0.9f, 1f, 0.95f));  // 통합 검증 - 파란 X
            Collider2D rhit = PickEntityAt(mouseWorld);
            Debug.Log($"[RClickSim] sel={currentSelection.PawnName} drafted={currentSelection.IsDrafted} hit={(rhit != null ? rhit.gameObject.name : "empty")} at {worldPos}");
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
            // #작업배정-단일화(2026-06-03, 운영자 승인) — test==real 진짜 일치.
            //  이전엔 여기서 나무/광맥을 TryMark(지정 마커→아무 림 dispatch)했는데, 실제
            //  Update 우클릭(L340/L326)은 '선택된 림'에게 SetTreeTarget/SetVeinTarget 하는
            //  배타 명령이라 test 와 real 이 달랐다(운영자 "선택 림이 아닌 다른 림이 벌목").
            //  이제 시뮬도 실제와 동일하게 '선택 림 전용 명령'으로 통일.  대량 지정은 드래그
            //  마킹 모드(Orders→벌목/채광)가 담당하고, 거긴 자율 ChopTreeAction 이 처리한다.
            if (rhit != null)
            {
                // #38(2026-06-10) — real 경로(Update 지정 블록)와 완전 동일화: 지정 + 선택 림
                //  직접 배정.  이전 sim 은 직접 배정만 해 real(지정-only 회귀)의 버그를 가렸다.
                var treeR = rhit.GetComponent<TreeEntity>();
                if (treeR != null)
                {
                    if (TreeChopDesignation.Instance != null) TreeChopDesignation.Instance.TryMark(rhit.gameObject);
                    TryOrderSelectedChop(treeR);
                    return;
                }
                var veinR = rhit.GetComponent<StoneVeinEntity>();
                if (veinR != null)
                {
                    if (MineDesignation.Instance != null) MineDesignation.Instance.TryMark(rhit.gameObject);
                    TryOrderSelectedMine(veinR);
                    return;
                }
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
            // #audit4 #4 — FindFirstObjectByType 폴백 결과를 캐시(다음 선택부터 재탐색 회피).
            if (cc == null) cc = _cachedCameraController;
            if (cc == null) { cc = Object.FindFirstObjectByType<CameraController>(); _cachedCameraController = cc; }
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
