using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>If any fruit stays above gameOverY for graceTime, set
    /// IsGameOver = true.</summary>
    public class GameOverDetector : MonoBehaviour
    {
        public static bool IsGameOver = false;

        [SerializeField] private float gameOverY = 3.5f;
        [SerializeField] private float graceTime = 1.5f;
        private float aboveLineTimer = 0f;

        private void Start()
        {
            IsGameOver = false;
            aboveLineTimer = 0f;
        }

        private void Update()
        {
            if (IsGameOver) return;
            bool anyAbove = false;
            var fruits = Object.FindObjectsByType<Fruit>(FindObjectsSortMode.None);
            foreach (var f in fruits)
            {
                if (f == null || f.merged || f.justSpawned) continue;
                if (f.transform.position.y > gameOverY) { anyAbove = true; break; }
            }
            if (anyAbove) aboveLineTimer += Time.deltaTime;
            else aboveLineTimer = 0f;
            if (aboveLineTimer >= graceTime) IsGameOver = true;
        }
    }
}
