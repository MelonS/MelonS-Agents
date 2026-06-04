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

    /// <summary>
    /// 자율 취침 — 운영자 fb "림이 그 자리에서 자고 침대를 안 씀" fix.
    /// 졸리고(autoSleepThreshold 미만) 밤이면 가장 가까운 빈 BedEntity 를 예약하고
    /// 그 침대 위(인접 stand cell)로 이동시킨다.  발밑에 침대 도착하면 PawnNeeds 가
    /// IsSleeping=true (task readout "수면", bed.RestMul 회복) 처리.
    ///
    /// 생존 행동이라 PawnUtilityAI 가 작업 priority loop 보다 먼저 시도 (work settings
    /// 무관하게 항상 동작).  빈 침대가 없거나 도달 불가면 false 반환 → 기존 제자리
    /// 취침(PawnNeeds 의 sleep<30 && night) fallback.
    ///
    /// rcfix forcedResting(우클릭 침대) 와 정합: needs.HasRestOrder(사용자 명령) 가
    /// 있으면 자율 취침은 시작하지 않는다 (사용자 명령 우선).
    /// </summary>
    public class GoSleepAction : IPawnAction
    {
        public string DisplayName => "수면";
        // work settings 와 무관하게 PawnUtilityAI 의 생존 pre-pass 에서만 호출되므로
        //  Kind 는 priority 매핑에 쓰이지 않는다.  형식상 Gather 로 둔다.
        public WorkKind Kind => WorkKind.Gather;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.needs == null || ctx.movement == null) return false;
            // 졸리고 밤일 때만.  사용자 우클릭 휴식 명령 중이면 양보.
            if (!ctx.needs.WantsAutoSleep) return false;
            if (ctx.needs.HasRestOrder) return false;

            BedEntity bed = ctx.FindNearestFreeBed();
            if (bed == null) return false;  // 침대 없음 → 제자리 취침 fallback
            // 중앙 예약 — 같은 침대로 두 림이 몰리지 않게.
            if (!ReservationManager.TryReserve(bed, ctx.transform.gameObject)) return false;

            ctx.needs.SetAutoSleepTarget(bed);
            // 수면은 침대 "위" 에서 일어난다 (PawnNeeds.GetBedUnderPawn 이 발밑 OverlapBox).
            //  ── REGRESSION FIX ──
            //  기존엔 TryGetWorkStandPos (침대 footprint *인접* stand cell) 로 이동시켰다.
            //  그러면 림이 침대 옆에 서고, GetBedUnderPawn 은 발밑(=옆 cell)에서 침대를
            //  못 찾아 onTargetBed 영영 false → "휴식이동" stuck, sleep 0 crash.
            //  채광/벌목은 옆에 서서 일하지만, 취침은 레퍼런스 콜로니심처럼 침대 cell 위로 올라가야 한다.
            //  → 우클릭 rcfix(SetRestTarget) 와 동일하게 침대 cell 자체를 target.
            //    1x2 침대는 두 cell 중 림에게 가까운 cell 로 (door/벽 때문에 한쪽만 닿을 수 있음).
            ctx.movement.SetTarget(BedStandPos(bed, ctx.transform.position));
            return true;
        }

        /// <summary>침대 footprint 의 cell 중 from 에 가장 가까운 cell 의 world 중심.
        ///  림이 그 위로 올라가 GetBedUnderPawn 이 침대를 인식하도록.</summary>
        internal static Vector2 BedStandPos(BedEntity bed, Vector2 from)
        {
            var covered = new System.Collections.Generic.HashSet<Vector2Int>();
            PathGrid.CoveredCells(bed.transform.position, bed.Size, covered);
            Vector2 best = bed.transform.position;
            float bestSq = float.MaxValue;
            bool any = false;
            foreach (var c in covered)
            {
                Vector2 w = PathGrid.CellToWorld(c);
                float sq = (w - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = w; any = true; }
            }
            return any ? best : (Vector2)bed.transform.position;
        }
    }

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
            // #199 C2 — reserve the chosen bush (central registry).
            if (!ReservationManager.TryReserve(bush, ctx.transform.gameObject)) return false;
            ctx.gatherer.SetBushTarget(bush);
            return true;
        }
        private static BerryBushEntity FindNearestBush(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<BerryBushEntity>(FindObjectsSortMode.None);
            // #199 C2 — central reservation: skip bushes reserved by ANOTHER pawn.
            var claimant = ctx.transform.gameObject;
            BerryBushEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var b in arr)
            {
                if (b == null || b.IsDepleted) continue;
                if (ReservationManager.IsReservedByOther(b, claimant)) continue;
                Vector3 bp = b.transform.position;
                if (Mathf.Abs(bp.x) > 43.5f || Mathf.Abs(bp.y) > 43.5f) continue;
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
            // #242 건설 굶김 ROOT FIX — 이전엔 raw food 만 검사해서, bare start(food=0,
            //  meals=50)에서 pawn 이 50끼를 두고도 영구 사냥 → BuildBlueprint(같은 p1, 리스트
            //  뒤)에 영영 안 닿아 "건설 안됨"(WINDIAG 28/28 '사냥').  meals 도 식량이므로 합산:
            //  먹을 게(raw+meals) 충분하면 사냥 안 함 → idle pawn 이 건설/운반으로 감.
            if (ResourceManager.Instance.food + ResourceManager.Instance.meals >= globalFoodThreshold) return false;
            AnimalEntity deer = FindNearestAnimal(ctx);
            if (deer == null) return false;
            // #199 C2 — reserve the chosen animal (central registry).  Hunter has no
            //  fixed stand cell (animal moves), but the TARGET reservation still
            //  stops two hunters chasing one deer.
            if (!ReservationManager.TryReserve(deer, ctx.transform.gameObject)) return false;
            ctx.hunter.SetAnimalTarget(deer);
            return true;
        }
        private static AnimalEntity FindNearestAnimal(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<AnimalEntity>(FindObjectsSortMode.None);
            // #199 C2 — central reservation: skip animals reserved by ANOTHER pawn.
            var claimant = ctx.transform.gameObject;
            AnimalEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var a in arr)
            {
                if (a == null || a.IsDead) continue;
                // #버그헌트2(2026-06-04): 길들인 동물은 자동 사냥 제외.  이전엔 IsTamed 필터가 없어
                //  식량 부족 시 콜로니스트가 자기 가축을 죽여 고기로 만들었다(도축은 별도 지정으로만).
                if (a.IsTamed) continue;
                if (ReservationManager.IsReservedByOther(a, claimant)) continue;
                Vector3 ap = a.transform.position;
                if (Mathf.Abs(ap.x) > 43.5f || Mathf.Abs(ap.y) > 43.5f) continue;
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
            // #199 C2 — reserve the stove (only one cook per stove, the reference sim).
            if (!ReservationManager.TryReserve(stove, ctx.transform.gameObject)) return false;
            ctx.cook.SetStoveTarget(stove);
            return true;
        }
        private static StoveEntity FindNearestStove(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<StoveEntity>(FindObjectsSortMode.None);
            var claimant = ctx.transform.gameObject;
            StoveEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var s in arr)
            {
                if (s == null) continue;
                if (ReservationManager.IsReservedByOther(s, claimant)) continue;  // #199 C2
                float sq = ((Vector2)s.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = s; }
            }
            return best;
        }
    }

    /// <summary>
    /// #202 SURVIVAL-LOOP FIX — harvest the nearest RIPE crop into the global food
    /// stockpile.  This was the missing utility-AI action: crops ripened but idle
    /// pawns never harvested them (manual right-click only), so global food stayed
    /// stuck → no cook ingredients → starvation.  High value: ripe-crop harvest is
    /// sustenance work, so it sits with the other gather work (WorkKind.Gather) and
    /// is registered ABOVE generic chop/haul labor in the priority list.
    /// </summary>
    public class HarvestCropAction : IPawnAction
    {
        public string DisplayName => "수확";
        public WorkKind Kind => WorkKind.Gather;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.harvester == null) return false;
            CropEntity crop = FindNearestRipeCrop(ctx);
            if (crop == null) return false;
            // #199 C2 — reserve the chosen crop so two pawns don't both walk to it.
            //  FindNearestRipeCrop already skips crops reserved by OTHERS; this guard
            //  covers a same-frame race (two pawns deciding the same tick).
            if (!ReservationManager.TryReserve(crop, ctx.transform.gameObject)) return false;
            ctx.harvester.SetCropTarget(crop);
            return true;
        }
        private static CropEntity FindNearestRipeCrop(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<CropEntity>(FindObjectsSortMode.None);
            var claimant = ctx.transform.gameObject;
            CropEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var c in arr)
            {
                if (c == null || !c.IsRipe) continue;  // only RIPE crops are harvestable
                if (ReservationManager.IsReservedByOther(c, claimant)) continue;
                Vector3 cp = c.transform.position;
                if (Mathf.Abs(cp.x) > 43.5f || Mathf.Abs(cp.y) > 43.5f) continue;
                float sq = ((Vector2)cp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = c; }
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
            // #199 C2 — reserve the chosen tree (the reference sim).  FindNearestTree already
            //  skipped trees reserved by OTHERS, so this should succeed; the guard
            //  covers a same-frame race (two pawns deciding the same tick).  On
            //  failure, yield this tick — the AI retries next decision interval and
            //  picks a different tree.
            var claimant = ctx.transform.gameObject;
            if (!ReservationManager.TryReserve(tree, claimant)) return false;
            ctx.chopper.SetTreeTarget(tree);
            return true;
        }
        private static TreeEntity FindNearestTree(PawnContext ctx)
        {
            // #작업배정-단일화(운영자 승인 2026-06-03) — RimWorld 모델: 림은 '지정된(마킹된)'
            //  나무만 자율 벌목한다.  이전엔 지정과 무관하게 맵의 아무 나무나 골라(=#38 "다른
            //  림이 벌목", "다같이 감"), 게다가 TreeChopDesignation 의 별도 dispatch 와 충돌했다.
            //  이제 이 자율 경로가 유일한 배정자이고, 지정된 나무로만 한정한다.  지정 시스템이
            //  없으면(테스트 부트스트랩 누락 등) 자율 벌목을 하지 않는다(안전 측).
            var design = TreeChopDesignation.Instance;
            if (design == null) return null;
            var arr = Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None);
            // #199 C2 — central reservation registry: skip trees reserved by ANOTHER
            //  pawn so each idle pawn picks a DIFFERENT marked tree.
            var claimant = ctx.transform.gameObject;
            TreeEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var t in arr)
            {
                if (t == null || t.IsDestroyed) continue;
                if (!design.IsMarked(t)) continue;   // #단일화 — 지정된 나무만 자율 벌목
                if (ReservationManager.IsReservedByOther(t, claimant)) continue;  // 다른 pawn 의 target
                Vector3 tp = t.transform.position;
                if (Mathf.Abs(tp.x) > 43.5f || Mathf.Abs(tp.y) > 43.5f) continue;
                float sq = ((Vector2)tp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = t; }
            }
            return best;
        }
    }

    public class BuildBlueprintAction : IPawnAction
    {
        public string DisplayName => "건설";
        // #작업종류확장: RimWorld 처럼 건축(Construction)을 별도 work type 으로.
        public WorkKind Kind => WorkKind.Build;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.builder == null) return false;
            BlueprintEntity bp = FindNearestBlueprint(ctx);
            if (bp == null) return false;
            // #199 C2 — reserve via central registry (Builder also keeps ReservedBy
            //  in sync for legacy reads).  Skip on a same-frame race.
            if (!ReservationManager.TryReserve(bp, ctx.transform.gameObject)) return false;
            ctx.builder.SetBlueprintTarget(bp);
            return true;
        }
        private static BlueprintEntity FindNearestBlueprint(PawnContext ctx)
        {
            // #197 운영자 fb "두 번째 벽 건축 안 됨" root cause:
            //  이전 코드: builder 가 자재 부족 blueprint 도 reserve → 그 자리에 멍하니 서있음.
            //  hauler 가 자재 deposit 못 함 (실제로는 deposit 가능하지만 builder 가 ReserveBy 잡음).
            //  fix: HasAllMaterials=true 인 blueprint 만 builder 후보.  자재 부족은 hauler 가 자재 운반 책임.
            var arr = Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None);
            BlueprintEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var bp in arr)
            {
                if (bp == null || bp.IsComplete) continue;
                if (!bp.HasAllMaterials) continue;  // #197 - 자재 완비 안 됐으면 skip
                // #audit2 #0 — 중앙 레지스트리로 예약 판정.  이전엔 레거시 BlueprintEntity
                //  .ReservedBy 만 봤는데, 예약한 builder 가 죽어도 ReservedBy 는 정리되지 않아
                //  (ReservationManager 는 죽은 owner 를 free 처리) 청사진이 영구히 "예약됨"으로
                //  남아 아무도 못 짓는 desync 가 났다.  다른 모든 AI action 과 동일하게
                //  IsReservedByOther(죽은 owner 자동 해제 포함)로 통일.
                if (ReservationManager.IsReservedByOther(bp, ctx.builder.gameObject)) continue;
                Vector3 bpp = bp.transform.position;
                if (Mathf.Abs(bpp.x) > 43.5f || Mathf.Abs(bpp.y) > 43.5f) continue;
                float sq = ((Vector2)bpp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = bp; }
            }
            return best;
        }
    }

    public class TendPatientAction : IPawnAction
    {
        public string DisplayName => "치료";
        public WorkKind Kind => WorkKind.Doctor;  // #작업종류확장: 의료(Medical) 별도 work type
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.doctor == null) return false;
            PawnHealth patient = FindNearestDowned(ctx);
            if (patient == null) return false;
            // #199 C2 — reserve the patient so two doctors don't tend the same one.
            if (!ReservationManager.TryReserve(patient, ctx.transform.gameObject)) return false;
            ctx.doctor.SetPatientTarget(patient);
            return true;
        }
        private static PawnHealth FindNearestDowned(PawnContext ctx)
        {
            var arr = Object.FindObjectsByType<PawnHealth>(FindObjectsSortMode.None);
            var claimant = ctx.transform.gameObject;
            PawnHealth best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var h in arr)
            {
                if (h == null || h.IsDead) continue;
                if (h.gameObject == ctx.transform.gameObject) continue;  // self skip
                if (ReservationManager.IsReservedByOther(h, claimant)) continue;  // #199 C2
                // 의식불명 or 출혈 중 = 환자
                bool bleeding = false;
                if (h.parts != null)
                {
                    foreach (var p in h.parts)
                        if (p != null && p.bleedRate > 0.1f && !p.bandaged) { bleeding = true; break; }
                }
                if (!h.IsDowned && !bleeding) continue;
                Vector3 hp = h.transform.position;
                if (Mathf.Abs(hp.x) > 43.5f || Mathf.Abs(hp.y) > 43.5f) continue;
                float sq = ((Vector2)hp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = h; }
            }
            return best;
        }
    }

    public class MineStoneAction : IPawnAction
    {
        public string DisplayName => "채광";
        public WorkKind Kind => WorkKind.Mine;  // #작업종류확장: 채광(Mining) 별도 work type
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.miner == null) return false;
            StoneVeinEntity vein = FindNearestVein(ctx);
            if (vein == null) return false;
            // #199 C2 — reserve the chosen vein (central registry).
            if (!ReservationManager.TryReserve(vein, ctx.transform.gameObject)) return false;
            ctx.miner.SetVeinTarget(vein);
            return true;
        }
        private static StoneVeinEntity FindNearestVein(PawnContext ctx)
        {
            // #작업배정-단일화 — 벌목과 동일: 림은 '지정된(마킹된)' 광맥만 자율 채광한다.
            //  지정 시스템 없으면 자율 채광 안 함.
            var design = MineDesignation.Instance;
            if (design == null) return null;
            var arr = Object.FindObjectsByType<StoneVeinEntity>(FindObjectsSortMode.None);
            // #199 C2 — central reservation: skip veins reserved by ANOTHER pawn.
            var claimant = ctx.transform.gameObject;
            StoneVeinEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var v in arr)
            {
                if (v == null || v.IsDestroyed) continue;
                if (!design.IsMarked(v)) continue;   // #단일화 — 지정된 광맥만 자율 채광
                if (ReservationManager.IsReservedByOther(v, claimant)) continue;
                if (ctx.miner != null && ctx.miner.IsRecentlyGivenUp(v)) continue;  // #221 도달불가 광맥 쿨다운 스킵
                Vector3 vp = v.transform.position;
                if (Mathf.Abs(vp.x) > 43.5f || Mathf.Abs(vp.y) > 43.5f) continue;
                float sq = ((Vector2)vp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = v; }
            }
            return best;
        }
    }

    public class HaulMeatAction : IPawnAction
    {
        public string DisplayName => "고기 운반";
        public WorkKind Kind => WorkKind.Haul;  // #작업종류확장: 운반(Hauling) 별도
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.hauler == null) return false;
            // 운영자 2026-06-02: 받을 곳(식량 stockpile)이 없으면 운반을 시작하지 않는다.
            //  없으면 줍→둘곳없음→발밑드롭→재줍 무한루프(저장구역 없을 때 관찰됨).
            if (StockpileZoneEntity.FindBest(ctx.transform.position, StockItemKind.Food) == null)
                return false;
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
                if (Mathf.Abs(mp.x) > 43.5f || Mathf.Abs(mp.y) > 43.5f) continue;
                float sq = ((Vector2)mp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = m; }
            }
            return best;
        }
    }

    public class HaulStoneAction : IPawnAction
    {
        public string DisplayName => "돌 운반";
        public WorkKind Kind => WorkKind.Haul;  // #작업종류확장: 운반(Hauling) 별도
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.hauler == null) return false;
            // 운영자 2026-06-02: 받을 곳(석재 stockpile 또는 석재 필요한 청사진) 없으면 운반 안 함
            //  — 줍→발밑드롭→재줍 무한루프 방지.
            if (StockpileZoneEntity.FindBest(ctx.transform.position, StockItemKind.Stone) == null
                && !AnyBlueprintNeedsStone())
                return false;
            StoneChunkEntity chunk = FindNearestChunk(ctx);
            if (chunk == null) return false;
            ctx.hauler.SetStoneTarget(chunk);
            return true;
        }
        private static bool AnyBlueprintNeedsStone()
        {
            foreach (var bp in Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
                if (bp != null && bp.RemainingStone > 0) return true;
            return false;
        }
        private static StoneChunkEntity FindNearestChunk(PawnContext ctx)
        {
            // #196 - stone 도 같은 패턴.  blueprint 가 석재 필요 시 stockpile chunk 도 pickup 허용.
            bool anyBpNeedsStone = AnyBlueprintNeedsStone();
            var arr = Object.FindObjectsByType<StoneChunkEntity>(FindObjectsSortMode.None);
            StoneChunkEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var c in arr)
            {
                if (c == null) continue;
                if (c.IsReserved && c.ReservedBy != ctx.hauler.gameObject) continue;
                if (c.InStockpile && !anyBpNeedsStone) continue;
                Vector3 cp = c.transform.position;
                if (Mathf.Abs(cp.x) > 43.5f || Mathf.Abs(cp.y) > 43.5f) continue;
                float sq = ((Vector2)cp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = c; }
            }
            return best;
        }
    }

    public class HaulWoodAction : IPawnAction
    {
        public string DisplayName => "운반";
        // #작업종류확장: 운반(Hauling)을 별도 work type 으로 (이전엔 Chop 슬롯에 묶임).
        public WorkKind Kind => WorkKind.Haul;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.hauler == null) return false;
            // 운영자 2026-06-02: 받을 곳(목재 stockpile 또는 목재 필요한 청사진) 없으면 운반 안 함
            //  — 저장구역 없을 때 줍→발밑드롭→재줍 무한루프(림이 통나무 들었다 놨다)를 막는다.
            if (StockpileZoneEntity.FindBest(ctx.transform.position, StockItemKind.Wood) == null
                && !AnyBlueprintNeedsWood())
                return false;
            WoodPileEntity pile = FindNearestPile(ctx);
            if (pile == null) return false;
            ctx.hauler.SetPileTarget(pile);
            return true;
        }
        private static bool AnyBlueprintNeedsWood()
        {
            foreach (var bp in Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
                if (bp != null && bp.RemainingWood > 0) return true;
            return false;
        }
        private static WoodPileEntity FindNearestPile(PawnContext ctx)
        {
            // #196 - 운영자 fb "건축 실제 안 됨" 핵심 원인:
            //  이전 코드는 InStockpile=true 인 wood pile 을 무조건 skip.
            //  결과: stockpile 에 wood 쌓여있어도 청사진으로 운반 X → 건축 무한 대기.
            //  fix: 청사진이 자재 필요하면 stockpile pile 도 pickup target.
            //       청사진 없으면만 skip (stockpile → stockpile 재운반 loop 방지).
            bool anyBpNeedsWood = AnyBlueprintNeedsWood();
            var arr = Object.FindObjectsByType<WoodPileEntity>(FindObjectsSortMode.None);
            WoodPileEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var p in arr)
            {
                if (p == null) continue;
                // 다른 hauler 가 이미 reserve 했으면 skip
                if (p.IsReserved && p.ReservedBy != ctx.hauler.gameObject) continue;
                // #196 - InStockpile pile 은 청사진 자재 필요 시에만 pickup (stockpile→stockpile loop 차단).
                if (p.InStockpile && !anyBpNeedsWood) continue;
                Vector3 pp = p.transform.position;
                if (Mathf.Abs(pp.x) > 43.5f || Mathf.Abs(pp.y) > 43.5f) continue;
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
