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
        // #219 운영자 fb "작물 채집하면 고기가 나옴?" — 이 더미는 raw 식량 일반 (고기/농작물/
        //  베리 공용).  표시명을 인스턴스별로 지정해 인스펙터가 올바르게 보여준다(기본=고기).
        public string DisplayName = "고기";
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

        // #215 운영자 fb "먹거리 순간이동" — CropEntity 수확이 물리 식량 더미를 떨어뜨릴
        //  때 sprite 가 null 이어도 보이도록 WoodPile.EnsureSprite 패턴 복제 (붉은 식량 더미).
        private static Sprite _fallbackSprite;
        public static Sprite EnsureSprite(Sprite candidate)
        {
            if (candidate != null) return candidate;
            if (_fallbackSprite != null) return _fallbackSprite;
            var loaded = Resources.Load<Sprite>("Sprites/meat_pile");
            if (loaded != null) { _fallbackSprite = loaded; return _fallbackSprite; }
            const int W = 14, H = 10;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var clear = new Color(0f, 0f, 0f, 0f);
            var food    = new Color(0.78f, 0.34f, 0.26f, 1f);   // 붉은 식량
            var foodLit = new Color(0.90f, 0.50f, 0.38f, 1f);
            var foodDk  = new Color(0.55f, 0.22f, 0.18f, 1f);
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            void Blob(int cx, int cy, int rx, int ry)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
                        if (dx * dx + dy * dy > 1f) continue;
                        bool top = y >= cy + ry - 1;
                        bool bot = y <= cy - ry + 1;
                        px[y * W + x] = top ? foodLit : (bot ? foodDk : food);
                    }
            }
            Blob(5, 4, 4, 3);
            Blob(9, 5, 4, 3);
            tex.SetPixels(px);
            tex.Apply();
            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 16f);
            _fallbackSprite.name = "MeatPile_RuntimeFallback";
            return _fallbackSprite;
        }

        public static MeatPileEntity Spawn(Vector3 pos, int amount, Sprite sprite)
            => Spawn(pos, amount, sprite, "고기");

        // #219 - displayName 으로 고기/농작물/베리 구분 (raw 식량 공용 더미).
        public static MeatPileEntity Spawn(Vector3 pos, int amount, Sprite sprite, string displayName)
        {
            var go = new GameObject($"FoodPile_{displayName}_{amount}");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EnsureSprite(sprite);  // #215 - null 이면 코드 기본 sprite (항상 가시)
            sr.sortingOrder = 7;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.6f);
            col.isTrigger = true;
            var m = go.AddComponent<MeatPileEntity>();
            m.SetFood(amount);
            m.DisplayName = displayName;
            return m;
        }
    }
}
