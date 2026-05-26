using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>Top-right score display.</summary>
    [RequireComponent(typeof(Text))]
    public class ScoreUI : MonoBehaviour
    {
        private Text txt;

        private void Awake()
        {
            txt = GetComponent<Text>();
            Refresh(ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0);
        }

        private void OnEnable()
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnScoreChanged += Refresh;
        }

        private void OnDisable()
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnScoreChanged -= Refresh;
        }

        private void Refresh(int s)
        {
            if (txt != null) txt.text = "Score: " + s;
        }
    }
}
