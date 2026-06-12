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
            // UI겹침 P1-4 — 건축 열림 시 시프트된 인포패널(272~652) 위에 떠서 본문을
            //  가리던 것: (400,58) → (660,108).  커플링 A — (272,314) 금지.
            var amS = ArchitectMenu.Instance;
            var crt = container.GetComponent<RectTransform>();
            if (crt != null)
            {
                var wantS = (amS != null && amS.IsOpen) ? new Vector2(660f, 108f) : new Vector2(400f, 58f);
                if (!Mathf.Approximately(crt.anchoredPosition.x, wantS.x)
                    || !Mathf.Approximately(crt.anchoredPosition.y, wantS.y))
                    crt.anchoredPosition = wantS;
            }
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
