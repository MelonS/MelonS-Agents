using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Escalating-rate enemy spawner.</summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject[] enemyPrefabs;
        [SerializeField] private float startDelay = 2f;
        [SerializeField] private float startRate = 0.6f;
        [SerializeField] private float rateStep = 0.15f;
        [SerializeField] private float escalationInterval = 20f;
        [SerializeField] private int maxAlive = 80;
        [SerializeField] private float spawnRingRadius = 9f;

        private float nextSpawnAt;
        private float nextEscalationAt;
        private float currentRate;
        private readonly List<GameObject> alive = new();

        public int AliveCount => alive.Count;

        public void SetEnemyPrefabs(GameObject[] prefabs) { enemyPrefabs = prefabs; }

        private void Start()
        {
            currentRate = startRate;
            nextSpawnAt = Time.time + startDelay;
            nextEscalationAt = Time.time + escalationInterval;
        }

        private void Update()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
                if (alive[i] == null) alive.RemoveAt(i);

            if (Time.time >= nextEscalationAt)
            {
                currentRate += rateStep;
                nextEscalationAt = Time.time + escalationInterval;
            }
            if (Time.time < nextSpawnAt) return;
            if (alive.Count >= maxAlive) { nextSpawnAt = Time.time + 0.5f; return; }
            if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;

            Vector3 center = PlayerMovement.Instance != null
                ? PlayerMovement.Instance.transform.position : Vector3.zero;
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector3 pos = center + new Vector3(Mathf.Cos(a) * spawnRingRadius, Mathf.Sin(a) * spawnRingRadius, 0);

            GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            var go = Instantiate(prefab, pos, Quaternion.identity);
            alive.Add(go);
            nextSpawnAt = Time.time + (1f / Mathf.Max(0.01f, currentRate));
        }
    }
}
