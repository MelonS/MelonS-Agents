using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #142 (운영자 fb v2) - 레퍼런스 콜로니심 정상 청사진 흐름.
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

        /// <summary>R 로 90° 돌려 놓은 청사진인가.  완성 시 SpawnFinished 에 그대로 넘겨
        ///  완성물이 같은 방향으로 서게 한다 (안 넘기면 다 짓는 순간 침대가 홱 돌아간다).</summary>
        [SerializeField] private bool isRotated = false;
        public bool IsRotated => isRotated;

        /// <summary>sz 는 **회전 전** footprint.  rotated=true 면 transform 을 90° 돌린다 —
        ///  로컬 스케일·콜라이더는 그대로 두고 회전이 월드 가로/세로를 교환하게 한다.</summary>
        public void SetSize(Vector2Int sz, bool rotated = false)
        {
            var c2 = GetComponent<BoxCollider2D>();
            if (c2 != null) c2.size = new Vector2(sz.x * 0.9f, sz.y * 0.9f);   // T2 footprint 반영
            footprint = sz;
            isRotated = rotated && sz.x != sz.y;
            transform.rotation = isRotated ? Quaternion.Euler(0, 0, 90f) : Quaternion.identity;
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
            // 첫사이클 T2 (2026-06-12) — 청사진 무콜라이더로 물리쿼리 소비처 5경로
            //  (중복가드/우클릭 취소·우선건설/영역취소/툴팁)가 전부 죽어 있었고,
            //  드래그마다 앵커 셀에 2장씩 적치됐다.  isTrigger 라 통행/전투 무영향.
            var bpCol = GetComponent<BoxCollider2D>();
            if (bpCol == null) bpCol = gameObject.AddComponent<BoxCollider2D>();
            bpCol.isTrigger = true;
            bpCol.size = Vector2.one * 0.9f;
            EnsureStatusLabel();
            UpdateVisual();
        }

        // 운영자 2026-07-24 "% 말고 게이지로 + % 하나도 안 보임": TextMesh(%·이모지
        //  tofu 위험·폰트 의존) → 월드 스프라이트 게이지 바로 전면 교체.  자재 대기
        //  = 앰버(채움 비율), 건설 중 = 녹색(진행률).  폰트 무의존이라 비가시 원천 해소.
        private Transform barRoot;
        private SpriteRenderer barBg, barFill;
        private static Sprite _barSprite;
        private static Sprite BarSprite()
        {
            if (_barSprite == null)
                _barSprite = Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
            return _barSprite;
        }

        private void EnsureStatusLabel()
        {
            if (barRoot != null) return;
            var go = new GameObject("BlueprintBar");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0.55f / Mathf.Max(0.01f, transform.localScale.y), 0);
            barRoot = go.transform;
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(barRoot, false);
            bgGo.transform.localPosition = new Vector3(-0.45f, 0f, 0f);
            bgGo.transform.localScale = new Vector3(0.90f, 0.14f, 1f);
            barBg = bgGo.AddComponent<SpriteRenderer>();
            barBg.sprite = BarSprite();
            barBg.color = new Color(0f, 0f, 0f, 0.55f);
            // UI겹침 P1-8 계승: NightOverlay(25) 위 26/27 — 밤에도 판독 가능.
            barBg.sortingOrder = 26;
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barRoot, false);
            fillGo.transform.localPosition = new Vector3(-0.43f, 0f, 0f);
            fillGo.transform.localScale = new Vector3(0f, 0.10f, 1f);
            barFill = fillGo.AddComponent<SpriteRenderer>();
            barFill.sprite = BarSprite();
            barFill.sortingOrder = 27;
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
            // #196 개정 (운영자 2026-07-24): 자재/진행 = 게이지 바.
            if (barRoot != null && barFill != null)
            {
                if (IsComplete)
                {
                    barRoot.gameObject.SetActive(false);
                }
                else
                {
                    barRoot.gameObject.SetActive(true);
                    float ratio = HasAllMaterials ? Progress : matRatio;
                    barFill.color = HasAllMaterials
                        ? new Color(0.55f, 1.0f, 0.65f, 0.95f)    // 녹색 - 건설 진행
                        : new Color(1.0f, 0.78f, 0.35f, 0.95f);   // 앰버 - 자재 채움
                    barFill.transform.localScale =
                        new Vector3(0.86f * Mathf.Clamp01(ratio), 0.10f, 1f);
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
            // #249 나무 위 건축 — 완성 시 footprint 와 겹친 나무를 제거(the reference sim: 식물 위 건설 시
            //  식물 클리어).  자재는 안 줌(단순 제거).
            {
                Vector2 sz = new Vector2(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
                foreach (var h in Physics2D.OverlapBoxAll(transform.position, sz * 0.9f, 0f))
                {
                    if (h == null) continue;
                    var tree = h.GetComponent<TreeEntity>();
                    if (tree != null) Object.Destroy(tree.gameObject);
                    // 2026-07-30 — 광맥도 같은 폴백을 준다.  배치 시 BuildManager.AutoClearObstacles
                    //  가 채광을 지정하므로 보통은 광부가 먼저 캐서 석재가 들어온다.  다만
                    //  건설이 채광보다 먼저 끝나는 경우가 있고, 그때 구조물이 광맥 위에 겹쳐
                    //  남으면 운영자가 2026-06-12 에 지적한 "돌이랑 침대가 겹쳐"가 재발한다.
                    var vein = h.GetComponent<StoneVeinEntity>();
                    if (vein != null) Object.Destroy(vein.gameObject);
                }
            }
            if (finishedPrefab != null)
            {
                // #버그헌트2(2026-06-04): 완성 구조물 스폰을 BuildManager.SpawnFinished 단일 경로로
                //  통일(DRY).  prefab 활성화/footprint scale/벽 재질/침대 품질 + StructureTag(mode)
                //  스탬프를 모두 그 함수가 처리 → save/load 재구성과 '동일한 결과'가 보장된다.
                //  (이전엔 이 블록에 인라인으로 중복돼 있어 재구성 경로와 드리프트 위험이 있었다.)
                GameObject spawned = (BuildManager.Instance != null)
                    ? BuildManager.Instance.SpawnFinished(mode, transform.position, isRotated)
                    : null;
                if (spawned == null)
                {
                    // 안전 폴백(이론상 도달 안 함 — BuildManager 가 이 청사진을 만들었으므로 Instance 존재).
                    spawned = Object.Instantiate(finishedPrefab, transform.position, Quaternion.identity);
                    spawned.SetActive(true);
                    spawned.hideFlags = HideFlags.None;
                    var tag = spawned.GetComponent<StructureTag>();
                    if (tag == null) tag = spawned.AddComponent<StructureTag>();
                    tag.modeInt = (int)mode;
                }
            }
            // W-M2-01 Lane C / wiki Dim2 #1: 벽 등 구조물 완성 시 건설 사운드 1회 재생 (throttled).
            // PlayBuild() 자체가 AudioBank 에서 throttle 되므로 완성 1회 = 사운드 1회.
            AudioBank.Instance?.PlayBuild();
            Destroy(gameObject);
        }
    }
}
