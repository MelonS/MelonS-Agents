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
            // #190 - 운영자 "청사진 설치 안 됨" 진짜 원인 진단:
            //   placement 후 alpha 0.45 회색 + ghostSprite 그대로 → 운영자 시야에 안 들어옴.
            //   클릭 성공해도 "안 보여서 → 안 됨" 으로 해석.  fix: 청록 outline 강조 + alpha 0.85+.
            //   자재 대기: 청록 (선명) / 자재 충분: 형광 청록 / 건설 중: 흰 진해짐.
            float matRatio = (needWood + needStone) > 0
                ? ((float)(collectedWood + collectedStone) / (needWood + needStone))
                : 1f;
            if (!HasAllMaterials)
            {
                // 청록 강조 (자재 대기 청사진 - "여기 짓기 예약됨" 명확히)
                sr.color = new Color(0.55f, 0.85f, 1.0f, 0.85f + matRatio * 0.10f);
            }
            else
            {
                // 형광 청록 (자재 완비, 건설 중 = 흰색으로 점점 진해짐)
                sr.color = new Color(0.65f + Progress * 0.35f, 1.0f, 1.0f, 0.90f + Progress * 0.10f);
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
                var spawned = Object.Instantiate(finishedPrefab, transform.position, Quaternion.identity);
                // #150 - WallStone mode = stone wall, else wood
                if (mode == BuildManager.Mode.Wall || mode == BuildManager.Mode.WallStone)
                {
                    var w = spawned.GetComponent<WallEntity>();
                    if (w != null)
                    {
                        w.SetMaterial(mode == BuildManager.Mode.WallStone ? WallMaterial.Stone : WallMaterial.Wood);
                    }
                }
                // #154 - Bed quality 별 spawn (SleepingSpot 0.8x / Wood 1.0x / Fine 1.4x).
                if (mode == BuildManager.Mode.Bed
                    || mode == BuildManager.Mode.BedSleepingSpot
                    || mode == BuildManager.Mode.BedFine)
                {
                    var b = spawned.GetComponent<BedEntity>();
                    if (b != null)
                    {
                        var q = mode switch
                        {
                            BuildManager.Mode.BedSleepingSpot => BedQuality.SleepingSpot,
                            BuildManager.Mode.BedFine         => BedQuality.Fine,
                            _                                  => BedQuality.Wood,
                        };
                        b.SetQuality(q);
                    }
                }
            }
            Destroy(gameObject);
        }
    }
}
