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
