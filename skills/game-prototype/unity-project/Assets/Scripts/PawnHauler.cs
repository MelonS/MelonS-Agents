using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #116 - 림월드 Haul 작업.
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
        [SerializeField] private float pickupRange = 1.0f;
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
        // #121/#142 - 줍은 후 운반 phase: pickup → blueprint(우선) OR stockpile.
        private enum Phase { GoToItem, GoToStockpile, GoToBlueprint }
        private Phase phase = Phase.GoToItem;
        private int carryingWood = 0;
        private int carryingStone = 0;
        private int carryingFood = 0;          // #129
        private StockpileZoneEntity dropTarget;
        private BlueprintEntity bpDropTarget;  // #142

        public bool HasTask => targetPile != null || targetStone != null || targetMeat != null
            || carryingWood > 0 || carryingStone > 0 || carryingFood > 0;
        public WoodPileEntity Target => targetPile;
        public StoneChunkEntity TargetStone => targetStone;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetPileTarget(WoodPileEntity pile)
        {
            ClearTask();
            targetPile = pile;
            phase = Phase.GoToItem;
            if (pile != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, pile.transform.position));
                pile.ReservedBy = gameObject;
                movement.SetTarget(pile.transform.position);
            }
        }

        public void SetStoneTarget(StoneChunkEntity stone)  // #119
        {
            ClearTask();
            targetStone = stone;
            phase = Phase.GoToItem;
            if (stone != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, stone.transform.position));
                stone.ReservedBy = gameObject;
                movement.SetTarget(stone.transform.position);
            }
        }

        public void SetMeatTarget(MeatPileEntity meat)  // #129
        {
            ClearTask();
            targetMeat = meat;
            phase = Phase.GoToItem;
            if (meat != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, meat.transform.position));
                meat.ReservedBy = gameObject;
                movement.SetTarget(meat.transform.position);
            }
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
            // 운반 중인 자원은 inventory 로 즉시 (drop 자리에 새 pile 만들면 다시 hauler 가 줍어서 무한 loop).
            if (carryingWood > 0)  { ResourceManager.Instance?.AddWood(carryingWood);   carryingWood = 0; }
            if (carryingStone > 0) { ResourceManager.Instance?.AddStone(carryingStone); carryingStone = 0; }
            if (carryingFood > 0)  { ResourceManager.Instance?.AddFood(carryingFood);   carryingFood = 0; }
            dropTarget = null;
            bpDropTarget = null;
            phase = Phase.GoToItem;
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
                if (bpDropTarget == null || bpDropTarget.gameObject == null)
                {
                    // blueprint 사라짐 - stockpile fallback (#155 priority 우선)
                    var sp = StockpileZoneEntity.FindBest(transform.position);
                    if (sp != null)
                    {
                        dropTarget = sp;
                        phase = Phase.GoToStockpile;
                        movement.SetTarget(sp.transform.position);
                    }
                    else
                    {
                        ClearTask();
                    }
                    return;
                }
                float bdist = Vector2.Distance(transform.position, bpDropTarget.transform.position);
                if (bdist <= pickupRange)
                {
                    if (carryingWood > 0)
                    {
                        bpDropTarget.DepositWood(carryingWood);
                        Debug.Log($"[Hauler] {name} blueprint 자재 넣음: 목재 {carryingWood}");
                        carryingWood = 0;
                    }
                    if (carryingStone > 0)
                    {
                        bpDropTarget.DepositStone(carryingStone);
                        Debug.Log($"[Hauler] {name} blueprint 자재 넣음: 석재 {carryingStone}");
                        carryingStone = 0;
                    }
                    if (carryingFood > 0)
                    { ResourceManager.Instance?.AddFood(carryingFood); carryingFood = 0; }
                    bpDropTarget = null;
                    phase = Phase.GoToItem;
                    movement.ClearTarget();
                }
                else
                {
                    movement.SetTarget(bpDropTarget.transform.position);
                }
                return;
            }
            // #121 - 줍은 후 stockpile 으로 이동 phase 우선
            if (phase == Phase.GoToStockpile)
            {
                if (dropTarget == null || dropTarget.gameObject == null)
                {
                    // stockpile 없으면 그냥 inventory 추가
                    if (carryingWood > 0)  { ResourceManager.Instance?.AddWood(carryingWood);   carryingWood = 0; }
                    if (carryingStone > 0) { ResourceManager.Instance?.AddStone(carryingStone); carryingStone = 0; }
                    phase = Phase.GoToItem;
                    movement.ClearTarget();
                    return;
                }
                float ddist = Vector2.Distance(transform.position, dropTarget.transform.position);
                if (ddist <= pickupRange)
                {
                    // 림 vanilla: stockpile 도착 시 pile 을 stockpile 위에 그대로 stack.
                    //  pile 사라지지 X.  inventory counter 는 derived (모든 pile 합).
                    Vector3 dropPos = dropTarget.transform.position;
                    // stockpile 안 pile 은 InStockpile=true (재운반 loop 방지).
                    if (carryingWood > 0 && WoodPileSpriteRef != null)
                    {
                        var p = WoodPileEntity.Spawn(dropPos, carryingWood, WoodPileSpriteRef);
                        if (p != null) p.InStockpile = true;
                        ResourceManager.Instance?.AddWood(carryingWood);
                        carryingWood = 0;
                    }
                    if (carryingStone > 0 && StoneChunkSpriteRef != null)
                    {
                        var c = StoneChunkEntity.Spawn(dropPos, carryingStone, StoneChunkSpriteRef);
                        if (c != null) c.InStockpile = true;
                        ResourceManager.Instance?.AddStone(carryingStone);
                        carryingStone = 0;
                    }
                    if (carryingFood > 0 && MeatPileSpriteRef != null)
                    {
                        var m = MeatPileEntity.Spawn(dropPos, carryingFood, MeatPileSpriteRef);
                        if (m != null) m.InStockpile = true;
                        ResourceManager.Instance?.AddFood(carryingFood);
                        carryingFood = 0;
                    }
                    // sprite null fallback 만 inventory 직접 (legacy 호환)
                    if (carryingWood > 0)  { ResourceManager.Instance?.AddWood(carryingWood);   carryingWood = 0; }
                    if (carryingStone > 0) { ResourceManager.Instance?.AddStone(carryingStone); carryingStone = 0; }
                    if (carryingFood > 0)  { ResourceManager.Instance?.AddFood(carryingFood);   carryingFood = 0; }
                    Debug.Log($"[Hauler] {name} stockpile 도착, pile stack 보존");
                    dropTarget = null;
                    phase = Phase.GoToItem;
                    movement.ClearTarget();
                }
                else
                {
                    movement.SetTarget(dropTarget.transform.position);
                }
                return;
            }
            // wood pile 우선
            if (targetPile != null)
            {
                if (targetPile.gameObject == null) { targetPile = null; return; }
                float dist = Vector2.Distance(transform.position, targetPile.transform.position);
                // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
                if (dist > pickupRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
                {
                    Debug.Log($"[Hauler] {name} give up pile (dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                    ClearTask();
                    return;
                }
                if (dist <= pickupRange)
                {
                    movement.ClearTarget();
                    int amount = targetPile.Wood;
                    UnityEngine.Object.Destroy(targetPile.gameObject);
                    targetPile = null;
                    carryingWood += amount;
                    // #142 - 자재 필요한 blueprint 우선, 없으면 stockpile, 그것도 없으면 inventory.
                    var bp = FindBlueprintNeeding(transform.position, needWood: true);
                    if (bp != null)
                    {
                        bpDropTarget = bp;
                        phase = Phase.GoToBlueprint;
                        movement.SetTarget(bp.transform.position);
                    }
                    else
                    {
                        var sp = StockpileZoneEntity.FindBest(transform.position);
                        if (sp != null)
                        {
                            dropTarget = sp;
                            phase = Phase.GoToStockpile;
                            movement.SetTarget(sp.transform.position);
                        }
                        else
                        {
                            ResourceManager.Instance?.AddWood(carryingWood);
                            carryingWood = 0;
                        }
                    }
                }
                else
                {
                    movement.SetTarget(targetPile.transform.position);
                }
                return;
            }
            // meat pile - #129
            if (targetMeat != null)
            {
                if (targetMeat.gameObject == null) { targetMeat = null; return; }
                float dist = Vector2.Distance(transform.position, targetMeat.transform.position);
                // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
                if (dist > pickupRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
                {
                    Debug.Log($"[Hauler] {name} give up meat (dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                    ClearTask();
                    return;
                }
                if (dist <= pickupRange)
                {
                    movement.ClearTarget();
                    int amount = targetMeat.Food;
                    UnityEngine.Object.Destroy(targetMeat.gameObject);
                    targetMeat = null;
                    var sp = StockpileZoneEntity.FindBest(transform.position);
                    if (sp != null)
                    {
                        carryingFood += amount;
                        dropTarget = sp;
                        phase = Phase.GoToStockpile;
                        movement.SetTarget(sp.transform.position);
                    }
                    else
                    {
                        ResourceManager.Instance?.AddFood(amount);
                    }
                }
                else
                {
                    movement.SetTarget(targetMeat.transform.position);
                }
                return;
            }
            // stone chunk
            if (targetStone != null)
            {
                if (targetStone.gameObject == null) { targetStone = null; return; }
                float dist = Vector2.Distance(transform.position, targetStone.transform.position);
                // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
                if (dist > pickupRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
                {
                    Debug.Log($"[Hauler] {name} give up stone (dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                    ClearTask();
                    return;
                }
                if (dist <= pickupRange)
                {
                    movement.ClearTarget();
                    int amount = targetStone.Stone;
                    UnityEngine.Object.Destroy(targetStone.gameObject);
                    targetStone = null;
                    carryingStone += amount;
                    // #142 - stone blueprint 우선
                    var bp = FindBlueprintNeeding(transform.position, needWood: false);
                    if (bp != null)
                    {
                        bpDropTarget = bp;
                        phase = Phase.GoToBlueprint;
                        movement.SetTarget(bp.transform.position);
                    }
                    else
                    {
                        var sp = StockpileZoneEntity.FindBest(transform.position);
                        if (sp != null)
                        {
                            dropTarget = sp;
                            phase = Phase.GoToStockpile;
                            movement.SetTarget(sp.transform.position);
                        }
                        else
                        {
                            ResourceManager.Instance?.AddStone(carryingStone);
                            carryingStone = 0;
                        }
                    }
                }
                else
                {
                    movement.SetTarget(targetStone.transform.position);
                }
            }
        }
    }
}
