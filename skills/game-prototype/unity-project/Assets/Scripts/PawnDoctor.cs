using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// #125 - 레퍼런스 콜로니심 의료 (단순화).
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
            // #199 C2 — release previous patient on switch (the reference sim job-switch).
            if (targetPatient != null && targetPatient != patient)
                MelonS.GameProto.AI.ReservationManager.Release(targetPatient, gameObject);
            targetPatient = patient;
            tendProgress = 0f;
            if (patient != null)
            {
                MelonS.GameProto.AI.ReservationManager.TryReserve(patient, gameObject);
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, patient.transform.position));
                movement.SetTarget(patient.transform.position);
            }
        }

        public void ClearTask()
        {
            if (targetPatient != null)
                MelonS.GameProto.AI.ReservationManager.Release(targetPatient, gameObject);
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
                Debug.Log($"[Doctor] {GetComponent<PawnEntity>()?.PawnName ?? name} give up patient (pathFailed={movement.LastPathFailed})");
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
                    Debug.Log($"[Doctor] {GetComponent<PawnEntity>()?.PawnName ?? name} 치료 완료 → {targetPatient.GetComponent<PawnEntity>()?.PawnName ?? targetPatient.name}");
                    // 감사 rank7: 치료 완료 시각 피드백(피격 -N / 수확 +N 과 대칭).
                    FloatingText.Spawn(targetPatient.transform.position + Vector3.up * 0.6f,
                        "+치료", new Color(0.60f, 0.80f, 1.00f));
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
