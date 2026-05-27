using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// A choppable tree.  Day 3 = HP + on-chop damage + destroy + wood drop.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class TreeEntity : MonoBehaviour
    {
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private int woodDrop = 5;

        // 운영자 fb #116 - 벌목 시 즉시 inventory 추가가 아닌 WoodPile entity drop.
        //  SceneSetup 이 sprite 로딩 후 여기에 박음.
        public static Sprite WoodPileSprite;

        [Header("Regen (Day 12)")]
        [SerializeField] private float saplingDelaySec = 120f;

        private float hp;
        private SpriteRenderer spriteRenderer;

        public bool IsDestroyed => hp <= 0f;

        private void Awake()
        {
            hp = maxHp;
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Throttle chop SFX — without this it fires every frame (60x/sec
        // = white-noise buzz instead of rhythmic chop).
        private float lastChopSoundTime = -10f;
        private const float ChopSoundInterval = 0.45f;

        /// <summary>Apply chop damage. Returns true if tree was destroyed this hit.</summary>
        public bool TakeChopDamage(float dmg)
        {
            if (IsDestroyed) return false;
            hp -= dmg;
            // Visual feedback — darken as HP drops
            if (spriteRenderer != null)
            {
                float t = Mathf.Clamp01(hp / maxHp);
                spriteRenderer.color = new Color(t, t, t, 1f);
            }
            // Chop SFX — throttled so it sounds rhythmic, not buzzy
            if (Time.time - lastChopSoundTime >= ChopSoundInterval)
            {
                AudioBank.Instance?.PlayChop();
                lastChopSoundTime = Time.time;
            }
            if (hp <= 0f)
            {
                // 운영자 fb #116 - 즉시 inventory 추가가 아닌 wood pile drop.
                //  hauler 가 줍어야 inventory 차감 (림 vanilla).
                //  fallback: sprite 없으면 (legacy build / SceneSetup 못 박은 경우) 즉시 추가.
                if (WoodPileSprite != null)
                {
                    WoodPileEntity.Spawn(transform.position, woodDrop, WoodPileSprite);
                }
                else
                {
                    ResourceManager.Instance?.AddWood(woodDrop);
                }
                // Day 12: enqueue a future sapling at this tree's position
                // BEFORE Destroy(gameObject) — `transform.position` is read
                // synchronously, the scheduler stashes a Vector3, so once
                // Destroy fires the queued entry stands alone.  Null-guard
                // the singleton per lesson #7 (poll, never subscribe).
                RegrowthScheduler.Instance?.EnqueueSapling(transform.position, saplingDelaySec);
                Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
