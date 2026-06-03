using System.Collections.Generic;
using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 53 — Research tree singleton.
    /// 5 vanilla-style techs with prerequisites and Korean labels.
    /// A pawn standing within range of a ResearchBench accumulates research
    /// points at base_rate * intelligenceMul (1.0 for now — could tie to Skill).
    /// Player picks active tech via Research UI; points flow into it until completed.
    /// On completion, fires OnTechCompleted event so other systems can unlock features.
    /// </summary>
    public class ResearchManager : MonoBehaviour
    {
        public static ResearchManager Instance => Services.Get<ResearchManager>();  // R6

        [System.Serializable]
        public class Tech
        {
            public string id;
            public string nameKr;
            public string descKr;
            public int requiredPoints;
            public int currentPoints;
            public bool completed;
            public string[] prerequisites;  // tech ids
            public Tech(string id, string nameKr, string desc, int points, params string[] prereq)
            {
                this.id = id;
                this.nameKr = nameKr;
                this.descKr = desc;
                this.requiredPoints = points;
                this.currentPoints = 0;
                this.completed = false;
                this.prerequisites = prereq;
            }
        }

        public List<Tech> techs = new List<Tech>();
        public Tech activeTech;
        public float pointsPerSecondPerBench = 2f;
        private float pointsFraction = 0f;  // Day 75 fractional accumulator

        // Lesson #4 firewall - FindObjectsByType per-Update 비쌈.  cache + 1s 재검색.
        private ResearchBench[] cachedBenches;
        private float nextBenchSearchTime = -10f;
        private const float BenchSearchInterval = 1.0f;

        public delegate void TechCompletedHandler(Tech t);
        public event TechCompletedHandler OnTechCompleted;

        private void Awake()
        {
            if (Services.Has<ResearchManager>() && Services.Get<ResearchManager>() != this)
            { Destroy(gameObject); return; }
            Services.Register<ResearchManager>(this);
            BuildTree();
            // Day 75: 첫 tech 자동 활성화 — 플레이어가 N키 누르기 전에도 진행이 보임.
            if (techs.Count > 0) activeTech = techs[0];
        }

        private void BuildTree()
        {
            // Tier 1 (no prerequisites)
            // #200 truth-in-UI fix: description said "12 dmg 화살" but pawns actually
            //  shoot 3-5 dmg arrows (PawnUtilityAI rDmg), tuned to the small enemy HP
            //  pools (deer 12 / rabbit 3 / wolf 18).  Raising arrow dmg to 12 would
            //  one-shot most animals — an unrealistic combat rewrite the operator
            //  forbade.  So the honest fix is the description, not the damage.
            techs.Add(new Tech("simple_bow",     "원시 활",       "원거리 사냥/방어 가능. 3~5 dmg 화살.", 100));
            techs.Add(new Tech("stone_walls",    "석재 벽 건설",  "나무 벽보다 4배 튼튼 (HP 200).",     150));
            techs.Add(new Tech("better_stove",   "개선된 화덕",   "조리 속도 2배, 식사 mood +5.",       120));
            // #범위정합: 전기(electricity)·태양광(solar_panel) tech 제거 — README Out-of-scope
            //  (Power grid)라 발전기/배터리/전선 등 해금 대상이 코드에 전혀 없어, 연구해도 아무
            //  변화 없는 dead tech 였음(자동-연구가 빈 tech 를 갈게 됨).  배선 가능한 날 재도입.
        }

        private void Update()
        {
            if (activeTech == null || activeTech.completed) return;
            // Count benches with at least one pawn within research-radius
            //  Lesson #4 firewall - bench list 1s 캐시 (이전 매 frame FindObjects 호출 비쌈).
            if (cachedBenches == null || Time.time >= nextBenchSearchTime)
            {
                cachedBenches = GameObject.FindObjectsByType<ResearchBench>(FindObjectsSortMode.None);
                nextBenchSearchTime = Time.time + BenchSearchInterval;
            }
            ResearchBench[] benches = cachedBenches;
            if (benches == null || benches.Length == 0) return;
            // #169 - 이전: bench 별 0/1 active 카운트.  지금: pawn.manipulation 합계로
            //  wiki: Intellectual skill 영향 (lvl 0 = 0.13x, lvl 20 = 1.83x) 의 단순화.
            //  여러 bench/pawn 동시 연구 시 sum 으로 가산.
            float speedMul = 0f;
            foreach (var b in benches)
            {
                if (b == null) continue;
                speedMul += b.ResearcherSpeedSum();
            }
            if (speedMul <= 0.001f) return;
            float gain = pointsPerSecondPerBench * speedMul * Time.deltaTime;
            pointsFraction += gain;
            int whole = Mathf.FloorToInt(pointsFraction);
            if (whole > 0)
            {
                activeTech.currentPoints += whole;
                pointsFraction -= whole;
            }
            if (activeTech.currentPoints >= activeTech.requiredPoints)
            {
                activeTech.currentPoints = activeTech.requiredPoints;
                activeTech.completed = true;
                Debug.Log($"[Research] 완료: {activeTech.nameKr}");
                if (OnTechCompleted != null) OnTechCompleted(activeTech);
                // #루프 완료 후 다음 연구 자동선택 — 안 하면 activeTech=null 로 연구가 멈춰
                //  플레이어가 N키로 수동 선택할 때까지 진행 정지(루프 stall).  시작 시 techs[0]
                //  자동선택(L61)과 동일하게, 시작 가능한(prereq 충족) 첫 미완료 tech 를 잇는다.
                activeTech = null;
                foreach (var t in techs)
                {
                    if (CanStart(t)) { activeTech = t; Debug.Log($"[Research] 다음 자동선택: {t.nameKr}"); break; }
                }
            }
        }

        public bool IsUnlocked(string techId)
        {
            foreach (var t in techs) if (t.id == techId) return t.completed;
            return false;
        }

        public bool CanStart(Tech t)
        {
            if (t.completed) return false;
            if (t.prerequisites == null) return true;
            foreach (var prereq in t.prerequisites)
            {
                if (!IsUnlocked(prereq)) return false;
            }
            return true;
        }

        public void SetActive(Tech t)
        {
            if (t == null) { activeTech = null; return; }
            if (!CanStart(t)) { Debug.Log($"[Research] {t.nameKr} — prereq 미완료"); return; }
            activeTech = t;
            Debug.Log($"[Research] 시작: {t.nameKr} ({t.requiredPoints} pts)");
        }
    }
}
