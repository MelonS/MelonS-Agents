using UnityEngine;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // 운영자 fb 2026-05-31: function-first — 기능없는 장식 오브젝트 제거.
    // decor_rock/grass_tuft/wildflower1/wildflower2 는 상호작용 없고 광맥(StoneVein)과
    // 혼동되거나 쓸모없는 오브젝트로 보임 → SpawnScatterDecor 가 아무것도 스폰하지 않음.
    // 지형 타일 텍스처/조명/비네트(순수 배경)와 기능 오브젝트(나무/광맥/침대/화덕)는 보존.
    // water_edge_band 는 ScatterVarietyDriver(지형 전환 텍스처, 순수 배경)에서 유지.
    public static partial class SceneSetup
    {
        private static void SpawnScatterDecor(TerrainLayout layout)
        {
            // 운영자 fb 2026-05-31: function-first — 장식 scatter 오브젝트 모두 제거.
            // decor_rock / grass_tuft / wildflower1 / wildflower2 스폰 없음.
            Debug.Log("[SceneSetup] scatter decor: 기능없는 장식 오브젝트 제거됨 (function-first)");
        }
    }
}
