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

        /// <summary>[DEPRECATED #212] 줍는 즉시 inventory +N + entity 제거.
        ///  림월드 정상 흐름(줍기→stockpile 운반→도착 시 적립)과 충돌하므로
        ///  현재 hauler 경로(PawnHauler)는 이걸 호출하지 않는다.  호출 시 목재가
        ///  stockpile 을 거치지 않고 카운터에 즉시 들어가 priority-haul 을 무력화함.
        ///  남겨둔 것은 호환용일 뿐 — 신규 코드에서 사용 금지.</summary>
        public bool Pickup()
        {
            if (this == null || gameObject == null) return false;
            ResourceManager.Instance?.AddWood(wood);
            Destroy(gameObject);
            return true;
        }

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

        /// <summary>외부 (SceneSetup, TreeEntity) 에서 spawn 헬퍼.
        /// #212 - sprite 가 null 이어도 pile 은 생성한다.  과거엔 null sprite 면
        ///  호출부(TreeEntity)가 즉시 AddWood 로 우회 → "벌목 즉시 적립" 버그.
        ///  이제 pile 은 항상 물리적으로 존재(보이지 않을 수 있음)하며 hauler 가
        ///  운반해야만 카운터에 반영된다.  pile 위치 = pos (벌목 시 = 나무 cell).</summary>
        public static WoodPileEntity Spawn(Vector3 pos, int amount, Sprite sprite)
        {
            var go = new GameObject($"WoodPile_{amount}");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;  // null 허용 — 보이지 않아도 entity 는 haulable
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
