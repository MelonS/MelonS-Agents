using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    [RequireComponent(typeof(Text))]
    public class GameOverUI : MonoBehaviour
    {
        private Text txt;
        private void Awake() { txt = GetComponent<Text>(); txt.enabled = false; }
        private void Update()
        {
            if (GameManager.Instance == null) return;
            txt.enabled = GameManager.Instance.IsGameOver;
        }
    }
}
