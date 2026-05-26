using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Day 18: built floor tile.  No collider — pawns walk over.
    /// Future: terrain modifier (walk speed boost, mood bonus indoors).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FloorEntity : MonoBehaviour
    {
        public bool IsIndoor => true;
    }
}
