using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #119 - 채광 후 바닥에 떨어지는 돌덩이 (WoodPile 패턴 동일).
    /// PawnHauler 가 운반 → ResourceManager.AddStone.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class StoneChunkEntity : MonoBehaviour
    {
        [SerializeField] private int stone = 1;
        [SerializeField] private float lifetimeSec = 180f;  // 3분
        public bool InStockpile = false;  // stockpile stack 보존

        public int Stone => stone;
        public GameObject ReservedBy { get; set; }
        public bool IsReserved => ReservedBy != null;

        private float spawnTime;

        public void SetStone(int v) { stone = v; }

        private void Awake() { spawnTime = Time.time; }

        public bool Pickup()
        {
            if (this == null || gameObject == null) return false;
            ResourceManager.Instance?.AddStone(stone);
            Destroy(gameObject);
            return true;
        }

        private void Update()
        {
            if (Time.time - spawnTime > lifetimeSec) Destroy(gameObject);
        }

        public static StoneChunkEntity Spawn(Vector3 pos, int amount, Sprite sprite)
        {
            var go = new GameObject($"StoneChunk_{amount}");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 7;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.6f);
            col.isTrigger = true;
            var chunk = go.AddComponent<StoneChunkEntity>();
            chunk.SetStone(amount);
            return chunk;
        }
    }
}
