using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>Top-right score display.  Polls ScoreManager.Instance every
    /// frame — avoids the Awake/OnEnable subscription-order race that would
    /// otherwise leave the UI stuck on 0 if ScoreManager.Awake fires after
    /// ScoreUI.OnEnable.</summary>
    [RequireComponent(typeof(Text))]
    public class ScoreUI : MonoBehaviour
    {
        private Text txt;
        private int lastShown = -1;

        private void Awake()
        {
            txt = GetComponent<Text>();
            Refresh(0);
        }

        private void Update()
        {
            int cur = ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0;
            if (cur != lastShown) Refresh(cur);
        }

        private void Refresh(int s)
        {
            if (txt != null) txt.text = "Score: " + s;
            lastShown = s;
        }
    }
}
