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

        private void OnCollisionEnter2D(Collision2D collision) => TryMerge(collision);
        private void OnCollisionStay2D(Collision2D collision) => TryMerge(collision);

        private void TryMerge(Collision2D collision)
        {
            if (me == null || me.merged || me.justSpawned) return;
            var other = collision.gameObject.GetComponent<Fruit>();
            if (other == null || other.merged || other.justSpawned) return;
            if (other.tier != me.tier) return;
            // Tiebreaker: only the GameObject with the smaller ID handles the
            // merge.  IMPORTANT: must compare gameObject IDs (NOT component IDs),
            // because components are created AFTER GameObjects so a component ID
            // is always larger than ANY GO ID, causing both sides to bail.
            if (gameObject.GetInstanceID() > collision.gameObject.GetInstanceID()) return;

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
            if (AudioBank.Instance != null) AudioBank.Instance.PlayMerge();

            // Floating "+N" popup
            var popup = new GameObject("ScorePopup");
            var sp = popup.AddComponent<ScorePopup>();
            sp.Setup(gain, mid);
        }
    }
}
