using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M4-06 Lane A — wiki Dimension 4 (건축) #20:
    ///   "Add table+chair buildable (eat/rec spot)."
    ///
    /// The chair / stool half of the combined table+chair buildable.  In this
    /// prototype #20 is ONE build op producing a single 1×1 piece that reads as
    /// a table WITH a chair (the other catalogue furniture — stove, sleeping
    /// spot — is single-cell too), so BuildManager's lazy template adds BOTH a
    /// <see cref="TableEntity"/> and this ChairEntity to the same placed object.
    /// Splitting them into two marker components (rather than one combined
    /// marker) keeps the bed/bench entity convention — one MonoBehaviour per
    /// furniture concept — and lets a future wave separate them into two
    /// placeable pieces with ZERO change to consumers.
    ///
    /// Like LampEntity / StoveEntity this is a thin passive marker: it carries
    /// no per-frame state.  The "rec spot" value (a small mood lift for sitting)
    /// is exposed read-only here and granted through the EXISTING eat/rest mood
    /// path (see <see cref="TableEntity"/>'s doc) — no new need, no new dispatch.
    /// </summary>
    public class ChairEntity : MonoBehaviour
    {
        /// <summary>
        /// "Sat on a comfortable chair" recreation/comfort mood bonus.  Kept
        /// small (a chair is comfort flavour, not a core need) and serialized so
        /// the designer can tune it without a code change.  The eat-spot bonus
        /// proper lives on <see cref="TableEntity"/>; this is the chair's own
        /// minor comfort contribution if a future wave wants seating comfort.
        /// </summary>
        [SerializeField] private float chairComfortMoodBonus = 1f;

        public float ChairComfortMoodBonus => chairComfortMoodBonus;

        /// <summary>
        /// Read-only probe mirroring <see cref="TableEntity.EatSpotAt"/>: is
        /// there a chair at world position <paramref name="pos"/>?  Same tiny
        /// OverlapBox the furniture-under-pawn detectors use.  Lets the existing
        /// eat/rec path opt into the comfort bonus with one read-only line.
        /// </summary>
        public static ChairEntity ChairAt(Vector2 pos)
        {
            var hits = Physics2D.OverlapBoxAll(pos, Vector2.one * 0.4f, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var c = h.GetComponent<ChairEntity>();
                if (c != null) return c;
            }
            return null;
        }
    }
}
