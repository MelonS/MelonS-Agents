using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Bottom-left UI panel showing selected pawn's needs as bars.
    /// Subscribes to ClickSelector's selection events.  Day 2 = static
    /// panel, always rendered, refreshes per-frame.
    /// </summary>
    public class PawnInfoPanel : MonoBehaviour
    {
        [SerializeField] private ClickSelector selector;
        [SerializeField] private Text titleText;
        [SerializeField] private Image foodBar;
        [SerializeField] private Image sleepBar;
        [SerializeField] private Image moodBar;
        [SerializeField] private Text emptyText;
        [SerializeField] private Image panelBg;

        private void Update()
        {
            if (selector == null)
            {
                Debug.LogWarning("[PawnInfoPanel] no selector");
                return;
            }

            PawnEntity pawn = selector.CurrentSelection;
            bool any = (pawn != null);

            if (titleText != null) titleText.gameObject.SetActive(any);
            // foodBar.transform.parent = BarBg image GameObject.
            // BarBg.parent = the Row container that ALSO holds the label.
            // We toggle the row (grandparent) so the label vanishes too —
            // otherwise "Food/Sleep/Mood" labels remain visible when no
            // pawn is selected (Day 15 leftover).
            if (foodBar  != null) foodBar.transform.parent.parent.gameObject.SetActive(any);
            if (sleepBar != null) sleepBar.transform.parent.parent.gameObject.SetActive(any);
            if (moodBar  != null) moodBar.transform.parent.parent.gameObject.SetActive(any);
            if (emptyText != null) emptyText.gameObject.SetActive(!any);
            // Day 15: collapse panel background when no pawn — show only
            // the empty-text hint, no dark rectangle.
            if (panelBg != null) panelBg.enabled = any;

            if (!any) return;

            PawnNeeds needs = pawn.GetComponent<PawnNeeds>();
            if (titleText != null) titleText.text = pawn.PawnName;
            if (needs == null) return;

            if (foodBar  != null) foodBar.fillAmount  = needs.GetNormalized(NeedType.Food);
            if (sleepBar != null) sleepBar.fillAmount = needs.GetNormalized(NeedType.Sleep);
            if (moodBar  != null) moodBar.fillAmount  = needs.GetNormalized(NeedType.Mood);
        }
    }
}
