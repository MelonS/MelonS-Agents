using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #122 - 레퍼런스 콜로니심 mood thoughts 시스템.
    ///  각 thought = (label, offset, expireSec).  현재 mood 는 base(50) + Σ thoughts.offset.
    ///  PawnNeeds.mood 는 이 값으로 매 frame 업데이트.
    ///  PawnInfoPanel 에 breakdown 표시 ("좋은 식사 +5", "추웠음 -3").
    ///
    /// AddThought("좋은 식사", +5, 600s) - 동일 label 있으면 expire 재설정.
    /// </summary>
    public class PawnThoughts : MonoBehaviour
    {
        [System.Serializable]
        public class Thought
        {
            public string label;
            public float offset;
            public float expireTime;  // Time.time 기준
        }

        public List<Thought> active = new List<Thought>();
        public float baseMood = 50f;

        // 일반적인 thought 카탈로그 (label, offset, durationSec)
        public static readonly (string label, float offset, float dur)[] Catalog = new[] {
            ("최고의 식사",   +12f, 800f),  // #131 - fine meal
            ("맛있는 식사",   +5f, 600f),
            ("배부름",        +3f, 300f),
            ("푹 잠",         +4f, 400f),
            ("침대에서 잠",   +2f, 600f),
            ("따뜻한 실내",   +2f, 300f),
            ("극심한 배고픔", -10f, 120f),  // 위키 #3 — 정본 Starving 단계 (3티어 최하)
            ("배고픔",        -4f, 120f),
            ("출출함",        -2f, 120f),   // 퀵픽 허기 2티어 — 굶주림 전 단계 신호
            ("수면 부족",     -3f, 180f),
            ("야외 폭풍",     -6f, 120f),
            ("어둠 속 활동",  -4f, 120f),
            ("어두운 밤 작업",-2f,  60f),
            ("부상",          -5f, 600f),
            ("동료 사망",     -15f, 1800f),
            ("아름다운 환경", +3f, 300f),
        };

        /// <summary>
        /// label 의 thought 를 추가하거나 expireTime 갱신.  catalog 에 정의된 label 만 허용.
        /// </summary>
        // T7 — 실내 = 지붕 아래 (RoofDesignation BUILT 셀).
        private bool hadDarkThought;   // [Dark] 진입 1회 로그용 에지 검출

        private bool IsIndoors()
        {
            var rd = RoofDesignation.Instance;
            if (rd == null) return false;
            var cell = new Vector2Int(Mathf.FloorToInt(transform.position.x),
                                      Mathf.FloorToInt(transform.position.y));
            return rd.IsRoofed(cell);
        }

        public void AddThought(string label)
        {
            foreach (var (l, off, dur) in Catalog)
            {
                if (l == label)
                {
                    AddThought(label, off, dur);
                    return;
                }
            }
        }

        public void AddThought(string label, float offset, float durationSec)
        {
            foreach (var t in active)
            {
                if (t.label == label)
                {
                    t.expireTime = Time.time + durationSec;
                    return;
                }
            }
            active.Add(new Thought {
                label = label, offset = offset,
                expireTime = Time.time + durationSec
            });
        }

        public void RemoveThought(string label)
        {
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].label == label) active.RemoveAt(i);
        }

        public float CurrentMood
        {
            get
            {
                float sum = baseMood;
                foreach (var t in active) sum += t.offset;
                return Mathf.Clamp(sum, 0f, 100f);
            }
        }

        // #mood모델(2026-06-05) — thought offset 합(baseMood 제외).  decay+thought 합산 모델에서
        //  needs.mood 에 '델타'로 반영하기 위한 값.
        public float ThoughtSum
        {
            get { float s = 0f; foreach (var t in active) s += t.offset; return s; }
        }
        private float lastAppliedThoughtSum = 0f;

        private float lastCullTime = -1f;

        private void Update()
        {
            // expire 정리 - 1초마다
            if (Time.time - lastCullTime < 1f) return;
            lastCullTime = Time.time;
            for (int i = active.Count - 1; i >= 0; i--)
                if (Time.time >= active[i].expireTime) active.RemoveAt(i);

            // 운영자 2026-06-02: 부정 thought(배고픔/수면부족/부상)가 catalog 엔 있으나 게임
            //  코드에서 AddThought 가 한 번도 안 불려 mood 가 항상 ≥50 (기분이 안 나빠짐).
            //  → needs/health 를 읽어 부정 thought 를 환류(낮으면 추가, 회복되면 제거)해서
            //  "배고프면/못 자면/다치면 기분이 나빠진다"가 실제로 작동하게 한다.
            var needs = GetComponent<PawnNeeds>();
            if (needs != null)
            {
                // 위키 #3(2026-06-14): 정본 Hungry ~35% 정렬 + 3티어 굶주림(상호배타 밴드).
                //  극심한 배고픔(-10) <12 / 배고픔(-4) 12~35 / 출출함(-2) 35~45 / ≥45 무.
                //  ※ 폰은 eatThreshold=50 에서 먹어 평시 food>45 → 부족 시에만 발화(저회귀).
                if (needs.food < 12f) AddThought("극심한 배고픔"); else RemoveThought("극심한 배고픔");
                if (needs.food >= 12f && needs.food < 35f) AddThought("배고픔"); else RemoveThought("배고픔");
                if (needs.food >= 35f && needs.food < 45f) AddThought("출출함"); else RemoveThought("출출함");
                if (needs.sleep < 25f) AddThought("수면 부족"); else RemoveThought("수면 부족");
                // #폭풍fix(2026-06-10): '야외 폭풍'(-6)이 catalog 에만 있고 미배선이던 것을
                //  배고픔/수면부족과 동일 패턴으로 환류.  PawnNeeds 의 직접 드레인(-3/s)은 제거 —
                //  mood 변화는 전부 decay+thought 모델 경유 (운영자 승인 2026-06-05 모델).
                bool indoors = IsIndoors();
                bool stormExposed = WeatherController.Instance != null
                    && WeatherController.Instance.Current == WeatherKind.Storm
                    && !indoors;
                if (stormExposed) AddThought("야외 폭풍"); else RemoveThought("야외 폭풍");
                // 갭 ※ '어둠 thought' — 야간 실외에서 깨어 활동하면 -4 (수면 중 면제).
                bool darkActive = !indoors && needs != null && !needs.IsSleeping
                    && GameClock.Instance != null
                    && (GameClock.Instance.Hour >= 20 || GameClock.Instance.Hour < 6);
                if (darkActive && !hadDarkThought)
                    Debug.Log($"[Dark] {GetComponent<PawnEntity>()?.PawnName ?? name} 어둠 속 활동 -4 진입 hour={(GameClock.Instance != null ? GameClock.Instance.Hour : -1)}");
                hadDarkThought = darkActive;
                if (darkActive) AddThought("어둠 속 활동"); else RemoveThought("어둠 속 활동");
                // 첫사이클 T7 (2026-06-12) — '실내'의 정의를 바닥 1장(허허벌판 면역
                //  이질)에서 '지붕 아래(IsRoofed)'로: 벽+지붕으로 지은 집이 처음으로
                //  폭풍 차단·쾌적 무드를 보상한다.  (바닥은 이동 보너스 전담)
                if (indoors) AddThought("따뜻한 실내"); else RemoveThought("따뜻한 실내");
                var health = GetComponent<PawnHealth>();
                if (health != null && health.TotalHpRatio < 0.6f) AddThought("부상");
                else RemoveThought("부상");
                // #mood모델(2026-06-05, 운영자 "decay+thought 합산"): thought 를 절대 override(needs.mood
                //  = 50+Σ) 대신 '델타'로 가산한다.  이전 절대 override 는 매초 PawnNeeds 의 자연 decay·
                //  식사 즉시보너스(+20/+10)·save 복원 mood 를 모두 무력화 → mood 하한 ~38 로 정신붕괴
                //  (mood<20) 불가, 식사보너스 증발이었다(확정 #10/#11/#12).  델타 방식: thought 추가/만료
                //  시 그 음·양수만큼만 mood 가 변하고, 사이의 자연 decay/회복/즉시보너스는 그대로 누적
                //  → mood = baseline-decay + Σthought 합산.  굶주림·부상·동료사망 누적 시 실제로 20 밑
                //  으로 떨어져 정신붕괴 발동.
                float sum = ThoughtSum;
                float delta = sum - lastAppliedThoughtSum;
                if (Mathf.Abs(delta) > 0.0001f)
                {
                    // #버그헌트4(2026-06-05): clamp 로 잘린 양은 lastApplied 에 누적하지 않는다.  경계
                    //  (0/100)에서 delta 일부가 clamp 로 삼켜질 때 lastApplied 에 전체 sum 을 기록하면
                    //  thought 만료 시 역델타가 '전체' 적용돼 비대칭 영구 드리프트(식사 후 오히려 mood
                    //  하락, 동료사망 회복 후 과대상승)였다.  실제 적용량(clamp 후-전)만 반영해 대칭 유지.
                    float before = needs.mood;
                    needs.mood = Mathf.Clamp(before + delta, 0f, 100f);
                    lastAppliedThoughtSum += (needs.mood - before);
                }
            }
        }

        // #버그헌트4(2026-06-05): save/load 후 GameSaveButtons 가 호출 — 복원된 needs.mood 에 이미
        //  thought 효과가 녹아 있으므로(저장 당시 델타 반영분), 현재 active thought 합을 '적용됨'으로
        //  잡아 첫 Update 가 재가산하지 않게 한다.  안 하면 lastApplied=0 + PawnEquipment.Awake 의
        //  '좋은 옷차림' 재추가로 옷보너스가 복원 mood 위에 다시 얹혀 mood 가 점프했다.  신규 스폰은
        //  호출 안 하므로 lastApplied=0 유지 → 첫 Update 가 thought 를 정상 1회 적용.
        public void SyncThoughtBaseline()
        {
            lastAppliedThoughtSum = ThoughtSum;
        }
    }
}
