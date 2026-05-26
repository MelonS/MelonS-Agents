using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>On collision, if both fruits are same tier, destroys both
    /// and spawns the next-tier fruit at midpoint + awards score.</summary>
    [RequireComponent(typeof(Fruit))]
    public class FruitMerger : MonoBehaviour
    {
        [SerializeField] private GameObject[] allTierPrefabs;
        private Fruit me;

        private void Awake() { me = GetComponent<Fruit>(); }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (me == null || me.merged || me.justSpawned) return;
            var other = collision.gameObject.GetComponent<Fruit>();
            if (other == null || other.merged || other.justSpawned) return;
            if (other.tier != me.tier) return;
            if (GetInstanceID() > collision.gameObject.GetInstanceID()) return;

            int nextTier = me.tier + 1;
            me.merged = true;
            other.merged = true;

            Vector3 mid = (transform.position + collision.transform.position) * 0.5f;
            Destroy(collision.gameObject);
            Destroy(gameObject);

            if (allTierPrefabs != null && nextTier - 1 < allTierPrefabs.Length && nextTier - 1 >= 0)
            {
                GameObject prefab = allTierPrefabs[nextTier - 1];
                if (prefab != null) Instantiate(prefab, mid, Quaternion.identity);
            }

            int gain = (me.tier >= 0 && me.tier < Fruit.TIER_SCORES.Length) ? Fruit.TIER_SCORES[me.tier] : me.tier;
            if (ScoreManager.Instance != null) ScoreManager.Instance.AddScore(gain);
        }
    }
}
