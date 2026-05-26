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
