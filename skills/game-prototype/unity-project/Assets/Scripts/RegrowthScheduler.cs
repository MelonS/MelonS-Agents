using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 12: Time.time-based queue for delayed regen events.
    /// - Bushes that go depleted re-enqueue themselves to Restore() after N seconds.
    /// - Trees that get chopped enqueue a sapling spawn at the same position.
    ///
    /// Singleton via static Instance, bound on Awake (same pattern as
    /// ResourceManager / AudioBank).
    ///
    /// Lesson #7 firewall:
    ///   Callers MUST poll RegrowthScheduler.Instance from their own Update()
    ///   (or null-guard `Instance?.Enqueue...`) — NEVER subscribe in OnEnable,
    ///   because GameObject Awake-order between SceneSetup and entities is
    ///   not guaranteed and a subscribed event misses the bind.
    /// </summary>
    public class RegrowthScheduler : MonoBehaviour
    {
        public static RegrowthScheduler Instance { get; private set; }

        // Designer-visible defaults; SceneSetup may override before use, but
        // the public API takes explicit delays so these are only fallbacks.
        [Header("Sapling growth")]
        [SerializeField] private float saplingToTreeSec = 60f;
        [SerializeField] private float saplingScale = 0.4f;

        public float SaplingToTreeSec => saplingToTreeSec;
        public float SaplingScale => saplingScale;

        private struct BushPending
        {
            public BerryBushEntity bush;
            public float dueTime;
        }

        private struct SaplingPending
        {
            public Vector3 position;
            public float dueTime;
        }

        // Reference to a tree prefab/template for sapling-to-tree conversion.
        // Set by SceneSetup at world-gen.  Sapling uses this same reference
        // when promoting.  Can also be null — in that case TreeSapling falls
        // back to building a minimal Tree GameObject from scratch using the
        // sapling's own sprite scaled back up.
        [SerializeField] private GameObject treePrefab;
        public GameObject TreePrefab => treePrefab;
        public void SetTreePrefab(GameObject prefab) { treePrefab = prefab; }

        private readonly List<BushPending> bushQueue = new();
        private readonly List<SaplingPending> saplingQueue = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Schedule `bush.Restore()` after `seconds`.</summary>
        public void EnqueueBushRegen(BerryBushEntity bush, float seconds)
        {
            if (bush == null) return;
            bushQueue.Add(new BushPending
            {
                bush = bush,
                dueTime = Time.time + seconds
            });
        }

        /// <summary>Schedule a TreeSapling spawn at `pos` after `delay` seconds.</summary>
        public void EnqueueSapling(Vector3 pos, float delay)
        {
            saplingQueue.Add(new SaplingPending
            {
                position = pos,
                dueTime = Time.time + delay
            });
        }

        private void Update()
        {
            float now = Time.time;

            // Bushes — restore in place
            for (int i = bushQueue.Count - 1; i >= 0; i--)
            {
                var p = bushQueue[i];
                if (p.bush == null)
                {
                    // Bush was destroyed externally — drop the entry
                    bushQueue.RemoveAt(i);
                    continue;
                }
                if (now >= p.dueTime)
                {
                    p.bush.Restore();
                    bushQueue.RemoveAt(i);
                }
            }

            // Saplings — spawn a TreeSapling GameObject
            for (int i = saplingQueue.Count - 1; i >= 0; i--)
            {
                var p = saplingQueue[i];
                if (now >= p.dueTime)
                {
                    SpawnSapling(p.position);
                    saplingQueue.RemoveAt(i);
                }
            }
        }

        private void SpawnSapling(Vector3 pos)
        {
            var go = new GameObject("TreeSapling");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * saplingScale;
            var sr = go.AddComponent<SpriteRenderer>();
            // 전용 묘목 스프라이트 (2026-08-01).  이전에는 **나무 프리팹의 스프라이트를
            //  빌려** 작게 줄이고 초록으로 물들였는데, 그 프리팹이 아트 v2 이전 세대의
            //  동그란 덩어리라 축소하면 정체불명의 초록 얼룩이 됐다(운영자 "이건먼데").
            //  묘목은 '작은 나무' 로 보여야 심긴 것임을 알 수 있다.
            var sap = Resources.Load<Sprite>("flora32/flora32_sapling");
            if (sap == null) sap = UnityEngine.Resources.Load<Sprite>("flora32_sapling");
            if (sap != null)
            {
                sr.sprite = sap;
                sr.sortingOrder = 5;
                sr.color = Color.white;          // 스프라이트가 이미 어린 색이다
            }
            else if (treePrefab != null)
            {
                var protoSr = treePrefab.GetComponentInChildren<SpriteRenderer>();
                if (protoSr != null)
                {
                    sr.sprite = protoSr.sprite;
                    sr.sortingOrder = protoSr.sortingOrder;
                    sr.color = new Color(0.55f, 0.95f, 0.55f, 1f);
                }
            }

            var sapling = go.AddComponent<TreeSapling>();
            sapling.Configure(saplingToTreeSec, treePrefab);
        }
    }
}
