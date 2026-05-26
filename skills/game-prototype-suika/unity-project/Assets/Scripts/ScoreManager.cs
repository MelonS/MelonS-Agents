using System;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Singleton score state for Suika.</summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }
        public int Score { get; private set; }
        public event Action<int> OnScoreChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void AddScore(int delta)
        {
            Score += delta;
            OnScoreChanged?.Invoke(Score);
        }

        public void ResetScore()
        {
            Score = 0;
            OnScoreChanged?.Invoke(Score);
        }
    }
}
