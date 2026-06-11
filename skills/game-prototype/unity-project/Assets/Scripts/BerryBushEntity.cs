using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 11: a gather-able berry bush.  Holds a finite stock of berries;
    /// each TakeBerry() call drains some.  Visual darkens as stock drops,
    /// goes grey when depleted (does NOT destroy — gather AI checks
    /// IsDepleted to know when to abandon).
    ///
    /// Based on templates/cs/spawned-entity.cs.tmpl (spawn grace pattern),
    /// kept for future spawn-grace use if bushes get respawned dynamically.
    /// Pre-spawned scene instances inherit the default Awake time, which is
    /// fine since gather has no merge/collision logic — lesson #8 doesn't
    /// bite us here.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BerryBushEntity : MonoBehaviour
    {
        [Header("Stock")]
        [SerializeField] private int initialBerries = 5;
        [SerializeField] private int berryPerCollect = 1;

        [Header("Pacing")]
        // collectInterval is the gatherer's responsibility (PawnGatherer)
        // but exposed here for designer reference / future use.
        [SerializeField] private float collectInterval = 0.8f;

        [Header("Regen (Day 12)")]
        [SerializeField] private float bushRegenSec = 60f;
        // 게임루프 백로그 #2 (2026-06-11): 60실초/재생 = 덤불 1개가 림 16명분 무한 공급
        //  — 식량 희소성 0 으로 농사·사냥·요리가 전부 불필요했다 (생존축 붕괴의 a).
        //  ~1.2게임일(1200실초@1x)로 희소화 → 덤불 1개 ≈ 림 0.7일치.  씬에 60 이
        //  직렬화돼 있어 코드 기본값 변경만으론 안 먹힌다(#QR지면 교훈) → Awake clamp.
        private const float MinRegenSec = 1200f;

        [Header("Spawn grace (template:spawned-entity)")]
        [SerializeField] private float spawnGraceSeconds = 0.2f;
        private float spawnTime;

        private int berries;
        private SpriteRenderer spriteRenderer;
        // #161 - sprite 의 origin tint 보존 (생성 시 색을 base 로 캐시).
        //  이전: grayscale 덮어쓰기 → 베리 익었을 때 녹색이 회색으로.
        private Color baseColor = Color.white;

        public bool IsDepleted => berries <= 0;
        public int BerriesRemaining => berries;
        public float CollectInterval => collectInterval;

        /// <summary>true for `spawnGraceSeconds` after Awake.</summary>
        public bool InGrace => Time.time - spawnTime < spawnGraceSeconds;

        private void Awake()
        {
            spawnTime = Time.time;
            if (bushRegenSec < MinRegenSec) bushRegenSec = MinRegenSec;   // 백로그 #2 — 직렬화 60 무력화
            berries = initialBerries;
            spriteRenderer = GetComponent<SpriteRenderer>();
            // #161 - sprite color 가 setup 단계에서 이미 박혀있음 (녹색).  base 로 캐시.
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
            UpdateVisual();
        }

        /// <summary>
        /// Try to gather from this bush.  Returns berries actually taken
        /// (0 if depleted).  Caller (PawnGatherer) feeds the returned count
        /// into ResourceManager.AddFood.
        /// </summary>
        public int TakeBerry()
        {
            if (IsDepleted) return 0;
            int take = Mathf.Min(berryPerCollect, berries);
            berries -= take;
            UpdateVisual();
            // Day 12: when the bush just transitioned to depleted, schedule
            // a regen.  Null-guard the singleton — lesson #7 says the
            // scheduler may not yet be bound on early frames.  If it isn't,
            // this bush will sit depleted permanently (acceptable degradation;
            // a scene-bound scheduler will always be ready before any pawn
            // can drain a bush, so this is only a worst-case fallback).
            if (IsDepleted && RegrowthScheduler.Instance != null)
            {
                RegrowthScheduler.Instance.EnqueueBushRegen(this, bushRegenSec);
            }
            return take;
        }

        /// <summary>Day 12: scheduler calls this to refill the bush.</summary>
        public void Restore()
        {
            berries = initialBerries;
            UpdateVisual();
        }

        // 아트 v2 후속 (2026-06-12) — 수확후 전용 스프라이트 (빨간 점 없는 덤불).
        //  기존 '어둡게 틴트'는 줌아웃에서 그냥 그늘진 덤불로 읽혀 '딸 수 있나?' 가
        //  화면에서 판별이 안 됐다.  에셋 없으면 기존 틴트 폴백.
        private static Sprite _fullSpr, _pickedSpr;
        private static bool _sprLoaded;

        private void UpdateVisual()
        {
            if (spriteRenderer == null) return;
            if (!_sprLoaded)
            {
                _sprLoaded = true;
                _fullSpr = Resources.Load<Sprite>("flora32/flora32_bush_berry");
                _pickedSpr = Resources.Load<Sprite>("flora32/flora32_bush_picked");
            }
            if (_fullSpr != null && _pickedSpr != null)
            {
                spriteRenderer.sprite = IsDepleted ? _pickedSpr : _fullSpr;
                // 잔량은 가벼운 밝기만 (full 1.0 → 마지막 알 0.75)
                float tt = IsDepleted ? 0.9f
                    : Mathf.Lerp(0.75f, 1f, (float)berries / Mathf.Max(1, initialBerries));
                TintHelper.ApplyBrightness(spriteRenderer, baseColor, tt);
                return;
            }
            // #161 - base 녹색을 brightness 곱으로 조절 (회색 덮어쓰기 X).
            float t = IsDepleted
                ? 0.35f
                : Mathf.Lerp(0.4f, 1f, (float)berries / Mathf.Max(1, initialBerries));
            TintHelper.ApplyBrightness(spriteRenderer, baseColor, t);
        }
    }
}
