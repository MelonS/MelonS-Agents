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
    }

    [Serializable]
    public class SaveData
    {
        public int wood;
        public int food;
        public List<PawnSave> pawns = new List<PawnSave>();
        public List<TreeSave> trees = new List<TreeSave>();
        public string version = "0.1.0";
        public string savedAtIso;
    }

    /// <summary>
    /// JSON save/load to Application.persistentDataPath/save.json.
    /// Day 6 minimal: pawns + trees + resources.  No tilemap state
    /// (regenerated deterministically on Load).
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
                    name = pawn.PawnName,
                    position = pawn.transform.position,
                    food = needs != null ? needs.food : 80f,
                    sleep = needs != null ? needs.sleep : 80f,
                    mood = needs != null ? needs.mood : 80f,
                });
            }

            foreach (var tree in UnityEngine.Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None))
            {
                if (tree.IsDestroyed) continue;
                data.trees.Add(new TreeSave { position = tree.transform.position });
            }

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[SaveLoad] saved -> {SavePath}");
        }

        public static SaveData Load()
        {
            if (!SaveExists)
            {
                Debug.LogWarning("[SaveLoad] no save file");
                return null;
            }
            string json = File.ReadAllText(SavePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            Debug.Log($"[SaveLoad] loaded ({data.pawns.Count} pawns, {data.trees.Count} trees, wood={data.wood})");
            return data;
        }
    }
}
