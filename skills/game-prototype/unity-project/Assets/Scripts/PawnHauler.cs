using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #116 - 레퍼런스 콜로니심 Haul 작업.
    /// nearest WoodPileEntity 찾아서 가서 줍기 (inventory 차감).
    /// AI 가 다른 일 (chop/cook/hunt) 끝낸 후 fallback 으로 시도.
    ///
    /// 1차 단순화: stockpile 영역 없이 그냥 줍는 순간 inventory 들어감.
    ///   추후: 줍은 후 stockpile 까지 운반 후 drop 으로 확장.
    /// 다른 hauler 와 중복 reserve 안 하도록 WoodPileEntity.ReservedBy 사용.
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnHauler : MonoBehaviour
    {
        // #199 C1 — hauler stands adjacent to the item/blueprint (the reference sim) for
        //  pickup/deposit.  1.0 was too tight to even reach an orthogonal neighbour
        //  (center dist 1.0); bumped to 1.5 for diagonal adjacency (√2≈1.414).
        [SerializeField] private float pickupRange = 1.5f;
        [SerializeField] private float giveUpAfterSec = 8f;

        // 림 vanilla pile stack 유지 - stockpile 도착 시 새 pile spawn.
        //  GameManager 가 박음 (TreeEntity.WoodPileSprite 와 동일 sprite).
        public static Sprite WoodPileSpriteRef;
        public static Sprite StoneChunkSpriteRef;
        public static Sprite MeatPileSpriteRef;

        private WoodPileEntity targetPile;
        private StoneChunkEntity targetStone;  // #119
        private MeatPileEntity targetMeat;     // #129
        private PawnMovement movement;
        // #199 B2 (R-1) - path-aware give-up for the GoToItem approach phase
        //  (pile/meat/stone — only one active at a time).  See WorkGiveUp.
        private WorkGiveUp giveUp;
        // 첫사이클 T9 (2026-06-12) — 운반(저장/청사진) phase 는 give-up 이 없어 경로
        //  실패 시 매 프레임 제자리 재시도로 영구 동결(HasTask 가 busy 게이트를 잠가
        //  취침 인터럽트까지 전 작업 차단).  단순 stuck 타이머: dropPhaseStart 후
        //  giveUpAfterSec*2 안에 도착 못 하면 발밑 드롭 + 태스크 해제.
        private float dropPhaseStart = -1f;
        // #199 C2 — reserved stand cell for the current approach target.  Released
        //  on phase change (SetPhase) and ClearTask so a hauler never leaks a cell.
        private Vector2Int standCell = PawnMovement.INVALID_CELL;
        // #121/#142 - 줍은 후 운반 phase: pickup → blueprint(우선) OR stockpile.
        private enum Phase { GoToItem, GoToStockpile, GoToBlueprint }
        private Phase phase = Phase.GoToItem;
        private int carryingWood = 0;
        private int carryingStone = 0;
        private int carryingFood = 0;          // #129
        // 첫사이클 T10 (2026-06-12) — 수확물이 운반 순간 '고기'(기본명·90s 부패)로
        //  변신하던 것: 픽업 시 정체성(표시명/스프라이트/수명) 캡처 → 적치/드롭에 전달.
        private string carryingFoodName = "고기";
        private Sprite carryingFoodSprite;
        private float carryingFoodLifetime = 90f;
        private StockpileZoneEntity dropTarget;
        private BlueprintEntity bpDropTarget;  // #142

        public bool HasTask => targetPile != null || targetStone != null || targetMeat != null
            || carryingWood > 0 || carryingStone > 0 || carryingFood > 0;
        public WoodPileEntity Target => targetPile;
        public StoneChunkEntity TargetStone => targetStone;

        // #213 운영자 fb — "목재가 순간이동".  root cause (c): pile 은 나무에서 Destroy
        //  되고 stockpile 에서 새로 Spawn 되는데 그 사이 운반물의 시각 표현이 전혀 없다 →
        //  운영자 눈엔 "나무에서 사라졌다가 stockpile 에 뿅" = 순간이동.  pawn 이동 자체는
        //  PawnMovement 가 부드럽게 lerp/A* 하므로, 운반 중 pawn 위에 등짐 아이콘을 띄우면
        //  목재가 pawn 과 함께 부드럽게 따라온다 = 순간이동 아님.
        private GameObject carryVisual;
        private SpriteRenderer carryVisualSr;
        private static Sprite _carryBundleSprite;
        private static bool _carryBundleTried;

        private static Sprite CarryBundleSprite()
        {
            if (_carryBundleSprite != null) return _carryBundleSprite;
            if (!_carryBundleTried)
            {
                _carryBundleTried = true;
                _carryBundleSprite = Resources.Load<Sprite>("Sprites/carry_bundle");
            }
            // Resources asset 없으면 WoodPile 기본 sprite 재사용 (항상 non-null).
            return _carryBundleSprite != null ? _carryBundleSprite : WoodPileEntity.EnsureSprite(null);
        }

        // 운반물 유무에 따라 pawn 머리 위 등짐 아이콘 on/off.  매 pickup/deposit 후 호출.
        private void UpdateCarryVisual()
        {
            bool carrying = carryingWood > 0 || carryingStone > 0 || carryingFood > 0;
            if (carrying)
            {
                if (carryVisual == null)
                {
                    carryVisual = new GameObject("CarryVisual");
                    carryVisual.transform.SetParent(transform, worldPositionStays: false);
                    carryVisual.transform.localPosition = new Vector3(0f, 0.45f, 0f);  // 머리 위
                    carryVisual.transform.localScale = Vector3.one * 0.7f;
                    carryVisualSr = carryVisual.AddComponent<SpriteRenderer>();
                    // #sort-audit: carry bundle 규약은 11 (PawnPoseDriver 와 통일).  12 면
                    //  두 캐리 구현이 다른 층이라 일관성 깨짐 → pawn(10) 바로 위 11.
                    carryVisualSr.sortingOrder = 11;  // pawn(10) 위
                }
                carryVisualSr.sprite = CarryBundleSprite();
                carryVisualSr.color = carryingFood > 0
                    ? new Color(0.85f, 0.45f, 0.45f, 1f)   // 고기 = 붉은 tint
                    : carryingStone > 0
                        ? new Color(0.7f, 0.7f, 0.72f, 1f) // 석재 = 회색 tint
                        : Color.white;                     // 목재 = 원색
                carryVisual.SetActive(true);
            }
            else if (carryVisual != null)
            {
                carryVisual.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // pawn 비활성/파괴 시 잔존 아이콘 정리 (carryVisual 은 자식이라 같이 사라지지만 안전).
            if (carryVisual != null) carryVisual.SetActive(false);
            // #버그헌트(2026-06-04): 운반 중 pawn 사망(PawnHealth 가 모든 MonoBehaviour 를 disable)
            //  시 들고 있던 자원이 disabled 컴포넌트에 갇혀 영구 소실됐다('운반하던 림이 죽으면
            //  목재 증발 → 마을 자원이 까닭 없이 줄어듦').  발밑에 물리 더미로 떨어뜨려 보존
            //  (ClearTask 의 #214 drop 정책과 동일).  씬 언로드/종료 중에는 새 오브젝트 생성을
            //  피하려 scene.isLoaded 가드(teardown 시 spawn 경고/NRE 회피).
            if (gameObject.scene.isLoaded
                && (carryingWood > 0 || carryingStone > 0 || carryingFood > 0))
                DropCarriedAtFeet();
        }

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        // #199 C1/C2 — walk to a RESERVED walkable cell ADJACENT to a 1×1 target
        //  (pile/stone/meat/stockpile), colony-sim-style.  The helper reserves the
        //  stand cell (so two haulers don't crowd one pickup) and reuses it each
        //  frame; on a target/phase change the world target moves and a fresh cell
        //  is reserved (the old one released).  Returns false if unreachable/occupied.
        private bool WalkAdjacentTo(Vector2 targetWorld)
            => WalkAdjacentTo(targetWorld, new Vector2Int(1, 1));

        // #199 C1/C2 — adjacency for a (possibly multi-cell) blueprint deposit target.
        private bool WalkAdjacentTo(Vector2 targetWorld, Vector2Int footprint)
        {
            if (PawnMovement.TryReserveWorkStandPos(targetWorld, footprint, transform.position,
                    gameObject, ref standCell, out Vector2 stand))
            { movement.SetTarget(stand); return true; }
            return false;
        }

        // #214 — 운반 중이던 자원을 림 발밑에 물리 더미로 내려놓는다(순간이동 금지).
        //  InStockpile=false 로 두어 카운터에 적립하지 않는다(아직 저장 전).  다른 hauler 가
        //  나중에 줍어서 stockpile 로 옮긴다.  발밑 drop 이라 stockpile 위 drop 의 재줍기
        //  무한루프와는 무관하다(그 우려는 stockpile cell 에 놓을 때만 해당).
        private void DropCarriedAtFeet()
        {
            Vector3 at = transform.position;
            if (carryingWood > 0)
            {
                var p = WoodPileEntity.Spawn(at, carryingWood, WoodPileSpriteRef);
                if (p != null) p.InStockpile = false;
                carryingWood = 0;
            }
            if (carryingStone > 0)
            {
                var c = StoneChunkEntity.Spawn(at, carryingStone, StoneChunkSpriteRef);
                if (c != null) c.InStockpile = false;
                carryingStone = 0;
            }
            if (carryingFood > 0)
            {
                var m = MeatPileEntity.Spawn(at, carryingFood, carryingFoodSprite != null ? carryingFoodSprite : MeatPileSpriteRef, carryingFoodName, carryingFoodLifetime);   // T10
                if (m != null) m.InStockpile = false;
                carryingFood = 0;
            }
        }

        private void ReleaseStandCell()
        {
            if (standCell.x != PawnMovement.INVALID_CELL.x)
            {
                MelonS.GameProto.AI.ReservationManager.ReleaseCell(standCell, gameObject);
                standCell = PawnMovement.INVALID_CELL;
            }
        }

        // #버그헌트(2026-06-03): phase 전환 시 이전 approach 의 stand-cell 예약을 반드시 해제.
        //  이전엔 phase 를 직접 대입해 GoToBlueprint/Stockpile→GoToItem 전환·deposit 완료 시
        //  예약 cell 이 ReservationManager 에 누수(설계 주석 L37-38 위반)→다른 hauler 가 그 cell 을
        //  못 잡아 점점 막힘.  모든 phase 변경을 이 헬퍼로 라우팅(새 phase 는 WalkAdjacentTo 가
        //  새 cell 재예약).  cell 을 안 쓰는 GoToItem 으로 갈 땐 그냥 해제.
        private void SetPhase(Phase p)
        {
            dropPhaseStart = Time.time;   // T9 — 운반 phase stuck 타이머 기준
            if (p != phase) ReleaseStandCell();
            phase = p;
        }

        public void SetPileTarget(WoodPileEntity pile)
        {
            ClearTask();
            targetPile = pile;
            SetPhase(Phase.GoToItem);
            if (pile != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, pile.transform.position));
                pile.ReservedBy = gameObject;
                WalkAdjacentTo(pile.transform.position);
            }
        }

        public void SetStoneTarget(StoneChunkEntity stone)  // #119
        {
            ClearTask();
            targetStone = stone;
            SetPhase(Phase.GoToItem);
            if (stone != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, stone.transform.position));
                stone.ReservedBy = gameObject;
                WalkAdjacentTo(stone.transform.position);
            }
        }

        public void SetMeatTarget(MeatPileEntity meat)  // #129
        {
            ClearTask();
            targetMeat = meat;
            SetPhase(Phase.GoToItem);
            if (meat != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, meat.transform.position));
                meat.ReservedBy = gameObject;
                WalkAdjacentTo(meat.transform.position);
            }
        }

        // Z2 — 지금 등에 진 운반물의 종류 flag(들).  blueprint-fallback 처럼 한 hauler 가
        //  여러 종류를 동시에 들 수 있는 경로에서 "들고 있는 것 중 무엇이든 받는 zone" 을
        //  찾기 위해 OR 로 합친다.  아무것도 안 들었으면 All(=필터 없음, 기존 동작).
        private StockItemKind CarriedKind()
        {
            StockItemKind k = StockItemKind.None;
            if (carryingWood  > 0) k |= StockItemKind.Wood;
            if (carryingStone > 0) k |= StockItemKind.Stone;
            if (carryingFood  > 0) k |= StockItemKind.Food;
            return k == StockItemKind.None ? StockItemKind.All : k;
        }

        public void ClearTask()
        {
            if (targetPile != null && targetPile.ReservedBy == gameObject)
                targetPile.ReservedBy = null;
            if (targetStone != null && targetStone.ReservedBy == gameObject)
                targetStone.ReservedBy = null;
            if (targetMeat != null && targetMeat.ReservedBy == gameObject)
                targetMeat.ReservedBy = null;
            targetPile = null;
            targetStone = null;
            targetMeat = null;
            // #214 운영자 fb "아이템이 뿅 이동" — 운반 중 task 포기 시 즉시-credit(순간이동)
            //  대신 그 자리에 *물리 더미*로 내려놓는다.  과거엔 carryingX 를 곧장 카운터에
            //  적립(=발밑에서 저장공간으로 순간이동)했다.  이제 림이 들고 있던 것은 발밑에
            //  떨어지고, 나중에 다른 hauler 가 물리적으로 다시 줍어 stockpile 로 옮긴다.
            //  떨어진 더미는 InStockpile=false → 카운터 미적립(저장 전이므로) → 이중집계 없음.
            DropCarriedAtFeet();
            UpdateCarryVisual();  // #213 - 운반물 0 → 등짐 아이콘 끔
            dropTarget = null;
            bpDropTarget = null;
            SetPhase(Phase.GoToItem);
            ReleaseStandCell();   // #199 C2 — free the reserved approach cell
            movement.ClearTarget();
        }

        // #142 - 자재 필요한 blueprint 찾기 (wood/stone 별).
        private static BlueprintEntity FindBlueprintNeeding(Vector2 from, bool needWood)
        {
            var arr = Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None);
            BlueprintEntity best = null;
            float bestSq = float.MaxValue;
            foreach (var bp in arr)
            {
                if (bp == null) continue;
                int remaining = needWood ? bp.RemainingWood : bp.RemainingStone;
                if (remaining <= 0) continue;
                float sq = ((Vector2)bp.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = bp; }
            }
            return best;
        }

        private void Update()
        {
            // #142 - blueprint 운반 phase
            if (phase == Phase.GoToBlueprint)
            {
                // T9 — 운반 동결 가드: 제한시간 내 미도착이면 발밑 드롭 후 손 뗌.
                if (dropPhaseStart > 0f && Time.time - dropPhaseStart > giveUpAfterSec * 2f)
                {
                    Debug.Log($"[Hauler] {name} 운반(청사진) 도달 실패 → 발밑 드롭");
                    DropCarriedAtFeet();
                    UpdateCarryVisual();
                    ClearTask();
                    return;
                }
                if (bpDropTarget == null || bpDropTarget.gameObject == null)
                {
                    // blueprint 사라짐 - stockpile fallback (#155 priority 우선)
                    //  Z2 — 현재 운반물 종류를 받는 zone 만 (wood-only/food-only 등 존중).
                    var sp = StockpileZoneEntity.FindBest(transform.position, CarriedKind());
                    if (sp != null)
                    {
                        dropTarget = sp;
                        SetPhase(Phase.GoToStockpile);
                        WalkAdjacentTo(sp.transform.position);
                    }
                    else
                    {
                        ClearTask();
                    }
                    return;
                }
                // #199 C1 - nearest-footprint-cell distance (multi-cell blueprint).
                float bdist = PawnMovement.DistanceToFootprint(
                    bpDropTarget.transform.position, bpDropTarget.Footprint, transform.position);
                if (bdist <= pickupRange || movement.AtStandCell(standCell))  // #199 C2
                {
                    // #버그헌트3(2026-06-04): 청사진엔 '필요한 만큼만' 넣고 초과분은 보존한다.
                    //  이전엔 carryingWood/Stone 을 통째로 DepositWood 후 0 으로 zero 했는데,
                    //  DepositWood 는 needWood 로 clamp(초과분 폐기)하므로 5 들고 와 1 만 필요하면
                    //  4 가 증발했다.  게다가 pickup 때 카운터를 full(-5) 차감했으므로 그 4 는
                    //  카운터·물리 양쪽에서 영구 손실(자원모델 단일화 회귀).  필요분만 deposit 하고
                    //  남은 자재는 아래서 발밑 물리 드롭 → re-haul 로 회수, 카운터 정합 유지.
                    if (carryingWood > 0)
                    {
                        int accepted = Mathf.Clamp(bpDropTarget.needWood - bpDropTarget.collectedWood, 0, carryingWood);
                        if (accepted > 0)
                        {
                            bpDropTarget.DepositWood(accepted);
                            Debug.Log($"[Hauler] {name} blueprint 자재 넣음: 목재 {accepted} (보유 {carryingWood})");
                        }
                        carryingWood -= accepted;   // 초과분 보존
                    }
                    if (carryingStone > 0)
                    {
                        int accepted = Mathf.Clamp(bpDropTarget.needStone - bpDropTarget.collectedStone, 0, carryingStone);
                        if (accepted > 0)
                        {
                            bpDropTarget.DepositStone(accepted);
                            Debug.Log($"[Hauler] {name} blueprint 자재 넣음: 석재 {accepted} (보유 {carryingStone})");
                        }
                        carryingStone -= accepted;
                    }
                    // 남은 자재(청사진 초과분) + 식량은 발밑에 물리 드롭(InStockpile=false → re-haul
                    //  로 회수, 카운터 미적립).  이전엔 식량만 드롭하고 초과 자재는 증발했다.
                    if (carryingWood > 0 || carryingStone > 0 || carryingFood > 0) DropCarriedAtFeet();
                    UpdateCarryVisual();  // #213 - 운반 끝 → 등짐 아이콘 끔
                    bpDropTarget = null;
                    SetPhase(Phase.GoToItem);
                    movement.ClearTarget();
                }
                else
                {
                    WalkAdjacentTo(bpDropTarget.transform.position, bpDropTarget.Footprint);
                }
                return;
            }
            // #121 - 줍은 후 stockpile 으로 이동 phase 우선
            if (phase == Phase.GoToStockpile)
            {
                // T9 — 운반 동결 가드 (위와 동일).
                if (dropPhaseStart > 0f && Time.time - dropPhaseStart > giveUpAfterSec * 2f)
                {
                    Debug.Log($"[Hauler] {name} 운반(저장고) 도달 실패 → 발밑 드롭");
                    DropCarriedAtFeet();
                    UpdateCarryVisual();
                    ClearTask();
                    return;
                }
                if (dropTarget == null || dropTarget.gameObject == null)
                {
                    // #214 — stockpile 이 운반 도중 사라짐.  즉시-credit(순간이동) 대신
                    //  발밑에 물리 더미로 내려놓는다(미적립).  다른 hauler 가 나중에 줍어 옮김.
                    DropCarriedAtFeet();
                    UpdateCarryVisual();  // #213 - 운반 끝 → 등짐 아이콘 끔
                    SetPhase(Phase.GoToItem);
                    movement.ClearTarget();
                    return;
                }
                float ddist = Vector2.Distance(transform.position, dropTarget.transform.position);
                if (ddist <= pickupRange || movement.AtStandCell(standCell))  // #199 C2
                {
                    // 림 vanilla: stockpile 도착 시 pile 을 stockpile 위에 그대로 stack.
                    //  pile 사라지지 X.  inventory counter 는 derived (모든 pile 합).
                    Vector3 dropPos = dropTarget.transform.position;
                    // #213 - WoodPileEntity.Spawn 이 sprite null 이어도 기본 sprite 를
                    //  보장하므로 SpriteRef!=null 가드 제거.  stockpile 에 항상 눈에 보이는
                    //  pile 이 stack 된다 (과거엔 ref null 이면 pile 없이 카운터만 +N →
                    //  목재가 "어디론가 사라진" 것처럼 보임).  InStockpile=true 로 재운반 loop 방지.
                    if (carryingWood > 0)
                    {
                        var p = WoodPileEntity.Spawn(dropPos, carryingWood, WoodPileSpriteRef);
                        if (p != null) p.InStockpile = true;
                        ResourceManager.Instance?.AddWood(carryingWood);
                        carryingWood = 0;
                    }
                    // #214 — 석재/식량도 목재와 동일하게 *항상* 물리 더미를 stack 한다.
                    //  과거엔 SpriteRef==null 이면 pile 없이 카운터만 +N (legacy fallback) →
                    //  "저장공간으로 뿅" 처럼 보였다.  sprite 가 null 이어도 entity 는 만들어
                    //  카운터(=물리 더미 합)와 화면이 항상 일치하게 한다.
                    if (carryingStone > 0)
                    {
                        var c = StoneChunkEntity.Spawn(dropPos, carryingStone, StoneChunkSpriteRef);
                        if (c != null) c.InStockpile = true;
                        ResourceManager.Instance?.AddStone(carryingStone);
                        carryingStone = 0;
                    }
                    if (carryingFood > 0)
                    {
                        var m = MeatPileEntity.Spawn(dropPos, carryingFood, carryingFoodSprite != null ? carryingFoodSprite : MeatPileSpriteRef, carryingFoodName, carryingFoodLifetime);   // T10
                        if (m != null) m.InStockpile = true;
                        ResourceManager.Instance?.AddFood(carryingFood);
                        carryingFood = 0;
                    }
                    UpdateCarryVisual();  // #213 - 운반 끝 → 등짐 아이콘 끔
                    Debug.Log($"[Hauler] {name} stockpile 도착, pile stack 보존");
                    dropTarget = null;
                    SetPhase(Phase.GoToItem);
                    movement.ClearTarget();
                }
                else
                {
                    WalkAdjacentTo(dropTarget.transform.position);
                }
                return;
            }
            // wood pile 우선
            if (targetPile != null)
            {
                if (targetPile.gameObject == null) { ReleaseStandCell(); targetPile = null; return; }  // #버그헌트: cell 해제
                float dist = Vector2.Distance(transform.position, targetPile.transform.position);
                // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
                if (dist > pickupRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
                {
                    Debug.Log($"[Hauler] {name} give up pile (dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                    ClearTask();
                    return;
                }
                if (dist <= pickupRange || movement.AtStandCell(standCell))  // #199 C2
                {
                    movement.ClearTarget();
                    int amount = targetPile.Wood;
                    // #자원모델 단일화(2026-06-04): InStockpile 더미를 운반용으로 집으면 stockpile
                    //  에서 빠지는 것이므로 카운터를 −amount (deposit 의 +amount 와 대칭).  이 차감이
                    //  없어 카운터가 영구 과대집계됐다(목재 N 표시되나 실제 물리 0 → 새 건축 자재부족
                    //  정지).  바닥(비-stockpile) 더미는 애초에 미적립이라 차감 안 함(불변식: 카운터 =
                    //  Σ InStockpile 더미).
                    if (targetPile.InStockpile) ResourceManager.Instance?.AddWood(-amount);
                    UnityEngine.Object.Destroy(targetPile.gameObject);
                    targetPile = null;
                    carryingWood += amount;
                    UpdateCarryVisual();  // #213 - 줍는 순간 등짐 아이콘 on (운반 중 시각화)
                    // #142 - 자재 필요한 blueprint 우선, 없으면 stockpile, 그것도 없으면 inventory.
                    var bp = FindBlueprintNeeding(transform.position, needWood: true);
                    if (bp != null)
                    {
                        bpDropTarget = bp;
                        SetPhase(Phase.GoToBlueprint);
                        WalkAdjacentTo(bp.transform.position, bp.Footprint);
                    }
                    else
                    {
                        // Z2 — 목재를 받는 stockpile 만 (wood 거부 zone 은 skip).
                        var sp = StockpileZoneEntity.FindBest(transform.position, StockItemKind.Wood);
                        if (sp != null)
                        {
                            dropTarget = sp;
                            SetPhase(Phase.GoToStockpile);
                            WalkAdjacentTo(sp.transform.position);
                        }
                        else
                        {
                            // #214 — 받을 stockpile/blueprint 없음.  즉시-credit(순간이동) 대신
                            //  그 자리에 물리 더미로 내려놓는다(미적립).  나중에 stockpile 이
                            //  생기면 다른 hauler 가 물리적으로 줍어 옮긴다.
                            DropCarriedAtFeet();
                            UpdateCarryVisual();
                        }
                    }
                }
                else
                {
                    WalkAdjacentTo(targetPile.transform.position);
                }
                return;
            }
            // meat pile - #129
            if (targetMeat != null)
            {
                if (targetMeat.gameObject == null) { ReleaseStandCell(); targetMeat = null; return; }  // #버그헌트: cell 해제
                float dist = Vector2.Distance(transform.position, targetMeat.transform.position);
                // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
                if (dist > pickupRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
                {
                    Debug.Log($"[Hauler] {name} give up meat (dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                    ClearTask();
                    return;
                }
                if (dist <= pickupRange || movement.AtStandCell(standCell))  // #199 C2
                {
                    movement.ClearTarget();
                    int amount = targetMeat.Food;
                    // T10 — 정체성 캡처 (적치/드롭 Spawn 에 전달).
                    carryingFoodName = targetMeat.DisplayName;
                    var tmSr = targetMeat.GetComponent<SpriteRenderer>();
                    carryingFoodSprite = tmSr != null ? tmSr.sprite : null;
                    carryingFoodLifetime = targetMeat.LifetimeForHaul;
                    // #자원모델 단일화(2026-06-04): InStockpile 식량을 집으면 카운터 −amount (deposit 대칭).
                    if (targetMeat.InStockpile) ResourceManager.Instance?.AddFood(-amount);
                    UnityEngine.Object.Destroy(targetMeat.gameObject);
                    targetMeat = null;
                    carryingFood += amount;
                    UpdateCarryVisual();  // #213 - 줍는 순간 등짐 아이콘 on
                    // Z2 — 식량을 받는 stockpile 만 (food 거부 zone 은 skip).
                    var sp = StockpileZoneEntity.FindBest(transform.position, StockItemKind.Food);
                    if (sp != null)
                    {
                        dropTarget = sp;
                        SetPhase(Phase.GoToStockpile);
                        WalkAdjacentTo(sp.transform.position);
                    }
                    else
                    {
                        // #214 — 받을 stockpile 없음.  즉시-credit(순간이동) 대신 물리 드롭.
                        DropCarriedAtFeet();
                        UpdateCarryVisual();
                    }
                }
                else
                {
                    WalkAdjacentTo(targetMeat.transform.position);
                }
                return;
            }
            // stone chunk
            if (targetStone != null)
            {
                if (targetStone.gameObject == null) { ReleaseStandCell(); targetStone = null; return; }  // #버그헌트: cell 해제
                float dist = Vector2.Distance(transform.position, targetStone.transform.position);
                // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
                if (dist > pickupRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
                {
                    Debug.Log($"[Hauler] {name} give up stone (dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                    ClearTask();
                    return;
                }
                if (dist <= pickupRange || movement.AtStandCell(standCell))  // #199 C2
                {
                    movement.ClearTarget();
                    int amount = targetStone.Stone;
                    // #자원모델 단일화(2026-06-04): InStockpile 석재를 집으면 카운터 −amount (deposit 대칭).
                    if (targetStone.InStockpile) ResourceManager.Instance?.AddStone(-amount);
                    UnityEngine.Object.Destroy(targetStone.gameObject);
                    targetStone = null;
                    carryingStone += amount;
                    UpdateCarryVisual();  // #213 - 운반 중 시각화
                    // #142 - stone blueprint 우선
                    var bp = FindBlueprintNeeding(transform.position, needWood: false);
                    if (bp != null)
                    {
                        bpDropTarget = bp;
                        SetPhase(Phase.GoToBlueprint);
                        WalkAdjacentTo(bp.transform.position, bp.Footprint);
                    }
                    else
                    {
                        // Z2 — 석재(chunk 포함)를 받는 stockpile 만.  dumping zone(Stone-only)
                        //  이 chunk/석재를 받는 경로가 여기.
                        var sp = StockpileZoneEntity.FindBest(transform.position, StockItemKind.Stone);
                        if (sp != null)
                        {
                            dropTarget = sp;
                            SetPhase(Phase.GoToStockpile);
                            WalkAdjacentTo(sp.transform.position);
                        }
                        else
                        {
                            // #214 — 받을 stockpile/blueprint 없음.  즉시-credit(순간이동)
                            //  대신 물리 더미로 발밑에 드롭.
                            DropCarriedAtFeet();
                            UpdateCarryVisual();
                        }
                    }
                }
                else
                {
                    WalkAdjacentTo(targetStone.transform.position);
                }
            }
        }
    }
}
