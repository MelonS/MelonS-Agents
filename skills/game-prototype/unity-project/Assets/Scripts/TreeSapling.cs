using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 12: a young tree placeholder.  Spawned by RegrowthScheduler some
    /// time after a TreeEntity is chopped.  After `growSeconds` elapse it
    /// destroys itself and instantiates a full TreeEntity at the same
    /// position — i.e. replace-prefab style growth.
    ///
    /// Lesson #8 firewall (spawned-entity grace):
    ///   `spawnTime` is recorded in Awake via Time.time.  Promotion check
    ///   uses `Time.time - spawnTime >= growSeconds`, never a bool default-
    ///   true flag (which would fire on frame 1 against any code that
    ///   does the merge-then-check race).  This matches BerryBushEntity's
    ///   InGrace pattern.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class TreeSapling : MonoBehaviour
    {
        [SerializeField] private float growSeconds = 60f;
        private GameObject treePrefab;

        private float spawnTime;
        private bool promoted;

        /// <summary>seconds since this sapling was Awake'd.</summary>
        public float Age => Time.time - spawnTime;
        public float GrowSeconds => growSeconds;

        private void Awake()
        {
            spawnTime = Time.time;
        }

        /// <summary>
        /// Called by RegrowthScheduler immediately after AddComponent.
        /// Carries the prefab reference forward so the sapling can promote.
        /// </summary>
        public void Configure(float growSec, GameObject prefab)
        {
            growSeconds = growSec;
            treePrefab = prefab;
        }

        private void Update()
        {
            if (promoted) return;
            if (Age >= growSeconds)
            {
                Promote();
            }
        }

        private void Promote()
        {
            promoted = true;
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;

            if (treePrefab != null)
            {
                Instantiate(treePrefab, pos, rot);
            }
            else
            {
                // Fallback: build a minimal TreeEntity from the sapling's
                // own sprite scaled back up.  Less pretty but functional —
                // keeps the regen chain from breaking if no prefab was wired.
                var tree = new GameObject("Tree");
                tree.transform.position = pos;
                tree.transform.rotation = rot;
                tree.transform.localScale = Vector3.one;
                var sr = tree.AddComponent<SpriteRenderer>();
                var mySr = GetComponent<SpriteRenderer>();
                if (mySr != null)
                {
                    sr.sprite = mySr.sprite;
                    sr.sortingOrder = mySr.sortingOrder;
                    sr.color = Color.white;
                }
                tree.AddComponent<TreeEntity>();
            }

            Destroy(gameObject);
        }
    }
}
