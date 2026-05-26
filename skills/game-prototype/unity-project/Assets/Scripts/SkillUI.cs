using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>Day 21: 4 skill levels shown for selected pawn.
    /// Reads ClickSelector.CurrentSelection.GetComponent<PawnSkills>().
    /// Hides when no pawn selected (same pattern as PawnInfoPanel).</summary>
    public class SkillUI : MonoBehaviour
    {
        [SerializeField] private ClickSelector selector;
        [SerializeField] private Text gatherText;
        [SerializeField] private Text chopText;
        [SerializeField] private Text buildText;
        [SerializeField] private Text combatText;
        [SerializeField] private GameObject container;

        private void Update()
        {
            if (selector == null || container == null) return;
            var pawn = selector.CurrentSelection;
            bool any = pawn != null && !pawn.IsDead;
            container.SetActive(any);
            if (!any) return;
            var sk = pawn.GetComponent<PawnSkills>();
            if (sk == null) return;
            SetText(gatherText, "채집", sk.GetLevel(SkillKind.Gather));
            SetText(chopText,   "벌목", sk.GetLevel(SkillKind.Chop));
            SetText(buildText,  "건축", sk.GetLevel(SkillKind.Build));
            SetText(combatText, "전투", sk.GetLevel(SkillKind.Combat));
        }

        private static void SetText(Text t, string label, int level)
        {
            if (t == null) return;
            t.text = $"{label}: Lv {level}";
        }
    }
}
