using System.Collections.Generic;
using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 53 — Research tree singleton.
    /// Tier-1 techs (no prereqs) with Korean labels.  Every listed tech has a
    /// REAL gameplay effect wired via IsUnlocked(): simple_bow (PawnUtilityAI
    /// 원거리 공격 해금) + better_stove (PawnCook 조리 2배).  Dead/non-vanilla
    /// techs (electricity·solar_panel·stone_walls) were removed — see BuildTree.
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
        // 연구 디스패치 보정 (2026-06-12) — 디스패치 전 2f 는 '림이 벤치 옆을 우연히
        //  지나갈 때만' 전제의 수치였다.  DoResearchAction 으로 유휴 림이 상주하게 되자
        //  2게임일 만에 테크 사다리 전체(640pt+)가 소진(검증 소크 실측).  전담 1명 기준
        //  원시 활(100pt) ~ 1게임일(1000스케일초 × 0.15 × manipulation~1) → 사다리
        //  전체 ~ 1주+ 로 레퍼런스 페이싱 회복.
        // 0.15 → 0.25 (2026-07-31 실측 보정).  위 계산은 "전담 1명이 **하루 종일** 벤치에
        //  있다"를 전제로 100pt≈1게임일을 잡았는데, 실제로는 그 전제가 성립하지 않는다:
        //  담당도 잠을 자고(하루의 1/3), 치료 같은 1순위 호출에 끌려가고, 식사도 한다.
        //  실측은 하루 24pt — 전제의 1/4 이다.  전제를 현실에 맞추면 원시 활은 4일이 걸리고,
        //  '연구 1개 완료' 목표는 사실상 도달 불가 표시로 남는다(오늘 고친 것과 같은 유형).
        //  0.25 로 올려 관측 기준 하루 ~40pt = 첫 테크 1.5게임일.  뒤쪽 테크(120~240)는
        //  여전히 며칠씩 걸리므로 "2게임일에 사다리 전체 소진"(위 주석의 경계) 은 재발하지 않는다.
        public float pointsPerSecondPerBench = 0.25f;
        private float pointsFraction = 0f;  // Day 75 fractional accumulator
        private float lastGainTime = -999f; // 최근 포인트 적립 시각 (StallReason 용)

        // Lesson #4 firewall - FindObjectsByType per-Update 비쌈.  cache + 1s 재검색.
        private ResearchBench[] cachedBenches;
        private float nextBenchSearchTime = -10f;
        private const float BenchSearchInterval = 1.0f;

        public delegate void TechCompletedHandler(Tech t);
        public event TechCompletedHandler OnTechCompleted;

        /// <summary>완료된 연구 수 (FunTelemetry / 목표 판정 공용).</summary>
        public int CompletedCount
        {
            get { int n = 0; for (int i = 0; i < techs.Count; i++) if (techs[i].completed) n++; return n; }
        }

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
            // `techs` 는 public List 라 **직렬화된다** — 씬에 한 번이라도 구워지면
            //  여기서 Add 만 하던 코드가 목록을 두 배로 불린다 (2026-08-01 리뷰 #8).
            //  지금은 씬에 비어 있어 증상이 없지만, 누군가 인스펙터에서 한 번
            //  건드리는 순간 연구 목록이 중복되고 목표 판정까지 어긋난다.
            //  한 줄로 막을 수 있는 일을 조건에 맡기지 않는다.
            techs.Clear();

            // Tier 1 (no prerequisites)
            // #200 truth-in-UI fix: description said "12 dmg 화살" but pawns actually
            //  shoot 3-5 dmg arrows (PawnUtilityAI rDmg), tuned to the small enemy HP
            //  pools (deer 12 / rabbit 3 / wolf 18).  Raising arrow dmg to 12 would
            //  one-shot most animals — an unrealistic combat rewrite the operator
            //  forbade.  So the honest fix is the description, not the damage.
            // 100 → 60 (2026-07-31): **첫 해금은 빨라야 온보딩이 된다**.  플레이어가
            //  연구라는 장치가 실재한다는 걸 첫 게임일 안에 한 번은 봐야, 이후의 며칠짜리
            //  테크를 기다릴 이유가 생긴다.  뒤 테크들의 비용은 그대로 두어 사다리 경사는 유지.
            techs.Add(new Tech("simple_bow",     "원시 활",       "원거리 사냥/방어 가능. 3~5 피해 화살.", 60));   // QA F8 — 한글 UI 속 영문 dmg 제거
            // #truth-in-UI: better_stove 는 IsUnlocked 로 PawnCook 의 조리 속도 2배만
            //  배선돼 있다.  이전 설명의 "식사 mood +5" 는 어디에도 배선이 없어(식사 mood 는
            //  이미 meal +10 / fine +20 으로 별도 동작) 거짓 표기였음 → #200 활 설명 정정과
            //  동일하게, 실제 효과(조리 2배)만 적도록 설명을 진실에 맞춘다.
            techs.Add(new Tech("better_stove",   "개선된 화덕",   "조리 속도 2배.",                     120));
            // 게임루프 발전 아크 (2026-06-12) — 220pt 에서 끝나던 연구 사다리 연장.
            //  실효 배선: masonry → PawnMiner 채광 1.3x, irrigation → CropEntity 성장 1.4x.
            techs.Add(new Tech("masonry",        "석공술",        "채광 속도 +30%.",                    180));
            techs.Add(new Tech("irrigation",     "관개",          "작물 성장 +40%.",                    240));
            // #범위정합 / #spec-faithful: 전기·태양광에 이어 stone_walls(석재 벽) tech 도 제거.
            //  (1) dead tech — IsUnlocked("stone_walls") 를 소비하는 코드가 전혀 없어 연구해도
            //      아무 변화가 없었고, 설명("HP 200")조차 실제 석재 벽(HP 280)과 불일치한 거짓.
            //  (2) 비바닐라 — 기준 콜로니심은 석재 벽에 연구가 필요 없다(시작부터 건설 가능,
            //      실제로 Architect 메뉴 WallStone 으로 이미 건설된다).  연구 게이팅은 임의
            //      설계라 금지(follow-spec).  배선 가능한 진짜 효과가 생기는 날 재도입.
        }

        /// <summary>#게임필 배치4(2026-06-10 자율) — 연구 정지 원인 (HUD 티커 표시용).
        ///  null = 정상 진행.  '영구 0/100' 죽은 숫자가 HUD 의 유일한 목표 표시였는데
        ///  왜 안 오르는지(작업대 없음/연구자 없음)를 화면이 말해주지 않던 것 해소.</summary>
        public string StallReason
        {
            get
            {
                if (activeTech == null || activeTech.completed) return null;
                if (cachedBenches == null)
                    cachedBenches = GameObject.FindObjectsByType<ResearchBench>(FindObjectsSortMode.None);
                if (cachedBenches == null || cachedBenches.Length == 0)
                    return "작업대 필요 (건축 F8)";
                float sum = 0f;
                foreach (var b in cachedBenches)
                    if (b != null) sum += b.ResearcherSpeedSum();
                // grader 좌표 (2026-06-12) — 연구는 유휴 필러라 순간 스냅샷의 ~90%는
                //  실제로 벤치가 비어 있다.  진행 중인데 '연구자 없음'이 상시 표기되는
                //  혼란 → '최근 30 스케일초간 적립 0'일 때만 정지 사유로 인정.
                if (sum > 0.001f) return null;
                return (Time.time - lastGainTime > 30f) ? "연구자 없음 (작업대 근처 림)" : null;
            }
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
            // 2026-08-01 — **책상당 한 명**으로 바뀌었다 (운영자 "연구대를 여러명이
            //  같이 쓰는것도 이상해").  따라서 이 합은 이제 '여러 pawn' 이 아니라
            //  **여러 책상**에 대한 합이다 — 책상을 더 지어야 연구가 빨라진다.
            //  (ResearchBench.ResearcherSpeedSum 이 예약자만 세도록 게이트한다.)
            float speedMul = 0f;
            foreach (var b in benches)
            {
                if (b == null) continue;
                speedMul += b.ResearcherSpeedSum();
            }
            if (speedMul <= 0.001f) return;
            float gain = pointsPerSecondPerBench * speedMul * Time.deltaTime;
            pointsFraction += gain;
            lastGainTime = Time.time;   // StallReason 의 '최근 진행' 추적
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
                AudioBank.Instance?.PlayResearch();   // #게임필2 — 유일한 장기 보상 루프가 들린다
                if (OnTechCompleted != null) OnTechCompleted(activeTech);
                // 게임루프 백로그 #6 (2026-06-11): simple_bow 가 '죽은 테크'였다 — 발사는
                //  활 장착이 필수인데 획득 경로가 스폰 랜덤(~20%)뿐.  완료 시 원거리
                //  무기 없는 전 콜로니스트에게 활 지급 (Catalog/Equip 재사용).
                if (activeTech.id == "simple_bow")
                {
                    var bow = System.Array.Find(PawnEquipment.Catalog,
                        c => c.slot == PawnEquipment.Slot.Weapon && c.rangedEnabled);
                    foreach (var pe in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                    {
                        if (pe == null || pe.IsDead) continue;
                        var eq = pe.GetComponent<PawnEquipment>();
                        if (eq == null) continue;
                        var cur = eq.GetEquipped(PawnEquipment.Slot.Weapon);
                        if (cur != null && cur.rangedEnabled) continue;
                        eq.Equip(bow);
                        FloatingText.Spawn(pe.transform.position + Vector3.up * 0.7f,
                            "활 지급!", new Color(0.8f, 0.9f, 0.5f, 1f));
                    }
                }
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
