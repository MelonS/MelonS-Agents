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
        [SerializeField] private int   harvestFood = 8;           // wiki rice yield 8 per plant

        // W-M1-02 Art: per-stage sprites (wired by SceneSetup.Game.Settlement via
        // SerializedObject).  When null, falls back to the pre-existing color-tint
        // so pre-wired scenes and unit tests do not regress.
        [SerializeField] private Sprite spriteSeedling;  // stage 0: growth < 0.33
        [SerializeField] private Sprite spriteGrowing;   // stage 1: 0.33..0.66
        [SerializeField] private Sprite spriteRipe;      // stage 2: >= 0.66

        // Fallback colors used when stage sprites are not assigned (legacy path).
        private static readonly Color SPROUT_COLOR = new Color(0.51f, 0.78f, 0.31f, 1f);
        private static readonly Color GROWN_COLOR  = new Color(0.32f, 0.62f, 0.20f, 1f);
        private static readonly Color RIPE_COLOR   = new Color(0.85f, 0.75f, 0.20f, 1f);

        private SpriteRenderer sr;
        public bool IsRipe => growth >= 1f;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
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
            growth = Mathf.Clamp01(growth + growthPerSecond * Time.deltaTime);
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
            MeatPileEntity.Spawn(transform.position, harvestFood, MeatPileEntity.SharedSprite);
            AudioBank.Instance?.PlayHarvest();  // Day 80
            return harvestFood;
        }

        private void RefreshVisual()
        {
            if (sr == null) return;
            // W-M1-02: sprite-based stage switching.  When SceneSetup has wired
            // the three sprite refs, swap the sprite and reset tint to white so
            // the palette-correct art shows without color multiplication.
            // Falls back to color-tinting when sprites are not wired (editor /
            // unit-test scenes that predate this wave).
            if (spriteSeedling != null && spriteGrowing != null && spriteRipe != null)
            {
                if (growth < 0.33f)
                {
                    sr.sprite = spriteSeedling;
                }
                else if (growth < 0.66f)
                {
                    sr.sprite = spriteGrowing;
                }
                else
                {
                    sr.sprite = spriteRipe;
                }
                sr.color = Color.white;
            }
            else
            {
                // Legacy color-tint path (no stage sprites wired).
                if (growth < 0.33f)      sr.color = SPROUT_COLOR;
                else if (growth < 0.66f) sr.color = GROWN_COLOR;
                else                     sr.color = RIPE_COLOR;
            }
        }
    }
}
