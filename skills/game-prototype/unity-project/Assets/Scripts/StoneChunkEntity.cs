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

        // #214 운영자 fb: 즉시-credit/teleport 제거.  과거 Pickup() 은 줍는 즉시
        //  ResourceManager.AddStone + Destroy = 순간이동.  운반은 PawnHauler(물리 carry)
        //  전담이므로 이 즉시-적립 경로는 완전히 제거한다.  외부 호출처 없음(확인됨).

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
