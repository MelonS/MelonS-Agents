using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #121 - 림월드 stockpile zone.
    ///  Hauler 가 pile/chunk 를 줍어서 가장 가까운 zone center 로 가져가서 drop.
    ///  Zone 자체는 노란 반투명 marker.
    ///
    /// 1차 단순화: zone 안에 들어오면 그냥 AddWood/AddStone (inventory 직접).
    ///   추후: zone 마다 자원 저장 + 시각화 가능.
    /// </summary>
    public class StockpileZoneEntity : MonoBehaviour
    {
        // zone 의 cell coverage - 1x1 (단일 cell stockpile).  큰 영역은 여러 zone instance.

        public Vector2 ZoneCenter => transform.position;

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

        public static StockpileZoneEntity Spawn(Vector3 pos, Sprite markerSprite)
        {
            var go = new GameObject($"Stockpile_{pos.x:F0}_{pos.y:F0}");
            go.transform.position = new Vector3(Mathf.Floor(pos.x) + 0.5f, Mathf.Floor(pos.y) + 0.5f, 0);
            if (markerSprite != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = markerSprite;
                sr.sortingOrder = 2;  // 타일 위, entity 아래
                sr.color = new Color(0.95f, 0.85f, 0.30f, 0.45f);  // 노란 반투명
            }
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one * 0.95f;
            col.isTrigger = true;
            go.AddComponent<StockpileZoneEntity>();
            return go.GetComponent<StockpileZoneEntity>();
        }
    }
}
