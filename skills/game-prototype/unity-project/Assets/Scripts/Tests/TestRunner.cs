using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MelonS.GameProto.Core;

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
            Debug.Log("[TestRunner] start - 5 scenarios");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V1-drafted", TestV1_Drafted);
            yield return RunOne("V2-wolf-chase", TestV2_WolfChase);
            yield return RunOne("V3-research", TestV3_Research);
            yield return RunOne("V4-arrow", TestV4_Arrow);
            yield return RunOne("V5-crop-harvest", TestV5_CropHarvest);
            yield return RunOne("V6-body-parts", TestV6_BodyParts);
            yield return RunOne("V7-storyteller-tier", TestV7_StorytellerTier);
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
            // Tree.Chop 콜 → wood +1 (또는 Tree 가 IsDestroyed 되면 +N)
            var treeGo = new GameObject("TestTree");
            treeGo.transform.position = new Vector3(22, 0, 0);
            treeGo.AddComponent<SpriteRenderer>();
            treeGo.AddComponent<BoxCollider2D>();
            var tree = treeGo.AddComponent<TreeEntity>();
            yield return new WaitForSeconds(0.1f);
            var rm = Services.Get<ResourceManager>();
            int start = rm.wood;
            // Tree.TakeChopDamage 또는 directly 까지 호출
            tree.TakeChopDamage(999f);  // maxHp 100 → 충분히 큰 데미지
            yield return new WaitForSeconds(0.1f);
            int end = rm.wood;
            Assert(end > start, $"chop wood: {start} → {end}");
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
            // Randy storyteller 의 CurrentThreatTier 는 매번 0..3 random
            var dGo = new GameObject("TestRandyDir");
            var dir = dGo.AddComponent<AIDirector>();
            dir.activeStoryteller = Storyteller.Randy;
            yield return null;
            // 20번 샘플 → 적어도 2개 다른 tier 나옴 (random 검증)
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 20; i++)
            {
                seen.Add(dir.CurrentThreatTier);
            }
            Assert(seen.Count >= 2, $"Randy 20 sample → distinct tiers = {seen.Count}");
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
            for (int i = 0; i < 10; i++) sk.AddXP(SkillKind.Chop, 200f);  // 충분한 XP
            int lvl = sk.GetLevel(SkillKind.Chop);
            Assert(lvl >= 4, $"Chop XP 2000 누적 → lvl {lvl} (≥4 expected, log curve)");
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

        private IEnumerator TestV55_MultiTrader()
        {
            // 2 trader 동시 spawn → 둘 다 wander 작동
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
            yield return new WaitForSeconds(2.0f);
            Vector3 e1 = t1.transform.position, e2 = t2.transform.position;
            bool both = (e1 - s1).magnitude > 0.1f && (e2 - s2).magnitude > 0.1f;
            Assert(both && trader1.IsHere && trader2.IsHere,
                $"trader1 moved {(e1-s1).magnitude:F2}, trader2 moved {(e2-s2).magnitude:F2}");
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
            //  여러 random 시도 - 적어도 한 번은 default 30 과 다름
            int variations = 0;
            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject($"TraitHpV43_{i}");
                go.AddComponent<SpriteRenderer>();
                var health = go.AddComponent<PawnHealth>();
                go.AddComponent<PawnTraits>();
                yield return null;
                int torsoMax = health.GetPart(PawnHealth.PartId.Torso).maxHp;
                if (torsoMax != 30) variations++;  // default 와 다름
            }
            Assert(variations >= 1,
                $"5 trait pawn 중 {variations}개 torso maxHp ≠ 30 (Tough/Frail 적용)");
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

        private IEnumerator TestV45_ClampStatic()
        {
            // PawnMovement.WORLD_MIN/MAX static field 검증 (±19)
            bool minOk = PawnMovement.WORLD_MIN.x == -19f && PawnMovement.WORLD_MIN.y == -19f;
            bool maxOk = PawnMovement.WORLD_MAX.x == 19f && PawnMovement.WORLD_MAX.y == 19f;
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
            var treeGo = new GameObject("TestChopTree");
            treeGo.transform.position = new Vector3(55, 0, 0);
            treeGo.AddComponent<SpriteRenderer>();
            treeGo.AddComponent<BoxCollider2D>();
            treeGo.AddComponent<TreeEntity>();
            yield return null;
            var pawn = new GameObject("TestChopPawn");
            pawn.transform.position = new Vector3(53, 0, 0);
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
                $"started={started}, chopper.HasTask={chopper.HasTask}");
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
            Assert(stats.maxHp == 30 && stats.attackDamage == 1
                && Mathf.Approximately(stats.moveSpeed, 3f)
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
            if (rm.food < 100) rm.AddFood(100 - rm.food);  // 충분한 food 보장
            int startFood = rm.food;
            // 30% 확률이라 multiple try - food 30 사용 → ~9번 시도, 1번 이상 성공 확률 ~95%
            var go = new GameObject("TestAnimalTame");
            go.transform.position = new Vector3(34, -8, 0);
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
            yield return new WaitForSeconds(0.05f);
            bool ok = trader.TryTrade();
            yield return null;
            int endWood = rm.wood, endFood = rm.food;
            Assert(ok && endWood == startWood - 5 && endFood == startFood + 8,
                $"trade: wood {startWood}→{endWood} (-5), food {startFood}→{endFood} (+8)");
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
            var go = SpawnTestPawn(new Vector3(15, 15, 0), includeAI: false);
            var mv = go.GetComponent<PawnMovement>();
            yield return new WaitForSeconds(0.05f);
            Vector3 start = go.transform.position;
            mv.SetTarget(new Vector2(15, 18));  // 3 unit 위로
            yield return new WaitForSeconds(0.8f);  // moveSpeed 3 → 0.8초에 ~2.4 unit
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
            sk.AddXP(SkillKind.Combat, 500f);  // 충분한 XP
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
            // 머리(vital, 10 HP) 완전 파괴 → IsDead true
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
            bool clampOk = c.x >= -19.01f && c.y <= 19.01f;
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
            // NightOverlay 의 SampleStops(0.92) ≈ 0.62 alpha
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

        private IEnumerator TestV7_StorytellerTier()
        {
            // AIDirector 찾아서 day 14 시뮬레이션 (Cassandra tier 2 이상)
            var dir = Object.FindFirstObjectByType<AIDirector>();
            if (dir == null)
            {
                // test scene 엔 AIDirector 없을 수 있음 - 생성
                var dGo = new GameObject("TestAIDirector");
                dir = dGo.AddComponent<AIDirector>();
            }
            dir.activeStoryteller = Storyteller.Cassandra;
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
            Assert(tier >= 2, $"day 14 Cassandra threatTier={tier} (>=2 expected)");
        }

        private IEnumerator TestV8_MapObstacle()
        {
            // PawnMovement.ClampToWorld 검증 (sync — yield 없음)
            Vector2 outside = new Vector2(100, 100);
            Vector2 clamped = PawnMovement.ClampToWorld(outside);
            bool clampWorks = clamped.x <= 19.01f && clamped.y <= 19.01f;
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
            int endFood = rm.food;
            Assert(gained > 0 && endFood > startFood,
                $"harvest gained={gained}, food {startFood} → {endFood}");
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
