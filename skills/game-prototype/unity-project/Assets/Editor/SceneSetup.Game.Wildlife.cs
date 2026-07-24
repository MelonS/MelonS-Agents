using UnityEngine;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10 - SceneSetup.cs GenerateGame 의 wolf + deer spawn 블록 extract.
    //   Day 64 wolf 2 + Day 23/41 deer 8.
    //   원본 SceneSetup.cs L174-214 (40 LOC).
    public static partial class SceneSetup
    {
        // 늑대 비활성화 게이트 (운영자 요청 2026-05-31): 현재 늑대를 처리할
        // 게임플레이 방법이 없어 맵에 등장하지 않게 함.  되살리려면 true 로.
        //   - SpawnWildlife 의 wolf 스폰 블록을 게이트.
        //   - AIDirector.WolvesEnabled 와 짝.  둘 다 false 여야 늑대 완전 제거.
        private const bool WolvesEnabled = false;

        private static void SpawnWildlife()
        {
            // Day 64 + #108: Wolf predator - 3 마리 (60x60), 맵 외곽에서 wander.
            //   WolvesEnabled=false 인 동안 스폰하지 않음 (deer 등 평화 동물은 유지).
            if (WolvesEnabled)
            {
                Sprite wolfSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wolf.png");
                Vector2[] wolfPositions = new[] {
                    new Vector2(-27f, 27f), new Vector2(27f, -27f), new Vector2(-27f, -25f),
                };
                foreach (var wpos in wolfPositions)
                {
                    GameObject wGo = new GameObject($"Wolf_{wpos.x}_{wpos.y}");
                    wGo.transform.position = new Vector3(wpos.x + 0.5f, wpos.y + 0.5f, 0);
                    wGo.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
                    var wsr2 = wGo.AddComponent<SpriteRenderer>();
                    wsr2.sprite = wolfSpr;
                    wsr2.sortingOrder = 8;
                    var wcol = wGo.AddComponent<BoxCollider2D>();
                    wcol.size = new Vector2(1.2f, 0.8f);
                    wGo.AddComponent<WolfEnemy>();
                }
            }

            // Day 23+41 + #108: 14 wandering deer - 60x60 맵 비례
            Sprite deerSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/deer.png");
            // 운영자 2026-06-02: 종별 스프라이트 주입([Deer,Boar,Chicken,Rabbit] 순) — 멧돼지·닭·
            //  토끼가 사슴 모양으로 보이던 문제 fix.  AnimalEntity.SetSpecies 가 이 배열로 스왑.
            //  신규 png 는 Sprite/PPU16/Point 로 import 설정(deer 와 동일 픽셀 컨벤션).
            AssetDatabase.Refresh();   // 외부 Python 이 만든 신규 png 를 AssetDB 가 인식하도록
            foreach (var ap in new[] { "boar.png", "chicken.png", "rabbit.png" })
            {
                string apath = $"Assets/Sprites/{ap}";
                if (!System.IO.File.Exists(apath)) continue;
                // 브랜뉴 png: ImportAsset 로 등록해야 GetAtPath 가 importer 반환(아니면 Sprite
                //  설정 전 로드 → null → deer 폴백).
                AssetDatabase.ImportAsset(apath, ImportAssetOptions.ForceSynchronousImport);
                var ti = AssetImporter.GetAtPath(apath) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;  // 기본 Multiple(2)이면 LoadAssetAtPath<Sprite>=null
                    ti.spritePixelsPerUnit = 16;
                    ti.filterMode = FilterMode.Point;
                    ti.SaveAndReimport();
                }
            }
            var boarSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/boar.png");
            var chickenSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/chicken.png");
            var rabbitSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/rabbit.png");
            // 아트 v3.2 (2026-07-24): 잉크 문법 fauna64 4종 — PPU64 임포트 후 존재하면
            //  우선 사용 (없으면 구세대 픽셀 폴백).  flora64 임포트 블록과 동일 패턴.
            foreach (var fp in new[] { "fauna64_deer.png", "fauna64_boar.png",
                                       "fauna64_chicken.png", "fauna64_rabbit.png" })
            {
                string fpath = $"Assets/Sprites/{fp}";
                if (!System.IO.File.Exists(fpath)) continue;
                AssetDatabase.ImportAsset(fpath, ImportAssetOptions.ForceSynchronousImport);
                var fti = AssetImporter.GetAtPath(fpath) as TextureImporter;
                if (fti != null)
                {
                    fti.textureType = TextureImporterType.Sprite;
                    fti.spriteImportMode = SpriteImportMode.Single;
                    fti.spritePixelsPerUnit = 64;   // v3 밀도
                    fti.filterMode = FilterMode.Point;
                    fti.SaveAndReimport();
                }
            }
            // 아트 B2: 사슴 슬롯에 TS 양 우선 (팩 유일 동물 — 나머지 3종은 정합 생성 후속).
            var tsSheep = ImportSpriteAt("Assets/Sprites/ts_sheep.png", 160f);
            var deer64 = tsSheep != null ? tsSheep
                : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/fauna64_deer.png");
            var boar64 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/fauna64_boar.png");
            var chick64 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/fauna64_chicken.png");
            var rabbit64 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/fauna64_rabbit.png");
            if (deer64 != null) deerSpr = deer64;
            if (boar64 != null) boarSpr = boar64;
            if (chick64 != null) chickenSpr = chick64;
            if (rabbit64 != null) rabbitSpr = rabbit64;
            MelonS.GameProto.AnimalEntity.SpeciesSprites = new[] { deerSpr, boarSpr, chickenSpr, rabbitSpr };
            Vector2[] deerPositions = new[]
            {
                new Vector2(-21f,  12f), new Vector2( 19f,  14f), new Vector2( 24f, -5f),
                new Vector2(-23f, -6f),  new Vector2( -3f, 23f),  new Vector2(  9f,-18f),
                new Vector2(-15f, -3f),  new Vector2( 16f,  6f),  new Vector2(-12f, 16f),
                new Vector2( 22f, 18f),  new Vector2(  5f, 14f),  new Vector2(-19f, 9f),
                new Vector2( 10f, -8f),  new Vector2(-7f, -22f),
            };
            // #132 - 4종 동물 분포 (deer 6, boar 3, chicken 3, rabbit 2 = 14)
            var speciesPlan = new AnimalSpecies[] {
                AnimalSpecies.Deer, AnimalSpecies.Deer, AnimalSpecies.Deer,
                AnimalSpecies.Deer, AnimalSpecies.Deer, AnimalSpecies.Deer,
                AnimalSpecies.Boar, AnimalSpecies.Boar, AnimalSpecies.Boar,
                AnimalSpecies.Chicken, AnimalSpecies.Chicken, AnimalSpecies.Chicken,
                AnimalSpecies.Rabbit, AnimalSpecies.Rabbit,
            };
            for (int i = 0; i < deerPositions.Length; i++)
            {
                var dpos = deerPositions[i];
                AnimalSpecies sp = speciesPlan[i % speciesPlan.Length];
                string kr = sp switch {
                    AnimalSpecies.Deer => "Deer", AnimalSpecies.Boar => "Boar",
                    AnimalSpecies.Chicken => "Chicken", _ => "Rabbit" };
                GameObject dGo = new GameObject($"{kr}_{dpos.x}_{dpos.y}");
                dGo.transform.position = new Vector3(dpos.x + 0.5f, dpos.y + 0.5f, 0);
                var dsr2 = dGo.AddComponent<SpriteRenderer>();
                dsr2.sprite = deerSpr;
                dsr2.sortingOrder = 8;
                dGo.AddComponent<Rigidbody2D>();
                var dcol = dGo.AddComponent<CircleCollider2D>();
                dcol.radius = 0.4f;
                var ae = dGo.AddComponent<AnimalEntity>();
                ae.SetSpecies(sp);
            }
        }
    }
}
