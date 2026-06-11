using UnityEditor;

namespace MelonS.GameProto.EditorTools
{
    /// <summary>
    /// 아트 v2 (32px) 임포트 설정 강제 — Resources/pawn32 등 v2 자산은 PPU 32.
    /// LoadOrSetupSprite 의 크기 규칙((srcW>=64)?32:16)과 무관하게 폴더 단위로
    /// 결정론 보장.  isReadable: PawnSpriteAnimator 가 시트를 런타임
    /// Sprite.Create 로 슬라이스하므로 필수.
    /// (spriteMode Single 강제는 #QR지면 교훈 — Multiple+슬라이스0 = 빈 스프라이트.)
    /// </summary>
    public class ArtV2Import : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/pawn32/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 32f;
            ti.filterMode = UnityEngine.FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.isReadable = true;
        }
    }
}
