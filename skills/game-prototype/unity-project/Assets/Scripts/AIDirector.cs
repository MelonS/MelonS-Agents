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
                title = "A wanderer arrives",
                description = "A traveler appears at the edge of the camp, looking for shelter.",
                flavor = "Their boots are caked with mud from the long road.",
            });
            pool.Add(new GameEvent {
                id = "storm_warning",
                title = "Storm warning",
                description = "Heavy clouds gather to the north. A storm will hit within the hour.",
                flavor = "The wind already smells of rain.",
            });
            pool.Add(new GameEvent {
                id = "lucky_find",
                title = "Lucky find",
                description = "One of the colonists found a small cache of supplies in the woods.",
                flavor = "Tools, neatly wrapped in oiled cloth.",
            });
            pool.Add(new GameEvent {
                id = "morale_dip",
                title = "Morale dip",
                description = "The colonists seem listless today. Something is on their minds.",
                flavor = "Conversation at supper was sparse and short.",
            });
            pool.Add(new GameEvent {
                id = "bird_omen",
                title = "Birds gather",
                description = "Crows have been gathering in the dead oak since dawn.",
                flavor = "Old folk would say it means change is coming.",
            });
            pool.Add(new GameEvent {
                id = "good_harvest",
                title = "Good harvest",
                description = "The wood chopped this morning split cleaner than usual.",
                flavor = "Dry, dense, and honest grain.",
            });
            pool.Add(new GameEvent {
                id = "fox_sighting",
                title = "Fox at the treeline",
                description = "A red fox watches the camp from the treeline, unafraid.",
                flavor = "It does not run when noticed.",
            });
            pool.Add(new GameEvent {
                id = "quiet_evening",
                title = "Quiet evening",
                description = "The day winds down with no incident, only the crackle of the fire.",
                flavor = "These are the days that pass without history.",
            });
        }
    }
}
