using UnityEngine;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // 이력 2단:
    // 1) 운영자 fb 2026-05-31 function-first — decor_rock/grass_tuft/wildflower 가
    //    광맥·아이템과 혼동 → 전부 스폰 중단.
    // 2) 운영자 fb 2026-07-24 "바닥도 뭔가 좀 더 — 풀이 조금 올라오던지, 플레이에
    //    방해 안 되는 선에서" → 풀/야생화만 '명백한 배경 질감' 처리로 복원:
    //    외곽선 없는 원본 8px 스프라이트를 저알파(0.55~0.68)·저스케일(0.55~0.8)·
    //    잔디톤 틴트로 깔아 아이템으로 안 읽히게.  decor_rock 은 광맥 혼동 사유
    //    유지로 계속 제외.  잔디 타일 전용·결정론 seed·스폰존 회피.
    public static partial class SceneSetup
    {
        private static void SpawnScatterDecor(TerrainLayout layout)
        {
            var tuft = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/grass_tuft.png");
            var fl1 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wildflower1.png");
            var fl2 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wildflower2.png");
            if (tuft == null)
            {
                Debug.Log("[SceneSetup] scatter decor: grass_tuft.png 없음 — 스킵");
                return;
            }
            var root = new GameObject("GrassDecor");
            var rng = new System.Random(55217);
            int tufts = 0, flowers = 0;
            int half = TerrainLayout.MAP_HALF;
            // 밀도 상향 (2026-07-29) — 140그루/8100칸 = 1.7% 로 사실상 안 보였다.
            //  운영자 L3 지시 "바닥도 뭔가 좀 더 있어야, 풀이 조금 올라오던지" 가
            //  미이행 상태였고, 동시에 잔디 타일이 64px 격자로 반복돼 축소하면
            //  벽지처럼 읽히는 문제의 실질적 해법이기도 하다 — 타일 변형은 이음매를
            //  만들지만(좌우 엣지차 0.53 인 심리스 타일이라 롤/미러가 경계를 깬다)
            //  스캐터는 격자와 무관한 주기로 눈을 흩뜨린다.
            //  나무 배치와 같은 방식으로 **맵 면적에서 유도** — 맵이 커져도 따라온다.
            //  ※ 장식 '돌·자갈·낙엽' 은 늘리지 않는다: 2026-05-31 운영자 function-first
            //    판정(광맥과 혼동)으로 0 이며 그 결정은 유효하다.  풀·꽃만 늘린다.
            int span = 2 * half - 1;
            int tuftTarget = Mathf.RoundToInt(span * span * 0.055f);   // 약 5.5%
            int flowerCap  = Mathf.RoundToInt(tuftTarget * 0.17f);
            for (int i = 0; i < tuftTarget * 7 && tufts < tuftTarget; i++)
            {
                int cx = rng.Next(-(half - 1), half);
                int cy = rng.Next(-(half - 1), half);
                if (Mathf.Abs(cx) < 5 && Mathf.Abs(cy) < 3) continue;          // 스폰존 회피
                // 잔디 전용 (Grass 변형 타일 4종 전부 허용 — 이름 기준)
                var t = layout.tilemap.GetTile(new Vector3Int(cx, cy, 0));
                if (t == null || !t.name.StartsWith("Grass")) continue;
                bool flower = flowers < flowerCap && rng.Next(100) < 12;
                var sp = flower ? (rng.Next(2) == 0 ? fl1 : fl2) ?? tuft : tuft;
                var go = new GameObject(flower ? "wildflower" : "grass_tuft");
                go.transform.SetParent(root.transform, false);
                float jx = (float)rng.NextDouble() * 0.6f + 0.2f;
                float jy = (float)rng.NextDouble() * 0.6f + 0.2f;
                go.transform.position = new Vector3(cx + jx, cy + jy, 0f);
                float sc = 0.55f + (float)rng.NextDouble() * 0.25f;
                go.transform.localScale = new Vector3(sc, sc, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                // 저대비 잔디톤 틴트 + 저알파 = 질감으로만 읽힘 (아이템 오인 차단)
                sr.color = flower
                    ? new Color(0.95f, 0.92f, 0.85f, 0.68f)
                    : new Color(0.82f, 0.92f, 0.72f, 0.58f);
                sr.sortingOrder = 1;   // 데칼 레벨 — 폰(9+)/구조물(10+) 아래
                if (flower) flowers++; else tufts++;
            }
            Debug.Log($"[SceneSetup] scatter decor: 풀 {tufts} + 야생화 {flowers} (배경 질감 처리, 2026-07-24 복원)");
        }
    }
}
