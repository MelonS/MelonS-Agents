using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>Shows GameOver panel + final score when detector fires.</summary>
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text finalScore;

        private void Awake() { if (panel != null) panel.SetActive(false); }

        private void Update()
        {
            if (!GameOverDetector.IsGameOver) return;
            if (panel != null && !panel.activeSelf)
            {
                panel.SetActive(true);
                if (finalScore != null && ScoreManager.Instance != null)
                    finalScore.text = "Final: " + ScoreManager.Instance.Score;
                if (AudioBank.Instance != null) AudioBank.Instance.PlayGameOver();
            }
        }
    }
}
