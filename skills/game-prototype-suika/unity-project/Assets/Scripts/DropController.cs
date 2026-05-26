using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Click to drop a low-tier fruit at cursor X position.</summary>
    public class DropController : MonoBehaviour
    {
        [SerializeField] private GameObject[] lowTierPrefabs;
        [SerializeField] private float dropY = 4f;
        [SerializeField] private float dropMinX = -2.5f;
        [SerializeField] private float dropMaxX = 2.5f;
        [SerializeField] private float spawnCooldown = 0.3f;
        private float lastSpawnTime = -10f;
        private int nextTierIdx = 0;
        private Camera cam;

        private void Awake() { cam = Camera.main; }

        private void Update()
        {
            if (GameOverDetector.IsGameOver) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (Time.time - lastSpawnTime < spawnCooldown) return;
            if (lowTierPrefabs == null || lowTierPrefabs.Length == 0) return;
            if (cam == null) cam = Camera.main;

            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            float x = Mathf.Clamp(mw.x, dropMinX, dropMaxX);
            GameObject prefab = lowTierPrefabs[nextTierIdx % lowTierPrefabs.Length];
            Instantiate(prefab, new Vector3(x, dropY, 0), Quaternion.identity);

            lastSpawnTime = Time.time;
            nextTierIdx = Random.Range(0, lowTierPrefabs.Length);
        }
    }
}
