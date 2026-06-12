using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 67-68 — Crop tile.  Grows over game time when in daylight.
    /// Stages:
    ///   0.0–0.33  새싹 (sprout) — green light
    ///   0.33–0.66 자란 (grown)  — green darker
    ///   0.66–1.00 익은 (ripe)   — golden — harvestable
    /// When ripe, right-click harvests → +5 food, growth resets to 0.
    /// Growth rate: ~ 1.0 per 80 game-minutes (= ~13 real-seconds at 1x speed,
    ///   since 1 day = 4 real-min = 1440 game-min).
    /// Growth pauses at night (alpha < 0.4 from NightOverlay logic — checked via GameClock.DayProgress).
    /// </summary>
    public class CropEntity : MonoBehaviour
    {
        [SerializeField] private float growth = 0f;        // 0..1
        // #170 - wiki: rice 3.5 game days = ~ 14 real-min @ 1x.  prototype 가속:
        //   1.5 min (90s) - 너무 빠르지도 느리지도 않은 plays-per-session 적합.
        //   이전 40s 는 wiki 14배 빠름 - 식량 사이클 깨짐.
        [SerializeField] private float growthPerSecond = 0.011f;  // ~90s real to ripen
        // #224 QA fix — 장기 식량 고갈(800s LongPlay: meals 16→0, food 0 고착, Day11
        //  굶주림 직전).  정상상태 food 가 요리 임계(15) 밑이라 요리 중단→식사 고갈.
        //  수확량 8→14 로 지속 가능한 잉여 확보(농장 12타일 재생).  wiki rice 8 보다 높지만
        //  prototype 3림 생존 균형용.
        [SerializeField] private int   harvestFood = 14;

        // W-M1-02 Art: per-stage sprites (wired by SceneSetup.Game.Settlement via
        // SerializedObject).  When null, falls back to the pre-existing color-tint
        // so pre-wired scenes and unit tests do not regress.
        [SerializeField] private Sprite spriteSeedling;  // stage 0: growth < 0.33
        [SerializeField] private Sprite spriteGrowing;   // stage 1: 0.33..0.66
        [SerializeField] private Sprite spriteRipe;      // stage 2: >= 0.66

        // #272 런타임 파종 crop 용 기본 stage 스프라이트 (레코더/SceneSetup 이 실제
        //  crop_rice_* 에셋으로 세팅 → 절차적 모양 대신 진짜 스프라이트가 보인다).
        public static Sprite DefSeedling, DefGrowing, DefRipe;

        // Fallback colors used when stage sprites are not assigned (legacy path).
        // #272 zone(연두) 위에서도 또렷하도록 대비 강한 톤 (어두운 외곽선 sprite 와 함께).
        private static readonly Color SPROUT_COLOR = new Color(0.40f, 0.90f, 0.25f, 1f);  // 밝은 새싹
        private static readonly Color GROWN_COLOR  = new Color(0.20f, 0.65f, 0.12f, 1f);  // 진한 초록
        private static readonly Color RIPE_COLOR   = new Color(0.95f, 0.80f, 0.15f, 1f);  // 노란 이삭

        private SpriteRenderer sr;
        public bool IsRipe => growth >= 1f;

        // #save-load 완성(2026-06-04) — 작물 성장도(0..1)가 로드 시 0 으로 리셋되던 것 복원용.
        //  SaveLoadManager 가 Save 에서 Growth 를 읽고, ApplyLoadedSubStates 에서 SetGrowth 로
        //  씬 재생성된 farm 타일에 위치 매칭 재적용(beds/walls 와 동일 패턴).
        public float Growth => growth;
        public void SetGrowth(float g)
        {
            growth = Mathf.Clamp01(g);
            RefreshVisual();
        }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            // #272 런타임 grow-zone 파종 crop 이 stage 스프라이트가 없어 안 보이던 문제.
            //  실제 작물 스프라이트(crop_rice_*)를 static 기본값으로 받아 쓴다(레코더/
            //  SceneSetup 이 세팅).  없으면 절차적 폴백.
            if (sr != null)
            {
                if (sr.sprite == null)
                {
                    if (spriteSeedling == null && DefSeedling != null)
                    {
                        spriteSeedling = DefSeedling;
                        spriteGrowing  = DefGrowing != null ? DefGrowing : DefSeedling;
                        spriteRipe     = DefRipe    != null ? DefRipe    : DefSeedling;
                    }
                    sr.sprite = spriteSeedling != null ? spriteSeedling : ProcCropSprite;
                }
                // #272 항상 확실히 보이게 — zone 마커/바닥 위 + 한 칸보다 크게.
                sr.sortingOrder = 12;
                transform.localScale = Vector3.one * 1.5f;   // 한 칸 넘게 — 또렷이
            }
            RefreshVisual();
            // Add collider so right-click can target it
            if (GetComponent<Collider2D>() == null)
            {
                var c = gameObject.AddComponent<BoxCollider2D>();
                c.size = Vector2.one;
            }
        }

        // Day 79: 다른 component가 reflection 으로 growth 변경 후 visual 동기화 hook
        private void Start()
        {
            RefreshVisual();
        }

        private void Update()
        {
            if (IsRipe) return;
            // Daylight check via GameClock — between 06:00 and 20:00 grow.
            if (GameClock.Instance != null)
            {
                float t = GameClock.Instance.DayProgress;
                bool daylight = (t > 0.25f && t < 0.83f);
                if (!daylight) return;
            }
            // 발전 아크 — 관개 연구 시 성장 +40%.
            float gmul = (ResearchManager.Instance != null
                          && ResearchManager.Instance.IsUnlocked("irrigation")) ? 1.4f : 1f;
            growth = Mathf.Clamp01(growth + growthPerSecond * gmul * Time.deltaTime);
            RefreshVisual();
        }

        /// <summary>Returns food gained, or 0 if not ripe.</summary>
        public int Harvest()
        {
            if (!IsRipe) return 0;
            growth = 0f;
            RefreshVisual();
            // #215 운영자 fb "먹거리(자원) 순간이동" — 즉시 AddFood(텔레포트) 대신 물리 식량
            //  더미를 그 자리에 떨어뜨린다.  hauler 가 저장고로 운반해야 카운터에 적립되고,
            //  배고픈 림은 그 자리서 직접 집어먹는다(PawnNeeds 물리 섭취 경로 1).
            // #219 운영자 fb "작물 채집하면 고기?" — 작물은 고기가 아니라 농작물 식량 더미를
            //  떨어뜨린다.  익은 작물 sprite(spriteRipe) + 표시명 "농작물" 로 구분.
            MeatPileEntity.Spawn(transform.position, harvestFood,
                spriteRipe != null ? spriteRipe : MeatPileEntity.SharedSprite, "농작물", 600f);  // #223 쌀은 오래 감
            AudioBank.Instance?.PlayHarvest();  // Day 80
            // grader 좌표 (2026-06-12) — 수확 완료가 무로그라 격리 채점이 '수확 0'과
            //  '드롭 후 소비/부패'를 구분 못 했다.  관측성 1줄 (효과 어서션/채점용).
            Debug.Log($"[Harvest] 농작물 +{harvestFood} drop @ ({transform.position.x:F1},{transform.position.y:F1}) day={(GameClock.Instance != null ? GameClock.Instance.Day : -1)} hour={(GameClock.Instance != null ? GameClock.Instance.Hour : -1)}");
            return harvestFood;
        }

        // #272 절차적 새싹 스프라이트 (흰색 식물 모양 → color-tint 로 단계별 초록/노랑).
        private static Sprite _procCrop;
        private static Sprite ProcCropSprite
        {
            get
            {
                if (_procCrop != null) return _procCrop;
                const int S = 24;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                var px = new Color32[S * S];
                // 두툼한 3-잎 덤불(채워진 형태) + 어두운 외곽선 → 연두 zone 위에서도 또렷.
                //  흰색 내부는 color-tint 로 단계별 초록/노랑, 회색 외곽선은 더 진한 톤이 된다.
                var lobes = new (float cx, float cy, float r)[]
                {
                    (12f, 14f, 5.5f),   // 위 잎
                    (8f, 9f, 5.0f),     // 좌 잎
                    (16f, 9f, 5.0f),    // 우 잎
                    (12f, 8f, 5.0f),    // 중앙 아래
                };
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float maxDepth = -999f;   // union 안쪽으로 얼마나 깊은지(최대)
                        foreach (var L in lobes)
                        {
                            float d = Mathf.Sqrt((x + 0.5f - L.cx) * (x + 0.5f - L.cx) + (y + 0.5f - L.cy) * (y + 0.5f - L.cy));
                            maxDepth = Mathf.Max(maxDepth, L.r - d);   // 양수면 그 lobe 내부
                        }
                        if (maxDepth <= 0f) { px[y * S + x] = new Color32(0, 0, 0, 0); continue; }
                        bool edge = maxDepth < 1.3f;                    // union 가장자리
                        px[y * S + x] = edge ? new Color32(60, 90, 40, 255)    // 외곽선(진한 초록)
                                             : new Color32(255, 255, 255, 255); // 내부(틴트시 초록)
                    }
                tex.SetPixels32(px); tex.Apply(false);
                _procCrop = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
                _procCrop.name = "ProcCrop";
                return _procCrop;
            }
        }

        private void RefreshVisual()
        {
            if (sr == null) return;
            // W-M1-02: sprite-based stage switching.  When SceneSetup has wired
            // the three sprite refs, swap the sprite and reset tint to white so
            // the palette-correct art shows without color multiplication.
            // Falls back to color-tinting when sprites are not wired (editor /
            // unit-test scenes that predate this wave).
            // 첫사이클 T11 (2026-06-12) — 시각 익음(0.66)과 IsRipe(1.0) 불일치:
            //  황금색인데 ~30초간 수확 불가(우클릭 이동으로 새고 AI 도 skip).
            //  익은 모습은 정확히 IsRipe 일 때만 — 단계 경계 0.4/0.8 재배분.
            if (spriteSeedling != null && spriteGrowing != null && spriteRipe != null)
            {
                if (IsRipe)              sr.sprite = spriteRipe;
                else if (growth < 0.4f)  sr.sprite = spriteSeedling;
                else                     sr.sprite = spriteGrowing;
                sr.color = Color.white;
            }
            else
            {
                // Legacy color-tint path (no stage sprites wired).
                if (IsRipe)              sr.color = RIPE_COLOR;
                else if (growth < 0.4f)  sr.color = SPROUT_COLOR;
                else                     sr.color = GROWN_COLOR;
            }
        }
    }
}
