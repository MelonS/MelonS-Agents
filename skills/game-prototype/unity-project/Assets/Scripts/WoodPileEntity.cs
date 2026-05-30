using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #116 - 나무 캐면 즉시 inventory 들어가지 말고
    /// 바닥에 통나무 더미 entity 생성.  pawn 의 PawnHauler 가 줍어서 운반.
    ///
    /// 1차 단순화: stockpile 개념 없이 줍기만 (줍는 순간 inventory 차감).
    ///   추후 stockpile 영역 + 거기로 운반 의무 추가 가능.
    /// 다른 hauler 가 같은 pile 중복 reserve 안 하도록 reservedBy 필드.
    /// 60s 동안 안 줍히면 사라짐 (림 vanilla: deteriorate).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class WoodPileEntity : MonoBehaviour
    {
        [SerializeField] private int wood = 5;
        [SerializeField] private float lifetimeSec = 120f;  // 2분 후 사라짐 (legacy)
        public bool InStockpile = false;

        // #152 - 림 vanilla deteriorate (옥외 noroof 2 HP/day, indoor/roof 0).
        //  InStockpile=true 이면 indoor 가정 (현재 roof 시스템 없음 - stockpile 마커 위치 = indoor).
        //  #197 - 운영자 fb "목재 너무 빨리 사라짐": 5s → 30s (6x 느림).
        //   wiki: 1 game day = 2분 = 24sec 인데 5s 마다 -1 = 1day -4 wood 이건 너무 가속.
        //   30s 마다 -1 = 약 wiki 정합 (1day ~2 wood).
        private float lastDeteriorate = -10f;
        private const float DeteriorateInterval = 30f;

        public int Wood => wood;
        public GameObject ReservedBy { get; set; }   // PawnHauler 가 set/clear
        public bool IsReserved => ReservedBy != null;

        private float spawnTime;

        public void SetWood(int amount) { wood = amount; }

        private void Awake()
        {
            spawnTime = Time.time;
        }

        // #214 운영자 fb "아이템이 뿅 이동" — 즉시-credit/teleport 완전 제거.
        //  과거 Pickup() 은 줍는 즉시 ResourceManager.AddWood + Destroy = 순간이동으로,
        //  stockpile 운반(물리)을 우회해 priority-haul 을 무력화했다.  현재 운반은
        //  PawnHauler 가 줍기→물리 carry→내려놓기로 전담하므로 이 경로를 삭제한다.
        //  외부 호출처 없음(확인됨).

        private void Update()
        {
            // #152 - 옥외 pile 부패 (림 wiki 2 HP/day, 1 game day = 2분 = 24 sec 이므로 5초마다 ~1 wood).
            if (!InStockpile && Time.time - lastDeteriorate > DeteriorateInterval)
            {
                lastDeteriorate = Time.time;
                wood = Mathf.Max(0, wood - 1);
                if (wood <= 0)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            // legacy lifetime fallback
            if (Time.time - spawnTime > lifetimeSec)
            {
                Destroy(gameObject);
            }
        }

        // #213 운영자 fb — "목재가 나무 위치에 안 보이고 순간이동".  root cause (a):
        //  Spawn 이 넘겨받은 sprite 가 null 이면 (SceneSetup 미배선 / GameManager 가
        //  woodPileSpriteRuntime!=null 일 때만 static field 를 채움) pile 이 보이지
        //  않게 떨어진다 → 운영자에겐 "나무 쓰러졌는데 목재가 없음" → 나중에 hauler 가
        //  stockpile 에 새 pile 을 만들 때 비로소 보임 = "순간이동".
        //  해결: Spawn 이 null 을 받으면 코드에서 기본 sprite 를 보장한다.  먼저
        //  Resources 에 실 asset 이 있으면 그걸, 없으면 런타임 생성한 통나무더미
        //  텍스처를 쓴다.  이로써 pile 은 항상 "나무 쓰러진 자리"에 눈에 보이게 떨어진다.
        private static Sprite _fallbackSprite;
        public static Sprite EnsureSprite(Sprite candidate)
        {
            if (candidate != null) return candidate;
            if (_fallbackSprite != null) return _fallbackSprite;

            // 1) Resources 에 실제 asset 이 있으면 우선 사용 (디자이너 교체 가능).
            var loaded = Resources.Load<Sprite>("Sprites/wood_pile");
            if (loaded != null) { _fallbackSprite = loaded; return _fallbackSprite; }

            // 2) 없으면 런타임 생성 — 갈색 통나무 더미 (16x12, 4 stacked logs).
            const int W = 16, H = 12;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var clear = new Color(0f, 0f, 0f, 0f);
            var bark    = new Color(0.42f, 0.28f, 0.14f, 1f);   // 짙은 갈
            var barkLit = new Color(0.55f, 0.38f, 0.20f, 1f);   // 윗면 하이라이트
            var ringC   = new Color(0.78f, 0.64f, 0.42f, 1f);   // 단면 나이테
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            // 4개 통나무를 2단으로 쌓음 (가로 누운 원기둥 느낌).
            void Log(int x0, int y0, int w, int h)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int gx = x0 + x, gy = y0 + y;
                        if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;
                        bool top = y == h - 1;
                        bool endRing = x <= 1;          // 왼쪽 단면 = 나이테
                        px[gy * W + gx] = endRing ? ringC : (top ? barkLit : bark);
                    }
            }
            Log(1, 1, 13, 4);   // bottom row
            Log(3, 5, 11, 4);   // top row (살짝 안쪽)
            tex.SetPixels(px);
            tex.Apply();
            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, W, H),
                new Vector2(0.5f, 0.5f), 16f);  // 16 ppu → 약 1 cell
            _fallbackSprite.name = "WoodPile_RuntimeFallback";
            return _fallbackSprite;
        }

        /// <summary>외부 (SceneSetup, TreeEntity) 에서 spawn 헬퍼.
        /// #213 - sprite 가 null 이면 EnsureSprite 가 기본 sprite 를 보장하므로
        ///  pile 은 항상 눈에 보인다.  pile 위치 = pos (벌목 시 = 나무 cell = 쓰러진 자리).
        ///  hauler 가 운반해야만 카운터에 반영된다.</summary>
        public static WoodPileEntity Spawn(Vector3 pos, int amount, Sprite sprite)
        {
            var go = new GameObject($"WoodPile_{amount}");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EnsureSprite(sprite);  // #213 - null 이면 코드 기본 sprite 보장 (항상 가시)
            sr.sortingOrder = 7;  // pawn(10)보다 아래, 타일보다 위
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.9f, 0.6f);
            col.isTrigger = true;  // 충돌 안 막음 - pawn 통과 가능
            var pile = go.AddComponent<WoodPileEntity>();
            pile.SetWood(amount);
            return pile;
        }
    }
}
