using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// A single colonist (pawn) entity.  Day 1 = idle stand, click-to-select.
    /// Day 2+ will add: movement, needs (food/sleep/mood), utility AI.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PawnEntity : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string pawnName = "Colonist";

        [Header("Selection visual")]
        [SerializeField] private Color selectedOutlineColor = new Color(1f, 0.9f, 0.3f, 1f);
        [SerializeField] private Color unselectedColor = Color.white;

        private SpriteRenderer spriteRenderer;
        private bool selected;

        public string PawnName => pawnName;
        public bool IsSelected => selected;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyVisual();
        }

        public void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value;
            ApplyVisual();
            Debug.Log($"[Pawn:{pawnName}] selected={selected}");
        }

        private void ApplyVisual()
        {
            if (spriteRenderer == null) return;
            // Day 1: tint to indicate selection. Real outline shader = later.
            spriteRenderer.color = selected ? selectedOutlineColor : unselectedColor;
        }
    }
}
