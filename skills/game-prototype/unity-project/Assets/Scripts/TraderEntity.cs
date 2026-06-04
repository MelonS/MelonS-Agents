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

        // #133 - 거래 메뉴 6 옵션 (give → receive 식).  TryTrade(idx) 로 선택.
        public static readonly (string label, int giveWood, int giveStone, int giveFood,
                                int rWood, int rStone, int rFood, int rMeals)[] TradeOptions = {
            ("목재 5 → 식량 8",       5, 0, 0,  0, 0, 8, 0),
            ("목재 10 → 식사 2",      10, 0, 0, 0, 0, 0, 2),
            ("석재 5 → 목재 8",       0, 5, 0,  8, 0, 0, 0),
            ("석재 10 → 식량 12",     0, 10, 0, 0, 0, 12, 0),
            ("식량 15 → 석재 8",      0, 0, 15, 0, 8, 0, 0),
            ("식량 20 → 목재 12",     0, 0, 20, 12, 0, 0, 0),
        };

        /// <summary>legacy 단일 거래 (목재 5 → 식량 8 = idx 0).</summary>
        public bool TryTrade() => TryTrade(0);

        // #168 - 거래는 wiki: 콜로니스트가 trader 옆에 있을 때만 가능 (proximity check).
        //  이전: 맵 끝에서도 거래 됐음.  지금: 5 unit 안에 살아있는 pawn 있어야 함.
        public const float TradeRangeUnits = 5f;

        /// <summary>idx 의 거래 옵션 실행. true 반환 = 성공.</summary>
        public bool TryTrade(int idx)
        {
            var rm = Services.Get<ResourceManager>();
            if (rm == null) return false;
            if (idx < 0 || idx >= TradeOptions.Length) return false;
            // #168 - proximity 검사 (살아있는 pawn 중 가장 가까운 것).
            if (!HasPawnNearby(out PawnEntity nearestPawn, out float nearestDist))
            {
                Debug.Log($"[Trader] 콜로니스트가 {TradeRangeUnits:F0}u 안에 없음 (가장 가까운={(nearestPawn!=null?nearestPawn.PawnName:"?")} dist={nearestDist:F1})");
                return false;
            }
            // #178 - socialMul 가격 보정 (이전: SET-only).
            //  가장 가까운 pawn 의 socialMul (0.80~1.20) 를 receive 측에 곱.
            //  높으면 더 많이 받음, 낮으면 적게 받음.  give 는 그대로.
            float socMul = 1f;
            if (nearestPawn != null)
            {
                var abil = nearestPawn.GetComponent<PawnAbilities>();
                if (abil != null) socMul = abil.socialMul;
            }
            var (label, gW, gS, gF, rW, rS, rF, rM) = TradeOptions[idx];
            if (rm.wood < gW || rm.stone < gS || rm.food < gF)
            {
                Debug.Log($"[Trader] 자원 부족 - {label}");
                return false;
            }
            // #자원모델 단일화(2026-06-04): 판매(give)는 카운터 + 물리 InStockpile 더미 함께 소비.
            if (gW > 0) rm.SpendStockpiledWood(gW);
            if (gS > 0) rm.SpendStockpiledStone(gS);
            if (gF > 0) rm.SpendStockpiledFood(gF);
            int rWa = Mathf.Max(0, Mathf.RoundToInt(rW * socMul));
            int rSa = Mathf.Max(0, Mathf.RoundToInt(rS * socMul));
            int rFa = Mathf.Max(0, Mathf.RoundToInt(rF * socMul));
            int rMa = Mathf.Max(0, Mathf.RoundToInt(rM * socMul));
            // #자원모델 단일화(2026-06-04): 구매(receive)는 카운터(+) 대신 trader 위치에 물리 더미로
            //  드롭(RimWorld: 거래 물품이 거래 지점에 떨어져 림이 운반).  살짝 오프셋해 더미 겹침 완화.
            //  조리식(meals)은 물리 entity 가 없어 추상 카운터 유지.
            Vector3 at = transform.position;
            if (rWa > 0) WoodPileEntity.Spawn(at + new Vector3(0.3f, 0f, 0f), rWa, PawnHauler.WoodPileSpriteRef);
            if (rSa > 0) StoneChunkEntity.Spawn(at + new Vector3(-0.3f, 0f, 0f), rSa, PawnHauler.StoneChunkSpriteRef);
            if (rFa > 0) MeatPileEntity.Spawn(at + new Vector3(0f, 0.3f, 0f), rFa, PawnHauler.MeatPileSpriteRef);
            if (rMa > 0) rm.AddMeals(rMa);
            Debug.Log($"[Trader] 거래 성공: {label} (social {socMul:F2}x → {rWa}/{rSa}/{rFa}/{rMa})");
            return true;
        }

        // #168 - 살아있는 가장 가까운 pawn 이 TradeRangeUnits 안인가.
        private bool HasPawnNearby(out PawnEntity nearest, out float dist)
        {
            nearest = null; dist = float.MaxValue;
            var pawns = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            Vector3 myPos = transform.position;
            foreach (var p in pawns)
            {
                if (p == null) continue;
                if (p.IsDead) continue;
                float d = Vector3.Distance(p.transform.position, myPos);
                if (d < dist) { dist = d; nearest = p; }
            }
            return nearest != null && dist <= TradeRangeUnits;
        }
    }
}
