using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #129 - 동물/늑대 죽음 시 바닥에 떨어지는 raw 고기 (WoodPile/StoneChunk 패턴).
    /// PawnHauler 가 운반 → ResourceManager.AddFood.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class MeatPileEntity : MonoBehaviour
    {
        [SerializeField] private int food = 5;
        [SerializeField] private float lifetimeSec = 90f;  // raw meat 빨리 상함 (1.5분)
        public bool InStockpile = false;  // stockpile stack 보존

        // GameManager 가 SceneSetup 으로부터 sprite 받아 박음
        public static Sprite SharedSprite;

        public int Food => food;
        public GameObject ReservedBy { get; set; }
        public bool IsReserved => ReservedBy != null;

        private float spawnTime;
        public void SetFood(int v) { food = v; }
        private void Awake() { spawnTime = Time.time; }

        // #214 운영자 fb: "아이템이 먹거나 하면 뿅 이동" — 즉시-credit/teleport 제거.
        //  과거 Pickup() 은 줍는 즉시 ResourceManager.AddFood + Destroy = 순간이동이었다.
        //  현재 운반은 PawnHauler(물리 carry), 섭취는 PawnNeeds(물리 도착 후 소비)가
        //  전담하므로 이 즉시-적립 경로는 완전히 제거한다.  외부 호출처 없음(확인됨).

        private void Update()
        {
            if (Time.time - spawnTime > lifetimeSec) Destroy(gameObject);
        }

        public static MeatPileEntity Spawn(Vector3 pos, int amount, Sprite sprite)
        {
            var go = new GameObject($"MeatPile_{amount}");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 7;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.6f);
            col.isTrigger = true;
            var m = go.AddComponent<MeatPileEntity>();
            m.SetFood(amount);
            return m;
        }
    }
}
