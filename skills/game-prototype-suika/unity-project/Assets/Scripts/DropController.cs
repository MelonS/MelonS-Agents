using System;
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
        [SerializeField] private NextTierPreview preview;
        private float lastSpawnTime = -10f;
        public int NextTierIdx { get; private set; } = 0;
        public event Action<int> OnNextTierChanged;
        private Camera cam;

        private void Awake()
        {
            cam = Camera.main;
            NextTierIdx = UnityEngine.Random.Range(0, Mathf.Max(1, lowTierPrefabs == null ? 1 : lowTierPrefabs.Length));
        }

        private void Start()
        {
            NotifyPreview();
        }

        private void NotifyPreview()
        {
            OnNextTierChanged?.Invoke(NextTierIdx);
            if (preview != null) preview.SetTier(NextTierIdx);
        }

        private void Update()
        {
            if (GameOverDetector.IsGameOver) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (Time.time - lastSpawnTime < spawnCooldown) return;
            if (lowTierPrefabs == null || lowTierPrefabs.Length == 0) return;
            if (cam == null) cam = Camera.main;

            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            float x = Mathf.Clamp(mw.x, dropMinX, dropMaxX);
            GameObject prefab = lowTierPrefabs[NextTierIdx % lowTierPrefabs.Length];
            Instantiate(prefab, new Vector3(x, dropY, 0), Quaternion.identity);
            if (AudioBank.Instance != null) AudioBank.Instance.PlayDrop();

            lastSpawnTime = Time.time;
            NextTierIdx = UnityEngine.Random.Range(0, lowTierPrefabs.Length);
            NotifyPreview();
        }
    }
}
