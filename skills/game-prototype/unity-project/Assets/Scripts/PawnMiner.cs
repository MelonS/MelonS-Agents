using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #119 - 림월드 채광 작업.  PawnChopper 와 동일 패턴.
    ///  StoneVeinEntity target 설정 후 mineRange 도달 → 데미지 누적 → 소진 시 chunks drop.
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnMiner : MonoBehaviour
    {
        [SerializeField] private float mineRange = 1.2f;
        [SerializeField] private float mineDamagePerSec = 20f;  // 광맥 HP 200 → 10s
        [SerializeField] private float giveUpAfterSec = 12f;

        private StoneVeinEntity targetVein;
        private PawnMovement movement;
        private float taskStartTime = -10f;

        public bool HasTask => targetVein != null;
        public StoneVeinEntity Target => targetVein;

        private void Awake() { movement = GetComponent<PawnMovement>(); }

        public void SetVeinTarget(StoneVeinEntity vein)
        {
            targetVein = vein;
            taskStartTime = Time.time;
            if (vein != null) movement.SetTarget(vein.transform.position);
        }

        public void ClearTask()
        {
            targetVein = null;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetVein == null) return;
            if (targetVein.IsDestroyed) { ClearTask(); return; }
            float dist = Vector2.Distance(transform.position, targetVein.transform.position);
            if (Time.time - taskStartTime > giveUpAfterSec && dist > mineRange)
            {
                Debug.Log($"[Miner] {name} give up vein (dist={dist:F2})");
                ClearTask();
                return;
            }
            if (dist <= mineRange)
            {
                movement.ClearTarget();
                var abil = GetComponent<PawnAbilities>();  // #120
                float mul = abil != null ? abil.miningMul * abil.manipulation : 1f;
                // #164 - PawnTraits workSpeedMul 적용
                var traits = GetComponent<PawnTraits>();
                if (traits != null) mul *= traits.workSpeedMul;
                bool done = targetVein.TakeMineDamage(mineDamagePerSec * Time.deltaTime * mul);
                // 채광 skill - Chop 으로 재활용 (#120 에서 Mining 별도 추가 가능)
                var skills = GetComponent<PawnSkills>();
                if (skills != null) skills.AddXP(SkillKind.Chop, mineDamagePerSec * Time.deltaTime * 0.5f);
                if (done) ClearTask();
            }
            else
            {
                movement.SetTarget(targetVein.transform.position);
            }
        }
    }
}
