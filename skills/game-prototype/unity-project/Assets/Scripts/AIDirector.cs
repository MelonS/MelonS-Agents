using System;
using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    [Serializable]
    public class GameEvent
    {
        public string id;
        public string title;
        public string description;
        public string flavor;  // optional LLM-generated 1-line atmosphere
        public int threatTier = 0;  // Day 73: 0=safe, 1=mild, 2=severe, 3=critical

        public string Formatted =>
            string.IsNullOrEmpty(flavor)
                ? $"<b>{title}</b>: {description}"
                : $"<b>{title}</b>: {description}\n<i>\"{flavor}\"</i>";
    }

    /// <summary>Day 73 — DirectorMode personality affects event frequency + threat curve.</summary>
    public enum DirectorMode
    {
        Steady,  // 꾸준한 사건 — 일정 간격, 위협도 단계적 상승
        Calm,     // 평온한 시간 — 사건 드물게, 위협도 천천히
        Chaos,      // 무작위 — 사건 자주 + 위협도 변동 큼
    }

    /// <summary>
    /// Day 5 AI Director — emits events on an interval to give the
    /// playthrough emergent narrative.  Events pool is loaded from a
    /// static seed at runtime; future iteration will load from
    /// Resources/events.json (operator regenerates pool via agent.py
    /// gen-events) so the pool itself is LLM-curated dev-time.
    /// </summary>
    public class AIDirector : MonoBehaviour
    {
        public event Action<GameEvent> OnEventFired;

        // 늑대 비활성화 게이트 (운영자 요청 2026-05-31): 현재 늑대를 처리할
        // 게임플레이 방법이 없어 wolf_pack 위협 이벤트를 발화하지 않음.
        //   되살리려면 true 로 (SceneSetup.WolvesEnabled 와 짝).  다른 이벤트
        //   (raid bandit, storm, infestation 등)는 영향 없음.  false 인 동안
        //   wolf_pack 이 pool 에 추가되지 않아 PlayWolfHowl 도 호출되지 않음.
        public const bool WolvesEnabled = false;

        [Header("Day 73: DirectorMode (3 종류)")]
        [SerializeField] public DirectorMode directorMode = DirectorMode.Steady;

        // #게임필(2026-06-10, 자율) — 이벤트 간격을 실시간(스케일드)에서 게임시간으로 전환.
        //  이전 15-30 스케일초는 3x 에서 실시간 5-10초당 1발 = 카드 도배 → '약탈자 접근!'도
        //  배경 소음이 됐다.  게임시간 8-16시간 간격 = 하루 1.5-3회, 드물어야 사건이 사건답다.
        [SerializeField] private float minIntervalGameHours = 8f;
        [SerializeField] private float maxIntervalGameHours = 16f;

        private float nextFireGameSec = -1f;   // GameClock.GameSeconds 기준 (-1 = 미스케줄)
        private GameEvent lastEvent;
        private readonly List<GameEvent> pool = new List<GameEvent>();

        // Day 73: 현재 threat tier — day 진행에 따라 자동 상승
        public int CurrentThreatTier
        {
            get
            {
                int day = (GameClock.Instance != null) ? GameClock.Instance.Day : 1;
                if (day < 1) day = 1;
                // Steady: 일정 (3/7/14일 임계점)
                if (directorMode == DirectorMode.Steady)
                {
                    if (day >= 14) return 3;
                    if (day >= 7) return 2;
                    if (day >= 3) return 1;
                    return 0;
                }
                // Calm: 느림 (6/14/25일)
                if (directorMode == DirectorMode.Calm)
                {
                    if (day >= 25) return 3;
                    if (day >= 14) return 2;
                    if (day >= 6) return 1;
                    return 0;
                }
                // Chaos: 랜덤 — day 와 무관, 0..3 random
                return UnityEngine.Random.Range(0, 4);
            }
        }

        // Day 13 raid scheduling.
        //
        // RAID CALIBRATION (recalibrated 2026-05-31 — operator goal "생존 유지").
        // Symptom that triggered this: a 3-pawn colony was WIPED in a ~19-day
        // LongPlay because raids fired every 3 days from day 3 and the size was
        // driven off the day-based CurrentThreatTier (tier3 = 5 bandits from
        // day 14), so by mid-game 5-bandit waves arrived every 3 days.  the reference sim
        // calibration: first raid ~day 5-10, raids days apart, small early, slow
        // escalation.  All knobs below are serialized so the operator can
        // re-tune difficulty without a code change.
        //
        //   RaidGraceDays      — no raid can fire before this in-game day.
        //   RaidIntervalDays   — minimum in-game days BETWEEN raids.
        //   MaxConcurrentGroups— hard cap on bandits in a single raid (escalation
        //                        ceiling; was effectively 5 via tier3).
        //   BaseRaidGroupSize  — first raid's bandit count.  Each subsequent raid
        //                        adds +1 every RaidsPerSizeStep raids, clamped to
        //                        MaxConcurrentGroups.  This DECOUPLES raid scale
        //                        from the day-based threat tier so escalation is
        //                        slow + countable, not a step-jump to 5.
        //
        // FIRST-RAID SOFTENING (recalibrated 2026-05-31 #2 — operator goal
        // "콜로니가 첫 습격에 림을 안 잃게").  Symptom that triggered THIS pass:
        // after the enemy-wall-respect fix bandits reach pawns far more reliably,
        // so the Day-6 first raid was killing 1 of 3 pawns (later raids survived
        // with 2).  We soften the OPENING WITHOUT touching raid frequency or
        // edge-spawn (those were just calibrated — see RaidSpawnEdge) and WITHOUT
        // touching BanditEnemy (out of this lane).  Two serialized levers:
        //
        //   RaidGraceDays 6 → 9 — the colony gets ~3 more in-game days to grow
        //     pawn count / combat skill / defenses before the first contact.
        //     Within the operator's suggested 8~10 window.
        //   FirstRaidExtraGraceDays — additional days the FIRST raid waits beyond
        //     RaidGraceDays (only the first; subsequent raids use the normal
        //     RaidIntervalDays cadence).  This pushes the opening contact to
        //     ~day 11 so a young 3-pawn colony can survive a single bandit, while
        //     the threat still exists and escalates afterward.  Set to 0 to make
        //     the first raid land exactly at RaidGraceDays (old behavior).
        [Header("Day 13 / raid calibration (the reference sim-ish — tunable)")]
        // #게임필(2026-06-10, 자율) — grace 9+2 는 첫 위협을 day 11(3x 실시간 ~60분)로
        //  밀어 어떤 세션에서도 위협이 등장하지 않는 '게임 아님' 상태를 만들었다(격차 분석
        //  4갈래 중 2갈래가 1순위 지목).  1마리 습격(BaseRaidGroupSize=1, cap 2)은 3림이
        //  감당 가능하므로 첫 접촉을 day 2 아침으로 — 5/31 wipe 의 원인은 '5마리×3일'
        //  조합이었지 grace 가 아니었다.  체감 난이도는 운영자 피드백으로 재조정.
        [SerializeField] private int RaidGraceDays = 2;        // first raid not before day 2
        [SerializeField] private int RaidIntervalDays = 3;     // raids ~3 in-game days apart
        [SerializeField] private int MaxConcurrentGroups = 2;  // escalation ceiling (bandits/raid)
        [SerializeField] private int BaseRaidGroupSize = 1;    // first raid = 1 bandit
        [SerializeField] private int RaidsPerSizeStep = 2;     // +1 bandit every 2 raids
        [SerializeField] private int FirstRaidExtraGraceDays = 0;  // 첫 습격 추가 유예 없음
        private int lastRaidDay = -1;
        private int raidCount = 0;     // how many raids have fired this run (drives slow size escalation)

        // #버그헌트3(2026-06-04): 레이드 스케줄러 상태 save/load.  시계(GameSeconds)만 복원되고
        //  lastRaidDay/raidCount 는 인스턴스 필드라 로드 시 -1/0 으로 리셋 → 로드 직후 첫 06:00 에
        //  즉시 레이드 + escalation(크기) 초반 수준 리셋됐다.  #276(시계 복원)과 짝을 맞춘다.
        public int LastRaidDay => lastRaidDay;
        public int RaidCount => raidCount;
        public void RestoreRaidState(int lastDay, int count)
        {
            lastRaidDay = lastDay;
            raidCount = Mathf.Max(0, count);
        }
        // raidSpawnRadius is RETAINED for any legacy/other callers but is NO LONGER
        // used to place raid bandits — see RaidSpawnEdge below.
        [SerializeField] private float raidSpawnRadius = 12f;

        // EDGE SPAWN (operator 2026-05-31): bandits used to appear at raidSpawnRadius
        // (=12) which is mid-map, right next to the settlement — they "갑툭튀"
        // (popped in) on top of the colony.  Operator wants them to WALK IN from
        // the map edge.  The world bound is ±29 (WORLD_HALF); we spawn just inside
        // that at ±RaidSpawnEdge so bandits enter from off-screen and their AI
        // (BanditEnemy pawn-chase) paths them toward the colony.  Serialized so the
        // operator can re-tune how far out they appear without a code change.
        //
        //   - The chosen side fixes ONE axis to ±RaidSpawnEdge (the edge).
        //   - The "along" axis now spans the full edge length (±RaidSpawnEdge) so
        //     bandits can enter from anywhere along that edge, not just the middle.
        [SerializeField] private float RaidSpawnEdge = 28f;

        // Stretch: trader sprite 주입 (SceneSetup 에서 wire)
        [SerializeField] private Sprite traderSprite;
        public void SetTraderSprite(Sprite s) { traderSprite = s; }

        private void Awake()
        {
            BuildDefaultPool();
        }

        private void Start()
        {
            ScheduleNext();
        }

        private void Update()
        {
            // #게임필 — 게임시간 스케줄: GameClock 폴링 (lesson #7: 싱글톤 구독 금지).
            var clock = GameClock.Instance;
            if (clock != null)
            {
                if (nextFireGameSec < 0f) ScheduleNext();   // 클럭 늦은 부트 대비
                if (clock.GameSeconds >= nextFireGameSec)
                {
                    FireRandomEvent();
                    ScheduleNext();
                }
            }

            // Day 13: raid check.  Poll GameClock from Update (lesson #7 firewall:
            // never subscribe singleton in OnEnable — bind order isn't guaranteed).
            TryScheduleRaid();
        }

        private void TryScheduleRaid()
        {
            var clock = GameClock.Instance;
            if (clock == null) return;
            int day = clock.Day;
            int hour = clock.Hour;
            // Grace period: no raids before RaidGraceDays.  The FIRST raid (none
            // fired yet → raidCount==0) waits an additional FirstRaidExtraGraceDays
            // so the opening contact lands later and a young colony can survive it
            // without a death.  Subsequent raids use the normal RaidIntervalDays
            // cadence (the spacing check below), so escalation is unchanged.
            int graceDays = RaidGraceDays + (raidCount == 0 ? Mathf.Max(0, FirstRaidExtraGraceDays) : 0);
            if (day < graceDays) return;
            // Fire at the morning window (hour 6) so the colony has daylight to
            // respond — same window as before.
            if (hour != 6) return;
            // Spacing: at least RaidIntervalDays must have elapsed since the last
            // raid.  lastRaidDay starts at -1 so the first eligible morning fires.
            if (lastRaidDay >= 0 && day - lastRaidDay < RaidIntervalDays) return;
            lastRaidDay = day;
            SpawnRaid();
        }

        private void SpawnRaid()
        {
            // Raid size is now driven by raidCount (how many raids have fired this
            // run) instead of the day-based CurrentThreatTier.  The old tier path
            // step-jumped to 5 bandits at day 14 which, combined with a raid every
            // 3 days, wiped a 3-pawn colony.  New escalation is slow + capped:
            //   group = BaseRaidGroupSize + (raidCount / RaidsPerSizeStep), clamped
            //   to MaxConcurrentGroups.  e.g. with defaults (base 1, step 2, cap 2):
            //   raid#0=1, raid#1=1, raid#2=2, raid#3=2, ... (never exceeds 2).
            // 게임루프 백로그 #9 (2026-06-11): cap 2 영구 고정 → 콜로니는 강해지는데
            //  위협 불변 = 발전이 취향이 되던 평탄화 해소.  상한을 '생존 림 수 - 1' 과
            //  기존 cap 중 큰 쪽으로 — 3림 기본은 현행 동일(5/31 wipe 재발 없음).
            int aliveColonists = 0;
            foreach (var pp in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                if (pp != null && !pp.IsDead) aliveColonists++;
            int dynamicCap = Mathf.Max(Mathf.Max(1, MaxConcurrentGroups), aliveColonists - 1);
            int banditCount = Mathf.Clamp(
                BaseRaidGroupSize + (raidCount / Mathf.Max(1, RaidsPerSizeStep)),
                1, dynamicCap);
            raidCount++;

            // Wiki Dim2 #2 (sound wiring only — no threat/balance change): every raid
            // sounds the alert siren, scaled by current threat tier for audio flavor
            // only (does NOT affect bandit count anymore).  PlayAlert(int) null-guard.
            AudioBank.Instance?.PlayAlert(CurrentThreatTier);

            // ONE log line per raid (was one per bandit) so the raid cadence is
            // countable from the play log: grep '[AIDirector] RAID'.
            Debug.Log($"[AIDirector] RAID #{raidCount} day={clockDayForLog()} bandits={banditCount} (grace={RaidGraceDays} interval={RaidIntervalDays} cap={MaxConcurrentGroups})");
            // #버그헌트(2026-06-03): 레이드 이벤트는 레이드당 1회만 발생해야 한다.  이전엔
            //  SpawnSingleBandit 안에서 OnEventFired 를 호출해 bandit 수만큼 "약탈자 접근!"이
            //  중복 발생(이벤트 로그/경보 도배)했다 → 레이드 단위로 1회 여기서 발생.
            var raidEv = new GameEvent
            {
                id = "bandit_raid",
                title = "약탈자 접근!",
                description = "무장한 약탈자가 지도 외곽에 나타났다.",
                flavor = "칼날에 새벽빛이 비친다.",
                // 게임루프 백로그 #1 (2026-06-11): tier 기본 0 이라 AlertStackUI 의
                //  threatTier<1 필터가 '습격만' 걸러냈다 — 영구 경보 카드 + 클릭 카메라
                //  팬 사슬이 통째로 죽어 있던 버그.  상인(2)과 동급 이상이 마땅하다.
                threatTier = 2,
            };
            lastEvent = raidEv;
            OnEventFired?.Invoke(raidEv);
            for (int i = 0; i < banditCount; i++) SpawnSingleBandit(i);
        }

        private void SpawnSingleBandit(int waveIndex)
        {
            try
            {
                // 같은 side 에서 약간 흩어진 위치 (전체 wave 가 함께 진입).
                // EDGE SPAWN: the fixed axis sits at ±RaidSpawnEdge (map edge, just
                // inside the ±29 world bound) so bandits enter from off-screen and
                // walk toward the colony via BanditEnemy pawn-chase.  The "along"
                // axis spans the whole edge so they can come from any point along it.
                int side = (waveIndex == 0)
                    ? UnityEngine.Random.Range(0, 4)
                    : (waveIndex % 4);
                float along = UnityEngine.Random.Range(-RaidSpawnEdge, RaidSpawnEdge);
                Vector3 pos;
                switch (side)
                {
                    case 0: pos = new Vector3( RaidSpawnEdge, along, 0); break;
                    case 1: pos = new Vector3(-RaidSpawnEdge, along, 0); break;
                    case 2: pos = new Vector3(along,  RaidSpawnEdge, 0); break;
                    default: pos = new Vector3(along, -RaidSpawnEdge, 0); break;
                }
                // 같은 wave 끼리 살짝 spread
                pos.x += UnityEngine.Random.Range(-1.5f, 1.5f);
                pos.y += UnityEngine.Random.Range(-1.5f, 1.5f);

                GameObject go = new GameObject($"Bandit_Raid_{waveIndex}");
                go.transform.position = pos;
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                // Reuse pawn sprite tinted red — sprite is already imported by
                // ForceImportAllSprites in SceneSetup; a dedicated bandit sprite
                // is a future polish item.
                Sprite s = Resources.Load<Sprite>("pawn_colonist");
                if (s == null)
                {
                    // Fallback: any PawnEntity in scene shares a sprite asset we
                    // can mirror at runtime.  Spawn-time copy from an existing
                    // pawn's renderer keeps this build self-contained.
                    var anyPawn = GameObject.FindFirstObjectByType<PawnEntity>();
                    if (anyPawn != null)
                    {
                        var pawnSr = anyPawn.GetComponent<SpriteRenderer>();
                        if (pawnSr != null) s = pawnSr.sprite;
                    }
                }
                // sr.sprite = null is allowed by Unity (renders nothing) — no
                // need to abort the raid just because we lack a sprite asset.
                sr.sprite = s;
                sr.color = new Color(0.9f, 0.3f, 0.3f, 1f);
                // #sort-audit: pawn 본체 규약은 10 (BanditEnemy 포섭 후도 10).  11 이면
                //  운반중 콜로니스트(carry bundle=11)와 같은 층이라 본체 위로 떠 보였음 → 10.
                sr.sortingOrder = 10;
                // IMPORTANT: BoxCollider2D MUST be added BEFORE BanditEnemy
                // because BanditEnemy declares [RequireComponent(typeof(Collider2D))]
                // and Unity will auto-add a default Collider2D if missing, which
                // can race with our intended BoxCollider2D sizing.
                BoxCollider2D col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(2f, 2f);
                go.AddComponent<BanditEnemy>();

                // #버그헌트: 레이드 이벤트(OnEventFired)는 SpawnRaid 에서 레이드당 1회만 발생.
                //  여기(per-bandit)서 발생시키던 중복 호출 제거.  per-bandit 은 조용한 로그만.
                Debug.Log($"[AIDirector] bandit spawn wave={waveIndex} pos={pos}");
            }
            catch (Exception e)
            {
                // Raid spawn failed — log loudly and keep the simulation
                // running.  Next raid scheduling tick will try again on the
                // next day-3-multiple morning.
                Debug.LogError($"[AIDirector] SpawnRaid failed: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private int clockDayForLog()
        {
            var c = GameClock.Instance;
            return c != null ? c.Day : -1;
        }

        private void ScheduleNext()
        {
            // Day 73: director mode마다 간격 다름 (#게임필 — 단위가 게임시간으로 바뀜)
            float min = minIntervalGameHours, max = maxIntervalGameHours;
            if (directorMode == DirectorMode.Calm)
            {
                min *= 2f; max *= 2f;  // 평온한 시간 — 사건 드물게
            }
            else if (directorMode == DirectorMode.Chaos)
            {
                min *= 0.6f; max *= 0.6f;  // 무작위 — 사건 자주
            }
            // #175 - threat tier 가 올라가면 event 빈도 증가 (wiki: late-game 빠른 raid pace).
            //  tier 0 = ×1.0, tier 1 = ×0.85, tier 2 = ×0.70, tier 3 = ×0.55 (interval).
            int tier = CurrentThreatTier;
            float tierMul = 1f - tier * 0.15f;
            min *= tierMul; max *= tierMul;
            float waitHours = UnityEngine.Random.Range(min, max);
            var clock = GameClock.Instance;
            float baseSec = clock != null ? clock.GameSeconds : 0f;
            nextFireGameSec = baseSec + waitHours * 3600f;
        }

        /// <summary>Trader spawn helper - trader_caravan event 발화 시 호출.</summary>
        private void SpawnTrader()
        {
            try
            {
                var traderSpr = traderSprite;  // SetTraderSprite 로 주입된 ref
                int side = UnityEngine.Random.Range(0, 4);
                float along = UnityEngine.Random.Range(-12f, 12f);
                Vector3 pos = side switch
                {
                    0 => new Vector3( 18f, along, 0),
                    1 => new Vector3(-18f, along, 0),
                    2 => new Vector3(along,  18f, 0),
                    _ => new Vector3(along, -18f, 0),
                };
                GameObject go = new GameObject("Trader_Caravan");
                go.transform.position = pos;
                go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                if (traderSpr != null) sr.sprite = traderSpr;
                else sr.color = new Color(0.85f, 0.65f, 0.30f, 1f);  // 황금색 fallback
                // #sort-audit: 9 는 그림자 층 — trader 본체가 일반 pawn 본체(10)에 가려졌음.
                //  trader 도 보행 NPC 이므로 본체 규약 10 으로 정렬.
                sr.sortingOrder = 10;
                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.8f, 1.2f);
                go.AddComponent<TraderEntity>();
                Debug.Log($"[AIDirector] Trader spawn @ {pos}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIDirector] SpawnTrader FAIL: {e.Message}");
            }
        }

        private void FireRandomEvent()
        {
            if (pool.Count == 0) return;
            // Day 73: 현재 tier 이하 이벤트만 선택 (Steady/Calm) 또는
            //  완전 무작위 (Chaos)
            int curTier = CurrentThreatTier;
            List<GameEvent> candidates = new List<GameEvent>();
            foreach (var ev in pool)
            {
                if (directorMode == DirectorMode.Chaos || ev.threatTier <= curTier)
                    candidates.Add(ev);
            }
            if (candidates.Count == 0) candidates.AddRange(pool);

            GameEvent next;
            int tries = 0;
            do
            {
                next = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                tries++;
                // #게임필 — quiet_evening 은 저녁(18시+)에만: '조용한 저녁'이 오후 2시에
                //  뜨던 시간대 불일치 수정.  재추첨 5회 안에 못 피하면 그냥 통과(무한루프 방지).
            } while ((next == lastEvent
                      || (next.id == "quiet_evening"
                          && GameClock.Instance != null && GameClock.Instance.Hour < 18))
                     && tries < 5);
            lastEvent = next;
            OnEventFired?.Invoke(next);
            Debug.Log($"[AIDirector:{directorMode} T{curTier}] {next.title}: {next.description}");
            ApplyEventEffect(next);   // #게임필 — 카드가 말하는 일을 실제로 일으킨다
            // Stretch: trader_caravan event → actual Trader entity spawn
            if (next.id == "trader_caravan") SpawnTrader();
            // Wiki Dim2 #3 (sound wiring — 0-callers fix): wolf_pack event plays the
            // existing howl clip.  PlayWolfHowl() exists today; null-guard.
            if (next.id == "wolf_pack")
            {
                AudioBank.Instance?.PlayWolfHowl();
                AudioBank.Instance?.PlayAlert(next.threatTier);
            }
        }

        /// <summary>#게임필(2026-06-10, 자율) — 이벤트 실효 배선.  텍스트가 약속한 일을
        ///  기존 시스템 호출로 실제로 일으킨다 (신규 시스템 없음 — Pile.Spawn 은 GameManager
        ///  시작 드롭, mood 는 PawnNeeds 공개 필드).  효과는 로그로 검증 가능.</summary>
        private void ApplyEventEffect(GameEvent ev)
        {
            if (ev == null) return;
            switch (ev.id)
            {
                case "lucky_find":
                {
                    Vector3 c = ColonyCenterOrZero();
                    var p = new Vector3(Mathf.Floor(c.x + UnityEngine.Random.Range(-2f, 3f)) + 0.5f,
                                        Mathf.Floor(c.y + UnityEngine.Random.Range(-2f, 3f)) + 0.5f, 0f);
                    WoodPileEntity.Spawn(p, 25, null);   // null sprite → EnsureSprite 기본
                    Debug.Log($"[AIDirector] lucky_find 실효: 목재 25 더미 드롭 @ ({p.x:F1},{p.y:F1})");
                    break;
                }
                case "morale_dip":
                {
                    int n = 0;
                    foreach (var nd in FindAllNeeds())
                        if (nd != null) { nd.mood = Mathf.Max(0f, nd.mood - 10f); n++; }
                    Debug.Log($"[AIDirector] morale_dip 실효: {n}명 기분 -10");
                    break;
                }
                case "minor_disease":
                {
                    var all = FindAllNeeds();
                    if (all.Length > 0)
                    {
                        var v = all[UnityEngine.Random.Range(0, all.Length)];
                        if (v != null)
                        {
                            v.mood = Mathf.Max(0f, v.mood - 8f);
                            v.sleep = Mathf.Max(0f, v.sleep - 10f);
                            Debug.Log($"[AIDirector] minor_disease 실효: {v.name} 기분 -8 / 수면 -10");
                        }
                    }
                    break;
                }
            }
        }

        private static PawnNeeds[] FindAllNeeds()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<PawnNeeds>(FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<PawnNeeds>();
#endif
        }

        private Vector3 ColonyCenterOrZero()
        {
            var pawns =
#if UNITY_2023_1_OR_NEWER
                UnityEngine.Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
#else
                UnityEngine.Object.FindObjectsOfType<PawnEntity>();
#endif
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var p in pawns)
                if (p != null && !p.IsDead) { sum += p.transform.position; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        private void BuildDefaultPool()
        {
            pool.Clear();
            // Pre-seeded events.  Operator can later regenerate via
            // game-dev-agent's runtime_director module (LLM-generated
            // variants stored in Resources/events.json).
            // Day 73: tier 0 (safe events)
            pool.Add(new GameEvent {
                id = "wanderer_arrival", threatTier = 0,
                title = "방랑자 도착",
                description = "한 여행자가 야영지 외곽에 나타나 머물 곳을 찾고 있다.",
                flavor = "장화에 묻은 진흙이 그가 걸어온 길의 길이를 말해준다.",
            });
            pool.Add(new GameEvent {
                id = "lucky_find", threatTier = 0,
                title = "행운의 발견",
                description = "콜로니스트 한 명이 숲에서 작은 보급품 더미를 발견했다.",
                flavor = "기름천에 잘 싸인 도구들.",
            });
            pool.Add(new GameEvent {
                id = "bird_omen", threatTier = 0,
                title = "새들의 징조",
                description = "새벽부터 까마귀들이 죽은 참나무 위로 모여들고 있다.",
                flavor = "노인들 말로는 무언가 변화의 조짐이라고 한다.",
            });
            pool.Add(new GameEvent {
                id = "good_harvest", threatTier = 0,
                title = "좋은 수확",
                description = "오늘 아침 베어낸 나무가 평소보다 깔끔하게 쪼개졌다.",
                flavor = "건조하고 단단한, 정직한 나뭇결.",
            });
            pool.Add(new GameEvent {
                id = "quiet_evening", threatTier = 0,
                title = "조용한 저녁",
                description = "별일 없이 하루가 저문다. 모닥불 타는 소리만 들린다.",
                flavor = "역사에 남지 않을, 그저 흘러가는 날들.",
            });

            // Day 73: tier 1 (mild) — storms, morale, predators
            pool.Add(new GameEvent {
                id = "storm_warning", threatTier = 1,
                title = "폭풍 경보",
                description = "북쪽에서 짙은 먹구름이 몰려온다. 한 시간 안에 폭풍이 닥칠 것이다.",
                flavor = "바람에서 벌써 빗냄새가 난다.",
            });
            // #게임필(2026-06-10, 자율) — 신호 신뢰 회복: 카드(tier≥1)가 말하는 일은 실제로
            //  일어나야 한다.  morale_dip/minor_disease 는 ApplyEventEffect 로 실효 배선,
            //  fox_sighting 은 월드 효과가 없으므로 tier 0 분위기 텍스트로 강등.
            pool.Add(new GameEvent {
                id = "morale_dip", threatTier = 1,
                title = "사기 저하",
                description = "오늘 콜로니스트들이 어딘가 무기력해 보인다. (전원 기분 -10)",
                flavor = "저녁 식탁의 대화가 짧고 띄엄띄엄했다.",
            });
            pool.Add(new GameEvent {
                id = "fox_sighting", threatTier = 0,
                title = "여우 출현",
                description = "붉은 여우가 숲 가장자리에서 야영지를 지켜본다. 두려워하지 않는다.",
                flavor = "발견되어도 도망가지 않는다.",
            });
            pool.Add(new GameEvent {
                id = "minor_disease", threatTier = 1,
                title = "감기 기운",
                description = "콜로니스트 한 명이 기침을 시작했다. 컨디션이 떨어진다.",
                flavor = "감기인지 더 나쁜 것인지 아직 모른다.",
            });

            // Day 73: tier 2 (severe)
            pool.Add(new GameEvent {
                id = "trader_caravan", threatTier = 2,
                title = "상인 도착",
                description = "상인 일행이 방문하여 거래를 제안한다. (실제 거래 시스템: 향후 Day)",
                flavor = "그들의 마차에서 새로운 냄새가 난다.",
            });
            // 늑대 비활성화 게이트: WolvesEnabled=false 인 동안 wolf_pack 이벤트는
            //   pool 에 추가하지 않음 → 발화/PlayWolfHowl 모두 안 됨.  되살리려면 true.
            if (WolvesEnabled)
            {
                pool.Add(new GameEvent {
                    id = "wolf_pack", threatTier = 2,
                    title = "늑대 무리",
                    description = "굶주린 늑대 무리가 야영지 외곽을 어슬렁거린다.",
                    flavor = "송곳니가 달빛에 번뜩였다.",
                });
            }
            // #게임필(2026-06-10, 자율) — 거짓 경보 제거: food_blight(작물 시들음)·large_raid
            //  (5명 약탈)·siege_camp(포위)·infestation(벌레떼)은 어떤 시스템도 소비하지 않는
            //  순수 텍스트였다.  '영구 빨간 카드인데 아무 일도 안 일어남'이 반복되면 진짜
            //  습격 카드(bandit_raid, SpawnRaid 발)도 무시된다 — 레터는 항상 실제 사건이라는
            //  레퍼런스 컨벤션 회복.  실제 습격은 TryScheduleRaid 가 전담하므로 랜덤 풀에
            //  가짜 raid 류는 두지 않는다.  되살릴 땐 반드시 효과 배선과 함께.
        }
    }
}
