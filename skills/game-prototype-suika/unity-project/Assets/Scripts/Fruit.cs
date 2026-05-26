using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Suika fruit physics body.  Tier defines size/value.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Fruit : MonoBehaviour
    {
        public int tier = 1;
        public bool merged = false;
        public bool justSpawned = true;
        private float spawnGrace = 0.2f;
        private float spawnTimer = 0f;

        public static int[] TIER_SCORES = new int[] { 0, 1, 3, 6, 10, 15, 21, 28 };

        private void Update()
        {
            if (justSpawned)
            {
                spawnTimer += Time.deltaTime;
                if (spawnTimer >= spawnGrace) justSpawned = false;
            }
        }
    }
}
