using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Day 17: a built wall.  Has collider so pawns + bandits
    /// route around it.  Could be extended later (HP, dismantle).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class WallEntity : MonoBehaviour
    {
        // Placeholder for future Day-22+ "cover" pattern (drafted combat).
        public bool ProvidesCover => true;
    }
}
