using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// GDD G3 — 매장 워커.  시신까지 걸어가 수습(carry) → 빈 무덤까지 운반 → 3초
    /// 안장.  PawnDoctor/PawnHauler 와 동일 패턴 (물리 운반 — 순간이동 금지 #214).
    /// 운반 중 중단(사망/포기) 시 시신을 현재 위치에 내려놓는다 (소실 방지).
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnBurier : MonoBehaviour
    {
        [SerializeField] private float reachRange = 1.3f;
        [SerializeField] private float burySeconds = 3f;
        [SerializeField] private float giveUpAfterSec = 15f;

        private PawnEntity corpse;          // 수습 대상 (IsDead)
        private GraveEntity grave;          // 목적지 무덤
        private bool carrying;              // true = 시신 들고 이동 중
        private float buryProgress;
        private PawnMovement movement;
        private WorkGiveUp giveUp;

        public bool HasTask => grave != null && (corpse != null || carrying);

        private void Awake() { movement = GetComponent<PawnMovement>(); }

        public bool SetTask(PawnEntity deadPawn, GraveEntity targetGrave)
        {
            if (deadPawn == null || targetGrave == null) return false;
            if (!ReservationManager.TryReserve(deadPawn, gameObject)) return false;
            if (!ReservationManager.TryReserve(targetGrave, gameObject))
            {
                ReservationManager.Release(deadPawn, gameObject);
                return false;
            }
            corpse = deadPawn;
            grave = targetGrave;
            carrying = false;
            buryProgress = 0f;
            giveUp.Reset(Time.time, Vector2.Distance(transform.position, deadPawn.transform.position));
            movement.SetTarget(deadPawn.transform.position);
            return true;
        }

        public void ClearTask()
        {
            // 운반 중 중단 → 시신을 현재 위치에 내려놓기 (소실 방지)
            if (carrying && corpse != null)
            {
                corpse.transform.position = transform.position;
                corpse.gameObject.SetActive(true);
            }
            if (corpse != null) ReservationManager.Release(corpse, gameObject);
            if (grave != null) ReservationManager.Release(grave, gameObject);
            corpse = null; grave = null; carrying = false; buryProgress = 0f;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (grave == null) { if (corpse != null) ClearTask(); return; }
            if (grave.Occupied) { ClearTask(); return; }   // 다른 경로로 채워졌으면 포기
            if (corpse == null) { ClearTask(); return; }

            Vector2 targetPos = carrying ? (Vector2)grave.transform.position
                                         : (Vector2)corpse.transform.position;
            float dist = Vector2.Distance(transform.position, targetPos);

            if (dist > reachRange)
            {
                if (giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
                {
                    Debug.Log($"[Burier] {GetComponent<PawnEntity>()?.PawnName ?? name} 매장 포기 (pathFailed={movement.LastPathFailed})");
                    ClearTask();
                    return;
                }
                movement.SetTarget(targetPos);
                return;
            }

            if (!carrying)
            {
                // 시신 수습 — 물리 carry (비활성화 + 참조 유지, 내려놓기 가능)
                carrying = true;
                corpse.gameObject.SetActive(false);
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, grave.transform.position));
                movement.SetTarget(grave.transform.position);
                return;
            }

            // 무덤 도착 — 안장 진행
            movement.ClearTarget();
            buryProgress += Time.deltaTime;
            if (buryProgress >= burySeconds)
            {
                string pawnName = corpse.PawnName;
                string story = PawnBackstory.GetLine(corpse);
                grave.Bury(pawnName, story);
                ReservationManager.Release(corpse, gameObject);
                ReservationManager.Release(grave, gameObject);
                Object.Destroy(corpse.gameObject);
                FloatingText.Spawn(grave.transform.position + Vector3.up * 0.6f,
                    "안장", new Color(0.75f, 0.72f, 0.60f));
                corpse = null; grave = null; carrying = false; buryProgress = 0f;
            }
        }
    }
}
