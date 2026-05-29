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

        // #UI-restyle U5 — runtime border frame so this panel matches the global
        //   bordered-panel system (Editor builder only made the flat fill).  Added
        //   lazily as 4 Divider-colored edge Images; toggled with the panel.
        private Image[] borderEdges;

        private void EnsureBorder()
        {
            if (borderEdges != null || panelBg == null) return;
            var prt = panelBg.GetComponent<RectTransform>();
            if (prt == null) { borderEdges = new Image[0]; return; }
            float t = MelonS.GameProto.Core.UITheme.BorderPx;
            borderEdges = new Image[4];
            // top, bottom, left, right
            borderEdges[0] = MakeEdge("BorderTop",    prt, new Vector2(0,1), new Vector2(1,1), new Vector2(0,t),  new Vector2(0, 0));
            borderEdges[1] = MakeEdge("BorderBottom", prt, new Vector2(0,0), new Vector2(1,0), new Vector2(0,t),  new Vector2(0, 0));
            borderEdges[2] = MakeEdge("BorderLeft",   prt, new Vector2(0,0), new Vector2(0,1), new Vector2(t,0),  new Vector2(0, 0));
            borderEdges[3] = MakeEdge("BorderRight",  prt, new Vector2(1,0), new Vector2(1,1), new Vector2(t,0),  new Vector2(0, 0));
        }

        private Image MakeEdge(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;   // 0 on the stretched axis, thickness on the other
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = MelonS.GameProto.Core.UITheme.Divider;
            img.raycastTarget = false;
            return img;
        }

        private void SetBorderVisible(bool v)
        {
            if (borderEdges == null) return;
            foreach (var e in borderEdges) if (e != null) e.enabled = v;
        }

        private void Update()
        {
            EnsureBorder();
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
            SetBorderVisible(any);  // #UI-restyle U5 — border shows only when populated

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

                    // #120 - 능력치 (PawnAbilities)
                    var abil = pawn.GetComponent<PawnAbilities>();
                    if (abil != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine("<color=#ddc28a>능력치:</color>");
                        foreach (var (key, label) in PawnAbilities.DisplayMap)
                        {
                            float v = abil.GetByKey(key);
                            // 1.0 기준 색상 - 좋음 녹색, 평균 옅음, 나쁨 빨강
                            string col = v >= 1.10f ? "#9adb86" : (v >= 0.95f ? "#dddddd" : "#e88c54");
                            sb.AppendLine($"  <color={col}>{label}: {v:F2}</color>");
                        }
                    }

                    // #122 - 기분 thoughts (림월드 breakdown 패턴)
                    var thoughts = pawn.GetComponent<PawnThoughts>();
                    if (thoughts != null && thoughts.active.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("<color=#ddc28a>기분:</color>");
                        foreach (var t in thoughts.active)
                        {
                            string col = t.offset >= 0f ? "#9adb86" : "#e88c54";
                            string sign = t.offset >= 0f ? "+" : "";
                            sb.AppendLine($"  <color={col}>{t.label} {sign}{t.offset:F0}</color>");
                        }
                    }

                    // #123 - 장비 (의류/무기)
                    var eq = pawn.GetComponent<PawnEquipment>();
                    if (eq != null && eq.equipped.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("<color=#ddc28a>장비:</color>");
                        string[] slotLabels = { "셔츠", "바지", "모자", "무기" };
                        var slots = (PawnEquipment.Slot[])System.Enum.GetValues(typeof(PawnEquipment.Slot));
                        for (int i = 0; i < slots.Length; i++)
                        {
                            var it = eq.GetEquipped(slots[i]);
                            string val = it != null ? it.nameKr : "(없음)";
                            string col = it != null ? "#dddddd" : "#888888";
                            sb.AppendLine($"  <color={col}>{slotLabels[i]}: {val}</color>");
                        }
                    }

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
