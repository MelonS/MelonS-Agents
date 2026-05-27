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
        private PawnMovement movement;
        private float taskStartTime = -10f;

        public bool HasTask => targetPile != null;
        public WoodPileEntity Target => targetPile;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetPileTarget(WoodPileEntity pile)
        {
            // 이전 target reservation 해제
            if (targetPile != null && targetPile.ReservedBy == gameObject)
                targetPile.ReservedBy = null;

            targetPile = pile;
            taskStartTime = Time.time;
            if (pile != null)
            {
                pile.ReservedBy = gameObject;
                movement.SetTarget(pile.transform.position);
            }
        }

        public void ClearTask()
        {
            if (targetPile != null && targetPile.ReservedBy == gameObject)
                targetPile.ReservedBy = null;
            targetPile = null;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetPile == null) return;
            // pile 사라졌으면 task 종료
            if (targetPile.gameObject == null)
            {
                targetPile = null;
                return;
            }
            float dist = Vector2.Distance(transform.position, targetPile.transform.position);
            // 도달 불가 시 포기
            if (Time.time - taskStartTime > giveUpAfterSec && dist > pickupRange)
            {
                Debug.Log($"[Hauler] {name} give up pile (dist={dist:F2})");
                ClearTask();
                return;
            }
            if (dist <= pickupRange)
            {
                movement.ClearTarget();
                bool ok = targetPile.Pickup();
                if (ok)
                {
                    Debug.Log($"[Hauler] {name} picked up wood pile (+{targetPile.Wood})");
                }
                targetPile = null;  // pickup 시 destroy 됨
            }
            else
            {
                // 매 프레임 target 재설정 (pile 이 이동하진 않지만 safe)
                movement.SetTarget(targetPile.transform.position);
            }
        }
    }
}
