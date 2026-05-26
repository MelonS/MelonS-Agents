using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;

namespace MelonS.GameProto.EditorTools
{
    // R4: SceneSetup partial - Sprite/Tile import helpers.
    //  원본 SceneSetup.cs(1378-1430)에서 이동.
    public static partial class SceneSetup
    {
        private static Sprite LoadOrSetupSprite(string assetPath)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (s != null) return s;
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) { Debug.LogWarning($"[LoadOrSetupSprite] no importer for {assetPath}"); return null; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spritePixelsPerUnit = 16;
            ti.filterMode = FilterMode.Point;  // pixel-art crisp
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Tile LoadOrCreateTile(string spritePath, string tileAssetPath)
        {
            // Day 39 lesson: Unity 6에서 신규 PNG → Sprite import이 SaveAndReimport
            //  호출 후에도 LoadAssetAtPath가 즉시 null 반환할 때가 있음.
            //  순서: ImportAsset(ForceSync) → Refresh → LoadAssetAtPath.
            //  그래도 null이면 한 번 더 ImportAsset.
            //  Tile.asset은 매번 새로 만들어 sprite를 보장.
            TextureImporter ti = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spritePixelsPerUnit = 16;
                ti.filterMode = FilterMode.Point;
                ti.SaveAndReimport();
            }
            AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (spr == null)
            {
                // 두 번째 시도 (race fallback).
                AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                spr = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            }
            if (spr == null)
            {
                Debug.LogError($"[LoadOrCreateTile] sprite STILL null for {spritePath} — Tile will be blank");
            }
            // 기존 Tile.asset 삭제 후 재생성 (sprite 참조가 깨졌을 가능성).
            if (AssetDatabase.LoadAssetAtPath<Tile>(tileAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(tileAssetPath);
            }
            Tile t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = spr;
            AssetDatabase.CreateAsset(t, tileAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LoadOrCreateTile] {tileAssetPath} sprite={(spr==null?"NULL":spr.name)}");
            return t;
        }
    }
}
