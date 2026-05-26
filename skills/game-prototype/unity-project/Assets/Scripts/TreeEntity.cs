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
            // Day 6 — play chop SFX
            AudioBank.Instance?.PlayChop();
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
