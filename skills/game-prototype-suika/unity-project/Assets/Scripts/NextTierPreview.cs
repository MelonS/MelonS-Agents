using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>Shows the next fruit's sprite + "NEXT" label.  Hooks into
    /// DropController.OnNextTierChanged to know what's coming.</summary>
    public class NextTierPreview : MonoBehaviour
    {
        [SerializeField] private Image previewImage;
        [SerializeField] private Text label;
        [SerializeField] private Sprite[] tierSprites; // index 0 = tier1, etc

        public void SetTier(int tierIdx)
        {
            if (previewImage == null) return;
            if (tierSprites == null || tierIdx < 0 || tierIdx >= tierSprites.Length) return;
            previewImage.sprite = tierSprites[tierIdx];
            previewImage.preserveAspect = true;
            if (label != null) label.text = "NEXT";
        }
    }
}
