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
        [SerializeField] private float lifetimeSec = 120f;  // 2분 후 사라짐

        public int Wood => wood;
        public GameObject ReservedBy { get; set; }   // PawnHauler 가 set/clear
        public bool IsReserved => ReservedBy != null;

        private float spawnTime;

        public void SetWood(int amount) { wood = amount; }

        private void Awake()
        {
            spawnTime = Time.time;
        }

        /// <summary>줍기 - inventory 추가 + entity 제거. true 반환 = 성공.</summary>
        public bool Pickup()
        {
            if (this == null || gameObject == null) return false;
            ResourceManager.Instance?.AddWood(wood);
            Destroy(gameObject);
            return true;
        }

        private void Update()
        {
            if (Time.time - spawnTime > lifetimeSec)
            {
                // deteriorate (사라짐)
                Destroy(gameObject);
            }
        }

        /// <summary>외부 (SceneSetup, TreeEntity) 에서 spawn 헬퍼.</summary>
        public static WoodPileEntity Spawn(Vector3 pos, int amount, Sprite sprite)
        {
            var go = new GameObject($"WoodPile_{amount}");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
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
