using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #155 - 림월드 vanilla stockpile priority (5 tier).
    ///   Critical=4 (긴급) → Low=0.  Hauler 는 priority 높은 zone 우선.
    ///   wiki: Critical/Important/Preferred/Normal/Low 다섯 단계.
    /// </summary>
    public enum StockpilePriority { Low = 0, Normal = 1, Preferred = 2, Important = 3, Critical = 4 }

    /// <summary>
    /// #121 - 림월드 stockpile zone.  #155 - priority 추가.
    ///  Hauler 가 pile/chunk 를 줍어서 priority 높은 zone center 로 가져가서 drop.
    ///  Zone 우클릭 = priority 한 단계 상승 (Low → Normal → Preferred → ... → Low 순환).
    ///
    /// 시각: tint by priority (Critical 빨강, Normal 노랑, Low 회색).
    /// </summary>
    public class StockpileZoneEntity : MonoBehaviour
    {
        // zone 의 cell coverage - 1x1 (단일 cell stockpile).  큰 영역은 여러 zone instance.
        [SerializeField] private StockpilePriority priority = StockpilePriority.Normal;

        private SpriteRenderer sr;

        public Vector2 ZoneCenter => transform.position;
        public StockpilePriority Priority => priority;
        public string PriorityKr => priority switch
        {
            StockpilePriority.Critical  => "긴급",
            StockpilePriority.Important => "중요",
            StockpilePriority.Preferred => "우선",
            StockpilePriority.Normal    => "보통",
            StockpilePriority.Low       => "낮음",
            _ => "보통",
        };

        // priority 별 tint - Critical (빨강) → Normal (노랑) → Low (회색)
        public static readonly Color[] PriorityTints = {
            new Color(0.55f, 0.55f, 0.55f, 0.45f),  // Low - grey
            new Color(0.95f, 0.85f, 0.30f, 0.45f),  // Normal - yellow (default)
            new Color(0.50f, 0.95f, 0.40f, 0.50f),  // Preferred - green
            new Color(0.40f, 0.75f, 1.00f, 0.55f),  // Important - blue
            new Color(1.00f, 0.40f, 0.30f, 0.60f),  // Critical - red
        };

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            ApplyTint();
        }

        public void SetPriority(StockpilePriority p)
        {
            priority = p;
            ApplyTint();
        }

        /// <summary>우클릭 = priority 한 단계 순환 (Low → ... → Critical → Low).</summary>
        public void CyclePriority()
        {
            int next = ((int)priority + 1) % 5;
            SetPriority((StockpilePriority)next);
        }

        private void ApplyTint()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = PriorityTints[(int)priority];
        }

        /// <summary>거리만 본다 (back-compat).  #155 - hauler 는 FindBest 권장.</summary>
        public static StockpileZoneEntity FindNearest(Vector2 from)
        {
            var arr = Object.FindObjectsByType<StockpileZoneEntity>(FindObjectsSortMode.None);
            StockpileZoneEntity best = null;
            float bestSq = float.MaxValue;
            foreach (var z in arr)
            {
                if (z == null) continue;
                float sq = ((Vector2)z.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = z; }
            }
            return best;
        }

        /// <summary>#155 - priority 높은 zone 우선, 동률이면 가까운 zone.</summary>
        public static StockpileZoneEntity FindBest(Vector2 from)
        {
            var arr = Object.FindObjectsByType<StockpileZoneEntity>(FindObjectsSortMode.None);
            StockpileZoneEntity best = null;
            int bestPrio = -1;
            float bestSq = float.MaxValue;
            foreach (var z in arr)
            {
                if (z == null) continue;
                int p = (int)z.priority;
                float sq = ((Vector2)z.transform.position - from).sqrMagnitude;
                if (p > bestPrio || (p == bestPrio && sq < bestSq))
                {
                    bestPrio = p; bestSq = sq; best = z;
                }
            }
            return best;
        }

        public static StockpileZoneEntity Spawn(Vector3 pos, Sprite markerSprite,
            StockpilePriority priority = StockpilePriority.Normal)
        {
            var go = new GameObject($"Stockpile_{pos.x:F0}_{pos.y:F0}");
            go.transform.position = new Vector3(Mathf.Floor(pos.x) + 0.5f, Mathf.Floor(pos.y) + 0.5f, 0);
            if (markerSprite != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = markerSprite;
                sr.sortingOrder = 2;  // 타일 위, entity 아래
            }
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one * 0.95f;
            col.isTrigger = true;
            var z = go.AddComponent<StockpileZoneEntity>();
            z.SetPriority(priority);
            return z;
        }
    }
}
