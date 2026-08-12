using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M6-03 B4 — wiki Dimension 4 (건축) row B4:
    ///   "Barricade/sandbag buildable (defense cover marker) — vanilla Security
    ///    tab; visual+pathing only (no cover-math, that's gated)."
    ///   Acceptance (binary): a sandbag/barricade is buildable from Architect
    ///   (Security category) and pawns PATH AROUND it like a low wall — a pawn
    ///   walking from one side to the other detours rather than passing straight
    ///   through.
    ///
    /// The placed barricade — a thin marker component in the build-catalogue
    /// family.  It is the DELIBERATE OPPOSITE of <see cref="FenceEntity"/>:
    ///
    ///   FenceEntity  : LOW + PASSABLE  — NO pathing block (no collider, never
    ///                  registers a PathGrid cell).  A pawn walks straight
    ///                  through a fence.
    ///   BarricadeEntity (this) : LOW + IMPASSABLE-to-pathing — it BLOCKS its
    ///                  cell for pathfinding exactly the way a WALL does, so a
    ///                  pawn detours around it.  It reads visually LOW (a piled
    ///                  sandbag row, drawn in the lower half of the tile) but it
    ///                  is a hard pathing obstacle, mirroring how the reference sim
    ///                  sandbags fill their cell as impassable cover.
    ///
    /// PATHING BLOCK — the load-bearing property of this row:
    ///   Pathfinding in this prototype is decided by the PathGrid (AStar reads
    ///   PathGrid.IsBlocked per cell).  A WALL marks its cell impassable via
    ///   PawnMovement.RegisterWallCell(worldPos) → PathGrid.SetStructureBlocked
    ///   (cell, true), and releases it on destroy.  This barricade REUSES that
    ///   EXACT existing wall pathing-block API READ-ONLY — it invents NO new
    ///   pathfinding.  It mirrors WallEntity's register/eject/release lifecycle
    ///   line-for-line so a barricade and a wall are indistinguishable to the
    ///   pathfinder (a pawn paths AROUND both), while remaining a separate,
    ///   cheaper, LOW-reading Security-tab entity.
    ///
    /// LOW BARRIER — the visual read:
    ///   The barricade renders LOW.  Its sprite is a short piled sandbag row
    ///   (drawn in the lower ~half of the 16×16 tile by _gen_barricade.py), and
    ///   its SpriteRenderer sits at sortingOrder 2 — above ground/floor tiles
    ///   (floors are 1) but BELOW furniture/wall bodies (those are 5+).  So a
    ///   barricade reads as a low waist-height defensive line, never as a full
    ///   wall — even though it blocks pathing like one.
    ///
    /// NO cover-math:
    ///   the reference sim barricades give a 55% ranged cover bonus.  Cover math /
    ///   accuracy-reduction is explicitly OUT OF SCOPE / OP-gated for this lane —
    ///   this component carries NONE.  It is visual + pathing only.  A future
    ///   cover wave can add accuracy math without touching this file.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BarricadeEntity : MonoBehaviour
    {
        /// <summary>
        /// A barricade always blocks pathing — pawns detour around it like a low
        /// wall.  Exposed read-only so any future pathing/cover consumer can
        /// assert the contract in one place rather than re-deriving it.  This is
        /// the DELIBERATE inverse of FenceEntity.IsPassable (which is true).
        /// </summary>
        public bool IsPassable => false;

        // Mirrors WallEntity's pathing-block lifecycle EXACTLY (READ-ONLY use of
        //  the existing wall pathing-block API — no new pathfinding invented).
        //  Registered in Start (NOT Awake) so the PathGrid built in
        //  TilemapStaticRefInit.Awake is guaranteed to exist (all Awakes run
        //  before any Start).  Runtime-spawned barricades (blueprint complete)
        //  register the frame after Instantiate — same Start path, no special
        //  casing.  We cache the cell we registered so OnDestroy clears the SAME
        //  cell, keeping the PathGrid ref-count balanced (never double-clears,
        //  never leaks a blocked cell).
        private bool _cellRegistered;
        private Vector2Int _registeredCell;

        private void Start()
        {
            _registeredCell = AI.PathGrid.WorldToCell(transform.position);
            // Block this cell for pathfinding — a pawn now paths AROUND the
            //  barricade (the B4 acceptance).  Same call WallEntity uses.
            PawnMovement.RegisterWallCell(transform.position);
            _cellRegistered = true;
            // the reference sim eject-on-block: the barricade just made its cell
            //  impassable.  Any pawn standing in that cell would be sealed on a
            //  blocked START cell (AStar refuses a blocked start), so push it out
            //  to the nearest open neighbour — exactly like a freshly-built wall.
            //  Called AFTER RegisterWallCell so the cell already reads blocked and
            //  the ejected pawn lands on a genuinely-open neighbour.
            PawnMovement.EjectPawnsFromCell(transform.position);
        }

        private void OnDestroy()
        {
            // A destroyed barricade reopens its cell — pawns immediately path
            //  through the gap (PathGrid bumps Version → in-flight pawns re-path).
            //  Guard: only clear if we actually registered, and clear the cached
            //  cell so the ref-count stays balanced.  Same path as WallEntity.
            if (!_cellRegistered) return;
            if (PawnMovement.Grid != null)
                PawnMovement.Grid.SetStructureBlocked(_registeredCell, false);
            _cellRegistered = false;
        }
    }
}
