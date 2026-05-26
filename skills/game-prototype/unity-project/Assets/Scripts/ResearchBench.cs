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

        public bool HasResearcherNearby()
        {
            // Tally any PawnEntity within radius.  Cheap O(n_pawns) check.
            PawnEntity[] pawns = GameObject.FindObjectsOfType<PawnEntity>();
            if (pawns == null || pawns.Length == 0) return false;
            Vector2 me = transform.position;
            foreach (var p in pawns)
            {
                if (p == null) continue;
                if (p.IsDead) continue;
                // Idle/drafted/working — doesn't matter — proximity counts.
                if (Vector2.Distance(p.transform.position, me) <= researchRadius)
                    return true;
            }
            return false;
        }
    }
}
