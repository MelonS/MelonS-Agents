using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #142 (운영자 fb v2) - 림월드 정상 청사진 흐름.
    ///   1. Architect → 벽 선택 → 클릭으로 청사진 spawn (자원 차감 X).
    ///   2. 청사진 = needWood/needStone 표시.
    ///   3. Hauler 가 stockpile 의 wood pile 을 청사진까지 운반 (자재 넣음).
    ///      도착 시 청사진.collectedWood += amount.
    ///   4. collectedWood >= needWood 이면 PawnBuilder 가 건설 작업 진행.
    ///   5. 건설 완료 → real prefab 교체.
    ///
    /// 이전 (#118): spawn 시 즉시 자원 차감, hauler 단계 없음.
    /// </summary>
    public class BlueprintEntity : MonoBehaviour
    {
        [SerializeField] private BuildManager.Mode mode;
        [SerializeField] private GameObject finishedPrefab;
        [SerializeField] private float buildSecondsNeeded = 5f;
        [SerializeField] public int needWood = 0;
        [SerializeField] public int needStone = 0;
        public int collectedWood = 0;
        public int collectedStone = 0;

        public BuildManager.Mode Mode => mode;
        public GameObject FinishedPrefab => finishedPrefab;
        public float Progress { get; private set; }
        public float BuildSeconds => buildSecondsNeeded;
        public GameObject ReservedBy { get; set; }
        public bool IsReserved => ReservedBy != null;
        public bool IsComplete => Progress >= 1f;

        // 자재 채워졌나 (PawnBuilder 가 작업 시작 조건)
        public bool HasAllMaterials =>
            collectedWood >= needWood && collectedStone >= needStone;

        // 자재 운반 reservation (hauler)
        public GameObject HaulReservedBy { get; set; }

        private SpriteRenderer sr;

        public void Init(BuildManager.Mode m, GameObject prefab, Sprite ghostSprite,
            int wood, int stone, float secs = 5f)
        {
            mode = m;
            finishedPrefab = prefab;
            buildSecondsNeeded = secs;
            needWood = wood;
            needStone = stone;
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = ghostSprite;
            sr.sortingOrder = 15;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (sr == null) return;
            // 자재 부족 = 회색 반투명, 자재 충분 = 청록, 건설 중 = 점점 진해짐
            float matRatio = (needWood + needStone) > 0
                ? ((float)(collectedWood + collectedStone) / (needWood + needStone))
                : 1f;
            if (!HasAllMaterials)
            {
                // 회색 반투명 (자재 대기)
                sr.color = new Color(0.7f, 0.7f, 0.75f, 0.45f + matRatio * 0.2f);
            }
            else
            {
                // 청록 (건설 가능)
                sr.color = new Color(0.5f, 0.9f, 1.0f, 0.50f + Progress * 0.4f);
            }
        }

        /// <summary>hauler 가 호출 - 자재 넣기.</summary>
        public void DepositWood(int amount)
        {
            collectedWood = Mathf.Min(needWood, collectedWood + amount);
            UpdateVisual();
        }
        public void DepositStone(int amount)
        {
            collectedStone = Mathf.Min(needStone, collectedStone + amount);
            UpdateVisual();
        }

        public int RemainingWood => Mathf.Max(0, needWood - collectedWood);
        public int RemainingStone => Mathf.Max(0, needStone - collectedStone);

        /// <summary>PawnBuilder 가 호출 - 자재 충분 시에만 진행.</summary>
        public bool AddWork(float deltaSec)
        {
            if (IsComplete) return true;
            if (!HasAllMaterials) return false;  // 자재 미달 시 작업 X
            Progress += deltaSec / buildSecondsNeeded;
            if (Progress >= 1f)
            {
                Complete();
                return true;
            }
            UpdateVisual();
            return false;
        }

        private void Complete()
        {
            if (finishedPrefab != null)
            {
                Object.Instantiate(finishedPrefab, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
    }
}
