using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Top-right resource counter.  Day 3 shows wood + food.
    /// </summary>
    public class ResourceCounterUI : MonoBehaviour
    {
        [SerializeField] private Text woodText;
        [SerializeField] private Text foodText;

        private void OnEnable()
        {
            Refresh();
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.OnChanged += Refresh;
        }

        private void OnDisable()
        {
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.OnChanged -= Refresh;
        }

        private void Update()
        {
            // Defensive: subscribe late if RM was created after this
            if (ResourceManager.Instance != null && woodText != null)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (ResourceManager.Instance == null) return;
            if (woodText != null) woodText.text = $"목재: {ResourceManager.Instance.wood}";
            if (foodText != null) foodText.text = $"식량: {ResourceManager.Instance.food}";
        }
    }
}
