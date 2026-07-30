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
            if (bed == null)
            {
                // #QA플레이 F3 — 빈 침대 없음 힌트: 스케줄 밤잠 림이 바닥에서 자게.
                // r8 관찰 — 개막(주간 Sleep 슬롯) 스팸 46건: 로그를 MarkNoBedTonight
                //  안(밤 게이트 통과 시)으로 이동.  실제 밤의 침대 부족만 기록된다.
                ctx.needs.MarkNoBedTonight(ctx.entity?.PawnName);
                return false;  // 침대 없음 → 제자리 취침 fallback
            }
            // 중앙 예약 — 같은 침대로 두 림이 몰리지 않게.
            if (!ReservationManager.TryReserve(bed, ctx.transform.gameObject))
            {
                Debug.Log($"[GoSleep] {ctx.entity?.PawnName}: 예약 실패 bed@{bed.transform.position}");
                return false;
            }
            ctx.needs.ClearNoBedHint();   // F3 — 침대 확보 = 바닥취침 스트릭 리셋
            Debug.Log($"[GoSleep] {ctx.entity?.PawnName}: 침대로 출발 @{bed.transform.position}");

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

    /// <summary>갭 TOP-10 환자 행동 (2026-06-12, ※갈림길 아님) — 출혈 중인 림은
    /// 벌목을 계속하는 대신 스스로 침대로 가 치료를 기다린다 (우클릭 휴식과 동일
    /// 경로 SetRestTarget → 라벨 '휴식이동'/'휴식').  침대에 누우면 정지하므로
    /// '의사가 동속 표적을 추격하는 들판 치료'도 자연 해소된다.  의식불명(IsDowned)
    /// 은 이동 불가라 제외(기존 TendPatient 가 현장 처치).</summary>
    public class RestWhenBleedingAction : IPawnAction
    {
        public string DisplayName => "치료 대기";
        public WorkKind Kind => WorkKind.Gather;   // 생존 pre-pass 전용 — 매핑 미사용
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.needs == null || ctx.movement == null) return false;
            if (ctx.needs.HasRestOrder) return false;
            var h = ctx.transform.GetComponent<PawnHealth>();
            if (h == null || h.IsDead || h.IsDowned || h.parts == null) return false;
            bool bleeding = false;
            foreach (var part in h.parts)
                if (part != null && part.bleedRate > 0.1f && !part.bandaged) { bleeding = true; break; }
            if (!bleeding) return false;
            BedEntity bed = ctx.FindNearestFreeBed();
            if (bed == null) return false;
            if (!ReservationManager.TryReserve(bed, ctx.transform.gameObject)) return false;
            ctx.needs.SetRestTarget(bed);
            ctx.movement.SetTarget(GoSleepAction.BedStandPos(bed, ctx.transform.position));
            Debug.Log($"[Patient] {ctx.entity?.PawnName} 출혈 → 침대 치료 대기 @ ({bed.transform.position.x:F1},{bed.transform.position.y:F1})");
            return true;
        }
    }

    /// <summary>G-스케줄 최소형 (2026-06-13) — Joy(여가) 슬롯: 모닥불가로 모여 쉰다.
    /// '조용한 저녁' 카드의 모닥불이 처음으로 행동이 된다.  PawnThoughts 가
    /// 모닥불 2.5셀 내 체류에 '모닥불 곁' +2 를 환류.</summary>
    public class JoyAction : IPawnAction
    {
        public string DisplayName => "휴식(여가)";
        public WorkKind Kind => WorkKind.Gather;   // fallback 직전 전용 — 매핑 미사용
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.movement == null) return false;
            var sched = ctx.transform.GetComponent<PawnSchedule>();
            if (sched == null || sched.GetCurrentSlot() != TimeSlot.Joy) return false;
            var fire = FindCampfire();
            if (fire == null) return false;
            Vector2 c = fire.transform.position;
            if (((Vector2)ctx.transform.position - c).sqrMagnitude < 6.25f) return true;  // 이미 곁 — 머문다
            var off = Random.insideUnitCircle.normalized * Random.Range(1.0f, 2.0f);
            ctx.movement.SetTarget(PawnMovement.ClampToWorld(c + off));
            return true;
        }

        public static GameObject FindCampfire()
        {
            foreach (var fl in Object.FindObjectsByType<FlickerLight>(FindObjectsSortMode.None))
                if (fl != null && fl.name.Contains("Campfire")) return fl.gameObject;
            return null;
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
        /// <summary>다른 폰이 예약하지 않은 가장 가까운 성숙 수풀.  GatherBerryAction 이
        ///  같은 탐색을 재사용한다 — 두 벌로 갈라지면 예약/월드경계 규칙이 조용히 어긋난다.</summary>
        internal static BerryBushEntity FindNearestFreeBush(PawnContext ctx) => FindNearestBush(ctx);

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
            // #버그헌트(2026-06-05) CRITICAL: 물리 식량(바닥/저장고 MeatPile)도 식량으로 합산한다.
            //  자원모델 단일화(haul-required)로 ResourceManager.food 는 '저장(InStockpile)된' 식량만
            //  세는데, 초반·평소엔 저장량이 0 이라 이 gate(food+meals<5)가 사실상 영구 true → 모든
            //  pawn 이 매 Decide 마다 사냥(Hunt)을 먼저 잡아(우선순위 상위) 그 아래 운반/건축/요리/수확이
            //  영영 굶었다.  게다가 사냥 대상이 멀거나 없으면 hunt task 가 곧 풀려 pawn 은 떠돌기만 함
            //  (운영자 '저장공간·운반물 있는데 떠돈다'의 진짜 원인).  물리 MeatPile.Food 합까지 더해
            //  '실제 먹을 게 충분하면' 사냥하지 않게 한다(starter 물리 고기·바닥 식량 포함).
            int physFood = 0;
            foreach (var m in Object.FindObjectsByType<MeatPileEntity>(FindObjectsSortMode.None))
                if (m != null) physFood += m.Food;
            if (ResourceManager.Instance.food + ResourceManager.Instance.meals + physFood >= globalFoodThreshold)
                return false;
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

    /// <summary>소크 r2 관찰 #1 (2026-06-12) — 지붕은 이제 빌더 노동으로 시공된다.
    /// 청사진 건설보다 후순위(액션 리스트 등록 위치가 우선순위): 구조물 골조가 먼저,
    /// 지붕은 그 다음 — 레퍼런스의 시공 순서 감각과 일치.</summary>
    public class BuildRoofAction : IPawnAction
    {
        public string DisplayName => "지붕 시공";
        public WorkKind Kind => WorkKind.Build;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.builder == null) return false;
            var rd = RoofDesignation.Instance;
            if (rd == null) return false;
            if (!rd.TryClaimNearestPending(ctx.transform.position, ctx.transform.gameObject, out var cell))
                return false;
            ctx.builder.SetRoofTarget(cell);
            return true;
        }
    }

    public class CookMealAction : IPawnAction
    {
        public string DisplayName => "요리";
        public WorkKind Kind => WorkKind.Cook;
        public float foodSurplus = 5f;
        // 소크 r2 채점(2026-06-12) — 화덕이 raw food 전량을 meals 로 전환해 8게임일에
        //  1,547인분 적체 + food=0 고착.  레퍼런스의 '수량 X까지 제작' 빌과 같은 상한:
        //  1인당 5끼 비축이면 충분(여유분 포함), 그 이상은 다른 일을 하러 간다.
        public const int MealsPerColonistCap = 5;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.cook == null) return false;
            if (ResourceManager.Instance == null) return false;
            if (ResourceManager.Instance.food <= foodSurplus) return false;
            int colonists = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None).Length;
            if (ResourceManager.Instance.meals + ResourceManager.Instance.fineMeals
                >= colonists * MealsPerColonistCap) return false;
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
        /// <summary>이 폰이 지금 맡을 수 있는(지정됐고 아무도 예약하지 않은) 대상.
        ///  GatherBerryAction 이 "플레이어 지시가 아직 남았는가"를 판단할 때
        ///  **같은 탐색**을 재사용한다 — 규칙이 두 벌로 갈라지면 조용히 어긋난다.</summary>
        internal static TreeEntity FindFreeMarkedTree(PawnContext ctx) => FindNearestTree(ctx);

        private static TreeEntity FindNearestTree(PawnContext ctx)
        {
            // #작업배정-단일화(운영자 승인 2026-06-03) — the reference sim 모델: 림은 '지정된(마킹된)'
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
        // #작업종류확장: the reference sim 처럼 건축(Construction)을 별도 work type 으로.
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
        /// <summary>이 폰이 지금 맡을 수 있는(지정됐고 아무도 예약하지 않은) 대상.
        ///  GatherBerryAction 이 "플레이어 지시가 아직 남았는가"를 판단할 때
        ///  **같은 탐색**을 재사용한다 — 규칙이 두 벌로 갈라지면 조용히 어긋난다.</summary>
        internal static BlueprintEntity FindFreeBlueprint(PawnContext ctx) => FindNearestBlueprint(ctx);

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
                // TOP-10 — 침대 대기(휴식 명령/취침) 환자 우선: 정지 표적부터 치료.
                var pn = h.GetComponent<PawnNeeds>();
                bool stationary = h.IsDowned || (pn != null && (pn.HasRestOrder || pn.IsSleeping));
                if (!stationary) sq += 1_000_000f;   // 이동 중 환자는 사실상 후순위
                if (sq < bestSq) { bestSq = sq; best = h; }
            }
            return best;
        }
    }

    /// <summary>GDD G3 — 시신 매장.  빈 무덤 + 미예약 시신이 있으면 수습→운반→안장.
    /// Haul 카테고리 최상단 (시체 > 일반 더미 — design-social §G3).</summary>
    public class BuryCorpseAction : IPawnAction
    {
        public string DisplayName => "매장";
        public WorkKind Kind => WorkKind.Haul;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.burier == null) return false;
            var claimant = ctx.transform.gameObject;
            // 시신 탐색 — 죽은 PawnEntity (자기 자신 제외, 타인 예약 제외)
            PawnEntity corpse = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                if (p == null || !p.IsDead) continue;
                if (!p.gameObject.activeSelf) continue;              // 이미 운구 중
                if (p.gameObject == claimant) continue;
                if (ReservationManager.IsReservedByOther(p, claimant)) continue;
                float sq = ((Vector2)p.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; corpse = p; }
            }
            if (corpse == null) return false;
            var grave = GraveEntity.FindNearestEmpty(corpse.transform.position, claimant);
            if (grave == null) return false;
            return ctx.burier.SetTask(corpse, grave);
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
        /// <summary>이 폰이 지금 맡을 수 있는(지정됐고 아무도 예약하지 않은) 대상.
        ///  GatherBerryAction 이 "플레이어 지시가 아직 남았는가"를 판단할 때
        ///  **같은 탐색**을 재사용한다 — 규칙이 두 벌로 갈라지면 조용히 어긋난다.</summary>
        internal static StoneVeinEntity FindFreeMarkedVein(PawnContext ctx) => FindNearestVein(ctx);

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
        /// <summary>이 폰이 지금 나를 수 있는(예약되지 않은) 바닥 더미.
        ///  GatherBerryAction 이 "운반이 먼저다" 원칙을 밴드 너머로 강제할 때 재사용한다.</summary>
        internal static MeatPileEntity FindFreeMeatPile(PawnContext ctx) => FindNearestMeat(ctx);

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
                if (m.InStockpile
                    && !StockpileZoneEntity.UpgradeHaulWanted(m.transform.position, StockItemKind.Food))
                    continue;  // 저장 meat 재운반은 상향 이주(TOP-3)일 때만
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
        /// <summary>이 폰이 지금 나를 수 있는(예약되지 않은) 바닥 더미.
        ///  GatherBerryAction 이 "운반이 먼저다" 원칙을 밴드 너머로 강제할 때 재사용한다.</summary>
        internal static StoneChunkEntity FindFreeStoneChunk(PawnContext ctx) => FindNearestChunk(ctx);

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
                if (c.InStockpile && !anyBpNeedsStone
                    && !StockpileZoneEntity.UpgradeHaulWanted(c.transform.position, StockItemKind.Stone))
                    continue;  // 저장 chunk: 청사진 수요 또는 상향 이주(TOP-3)만
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
        /// <summary>이 폰이 지금 나를 수 있는(예약되지 않은) 바닥 더미.
        ///  GatherBerryAction 이 "운반이 먼저다" 원칙을 밴드 너머로 강제할 때 재사용한다.</summary>
        internal static WoodPileEntity FindFreeWoodPile(PawnContext ctx) => FindNearestPile(ctx);

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
                if (p.InStockpile && !anyBpNeedsWood
                    && !StockpileZoneEntity.UpgradeHaulWanted(p.transform.position, StockItemKind.Wood))
                    continue;  // 저장 pile: 청사진 수요 또는 상향 이주(TOP-3)만
                Vector3 pp = p.transform.position;
                if (Mathf.Abs(pp.x) > 43.5f || Mathf.Abs(pp.y) > 43.5f) continue;
                float sq = ((Vector2)pp - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p; }
            }
            return best;
        }
    }

    /// <summary>
    /// 연구 디스패치 (2026-06-12) — 직업 탭 '연구' 컬럼이 우선순위 저장/로드까지 되면서
    /// WorkKind.Research 소비처가 0 이던 거짓 UI 교정.  번영 소크 실증: 연구대를 지어도
    /// 림이 옆에 머물 이유가 없어 8게임일에 2pt (사실상 정체).
    ///
    /// 연구대는 ResearcherSpeedSum(radius 1.5)이 반경 내 폰을 자동 감지하므로 워커
    /// 컴포넌트 없이 '벤치 옆에 서 있기'가 곧 연구다: 반경 밖이면 벤치 앞 칸으로 이동,
    /// 반경 안이면 true 만 반환(머무름 = 작업).  PawnUtilityAI 리스트 최하단 등록 —
    /// 같은 우선순위에선 다른 일감이 전부 소진된 뒤에만 잡고, 직업 탭에서 연구 1/
    /// 나머지 2+ 로 두면 전담 연구자가 된다.
    /// </summary>
    /// <summary>
    /// 작업대에서 무기를 만든다 (2026-07-30 운영자 "습격을 처리하려면 무기가 있어야함").
    ///
    /// 이전에는 무기 획득 경로가 **연구 `simple_bow` 완료 시 자동 지급** 하나뿐이었다.
    /// 즉 플레이어가 무기를 만드는 행위가 게임에 없었다.
    ///
    /// `WorkKind.Build`(만들기)로 묶는다 — 9직종 그리드는 이 게임의 차별점 화면이라
    /// 열을 늘리면 그 화면이 바뀐다.  건설과 제작은 같은 손일이므로 무리한 배정도 아니다.
    /// 우선순위는 **청사진 건설보다 아래** 에 둔다: 집이 없는데 무기를 먼저 만들면 곤란하고,
    /// 습격은 유예 2일 + 간격 지터로 4~6일차에 오므로 급하지 않다.
    /// </summary>
    public class CraftWeaponAction : IPawnAction
    {
        public string DisplayName => "무기 제작";
        public WorkKind Kind => WorkKind.Build;
        public bool TryStart(PawnContext ctx)
        {
            if (ctx.smith == null) return false;
            // 만들 사람이 필요하고(무장 안 된 콜로니스트) 자재가 있어야 한다.
            if (PawnWeaponsmith.FindUnarmed() == null) return false;
            if (!PawnWeaponsmith.TryPickRecipe(out _)) return false;
            ResearchBench best = null; float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var b in Object.FindObjectsByType<ResearchBench>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                if (ReservationManager.IsReservedByOther(b, ctx.transform.gameObject)) continue;
                float sq = ((Vector2)b.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            if (best == null) return false;
            ctx.smith.SetBenchTarget(best);
            return true;
        }
    }

    public class DoResearchAction : IPawnAction
    {
        public string DisplayName => "연구";
        public WorkKind Kind => WorkKind.Research;
        public bool TryStart(PawnContext ctx)
        {
            var rm = ResearchManager.Instance;
            if (rm == null || rm.activeTech == null || rm.activeTech.completed) return false;
            var benches = Object.FindObjectsByType<ResearchBench>(FindObjectsSortMode.None);
            ResearchBench best = null;
            float bestSq = float.MaxValue;
            Vector2 me = ctx.transform.position;
            foreach (var b in benches)
            {
                if (b == null) continue;
                float sq = ((Vector2)b.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            if (best == null) return false;
            // 연구를 작업으로 잡았다는 도장.  진행도 적립(ResearchBench.ResearcherSpeedSum)과
            //  머리위 라벨이 **둘 다 이 도장만** 본다 — 세 곳이 같은 사실을 말하도록.
            //  (PawnResearchWork 주석에 왜 위치 기반 추정을 버렸는지 적어 뒀다.)
            //  이동 중에도 찍는다: 벤치로 걸어가는 것도 연구 작업의 일부이고, 라벨이
            //  "연구"로 유지돼야 그 걸음의 의미가 읽힌다.
            var rw = ctx.transform.GetComponent<PawnResearchWork>();
            if (rw == null) rw = ctx.transform.gameObject.AddComponent<PawnResearchWork>();
            rw.Mark();
            if (bestSq <= 1.5f * 1.5f) return true;   // 반경 안 — 머무는 것이 작업
            if (ctx.movement == null) return false;
            // 벤치 앞 칸(남쪽) — 벤치 본체 콜라이더 위로 끼지 않게.
            ctx.movement.SetTarget((Vector2)best.transform.position + Vector2.down);
            return true;
        }
    }

    /// <summary>콜로니 식량이 목표선 아래면 베리를 채집해 온다 — **항상 존재하는 자율 노동**.
    ///
    /// 계기 (2026-07-31 운영자): "플레이 영상을 보고 있으면 동작 하나하나에 의미가 있어야
    /// 하는데 뭐 하고 있는건지 모르겠음."  머리 위 활동 라벨을 켜고 나서야 실제 상태가
    /// 보였다 — 오후 1시에 세 명 전원이 "떠도는중"이었다.
    ///
    /// 원인은 라벨이 아니라 **행동 목록의 구멍**이다.  자율 작업은 전부 전제가 있다:
    ///   벌목·채광 = 플레이어가 지정해야, 건설 = 청사진이 있어야, 운반 = 바닥에 더미가
    ///   있어야, 요리 = 식량 여유가 있어야, 수확 = 작물이 익어야, 연구 = 담당자만.
    /// 그래서 플레이어가 찍어 준 나무를 다 베고 나면 콜로니는 **말 그대로 할 일이 없다**.
    /// 콜로니 심에서 사람이 멍하니 서 있는 화면은 "AI 가 죽었다"로 읽힌다.
    ///
    /// 베리 채집은 그 구멍을 메우기에 맞는 일이다 — 맵에 항상 있고, 식량은 항상 줄고,
    /// 결과가 눈에 보인다(수풀로 걸어가 → 채집 → 더미를 창고로 운반).  개인 허기로
    /// 발동하는 EatBerryAction 과 달리 **콜로니 비축**을 기준으로 삼는다.
    ///
    /// 목록에서의 위치는 맨 아래다.  플레이어가 손으로 찍은 일(벌목·채광)과 무기 제작이
    /// 먼저다 — 무기 제작을 위로 올렸다가 지정한 나무를 아무도 안 베는 회귀를 이미 한 번
    /// 냈다(p0-remote-chop).  같은 실수를 반복하지 않는다.</summary>
    public class GatherBerryAction : IPawnAction
    {
        public string DisplayName => "채집";
        public WorkKind Kind => WorkKind.Gather;

        // 이 아래면 채집하러 간다.  요리(CookMealAction)가 식량을 식사로 바꿔 쓰므로
        //  원재료는 넉넉히 유지해야 '요리할 게 없어 굶는' 구간이 안 생긴다.
        //  60 → 120 (2026-07-31 실측): 60 에서 멈추니 목표선에 닿자마자 다시 전원 '떠도는중'
        //  이 됐다.  3인 콜로니가 하루에 식사 여러 번 + 겨울 대비까지 하려면 원재료 버퍼는
        //  이보다 두텁다.  수풀은 맵에 흔하고 재생하므로 고갈 걱정도 없다.
        public float colonyFoodTarget = 120f;

        public bool TryStart(PawnContext ctx)
        {
            if (ctx.gatherer == null) return false;
            var rm = ResourceManager.Instance;
            if (rm != null && rm.food >= colonyFoodTarget) return false;
            // ⚠ 플레이어가 찍어 둔 일이 남아 있으면 자율 채집은 하지 않는다.
            //  이 게이트가 없으면 목록 최하단에 둔 것만으로는 부족하다 — Decide 는
            //  **우선순위 밴드를 먼저** 돌고 그 안에서 목록 순서를 보기 때문이다.
            //  이 액션의 Kind 는 Gather 이므로, 채집 숙련이 높아 Gather=1~2 인 림은
            //  벌목(Chop=3~4)보다 **앞선 밴드에서** 채집을 집어 간다.
            //  실측 회귀(`p0-remote-chop`): 나무를 지정했는데 30초 동안 아무도 '벌목'을
            //  시작하지 않았다(나무는 나중에 베이긴 했다 — 246→245).  무기 제작을 위로
            //  올렸다가 낸 회귀와 **같은 원인**이다.  그때는 목록 순서를 내려서 막았는데,
            //  밴드가 순서를 앞지르는 경우엔 그것만으로 안 된다.
            //  그래서 규칙을 '목록에서의 위치'가 아니라 **미처리 지정의 유무**로 표현한다.
            if (HasWorkThatComesFirst(ctx)) return false;
            var bush = EatBerryAction.FindNearestFreeBush(ctx);
            if (bush == null) return false;
            if (!ReservationManager.TryReserve(bush, ctx.transform.gameObject)) return false;
            ctx.gatherer.SetBushTarget(bush);
            return true;
        }

        /// <summary>**내가 지금 맡을 수 있는, 자율 채집보다 먼저인 일**이 남아 있는가.
        ///  두 갈래다 — ① 플레이어가 낸 지시(벌목·채광·건설) ② 바닥에 남은 운반.
        ///
        ///  1차 구현은 "지정이 하나라도 있으면" 이었는데 너무 뭉툭했다 — 실측 화면에서
        ///  나무 한 그루를 찍자 한 명이 그것을 맡고 **나머지 두 명이 채집도 않고 서 있었다**.
        ///  이미 남이 맡은 일까지 기다린 셈이다.
        ///
        ///  그래서 각 작업 액션의 **자기 탐색기를 그대로 재사용한다**.  그 탐색기는 이미
        ///  '지정됐고 + 다른 폰이 예약하지 않은' 대상만 돌려주므로, 여기서 non-null 이면
        ///  "내가 지금 착수할 수 있는 지시가 실재한다"는 뜻이다.  판정을 복제하지 않으니
        ///  예약 규칙이 바뀌어도 두 곳이 갈라지지 않는다.
        ///
        ///  개인 허기로 발동하는 EatBerryAction 은 이 게이트를 쓰지 않는다 — 굶는 것은
        ///  지시 이행보다 먼저다.  여기서 미루는 것은 **여유 있을 때의 비축 노동**뿐이다.</summary>
        private static bool HasWorkThatComesFirst(PawnContext ctx)
        {
            if (ChopTreeAction.FindFreeMarkedTree(ctx) != null) return true;
            if (MineStoneAction.FindFreeMarkedVein(ctx) != null) return true;
            if (BuildBlueprintAction.FindFreeBlueprint(ctx) != null) return true;
            // 바닥에 굴러다니는 더미도 먼저다.  이 원칙은 이미 PawnUtilityAI 의 액션 목록
            //  주석에 적혀 있었다 — "줍을 더미가 있으면 새 채집/벌목보다 운반을 먼저 한다"
            //  (야외 더미는 부패하고, 창고에 들어가야 카운터에 적립된다).  그런데 그 원칙도
            //  **목록 순서로만** 지켜져 있어서 밴드를 넘지 못했다: 채집(Gather)은 숙련이
            //  높으면 1~2 인데 운반(Haul)은 3 이라, 목록에서 운반이 위에 있어도 채집이
            //  앞선 밴드에서 먼저 집힌다.
            //  실측(`p6-objectives`): 나무를 베어 놓고 아무도 나르지 않아 목재 카운터가
            //  400 에 닿지 못했다 — 바닥엔 목재가 널려 있는데 셋 다 베리를 따고 있었다.
            //  §회귀와 같은 교훈: **Kind 가 다르면 목록 순서는 보호 장치가 아니다.**
            if (HaulWoodAction.FindFreeWoodPile(ctx) != null) return true;
            if (HaulStoneAction.FindFreeStoneChunk(ctx) != null) return true;
            return HaulMeatAction.FindFreeMeatPile(ctx) != null;
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
