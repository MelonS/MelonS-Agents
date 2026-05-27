using UnityEngine;
using System.Collections;

namespace MelonS.GameProto
{
    /// <summary>
    /// #147 - 건축 graphics 모드 자동 QA.  CLI flag `-build-qa` 시 활성.
    ///  3s 후 wall 청사진 spawn → 30s 동안 hauler/builder 작동 →
    ///  완성된 wall 위치 + collected/완성 상태 [BuildQA] log 출력.
    ///  운영자 시점 (Game scene 자동 시작 + click 시뮬) 검증.
    /// </summary>
    public class BuildAutoQA : MonoBehaviour
    {
        public static bool Enabled = false;

        public static void EnsureInScene()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            foreach (var a in args) if (a == "-build-qa") { Enabled = true; break; }
            if (!Enabled) return;
            var go = new GameObject("BuildAutoQA");
            go.AddComponent<BuildAutoQA>();
        }

        private void Start()
        {
            StartCoroutine(RunQA());
        }

        private IEnumerator RunQA()
        {
            yield return new WaitForSeconds(3.0f);
            // 첫 chop 까지 기다림 → wood pile 생김 → blueprint 도 자재 받음
            Debug.Log("[BuildQA] t=3s: build mode 시작");

            // wall blueprint 직접 spawn (BuildManager.TryPlace 우회 - 자원 0 차감)
            var bm = BuildManager.Instance;
            if (bm == null) { Debug.LogError("[BuildQA] BuildManager null"); yield break; }
            // wallPrefab + sprite 가져오기
            var wallSr = Object.FindFirstObjectByType<WallEntity>();
            Sprite wallSpr = wallSr != null ? wallSr.GetComponent<SpriteRenderer>()?.sprite : null;
            var prefabAll = Resources.FindObjectsOfTypeAll<GameObject>();
            GameObject wallPrefab = null;
            foreach (var p in prefabAll)
            {
                if (p != null && p.name == "Wall" && p.GetComponent<WallEntity>() != null)
                { wallPrefab = p; break; }
            }
            if (wallPrefab == null || wallSpr == null)
            {
                Debug.LogError($"[BuildQA] wallPrefab/sprite NULL (prefab={wallPrefab!=null} spr={wallSpr!=null})");
                yield break;
            }

            // 정착지 근처 빈 cell - (-2, 3)
            Vector3 bpPos = new Vector3(-1.5f, 3.5f, 0);
            var bpGo = new GameObject("BuildQA_Blueprint");
            bpGo.transform.position = bpPos;
            var bp = bpGo.AddComponent<BlueprintEntity>();
            bp.Init(BuildManager.Mode.Wall, wallPrefab, wallSpr, wood: 5, stone: 0, secs: 5f);
            Debug.Log($"[BuildQA] t=3s: 청사진 spawn at ({bpPos.x},{bpPos.y}), 자재 5목재 필요");

            // 5초 간격 상태 로그
            for (int t = 5; t <= 45; t += 5)
            {
                yield return new WaitForSeconds(5f);
                if (bp == null || bp.gameObject == null)
                {
                    // 청사진 사라짐 = 완성 (wall instantiated)
                    var walls = Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None);
                    bool found = false;
                    foreach (var w in walls)
                    {
                        if (w == null) continue;
                        if (Vector2.Distance(w.transform.position, bpPos) < 0.8f) { found = true; break; }
                    }
                    Debug.Log($"[BuildQA] t={t}s: 완성! wall@bpPos={found} (total walls={walls.Length})");
                    yield break;
                }
                Debug.Log($"[BuildQA] t={t}s: collected wood={bp.collectedWood}/{bp.needWood} progress={bp.Progress*100f:F0}% hasMat={bp.HasAllMaterials} reserved={bp.IsReserved}");
            }
            Debug.LogWarning($"[BuildQA] t=45s timeout: 청사진 아직 미완성. final collected={bp.collectedWood}/{bp.needWood} progress={bp.Progress*100f:F0}%");
        }
    }
}
