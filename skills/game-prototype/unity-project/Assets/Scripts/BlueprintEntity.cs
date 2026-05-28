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

        // #193 - multi-cell entity (침대 1x2 등) footprint.  default 1x1.
        [SerializeField] private Vector2Int footprint = new Vector2Int(1, 1);
        public Vector2Int Footprint => footprint;

        public void SetSize(Vector2Int sz)
        {
            footprint = sz;
            // sprite 가 1:1 비율 + footprint 가 1x2 면 transform.localScale 로 확장.
            //  sprite 가 이미 1x2 비율 (16x32 등) 이면 그대로.
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Vector2 sw = sr.sprite.bounds.size;
                if (sw.x > 0.01f && sw.y > 0.01f)
                    transform.localScale = new Vector3(sz.x / sw.x, sz.y / sw.y, 1f);
            }
        }

        private SpriteRenderer sr;
        // #196 - 진행도 표시 (운영자 fb step 3 "자재 들어갔다는 표시", step 4 "건설 진행도 보임")
        private TextMesh statusLabel;

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
            EnsureStatusLabel();
            UpdateVisual();
        }

        private void EnsureStatusLabel()
        {
            if (statusLabel != null) return;
            var labelGo = new GameObject("BlueprintStatus");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0, 0.6f / Mathf.Max(0.01f, transform.localScale.y), 0);
            statusLabel = labelGo.AddComponent<TextMesh>();
            statusLabel.fontSize = 24;
            statusLabel.characterSize = 0.05f;
            statusLabel.anchor = TextAnchor.MiddleCenter;
            statusLabel.alignment = TextAlignment.Center;
            statusLabel.color = new Color(0.95f, 0.95f, 0.85f, 1f);
            // 한국어 OS font
            string[] cands = { "Malgun Gothic", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" };
            foreach (var n in cands)
            {
                var f = Font.CreateDynamicFontFromOSFont(n, 24);
                if (f != null) { statusLabel.font = f; statusLabel.GetComponent<MeshRenderer>().material = f.material; break; }
            }
            statusLabel.GetComponent<MeshRenderer>().sortingOrder = 25;
        }

        private void UpdateVisual()
        {
            if (sr == null) return;
            float matRatio = (needWood + needStone) > 0
                ? ((float)(collectedWood + collectedStone) / (needWood + needStone))
                : 1f;
            if (!HasAllMaterials)
            {
                // 청록 강조 (자재 대기)
                sr.color = new Color(0.55f, 0.85f, 1.0f, 0.85f + matRatio * 0.10f);
            }
            else
            {
                // 형광 청록 → 흰 진해짐 (건설 중)
                sr.color = new Color(0.65f + Progress * 0.35f, 1.0f, 1.0f, 0.90f + Progress * 0.10f);
            }
            // #196 - 운영자 fb step 3/4: 자재 표시 + 건설 진행도 텍스트.
            if (statusLabel != null)
            {
                if (!HasAllMaterials)
                {
                    string woodLine = needWood > 0 ? $"🪵{collectedWood}/{needWood}" : "";
                    string stoneLine = needStone > 0 ? $"⛏{collectedStone}/{needStone}" : "";
                    statusLabel.text = (woodLine + (woodLine.Length > 0 && stoneLine.Length > 0 ? " " : "") + stoneLine).Trim();
                    statusLabel.color = new Color(1.0f, 0.85f, 0.40f, 1f);  // 노랑 - 자재 대기
                }
                else if (!IsComplete)
                {
                    statusLabel.text = $"건설 {Progress * 100f:F0}%";
                    statusLabel.color = new Color(0.55f, 1.0f, 0.65f, 1f);  // 녹색 - 건설 중
                }
                else
                {
                    statusLabel.text = "";
                }
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
                // #193 - 완성 prefab 도 footprint 의 scale 적용.  Bed 의 Start() 에서 ApplyVisualSize 가 다시 적용되므로 OK 지만 spawn 직후 1 frame 시각 일관성 위해 여기서도.
                var spawnedSr = spawned.GetComponent<SpriteRenderer>();
                if (spawnedSr != null && spawnedSr.sprite != null)
                {
                    Vector2 sw = spawnedSr.sprite.bounds.size;
                    if (sw.x > 0.01f && sw.y > 0.01f)
                        spawned.transform.localScale = new Vector3(footprint.x / sw.x, footprint.y / sw.y, 1f);
                }
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
