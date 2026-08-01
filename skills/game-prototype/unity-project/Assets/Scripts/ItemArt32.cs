using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 아트 v2 — 드롭 아이템 32px 스테이지 스프라이트 (운영자 2026-06-11 "한칸을 다
    /// 차지하는 아이템은 별로 없음" + visual-polish TOP-12 '고유 실루엣').
    /// Resources/items32/item_{kind}_v2.png (96x32 = 소/중/만재 3단계, ~20px 풋프린트)
    /// 를 런타임 슬라이스해 양에 맞는 단계를 돌려준다.  스택 표현은 스프라이트
    /// 단계로 — 스케일 늘리기(구 PileScale)는 v2 에선 1 고정.
    /// 시트가 없으면 null → 호출측이 기존 16px+PileScale 폴백 유지.
    /// </summary>
    public static class ItemArt32
    {
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        // 아트 B2 (2026-07-24): TS/절차 단일 스프라이트 우선 — 있으면 스테이지 시트
        //  대신 사용 (스택 표현은 TS 관례대로 단일 외형).  Resources/Sprites/ 에서 로드.
        private static readonly Dictionary<string, string> TsOverride = new Dictionary<string, string>
        {
            { "wood",  "Sprites/ts_wood_pile" },
            { "meat",  "Sprites/ts_meat_pile" },
            // 2026-08-02 운영자 "석재 … 우리껀 너무 안보여" — stone 을 이 표에서 뺀다.
            //  단일 스프라이트로 고정돼 있어 1개를 캐든 40개를 캐든 그림이 똑같았다.
            //  아래 3단계 시트(items32/item_stone_v2)로 돌아가면 **양이 실루엣으로**
            //  읽힌다 (파편 1개 → 3개 → 5개).
        };
        private static readonly Dictionary<string, Sprite> tsCache = new Dictionary<string, Sprite>();

        /// <summary>kind: wood/stone/meat/meal/berry.  amount 로 3단계 중 선택.</summary>
        public static Sprite Stage(string kind, int amount)
        {
            if (TsOverride.TryGetValue(kind, out var tsPath))
            {
                if (!tsCache.TryGetValue(kind, out var ts))
                {
                    ts = Resources.Load<Sprite>(tsPath);
                    tsCache[kind] = ts;   // null 도 캐시 (매 호출 로드 방지)
                }
                if (ts != null) return ts;
            }
            var arr = Load(kind);
            if (arr == null) return null;
            int s = amount >= 20 ? 2 : (amount >= 5 ? 1 : 0);
            return arr[s];
        }

        /// <summary>FoodPile displayName(고기/간편식/베리류) → kind 매핑.</summary>
        public static string KindFromFoodName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "meat";
            if (displayName.Contains("간편식")) return "meal";
            // 첫사이클 T10 (2026-06-12) — '농작물' 분기 부재 + '산딸기'≠'베리' 불일치로
            //  수확물·베리가 드롭 시점부터 고기 스프라이트였다.
            if (displayName.Contains("베리") || displayName.Contains("산딸기")) return "berry";
            if (displayName.Contains("농작물")) return "crop";
            return "meat";
        }

        private static Sprite[] Load(string kind)
        {
            if (cache.TryGetValue(kind, out var hit)) return hit;
            var tex = Resources.Load<Texture2D>($"items32/item_{kind}_v2");
            Sprite[] arr = null;
            if (tex != null && tex.width >= 96)
            {
                arr = new Sprite[3];
                for (int i = 0; i < 3; i++)
                {
                    arr[i] = Sprite.Create(tex, new Rect(i * 32, 0, 32, 32),
                                           new Vector2(0.5f, 0.5f), 32f, 0,
                                           SpriteMeshType.FullRect);
                    arr[i].name = $"item32_{kind}_s{i + 1}";
                }
            }
            cache[kind] = arr;
            return arr;
        }
    }
}
