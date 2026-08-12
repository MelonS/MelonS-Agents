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

        /// <summary>묘목이 설 자리 — 벤 자리가 비어 있으면 거기, 아니면 가장 가까운 빈 칸.
        ///
        /// 2026-08-01 운영자 "목재 캔곳에서 새로운 나무 나오는처리 이상함".
        ///  나무를 베면 **같은 칸에** 목재 더미가 떨어지는데 묘목도 같은 칸에 심겼다.
        ///  둘이 정확히 포개져서 목재 위로 나무가 자라는 것처럼 보였다.
        ///  숲에서도 그루터기 바로 위가 아니라 곁에서 새싹이 오르므로, 비켜 심는 것이
        ///  자연스럽기도 하다.</summary>
        private static Vector3 FreeSpotNear(Vector3 pos)
        {
            if (!Occupied(pos)) return pos;
            // 8방향을 가까운 순으로 — 대각선보다 상하좌우를 먼저 본다.
            var ring = new[]
            {
                new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
                new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f),
            };
            for (int i = 0; i < ring.Length; i++)
            {
                var cand = pos + new Vector3(ring[i].x, ring[i].y, 0f);
                if (!Occupied(cand) && !PawnMovement.IsBlockedAt(cand)) return cand;
            }
            return pos;   // 사방이 막혔으면 원래 자리 (사라지는 것보다 겹치는 게 낫다)
        }

        private static bool Occupied(Vector3 p)
        {
            // 반 칸 반경 — 같은 칸에 있는 것만 잡는다.  자원 더미·다른 묘목·나무.
            var hits = Physics2D.OverlapCircleAll(p, 0.45f);
            for (int i = 0; i < hits.Length; i++)
            {
                // 콜라이더가 자식에 붙는 경우가 있어 부모까지 훑는다.
                if (hits[i].GetComponentInParent<WoodPileEntity>() != null) return true;
                if (hits[i].GetComponentInParent<StoneChunkEntity>() != null) return true;
                if (hits[i].GetComponentInParent<TreeSapling>() != null) return true;
                if (hits[i].GetComponentInParent<TreeEntity>() != null) return true;
            }
            return false;
        }

        private void SpawnSapling(Vector3 pos)
        {
            pos = FreeSpotNear(pos);
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
