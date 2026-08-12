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
            string p = assetPath.Replace('\\', '/');
            bool pawn = p.Contains("/Resources/pawn32/");
            // A단계: 32px 지형/식생/아이템 — LoadOrSetupSprite 의 크기 규칙
            //  ((srcW>=64)?32:16)이 32px 단일 타일에 PPU 16 을 주는 것을 폴더/접두로 차단.
            bool v2 = p.Contains("/Sprites/tile32_") || p.Contains("/Sprites/flora32_")
                   || p.Contains("/Sprites/struct32_") || p.Contains("/Resources/struct32/")
                   || p.Contains("/Resources/items32/") || p.Contains("/Resources/animals32/")
                   || p.Contains("/Resources/pawn32tool/") || p.Contains("/Resources/flora32/");
            if (!pawn && !v2) return;
            // v2 폴더 안에 살지만 **v2 밀도가 아닌** 재생성 자산은 제외한다.
            //  베리덤불 2종은 gen-ts-props.py 가 TS 전환(2026-07-24) 때 128px 로 다시 그려
            //  Resources/flora32/ 에 그대로 얹혔다.  여기서 PPU 32 를 강제하면 128/32 =
            //  **4×4칸** 이 되어 나무(2칸)보다 커지고, 콜라이더(1×1)와 어긋나 클릭도 안 먹는다
            //  (2026-07-27 운영자 "베리인거 같은데 왜케 커? 아무런 인터렉션도 안되고").
            //  .meta 를 고쳐도 이 포스트프로세서가 임포트마다 되돌려서 원인 추적이 어려웠다 —
            //  SceneSetup.Game.Entities.cs 의 PPU128 등록조차 재임포트로 무력화되고 있었다.
            //  → 예외로 빼서 .meta/베이크 등록값(128)이 그대로 살아 있게 한다.
            //  검증: scripts/check-sprite-ppu.py (등록 PPU vs .meta 전수 대조).
            if (p.EndsWith("/flora32_bush_berry.png") || p.EndsWith("/flora32_bush_picked.png"))
                return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 32f;
            ti.filterMode = UnityEngine.FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            // 시트 슬라이스(pawn32/items32)는 readable 필요, 타일/식생은 불필요.
            ti.isReadable = pawn || p.Contains("/Resources/items32/")
                          || p.Contains("/Resources/animals32/");
        }
    }
}
