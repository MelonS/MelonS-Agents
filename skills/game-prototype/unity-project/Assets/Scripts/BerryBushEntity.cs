using UnityEngine;

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

        [Header("Spawn grace (template:spawned-entity)")]
        [SerializeField] private float spawnGraceSeconds = 0.2f;
        private float spawnTime;

        private int berries;
        private SpriteRenderer spriteRenderer;

        public bool IsDepleted => berries <= 0;
        public int BerriesRemaining => berries;
        public float CollectInterval => collectInterval;

        /// <summary>true for `spawnGraceSeconds` after Awake.</summary>
        public bool InGrace => Time.time - spawnTime < spawnGraceSeconds;

        private void Awake()
        {
            spawnTime = Time.time;
            berries = initialBerries;
            spriteRenderer = GetComponent<SpriteRenderer>();
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

        private void UpdateVisual()
        {
            if (spriteRenderer == null) return;
            if (IsDepleted)
            {
                // Grey when stripped bare
                spriteRenderer.color = new Color(0.4f, 0.4f, 0.4f, 1f);
                return;
            }
            // Darken proportionally as stock drains: full = 1.0, empty = 0.4
            float t = (float)berries / Mathf.Max(1, initialBerries);
            float shade = Mathf.Lerp(0.4f, 1f, t);
            spriteRenderer.color = new Color(shade, shade, shade, 1f);
        }
    }
}
