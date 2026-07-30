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
        // 머리위 상태 라벨(PawnNameLabel)이 "연구" 표시 판정에 쓰는 read-only 반경.
        public float Radius => researchRadius;
        // #195 - the reference sim wiki: research bench 2x1 footprint.  sprite 32x16 정합.
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
                // 2026-07-31 — 반경 안에 **있기만 하면** 연구가 오르던 것을 막는다.
                //  시작 집 안에 연구대가 있어서, 요리하러 들어온 사람도 밥 먹는 사람도
                //  전부 연구 진행에 기여했다 = 사실상 "실내에 있으면 연구 중".  그러면
                //  직업 탭의 '연구' 우선순위는 결과를 바꾸지 못하는 장식이 된다.
                //  이제 DoResearchAction 이 실제로 연구를 작업으로 잡은 폰만 센다
                //  (판정 정본은 PawnResearchWork — 머리위 라벨도 같은 값을 읽는다).
                var work = p.GetComponent<PawnResearchWork>();
                if (work == null || !work.IsResearching) continue;
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
