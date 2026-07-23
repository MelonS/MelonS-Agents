using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MelonS.GameProto.EditorTools
{
    /// <summary>
    /// One-shot migration for the WebGL build: scenes were saved with editor-created
    /// "Malgun Gothic" OS fonts embedded (no font data → WebGL build error
    /// "Need to include font data"). Reassigns every serialized Text/TextMesh font
    /// to the bundled Noto Sans KR (OFL) asset; the orphaned embedded Font objects
    /// drop out of the scene file on save.
    /// CLI: -executeMethod MelonS.GameProto.EditorTools.WebGLFontMigration.Migrate
    /// </summary>
    public static class WebGLFontMigration
    {
        public static void Migrate()
        {
            var noto = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NotoSansKR.ttf");
            if (noto == null)
            {
                Debug.LogError("[FontMigration] Assets/Resources/Fonts/NotoSansKR.ttf not found");
                EditorApplication.Exit(1);
                return;
            }

            string[] scenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Game.unity" };
            foreach (var path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int texts = 0, meshes = 0;
                foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    t.font = noto;
                    texts++;
                }
                foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    tm.font = noto;
                    var mr = tm.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sharedMaterial = noto.material;
                    meshes++;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[FontMigration] {path}: {texts} Text + {meshes} TextMesh -> NotoSansKR");
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (var t in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    if (t.font != noto) { t.font = noto; changed = true; }
                }
                foreach (var tm in root.GetComponentsInChildren<TextMesh>(true))
                {
                    tm.font = noto;
                    var mr = tm.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sharedMaterial = noto.material;
                    changed = true;
                }
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[FontMigration] prefab {path} -> NotoSansKR");
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[FontMigration] DONE");
            EditorApplication.Exit(0);
        }
    }
}
