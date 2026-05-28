using UnityEngine;

namespace MelonS.GameProto.Core
{
    /// <summary>
    /// #167 - sprite tint 보존 + brightness 곱셈 helper.  #156 lesson 추출.
    ///
    /// 이전 버그 패턴: TakeDamage 시 sr.color = new Color(t,t,t,1) 로 grayscale
    /// 덮어쓰기 → species/material tint 영구 손실.
    /// 적용 entity 5종: TreeEntity / WallEntity / StoneVeinEntity /
    ///                  BerryBushEntity / AnimalEntity (hit-flash 복원).
    ///
    /// Helper API:
    ///   ApplyBrightness(sr, baseTint, brightness)  - tint × brightness 곱
    ///   ApplyHpBrightness(sr, baseTint, hp/maxHp, min, max)  - HP 비례 darken
    ///
    /// 사용 예:
    ///   private Color baseTint = ...;  // Awake 시 캐시
    ///   void TakeDamage() { TintHelper.ApplyHpBrightness(sr, baseTint, hp/maxHp); }
    /// </summary>
    public static class TintHelper
    {
        /// <summary>baseTint 의 hue 유지 + brightness 곱.  alpha 도 곱하지 않음 (유지).</summary>
        public static void ApplyBrightness(SpriteRenderer sr, Color baseTint, float brightness)
        {
            if (sr == null) return;
            sr.color = new Color(baseTint.r * brightness, baseTint.g * brightness,
                                 baseTint.b * brightness, baseTint.a);
        }

        /// <summary>HP 비례 brightness (minBright ~ 1.0).
        /// 예: hpRatio=0.5, minBright=0.4 → brightness = 0.4 + 0.6 * 0.5 = 0.7
        /// minBright 으로 완파 직전에도 sprite 식별 가능.</summary>
        public static void ApplyHpBrightness(SpriteRenderer sr, Color baseTint,
            float hpRatio, float minBright = 0.4f)
        {
            float t = Mathf.Clamp01(hpRatio);
            float b = minBright + (1f - minBright) * t;
            ApplyBrightness(sr, baseTint, b);
        }
    }
}
