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
        // #195 - RimWorld wiki: research bench 2x1 footprint.  sprite 32x16 정합.
        public static readonly Vector2Int FootprintSize = new Vector2Int(2, 1);

        private void Start()
        {
            ApplyVisualSize();
        }

        private void ApplyVisualSize()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;
            Vector2 worldSize = sr.sprite.bounds.size;
            if (worldSize.x < 0.01f || worldSize.y < 0.01f) return;
            transform.localScale = new Vector3(
                FootprintSize.x / worldSize.x,
                FootprintSize.y / worldSize.y,
                1f);
        }

        // Lesson #4 - FindObjects per call 비쌈.  자체 1s 캐시.
        private static PawnEntity[] cachedPawns;
        private static float nextPawnSearchTime = -10f;
        private const float PawnSearchInterval = 1.0f;

        public bool HasResearcherNearby()
        {
            // #169 - 이전 단순 bool 반환.  지금은 ResearcherSpeedSum() 사용 권장.
            return ResearcherSpeedSum() > 0.001f;
        }

        /// <summary>#169 - wiki: research speed 는 manipulation skill 의 sum.
        /// 가까운 모든 살아있는 pawn 의 EffectiveWorkMul(Research) 합계.
        /// 활동 중인 pawn 없으면 0 반환.</summary>
        public float ResearcherSpeedSum()
        {
            // Tally any PawnEntity within radius.  Cheap O(n_pawns) check.
            //  pawn 캐시 1s (모든 bench 가 같은 list 공유 - static)
            if (cachedPawns == null || Time.time >= nextPawnSearchTime)
            {
                cachedPawns = GameObject.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
                nextPawnSearchTime = Time.time + PawnSearchInterval;
            }
            if (cachedPawns == null || cachedPawns.Length == 0) return 0f;
            Vector2 me = transform.position;
            float sum = 0f;
            foreach (var p in cachedPawns)
            {
                if (p == null) continue;
                if (p.IsDead) continue;
                if (Vector2.Distance(p.transform.position, me) > researchRadius) continue;
                var abil = p.GetComponent<PawnAbilities>();
                float mul = abil != null ? abil.EffectiveWorkMul(WorkKind.Research) : 1f;
                // PawnTraits.workSpeedMul (Industrious/Lazy) 도 적용
                var traits = p.GetComponent<PawnTraits>();
                if (traits != null) mul *= traits.workSpeedMul;
                sum += mul;
            }
            return sum;
        }
    }
}
