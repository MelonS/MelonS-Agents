using UnityEngine;
using UnityEngine.Tilemaps;

namespace MelonS.GameProto
{
    /// <summary>
    /// Step 81 — Initializes PawnMovement.GroundTilemap / WaterTile / RockTile
    /// at runtime Start.  Statics don't persist from Editor batchmode →
    /// runtime build.  This component is added to the Game scene by SceneSetup.
    /// </summary>
    public class TilemapStaticRefInit : MonoBehaviour
    {
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private TileBase waterTile;
        [SerializeField] private TileBase rockTile;

        public void SetRefs(Tilemap gt, TileBase wt, TileBase rt)
        {
            groundTilemap = gt;
            waterTile = wt;
            rockTile = rt;
        }

        private void Awake()
        {
            // Earliest possible — before PawnMovement.Update runs
            PawnMovement.GroundTilemap = groundTilemap;
            PawnMovement.WaterTile = waterTile;
            PawnMovement.RockTile = rockTile;
            Debug.Log($"[TilemapStaticRefInit] ground={(groundTilemap!=null)} water={(waterTile!=null)} rock={(rockTile!=null)}");
        }
    }
}
