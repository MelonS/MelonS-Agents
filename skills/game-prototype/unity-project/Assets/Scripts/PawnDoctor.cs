using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// #125 - 림월드 의료 (단순화).
    ///  다친/의식불명 pawn 옆에 가서 tend → bleed stop + body part heal.
    ///  PawnHauler/PawnBuilder 와 동일 패턴.
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnDoctor : MonoBehaviour
    {
        [SerializeField] private float tendRange = 1.4f;
        [SerializeField] private float tendSeconds = 5f;
        [SerializeField] private float giveUpAfterSec = 12f;

        private PawnHealth targetPatient;
        private PawnMovement movement;
        // #199 B2 (R-1) - path-aware give-up (see WorkGiveUp).  Patients can be
        //  downed/moving; give-up keys on real unreachability + stall, not detour.
        private WorkGiveUp giveUp;
        private float tendProgress = 0f;

        public bool HasTask => targetPatient != null;
        public PawnHealth Target => targetPatient;

        private void Awake() { movement = GetComponent<PawnMovement>(); }

        public void SetPatientTarget(PawnHealth patient)
        {
            targetPatient = patient;
            tendProgress = 0f;
            if (patient != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, patient.transform.position));
                movement.SetTarget(patient.transform.position);
            }
        }

        public void ClearTask()
        {
            targetPatient = null;
            tendProgress = 0f;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetPatient == null) return;
            // 환자 죽었으면 task 종료
            if (targetPatient.IsDead) { ClearTask(); return; }
            float dist = Vector2.Distance(transform.position, targetPatient.transform.position);
            // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
            if (dist > tendRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
            {
                Debug.Log($"[Doctor] {name} give up patient (pathFailed={movement.LastPathFailed})");
                ClearTask();
                return;
            }
            if (dist <= tendRange)
            {
                movement.ClearTarget();
                tendProgress += Time.deltaTime;
                if (tendProgress >= tendSeconds)
                {
                    // Tend 완료 - 모든 부위 출혈 0 + 손상 부위에 +5 hp 회복
                    if (targetPatient.parts != null)
                    {
                        foreach (var p in targetPatient.parts)
                        {
                            if (p == null) continue;
                            p.bleedRate = 0f;
                            p.bandaged = true;
                            if (p.hp < p.maxHp) p.hp = Mathf.Min(p.maxHp, p.hp + 5);
                        }
                    }
                    // mood thought
                    var th = targetPatient.GetComponent<PawnThoughts>();
                    if (th != null) th.AddThought("치료 받음", +3f, 300f);
                    Debug.Log($"[Doctor] {name} 치료 완료 → {targetPatient.name}");
                    ClearTask();
                }
            }
            else
            {
                movement.SetTarget(targetPatient.transform.position);
            }
        }
    }
}
