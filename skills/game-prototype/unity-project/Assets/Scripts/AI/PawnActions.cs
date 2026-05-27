using UnityEngine;

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
        public float globalFoodThreshold = 10f;
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

    public class WanderAction : IPawnAction
    {
        public string DisplayName => "어슬렁";
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.movement == null) return false;
            Vector2 cur = ctx.transform.position;
            ctx.movement.SetTarget(cur + Random.insideUnitCircle * ctx.idleWanderRadius);
            return true;
        }
    }
}
