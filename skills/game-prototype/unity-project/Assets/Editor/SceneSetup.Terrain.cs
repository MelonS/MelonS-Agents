using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;

namespace MelonS.GameProto.EditorTools
{
    // R4: SceneSetup partial - Sprite/Tile import helpers.
    //  원본 SceneSetup.cs(1378-1430)에서 이동.
    public static partial class SceneSetup
    {
        /// <summary>#141 - PNG IHDR width/height 직접 읽기 (TextureImporter 가 import 전엔 size 0).</summary>
        private static void GetPngSize(string assetPath, out int w, out int h)
        {
            w = 0; h = 0;
            try
            {
                var bytes = System.IO.File.ReadAllBytes(assetPath);
                if (bytes.Length < 24) return;
                // PNG header: 8B sig + IHDR(4 length + 4 'IHDR' + 4 width + 4 height + ...)
                w = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
                h = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            }
            catch { /* size unknown */ }
        }

        private static Sprite LoadOrSetupSprite(string assetPath)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (s != null) return s;
            // #116 fb - 새로 생성된 png 는 meta 없어서 AssetImporter null 가능.
            //  ForceSynchronousImport 한 번 더 시도해서 meta 생성 유도.
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) { Debug.LogWarning($"[LoadOrSetupSprite] no importer for {assetPath}"); return null; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;  // #116 - 새 png 가 multiple 모드 디폴트
            // #141 - 64x64 sprite 는 PPU 32 (world 크기 = 32x32@PPU16 와 동일).
            //  PNG 직접 읽어 size 판정.
            int srcW, srcH;
            GetPngSize(assetPath, out srcW, out srcH);
            ti.spritePixelsPerUnit = (srcW >= 64) ? 32 : 16;
            ti.filterMode = FilterMode.Point;  // pixel-art crisp
            ti.SaveAndReimport();
            s = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (s == null)
            {
                // race fallback - 두 번째 import
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                s = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }
            return s;
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
                // #QR지면 근본원인(2026-06-11): tile_grass_b/c.png.meta 가 spriteMode 2
                //  (Multiple)로 박혀 있었다 — 슬라이스 0개면 Sprite 서브에셋이 안 생겨
                //  LoadAssetAtPath<Sprite> 가 null → GrassB/C.asset 의 m_Sprite null →
                //  잔디 셀 40%(b25+c15)가 "구멍"으로 비어 카메라 배경색이 비쳤다
                //  (새벽 마우브/정오 세이지 플랫 사각형 — 운영자 "QR코드 지면"의 정체).
                //  Single 강제로 메타가 어떤 상태든 자가치유.
                ti.spriteImportMode = SpriteImportMode.Single;
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
