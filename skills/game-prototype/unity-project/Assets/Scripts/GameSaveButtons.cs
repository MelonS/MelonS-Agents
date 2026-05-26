using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 6 in-game Save / Load buttons (top-left).  Save serializes
    /// pawn+tree+resource state; Load destroys current state then
    /// re-instantiates from save file.
    /// </summary>
    public class GameSaveButtons : MonoBehaviour
    {
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private GameObject treePrefabRef;  // optional: a fallback tree prefab
        [SerializeField] private Sprite treeSprite;

        private void Awake()
        {
            if (saveButton != null)
            {
                saveButton.onClick.RemoveAllListeners();
                saveButton.onClick.AddListener(OnSave);
            }
            if (loadButton != null)
            {
                loadButton.onClick.RemoveAllListeners();
                loadButton.onClick.AddListener(OnLoad);
            }
        }

        private void Update()
        {
            // Hotkeys: F5 = save, F9 = load
            if (Input.GetKeyDown(KeyCode.F5)) OnSave();
            if (Input.GetKeyDown(KeyCode.F9)) OnLoad();
        }

        private void OnSave()
        {
            SaveLoadManager.Save();
        }

        private void OnLoad()
        {
            SaveData data = SaveLoadManager.Load();
            if (data == null) return;

            // Destroy current pawns + trees
            foreach (var p in FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                Destroy(p.gameObject);
            foreach (var t in FindObjectsByType<TreeEntity>(FindObjectsSortMode.None))
                Destroy(t.gameObject);

            // Restore resources
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.wood = data.wood;
                ResourceManager.Instance.food = data.food;
            }

            // Re-spawn pawns
            foreach (var ps in data.pawns)
            {
                if (pawnPrefab == null) continue;
                GameObject p = Instantiate(pawnPrefab, ps.position, Quaternion.identity);
                PawnEntity entity = p.GetComponent<PawnEntity>();
                if (entity != null)
                {
                    var nameField = typeof(PawnEntity).GetField("pawnName",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (nameField != null) nameField.SetValue(entity, ps.name);
                }
                PawnNeeds needs = p.GetComponent<PawnNeeds>();
                if (needs != null)
                {
                    needs.food = ps.food;
                    needs.sleep = ps.sleep;
                    needs.mood = ps.mood;
                }
            }

            // Re-spawn trees (use treeSprite reference if no prefab)
            foreach (var ts in data.trees)
            {
                GameObject t;
                if (treePrefabRef != null)
                {
                    t = Instantiate(treePrefabRef, ts.position, Quaternion.identity);
                }
                else
                {
                    t = new GameObject("Tree");
                    t.transform.position = ts.position;
                    var sr = t.AddComponent<SpriteRenderer>();
                    if (treeSprite != null) sr.sprite = treeSprite;
                    sr.sortingOrder = 5;
                    var col = t.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(1.5f, 1.5f);
                    t.AddComponent<TreeEntity>();
                }
            }

            Debug.Log($"[SaveLoad] restored: {data.pawns.Count} pawns, {data.trees.Count} trees");
        }
    }
}
