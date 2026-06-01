using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MelonS.GameProto
{
    [Serializable]
    public class PawnSave
    {
        public string name;
        public Vector2 position;
        public float food;
        public float sleep;
        public float mood;
    }

    [Serializable]
    public class TreeSave
    {
        public Vector2 position;
        // M5 serialization fix (#29): tree species survives save->load.
        // int cast mirrors BedSave/WallSave pattern; cast back to TreeSpecies on Load.
        public int species;
    }

    // M5 serialization fix (#29) — new save records for entity sub-states.
    // Wiki acceptance #29: fine bed stays fine / stockpile priority / tree species
    // / wall material all survive a save->load round-trip.

    [Serializable]
    public class BedSave
    {
        public Vector2 position;
        // int cast of BedQuality enum — JsonUtility serializes int reliably.
        public int quality;
    }

    [Serializable]
    public class StockpileSave
    {
        // StockpileZoneEntity is a single-cell zone; position identifies the instance.
        public Vector2 position;
        // int cast of StockpilePriority enum.
        public int priority;
    }

    [Serializable]
    public class WallSave
    {
        public Vector2 position;
        // int cast of WallMaterial enum.
        public int material;
    }

    [Serializable]
    public class SaveData
    {
        public int wood;
        public int food;
        public List<PawnSave>      pawns      = new List<PawnSave>();
        public List<TreeSave>      trees      = new List<TreeSave>();
        // M5 serialization fix (#29): added beds/stockpiles/walls lists.
        public List<BedSave>       beds       = new List<BedSave>();
        public List<StockpileSave> stockpiles = new List<StockpileSave>();
        public List<WallSave>      walls      = new List<WallSave>();
        public string version = "0.2.0";
        public string savedAtIso;
    }

    /// <summary>
    /// JSON save/load to Application.persistentDataPath/save.json.
    /// Day 6 minimal: pawns + trees + resources.  No tilemap state
    /// (regenerated deterministically on Load).
    ///
    /// M5 fix (#29): beds (quality), stockpiles (priority), trees (species),
    /// walls (material) are now serialized and re-applied on Load.
    /// All four entity types expose public SetX() setters used here on Load.
    /// </summary>
    public static class SaveLoadManager
    {
        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "save.json");

        public static bool SaveExists => File.Exists(SavePath);

        public static void Save()
        {
            SaveData data = new SaveData
            {
                savedAtIso = DateTime.UtcNow.ToString("o"),
            };

            if (ResourceManager.Instance != null)
            {
                data.wood = ResourceManager.Instance.wood;
                data.food = ResourceManager.Instance.food;
            }

            foreach (var pawn in UnityEngine.Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                PawnNeeds needs = pawn.GetComponent<PawnNeeds>();
                data.pawns.Add(new PawnSave
                {
                    name     = pawn.PawnName,
                    position = pawn.transform.position,
                    food     = needs != null ? needs.food  : 80f,
                    sleep    = needs != null ? needs.sleep : 80f,
                    mood     = needs != null ? needs.mood  : 80f,
                });
            }

            foreach (var tree in UnityEngine.Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None))
            {
                if (tree.IsDestroyed) continue;
                data.trees.Add(new TreeSave
                {
                    position = tree.transform.position,
                    species  = (int)tree.Species,
                });
            }

            // M5 serialization fix (#29): serialize bed quality sub-state.
            foreach (var bed in UnityEngine.Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None))
            {
                data.beds.Add(new BedSave
                {
                    position = bed.transform.position,
                    quality  = (int)bed.Quality,
                });
            }

            // M5 serialization fix (#29): serialize stockpile priority sub-state.
            foreach (var zone in UnityEngine.Object.FindObjectsByType<StockpileZoneEntity>(FindObjectsSortMode.None))
            {
                data.stockpiles.Add(new StockpileSave
                {
                    position = zone.transform.position,
                    priority = (int)zone.Priority,
                });
            }

            // M5 serialization fix (#29): serialize wall material sub-state.
            foreach (var wall in UnityEngine.Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None))
            {
                data.walls.Add(new WallSave
                {
                    position = wall.transform.position,
                    material = (int)wall.Material,
                });
            }

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[SaveLoad] saved -> {SavePath} " +
                      $"(pawns={data.pawns.Count} trees={data.trees.Count} " +
                      $"beds={data.beds.Count} stockpiles={data.stockpiles.Count} " +
                      $"walls={data.walls.Count})");
        }

        public static SaveData Load()
        {
            if (!SaveExists)
            {
                Debug.LogWarning("[SaveLoad] no save file");
                return null;
            }
            // #276 손상 save 견고성 — 파싱 실패/빈 데이터에 크래시·부분초기화 대신 안전 실패.
            SaveData data;
            try
            {
                string json = File.ReadAllText(SavePath);
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveLoad] JSON 파싱 실패 — 로드 취소: {e.Message}");
                return null;
            }
            if (data == null || data.pawns == null)
            {
                Debug.LogError("[SaveLoad] save 데이터 손상(null) — 로드 취소");
                return null;
            }
            Debug.Log($"[SaveLoad] loaded (pawns={data.pawns.Count} trees={data.trees.Count} " +
                      $"beds={data.beds.Count} stockpiles={data.stockpiles.Count} " +
                      $"walls={data.walls.Count} wood={data.wood})");
            return data;
        }

        // M5 serialization fix (#29): re-apply serialized sub-states onto scene entities
        // after a full scene-reload rebuild.  Call this AFTER the scene has been
        // reconstructed from SaveData (pawns/trees/resources are your caller's
        // responsibility; call this for the four new sub-state lists).
        //
        // Match strategy: nearest-entity within 0.6 world units of saved position.
        // This mirrors the pre-existing pawn/tree rebuild pattern where positions are
        // the stable identifier (no GUID system exists yet).
        //
        // All four entity types expose public SetX() setters (verified in W-M4-02).
        // No entity .cs files were widened — all setters were pre-existing.
        public static void ApplyLoadedSubStates(SaveData data)
        {
            if (data == null) return;

            const float kMatchRadius = 0.6f;

            // Re-apply BedEntity quality.
            if (data.beds != null && data.beds.Count > 0)
            {
                var sceneBeds = UnityEngine.Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None);
                foreach (var bs in data.beds)
                {
                    BedEntity best = FindNearest(sceneBeds, bs.position, kMatchRadius,
                        b => (Vector2)b.transform.position);
                    if (best != null)
                        best.SetQuality((BedQuality)bs.quality);
                }
            }

            // Re-apply StockpileZoneEntity priority.
            if (data.stockpiles != null && data.stockpiles.Count > 0)
            {
                var sceneZones = UnityEngine.Object.FindObjectsByType<StockpileZoneEntity>(FindObjectsSortMode.None);
                foreach (var ss in data.stockpiles)
                {
                    StockpileZoneEntity best = FindNearest(sceneZones, ss.position, kMatchRadius,
                        z => (Vector2)z.transform.position);
                    if (best != null)
                        best.SetPriority((StockpilePriority)ss.priority);
                }
            }

            // Re-apply TreeEntity species.
            if (data.trees != null && data.trees.Count > 0)
            {
                var sceneTrees = UnityEngine.Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None);
                foreach (var ts in data.trees)
                {
                    TreeEntity best = FindNearest(sceneTrees, ts.position, kMatchRadius,
                        t => (Vector2)t.transform.position);
                    if (best != null)
                        best.SetSpecies((TreeSpecies)ts.species);
                }
            }

            // Re-apply WallEntity material.
            if (data.walls != null && data.walls.Count > 0)
            {
                var sceneWalls = UnityEngine.Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None);
                foreach (var ws in data.walls)
                {
                    WallEntity best = FindNearest(sceneWalls, ws.position, kMatchRadius,
                        w => (Vector2)w.transform.position);
                    if (best != null)
                        best.SetMaterial((WallMaterial)ws.material);
                }
            }
        }

        private static T FindNearest<T>(T[] candidates, Vector2 target, float maxDist,
            System.Func<T, Vector2> posOf) where T : UnityEngine.Object
        {
            T best = null;
            float bestSq = maxDist * maxDist;
            foreach (var c in candidates)
            {
                if (c == null) continue;
                float sq = (posOf(c) - target).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = c; }
            }
            return best;
        }
    }
}
