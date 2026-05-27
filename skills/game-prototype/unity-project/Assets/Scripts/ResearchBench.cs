using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 52 — Research bench prefab.  Built like Stove (B-mode).
    /// Reports HasResearcherNearby() = true if any pawn is within research
    /// radius 1.5 units.  Cost: 25 wood.  Sortng order 5 (above tiles).
    /// </summary>
    public class ResearchBench : MonoBehaviour
    {
        [SerializeField] private float researchRadius = 1.5f;

        // Lesson #4 - FindObjects per call 비쌈.  자체 1s 캐시.
        private static PawnEntity[] cachedPawns;
        private static float nextPawnSearchTime = -10f;
        private const float PawnSearchInterval = 1.0f;

        public bool HasResearcherNearby()
        {
            // Tally any PawnEntity within radius.  Cheap O(n_pawns) check.
            //  pawn 캐시 1s (모든 bench 가 같은 list 공유 - static)
            if (cachedPawns == null || Time.time >= nextPawnSearchTime)
            {
                cachedPawns = GameObject.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
                nextPawnSearchTime = Time.time + PawnSearchInterval;
            }
            if (cachedPawns == null || cachedPawns.Length == 0) return false;
            Vector2 me = transform.position;
            foreach (var p in cachedPawns)
            {
                if (p == null) continue;
                if (p.IsDead) continue;
                // Idle/drafted/working - doesn't matter - proximity counts.
                if (Vector2.Distance(p.transform.position, me) <= researchRadius)
                    return true;
            }
            return false;
        }
    }
}
