using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    [RequireComponent(typeof(Text))]
    public class ScoreUI : MonoBehaviour
    {
        private Text txt;
        private int lastShown = -1;

        private void Awake() { txt = GetComponent<Text>(); }

        private void Update()
        {
            int cur = GameManager.Instance != null ? GameManager.Instance.Score : 0;
            if (cur == lastShown) return;
            if (txt != null) txt.text = "Score: " + cur;
            lastShown = cur;
        }
    }
}
