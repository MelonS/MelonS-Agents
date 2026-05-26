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
                ResourceManager.Instance?.AddWood(woodDrop);
                Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
