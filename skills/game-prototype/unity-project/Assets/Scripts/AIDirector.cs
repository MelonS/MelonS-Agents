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

    /// <summary>Day 73 — Storyteller personality affects event frequency + threat curve.</summary>
    public enum Storyteller
    {
        Cassandra,  // 꾸준한 사건 — 일정 간격, 위협도 단계적 상승
        Phoebe,     // 평온한 시간 — 사건 드물게, 위협도 천천히
        Randy,      // 무작위 — 사건 자주 + 위협도 변동 큼
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

        [Header("Day 73: Storyteller (3 종류)")]
        [SerializeField] public Storyteller activeStoryteller = Storyteller.Cassandra;

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
                // Cassandra: 일정 (3/7/14일 임계점)
                if (activeStoryteller == Storyteller.Cassandra)
                {
                    if (day >= 14) return 3;
                    if (day >= 7) return 2;
                    if (day >= 3) return 1;
                    return 0;
                }
                // Phoebe: 느림 (6/14/25일)
                if (activeStoryteller == Storyteller.Phoebe)
                {
                    if (day >= 25) return 3;
                    if (day >= 14) return 2;
                    if (day >= 6) return 1;
                    return 0;
                }
                // Randy: 랜덤 — day 와 무관, 0..3 random
                return UnityEngine.Random.Range(0, 4);
            }
        }

        // Day 13 raid scheduling
        private int lastRaidDay = -1;
        [SerializeField] private float raidSpawnRadius = 12f;

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
            if (day < 3) return;
            if (day % 3 != 0) return;
            if (hour != 6) return;
            if (lastRaidDay == day) return; // throttle: one raid per day
            lastRaidDay = day;
            SpawnRaid();
        }

        private void SpawnRaid()
        {
            // Lesson #8 firewall: wrap raid spawn in try/catch so a single
            // raid failure (missing sprite asset, scene-rebuild race, etc.)
            // never tears down the whole simulation.  qa.py crash at Day 3
            // 06:00 (2026-05-27) traced back to silent NRE in this path
            // when Resources/pawn_colonist was absent AND no PawnEntity
            // existed to fall back to.
            try
            {
                // Pick a random side of the square map edge (radius raidSpawnRadius)
                // and a random point along that side.
                int side = UnityEngine.Random.Range(0, 4);
                float along = UnityEngine.Random.Range(-raidSpawnRadius, raidSpawnRadius);
                Vector3 pos;
                switch (side)
                {
                    case 0: pos = new Vector3( raidSpawnRadius, along, 0); break;
                    case 1: pos = new Vector3(-raidSpawnRadius, along, 0); break;
                    case 2: pos = new Vector3(along,  raidSpawnRadius, 0); break;
                    default: pos = new Vector3(along, -raidSpawnRadius, 0); break;
                }

                GameObject go = new GameObject("Bandit_Raid");
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
                Debug.Log($"[AIDirector] RAID day={clockDayForLog()} pos={pos}");
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
            // Day 73: storyteller마다 간격 다름
            float min = minIntervalSec, max = maxIntervalSec;
            if (activeStoryteller == Storyteller.Phoebe)
            {
                min *= 2f; max *= 2f;  // 평온한 시간 — 사건 드물게
            }
            else if (activeStoryteller == Storyteller.Randy)
            {
                min *= 0.6f; max *= 0.6f;  // 무작위 — 사건 자주
            }
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
            // Day 73: 현재 tier 이하 이벤트만 선택 (Cassandra/Phoebe) 또는
            //  완전 무작위 (Randy)
            int curTier = CurrentThreatTier;
            List<GameEvent> candidates = new List<GameEvent>();
            foreach (var ev in pool)
            {
                if (activeStoryteller == Storyteller.Randy || ev.threatTier <= curTier)
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
            Debug.Log($"[AIDirector:{activeStoryteller} T{curTier}] {next.title}: {next.description}");
            // Stretch: trader_caravan event → actual Trader entity spawn
            if (next.id == "trader_caravan") SpawnTrader();
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
            pool.Add(new GameEvent {
                id = "wolf_pack", threatTier = 2,
                title = "늑대 무리",
                description = "굶주린 늑대 무리가 야영지 외곽을 어슬렁거린다.",
                flavor = "송곳니가 달빛에 번뜩였다.",
            });
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
