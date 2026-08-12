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

            var bm = BuildManager.Instance;
            if (bm == null) { Debug.LogError("[BuildQA] BuildManager null"); yield break; }

            // #159 - BuildManager 의 prefab/sprite ref 직접 사용 (SceneSetup 이 SetRefs 로 박음).
            //   Resources.FindObjectsOfTypeAll 은 SR.sprite null 인 prefab 만 반환.
            GameObject wallPrefab = bm.WallPrefabRef;
            Sprite wallSpr = bm.WallSpriteRef;
            GameObject bedPrefab = bm.BedPrefabRef;
            Sprite bedSpr = bm.BedSpriteRef;
            if (wallPrefab == null || wallSpr == null)
            {
                Debug.LogError($"[BuildQA] wallPrefab/sprite NULL (prefab={wallPrefab!=null} spr={wallSpr!=null})");
                yield break;
            }

            // -- Phase 1: wall blueprint at (-1.5, 3.5) --
            //  #242 verify-the-real-path: 직접 spawn(우회) 대신 실제 플레이 경로
            //  (BuildManager.SetMode + TryPlaceAt) 로 배치 → TryPlace 의 카운터-funding 까지
            //  포함해 검증.  배치 시 카운터가 자재를 감당하면 funding 되고 builder 가 건설.
            Vector3 bpPos = new Vector3(-1.5f, 3.5f, 0);
            int wallCx = -2, wallCy = 3;   // floor((-1.5,3.5)) → cell center (-1.5,3.5) = bpPos
            BlueprintEntity bp = null;
            if (BuildManager.Instance != null)
            {
                BuildManager.Instance.SetMode(BuildManager.Mode.Wall);
                BuildManager.Instance.TryPlaceAt(wallCx, wallCy);
                BuildManager.Instance.SetMode(BuildManager.Mode.Off);
                foreach (var b in Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
                    if (b != null && Vector2.Distance(b.transform.position, bpPos) < 0.8f) { bp = b; break; }
            }
            if (bp == null)
            {
                Debug.LogError("[BuildQA] wall 청사진 TryPlaceAt 실패 (배치 거부?)");
                yield break;
            }
            Debug.Log($"[BuildQA] t=3s: wall 청사진 배치(실경로) at ({bpPos.x},{bpPos.y}), collected={bp.collectedWood}/{bp.needWood} hasMat={bp.HasAllMaterials}");

            // 5초 간격 상태 로그 (wall 완성까지)
            bool wallDone = false;
            for (int t = 5; t <= 30; t += 5)
            {
                yield return new WaitForSeconds(5f);
                if (bp == null || bp.gameObject == null)
                {
                    var walls = Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None);
                    bool found = false;
                    foreach (var w in walls)
                    {
                        if (w == null) continue;
                        if (Vector2.Distance(w.transform.position, bpPos) < 0.8f) { found = true; break; }
                    }
                    Debug.Log($"[BuildQA] t={t}s: wall 완성! wall@bpPos={found} (total walls={walls.Length})");
                    wallDone = true;
                    break;
                }
                Debug.Log($"[BuildQA] t={t}s: wall collected wood={bp.collectedWood}/{bp.needWood} progress={bp.Progress*100f:F0}% hasMat={bp.HasAllMaterials} reserved={bp.IsReserved}");
            }
            if (!wallDone)
            {
                Debug.LogWarning($"[BuildQA] wall 청사진 timeout 30s.  final collected={bp.collectedWood}/{bp.needWood} progress={bp.Progress*100f:F0}%");
            }

            // -- Phase 2: Fine bed blueprint at (-3.5, 3.5) → BedQuality.Fine 검증 --
            if (bedPrefab == null || bedSpr == null)
            {
                Debug.LogWarning($"[BuildQA] bedPrefab/sprite NULL - bed QA skip.  prefab={bedPrefab!=null} spr={bedSpr!=null}");
                yield break;
            }
            Vector3 bedPos = new Vector3(-3.5f, 3.5f, 0);
            var bedBpGo = new GameObject("BuildQA_Blueprint_BedFine");
            bedBpGo.transform.position = bedPos;
            var bedBp = bedBpGo.AddComponent<BlueprintEntity>();
            // BedFine - wood 30 (BuildManager.bedFineCost), 5s build
            bedBp.Init(BuildManager.Mode.BedFine, bedPrefab, bedSpr, wood: 30, stone: 0, secs: 5f);
            Debug.Log($"[BuildQA] Phase2: BedFine 청사진 spawn at ({bedPos.x},{bedPos.y}), 자재 30목재 필요");

            for (int t = 5; t <= 60; t += 5)
            {
                yield return new WaitForSeconds(5f);
                if (bedBp == null || bedBp.gameObject == null)
                {
                    // bed 완성 - BedEntity 의 Quality 확인
                    var beds = Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None);
                    BedEntity foundBed = null;
                    foreach (var b in beds)
                    {
                        if (b == null) continue;
                        if (Vector2.Distance(b.transform.position, bedPos) < 0.8f) { foundBed = b; break; }
                    }
                    bool qOk = foundBed != null && foundBed.Quality == BedQuality.Fine;
                    Debug.Log($"[BuildQA] Phase2 t+{t}s: bed 완성! quality={(foundBed!=null?foundBed.QualityKr:"NULL")} expected=고급 침대 → match={qOk}");
                    yield break;
                }
                Debug.Log($"[BuildQA] Phase2 t+{t}s: bed collected wood={bedBp.collectedWood}/{bedBp.needWood} progress={bedBp.Progress*100f:F0}%");
            }
            Debug.LogWarning($"[BuildQA] Phase2 bed timeout 60s.  final collected={bedBp.collectedWood}/{bedBp.needWood}");
        }
    }
}
