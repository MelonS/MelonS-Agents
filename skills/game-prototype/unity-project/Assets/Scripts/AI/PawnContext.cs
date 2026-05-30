using UnityEngine;

namespace MelonS.GameProto.AI
{
    /// <summary>
    /// R5 - IPawnAction 들이 의존하는 컴포넌트 묶음.
    /// 각 action 이 GetComponent 를 반복하지 않도록 PawnUtilityAI 가
    /// 한 번 만들어 매번 같은 ctx 를 넘김.
    /// 모든 필드 nullable - action 이 null 체크 후 skip 가능 (legacy 호환).
    /// </summary>
    public class PawnContext
    {
        public PawnEntity entity;
        public PawnMovement movement;
        public PawnChopper chopper;
        public PawnGatherer gatherer;
        public PawnHunter hunter;
        public PawnCook cook;
        public PawnHauler hauler;  // #116
        public PawnHarvester harvester;  // #202 — ripe crop harvest (survival loop)
        public PawnBuilder builder;  // #118 — blueprint 건설
        public PawnMiner miner;   // #119 — 채광
        public PawnDoctor doctor; // #125 — 의료
        public PawnNeeds needs;
        public PawnSkills skills;
        public Transform transform;
        public float idleWanderRadius = 3f;

        /// <summary>이미 어떤 외부 시스템 (chopper/gatherer/etc.) 가 진행중인 task 있는지.</summary>
        public bool HasActiveTask()
        {
            if (chopper  != null && chopper.HasTask)  return true;
            if (gatherer != null && gatherer.HasTask) return true;
            if (hunter   != null && hunter.HasTask)   return true;
            if (cook     != null && cook.HasTask)     return true;
            if (hauler   != null && hauler.HasTask)   return true;
            if (harvester != null && harvester.HasTask) return true;  // #202
            if (builder  != null && builder.HasTask)  return true;
            if (miner    != null && miner.HasTask)    return true;
            if (doctor   != null && doctor.HasTask)   return true;
            return false;
        }
    }
}
