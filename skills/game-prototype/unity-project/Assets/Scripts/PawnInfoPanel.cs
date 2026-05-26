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
        // Day 55: 부위별 health 표시 — 클릭 시 RimWorld vanilla 처럼.
        [SerializeField] private Text healthText;

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
            // Day 56: name + traits in title
            PawnTraits traits = pawn.GetComponent<PawnTraits>();
            if (titleText != null)
            {
                string title = pawn.PawnName;
                if (traits != null)
                {
                    string ts = traits.SummaryKr();
                    if (!string.IsNullOrEmpty(ts))
                        title += $"  <size=12><color=#c0b090>({ts})</color></size>";
                }
                titleText.text = title;
                titleText.supportRichText = true;
            }
            if (needs == null) return;

            if (foodBar  != null) foodBar.fillAmount  = needs.GetNormalized(NeedType.Food);
            if (sleepBar != null) sleepBar.fillAmount = needs.GetNormalized(NeedType.Sleep);
            if (moodBar  != null) moodBar.fillAmount  = needs.GetNormalized(NeedType.Mood);

            // Day 55: 부위별 health 표시 (한글)
            if (healthText != null)
            {
                PawnHealth health = pawn.GetComponent<PawnHealth>();
                if (health != null && health.parts != null)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("<color=#ddc28a>상태:</color>");
                    foreach (var part in health.parts)
                    {
                        float r = (float)part.hp / part.maxHp;
                        string color = r > 0.7f ? "#9adb86" : (r > 0.3f ? "#e8b454" : "#e85454");
                        string bleed = part.bleedRate > 0.1f ? " <color=#ff6464>출혈</color>" : "";
                        string bandage = part.bandaged ? " <color=#a0c8ff>붕대</color>" : "";
                        sb.AppendLine($"<color={color}>{part.nameKr}: {part.hp}/{part.maxHp}</color>{bleed}{bandage}");
                    }
                    if (health.IsDowned) sb.AppendLine("<color=#ff6464>의식불명</color>");
                    if (health.IsDead)   sb.AppendLine("<color=#ff0000>사망</color>");
                    healthText.text = sb.ToString();
                }
                else
                {
                    healthText.text = "";
                }
            }
        }
    }
}
