using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    [RequireComponent(typeof(Text))]
    public class HUD : MonoBehaviour
    {
        public enum Mode { HP, XP }
        public Mode mode = Mode.HP;
        private Text txt;
        private int lastShown = -1;

        private void Awake() { txt = GetComponent<Text>(); }

        private void Update()
        {
            int cur;
            if (mode == Mode.HP) cur = PlayerHealth.Instance != null ? PlayerHealth.Instance.Hp : 0;
            else cur = XPManager.Instance != null ? XPManager.Instance.XP : 0;
            if (cur == lastShown) return;
            if (txt != null) txt.text = (mode == Mode.HP ? "HP: " : "XP: ") + cur;
            lastShown = cur;
        }
    }
}
