using UnityEngine;

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

        private WoodPileEntity targetPile;
        private StoneChunkEntity targetStone;  // #119
        private MeatPileEntity targetMeat;     // #129
        private PawnMovement movement;
        private float taskStartTime = -10f;
        // #121 - 줍은 후 운반 phase: pickup → drop-at-stockpile.
        private enum Phase { GoToItem, GoToStockpile }
        private Phase phase = Phase.GoToItem;
        private int carryingWood = 0;
        private int carryingStone = 0;
        private int carryingFood = 0;          // #129
        private StockpileZoneEntity dropTarget;

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
            taskStartTime = Time.time;
            if (pile != null)
            {
                pile.ReservedBy = gameObject;
                movement.SetTarget(pile.transform.position);
            }
        }

        public void SetStoneTarget(StoneChunkEntity stone)  // #119
        {
            ClearTask();
            targetStone = stone;
            phase = Phase.GoToItem;
            taskStartTime = Time.time;
            if (stone != null)
            {
                stone.ReservedBy = gameObject;
                movement.SetTarget(stone.transform.position);
            }
        }

        public void SetMeatTarget(MeatPileEntity meat)  // #129
        {
            ClearTask();
            targetMeat = meat;
            phase = Phase.GoToItem;
            taskStartTime = Time.time;
            if (meat != null)
            {
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
            phase = Phase.GoToItem;
            movement.ClearTarget();
        }

        private void Update()
        {
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
                    // 도착 - 자원 inventory 추가 (zone 마커 시각 효과는 그대로)
                    if (carryingWood > 0)  { ResourceManager.Instance?.AddWood(carryingWood);   carryingWood = 0; }
                    if (carryingStone > 0) { ResourceManager.Instance?.AddStone(carryingStone); carryingStone = 0; }
                    if (carryingFood > 0)  { ResourceManager.Instance?.AddFood(carryingFood);   carryingFood = 0; }
                    Debug.Log($"[Hauler] {name} stockpile 도착, 자원 적재 완료");
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
                if (Time.time - taskStartTime > giveUpAfterSec && dist > pickupRange)
                {
                    Debug.Log($"[Hauler] {name} give up pile (dist={dist:F2})");
                    ClearTask();
                    return;
                }
                if (dist <= pickupRange)
                {
                    movement.ClearTarget();
                    // #121 - inventory 즉시 추가 대신 carry + stockpile 이동
                    int amount = targetPile.Wood;
                    UnityEngine.Object.Destroy(targetPile.gameObject);
                    targetPile = null;
                    var sp = StockpileZoneEntity.FindNearest(transform.position);
                    if (sp != null)
                    {
                        carryingWood += amount;
                        dropTarget = sp;
                        phase = Phase.GoToStockpile;
                        movement.SetTarget(sp.transform.position);
                    }
                    else
                    {
                        // 없으면 즉시 inventory (legacy)
                        ResourceManager.Instance?.AddWood(amount);
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
                if (Time.time - taskStartTime > giveUpAfterSec && dist > pickupRange)
                {
                    Debug.Log($"[Hauler] {name} give up meat (dist={dist:F2})");
                    ClearTask();
                    return;
                }
                if (dist <= pickupRange)
                {
                    movement.ClearTarget();
                    int amount = targetMeat.Food;
                    UnityEngine.Object.Destroy(targetMeat.gameObject);
                    targetMeat = null;
                    var sp = StockpileZoneEntity.FindNearest(transform.position);
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
                if (Time.time - taskStartTime > giveUpAfterSec && dist > pickupRange)
                {
                    Debug.Log($"[Hauler] {name} give up stone (dist={dist:F2})");
                    ClearTask();
                    return;
                }
                if (dist <= pickupRange)
                {
                    movement.ClearTarget();
                    int amount = targetStone.Stone;
                    UnityEngine.Object.Destroy(targetStone.gameObject);
                    targetStone = null;
                    var sp = StockpileZoneEntity.FindNearest(transform.position);
                    if (sp != null)
                    {
                        carryingStone += amount;
                        dropTarget = sp;
                        phase = Phase.GoToStockpile;
                        movement.SetTarget(sp.transform.position);
                    }
                    else
                    {
                        ResourceManager.Instance?.AddStone(amount);
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
