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

        public string Formatted =>
            string.IsNullOrEmpty(flavor)
                ? $"<b>{title}</b>: {description}"
                : $"<b>{title}</b>: {description}\n<i>\"{flavor}\"</i>";
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

        [SerializeField] private float minIntervalSec = 15f;
        [SerializeField] private float maxIntervalSec = 30f;

        private float nextFireTime;
        private GameEvent lastEvent;
        private readonly List<GameEvent> pool = new List<GameEvent>();

        // Day 13 raid scheduling
        private int lastRaidDay = -1;
        [SerializeField] private float raidSpawnRadius = 12f;

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
                    var anyPawn = GameObject.FindObjectOfType<PawnEntity>();
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
            float wait = UnityEngine.Random.Range(minIntervalSec, maxIntervalSec);
            nextFireTime = Time.timeSinceLevelLoad + wait;
        }

        private void FireRandomEvent()
        {
            if (pool.Count == 0) return;
            GameEvent next;
            int tries = 0;
            do
            {
                next = pool[UnityEngine.Random.Range(0, pool.Count)];
                tries++;
            } while (next == lastEvent && tries < 5);
            lastEvent = next;
            OnEventFired?.Invoke(next);
            Debug.Log($"[AIDirector] {next.title}: {next.description}");
        }

        private void BuildDefaultPool()
        {
            pool.Clear();
            // Pre-seeded events.  Operator can later regenerate via
            // game-dev-agent's runtime_director module (LLM-generated
            // variants stored in Resources/events.json).
            pool.Add(new GameEvent {
                id = "wanderer_arrival",
                title = "방랑자 도착",
                description = "한 여행자가 야영지 외곽에 나타나 머물 곳을 찾고 있다.",
                flavor = "장화에 묻은 진흙이 그가 걸어온 길의 길이를 말해준다.",
            });
            pool.Add(new GameEvent {
                id = "storm_warning",
                title = "폭풍 경보",
                description = "북쪽에서 짙은 먹구름이 몰려온다. 한 시간 안에 폭풍이 닥칠 것이다.",
                flavor = "바람에서 벌써 빗냄새가 난다.",
            });
            pool.Add(new GameEvent {
                id = "lucky_find",
                title = "행운의 발견",
                description = "콜로니스트 한 명이 숲에서 작은 보급품 더미를 발견했다.",
                flavor = "기름천에 잘 싸인 도구들.",
            });
            pool.Add(new GameEvent {
                id = "morale_dip",
                title = "사기 저하",
                description = "오늘 콜로니스트들이 어딘가 무기력해 보인다.",
                flavor = "저녁 식탁의 대화가 짧고 띄엄띄엄했다.",
            });
            pool.Add(new GameEvent {
                id = "bird_omen",
                title = "새들의 징조",
                description = "새벽부터 까마귀들이 죽은 참나무 위로 모여들고 있다.",
                flavor = "노인들 말로는 무언가 변화의 조짐이라고 한다.",
            });
            pool.Add(new GameEvent {
                id = "good_harvest",
                title = "좋은 수확",
                description = "오늘 아침 베어낸 나무가 평소보다 깔끔하게 쪼개졌다.",
                flavor = "건조하고 단단한, 정직한 나뭇결.",
            });
            pool.Add(new GameEvent {
                id = "fox_sighting",
                title = "여우 출현",
                description = "붉은 여우가 숲 가장자리에서 야영지를 지켜본다. 두려워하지 않는다.",
                flavor = "발견되어도 도망가지 않는다.",
            });
            pool.Add(new GameEvent {
                id = "quiet_evening",
                title = "조용한 저녁",
                description = "별일 없이 하루가 저문다. 모닥불 타는 소리만 들린다.",
                flavor = "역사에 남지 않을, 그저 흘러가는 날들.",
            });
        }
    }
}
