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
        // #audit2 #15/#17 — 이전엔 needs(food/sleep/mood)만 저장돼, 로드 시 스킬 진행도와
        //  징집 상태가 전부 default 로 리셋됐다(progression 소실).  순서 고정(SkillKind
        //  Gather/Chop/Build/Combat = index 0~3)로 level+xp 를 저장/복원.  구(舊) 세이브엔
        //  이 필드가 없어 JsonUtility 가 초기값(빈 배열/false)을 두므로 로드 시 length 가드.
        public bool drafted;
        public int[] skillLevels;
        public float[] skillXp;
        // #save-load 완성(2026-06-04) — 부위별 HP/출혈/붕대(부상 상태)가 로드 시 전부 full 로
        //  리셋되던 것 복원.  index = PartId 0~5(Head..RightLeg).  구 세이브엔 이 필드가 없어
        //  JsonUtility 가 빈 배열을 두므로 PawnHealth.RestorePartState 가 길이 가드로 스킵한다.
        public int[] partHp;
        public float[] partBleed;
        public bool[] partBandaged;
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
        public float gameSeconds;   // #276 게임 시계 — 로드 시 시계 리셋(레이드 스케줄 파손) 방지
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
            if (GameClock.Instance != null)
                data.gameSeconds = GameClock.Instance.GameSeconds;   // #276

            foreach (var pawn in UnityEngine.Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                PawnNeeds needs = pawn.GetComponent<PawnNeeds>();
                var ps = new PawnSave
                {
                    name     = pawn.PawnName,
                    position = pawn.transform.position,
                    food     = needs != null ? needs.food  : 80f,
                    sleep    = needs != null ? needs.sleep : 80f,
                    mood     = needs != null ? needs.mood  : 80f,
                    drafted  = pawn.IsDrafted,   // #audit2 #15
                };
                // #audit2 #17 — 스킬 level+xp 저장(entries 순서 = SkillKind 0~3).
                var sk = pawn.GetComponent<PawnSkills>();
                if (sk != null && sk.entries != null)
                {
                    int n = sk.entries.Length;
                    ps.skillLevels = new int[n];
                    ps.skillXp = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        if (sk.entries[i] == null) continue;
                        ps.skillLevels[i] = sk.entries[i].level;
                        ps.skillXp[i] = sk.entries[i].xp;
                    }
                }
                // #save-load 완성 — 부위별 HP/출혈/붕대 저장(index = PartId 0~5).
                var ph = pawn.GetComponent<PawnHealth>();
                if (ph != null && ph.parts != null)
                {
                    int pn = ph.parts.Length;
                    ps.partHp = new int[pn];
                    ps.partBleed = new float[pn];
                    ps.partBandaged = new bool[pn];
                    for (int i = 0; i < pn; i++)
                    {
                        if (ph.parts[i] == null) continue;
                        ps.partHp[i] = ph.parts[i].hp;
                        ps.partBleed[i] = ph.parts[i].bleedRate;
                        ps.partBandaged[i] = ph.parts[i].bandaged;
                    }
                }
                data.pawns.Add(ps);
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

            // #버그헌트: 각 타입별 used 집합으로 1:1 매칭(같은 엔티티 중복 적용 방지).
            // Re-apply BedEntity quality.
            if (data.beds != null && data.beds.Count > 0)
            {
                var sceneBeds = UnityEngine.Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None);
                var used = new HashSet<BedEntity>();
                foreach (var bs in data.beds)
                {
                    BedEntity best = FindNearest(sceneBeds, bs.position, kMatchRadius,
                        b => (Vector2)b.transform.position, used);
                    if (best != null) { best.SetQuality((BedQuality)bs.quality); used.Add(best); }
                }
            }

            // Re-apply StockpileZoneEntity priority.
            if (data.stockpiles != null && data.stockpiles.Count > 0)
            {
                var sceneZones = UnityEngine.Object.FindObjectsByType<StockpileZoneEntity>(FindObjectsSortMode.None);
                var used = new HashSet<StockpileZoneEntity>();
                foreach (var ss in data.stockpiles)
                {
                    StockpileZoneEntity best = FindNearest(sceneZones, ss.position, kMatchRadius,
                        z => (Vector2)z.transform.position, used);
                    if (best != null) { best.SetPriority((StockpilePriority)ss.priority); used.Add(best); }
                }
            }

            // Re-apply TreeEntity species.
            if (data.trees != null && data.trees.Count > 0)
            {
                var sceneTrees = UnityEngine.Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None);
                var used = new HashSet<TreeEntity>();
                foreach (var ts in data.trees)
                {
                    TreeEntity best = FindNearest(sceneTrees, ts.position, kMatchRadius,
                        t => (Vector2)t.transform.position, used);
                    if (best != null) { best.SetSpecies((TreeSpecies)ts.species); used.Add(best); }
                }
            }

            // Re-apply WallEntity material.
            if (data.walls != null && data.walls.Count > 0)
            {
                var sceneWalls = UnityEngine.Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None);
                var used = new HashSet<WallEntity>();
                foreach (var ws in data.walls)
                {
                    WallEntity best = FindNearest(sceneWalls, ws.position, kMatchRadius,
                        w => (Vector2)w.transform.position, used);
                    if (best != null) { best.SetMaterial((WallMaterial)ws.material); used.Add(best); }
                }
            }
        }

        // #버그헌트(2026-06-03): used 집합을 받아 '이미 매칭된 씬 엔티티'를 제외한다.  이전엔
        //  dense 그리드(1x1 벽/침대 다수)에서 여러 save 항목이 같은 씬 엔티티(가장 가까운 하나)에
        //  매칭돼 sub-state(품질/재질 등)가 덮어써지는 데이터 손상이 있었다.  각 save 항목이
        //  서로 다른 '가장 가까운 미사용' 엔티티에 1:1 매칭되게 한다.
        private static T FindNearest<T>(T[] candidates, Vector2 target, float maxDist,
            System.Func<T, Vector2> posOf, HashSet<T> used = null) where T : UnityEngine.Object
        {
            T best = null;
            float bestSq = maxDist * maxDist;
            foreach (var c in candidates)
            {
                if (c == null) continue;
                if (used != null && used.Contains(c)) continue;
                float sq = (posOf(c) - target).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = c; }
            }
            return best;
        }
    }
}
