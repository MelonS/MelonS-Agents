using UnityEngine;
using MelonS.GameProto;

namespace MelonS.GameProto.AI
{
    /// <summary>
    /// R5 - 6 concrete IPawnAction implementations.
    /// Priority 순서 (PawnUtilityAI 가 이 순서로 시도):
    ///   1. EatBerryAction   (food < 40 자기 식량 부족)
    ///   2. HuntAnimalAction (global food < 10)
    ///   3. CookMealAction   (food > 5 + stove 존재)
    ///   4. ChopTreeAction   (default 노동)
    ///   5. WanderAction     (fallback)
    ///
    /// 각 action 책임:
    ///   - eligibility check (조건 만족하나)
    ///   - target 찾기 (FindNearestX)
    ///   - 외부 컴포넌트에 target 설정 (gatherer/hunter/cook/chopper/movement)
    ///   - return true = action 시작됨, false = 다음 action 시도
    /// </summary>

    public class EatBerryAction : IPawnAction
    {
        public string DisplayName => "베리채집";
        public WorkKind Kind => WorkKind.Gather;
        public float foodThreshold = 40f;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.needs == null || ctx.gatherer == null) return false;
            if (ctx.needs.food >= foodThreshold) return false;
            BerryBushEntity bush = FindNearestBush(ctx);
            if (bush == null) return false;
            ctx.gatherer.SetBushTarget(bush);
            return true;
        }
        private static BerryBushEntity FindNearestBush(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<BerryBushEntity>(FindObjectsSortMode.None);
            // 운영자: 림들 겹침 - 다른 gatherer 가 target 한 bush skip.
            var others = Object.FindObjectsByType<PawnGatherer>(FindObjectsSortMode.None);
            var claimed = new System.Collections.Generic.HashSet<BerryBushEntity>();
            foreach (var g in others)
            {
                if (g == null || g == ctx.gatherer) continue;
                if (g.Target != null) claimed.Add(g.Target);
            }
            BerryBushEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var b in arr)
            {
                if (b == null || b.IsDepleted) continue;
                if (claimed.Contains(b)) continue;
                Vector3 bp = b.transform.position;
                if (Mathf.Abs(bp.x) > 28.5f || Mathf.Abs(bp.y) > 28.5f) continue;
                float sq = ((Vector2)bp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            return best;
        }
    }

    public class HuntAnimalAction : IPawnAction
    {
        public string DisplayName => "사냥";
        public WorkKind Kind => WorkKind.Hunt;
        // 운영자 fb #6 - starter food=10 이라 즉시 hunt 발동했음.  5로 낮춤 = 정말 부족할 때만.
        public float globalFoodThreshold = 5f;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.hunter == null) return false;
            if (ResourceManager.Instance == null) return false;
            if (ResourceManager.Instance.food >= globalFoodThreshold) return false;
            AnimalEntity deer = FindNearestAnimal(ctx);
            if (deer == null) return false;
            ctx.hunter.SetAnimalTarget(deer);
            return true;
        }
        private static AnimalEntity FindNearestAnimal(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<AnimalEntity>(FindObjectsSortMode.None);
            // 운영자: 림들 겹침 - 다른 hunter 가 target 한 animal skip.
            var others = Object.FindObjectsByType<PawnHunter>(FindObjectsSortMode.None);
            var claimed = new System.Collections.Generic.HashSet<AnimalEntity>();
            foreach (var h in others)
            {
                if (h == null || h == ctx.hunter) continue;
                if (h.Target != null) claimed.Add(h.Target);
            }
            AnimalEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var a in arr)
            {
                if (a == null || a.IsDead) continue;
                if (claimed.Contains(a)) continue;
                Vector3 ap = a.transform.position;
                if (Mathf.Abs(ap.x) > 28.5f || Mathf.Abs(ap.y) > 28.5f) continue;
                float sq = ((Vector2)ap - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = a; }
            }
            return best;
        }
    }

    public class CookMealAction : IPawnAction
    {
        public string DisplayName => "요리";
        public WorkKind Kind => WorkKind.Cook;
        public float foodSurplus = 5f;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.cook == null) return false;
            if (ResourceManager.Instance == null) return false;
            if (ResourceManager.Instance.food <= foodSurplus) return false;
            StoveEntity stove = FindNearestStove(ctx);
            if (stove == null) return false;
            ctx.cook.SetStoveTarget(stove);
            return true;
        }
        private static StoveEntity FindNearestStove(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<StoveEntity>(FindObjectsSortMode.None);
            StoveEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var s in arr)
            {
                if (s == null) continue;
                float sq = ((Vector2)s.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = s; }
            }
            return best;
        }
    }

    public class ChopTreeAction : IPawnAction
    {
        public string DisplayName => "벌목";
        public WorkKind Kind => WorkKind.Chop;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.chopper == null) return false;
            TreeEntity tree = FindNearestTree(ctx);
            if (tree == null) return false;
            ctx.chopper.SetTreeTarget(tree);
            return true;
        }
        private static TreeEntity FindNearestTree(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None);
            // 운영자 피드백 "림들이 왜케 겹쳐서 이동" - 다른 pawn 이 이미 target 한 tree 는 skip.
            //  각 pawn 이 다른 tree pick 하도록 reservation 시스템.
            var otherChoppers = Object.FindObjectsByType<PawnChopper>(FindObjectsSortMode.None);
            System.Collections.Generic.HashSet<TreeEntity> claimed
                = new System.Collections.Generic.HashSet<TreeEntity>();
            foreach (var c in otherChoppers)
            {
                if (c == null || c == ctx.chopper) continue;
                if (c.Target != null) claimed.Add(c.Target);
            }
            TreeEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var t in arr)
            {
                if (t == null || t.IsDestroyed) continue;
                if (claimed.Contains(t)) continue;  // 이미 다른 pawn 의 target
                Vector3 tp = t.transform.position;
                if (Mathf.Abs(tp.x) > 28.5f || Mathf.Abs(tp.y) > 28.5f) continue;
                float sq = ((Vector2)tp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = t; }
            }
            return best;
        }
    }

    public class BuildBlueprintAction : IPawnAction
    {
        public string DisplayName => "건설";
        // 림 vanilla 에선 Construction 별 work type 이지만 1차로 Chop 슬롯에 묶음.
        public WorkKind Kind => WorkKind.Chop;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.builder == null) return false;
            BlueprintEntity bp = FindNearestBlueprint(ctx);
            if (bp == null) return false;
            ctx.builder.SetBlueprintTarget(bp);
            return true;
        }
        private static BlueprintEntity FindNearestBlueprint(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None);
            BlueprintEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var bp in arr)
            {
                if (bp == null || bp.IsComplete) continue;
                if (bp.IsReserved && bp.ReservedBy != ctx.builder.gameObject) continue;
                Vector3 bpp = bp.transform.position;
                if (Mathf.Abs(bpp.x) > 28.5f || Mathf.Abs(bpp.y) > 28.5f) continue;
                float sq = ((Vector2)bpp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = bp; }
            }
            return best;
        }
    }

    public class TendPatientAction : IPawnAction
    {
        public string DisplayName => "치료";
        public WorkKind Kind => WorkKind.Research;  // 1차로 Research 슬롯 재활용 (#126 에서 Medical 별도)
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.doctor == null) return false;
            PawnHealth patient = FindNearestDowned(ctx);
            if (patient == null) return false;
            ctx.doctor.SetPatientTarget(patient);
            return true;
        }
        private static PawnHealth FindNearestDowned(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<PawnHealth>(FindObjectsSortMode.None);
            PawnHealth best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var h in arr)
            {
                if (h == null || h.IsDead) continue;
                if (h.gameObject == ctx.transform.gameObject) continue;  // self skip
                // 의식불명 or 출혈 중 = 환자
                bool bleeding = false;
                if (h.parts != null)
                {
                    foreach (var p in h.parts)
                        if (p != null && p.bleedRate > 0.1f && !p.bandaged) { bleeding = true; break; }
                }
                if (!h.IsDowned && !bleeding) continue;
                Vector3 hp = h.transform.position;
                if (Mathf.Abs(hp.x) > 28.5f || Mathf.Abs(hp.y) > 28.5f) continue;
                float sq = ((Vector2)hp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = h; }
            }
            return best;
        }
    }

    public class MineStoneAction : IPawnAction
    {
        public string DisplayName => "채광";
        public WorkKind Kind => WorkKind.Chop;  // 1차로 Chop 슬롯 (#120 에서 Mining 별도 가능)
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.miner == null) return false;
            StoneVeinEntity vein = FindNearestVein(ctx);
            if (vein == null) return false;
            ctx.miner.SetVeinTarget(vein);
            return true;
        }
        private static StoneVeinEntity FindNearestVein(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<StoneVeinEntity>(FindObjectsSortMode.None);
            // 다른 miner 가 이미 채광 중인 vein skip
            var others = Object.FindObjectsByType<PawnMiner>(FindObjectsSortMode.None);
            var claimed = new System.Collections.Generic.HashSet<StoneVeinEntity>();
            foreach (var m in others)
            {
                if (m == null || m == ctx.miner) continue;
                if (m.Target != null) claimed.Add(m.Target);
            }
            StoneVeinEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var v in arr)
            {
                if (v == null || v.IsDestroyed) continue;
                if (claimed.Contains(v)) continue;
                Vector3 vp = v.transform.position;
                if (Mathf.Abs(vp.x) > 28.5f || Mathf.Abs(vp.y) > 28.5f) continue;
                float sq = ((Vector2)vp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = v; }
            }
            return best;
        }
    }

    public class HaulMeatAction : IPawnAction
    {
        public string DisplayName => "고기 운반";
        public WorkKind Kind => WorkKind.Chop;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.hauler == null) return false;
            MeatPileEntity meat = FindNearestMeat(ctx);
            if (meat == null) return false;
            ctx.hauler.SetMeatTarget(meat);
            return true;
        }
        private static MeatPileEntity FindNearestMeat(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<MeatPileEntity>(FindObjectsSortMode.None);
            MeatPileEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var m in arr)
            {
                if (m == null) continue;
                if (m.IsReserved && m.ReservedBy != ctx.hauler.gameObject) continue;
                if (m.InStockpile) continue;  // 림 - stockpile 안 meat 재운반 X
                Vector3 mp = m.transform.position;
                if (Mathf.Abs(mp.x) > 28.5f || Mathf.Abs(mp.y) > 28.5f) continue;
                float sq = ((Vector2)mp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = m; }
            }
            return best;
        }
    }

    public class HaulStoneAction : IPawnAction
    {
        public string DisplayName => "돌 운반";
        public WorkKind Kind => WorkKind.Chop;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.hauler == null) return false;
            StoneChunkEntity chunk = FindNearestChunk(ctx);
            if (chunk == null) return false;
            ctx.hauler.SetStoneTarget(chunk);
            return true;
        }
        private static StoneChunkEntity FindNearestChunk(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<StoneChunkEntity>(FindObjectsSortMode.None);
            StoneChunkEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var c in arr)
            {
                if (c == null) continue;
                if (c.IsReserved && c.ReservedBy != ctx.hauler.gameObject) continue;
                if (c.InStockpile) continue;  // 림 - stockpile 안 chunk 재운반 X
                Vector3 cp = c.transform.position;
                if (Mathf.Abs(cp.x) > 28.5f || Mathf.Abs(cp.y) > 28.5f) continue;
                float sq = ((Vector2)cp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = c; }
            }
            return best;
        }
    }

    public class HaulWoodAction : IPawnAction
    {
        public string DisplayName => "운반";
        // 운반도 림 vanilla 에선 Hauling 별 work type 이지만 1차로 Chop 슬롯에 묶음.
        public WorkKind Kind => WorkKind.Chop;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.hauler == null) return false;
            WoodPileEntity pile = FindNearestPile(ctx);
            if (pile == null) return false;
            ctx.hauler.SetPileTarget(pile);
            return true;
        }
        private static WoodPileEntity FindNearestPile(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<WoodPileEntity>(FindObjectsSortMode.None);
            WoodPileEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var p in arr)
            {
                if (p == null) continue;
                // 다른 hauler 가 이미 reserve 했으면 skip
                if (p.IsReserved && p.ReservedBy != ctx.hauler.gameObject) continue;
                if (p.InStockpile) continue;  // 림 - stockpile 안 pile 재운반 X
                Vector3 pp = p.transform.position;
                if (Mathf.Abs(pp.x) > 28.5f || Mathf.Abs(pp.y) > 28.5f) continue;
                float sq = ((Vector2)pp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p; }
            }
            return best;
        }
    }

    public class WanderAction : IPawnAction
    {
        public string DisplayName => "어슬렁";
        // 운영자 fb #114 - wander 는 fallback 이므로 Chop priority 따라감 (실제 work 아님).
        public WorkKind Kind => WorkKind.Chop;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.movement == null) return false;
            Vector2 cur = ctx.transform.position;
            ctx.movement.SetTarget(cur + Random.insideUnitCircle * ctx.idleWanderRadius);
            return true;
        }
    }
}
