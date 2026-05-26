using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Trader caravan — AIDirector 의 trader_caravan event 발화 시 spawn.
    /// 맵 가장자리에서 일정 시간 머무름 (60초).  우클릭 시 trade popup
    /// (현재는 단순 — wood 5 → food 8 단일 거래만, silver 추가는 향후).
    /// </summary>
    public class TraderEntity : MonoBehaviour
    {
        [SerializeField] private float lifetimeSec = 60f;
        [SerializeField] private float wanderRadius = 3f;
        [SerializeField] private float wanderSpeed = 0.5f;

        private float spawnTime;
        private Vector3 wanderTarget;
        private float nextWanderPick = -1f;

        public bool IsHere => Time.time - spawnTime < lifetimeSec;

        private void Awake()
        {
            spawnTime = Time.time;
            PickNewWanderTarget();
        }

        private void Update()
        {
            if (!IsHere) { Destroy(gameObject); return; }
            if (Time.time > nextWanderPick) PickNewWanderTarget();
            Vector3 d = wanderTarget - transform.position;
            if (d.magnitude < 0.1f) { PickNewWanderTarget(); return; }
            Vector3 step = d.normalized * wanderSpeed * Time.deltaTime;
            Vector3 newPos = transform.position + step;
            Vector2 clamped = PawnMovement.ClampToWorld(newPos);
            transform.position = new Vector3(clamped.x, clamped.y, newPos.z);
        }

        private void PickNewWanderTarget()
        {
            float r = Random.Range(1f, wanderRadius);
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector2 raw = (Vector2)transform.position + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            Vector2 clamped = PawnMovement.ClampToWorld(raw);
            wanderTarget = new Vector3(clamped.x, clamped.y, 0f);
            nextWanderPick = Time.time + Random.Range(2f, 5f);
        }

        /// <summary>우클릭 시 호출 - 간단 거래 (wood 5 → food 8).</summary>
        public bool TryTrade()
        {
            var rm = Services.Get<ResourceManager>();
            if (rm == null) return false;
            if (rm.wood < 5) { Debug.Log("[Trader] 목재 5 필요"); return false; }
            rm.AddWood(-5);
            rm.AddFood(8);
            Debug.Log("[Trader] 거래 성공 - 목재 5 → 식량 8");
            return true;
        }
    }
}
