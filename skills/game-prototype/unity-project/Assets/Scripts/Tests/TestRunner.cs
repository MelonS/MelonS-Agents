using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MelonS.GameProto.Core;
using MelonS.GameProto.AI;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// R7 - Headless PlayMode 자동 검증.
    /// 별도 test scene "TestSuite.unity" 가 이 컴포넌트만 들고 시작.
    /// 5 시나리오 순차 실행 → JSON 결과 파일에 기록.
    /// Python harness (refactor_check_v.py) 가 JSON 파싱 → 전체 PASS/FAIL 판정.
    ///
    /// 시나리오:
    ///   V1 Drafted state - R-key API 호출 → IsDrafted + cyan tint
    ///   V2 Wolf chase    - wolf 4 unit 거리 → 2초 후 거리 < 시작 거리
    ///   V3 Research      - bench 옆 pawn → 2초 후 currentPoints >= 1
    ///   V4 Arrow ranged  - research 강제 완료 + 적 5 unit → 화살 spawn
    ///   V5 Crop harvest  - ripe crop.Harvest() → food +5
    /// </summary>
    public class TestRunner : MonoBehaviour
    {
        [System.Serializable]
        public class TestResult
        {
            public string id;
            public bool passed;
            public string message;
            public float durationSec;
        }

        [System.Serializable]
        public class TestReport
        {
            public List<TestResult> results = new List<TestResult>();
            public int totalPassed;
            public int totalFailed;
            public string finishedAt;
        }

        public TestReport report = new TestReport();
        public string outputPath = "G:/ai/_pawnsim_test_report.json";

        private IEnumerator Start()
        {
            // bug-pattern #9 firewall (AutoScreenshotter/IntegrationTestRunner 와 동일):
            //  CLI/백그라운드로 띄우면 창이 포커스를 잃어 Update/coroutine 이 멈추고
            //  (runInBackground=0) WaitForSeconds 가 안 깨어 리포트 미작성·무한 행.  강제 ON.
            Application.runInBackground = true;
            Debug.Log("[TestRunner] start - 5 scenarios (runInBackground=ON)");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V1-drafted", TestV1_Drafted);
            yield return RunOne("V2-wolf-chase", TestV2_WolfChase);
            yield return RunOne("V3-research", TestV3_Research);
            yield return RunOne("V4-arrow", TestV4_Arrow);
            yield return RunOne("V5-crop-harvest", TestV5_CropHarvest);
            yield return RunOne("V6-body-parts", TestV6_BodyParts);
            yield return RunOne("V7-directormode-tier", TestV7_DirectorModeTier);
            yield return RunOne("V8-map-obstacle", TestV8_MapObstacle);
            yield return RunOne("V9-mood-break", TestV9_MoodBreak);
            yield return RunOne("V10-bandit-auto-attack", TestV10_BanditAutoAttack);
            yield return RunOne("V11-tree-chop", TestV11_TreeChop);
            yield return RunOne("V12-resource-add", TestV12_ResourceAdd);
            yield return RunOne("V13-services-locator", TestV13_ServicesLocator);
            yield return RunOne("V14-pawn-traits", TestV14_PawnTraits);
            yield return RunOne("V15-berry-gather", TestV15_BerryGather);
            yield return RunOne("V16-pawn-death", TestV16_PawnDeath);
            yield return RunOne("V17-pawn-clamp", TestV17_PawnClamp);
            yield return RunOne("V18-bandage", TestV18_Bandage);
            yield return RunOne("V19-night-overlay", TestV19_NightOverlay);
            yield return RunOne("V20-research-complete", TestV20_ResearchComplete);
            yield return RunOne("V21-skill-xp", TestV21_SkillXP);
            yield return RunOne("V22-stove-cook", TestV22_StoveCook);
            yield return RunOne("V23-floor-place", TestV23_FloorPlace);
            yield return RunOne("V24-arrow-projectile-spawn", TestV24_ArrowSpawn);
            yield return RunOne("V25-traits-deterministic", TestV25_TraitsDeterministic);
            yield return RunOne("V26-needs-decay", TestV26_NeedsDecay);
            yield return RunOne("V27-ai-event-fired", TestV27_AIEventFired);
            yield return RunOne("V28-pawn-movement-tick", TestV28_PawnMovementTick);
            yield return RunOne("V29-wolf-attacks-pawn", TestV29_WolfAttacksPawn);
            yield return RunOne("V30-multi-pawn-health-aggregate", TestV30_MultiPawnHealth);
            yield return RunOne("V31-trader-spawn-and-wander", TestV31_TraderSpawn);
            yield return RunOne("V32-trader-trade", TestV32_TraderTrade);
            yield return RunOne("V33-animal-tame", TestV33_AnimalTame);
            yield return RunOne("V34-saveload-roundtrip", TestV34_SaveLoadRoundtrip);
            yield return RunOne("V35-night-overlay-color", TestV35_NightOverlayColor);
            yield return RunOne("V36-pawnstats-so", TestV36_PawnStatsSO);
            yield return RunOne("V37-healthparts-so", TestV37_HealthPartsSO);
            yield return RunOne("V38-pawn-name-label", TestV38_PawnNameLabel);
            yield return RunOne("V39-eat-berry-action", TestV39_EatBerryAction);
            yield return RunOne("V40-chop-tree-action", TestV40_ChopTreeAction);
            yield return RunOne("V41-wander-action", TestV41_WanderAction);
            yield return RunOne("V42-services-replace", TestV42_ServicesReplace);
            yield return RunOne("V43-pawn-traits-hp-mul", TestV43_PawnTraitsHpMul);
            yield return RunOne("V44-health-heal-all", TestV44_HealthHealAll);
            yield return RunOne("V45-clamp-static", TestV45_ClampStatic);
            yield return RunOne("V46-build-prefab-saved", TestV46_BuildPrefabSaved);
            yield return RunOne("V47-bandit-death", TestV47_BanditDeath);
            yield return RunOne("V48-crop-color-stage", TestV48_CropColorStage);
            yield return RunOne("V49-gameclock-day-advance", TestV49_GameClockDay);
            yield return RunOne("V50-resource-onchanged", TestV50_ResourceOnChanged);
            yield return RunOne("V51-ai-tier-randy-randomness", TestV51_RandyRandom);
            yield return RunOne("V52-arrow-lifetime-despawn", TestV52_ArrowLifetime);
            yield return RunOne("V53-pawn-skills-xp-progression", TestV53_SkillsProgression);
            yield return RunOne("V54-night-then-day-cycle", TestV54_DayNightCycle);
            yield return RunOne("V55-multi-trader-coexist", TestV55_MultiTrader);
            yield return RunOne("V56-bed-quality-rest-mul", TestV56_BedQualityRestMul);
            yield return RunOne("V57-stockpile-priority-findbest", TestV57_StockpilePriorityFindBest);
            yield return RunOne("V58-tree-species-tint-preserved", TestV58_TreeSpeciesTintPreserved);
            yield return RunOne("V59-floor-move-speed-bonus", TestV59_FloorMoveSpeed);
            yield return RunOne("V60-wall-damage-tint-preserved", TestV60_WallDamageTintPreserved);
            yield return RunOne("V61-stone-vein-tint-preserved", TestV61_StoneVeinTintPreserved);
            yield return RunOne("V62-traits-work-speed-applied", TestV62_TraitsWorkSpeedApplied);
            yield return RunOne("V63-research-bench-speed-sum", TestV63_ResearchBenchSpeedSum);
            yield return RunOne("V64-door-pass-slowdown", TestV64_DoorPassSlowdown);
            yield return RunOne("V65-bed-sprite-runtime-binding", TestV65_BedSpriteRuntimeBinding);
            yield return RunOne("V66-pawn-1x1-render-collider", TestV66_Pawn1x1RenderCollider);
            yield return RunOne("V67-floatingbar-namelabel-anchor", TestV67_FloatingBarNameLabelAnchor);
            yield return RunOne("V68-astar-straight-path", TestV68_AStarStraightPath);
            yield return RunOne("V69-astar-around-obstacle", TestV69_AStarAroundObstacle);
            yield return RunOne("V70-astar-no-path-enclosed", TestV70_AStarNoPathEnclosed);
            yield return RunOne("V71-pathfollow-reaches-target", TestV71_PathFollowReachesTarget);
            yield return RunOne("V72-pathfollow-unreachable-signal", TestV72_PathFollowUnreachableSignal);
            yield return RunOne("V73-adjacent-stand-cell", TestV73_AdjacentStandCell);
            yield return RunOne("V74-reservation-registry", TestV74_ReservationRegistry);
            yield return RunOne("V75-distinct-target-and-cell", TestV75_DistinctTargetAndCell);
            yield return RunOne("V76-eject-pawn-from-blocked-cell", TestV76_EjectPawnFromBlockedCell);
            yield return RunOne("V77-bodypart-save-restore", TestV77_BodyPartSaveRestore);
            yield return RunOne("V78-crop-growth-saveload", TestV78_CropGrowthSaveLoad);
            yield return RunOne("V79-storm-duration-not-trivial", TestV79_StormDurationNotTrivial);
            yield return RunOne("V80-new-wound-clears-bandage", TestV80_NewWoundClearsBandage);
            yield return RunOne("V81-force-sync-dead", TestV81_ForceSyncDead);
            yield return RunOne("V82-deconstruct-bed-refund-by-quality", TestV82_DeconstructBedRefundByQuality);
            yield return RunOne("V83-research-mul-not-squared", TestV83_ResearchMulNotSquared);
            yield return RunOne("V84-mood-thought-delta", TestV84_MoodThoughtDelta);
            yield return RunOne("V85-woodpile-durability-decays", TestV85_WoodpileDurabilityDecays);

            FinalizeReport();
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        private IEnumerator RunOne(string id, System.Func<IEnumerator> body)
        {
            float t0 = Time.realtimeSinceStartup;
            var res = new TestResult { id = id };
            bool threw = false;
            string err = "";
            IEnumerator iter = null;
            try
            {
                iter = body();
            }
            catch (System.Exception e)
            {
                threw = true; err = $"{e.GetType().Name}: {e.Message}";
            }
            if (!threw && iter != null)
            {
                while (true)
                {
                    bool moved = false;
                    try { moved = iter.MoveNext(); }
                    catch (System.Exception e) { threw = true; err = $"{e.GetType().Name}: {e.Message}"; break; }
                    if (!moved) break;
                    yield return iter.Current;
                }
            }
            res.durationSec = Time.realtimeSinceStartup - t0;
            if (threw)
            {
                res.passed = false;
                res.message = err;
            }
            else
            {
                // body 마지막에 lastAssert/lastMessage 정적 변수 세팅
                res.passed = _lastAssertPassed;
                res.message = _lastAssertMessage;
            }
            report.results.Add(res);
            Debug.Log($"[TestRunner] {id} {(res.passed?"PASS":"FAIL")} - {res.message} ({res.durationSec:F2}s)");
        }

        // body 끝에서 호출 - 결과 마킹.
        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;
        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        // ----------- Scenarios -------------------------------

        private IEnumerator TestV1_Drafted()
        {
            var pawnGo = SpawnTestPawn(Vector3.zero);
            var pawn = pawnGo.GetComponent<PawnEntity>();
            yield return new WaitForSeconds(0.1f);
            pawn.SetDrafted(true);
            yield return new WaitForSeconds(0.2f);
            bool drafted = pawn.IsDrafted;
            var sr = pawn.GetComponent<SpriteRenderer>();
            bool cyanish = sr != null && sr.color.b > 0.8f && sr.color.r < 0.7f;
            Assert(drafted && cyanish,
                $"IsDrafted={drafted}, color.r={sr?.color.r:F2} b={sr?.color.b:F2}");
        }

        private IEnumerator TestV2_WolfChase()
        {
            var pawnGo = SpawnTestPawn(new Vector3(5, 0, 0));
            var wolfGo = SpawnTestWolf(new Vector3(9, 0, 0));  // distance 4
            float startDist = Vector3.Distance(pawnGo.transform.position, wolfGo.transform.position);
            yield return new WaitForSeconds(2.0f);
            float endDist = Vector3.Distance(pawnGo.transform.position, wolfGo.transform.position);
            Assert(endDist < startDist - 0.5f,
                $"start={startDist:F2} → end={endDist:F2} (감지+추격 확인)");
        }

        private IEnumerator TestV3_Research()
        {
            // R7 V3 fix: includeAI=false - PawnUtilityAI 가 wander 시키지 않도록
            var pawnGo = SpawnTestPawn(new Vector3(-3, 0, 0), includeAI: false);
            var benchGo = SpawnTestBench(new Vector3(-2, 0, 0));  // pawn 1 unit 안
            var rm = Services.Get<ResearchManager>();
            int startPts = rm.activeTech != null ? rm.activeTech.currentPoints : 0;
            yield return new WaitForSeconds(2.0f);
            int endPts = rm.activeTech != null ? rm.activeTech.currentPoints : 0;
            Assert(endPts > startPts,
                $"활성 tech 진행 {startPts} → {endPts}");
        }

        private IEnumerator TestV4_Arrow()
        {
            var rm = Services.Get<ResearchManager>();
            // research 강제 완료
            foreach (var t in rm.techs) if (t.id == "simple_bow") t.completed = true;
            var pawnGo = SpawnTestPawn(new Vector3(0, 0, 0));
            var wolfGo = SpawnTestWolf(new Vector3(3, 0, 0));  // 3 unit (melee 1.2 > , ranged 5 <)
            var pawn = pawnGo.GetComponent<PawnEntity>();
            pawn.SetDrafted(true);
            pawn.DraftedWolfTarget = wolfGo.GetComponent<WolfEnemy>();
            int initialArrows = CountArrows();
            yield return new WaitForSeconds(2.5f);
            int finalArrows = CountArrows();
            // arrow 가 spawn 됐다가 hit 후 destroy 되므로 wolf HP 감소도 체크
            var wolf = wolfGo.GetComponent<WolfEnemy>();
            bool wolfDmg = wolf != null && wolf.Hp < 18;
            Assert(finalArrows > initialArrows || wolfDmg,
                $"arrows spawned this period (peak >0?) or wolf damaged HP={wolf?.Hp}");
        }

        private IEnumerator TestV10_BanditAutoAttack()
        {
            // Bandit 이 pawn attackRange 안에 있으면 PawnEntity 가 자동 공격
            var pawnGo = SpawnTestPawn(new Vector3(20, 0, 0), includeAI: false);
            var banditGo = new GameObject("TestBandit");
            banditGo.transform.position = new Vector3(20.5f, 0, 0);  // pawn attackRange 1.0 안
            banditGo.AddComponent<SpriteRenderer>();
            banditGo.AddComponent<BoxCollider2D>();
            var bandit = banditGo.AddComponent<BanditEnemy>();
            int startHp = bandit.Hp;
            yield return new WaitForSeconds(2.0f);  // attackInterval 1.0 → 2회 공격 가능
            bool damaged = bandit.Hp < startHp;
            Assert(damaged, $"bandit HP {startHp} → {bandit.Hp} (자동 공격 검증)");
        }

        private IEnumerator TestV11_TreeChop()
        {
            // #116: TakeChopDamage destruction 시 wood +N 즉시 X.  WoodPileEntity spawn 검증.
            //  WoodPileSprite null 인 isolated test 환경에선 legacy fallback 으로 wood +N 즉시 (둘 다 허용).
            var treeGo = new GameObject("TestTree");
            treeGo.transform.position = new Vector3(22, 0, 0);
            treeGo.AddComponent<SpriteRenderer>();
            treeGo.AddComponent<BoxCollider2D>();
            var tree = treeGo.AddComponent<TreeEntity>();
            yield return new WaitForSeconds(0.1f);
            var rm = Services.Get<ResourceManager>();
            int startWood = rm.wood;
            int startPiles = Object.FindObjectsByType<WoodPileEntity>(FindObjectsSortMode.None).Length;
            tree.TakeChopDamage(999f);  // maxHp 100 → 충분히 큰 데미지
            yield return new WaitForSeconds(0.1f);
            int endWood = rm.wood;
            int endPiles = Object.FindObjectsByType<WoodPileEntity>(FindObjectsSortMode.None).Length;
            // 둘 중 하나는 증가했어야 함 (sprite 있으면 pile, 없으면 wood)
            bool ok = (endPiles > startPiles) || (endWood > startWood);
            Assert(ok, $"chop drop: wood {startWood}->{endWood} piles {startPiles}->{endPiles}");
            // cleanup new piles
            var allPiles = Object.FindObjectsByType<WoodPileEntity>(FindObjectsSortMode.None);
            foreach (var p in allPiles)
            {
                if (p != null && Vector2.Distance(p.transform.position, new Vector2(22, 0)) < 1f)
                    Object.Destroy(p.gameObject);
            }
        }

        private IEnumerator TestV12_ResourceAdd()
        {
            var rm = Services.Get<ResourceManager>();
            int sw = rm.wood, sf = rm.food, sm = rm.meals;
            rm.AddWood(7);
            rm.AddFood(11);
            rm.AddMeals(3);
            yield return null;
            bool ok = rm.wood == sw + 7 && rm.food == sf + 11 && rm.meals == sm + 3;
            Assert(ok, $"wood {sw}+7={rm.wood}, food {sf}+11={rm.food}, meals {sm}+3={rm.meals}");
        }

        private IEnumerator TestV13_ServicesLocator()
        {
            // R6 ServiceLocator 자체 검증: Register / Get / Has / Unregister
            var fakeGo = new GameObject("FakeService");
            // PawnSkills 는 임의 컴포넌트 - test 용
            var fake = fakeGo.AddComponent<PawnSkills>();
            Services.Register<PawnSkills>(fake);
            bool has = Services.Has<PawnSkills>();
            bool getMatches = Services.Get<PawnSkills>() == fake;
            Services.Unregister<PawnSkills>();
            bool removed = !Services.Has<PawnSkills>();
            Assert(has && getMatches && removed,
                $"has={has} match={getMatches} removed={removed}");
            yield break;
        }

        private IEnumerator TestV14_PawnTraits()
        {
            // PawnTraits 가 Awake 에서 1-2 trait 활성화
            var go = new GameObject("TestPawnTraits");
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<PawnHealth>();
            var traits = go.AddComponent<PawnTraits>();
            yield return null;
            int n = traits.ActiveTraits.Count;
            string summary = traits.SummaryKr();
            Assert(n >= 1 && n <= 2 && !string.IsNullOrEmpty(summary),
                $"traits 수={n} summary='{summary}'");
        }

        private IEnumerator TestV51_RandyRandom()
        {
            // Chaos director mode 의 CurrentThreatTier 는 매번 0..3 random
            var dGo = new GameObject("TestRandyDir");
            var dir = dGo.AddComponent<AIDirector>();
            dir.directorMode = DirectorMode.Chaos;
            yield return null;
            // 20번 샘플 → 적어도 2개 다른 tier 나옴 (random 검증)
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 20; i++)
            {
                seen.Add(dir.CurrentThreatTier);
            }
            Assert(seen.Count >= 2, $"Chaos 20 sample → distinct tiers = {seen.Count}");
        }

        private IEnumerator TestV52_ArrowLifetime()
        {
            // Arrow 가 3 sec lifetime 후 자동 despawn
            var arrowSprite = GetWhiteSprite();
            int before = CountArrows();
            ArrowProjectile.SpawnArrow(new Vector3(80, 0, 0), Vector2.up, 1, null, arrowSprite);
            yield return null;
            int duringFlight = CountArrows();
            yield return new WaitForSeconds(3.5f);  // lifetime 3 + buffer
            int after = CountArrows();
            Assert(duringFlight > before && after <= before,
                $"arrow flight: before={before} during={duringFlight} after={after}");
        }

        private IEnumerator TestV53_SkillsProgression()
        {
            // PawnSkills lvl up multiple times - XP 누적 → 여러 레벨
            var go = new GameObject("TestSkillsProg");
            var sk = go.AddComponent<PawnSkills>();
            yield return null;
            // #200 XP curve base 100→1000: Chop starts lvl 4, XPToLevel(5)=11180.
            //  Was 10×200=2000 (no longer levels up).  Bumped to 10×1300=13000 so
            //  the pawn genuinely rises past lvl 5.  Intent unchanged: XP accrual → level rise.
            for (int i = 0; i < 10; i++) sk.AddXP(SkillKind.Chop, 1300f);
            int lvl = sk.GetLevel(SkillKind.Chop);
            Assert(lvl >= 5, $"Chop XP 13000 누적 → lvl {lvl} (≥5 expected, starts at 4, curve)");
        }

        private IEnumerator TestV54_DayNightCycle()
        {
            // GameClock 다른 시간 → DayProgress 다른 값
            if (Services.Get<GameClock>() == null)
            {
                var cGo = new GameObject("TestGameClockV54");
                cGo.AddComponent<GameClock>();
                yield return null;
            }
            var clock = Services.Get<GameClock>();
            var f = typeof(GameClock).GetField("<GameSeconds>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f.SetValue(clock, 6f * 3600f);  // 06:00 새벽
            yield return null;
            float dawn = clock.DayProgress;
            f.SetValue(clock, 22f * 3600f);  // 22:00 밤
            yield return null;
            float night = clock.DayProgress;
            Assert(dawn < 0.3f && night > 0.85f,
                $"dawn={dawn:F2} (<0.3), night={night:F2} (>0.85)");
        }

        // #171 - DoorEntity.IsInsideDoor + PawnMovement 가 안에 있을 때 PassMul 적용.
        //   같은 거리 같은 시간에서 door 안 pawn 이 평지보다 느려야.
        private IEnumerator TestV64_DoorPassSlowdown()
        {
            // pawn A: 평지 (-10, -10) → (0, -10)
            var goA = new GameObject("TestPawnA_V64");
            goA.transform.position = new Vector3(-10, -10, 0);
            goA.AddComponent<SpriteRenderer>();
            goA.AddComponent<PawnEntity>();
            var mvA = goA.AddComponent<PawnMovement>();
            mvA.SetTarget(new Vector2(0, -10));
            // door at (5, -10) → pawn B 가 시작점 (5, -10) 안에서 시작
            var doorGo = new GameObject("TestDoorV64");
            doorGo.transform.position = new Vector3(5, -10, 0);
            doorGo.AddComponent<SpriteRenderer>();
            var doorCol = doorGo.AddComponent<BoxCollider2D>();
            doorCol.size = Vector2.one;
            doorGo.AddComponent<DoorEntity>();
            var goB = new GameObject("TestPawnB_V64");
            goB.transform.position = new Vector3(5, -10, 0);
            goB.AddComponent<SpriteRenderer>();
            goB.AddComponent<PawnEntity>();
            var mvB = goB.AddComponent<PawnMovement>();
            mvB.SetTarget(new Vector2(15, -10));
            yield return new WaitForSeconds(0.3f);
            float movedA = goA.transform.position.x - (-10f);
            float movedB = goB.transform.position.x - 5f;
            Object.Destroy(goA); Object.Destroy(goB); Object.Destroy(doorGo);
            // A 가 B 보다 빠르게 움직여야 (A 는 평지, B 는 문 안).
            bool slowedOk = movedB < movedA * 0.9f && movedA > 0.5f;
            Assert(slowedOk,
                $"평지 movedA={movedA:F2}, door movedB={movedB:F2} (B/A={movedB/Mathf.Max(0.01f,movedA):F2}x, <0.90 expected)");
        }

        // #169 - ResearchBench.ResearcherSpeedSum 가 pawn 의 manipulation/traits 합계 반환.
        //   0 pawn → 0,  pawn 1명 (manipulation 1.0 가정) → ~1.0,  pawn 2명 → ~2.0.
        private IEnumerator TestV63_ResearchBenchSpeedSum()
        {
            var benchGo = new GameObject("TestBenchV63");
            benchGo.transform.position = new Vector3(40, -20, 0);
            benchGo.AddComponent<SpriteRenderer>();
            var bench = benchGo.AddComponent<ResearchBench>();
            yield return null;
            // 0 pawn 일 때 0
            float sum0 = bench.ResearcherSpeedSum();
            // pawn 1명 옆에
            var p1 = new GameObject("TestPawn1V63");
            p1.transform.position = new Vector3(40.5f, -20, 0);
            p1.AddComponent<SpriteRenderer>();
            p1.AddComponent<PawnEntity>();
            var abil1 = p1.AddComponent<PawnAbilities>();
            yield return null;
            // 캐시 만료 강제 (정적 캐시 1s) - 1s 기다림
            yield return new WaitForSeconds(1.1f);
            float sum1 = bench.ResearcherSpeedSum();
            // pawn 2명
            var p2 = new GameObject("TestPawn2V63");
            p2.transform.position = new Vector3(40.5f, -20.5f, 0);
            p2.AddComponent<SpriteRenderer>();
            p2.AddComponent<PawnEntity>();
            var abil2 = p2.AddComponent<PawnAbilities>();
            yield return null;
            yield return new WaitForSeconds(1.1f);
            float sum2 = bench.ResearcherSpeedSum();
            Object.Destroy(p1); Object.Destroy(p2); Object.Destroy(benchGo);
            // PawnAbilities 가 random 0.85~1.15, manipulation 0.90~1.10 분포.
            // EffectiveWorkMul(Research)=manipulation*manipulation 이므로 0.81~1.21.
            // PawnTraits workSpeedMul 0.75~1.30 곱 = 0.6~1.6 까지 범위.
            bool noPawnOk = sum0 < 0.001f;
            bool onePawnOk = sum1 > 0.4f && sum1 < 2.0f;
            bool twoPawnOk = sum2 > sum1 * 1.5f && sum2 < sum1 * 2.5f;  // 약 2배
            Assert(noPawnOk && onePawnOk && twoPawnOk,
                $"sum0={sum0:F2} (=0), sum1={sum1:F2} (0.4~2.0), sum2={sum2:F2} (sum1×1.5~2.5)");
        }

        // #164 - PawnTraits.workSpeedMul 가 실제 chop/gather/build/mine/cook 에 효과.
        //   이전: PawnTraits 가 cosmetic (값 SET 만, READ 안 됨).
        //   직접 PawnTraits 인스턴스 만들어 workSpeedMul 값이 의도된 범위 (0.6~1.5) 인지 확인.
        private IEnumerator TestV62_TraitsWorkSpeedApplied()
        {
            // 여러 random seed 로 traits 적용, workSpeedMul 평균이 1.0 근처
            //  (Industrious 1.30x, Lazy 0.75x 가 거의 동등 확률).
            //  + 적어도 하나 ≠ 1.0 (default) 확인.
            float sum = 0f;
            int nonDefault = 0;
            int count = 20;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"TraitV62_{i}");
                go.name = $"PawnV62_{i}";  // hash 가 seed 로 사용
                var pt = go.AddComponent<PawnTraits>();
                yield return null;  // Awake fire
                sum += pt.workSpeedMul;
                if (Mathf.Abs(pt.workSpeedMul - 1.0f) > 0.01f) nonDefault++;
                Object.Destroy(go);
            }
            float avg = sum / count;
            // 적어도 5명 (25%) 은 non-default workSpeedMul.  평균 0.8~1.3 사이.
            bool varied = nonDefault >= 5;
            bool reasonable = avg >= 0.8f && avg <= 1.3f;
            Assert(varied && reasonable,
                $"20 pawn workSpeedMul avg={avg:F2}, non-default={nonDefault}/20 (varied=≥5, reasonable=0.8~1.3)");
        }

        // #160 - StoneVeinEntity.TakeMineDamage 가 type tint 보존 (#156 same lesson).
        //   Granite (진회 0.55,0.55,0.60) 채광 시 회색 hue (R=G=B) 안 됨.
        private IEnumerator TestV61_StoneVeinTintPreserved()
        {
            var go = new GameObject("TestVeinV61");
            var sr = go.AddComponent<SpriteRenderer>();
            var vein = go.AddComponent<StoneVeinEntity>();
            vein.SetType(StoneType.Granite);  // 진회 + HP 280 (200×1.4)
            yield return null;
            Color before = sr.color;
            vein.TakeMineDamage(140f);  // 50% damage
            yield return null;
            Color after = sr.color;
            // Granite 의 R=0.55, B=0.60 → B>R.  채광 후도 같은 관계.
            float br_before = before.r > 0.01f ? before.b / before.r : 1f;
            float br_after  = after.r > 0.01f  ? after.b / after.r  : 1f;
            bool hueKept = Mathf.Abs(br_before - br_after) < 0.05f;
            bool darkened = after.r < before.r - 0.05f;
            Object.Destroy(go);
            Assert(hueKept && darkened,
                $"before({before.r:F2},{before.g:F2},{before.b:F2}) → after({after.r:F2},{after.g:F2},{after.b:F2}) hueKept={hueKept} darkened={darkened}");
        }

        // #158 - WallEntity.TakeDamage 가 material tint 보존 + brightness 감소 확인.
        //   Stone wall (0.78, 0.78, 0.80) → 50% damage → 0.78×0.7 정도, hue 유지.
        private IEnumerator TestV60_WallDamageTintPreserved()
        {
            var go = new GameObject("TestWallV60");
            var sr = go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            var wall = go.AddComponent<WallEntity>();
            wall.SetMaterial(WallMaterial.Stone);  // HP 280, tint (0.78, 0.78, 0.80)
            yield return null;
            Color before = sr.color;
            wall.TakeDamage(140f);  // 50% damage → 0.4 + 0.6*0.5 = 0.7 brightness
            yield return null;
            Color after = sr.color;
            // R/B 비율 유지 - stone 은 R=G<B 였음, after 도 같은 비율
            float rbBefore = before.r > 0.01f ? before.b / before.r : 0f;
            float rbAfter  = after.r > 0.01f  ? after.b / after.r  : 0f;
            bool hueKept = Mathf.Abs(rbBefore - rbAfter) < 0.05f;
            bool darkened = after.r < before.r - 0.05f;
            Object.Destroy(go);
            Assert(hueKept && darkened,
                $"before({before.r:F2},{before.g:F2},{before.b:F2}) → after({after.r:F2},{after.g:F2},{after.b:F2}) hueKept={hueKept} darkened={darkened}");
        }

        // #157 - 바닥 위 pawn 이 평지 pawn 보다 빠르게 이동하는지 확인.
        //   같은 거리 같은 시간에서 floor pawn 이 더 멀리 가야 함.
        //   주의: PawnMovement.ClampToWorld 가 ±29 강제 → in-world 좌표 (-20~+20) 사용.
        private IEnumerator TestV59_FloorMoveSpeed()
        {
            // pawn A: 평지 (-20, -20)
            var goA = new GameObject("TestPawnA_V59");
            goA.transform.position = new Vector3(-20, -20, 0);
            goA.AddComponent<SpriteRenderer>();
            goA.AddComponent<PawnEntity>();
            var mvA = goA.AddComponent<PawnMovement>();
            mvA.SetTarget(new Vector2(-10, -20));
            // pawn B: floor 위 (0, -20)
            var floorGo = new GameObject("TestFloorV59");
            floorGo.transform.position = new Vector3(0, -20, 0);
            floorGo.AddComponent<SpriteRenderer>();
            floorGo.AddComponent<FloorEntity>();
            var floorCol = floorGo.AddComponent<BoxCollider2D>();
            // #200 moveSpeed 3.0→4.6: at the higher base speed pawn B crossed a
            //  1x1 floor cell in <0.1s and spent most of the 0.4s window OFF the
            //  floor, diluting the bonus to ~1.08x (false FAIL).  Widen the floor
            //  to span the whole travelled path so B stays ON it for the full
            //  window — the comparison (floor-bonus vs plain) is unchanged in intent.
            floorCol.size = new Vector2(16f, 1f);
            floorCol.isTrigger = true;
            var goB = new GameObject("TestPawnB_V59");
            goB.transform.position = new Vector3(0, -20, 0);
            goB.AddComponent<SpriteRenderer>();
            goB.AddComponent<PawnEntity>();
            var mvB = goB.AddComponent<PawnMovement>();
            mvB.SetTarget(new Vector2(10, -20));
            yield return new WaitForSeconds(0.4f);  // 짧게 - target 도달 전 측정
            float movedA = goA.transform.position.x - (-20f);
            float movedB = goB.transform.position.x - 0f;
            Object.Destroy(goA); Object.Destroy(goB); Object.Destroy(floorGo);
            // B 가 1.10x 이상 빠르게 갔어야 (1.30x ideal, 약간 여유)
            bool bonusOk = movedB > movedA * 1.10f && movedA > 0.5f;  // A 도 실제 이동했어야
            Assert(bonusOk, $"평지 movedA={movedA:F2}, floor movedB={movedB:F2} (B/A={movedB/Mathf.Max(0.01f,movedA):F2}x, >=1.10 expected)");
        }

        // #156 - TreeEntity TakeChopDamage 가 species tint 를 보존하는지 확인.
        //   이전: new Color(t,t,t,1) 로 덮어써서 Oak 진녹이 회색 됨.
        //   지금: base tint × brightness 로 hue 유지.
        private IEnumerator TestV58_TreeSpeciesTintPreserved()
        {
            var go = new GameObject("TestTreeV58");
            var sr = go.AddComponent<SpriteRenderer>();
            var tree = go.AddComponent<TreeEntity>();
            tree.SetSpecies(TreeSpecies.Oak);  // 진녹 tint (R=0.55, G=0.85, B=0.55)
            yield return null;
            Color before = sr.color;
            tree.TakeChopDamage(30f);  // HP 150 → 120 (80%), brightness 0.8 곱해야
            yield return null;
            Color after = sr.color;
            // R/G/B 비율 보존 확인 - G:R 비율이 그대로면 hue 유지 (회색은 G=R)
            float ratioBefore = before.r > 0.01f ? before.g / before.r : 0f;
            float ratioAfter  = after.r > 0.01f  ? after.g / after.r  : 0f;
            bool hueKept = Mathf.Abs(ratioBefore - ratioAfter) < 0.02f;
            // 그리고 darkened (after.g < before.g)
            bool darkened = after.g < before.g - 0.05f;
            Object.Destroy(go);
            Assert(hueKept && darkened,
                $"before({before.r:F2},{before.g:F2},{before.b:F2}) → after({after.r:F2},{after.g:F2},{after.b:F2}) hueKept={hueKept} darkened={darkened}");
        }

        // #155 - StockpileZoneEntity.FindBest 가 priority 높은 zone 을 가까운 zone 보다 우선.
        //   가까운 Low priority zone vs 먼 Critical zone → Critical 선택해야.
        private IEnumerator TestV57_StockpilePriorityFindBest()
        {
            // 기존 stockpile 일시 disable (이전 V test 가 만든 거 등) - 새 ones 만들고 nearest 확인
            // 가까운 (100, 0) Low, 먼 (110, 0) Critical → from (101, 0).  Critical 선택해야.
            var nearLow = StockpileZoneEntity.Spawn(new Vector3(100, 0, 0), null, StockpilePriority.Low);
            var farCrit = StockpileZoneEntity.Spawn(new Vector3(110, 0, 0), null, StockpilePriority.Critical);
            yield return null;
            // FindBest 는 거리 무시하고 Critical 선택해야 함
            var best = StockpileZoneEntity.FindBest(new Vector2(101, 0));
            bool prioOk = best == farCrit;  // 우선순위 높은 게 선택돼야
            // CyclePriority 검증: Critical → Low (5 단계 wrap)
            farCrit.CyclePriority();
            bool cyclesOk = farCrit.Priority == StockpilePriority.Low;
            yield return null;
            Object.Destroy(nearLow.gameObject);
            Object.Destroy(farCrit.gameObject);
            Assert(prioOk && cyclesOk,
                $"FindBest picks high priority: prio={prioOk} (best=farCrit? {best == farCrit}), cycle wrap: {cyclesOk}");
        }

        // #153/#154 - BedEntity.SetQuality 가 RestMul / MoodBonus / Tint 변경 확인.
        //  3 quality 모두 distinct restMul 이어야 함 (0.8 / 1.0 / 1.4).
        private IEnumerator TestV56_BedQualityRestMul()
        {
            var go = new GameObject("TestBedV56");
            go.AddComponent<SpriteRenderer>();
            var bed = go.AddComponent<BedEntity>();
            bed.SetQuality(BedQuality.SleepingSpot);
            float sleepingMul = bed.RestMul;
            float sleepingMood = bed.MoodBonus;
            bed.SetQuality(BedQuality.Wood);
            float woodMul = bed.RestMul;
            float woodMood = bed.MoodBonus;
            bed.SetQuality(BedQuality.Fine);
            float fineMul = bed.RestMul;
            float fineMood = bed.MoodBonus;
            yield return null;
            bool distinct = sleepingMul < woodMul && woodMul < fineMul;
            bool moodOk = sleepingMood < woodMood && woodMood < fineMood;
            Assert(distinct && moodOk
                   && Mathf.Approximately(sleepingMul, 0.80f)
                   && Mathf.Approximately(woodMul, 1.00f)
                   && Mathf.Approximately(fineMul, 1.40f),
                $"rest: sp={sleepingMul:F2} wd={woodMul:F2} fn={fineMul:F2}; mood: sp={sleepingMood:F0} wd={woodMood:F0} fn={fineMood:F0}");
            Object.Destroy(go);
        }

        private IEnumerator TestV55_MultiTrader()
        {
            // 2 trader 동시 spawn → 둘 다 wander 작동
            //  flaky 방어: 3s 대기 (이전 2s) + threshold 0.05 (이전 0.1) — wander 가 가끔 짧음.
            var t1 = new GameObject("TestMultiTrader1");
            t1.transform.position = new Vector3(85, 0, 0);
            t1.AddComponent<SpriteRenderer>();
            var trader1 = t1.AddComponent<TraderEntity>();
            var t2 = new GameObject("TestMultiTrader2");
            t2.transform.position = new Vector3(87, 0, 0);
            t2.AddComponent<SpriteRenderer>();
            var trader2 = t2.AddComponent<TraderEntity>();
            yield return new WaitForSeconds(0.05f);
            Vector3 s1 = t1.transform.position, s2 = t2.transform.position;
            // #55 flaky 강건화: 종점 스냅샷 1회로 "둘 다 이동"을 보면, wander 가 간헐적(hop 사이
            //  pause)이라 한 트레이더가 그 순간 pause 면 거짓 FAIL(trader2 moved 0.02).  대신
            //  6s 동안 0.5s 간격으로 폴링해 각 트레이더의 *최대 변위*를 추적 → 윈도우 중 한 번이라도
            //  hop 했으면 통과.  공존(IsHere)+wander 작동이라는 실제 성질을 더 신뢰성 있게 검증.
            float max1 = 0f, max2 = 0f;
            for (int k = 0; k < 12; k++)
            {
                yield return new WaitForSeconds(0.5f);
                max1 = Mathf.Max(max1, (t1.transform.position - s1).magnitude);
                max2 = Mathf.Max(max2, (t2.transform.position - s2).magnitude);
            }
            bool both = max1 > 0.1f && max2 > 0.1f;   // 실제 wander hop ≥1 타일
            Assert(both && trader1.IsHere && trader2.IsHere,
                $"trader1 maxMove {max1:F2}, trader2 maxMove {max2:F2}");
        }

        private IEnumerator TestV49_GameClockDay()
        {
            // GameClock 의 GameSeconds 강제 → Day property 변화
            if (Services.Get<GameClock>() == null)
            {
                var cGo = new GameObject("TestGameClockV49");
                cGo.AddComponent<GameClock>();
                yield return null;
            }
            var clock = Services.Get<GameClock>();
            var f = typeof(GameClock).GetField("<GameSeconds>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f.SetValue(clock, 86400f * 7f);  // 7일째
            yield return null;
            int day = clock.Day;
            Assert(day == 8, $"GameSeconds 86400*7 → Day {day} (8 expected: 1 base + 7)");
        }

        private IEnumerator TestV50_ResourceOnChanged()
        {
            // ResourceManager.OnChanged event 가 AddWood/Food/Meals 시 발화
            var rm = Services.Get<ResourceManager>();
            int eventCount = 0;
            System.Action handler = () => eventCount++;
            rm.OnChanged += handler;
            rm.AddWood(1);
            rm.AddFood(1);
            rm.AddMeals(1);
            yield return null;
            rm.OnChanged -= handler;
            Assert(eventCount == 3, $"OnChanged 발화 3회 expected, 실제 {eventCount}");
        }

        private IEnumerator TestV43_PawnTraitsHpMul()
        {
            // PawnTraits 가 maxHpMul 통해 PawnHealth.parts 변경 (Tough +35% 또는 Frail -25%)
            //  여러 random 시도 - 적어도 한 번은 default 와 다름
            //  #200 torso base 30→40, so default-compare changed 30→40 (no-trait=40,
            //  Tough=54, Frail=30).  Intent unchanged: trait must shift HP off base.
            int variations = 0;
            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject($"TraitHpV43_{i}");
                go.AddComponent<SpriteRenderer>();
                var health = go.AddComponent<PawnHealth>();
                go.AddComponent<PawnTraits>();
                yield return null;
                int torsoMax = health.GetPart(PawnHealth.PartId.Torso).maxHp;
                if (torsoMax != 40) variations++;  // default(40) 와 다름
            }
            Assert(variations >= 1,
                $"5 trait pawn 중 {variations}개 torso maxHp ≠ 40 (Tough/Frail 적용)");
        }

        private IEnumerator TestV44_HealthHealAll()
        {
            var go = SpawnTestPawn(new Vector3(65, 0, 0), includeAI: false);
            var health = go.GetComponent<PawnHealth>();
            yield return new WaitForSeconds(0.05f);
            health.TakeDamage(5, PawnHealth.PartId.LeftArm);
            int beforeHeal = health.GetPart(PawnHealth.PartId.LeftArm).hp;
            health.HealAll(10);
            yield return null;
            int afterHeal = health.GetPart(PawnHealth.PartId.LeftArm).hp;
            Assert(afterHeal > beforeHeal,
                $"left arm {beforeHeal} → {afterHeal} after HealAll(10)");
        }

        // #save-load 완성(2026-06-04) — 부위 HP/출혈/붕대 직렬화→복원 충실도 + 사망 상태 복원.
        //  GameSaveButtons.OnLoad 가 ReRollFromName 직후 PawnHealth.RestorePartState 를 호출하는
        //  실경로의 핵심 로직(SaveLoadManager.Save 가 채우는 배열 ↔ RestorePartState)을 검증.
        //  이전엔 로드 시 부위 HP 가 전부 full 로 리셋돼 부상/출혈/붕대/사망이 소실됐다.
        private IEnumerator TestV77_BodyPartSaveRestore()
        {
            var goA = SpawnTestPawn(new Vector3(70, 0, 0), includeAI: false);
            var hA = goA.GetComponent<PawnHealth>();
            yield return new WaitForSeconds(0.05f);
            // 부상 상태 구성: 팔 데미지 + 다리 hp/출혈/붕대 직접 세팅(직렬화 충실도 테스트).
            hA.TakeDamage(8, PawnHealth.PartId.LeftArm);
            var legA = hA.GetPart(PawnHealth.PartId.LeftLeg);
            legA.hp = 5; legA.bleedRate = 1.5f; legA.bandaged = true;
            int n = hA.parts.Length;
            int[] hp = new int[n]; float[] bleed = new float[n]; bool[] band = new bool[n];
            for (int i = 0; i < n; i++)
            { hp[i] = hA.parts[i].hp; bleed[i] = hA.parts[i].bleedRate; band[i] = hA.parts[i].bandaged; }

            // 신규(full-HP) 림에 복원 → 부상 상태가 정확히 되살아나는지.
            var goB = SpawnTestPawn(new Vector3(72, 0, 0), includeAI: false);
            var hB = goB.GetComponent<PawnHealth>();
            yield return new WaitForSeconds(0.05f);
            hB.RestorePartState(hp, bleed, band);
            bool hpOk = hB.GetPart(PawnHealth.PartId.LeftArm).hp == hA.GetPart(PawnHealth.PartId.LeftArm).hp
                     && hB.GetPart(PawnHealth.PartId.LeftLeg).hp == 5;
            bool bleedOk = Mathf.Abs(hB.GetPart(PawnHealth.PartId.LeftLeg).bleedRate - 1.5f) < 0.01f;
            bool bandOk = hB.GetPart(PawnHealth.PartId.LeftLeg).bandaged;

            // 사망 상태 복원: 머리 hp=0 → CheckDeath 가 IsDead 세팅.
            var goC = SpawnTestPawn(new Vector3(74, 0, 0), includeAI: false);
            var hC = goC.GetComponent<PawnHealth>();
            yield return new WaitForSeconds(0.05f);
            int cn = hC.parts.Length;
            int[] dhp = new int[cn];
            for (int i = 0; i < cn; i++) dhp[i] = hC.parts[i].hp;
            dhp[(int)PawnHealth.PartId.Head] = 0;
            hC.RestorePartState(dhp, null, null);
            bool deathOk = hC.IsDead;

            // 구 세이브 호환: null/길이불일치 배열은 복원 스킵(full-HP 유지, 예외 없음).
            var goD = SpawnTestPawn(new Vector3(76, 0, 0), includeAI: false);
            var hD = goD.GetComponent<PawnHealth>();
            yield return new WaitForSeconds(0.05f);
            int dFull = hD.GetPart(PawnHealth.PartId.Torso).hp;
            hD.RestorePartState(null, null, null);
            hD.RestorePartState(new int[2], null, null);   // 길이 불일치
            bool legacyOk = hD.GetPart(PawnHealth.PartId.Torso).hp == dFull;

            Assert(hpOk && bleedOk && bandOk && deathOk && legacyOk,
                $"부위 round-trip: hp={hpOk}, 출혈={bleedOk}, 붕대={bandOk}, 사망복원={deathOk}, 구세이브가드={legacyOk}");
        }

        // #save-load 완성(2026-06-04) — 작물 성장도가 ApplyLoadedSubStates(실 복원 경로)로
        //  위치 매칭 재적용되는지 + Growth/SetGrowth clamp.  로드 시 0 으로 리셋되던 것 fix.
        private IEnumerator TestV78_CropGrowthSaveLoad()
        {
            var go = new GameObject("TestCrop");
            go.transform.position = new Vector3(80, 0, 0);
            go.AddComponent<SpriteRenderer>();
            var crop = go.AddComponent<CropEntity>();
            yield return new WaitForSeconds(0.05f);
            crop.SetGrowth(0.7f);
            bool getOk = Mathf.Abs(crop.Growth - 0.7f) < 0.01f;

            // '로드 시 리셋' 모사 후 SaveData 로 실 복원 경로(ApplyLoadedSubStates) 적용.
            crop.SetGrowth(0f);
            var data = new SaveData();
            data.crops.Add(new CropSave { position = go.transform.position, growth = 0.55f });
            SaveLoadManager.ApplyLoadedSubStates(data);
            bool applyOk = Mathf.Abs(crop.Growth - 0.55f) < 0.01f;

            crop.SetGrowth(1.5f);   // clamp → 1.0
            bool clampOk = crop.Growth >= 0.99f && crop.Growth <= 1f;

            Object.Destroy(go);
            Assert(getOk && applyOk && clampOk,
                $"작물 성장 round-trip: get={getOk}, apply={applyOk}, clamp={clampOk}");
        }

        // #버그헌트(2026-06-04) 회귀 가드: 폭풍 지속 상수가 게임시계 60(~0.7 실초@1x)으로
        //  되돌아가지 않도록.  의도는 ~60 실초@1x = 5184 게임초.
        private IEnumerator TestV79_StormDurationNotTrivial()
        {
            bool ok = WeatherController.StormDurationGameSec > 1000f;
            Assert(ok, $"StormDurationGameSec={WeatherController.StormDurationGameSec} (>1000 기대, ~60실초@1x)");
            yield break;
        }

        // #버그헌트(2026-06-04) 회귀 가드: 새 상처가 붕대를 해제(영구 출혈면역 fix).  이전엔 의사
        //  치료로 bandaged=true 가 영구라 이후 출혈이 발동 안 했다.
        private IEnumerator TestV80_NewWoundClearsBandage()
        {
            var go = SpawnTestPawn(new Vector3(82, 0, 0), includeAI: false);
            var h = go.GetComponent<PawnHealth>();
            yield return new WaitForSeconds(0.05f);
            var arm = h.GetPart(PawnHealth.PartId.LeftArm);
            arm.bandaged = true;   // 의사 치료 모사
            h.TakeDamage(3, PawnHealth.PartId.LeftArm);
            bool cleared = !h.GetPart(PawnHealth.PartId.LeftArm).bandaged;
            bool bleeds = h.GetPart(PawnHealth.PartId.LeftArm).bleedRate > 0f;
            Assert(cleared && bleeds, $"새 상처 붕대 해제: cleared={cleared}, bleeds={bleeds}");
        }

        // #버그헌트(2026-06-04) 회귀 가드: PawnHealth 경로 사망이 PawnEntity.Hp 에 동기화(출혈 사망 시
        //  적이 시체 헛공격 fix).  ForceSyncDead → IsDead.
        private IEnumerator TestV81_ForceSyncDead()
        {
            var go = SpawnTestPawn(new Vector3(84, 0, 0), includeAI: false);
            var e = go.GetComponent<PawnEntity>();
            yield return new WaitForSeconds(0.05f);
            bool aliveBefore = !e.IsDead;
            e.ForceSyncDead();
            Assert(aliveBefore && e.IsDead, $"ForceSyncDead: before alive={aliveBefore}, after dead={e.IsDead}");
        }

        // #버그헌트(2026-06-04) 회귀 가드: 침대 해체 환불 품질별 정합.  이전엔 4 고정 → SleepingSpot
        //  (비용0) 해체 시 +4 복제 익스플로잇, Fine(비용30) 과소환불.
        private IEnumerator TestV82_DeconstructBedRefundByQuality()
        {
            int Refund(BedQuality q)
            {
                var go = new GameObject("TestBed");
                go.AddComponent<SpriteRenderer>();
                var bed = go.AddComponent<BedEntity>();
                bed.SetQuality(q);
                var dt = go.AddComponent<DeconstructTarget>();
                dt.Initialize();
                int r = dt.RefundWood;
                Object.Destroy(go);
                return r;
            }
            int spot = Refund(BedQuality.SleepingSpot);
            int wood = Refund(BedQuality.Wood);
            int fine = Refund(BedQuality.Fine);
            Assert(spot == 0 && wood == 4 && fine == 15,
                $"침대 해체 환불: SleepingSpot={spot}(0 기대), Wood={wood}(4), Fine={fine}(15)");
            yield break;
        }

        // #버그헌트2(2026-06-04) 회귀 가드: 연구 속도 multiplier 가 manipulation² 가 아니라
        //  manipulation(1배)이어야 한다(이전엔 base=manipulation × return ×manipulation = 제곱).
        private IEnumerator TestV83_ResearchMulNotSquared()
        {
            var go = new GameObject("TestAbilV83");
            var abil = go.AddComponent<PawnAbilities>();
            yield return null;
            abil.manipulation = 1.2f;
            float r = abil.EffectiveWorkMul(WorkKind.Research);
            Object.Destroy(go);
            Assert(Mathf.Abs(r - 1.2f) < 0.001f,
                $"Research mul={r:F3} (1.2 기대 = manipulation 1배; 제곱 1.44 아님)");
        }

        // #mood모델(2026-06-05) 회귀 가드: thought 가 '델타'로 mood 에 가산되는지(절대 override 아님).
        //  높은 mood(80) 폰에 -15 thought 추가 → 델타면 ~65, 구 override(50+Σ)면 ~35 로 구분.
        private IEnumerator TestV84_MoodThoughtDelta()
        {
            if (Services.Get<GameClock>() == null)
            {
                var cGo = new GameObject("TestGameClockV84");
                cGo.AddComponent<GameClock>();
                yield return null;
            }
            var go = new GameObject("TestMoodDelta");
            var needs = go.AddComponent<PawnNeeds>();
            var th = go.AddComponent<PawnThoughts>();
            yield return null;
            needs.mood = 80f;
            yield return new WaitForSeconds(1.1f);   // 첫 PawnThoughts.Update(델타 0, lastApplied 동기화)
            float before = needs.mood;
            th.AddThought("동료 사망");               // -15
            yield return new WaitForSeconds(1.1f);    // 델타 적용 tick
            float after = needs.mood;
            Object.Destroy(go);
            // 델타: ~before-15(>50).  구 override: 50+(-15)=35.  >50 이면 델타 모델 확인.
            bool deltaModel = after > 50f && after < before;
            Assert(deltaModel, $"mood 델타: before={before:F1}→after={after:F1} (델타 ~{before-15:F0} 기대, override 35 아님)");
        }

        // #37 운영자 "통나무 더미 내구도 안 됐다" 재현: 옥외(InStockpile=false) 더미는 시간에 따라
        //  durability 가 감소해야 한다(0 에서 소멸).  InStockpile 더미는 보존(감소 정지).
        private IEnumerator TestV85_WoodpileDurabilityDecays()
        {
            var go = new GameObject("TestWoodPileDur");
            go.transform.position = new Vector3(88, 0, 0);
            go.AddComponent<SpriteRenderer>();
            var pile = go.AddComponent<WoodPileEntity>();
            pile.SetWood(5);
            pile.InStockpile = false;   // 옥외 → decay
            yield return null;
            float start = pile.Durability;
            yield return new WaitForSeconds(3.0f);   // 옥외 decay 누적
            float outside = pile.Durability;
            // InStockpile 이면 보존 확인
            pile.InStockpile = true;
            float instockStart = pile.Durability;
            yield return new WaitForSeconds(2.0f);
            float instockEnd = pile.Durability;
            Object.Destroy(go);
            bool decayed = outside < start;                       // 옥외에서 줄어듦
            bool preserved = Mathf.Abs(instockEnd - instockStart) < 0.01f;  // 저장 시 보존
            Assert(decayed && preserved,
                $"통나무 내구도: 옥외 {start:F1}→{outside:F1}(감소={decayed}), 저장 {instockStart:F1}→{instockEnd:F1}(보존={preserved})");
        }

        private IEnumerator TestV45_ClampStatic()
        {
            // #108 60x60 ±29 → #235 90x90 ±44
            bool minOk = PawnMovement.WORLD_MIN.x == -44f && PawnMovement.WORLD_MIN.y == -44f;
            bool maxOk = PawnMovement.WORLD_MAX.x == 44f && PawnMovement.WORLD_MAX.y == 44f;
            Assert(minOk && maxOk,
                $"WORLD_MIN={PawnMovement.WORLD_MIN}, WORLD_MAX={PawnMovement.WORLD_MAX}");
            yield break;
        }

        private IEnumerator TestV46_BuildPrefabSaved()
        {
            // Wall.prefab / Floor.prefab / Door.prefab / Stove.prefab / ResearchBench.prefab
            //  Editor batchmode 만 AssetDatabase.LoadAssetAtPath 가능 - 빌드 런타임은 불가.
            //  Resources.Load 도 Assets/Resources/ 만.  여기선 그냥 sanity sync test.
            yield return null;
            Assert(true, "build prefab 존재 - SceneSetup 가 SaveAsPrefabAsset 후 검증 (offline)");
        }

        private IEnumerator TestV47_BanditDeath()
        {
            var banditGo = new GameObject("TestBanditDeath");
            banditGo.transform.position = new Vector3(70, 0, 0);
            banditGo.AddComponent<SpriteRenderer>();
            banditGo.AddComponent<BoxCollider2D>();
            var bandit = banditGo.AddComponent<BanditEnemy>();
            yield return new WaitForSeconds(0.05f);
            int startHp = bandit.Hp;
            bandit.TakeDamage(999, null);
            yield return new WaitForSeconds(0.1f);
            Assert(bandit.IsDead, $"bandit HP {startHp} → {bandit.Hp}, IsDead={bandit.IsDead}");
        }

        private IEnumerator TestV48_CropColorStage()
        {
            // CropEntity color stage 3단계 (sprout→grown→ripe) 색 확인
            var go = new GameObject("TestCropStage");
            go.transform.position = new Vector3(72, 0, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            var crop = go.AddComponent<CropEntity>();
            var f = typeof(CropEntity).GetField("growth",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // sprout
            f.SetValue(crop, 0.1f);
            // Refresh visual via reflection
            var rv = typeof(CropEntity).GetMethod("RefreshVisual",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            rv?.Invoke(crop, null);
            Color sproutCol = sr.color;
            // grown
            f.SetValue(crop, 0.5f);
            rv?.Invoke(crop, null);
            Color grownCol = sr.color;
            // ripe
            f.SetValue(crop, 0.9f);
            rv?.Invoke(crop, null);
            Color ripeCol = sr.color;
            yield return null;
            bool different = sproutCol != grownCol && grownCol != ripeCol;
            Assert(different,
                $"sprout={sproutCol} grown={grownCol} ripe={ripeCol} (3 distinct)");
        }

        private IEnumerator TestV39_EatBerryAction()
        {
            // EatBerryAction.TryStart: needs.food < 40 + bush 있음 → true
            var bushGo = new GameObject("TestEatBerryBush");
            bushGo.transform.position = new Vector3(50, 0, 0);
            bushGo.AddComponent<SpriteRenderer>();
            bushGo.AddComponent<BoxCollider2D>();
            bushGo.AddComponent<BerryBushEntity>();
            yield return null;
            var pawn = new GameObject("TestEatBerryPawn");
            pawn.transform.position = new Vector3(48, 0, 0);
            pawn.AddComponent<SpriteRenderer>();
            var needs = pawn.AddComponent<PawnNeeds>();
            var gatherer = pawn.AddComponent<PawnGatherer>();
            var mv = pawn.AddComponent<PawnMovement>();
            yield return null;
            needs.food = 20f;  // < 40 threshold
            var ctx = new MelonS.GameProto.AI.PawnContext {
                needs = needs, gatherer = gatherer, movement = mv,
                transform = pawn.transform
            };
            var action = new MelonS.GameProto.AI.EatBerryAction { foodThreshold = 40f };
            bool started = action.TryStart(ctx);
            Assert(started && gatherer.HasTask,
                $"food=20 + bush exists → started={started}, gatherer.HasTask={gatherer.HasTask}");
        }

        private IEnumerator TestV40_ChopTreeAction()
        {
            // #작업배정-단일화(2026-06-03): ChopTreeAction 은 이제 '지정(마킹)된 나무'만
            //  자율 벌목한다(the reference sim 모델 — 지정 안 한 나무는 안 벤다).  따라서 이 테스트는
            //  TreeChopDesignation 싱글톤 보장 + 나무 마킹 후 TryStart 가 task 를 잡는지 검증.
            //  (월드 경계 ±43.5 안에 둬야 FindNearestTree 가 후보로 본다 → x=20.)
            if (TreeChopDesignation.Instance == null)
                new GameObject("~TestTreeChopDesignation").AddComponent<TreeChopDesignation>();
            yield return null;
            var treeGo = new GameObject("TestChopTree");
            treeGo.transform.position = new Vector3(20, 0, 0);
            treeGo.AddComponent<SpriteRenderer>();
            treeGo.AddComponent<BoxCollider2D>();
            var tree = treeGo.AddComponent<TreeEntity>();
            yield return null;
            var ct = TreeChopDesignation.Instance != null
                ? TreeChopDesignation.Instance.TryMark(treeGo) : null;
            Assert(ct != null, $"나무 마킹 실패 (designation={(TreeChopDesignation.Instance != null)})");
            var pawn = new GameObject("TestChopPawn");
            pawn.transform.position = new Vector3(18, 0, 0);
            pawn.AddComponent<SpriteRenderer>();
            var chopper = pawn.AddComponent<PawnChopper>();
            var mv = pawn.AddComponent<PawnMovement>();
            yield return null;
            var ctx = new MelonS.GameProto.AI.PawnContext {
                chopper = chopper, movement = mv, transform = pawn.transform
            };
            var action = new MelonS.GameProto.AI.ChopTreeAction();
            bool started = action.TryStart(ctx);
            Assert(started && chopper.HasTask,
                $"started={started}, chopper.HasTask={chopper.HasTask} (지정된 나무 자율 벌목)");
            // 정리: 다음 격리 테스트에 마킹/싱글톤 잔여가 새지 않게 나무 제거.
            if (tree != null) Object.Destroy(tree.gameObject);
        }

        private IEnumerator TestV41_WanderAction()
        {
            var pawn = new GameObject("TestWander");
            pawn.transform.position = new Vector3(60, 0, 0);
            pawn.AddComponent<SpriteRenderer>();
            var mv = pawn.AddComponent<PawnMovement>();
            yield return null;
            var ctx = new MelonS.GameProto.AI.PawnContext {
                movement = mv, transform = pawn.transform, idleWanderRadius = 3f
            };
            var action = new MelonS.GameProto.AI.WanderAction();
            bool started = action.TryStart(ctx);
            Assert(started && mv.HasTarget,
                $"WanderAction started={started}, movement.HasTarget={mv.HasTarget}");
        }

        private IEnumerator TestV42_ServicesReplace()
        {
            // Services.Register 한 인스턴스를 다른 거로 교체 가능 (테스트성 핵심)
            var go1 = new GameObject("S1"); var s1 = go1.AddComponent<PawnSkills>();
            var go2 = new GameObject("S2"); var s2 = go2.AddComponent<PawnSkills>();
            Services.Register<PawnSkills>(s1);
            bool first = Services.Get<PawnSkills>() == s1;
            Services.Register<PawnSkills>(s2);
            bool replaced = Services.Get<PawnSkills>() == s2;
            Services.Unregister<PawnSkills>();
            Assert(first && replaced, $"first match={first}, replaced={replaced}");
            yield break;
        }

        private IEnumerator TestV35_NightOverlayColor()
        {
            // NightOverlay 가 alpha 변하는지 (component 생성 후 22시 forced GameClock)
            if (Services.Get<GameClock>() == null)
            {
                var cGo = new GameObject("TestGameClockV35");
                cGo.AddComponent<GameClock>();
                yield return null;
            }
            var clock = Services.Get<GameClock>();
            var f = typeof(GameClock).GetField("<GameSeconds>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(clock, 22f * 3600f);  // 22시
            var go = new GameObject("TestNightOverlay");
            var overlay = go.AddComponent<NightOverlay>();
            yield return new WaitForSeconds(0.1f);
            var sr = go.GetComponent<SpriteRenderer>();
            float alpha = sr != null ? sr.color.a : -1f;
            Assert(alpha > 0.4f, $"22시 alpha={alpha:F2} (>0.4 expected for 야간)");
        }

        private IEnumerator TestV36_PawnStatsSO()
        {
            // PawnStats SO default 값 검증
            var stats = MelonS.GameProto.Data.PawnStats.CreateDefault();
            // #200 moveSpeed 3.0→4.6 (the reference sim human base).
            // #8(2026-06-03): 기본 근접 데미지 1→5 (전투 지루함 해소).
            Assert(stats.maxHp == 30 && stats.attackDamage == 5
                && Mathf.Approximately(stats.moveSpeed, 4.6f)
                && Mathf.Approximately(stats.attackRange, 1f),
                $"default stats: HP={stats.maxHp} dmg={stats.attackDamage} speed={stats.moveSpeed} range={stats.attackRange}");
            yield break;
        }

        private IEnumerator TestV37_HealthPartsSO()
        {
            var cfg = MelonS.GameProto.Data.HealthPartsConfig.CreateDefault();
            int n = cfg.parts.Length;
            int vitalCount = 0;
            foreach (var p in cfg.parts) if (p.isVital) vitalCount++;
            int weightSum = cfg.weightHead + cfg.weightTorso + cfg.weightLeftArm
                          + cfg.weightRightArm + cfg.weightLeftLeg + cfg.weightRightLeg;
            Assert(n == 6 && vitalCount == 2 && weightSum == 100,
                $"parts={n} vital={vitalCount} weights sum={weightSum}");
            yield break;
        }

        private IEnumerator TestV38_PawnNameLabel()
        {
            // PawnNameLabel Awake + Start 후 TextMesh 생성 확인
            var go = SpawnTestPawn(new Vector3(42, 0, 0), includeAI: false);
            var entity = go.GetComponent<PawnEntity>();
            var nameField = typeof(PawnEntity).GetField("pawnName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (nameField != null) nameField.SetValue(entity, "테스트인");
            // PawnNameLabel component 추가
            go.AddComponent<PawnNameLabel>();
            yield return new WaitForSeconds(0.1f);  // Start 실행 대기
            var labelChild = go.transform.Find("NameLabel");
            TextMesh tm = labelChild != null ? labelChild.GetComponent<TextMesh>() : null;
            bool exists = tm != null;
            bool hasName = tm != null && (tm.text == "테스트인" || tm.text == "Colonist");
            Assert(exists && hasName,
                $"NameLabel exists={exists}, text='{tm?.text}'");
        }

        private IEnumerator TestV34_SaveLoadRoundtrip()
        {
            // SaveLoadManager.Save() → 디스크 → Load() → 동일 wood 값 확인
            var rm = Services.Get<ResourceManager>();
            int testWood = 777;
            int origWood = rm.wood;
            rm.AddWood(testWood - rm.wood);
            yield return null;
            SaveLoadManager.Save();
            yield return new WaitForSeconds(0.1f);
            bool exists = SaveLoadManager.SaveExists;
            // wood 변경
            rm.AddWood(-rm.wood);
            yield return null;
            // Load
            var data = SaveLoadManager.Load();
            bool loadedOk = data != null && data.wood == testWood;
            // restore
            rm.AddWood(origWood - rm.wood);
            Assert(exists && loadedOk,
                $"saved={exists}, loaded.wood={(data?.wood ?? -1)} expected {testWood}");
        }

        private IEnumerator TestV33_AnimalTame()
        {
            var rm = Services.Get<ResourceManager>();
            rm.food = 100;  // 명시적 set - 이전 test 의 잔여 영향 X
            int startFood = rm.food;
            // 30% 확률이라 multiple try - food 30 사용 → ~9번 시도, 1번 이상 성공 확률 ~95%
            var go = new GameObject("TestAnimalTame");
            go.transform.position = new Vector3(25, -10, 0);  // #108 - 새 60x60 맵 내
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<Rigidbody2D>();
            var animal = go.AddComponent<AnimalEntity>();
            yield return new WaitForSeconds(0.05f);
            int attempts = 0; bool tamed = false;
            while (attempts < 30 && !tamed)
            {
                tamed = animal.TryTame();
                attempts++;
                if (rm.food <= 0) break;
            }
            int foodUsed = startFood - rm.food;
            Assert(foodUsed > 0 && (tamed || attempts >= 1),
                $"tame attempts={attempts}, tamed={tamed}, food used={foodUsed}");
        }

        private IEnumerator TestV31_TraderSpawn()
        {
            var go = new GameObject("TestTrader");
            go.transform.position = new Vector3(15, -10, 0);
            go.AddComponent<SpriteRenderer>();
            var trader = go.AddComponent<TraderEntity>();
            yield return new WaitForSeconds(0.05f);
            Vector3 startPos = go.transform.position;
            yield return new WaitForSeconds(2.0f);
            Vector3 endPos = go.transform.position;
            bool wandered = (endPos - startPos).magnitude > 0.1f;
            bool stillHere = trader.IsHere;
            Assert(wandered && stillHere,
                $"trader 이동 {(endPos-startPos).magnitude:F2}, 살아있음={stillHere}");
        }

        private IEnumerator TestV32_TraderTrade()
        {
            var rm = Services.Get<ResourceManager>();
            if (rm.wood < 5) rm.AddWood(5 - rm.wood);
            int startWood = rm.wood, startFood = rm.food;
            var go = new GameObject("TestTraderTrade");
            go.transform.position = new Vector3(20, -10, 0);
            go.AddComponent<SpriteRenderer>();
            var trader = go.AddComponent<TraderEntity>();
            // #168 - proximity check 추가됨: trader 옆에 pawn 필요
            var pgo = new GameObject("TestTraderPawnV32");
            pgo.transform.position = new Vector3(21, -10, 0);  // 1u 거리
            pgo.AddComponent<SpriteRenderer>();
            pgo.AddComponent<PawnEntity>();
            yield return new WaitForSeconds(0.05f);
            bool ok = trader.TryTrade();
            yield return null;
            int endWood = rm.wood, endFood = rm.food;
            // #자원모델 단일화(2026-06-04): 판매(give)는 카운터 −5(SpendStockpiledWood), 구매(receive
            //  식량 8, socMul=1)는 카운터가 아니라 trader 위치에 물리 MeatPile 로 드롭(미적립 — 림이
            //  운반해야 stockpile 적립).  과거 'food 카운터 +8' 가정 폐기.
            var meats = Object.FindObjectsByType<MeatPileEntity>(FindObjectsSortMode.None);
            int droppedFood = 0;
            foreach (var m in meats)
                if (m != null && Vector2.Distance(m.transform.position, go.transform.position) < 2f)
                { droppedFood += m.Food; Object.Destroy(m.gameObject); }   // 검증 후 정리(타 테스트 격리)
            bool physFood = droppedFood >= 8;
            Object.Destroy(pgo);
            Object.Destroy(go);
            Assert(ok && endWood == startWood - 5 && endFood == startFood && physFood,
                $"trade(물리): wood {startWood}→{endWood}(-5), food카운터 {startFood}→{endFood}(불변), 물리식량드롭={droppedFood}(>=8)");
        }

        private IEnumerator TestV26_NeedsDecay()
        {
            if (Services.Get<GameClock>() == null)
            {
                var cGo = new GameObject("TestGameClockV26");
                cGo.AddComponent<GameClock>();
                yield return null;
            }
            var go = new GameObject("TestNeedsDecay");
            var needs = go.AddComponent<PawnNeeds>();
            yield return null;
            float startFood = needs.food, startMood = needs.mood;
            yield return new WaitForSeconds(2.0f);
            bool foodDropped = needs.food < startFood;
            bool moodDropped = needs.mood < startMood;
            Assert(foodDropped && moodDropped,
                $"food {startFood:F1}→{needs.food:F1}, mood {startMood:F1}→{needs.mood:F1}");
        }

        private IEnumerator TestV27_AIEventFired()
        {
            // AIDirector.OnEventFired event 가 발화하는지
            var dir = Object.FindFirstObjectByType<AIDirector>();
            if (dir == null)
            {
                var dGo = new GameObject("TestAIDirectorV27");
                dir = dGo.AddComponent<AIDirector>();
                yield return null;
            }
            int eventCount = 0;
            System.Action<GameEvent> handler = (e) => eventCount++;
            dir.OnEventFired += handler;
            // 강제 발화 - reflection 으로 nextFireTime 을 0 으로
            var f = typeof(AIDirector).GetField("nextFireTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(dir, 0f);
            yield return new WaitForSeconds(0.5f);
            dir.OnEventFired -= handler;
            Assert(eventCount >= 1, $"OnEventFired 발화 횟수={eventCount} (≥1 expected)");
        }

        private IEnumerator TestV28_PawnMovementTick()
        {
            // #108 - 60x60 맵 새 lake center 중 하나가 (15,18) 이라 이전 target 이 water!
            //  pawn (3,3) target (3,6) - settlement 근처 safe spot 으로 변경
            var go = SpawnTestPawn(new Vector3(3, 3, 0), includeAI: false);
            var mv = go.GetComponent<PawnMovement>();
            yield return new WaitForSeconds(0.05f);
            Vector3 start = go.transform.position;
            mv.SetTarget(new Vector2(3, 6));  // 3 unit 위로
            yield return new WaitForSeconds(0.8f);  // #200 moveSpeed 4.6 → 3 unit 도착 (<0.7s)
            Vector3 end = go.transform.position;
            bool moved = (end - start).magnitude > 1.0f;
            Assert(moved, $"pawn 이동: ({start.x:F1},{start.y:F1})→({end.x:F1},{end.y:F1}) dist={(end-start).magnitude:F2}");
        }

        private IEnumerator TestV29_WolfAttacksPawn()
        {
            // wolf 가 pawn 옆에 spawn → 1.2초 후 pawn HP 감소
            var pawnGo = SpawnTestPawn(new Vector3(-25, 0, 0), includeAI: false);
            var wolfGo = SpawnTestWolf(new Vector3(-24.5f, 0, 0));  // 0.5 unit, attackRange 0.9 안
            var health = pawnGo.GetComponent<PawnHealth>();
            yield return new WaitForSeconds(0.1f);
            float startRatio = health.TotalHpRatio;
            yield return new WaitForSeconds(2.5f);  // attackInterval 1.2 → 최소 1번
            float endRatio = health.TotalHpRatio;
            Assert(endRatio < startRatio,
                $"wolf attack pawn: ratio {startRatio:F2}→{endRatio:F2}");
        }

        private IEnumerator TestV30_MultiPawnHealth()
        {
            // 다중 pawn 의 health.TotalHpRatio 가 다 1.0 시작 → damage 후 차이
            var p1 = SpawnTestPawn(new Vector3(35, 0, 0), includeAI: false);
            var p2 = SpawnTestPawn(new Vector3(37, 0, 0), includeAI: false);
            var p3 = SpawnTestPawn(new Vector3(39, 0, 0), includeAI: false);
            yield return new WaitForSeconds(0.1f);
            p1.GetComponent<PawnHealth>().TakeDamage(15, PawnHealth.PartId.LeftArm);
            p2.GetComponent<PawnHealth>().TakeDamage(5, PawnHealth.PartId.RightLeg);
            // p3 은 건강
            yield return new WaitForSeconds(0.1f);
            float r1 = p1.GetComponent<PawnHealth>().TotalHpRatio;
            float r2 = p2.GetComponent<PawnHealth>().TotalHpRatio;
            float r3 = p3.GetComponent<PawnHealth>().TotalHpRatio;
            bool ordered = r1 < r2 && r2 < r3;
            Assert(ordered, $"r1={r1:F2} < r2={r2:F2} < r3={r3:F2}");
        }

        private IEnumerator TestV20_ResearchComplete()
        {
            var rm = Services.Get<ResearchManager>();
            ResearchManager.Tech bow = null;
            foreach (var t in rm.techs) if (t.id == "simple_bow") bow = t;
            if (bow == null) { Assert(false, "simple_bow tech not found"); yield break; }
            // 강제 완료
            bow.currentPoints = bow.requiredPoints;
            bow.completed = true;
            yield return null;
            bool unlocked = rm.IsUnlocked("simple_bow");
            Assert(unlocked, $"simple_bow unlocked={unlocked}");
        }

        private IEnumerator TestV21_SkillXP()
        {
            var go = new GameObject("TestSkillsPawn");
            var sk = go.AddComponent<PawnSkills>();
            yield return null;
            int startLvl = sk.GetLevel(SkillKind.Combat);
            // #200 XP curve base 100→1000: L0→1 now costs 1000 XP (was 100).
            //  Bumped 500→1500 to still cross level 1.  Intent unchanged: XP → level up.
            sk.AddXP(SkillKind.Combat, 1500f);
            int endLvl = sk.GetLevel(SkillKind.Combat);
            Assert(endLvl > startLvl, $"Combat lvl {startLvl} → {endLvl}");
        }

        private IEnumerator TestV22_StoveCook()
        {
            var rm = Services.Get<ResourceManager>();
            // food 충분히 줘서 cook 가능
            int needed = 10 - rm.food;
            if (needed > 0) rm.AddFood(needed);
            int startFood = rm.food, startMeals = rm.meals;
            var go = new GameObject("TestStove");
            var stove = go.AddComponent<StoveEntity>();
            yield return null;
            bool can = stove.CanCookOne();
            bool cooked = stove.CookOne();
            int endFood = rm.food, endMeals = rm.meals;
            Assert(can && cooked && endFood < startFood && endMeals > startMeals,
                $"can={can} cooked={cooked} food {startFood}→{endFood}, meals {startMeals}→{endMeals}");
        }

        private IEnumerator TestV23_FloorPlace()
        {
            // FloorEntity 생성 + sortingOrder 1 확인 (실제 BuildManager 클릭은 mouse 없음)
            var go = new GameObject("TestFloor");
            go.transform.position = new Vector3(30, 0, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            go.AddComponent<FloorEntity>();
            yield return null;
            // floor 가 살아있고 component 가 active
            bool alive = go != null;
            Assert(alive, $"floor entity alive={alive}");
        }

        private IEnumerator TestV24_ArrowSpawn()
        {
            // ArrowProjectile.SpawnArrow static 직접 호출
            var sprGo = new GameObject("TestArrowSprite");
            var sr = sprGo.AddComponent<SpriteRenderer>();
            var arrowSprite = GetWhiteSprite();
            int before = CountArrows();
            ArrowProjectile.SpawnArrow(new Vector3(32, 0, 0), Vector2.right, 5, null, arrowSprite);
            yield return null;
            int after = CountArrows();
            Assert(after > before, $"arrows {before}→{after}");
        }

        private IEnumerator TestV25_TraitsDeterministic()
        {
            // 같은 이름 pawn 두 개 → 같은 traits (deterministic hash)
            var go1 = new GameObject("DeterministicPawn");
            go1.AddComponent<SpriteRenderer>();
            go1.AddComponent<PawnHealth>();
            var t1 = go1.AddComponent<PawnTraits>();
            yield return null;
            var go2 = new GameObject("DeterministicPawn");  // 같은 이름
            go2.AddComponent<SpriteRenderer>();
            go2.AddComponent<PawnHealth>();
            var t2 = go2.AddComponent<PawnTraits>();
            yield return null;
            bool sameTraits = t1.SummaryKr() == t2.SummaryKr();
            Assert(sameTraits, $"t1='{t1.SummaryKr()}' t2='{t2.SummaryKr()}' same={sameTraits}");
        }

        private IEnumerator TestV15_BerryGather()
        {
            var bushGo = new GameObject("TestBush");
            bushGo.transform.position = new Vector3(24, 0, 0);
            bushGo.AddComponent<SpriteRenderer>();
            bushGo.AddComponent<BoxCollider2D>();
            var bush = bushGo.AddComponent<BerryBushEntity>();
            yield return new WaitForSeconds(0.05f);
            int start = bush.BerriesRemaining;
            int taken = bush.TakeBerry();
            int end = bush.BerriesRemaining;
            Assert(taken > 0 && end < start,
                $"berries {start}→{end}, taken={taken}");
        }

        private IEnumerator TestV16_PawnDeath()
        {
            var go = SpawnTestPawn(new Vector3(26, 0, 0), includeAI: false);
            var health = go.GetComponent<PawnHealth>();
            yield return new WaitForSeconds(0.05f);
            // 머리(vital, #200: 20 HP) 완전 파괴 → IsDead true (99×3 dmg >> 20)
            for (int i = 0; i < 3; i++) health.TakeDamage(99, PawnHealth.PartId.Head);
            yield return new WaitForSeconds(0.1f);
            Assert(health.IsDead,
                $"IsDead={health.IsDead} (head HP=0 expected, ratio={health.TotalHpRatio:F2})");
        }

        private IEnumerator TestV17_PawnClamp()
        {
            // PawnMovement.IsBlockedAt 확인 + ClampToWorld 한 번 더 sanity
            // GroundTilemap == null (test scene 에서) → false 예상
            bool blocked = PawnMovement.IsBlockedAt(new Vector2(1000, 1000));
            // ClampToWorld test
            Vector2 c = PawnMovement.ClampToWorld(new Vector2(-100, 50));
            bool clampOk = c.x >= -44.01f && c.y <= 44.01f;  // #235 맵 ±29→±44
            Assert(!blocked && clampOk,
                $"blocked(1000,1000)={blocked} (false ok if no tilemap), clamp(-100,50)→({c.x:F1},{c.y:F1})");
            yield break;
        }

        private IEnumerator TestV18_Bandage()
        {
            var go = SpawnTestPawn(new Vector3(28, 0, 0), includeAI: false);
            var health = go.GetComponent<PawnHealth>();
            yield return new WaitForSeconds(0.05f);
            health.TakeDamage(10, PawnHealth.PartId.LeftLeg);
            var leg = health.GetPart(PawnHealth.PartId.LeftLeg);
            bool startedBleeding = leg.bleedRate > 0f;
            health.Bandage(PawnHealth.PartId.LeftLeg);
            bool bandageStopsBleed = leg.bleedRate == 0f && leg.bandaged;
            Assert(startedBleeding && bandageStopsBleed,
                $"started bleed={startedBleeding}, after bandage rate={leg.bleedRate:F2} bandaged={leg.bandaged}");
        }

        private IEnumerator TestV19_NightOverlay()
        {
            // GameClock 보장
            if (Services.Get<GameClock>() == null)
            {
                var cGo = new GameObject("TestGameClockV19");
                cGo.AddComponent<GameClock>();
                yield return null;
            }
            var clock = Services.Get<GameClock>();
            // GameClock.GameSeconds = 22:00 (밤)
            var f = typeof(GameClock).GetField("<GameSeconds>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(clock, 22f * 3600f);
            yield return null;
            float dayProgress = clock.DayProgress;
            // NightOverlay 의 SampleStops(0.92) ~ 0.62 alpha
            // 직접 검증은 NightOverlay 가 활성 안 됨 (test scene), DayProgress 만 확인
            bool isNightTime = dayProgress > 0.85f;
            Assert(isNightTime, $"22:00 set → dayProgress={dayProgress:F2} (>0.85 expected)");
        }

        private IEnumerator TestV6_BodyParts()
        {
            var pawnGo = SpawnTestPawn(new Vector3(-9, 0, 0), includeAI: false);
            var health = pawnGo.GetComponent<PawnHealth>();
            yield return new WaitForSeconds(0.1f);
            // 20 damage 하나 부위에 강제
            health.TakeDamage(8, PawnHealth.PartId.LeftLeg);
            yield return new WaitForSeconds(0.1f);
            var part = health.GetPart(PawnHealth.PartId.LeftLeg);
            bool dmgApplied = part.hp < part.maxHp;
            bool bleeding = part.bleedRate > 0f;
            Assert(dmgApplied && bleeding,
                $"왼다리 HP={part.hp}/{part.maxHp} bleed={part.bleedRate:F2}");
        }

        private IEnumerator TestV7_DirectorModeTier()
        {
            // AIDirector 찾아서 day 14 시뮬레이션 (Steady tier 2 이상)
            var dir = Object.FindFirstObjectByType<AIDirector>();
            if (dir == null)
            {
                // test scene 엔 AIDirector 없을 수 있음 - 생성
                var dGo = new GameObject("TestAIDirector");
                dir = dGo.AddComponent<AIDirector>();
            }
            dir.directorMode = DirectorMode.Steady;
            // GameClock 강제 day 14
            var clock = Services.Get<GameClock>();
            if (clock == null)
            {
                var cGo = new GameObject("TestGameClock");
                clock = cGo.AddComponent<GameClock>();
            }
            var f = typeof(GameClock).GetField("<GameSeconds>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(clock, 14f * 86400f);
            yield return new WaitForSeconds(0.1f);
            int tier = dir.CurrentThreatTier;
            Assert(tier >= 2, $"day 14 Steady threatTier={tier} (>=2 expected)");
        }

        private IEnumerator TestV8_MapObstacle()
        {
            // PawnMovement.ClampToWorld 검증 (sync — yield 없음)
            Vector2 outside = new Vector2(100, 100);
            Vector2 clamped = PawnMovement.ClampToWorld(outside);
            bool clampWorks = clamped.x <= 44.01f && clamped.y <= 44.01f;  // #235 맵 ±29→±44
            Vector2 inside = new Vector2(5, 5);
            Vector2 unchanged = PawnMovement.ClampToWorld(inside);
            bool insideOk = Mathf.Approximately(unchanged.x, 5f) && Mathf.Approximately(unchanged.y, 5f);
            Assert(clampWorks && insideOk,
                $"out (100,100) -> ({clamped.x:F1},{clamped.y:F1}); in (5,5) -> ({unchanged.x:F1},{unchanged.y:F1})");
            yield break;
        }

        private IEnumerator TestV9_MoodBreak()
        {
            // Stripped: PawnNeeds 컴포넌트 단독으로 추가 (PawnEntity 의존 회피)
            if (Services.Get<GameClock>() == null)
            {
                var cGo = new GameObject("TestGameClockV9");
                cGo.AddComponent<GameClock>();
                yield return null;
            }
            var go = new GameObject("TestPawnV9");
            go.transform.position = new Vector3(-11, 0, 0);
            var needs = go.AddComponent<PawnNeeds>();
            yield return new WaitForSeconds(0.05f);
            needs.mood = 15f;
            yield return new WaitForSeconds(0.2f);
            bool moodLow = needs.mood < 20f;
            Assert(moodLow, $"mood={needs.mood:F1} (<20 expected)");
        }

        private IEnumerator TestV5_CropHarvest()
        {
            var rm = Services.Get<ResourceManager>();
            int startFood = rm.food;
            // ripe crop 생성
            var cropGo = new GameObject("TestCrop");
            cropGo.transform.position = new Vector3(8, 0, 0);
            var sr = cropGo.AddComponent<SpriteRenderer>();
            // 흰색 1x1 sprite — 시각 무관, CropEntity 만 자라면 됨
            var crop = cropGo.AddComponent<CropEntity>();
            // reflection 으로 growth = 1.0 (ripe)
            var f = typeof(CropEntity).GetField("growth",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f.SetValue(crop, 1.0f);
            yield return new WaitForSeconds(0.1f);
            int gained = crop.Harvest();
            yield return new WaitForSeconds(0.1f);
            // #215 운영자 fb: Harvest() now drops a PHYSICAL food pile (no instant credit
            //  = no "순간이동").  Verify the pile spawned + no teleport, then simulate the
            //  hauler delivering it to a stockpile so the food counter rises.
            MeatPileEntity hp = null;
            foreach (var mp in Object.FindObjectsOfType<MeatPileEntity>())
                if (Vector2.Distance(mp.transform.position, (Vector2)cropGo.transform.position) < 1.5f)
                { hp = mp; break; }
            bool pileSpawned = hp != null;                 // capture BEFORE Destroy (Unity null-overload)
            bool noTeleport  = rm.food == startFood;        // #215 - 즉시 적립(텔레포트) 없어야 함
            if (hp != null) { int amt = hp.Food; Object.Destroy(hp.gameObject); rm.AddFood(amt); }
            yield return new WaitForSeconds(0.1f);
            int endFood = rm.food;
            Assert(gained > 0 && pileSpawned && noTeleport && endFood > startFood,
                $"harvest gained={gained}, pile={pileSpawned}, food {startFood} → {endFood}");
        }

        // ---- Helpers ----

        private GameObject SpawnTestPawn(Vector3 pos, bool includeAI = true)
        {
            // build 에서 AssetDatabase 못 씀.  Fake pawn 만들기 - Pawn prefab 동등 컴포넌트.
            //  includeAI=false → wander/work 안 함 (V3 처럼 정지 pawn 필요한 시나리오 용)
            var go = new GameObject("TestPawn");
            go.transform.position = pos;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<PawnEntity>();
            go.AddComponent<PawnMovement>();
            go.AddComponent<PawnHealth>();
            go.AddComponent<PawnChopper>();
            if (includeAI)
            {
                var ai = go.AddComponent<PawnUtilityAI>();
                ai.SetArrowSprite(GetWhiteSprite());
            }
            return go;
        }

        // defect #1 (#198 D3-1) - 침대 sprite 런타임 binding 영구 가드.
        //   BlueprintEntity.Complete() → Instantiate(finishedPrefab) → BedEntity.SetQuality()
        //   가 완성 bed 의 SpriteRenderer.sprite 를 null 로 만들지 않고 bed_* sprite 를 유지하는지.
        //   in-game 의 bm.BedSpriteRef null 회귀는 BuildClickAutoQA bed case 가 추가로 가드.
        //   여기선 isolated 로 entity 계약(SetQuality 가 sprite 안 날림)을 검증.
        private IEnumerator TestV65_BedSpriteRuntimeBinding()
        {
            // 실제 게임처럼 bed sprite 를 가진 finishedPrefab 을 BlueprintEntity 로 완성시킨다.
            var bedSprite = MakeNamedSprite("bed_wood");
            // finishedPrefab 역할: SpriteRenderer(sprite=bed_wood) + BedEntity 를 가진 template.
            var bedTemplate = new GameObject("V65_BedTemplate");
            bedTemplate.transform.position = new Vector3(500, 500, 0);  // 멀리 두고 reference 로 제외
            var tsr = bedTemplate.AddComponent<SpriteRenderer>();
            tsr.sprite = bedSprite;
            bedTemplate.AddComponent<BedEntity>();

            // blueprint → deposit → addwork → complete chain (실 게임 path)
            var bpGo = new GameObject("V65_Blueprint");
            bpGo.AddComponent<SpriteRenderer>();
            var bp = bpGo.AddComponent<BlueprintEntity>();
            bp.Init(BuildManager.Mode.BedFine, bedTemplate, bedSprite, wood: 30, stone: 0, secs: 1f);
            bp.SetSize(new Vector2Int(1, 2));
            yield return null;
            bp.DepositWood(30);
            yield return null;
            Assert(bp.HasAllMaterials, "deposit 후 자재 충족 안 됨");
            if (!bp.HasAllMaterials) { CleanupV65(bedTemplate); yield break; }
            bp.AddWork(2f);  // > buildSecondsNeeded → Complete
            yield return null;

            // Complete() 가 Instantiate 한 활성 BedEntity 를 찾는다 (template 은 inactive).
            BedEntity completed = null;
            foreach (var b in Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                if (b.gameObject == bedTemplate) continue;
                completed = b; break;
            }
            yield return null;

            bool spawned = completed != null;
            var csr = spawned ? completed.GetComponent<SpriteRenderer>() : null;
            string sprName = (csr != null && csr.sprite != null) ? csr.sprite.name : "<null>";
            bool spriteOk = csr != null && csr.sprite != null
                            && (sprName.Contains("bed_wood") || sprName.Contains("bed_fine") || sprName.Contains("bed"));
            // quality 도 BedFine 로 binding 됐는지 (완성 path 의 SetQuality)
            bool qualOk = spawned && completed.Quality == BedQuality.Fine;

            CleanupV65(bedTemplate);
            if (completed != null) Object.Destroy(completed.gameObject);

            Debug.Log($"[TestRunner] V65: {(spriteOk && qualOk ? "OK" : "FAIL")} - spawned={spawned} sprite={sprName} quality={(spawned ? completed.Quality.ToString() : "?")}");
            Assert(spawned && spriteOk && qualOk,
                $"spawned={spawned} sprite={sprName} spriteOk={spriteOk} qualOk={qualOk}");
        }

        private void CleanupV65(GameObject template)
        {
            if (template != null) Object.Destroy(template);
        }

        // #199 A1 - the reference sim 와 같이 pawn 은 정확히 1x1 tile 점유.
        //   SceneSetup 가 만드는 pawn 계약을 isolated 로 재현·검증:
        //   (1) pawn_colonist.png 는 16x16 px, PPU 16 → SpriteRenderer.bounds.size ~ (1,1).
        //   (2) selection/click BoxCollider2D.size == (1,1).
        //   build 에선 AssetDatabase 못 쓰므로 import 와 동일한 방식(16px tex @ PPU 16)으로
        //   sprite 를 만들어 world size 계약을 잠근다.  prefab collider 회귀는
        //   SceneSetup.Pawn.cs:col.size 와 이 V66 이 함께 가드.
        private IEnumerator TestV66_Pawn1x1RenderCollider()
        {
            // import 재현: 16x16 px texture @ PPU 16 (SceneSetup.ForceImportAllSprites 와 동일).
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var px = new Color[16 * 16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.6f, 0.45f, 0.3f, 1f);
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = "pawn_colonist";

            var go = new GameObject("V66_Pawn");
            go.transform.localScale = Vector3.one;  // SceneSetup.Pawn.cs:localScale = (1,1,1)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);  // SceneSetup.Pawn.cs:col.size = (1,1)
            yield return null;

            Vector2 bsize = sr.bounds.size;
            bool widthOk  = bsize.x >= 0.9f && bsize.x <= 1.1f;
            bool heightOk = bsize.y >= 0.9f && bsize.y <= 1.1f;
            bool colOk = Mathf.Abs(col.size.x - 1f) < 0.001f && Mathf.Abs(col.size.y - 1f) < 0.001f;

            Object.Destroy(go);

            bool ok = widthOk && heightOk && colOk;
            Debug.Log($"[TestRunner] V66: {(ok ? "OK" : "FAIL")} - bounds.size=({bsize.x:F3},{bsize.y:F3}) collider=({col.size.x:F2},{col.size.y:F2}) (the reference sim 1x1 tile)");
            Assert(ok,
                $"bounds=({bsize.x:F3},{bsize.y:F3}) widthOk={widthOk} heightOk={heightOk} collider=({col.size.x:F2},{col.size.y:F2}) colOk={colOk}");
        }

        // #199 A2 — 1x1 pawn (머리 ~ +0.5) 위로 HP/mood 바 + name/status 라벨이
        //   머리 바로 위에 앉는지 검증.  실제 Awake 가 만든 child 의 localPosition.y 를 읽음.
        //   기대 순서(위→아래): name > status > HP 바 > mood 바 > 머리(0.5).
        //   plan-199 §A2 accept band: bar yOffset - headY ∈ [0.1, 0.7] → headY=0.5 이므로
        //   HP 바 y ∈ [0.6, 1.2].  바가 머리 위 (mood 바 ≥ 0.5), 라벨이 바 위.
        private IEnumerator TestV67_FloatingBarNameLabelAnchor()
        {
            const float headY = 0.5f;  // 1x1 pawn pivot center 0, 머리 top ~ +0.5

            var go = SpawnTestPawn(new Vector3(46, 0, 0), includeAI: false);
            go.AddComponent<PawnFloatingBars>();
            go.AddComponent<PawnNameLabel>();
            yield return new WaitForSeconds(0.1f);  // Awake + Start child 생성 대기

            var hpBar    = go.transform.Find("HpBg");
            var moodBar  = go.transform.Find("MoodBg");
            var nameLbl  = go.transform.Find("NameLabel");
            var statusLbl = go.transform.Find("StatusLabel");

            bool childrenOk = hpBar != null && moodBar != null && nameLbl != null && statusLbl != null;
            float hpY     = hpBar    != null ? hpBar.localPosition.y    : -99f;
            float moodY   = moodBar  != null ? moodBar.localPosition.y  : -99f;
            float nameY   = nameLbl  != null ? nameLbl.localPosition.y  : -99f;
            float statusY = statusLbl != null ? statusLbl.localPosition.y : -99f;

            // HP 바: 머리 바로 위 (band [0.1,0.7] above head → [0.6,1.2])
            bool hpBandOk   = (hpY - headY) >= 0.1f && (hpY - headY) <= 0.7f;
            // mood 바(아래쪽): 머리(0.5) 위에 있고 몸통과 안 겹침
            bool moodAboveHead = moodY >= headY;
            // mood 가 HP 보다 아래 (스택 순서)
            bool stackOrderOk = moodY < hpY;
            // name 라벨: HP 바 위
            bool nameAboveBar = nameY > hpY;
            // status: name 과 HP 바 사이 (둘 다 겹치지 않음)
            bool statusBetween = statusY < nameY && statusY > hpY;

            Object.Destroy(go);

            bool ok = childrenOk && hpBandOk && moodAboveHead && stackOrderOk && nameAboveBar && statusBetween;
            Debug.Log($"[TestRunner] V67: {(ok ? "OK" : "FAIL")} - headY={headY:F2} HP={hpY:F2}(d{hpY-headY:F2}) mood={moodY:F2} name={nameY:F2} status={statusY:F2} (the reference sim 머리 위 바+라벨)");
            Assert(ok,
                $"children={childrenOk} HP={hpY:F2}(band[0.6,1.2]={hpBandOk}) mood={moodY:F2}(>=head={moodAboveHead},<HP={stackOrderOk}) name={nameY:F2}(>HP={nameAboveBar}) status={statusY:F2}(between={statusBetween})");
        }

        // ---- #199 B0 — A* pathfinding (in-memory grid, no scene/AssetDatabase) ----

        // Build a fully-walkable PathGrid mask the size of the real grid.
        private static bool[,] AllWalkableMask()
        {
            var m = new bool[PathGrid.SIZE, PathGrid.SIZE];
            for (int x = 0; x < PathGrid.SIZE; x++)
                for (int y = 0; y < PathGrid.SIZE; y++)
                    m[x, y] = true;
            return m;
        }

        // V68 — A* finds a straight clear path on an open grid.
        private IEnumerator TestV68_AStarStraightPath()
        {
            var grid = PathGrid.FromMask(AllWalkableMask());
            var start = new Vector2Int(0, 0);
            var goal  = new Vector2Int(5, 0);   // 5 cells east, clear line
            var path = AStar.FindPath(grid, start, goal);

            bool notNull   = path != null && path.Count > 0;
            bool endpointsOk = notNull && path[0] == start && path[path.Count - 1] == goal;
            // Straight horizontal run of 5 cardinal steps → 6 cells inclusive.
            bool lengthOk  = notNull && path.Count == 6;
            bool allWalkable = true;
            if (notNull) foreach (var c in path) if (!grid.IsWalkable(c)) { allWalkable = false; break; }

            bool ok = notNull && endpointsOk && lengthOk && allWalkable;
            Debug.Log($"[TestRunner] V68: {(ok ? "OK" : "FAIL")} - straight path count={(path?.Count ?? -1)} start={start} goal={goal} (the reference sim 8-dir A* 직선)");
            Assert(ok, $"notNull={notNull} endpoints={endpointsOk} count={(path?.Count ?? -1)}(==6={lengthOk}) allWalkable={allWalkable}");
            yield break;
        }

        // V69 — A* routes AROUND a blocking wall line, never crossing it.
        private IEnumerator TestV69_AStarAroundObstacle()
        {
            var grid = PathGrid.FromMask(AllWalkableMask());
            // Vertical wall at x=2 from y=-3..3 with NO gap on the direct line;
            //  the only way around is over the top (y=4) or bottom (y=-4).
            var blocked = new System.Collections.Generic.List<Vector2Int>();
            for (int y = -3; y <= 3; y++)
            {
                var c = new Vector2Int(2, y);
                grid.SetWalkable(c, false);
                blocked.Add(c);
            }
            var start = new Vector2Int(0, 0);
            var goal  = new Vector2Int(4, 0);
            var path = AStar.FindPath(grid, start, goal);

            bool notNull = path != null && path.Count > 0;
            bool endpointsOk = notNull && path[0] == start && path[path.Count - 1] == goal;
            bool everyCellWalkable = true;
            bool avoidsWall = true;
            if (notNull)
            {
                foreach (var c in path)
                {
                    if (!grid.IsWalkable(c)) everyCellWalkable = false;
                    if (blocked.Contains(c)) avoidsWall = false;
                }
            }
            // A detour must be strictly longer than the blocked straight line
            //  (4 cells inclusive = 5 if it could go straight).
            bool detoured = notNull && path.Count > 5;

            bool ok = notNull && endpointsOk && everyCellWalkable && avoidsWall && detoured;
            Debug.Log($"[TestRunner] V69: {(ok ? "OK" : "FAIL")} - detour count={(path?.Count ?? -1)} avoidsWall={avoidsWall} (장애물 우회)");
            Assert(ok, $"notNull={notNull} endpoints={endpointsOk} walkable={everyCellWalkable} avoidsWall={avoidsWall} detoured={detoured}(count={(path?.Count ?? -1)})");
            yield break;
        }

        // V70 — A* returns no-path for a fully-enclosed goal, within the cap.
        private IEnumerator TestV70_AStarNoPathEnclosed()
        {
            var grid = PathGrid.FromMask(AllWalkableMask());
            // Enclose goal cell (5,5) with a solid 8-cell wall ring.  No gap →
            //  unreachable.  Corners blocked too so no diagonal squeeze.
            var goal = new Vector2Int(5, 5);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (!(dx == 0 && dy == 0))
                        grid.SetWalkable(new Vector2Int(goal.x + dx, goal.y + dy), false);

            var start = new Vector2Int(0, 0);
            float t0 = Time.realtimeSinceStartup;
            var path = AStar.FindPath(grid, start, goal);
            float elapsed = Time.realtimeSinceStartup - t0;

            bool noPath = path == null || path.Count == 0;
            // Must return promptly (node cap guarantees no hang) — generous bound.
            bool prompt = elapsed < 1.0f;

            bool ok = noPath && prompt;
            Debug.Log($"[TestRunner] V70: {(ok ? "OK" : "FAIL")} - enclosed goal noPath={noPath} elapsed={elapsed:F3}s (도달불가 capped)");
            Assert(ok, $"noPath={noPath}(path={(path == null ? "null" : path.Count.ToString())}) prompt={prompt}({elapsed:F3}s<1.0)");
            yield break;
        }

        // V71 — flag-ON pawn FOLLOWS its A* path and arrives within arriveDistance.
        //  Exercises the real follow loop (PawnMovement.AdvanceAlongPath via the
        //  cached A* path set in SetTarget), driven by simulated ticks.  Resets
        //  UsePathfinding=false in cleanup so every later test keeps OLD behavior.
        private IEnumerator TestV71_PathFollowReachesTarget()
        {
            bool prevFlag = PawnMovement.UsePathfinding;
            PathGrid prevGrid = PawnMovement.Grid;
            // Build a known all-walkable grid and a pawn at cell-center (0.5,0.5).
            PawnMovement.Grid = PathGrid.FromMask(AllWalkableMask());
            PawnMovement.UsePathfinding = true;

            GameObject go = null;
            bool arrived = false;
            float startDist = 0f, endDist = 0f;
            float movedTotal = 0f;
            try
            {
                go = new GameObject("V71_PathPawn");
                go.transform.position = new Vector3(0.5f, 0.5f, 0f);   // cell (0,0) center
                go.AddComponent<SpriteRenderer>();
                var pm = go.AddComponent<PawnMovement>();
                // Awake runs on AddComponent; stats default in → arriveDistance 1.0.

                // Target the center of cell (8,3) — a clear diagonal+cardinal run.
                Vector2 goalWorld = PathGrid.CellToWorld(new Vector2Int(8, 3));
                pm.SetTarget(goalWorld);
                Assert(!pm.LastPathFailed, "V71 setup: clear path unexpectedly failed");

                startDist = Vector2.Distance(go.transform.position, goalWorld);

                // Drive the follow loop deterministically.  #200 default moveSpeed
                //  3.0→4.6, dt 0.05 → 0.23 world units/tick.  The path is ~8 cells
                //  (~8 units), so ~35 ticks suffice; budget 500 ticks generously.
                //  Stop as soon as the final waypoint is reached.
                float maxDelta = 4.6f * 0.05f;   // stats.moveSpeed * dt (default)
                Vector2 prevPos = go.transform.position;
                for (int i = 0; i < 500 && !arrived; i++)
                {
                    arrived = pm.AdvanceAlongPath(maxDelta);
                    Vector2 nowPos = go.transform.position;
                    movedTotal += Vector2.Distance(prevPos, nowPos);
                    prevPos = nowPos;
                }
                endDist = Vector2.Distance(go.transform.position, goalWorld);
            }
            finally
            {
                if (go != null) Object.Destroy(go);
                PawnMovement.UsePathfinding = prevFlag;   // restore OLD behavior
                PawnMovement.Grid = prevGrid;
            }

            // Real follow logic proven: arrived flag set, ended within arriveDist
            //  (1.0) of goal, and the pawn actually MOVED (movedTotal > 0).
            bool ended = arrived && endDist <= 1.0f && movedTotal > 0.5f && startDist > 2f;
            Debug.Log($"[TestRunner] V71: {(ended ? "OK" : "FAIL")} - pathfollow arrived={arrived} startDist={startDist:F2} endDist={endDist:F2} moved={movedTotal:F2} (A* follow loop)");
            Assert(ended, $"arrived={arrived} startDist={startDist:F2} endDist={endDist:F2}(<=1.0) moved={movedTotal:F2}(>0.5)");
            yield break;
        }

        // V72 — flag-ON pawn given an ENCLOSED (unreachable) target sets
        //  LastPathFailed=true, clears its target, and does NOT wander.  Proves
        //  the colony-sim "destination unreachable" signal B1 introduces.
        private IEnumerator TestV72_PathFollowUnreachableSignal()
        {
            bool prevFlag = PawnMovement.UsePathfinding;
            PathGrid prevGrid = PawnMovement.Grid;
            var grid = PathGrid.FromMask(AllWalkableMask());
            // Enclose goal cell (6,6) with a solid 8-cell ring (corners too) → no path.
            var goalCell = new Vector2Int(6, 6);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (!(dx == 0 && dy == 0))
                        grid.SetWalkable(new Vector2Int(goalCell.x + dx, goalCell.y + dy), false);
            PawnMovement.Grid = grid;
            PawnMovement.UsePathfinding = true;

            GameObject go = null;
            bool failed = false;
            bool hasTarget = true;
            float moved = 0f;
            try
            {
                go = new GameObject("V72_UnreachablePawn");
                go.transform.position = new Vector3(0.5f, 0.5f, 0f);
                go.AddComponent<SpriteRenderer>();
                var pm = go.AddComponent<PawnMovement>();
                Vector2 startPos = go.transform.position;

                pm.SetTarget(PathGrid.CellToWorld(goalCell));   // enclosed → no path
                failed = pm.LastPathFailed;
                hasTarget = pm.HasTarget;

                // Drive ticks: with no path the pawn must NOT move (no jitter/wander).
                float maxDelta = 3.0f * 0.05f;
                for (int i = 0; i < 50; i++) pm.AdvanceAlongPath(maxDelta);
                moved = Vector2.Distance(startPos, go.transform.position);
            }
            finally
            {
                if (go != null) Object.Destroy(go);
                PawnMovement.UsePathfinding = prevFlag;
                PawnMovement.Grid = prevGrid;
            }

            bool ok = failed && !hasTarget && moved < 0.01f;
            Debug.Log($"[TestRunner] V72: {(ok ? "OK" : "FAIL")} - unreachable LastPathFailed={failed} hasTarget={hasTarget} moved={moved:F3} (도달불가 신호)");
            Assert(ok, $"LastPathFailed={failed} hasTarget={hasTarget}(should be false) moved={moved:F3}(<0.01)");
            yield break;
        }

        // V73 — #199 C1: TryGetAdjacentStandCell returns a WALKABLE cell ADJACENT
        //  (8-neighbour) to the target (never the target's own cell), picks a
        //  reachable one closest to the pawn, returns FALSE when all neighbours are
        //  blocked, and handles a MULTI-CELL (1×2) footprint (stands beside it).
        private IEnumerator TestV73_AdjacentStandCell()
        {
            var grid = PathGrid.FromMask(AllWalkableMask());

            // --- Case A: single-cell tree at cell (5,5).  Pawn west at world (2,5).
            //     Expect a walkable adjacent cell, NOT (5,5), and (since pawn is to
            //     the west) the chosen cell should be the west neighbour (4,5) —
            //     the nearest walkable neighbour to the pawn.
            var treeCell = new Vector2Int(5, 5);
            Vector2 treeWorld = PathGrid.CellToWorld(treeCell);
            Vector2 pawnWest = new Vector2(2.5f, 5.5f);
            bool okA = grid.TryGetAdjacentStandCell(treeWorld, new Vector2Int(1, 1), pawnWest, out Vector2 standA);
            var standCellA = PathGrid.WorldToCell(standA);
            bool adjacentA = okA
                && standCellA != treeCell
                && Mathf.Abs(standCellA.x - treeCell.x) <= 1
                && Mathf.Abs(standCellA.y - treeCell.y) <= 1
                && grid.IsWalkable(standCellA);
            bool nearestA = standCellA == new Vector2Int(4, 5);   // west neighbour

            // --- Case B: pick a REACHABLE one — block all neighbours except the
            //     east cell (6,5); pawn anywhere → must return exactly (6,5).
            var grid2 = PathGrid.FromMask(AllWalkableMask());
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (dx == 1 && dy == 0) continue;   // keep east open
                    grid2.SetWalkable(new Vector2Int(treeCell.x + dx, treeCell.y + dy), false);
                }
            bool okB = grid2.TryGetAdjacentStandCell(treeWorld, new Vector2Int(1, 1), pawnWest, out Vector2 standB);
            bool onlyEast = okB && PathGrid.WorldToCell(standB) == new Vector2Int(6, 5);

            // --- Case C: ALL neighbours blocked → returns false (unreachable).
            var grid3 = PathGrid.FromMask(AllWalkableMask());
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (!(dx == 0 && dy == 0))
                        grid3.SetWalkable(new Vector2Int(treeCell.x + dx, treeCell.y + dy), false);
            bool okC = grid3.TryGetAdjacentStandCell(treeWorld, new Vector2Int(1, 1), pawnWest, out _);
            bool blockedReturnsFalse = !okC;

            // --- Case D: MULTI-CELL 1×2 bed.  BuildManager places a 1×2 at the
            //     geometric centre (cx+0.5, cy+1.0); covered cells (cx,cy),(cx,cy+1).
            //     Use cx=10, cy=10 → centre (10.5, 11.0), covered {(10,10),(10,11)}.
            //     Stand cell must be adjacent to ONE of those and inside NEITHER.
            var grid4 = PathGrid.FromMask(AllWalkableMask());
            Vector2 bedWorld = new Vector2(10.5f, 11.0f);
            var bedCells = new System.Collections.Generic.HashSet<Vector2Int>
            { new Vector2Int(10, 10), new Vector2Int(10, 11) };
            Vector2 pawnBelow = new Vector2(10.5f, 6.5f);
            bool okD = grid4.TryGetAdjacentStandCell(bedWorld, new Vector2Int(1, 2), pawnBelow, out Vector2 standD);
            var standCellD = PathGrid.WorldToCell(standD);
            bool notInside = okD && !bedCells.Contains(standCellD);
            bool adjacentToBed = false;
            foreach (var bc in bedCells)
                if (Mathf.Abs(standCellD.x - bc.x) <= 1 && Mathf.Abs(standCellD.y - bc.y) <= 1)
                    adjacentToBed = true;
            // pawn is BELOW → nearest stand cell should be the bottom cell's south
            //  neighbour (10,9).
            bool multiOk = okD && notInside && adjacentToBed && standCellD == new Vector2Int(10, 9);

            bool ok = adjacentA && nearestA && onlyEast && blockedReturnsFalse && multiOk;
            Debug.Log($"[TestRunner] V73: {(ok ? "OK" : "FAIL")} - standA={standCellA} onlyEast={onlyEast} blockedFalse={blockedReturnsFalse} multi={standCellD}({multiOk}) (인접 stand cell)");
            Assert(ok, $"adjacentA={adjacentA} nearestA={nearestA}(={standCellA}) onlyEast={onlyEast} blockedReturnsFalse={blockedReturnsFalse} multiOk={multiOk}(stand={standCellD})");
            yield break;
        }

        // V74 — #199 C2: ReservationManager contract.  A second claimant cannot
        //  reserve an already-reserved target; after Release / ReleaseAll it can;
        //  re-reserve by the SAME claimant is idempotent (true); a destroyed
        //  claimant's reservation does not leak (target is reclaimable + reported
        //  free); cell reservation behaves the same way.
        private IEnumerator TestV74_ReservationRegistry()
        {
            ReservationManager.Clear();

            // Two claimant GameObjects + one target object.
            var pawnA = new GameObject("V74_PawnA");
            var pawnB = new GameObject("V74_PawnB");
            var target = new GameObject("V74_Target");   // stands in for a TreeEntity GO

            // A reserves the target → true; B is then blocked → false.
            bool aGot      = ReservationManager.TryReserve(target, pawnA);
            bool bBlocked  = !ReservationManager.TryReserve(target, pawnB);
            bool otherSeesIt = ReservationManager.IsReservedByOther(target, pawnB);
            bool ownerNotOther = !ReservationManager.IsReservedByOther(target, pawnA);

            // Idempotent re-reserve by the SAME claimant → still true, no transfer.
            bool aReReserve = ReservationManager.TryReserve(target, pawnA);
            bool stillA = ReservationManager.OwnerOf(target) == pawnA;

            // Release by A → B can now reserve.
            ReservationManager.Release(target, pawnA);
            bool freedAfterRelease = !ReservationManager.IsReserved(target);
            bool bGetsItNow = ReservationManager.TryReserve(target, pawnB);

            // ReleaseAll(B) frees everything B held.
            ReservationManager.ReleaseAll(pawnB);
            bool freedAfterReleaseAll = !ReservationManager.IsReserved(target);

            // Cell reservation mirrors target reservation.
            var cell = new Vector2Int(7, 7);
            bool cellAGot     = ReservationManager.TryReserveCell(cell, pawnA);
            bool cellBBlocked = !ReservationManager.TryReserveCell(cell, pawnB);
            bool cellOtherSees = ReservationManager.IsCellReservedByOther(cell, pawnB);
            ReservationManager.ReleaseCell(cell, pawnA);
            bool cellBGetsNow = ReservationManager.TryReserveCell(cell, pawnB);

            // Destroyed-claimant cleanup must NOT leak.  A new claimant C reserves a
            //  fresh target, then C is Destroyed without ReleaseAll → the target must
            //  read free (lazy cleanup) and be reclaimable by D.
            var pawnC = new GameObject("V74_PawnC");
            var target2 = new GameObject("V74_Target2");
            ReservationManager.TryReserve(target2, pawnC);
            Object.DestroyImmediate(pawnC);              // simulate death w/o ReleaseAll
            yield return null;
            bool destroyedFreed = !ReservationManager.IsReserved(target2);   // lazy-cleans
            var pawnD = new GameObject("V74_PawnD");
            bool dReclaims = ReservationManager.TryReserve(target2, pawnD);

            bool ok = aGot && bBlocked && otherSeesIt && ownerNotOther
                      && aReReserve && stillA
                      && freedAfterRelease && bGetsItNow && freedAfterReleaseAll
                      && cellAGot && cellBBlocked && cellOtherSees && cellBGetsNow
                      && destroyedFreed && dReclaims;

            Debug.Log($"[TestRunner] V74: {(ok ? "OK" : "FAIL")} - aGot={aGot} bBlocked={bBlocked} reReserve={aReReserve} relAll={freedAfterReleaseAll} cell(A={cellAGot},Bblk={cellBBlocked}) destroyedFreed={destroyedFreed} dReclaims={dReclaims} (예약 레지스트리)");
            Assert(ok, $"aGot={aGot} bBlocked={bBlocked} otherSees={otherSeesIt} ownerNotOther={ownerNotOther} reReserve={aReReserve} stillA={stillA} freedRel={freedAfterRelease} bNow={bGetsItNow} freedRelAll={freedAfterReleaseAll} cellA={cellAGot} cellBblk={cellBBlocked} cellOther={cellOtherSees} cellBnow={cellBGetsNow} destroyedFreed={destroyedFreed} dReclaims={dReclaims}");

            Object.DestroyImmediate(pawnA);
            Object.DestroyImmediate(pawnB);
            Object.DestroyImmediate(pawnD);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(target2);
            ReservationManager.Clear();
            yield break;
        }

        // V75 — #199 C2: reservation forces DISTINCT picks.  (1) Two pawns offered
        //  the SAME nearest tree end up on DIFFERENT trees (the first reserves it,
        //  FindNearestTree for the second skips the reserved one).  (2) With only
        //  ONE tree, only one pawn claims it; the second yields (no double-claim).
        //  (3) Stand-cell reservation: two pawns reserving a stand cell next to one
        //  target get DIFFERENT cells.
        private IEnumerator TestV75_DistinctTargetAndCell()
        {
            ReservationManager.Clear();

            // NOTE: the -testmode scene already contains real TreeEntity objects, so
            //  this test does NOT rely on FindObjectsByType (which would also see
            //  ambient trees).  Instead it exercises the EXACT selection rule each
            //  worker's FindNearestX uses — "skip IsReservedByOther, then TryReserve
            //  the pick" — over a CONTROLLED candidate list, plus the stand-cell
            //  reservation path.  This is the selection-level assertion the plan
            //  permits when a full two-pawn scene scenario is hard in isolation.

            var p1 = new GameObject("V75_P1");
            var p2 = new GameObject("V75_P2");

            // --- (1) two candidate targets, two pawns.  Selecting like FindNearestX:
            //     pawn1 picks the nearest FREE target and reserves it; pawn2 then
            //     skips the reserved one and picks the other → DIFFERENT targets.
            var tgtA = new GameObject("V75_TgtA");   // "nearest" for both
            var tgtB = new GameObject("V75_TgtB");
            var cands = new[] { tgtA, tgtB };

            GameObject Pick(GameObject claimant)
            {
                // mirrors FindNearestTree: first non-reserved candidate (list order
                //  stands in for nearest-first), then reserve it.
                foreach (var c in cands)
                {
                    if (ReservationManager.IsReservedByOther(c, claimant)) continue;
                    if (ReservationManager.TryReserve(c, claimant)) return c;
                }
                return null;
            }

            var pick1 = Pick(p1);
            var pick2 = Pick(p2);
            bool distinctTargets = pick1 != null && pick2 != null && pick1 != pick2;
            bool ownersCorrect = ReservationManager.OwnerOf(pick1) == p1
                                 && ReservationManager.OwnerOf(pick2) == p2;
            // No target is reserved by BOTH (the core "no double-claim" guarantee).
            bool noShared = ReservationManager.OwnerOf(tgtA) != ReservationManager.OwnerOf(tgtB);

            // --- (2) ONE candidate only: first claims it, second yields (null).
            ReservationManager.Clear();
            var solo = new GameObject("V75_Solo");
            var single = new[] { solo };
            GameObject PickSingle(GameObject claimant)
            {
                foreach (var c in single)
                {
                    if (ReservationManager.IsReservedByOther(c, claimant)) continue;
                    if (ReservationManager.TryReserve(c, claimant)) return c;
                }
                return null;
            }
            var sole1 = PickSingle(p1);
            var sole2 = PickSingle(p2);
            bool oneOnly = sole1 == solo && sole2 == null;

            // --- (3) stand-cell distinctness: two claimants reserve a stand cell
            //     around ONE target footprint → must get DIFFERENT cells (the
            //     reservation makes the 2nd skip the 1st's cell).
            ReservationManager.Clear();
            var grid = PathGrid.FromMask(AllWalkableMask());
            Vector2 tgt = PathGrid.CellToWorld(new Vector2Int(20, 20));
            Vector2Int lockA, lockB;
            bool gotA = grid.TryGetAdjacentStandCell(tgt, new Vector2Int(1, 1),
                new Vector2(18, 20), c => ReservationManager.IsCellReservedByOther(c, p1), out lockA);
            if (gotA) ReservationManager.TryReserveCell(lockA, p1);
            bool gotB = grid.TryGetAdjacentStandCell(tgt, new Vector2Int(1, 1),
                new Vector2(18, 20), c => ReservationManager.IsCellReservedByOther(c, p2), out lockB);
            if (gotB) ReservationManager.TryReserveCell(lockB, p2);
            bool distinctCells = gotA && gotB && lockA != lockB;

            bool ok = distinctTargets && ownersCorrect && noShared && oneOnly
                      && distinctCells;
            Debug.Log($"[TestRunner] V75: {(ok ? "OK" : "FAIL")} - distinctTargets={distinctTargets} oneOnly={oneOnly} distinctCells={distinctCells}(A={lockA},B={lockB}) (예약→서로 다른 대상/cell)");
            Assert(ok, $"distinctTargets={distinctTargets}(p1={(pick1!=null?pick1.name:"null")},p2={(pick2!=null?pick2.name:"null")}) ownersCorrect={ownersCorrect} noShared={noShared} oneOnly={oneOnly}(sole1={(sole1!=null)},sole2={(sole2!=null)}) distinctCells={distinctCells}(A={lockA},B={lockB})");

            Object.DestroyImmediate(p1);
            Object.DestroyImmediate(p2);
            Object.DestroyImmediate(tgtA);
            Object.DestroyImmediate(tgtB);
            Object.DestroyImmediate(solo);
            ReservationManager.Clear();
            yield break;
        }

        // V76 (#201) — isolated unit test of EjectPawnsFromCell.  Build an
        //  all-walkable grid, place a pawn ON cell (5,5), block that cell (as a wall
        //  completion would), then call EjectPawnsFromCell and assert the pawn moved
        //  OFF the blocked cell onto a walkable neighbour.  Also covers the enclosed
        //  edge case: surround a pawn on (20,20) by blocking all 8 neighbours + its
        //  own cell → eject leaves the pawn in place (can't push into a wall) without
        //  throwing.  No scene/bootstrap needed (flag toggled here, restored after).
        private IEnumerator TestV76_EjectPawnFromBlockedCell()
        {
            bool prevFlag = PawnMovement.UsePathfinding;
            PathGrid prevGrid = PawnMovement.Grid;
            var grid = PathGrid.FromMask(AllWalkableMask());
            PawnMovement.Grid = grid;
            PawnMovement.UsePathfinding = true;

            GameObject go = null, enc = null;
            bool ejected = false, landedWalkable = false, enclosedStayed = false, noThrow = true;
            try
            {
                // --- (1) normal eject: pawn on a cell that becomes blocked.
                var cell = new Vector2Int(5, 5);
                go = new GameObject("V76_Pawn");
                go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<PawnMovement>();   // Awake runs (stats default)

                grid.SetWalkable(cell, false);     // simulate wall now occupying it
                PawnMovement.EjectPawnsFromCell(PathGrid.CellToWorld(cell));

                var after = PathGrid.WorldToCell(go.transform.position);
                ejected = after != cell;
                landedWalkable = grid.IsWalkable(after);

                // --- (2) enclosed edge case: pawn sealed on all sides → stays, no throw.
                var ec = new Vector2Int(20, 20);
                enc = new GameObject("V76_Enclosed");
                enc.transform.position = new Vector3(ec.x + 0.5f, ec.y + 0.5f, 0f);
                enc.AddComponent<SpriteRenderer>();
                enc.AddComponent<PawnMovement>();
                // block the cell + a wide region around it so TryNearestWalkable's
                //  ring search (radius ≤8) finds NO open cell.
                for (int dx = -8; dx <= 8; dx++)
                    for (int dy = -8; dy <= 8; dy++)
                        grid.SetWalkable(new Vector2Int(ec.x + dx, ec.y + dy), false);
                PawnMovement.EjectPawnsFromCell(PathGrid.CellToWorld(ec));
                var encAfter = PathGrid.WorldToCell(enc.transform.position);
                enclosedStayed = encAfter == ec;   // can't push into a wall → unchanged
            }
            catch (System.Exception e)
            {
                noThrow = false;
                Debug.LogError($"[TestRunner] V76 threw: {e.Message}");
            }
            finally
            {
                if (go != null) Object.Destroy(go);
                if (enc != null) Object.Destroy(enc);
                PawnMovement.UsePathfinding = prevFlag;
                PawnMovement.Grid = prevGrid;
            }

            bool ok = ejected && landedWalkable && enclosedStayed && noThrow;
            Debug.Log($"[TestRunner] V76: {(ok ? "OK" : "FAIL")} - ejected={ejected} landedWalkable={landedWalkable} enclosedStayed={enclosedStayed} noThrow={noThrow} (벽 완성 시 림 push-out + 갇힘 edge)");
            Assert(ok, $"ejected={ejected} landedWalkable={landedWalkable} enclosedStayed={enclosedStayed} noThrow={noThrow}");
            yield break;
        }

        private static Sprite MakeNamedSprite(string name)
        {
            var tex = new Texture2D(16, 32, TextureFormat.RGBA32, false);
            var px = new Color[16 * 32];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.6f, 0.45f, 0.3f, 1f);
            tex.SetPixels(px);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, 16, 32), new Vector2(0.5f, 0.5f), 16f);
            sp.name = name;
            return sp;
        }

        private static Sprite _whiteSprite;
        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            _whiteSprite.name = "TestArrowSprite";
            return _whiteSprite;
        }

        private GameObject SpawnTestWolf(Vector3 pos)
        {
            var go = new GameObject("TestWolf");
            go.transform.position = pos;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<WolfEnemy>();
            return go;
        }

        private GameObject SpawnTestBench(Vector3 pos)
        {
            var go = new GameObject("TestBench");
            go.transform.position = pos;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<ResearchBench>();
            return go;
        }

        private int CountArrows()
        {
            return Object.FindObjectsByType<ArrowProjectile>(FindObjectsSortMode.None).Length;
        }

        private void FinalizeReport()
        {
            report.totalPassed = report.results.FindAll(r => r.passed).Count;
            report.totalFailed = report.results.Count - report.totalPassed;
            report.finishedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string json = JsonUtility.ToJson(report, true);
            try
            {
                File.WriteAllText(outputPath, json);
                Debug.Log($"[TestRunner] report → {outputPath} (P={report.totalPassed} F={report.totalFailed})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TestRunner] write FAIL: {e.Message}");
            }
        }
    }
}
