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

        // GameManager 가 SceneSetup 으로부터 sprite 받아 박음
        public static Sprite SharedSprite;

        public int Food => food;
        public GameObject ReservedBy { get; set; }
        public bool IsReserved => ReservedBy != null;

        private float spawnTime;
        public void SetFood(int v) { food = v; }
        private void Awake() { spawnTime = Time.time; }

        public bool Pickup()
        {
            if (this == null || gameObject == null) return false;
            ResourceManager.Instance?.AddFood(food);
            Destroy(gameObject);
            return true;
        }

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
