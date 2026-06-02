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

        [SerializeField] private float minIntervalSec = 15f;
        [SerializeField] private float maxIntervalSec = 30f;

        private float nextFireTime;
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
        [SerializeField] private int RaidGraceDays = 9;        // first raid not before ~day 9
        [SerializeField] private int RaidIntervalDays = 5;     // raids ~5 in-game days apart
        [SerializeField] private int MaxConcurrentGroups = 2;  // escalation ceiling (bandits/raid)
        [SerializeField] private int BaseRaidGroupSize = 1;    // first raid = 1 bandit
        [SerializeField] private int RaidsPerSizeStep = 2;     // +1 bandit every 2 raids
        [SerializeField] private int FirstRaidExtraGraceDays = 2;  // first raid waits +2 days beyond grace
        private int lastRaidDay = -1;
        private int raidCount = 0;     // how many raids have fired this run (drives slow size escalation)
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
            if (Time.timeSinceLevelLoad >= nextFireTime)
            {
                FireRandomEvent();
                ScheduleNext();
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
            int banditCount = Mathf.Clamp(
                BaseRaidGroupSize + (raidCount / Mathf.Max(1, RaidsPerSizeStep)),
                1, Mathf.Max(1, MaxConcurrentGroups));
            raidCount++;

            // Wiki Dim2 #2 (sound wiring only — no threat/balance change): every raid
            // sounds the alert siren, scaled by current threat tier for audio flavor
            // only (does NOT affect bandit count anymore).  PlayAlert(int) null-guard.
            AudioBank.Instance?.PlayAlert(CurrentThreatTier);

            // ONE log line per raid (was one per bandit) so the raid cadence is
            // countable from the play log: grep '[AIDirector] RAID'.
            Debug.Log($"[AIDirector] RAID #{raidCount} day={clockDayForLog()} bandits={banditCount} (grace={RaidGraceDays} interval={RaidIntervalDays} cap={MaxConcurrentGroups})");
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
                sr.sortingOrder = 11;
                // IMPORTANT: BoxCollider2D MUST be added BEFORE BanditEnemy
                // because BanditEnemy declares [RequireComponent(typeof(Collider2D))]
                // and Unity will auto-add a default Collider2D if missing, which
                // can race with our intended BoxCollider2D sizing.
                BoxCollider2D col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(2f, 2f);
                go.AddComponent<BanditEnemy>();

                // Surface the raid in the existing event log (reuses EventLogUI
                // subscribed to OnEventFired — same hook used by storm/wanderer
                // events above).
                var ev = new GameEvent
                {
                    id = "bandit_raid",
                    title = "약탈자 접근!",
                    description = "무장한 약탈자가 지도 외곽에 나타났다.",
                    flavor = "칼날에 새벽빛이 비친다.",
                };
                lastEvent = ev;
                OnEventFired?.Invoke(ev);
                // Per-bandit detail kept at a quieter level; the RAID summary line
                // is emitted once per raid in SpawnRaid() and is the countable one.
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
            // Day 73: director mode마다 간격 다름
            float min = minIntervalSec, max = maxIntervalSec;
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
            float wait = UnityEngine.Random.Range(min, max);
            nextFireTime = Time.timeSinceLevelLoad + wait;
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
                sr.sortingOrder = 9;
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
            } while (next == lastEvent && tries < 5);
            lastEvent = next;
            OnEventFired?.Invoke(next);
            Debug.Log($"[AIDirector:{directorMode} T{curTier}] {next.title}: {next.description}");
            // Stretch: trader_caravan event → actual Trader entity spawn
            if (next.id == "trader_caravan") SpawnTrader();
            // Wiki Dim2 #3 (sound wiring — 0-callers fix): wolf_pack event plays the
            // existing howl clip.  PlayWolfHowl() exists today; null-guard.
            if (next.id == "wolf_pack") AudioBank.Instance?.PlayWolfHowl();
            // Wiki Dim2 #2 (sound wiring): high-threat narrative events sound the
            // alert siren, tier-scaled via next.threatTier (tier3 events repeat
            // more than tier1).  PlayAlert(int) lands from Lane A this same wave;
            // null-guard.  No threat/scheduling/balance change — sound only.
            if (next.id == "wolf_pack" || next.id == "large_raid"
                || next.id == "siege_camp" || next.id == "infestation")
            {
                AudioBank.Instance?.PlayAlert(next.threatTier);
            }
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
            pool.Add(new GameEvent {
                id = "morale_dip", threatTier = 1,
                title = "사기 저하",
                description = "오늘 콜로니스트들이 어딘가 무기력해 보인다.",
                flavor = "저녁 식탁의 대화가 짧고 띄엄띄엄했다.",
            });
            pool.Add(new GameEvent {
                id = "fox_sighting", threatTier = 1,
                title = "여우 출현",
                description = "붉은 여우가 숲 가장자리에서 야영지를 지켜본다. 두려워하지 않는다.",
                flavor = "발견되어도 도망가지 않는다.",
            });
            pool.Add(new GameEvent {
                id = "minor_disease", threatTier = 1,
                title = "감기 유행",
                description = "콜로니스트 한 명이 기침을 시작했다. 며칠 안에 다른 사람들에게도 옮길 수 있다.",
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
            pool.Add(new GameEvent {
                id = "food_blight", threatTier = 2,
                title = "역병",
                description = "농작물 일부가 시들어가고 있다. 수확량 감소.",
                flavor = "잎이 검게 변해간다.",
            });

            // Day 73: tier 3 (critical) — large raids, sieges
            pool.Add(new GameEvent {
                id = "large_raid", threatTier = 3,
                title = "대규모 약탈단",
                description = "5명 이상의 무장 약탈자가 야영지를 향한다. 즉시 방어 준비.",
                flavor = "북소리가 점점 가까워진다.",
            });
            pool.Add(new GameEvent {
                id = "siege_camp", threatTier = 3,
                title = "포위 작전",
                description = "적이 외곽에 진을 치고 야영지를 포위했다. 장기전이 될 것이다.",
                flavor = "그들의 모닥불이 밤마다 더 가까워진다.",
            });
            pool.Add(new GameEvent {
                id = "infestation", threatTier = 3,
                title = "벌레떼 출몰",
                description = "거대한 곤충떼가 동굴에서 기어 나왔다. 격렬한 전투가 예상된다.",
                flavor = "땅이 흔들렸고 — 그게 시작이었다.",
            });
        }
    }
}
