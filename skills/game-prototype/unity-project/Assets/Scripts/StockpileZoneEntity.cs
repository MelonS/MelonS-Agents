using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #155 - 레퍼런스 콜로니심 vanilla stockpile priority (5 tier).
    ///   Critical=4 (긴급) → Low=0.  Hauler 는 priority 높은 zone 우선.
    ///   wiki: Critical/Important/Preferred/Normal/Low 다섯 단계.
    /// </summary>
    public enum StockpilePriority { Low = 0, Normal = 1, Preferred = 2, Important = 3, Critical = 4 }

    /// <summary>
    /// Z2 (운영자 '아이템 저장공간 설정') — coarse allowed-items filter on a stockpile.
    ///  레퍼런스 콜로니심 Storage 탭 category 체크트리의 최소 coarse 버전: PawnSim 이 실제로
    ///  운반하는 3종(목재/석재/식량)만.  [Flags] 라 한 zone 이 여러 종류를 동시 허용
    ///  가능(wood+stone 등) 하고, 단일 종류(wood-only/food-only)도 가능.
    ///  Chunk(돌무더기/refuse)는 PawnSim 에 별도 refuse item 이 없어 Stone 으로 접음
    ///  (spec Z2: "add Chunk if a distinct refuse item exists, else fold chunks into Stone").
    /// </summary>
    [System.Flags]
    public enum StockItemKind
    {
        None  = 0,
        Wood  = 1 << 0,
        Stone = 1 << 1,   // 석재 + chunk(refuse) 를 함께 담는 coarse 분류
        Food  = 1 << 2,
        All   = Wood | Stone | Food,
    }

    /// <summary>
    /// #121 - 레퍼런스 콜로니심 stockpile zone.  #155 - priority 추가.
    ///  Hauler 가 pile/chunk 를 줍어서 priority 높은 zone center 로 가져가서 drop.
    ///  Zone 우클릭 = priority 한 단계 상승 (Low → Normal → Preferred → ... → Low 순환).
    ///
    /// 시각: tint by priority (Critical 빨강, Normal 노랑, Low 회색).
    /// </summary>
    public class StockpileZoneEntity : MonoBehaviour
    {
        // zone 의 cell coverage - 1x1 (단일 cell stockpile).  큰 영역은 여러 zone instance.
        [SerializeField] private StockpilePriority priority = StockpilePriority.Normal;

        // Z2 — 이 zone 이 받는 아이템 종류.  기본 = 전부 허용(All) → 기존 동작과
        //  동일(필터 없는 stockpile).  Z2b UI / Z3 dumping preset 가 이걸 좁힌다.
        [SerializeField] private StockItemKind allowedKinds = StockItemKind.All;

        private SpriteRenderer sr;

        public Vector2 ZoneCenter => transform.position;
        public StockpilePriority Priority => priority;
        public StockItemKind AllowedKinds => allowedKinds;

        /// <summary>Z2 — 이 zone 이 해당 종류를 받는가.  hauler 가 FindBest 에서 사용.</summary>
        public bool Accepts(StockItemKind kind) => (allowedKinds & kind) != 0;

        /// <summary>Z2b — 한 종류 허용 on/off 토글(coarse UI 용).  최소 한 종류는 남도록
        ///  마지막 하나를 끄려 하면 무시(전부 거부하는 죽은 zone 방지).</summary>
        public void ToggleKind(StockItemKind kind)
        {
            var next = allowedKinds ^ kind;
            if (next == StockItemKind.None) return;   // 전부 끄기 금지
            allowedKinds = next;
        }

        /// <summary>Z3 — dumping/preset 등에서 허용 종류를 직접 지정.</summary>
        public void SetAllowedKinds(StockItemKind kinds)
        {
            allowedKinds = kinds == StockItemKind.None ? StockItemKind.All : kinds;
        }
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

        /// <summary>#155 - priority 높은 zone 우선, 동률이면 가까운 zone.
        ///  Z2 back-compat: 종류 무관(All) 검색.</summary>
        public static StockpileZoneEntity FindBest(Vector2 from)
            => FindBest(from, StockItemKind.All);

        /// <summary>Z2 — 운반물 종류(kind) 를 받는 zone 중 priority 높은 것 우선,
        ///  동률이면 가까운 것.  Accepts(kind)==false 인 zone(예: food-only zone 에
        ///  목재 운반)은 건너뛴다 → wood-only / food-only stockpile 가능.  종류를 받는
        ///  zone 이 하나도 없으면 null(=호출측이 inventory fallback).</summary>
        public static StockpileZoneEntity FindBest(Vector2 from, StockItemKind kind)
        {
            var arr = Object.FindObjectsByType<StockpileZoneEntity>(FindObjectsSortMode.None);
            StockpileZoneEntity best = null;
            int bestPrio = -1;
            float bestSq = float.MaxValue;
            foreach (var z in arr)
            {
                if (z == null) continue;
                if (!z.Accepts(kind)) continue;        // Z2 — 종류 필터
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
            StockpilePriority priority = StockpilePriority.Normal,
            StockItemKind allowed = StockItemKind.All)
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
            z.SetAllowedKinds(allowed);   // Z2/Z3 — preset 허용 종류 (기본 All)
            z.SetPriority(priority);
            return z;
        }
    }
}
