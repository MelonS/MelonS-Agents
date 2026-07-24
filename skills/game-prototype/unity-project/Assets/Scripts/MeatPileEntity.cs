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
        public bool InStockpile = false;  // stockpile stack 보존

        // GameManager 가 SceneSetup 으로부터 sprite 받아 박음
        public static Sprite SharedSprite;

        // 운영자 2026-06-02: 통째 즉시소멸 대신 개별 내구도가 천천히 닳으며 점점 흐려짐.
        //  raw 식량은 부패성이므로 wood 보다 빠른 decayPerDay(옥외, !InStockpile).  0 에서 소멸.
        //  기존 lifetimeSec(90s) 통째 Destroy 제거.
        [SerializeField] private float durability = 100f;
        [SerializeField] private float decayPerDay = 20f;  // 100/20 = 5 game-day ≈ 120s, 점진 fade
        private SpriteRenderer _sr;

        public int Food => food;
        // #219 운영자 fb "작물 채집하면 고기가 나옴?" — 이 더미는 raw 식량 일반 (고기/농작물/
        //  베리 공용).  표시명을 인스턴스별로 지정해 인스펙터가 올바르게 보여준다(기본=고기).
        public string DisplayName = "고기";
        public GameObject ReservedBy { get; set; }
        public bool IsReserved => ReservedBy != null;

        // 폴리싱(#61): 양에 비례해 더미가 커 보이도록(WoodPile 공용 스케일). spawn·shrink 동기.
        public void SetFood(int v)
        {
            food = v;
            // 아트 v2: DisplayName(고기/간편식/베리) → kind 매핑해 스프라이트 3단계.
            var v2 = ItemArt32.Stage(ItemArt32.KindFromFoodName(DisplayName), v);
            if (v2 != null)
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = v2;
                transform.localScale = Vector3.one;
            }
            else transform.localScale = Vector3.one * WoodPileEntity.PileScale(v);
        }
        // 부패 속도 외부 설정(작물/베리는 느리게).  CropEntity 수확 등에서 호출 가능.
        public void SetDecayPerDay(float v) { decayPerDay = Mathf.Max(0f, v); }
        // 첫사이클 T10 — hauler 가 픽업 시 정체성 캡처용: decayPerDay → lifetime 역산
        //  (Spawn(lifetime)의 2400/lifetime 변환과 대칭).
        public float LifetimeForHaul => decayPerDay > 0.01f ? 2400f / decayPerDay : 600f;
        private void Awake() { _sr = GetComponent<SpriteRenderer>(); }

        // #214 운영자 fb: "아이템이 먹거나 하면 뿅 이동" — 즉시-credit/teleport 제거.
        //  과거 Pickup() 은 줍는 즉시 ResourceManager.AddFood + Destroy = 순간이동이었다.
        //  현재 운반은 PawnHauler(물리 carry), 섭취는 PawnNeeds(물리 도착 후 소비)가
        //  전담하므로 이 즉시-적립 경로는 완전히 제거한다.  외부 호출처 없음(확인됨).

        private void Update()
        {
            if (!InStockpile)
            {
                // 게임루프 차순위 (2026-06-12) — 폭풍 물리 비용: 야외 방치 식량은 폭풍
                //  동안 4배로 상한다.  '텍스트+무드'뿐이던 폭풍의 첫 물리 결과 — 수확물을
                //  저장고로 들이는 운반(InStockpile=보호)이 폭풍 대비 행동이 된다.
                float stormMul = WeatherController.Instance != null
                    && WeatherController.Instance.Current == WeatherKind.Storm ? 4f : 1f;
                // 밸런스 A1(2026-07-24): /24f 구 시계 잔재 → /1000f (고기 rot 2게임일 의도 복원).
                durability -= decayPerDay * stormMul * (Time.deltaTime / 1000f);
                if (durability <= 0f) { Destroy(gameObject); return; }
            }
            if (_sr != null)
            {
                var c = _sr.color;
                c.a = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(durability / 100f));
                _sr.color = c;
            }
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
            => Spawn(pos, amount, sprite, "고기", 90f);

        public static MeatPileEntity Spawn(Vector3 pos, int amount, Sprite sprite, string displayName)
            => Spawn(pos, amount, sprite, displayName, 90f);

        // #219/#223 - displayName 으로 고기/농작물/베리 구분 + 종류별 수명.  raw 고기는 빨리
        //  상하지만(90s) 작물(쌀)/베리는 오래 간다.  #223 운영자 fb 후속(식량 빈약): 작물
        //  더미가 운반 전에 90s 만에 상해 식량 적립이 불안정했던 것 → 작물/베리 수명 ↑.
        public static MeatPileEntity Spawn(Vector3 pos, int amount, Sprite sprite, string displayName, float lifetime)
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
            // 아트 v2: SetFood 가 DisplayName 으로 kind(고기/간편식/베리)를 고르므로 먼저 지정.
            m.DisplayName = displayName;
            m.SetFood(amount);
            // 기존 lifetime(통째 소멸까지 초) → durability decayPerDay 로 환산:
            //  총 fade 시간 ≈ 기존 lifetime 이 되도록 decayPerDay = 100 / (lifetime/24).
            //  작물/베리(긴 lifetime)=느린 부패, 고기(짧은 lifetime)=빠른 부패 보존.
            m.SetDecayPerDay(lifetime > 0f ? 2400f / lifetime : 20f);
            return m;   // 양에 비례한 시각 스케일은 SetFood 에서 적용됨(#61)
        }
    }
}
