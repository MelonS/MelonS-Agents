using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Day 18: built door.  Has collider but allows PawnEntity
    /// to pass through (trigger).  Bandits also pass through (Day 18
    /// minimal — Day 22+ can add faction filtering).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class DoorEntity : MonoBehaviour
    {
        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;
        }
    }
}
