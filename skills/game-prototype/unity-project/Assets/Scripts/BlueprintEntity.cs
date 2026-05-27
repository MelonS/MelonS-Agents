using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #118 - 림월드 청사진 패턴.
    ///  Build mode 클릭 시 즉시 완성 X.  blueprint entity spawn (반투명 청록).
    ///  PawnBuilder 가 가까운 blueprint 찾아 5초 건설 → 완성 prefab 으로 교체.
    ///
    /// 단순화 (1차):
    ///   - 자원 차감은 BuildManager 가 즉시 (예약/운반 단계 생략).
    ///   - 작업 시간 hardcoded 5초 (건축 skill level 으로 단축 가능).
    ///   - reservedBy (PawnBuilder) 로 중복 건설 방지.
    /// 향후 (림 vanilla):
    ///   - 자원 hauler 가 옮긴 후 build.
    ///   - 부분 진행도 표시 (frame).
    /// </summary>
    public class BlueprintEntity : MonoBehaviour
    {
        [SerializeField] private BuildManager.Mode mode;
        [SerializeField] private GameObject finishedPrefab;
        [SerializeField] private float buildSecondsNeeded = 5f;

        public BuildManager.Mode Mode => mode;
        public GameObject FinishedPrefab => finishedPrefab;
        public float Progress { get; private set; }   // 0~1
        public float BuildSeconds => buildSecondsNeeded;
        public GameObject ReservedBy { get; set; }
        public bool IsReserved => ReservedBy != null;
        public bool IsComplete => Progress >= 1f;

        private SpriteRenderer sr;

        public void Init(BuildManager.Mode m, GameObject prefab, Sprite ghostSprite, float secs = 5f)
        {
            mode = m;
            finishedPrefab = prefab;
            buildSecondsNeeded = secs;
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = ghostSprite;
            sr.sortingOrder = 15;
            sr.color = new Color(0.5f, 0.9f, 1.0f, 0.45f);  // 청록 반투명 (림 blueprint 와 유사)
        }

        /// <summary>PawnBuilder 가 호출 - 한 프레임 분 진행도 추가. true 반환 = 완성됨.</summary>
        public bool AddWork(float deltaSec)
        {
            if (IsComplete) return true;
            Progress += deltaSec / buildSecondsNeeded;
            if (Progress >= 1f)
            {
                Complete();
                return true;
            }
            // 시각 progress - alpha 점점 진해짐
            if (sr != null) sr.color = new Color(0.5f, 0.9f, 1.0f, 0.45f + Progress * 0.4f);
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
