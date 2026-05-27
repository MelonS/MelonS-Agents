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
        private PawnMovement movement;
        private float taskStartTime = -10f;

        public bool HasTask => targetPile != null || targetStone != null;
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
            taskStartTime = Time.time;
            if (stone != null)
            {
                stone.ReservedBy = gameObject;
                movement.SetTarget(stone.transform.position);
            }
        }

        public void ClearTask()
        {
            if (targetPile != null && targetPile.ReservedBy == gameObject)
                targetPile.ReservedBy = null;
            if (targetStone != null && targetStone.ReservedBy == gameObject)
                targetStone.ReservedBy = null;
            targetPile = null;
            targetStone = null;
            movement.ClearTarget();
        }

        private void Update()
        {
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
                    targetPile.Pickup();
                    targetPile = null;
                }
                else
                {
                    movement.SetTarget(targetPile.transform.position);
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
                    targetStone.Pickup();
                    targetStone = null;
                }
                else
                {
                    movement.SetTarget(targetStone.transform.position);
                }
            }
        }
    }
}
