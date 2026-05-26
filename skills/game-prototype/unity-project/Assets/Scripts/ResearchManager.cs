using System.Collections.Generic;
using UnityEngine;

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
        public static ResearchManager Instance { get; private set; }

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

        public delegate void TechCompletedHandler(Tech t);
        public event TechCompletedHandler OnTechCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildTree();
        }

        private void BuildTree()
        {
            // Tier 1 (no prerequisites)
            techs.Add(new Tech("simple_bow",     "원시 활",       "원거리 사냥/방어 가능. 12 dmg 화살.", 100));
            techs.Add(new Tech("stone_walls",    "석재 벽 건설",  "나무 벽보다 4배 튼튼 (HP 200).",     150));
            techs.Add(new Tech("better_stove",   "개선된 화덕",   "조리 속도 2배, 식사 mood +5.",       120));
            // Tier 2 (require tier 1)
            techs.Add(new Tech("electricity",    "전기 기초",     "발전기·배터리·전선 해금.",           250, "stone_walls"));
            // Tier 3 (require electricity)
            techs.Add(new Tech("solar_panel",    "태양광 패널",   "낮 동안 무료 전력 생산.",            300, "electricity"));
        }

        private void Update()
        {
            if (activeTech == null || activeTech.completed) return;
            // Count benches with at least one pawn within research-radius
            ResearchBench[] benches = GameObject.FindObjectsOfType<ResearchBench>();
            if (benches == null || benches.Length == 0) return;
            int activeBenches = 0;
            foreach (var b in benches)
            {
                if (b == null) continue;
                if (b.HasResearcherNearby()) activeBenches++;
            }
            if (activeBenches == 0) return;
            float gain = pointsPerSecondPerBench * activeBenches * Time.deltaTime;
            activeTech.currentPoints += Mathf.CeilToInt(gain * 10f) / 10;  // cheap fractional accumulator
            if (activeTech.currentPoints >= activeTech.requiredPoints)
            {
                activeTech.currentPoints = activeTech.requiredPoints;
                activeTech.completed = true;
                Debug.Log($"[Research] 완료: {activeTech.nameKr}");
                if (OnTechCompleted != null) OnTechCompleted(activeTech);
                activeTech = null;
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
