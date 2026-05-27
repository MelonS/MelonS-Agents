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
                // 운영자 fb 두 번째 (목재 안 나옴 fix2): chop 완료 시 즉시 inventory += N
                //  + pile drop (시각 효과만).  pile 줍으면 +0 (이미 받음).
                //  운영자가 wood 숫자 즉시 보게 = TopBar flash + 만족.
                ResourceManager.Instance?.AddWood(woodDrop);
                if (WoodPileSprite != null)
                {
                    var pile = WoodPileEntity.Spawn(transform.position, 0, WoodPileSprite);
                    if (pile != null) pile.SetWood(0);  // pickup 시 +0
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
