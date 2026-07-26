using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #248 자가-발견 QA — 운영자가 일일이 시각/UX 버그를 찾아주지 않아도 되도록, 모든
    /// 건축물을 자동으로 배치·완성하며 단계별 스크린샷 + 완성 엔티티 존재 여부를 로깅한다.
    /// logic 테스트(isolated/integration)가 못 잡는 "완성 시 사라짐 / sprite 이상 / 안 지어짐"
    /// 류 버그를 스크린샷 + [AUDIT] 로그로 드러낸다.
    ///
    /// CLI flag `-feature-audit [-audit-dir <path>]` 로 활성 (GRAPHICS 빌드 필요 — ScreenCapture).
    /// 각 buildable 을 격자에 배치 → 강제 funding+완성 → 0.3s 후 ScreenCapture → 그 위치에
    /// 어떤 엔티티가 실제로 생겼는지(혹은 안 생겼는지) 로깅.  마지막에 전체 한 장 + quit.
    /// </summary>
    public class FeatureAuditQA : MonoBehaviour
    {
        public static bool Enabled = false;
        private static string dir = "audit";

        public static void EnsureInScene()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-feature-audit") Enabled = true;
                if (args[i] == "-audit-dir" && i + 1 < args.Length) dir = args[i + 1];
            }
            if (!Enabled) return;
            var go = new GameObject("FeatureAuditQA");
            go.AddComponent<FeatureAuditQA>();
        }

        private void Start() { StartCoroutine(RunAudit()); }

        // (mode, 격자위치) — 서로 안 겹치게 spread.  모든 buildable 한 번씩.
        private static readonly (BuildManager.Mode mode, int cx, int cy)[] Builds =
        {
            (BuildManager.Mode.Wall,       2, 3),
            (BuildManager.Mode.WallStone,  4, 3),
            (BuildManager.Mode.Door,       6, 3),
            (BuildManager.Mode.Floor,      2, 1),
            (BuildManager.Mode.FloorStone, 4, 1),
            (BuildManager.Mode.Stove,      6, 1),
            (BuildManager.Mode.Lamp,       8, 1),
            (BuildManager.Mode.Barricade,  8, 3),
            (BuildManager.Mode.Fence,     10, 1),
            (BuildManager.Mode.FenceGate, 10, 3),
            (BuildManager.Mode.Bed,        2, 5),
            (BuildManager.Mode.BedFine,    5, 5),
            (BuildManager.Mode.TableChair, 8, 5),
        };

        private IEnumerator RunAudit()
        {
            Debug.Log("[AUDIT] === FEATURE AUDIT 시작 ===");
            try { System.IO.Directory.CreateDirectory(dir); } catch { }
            yield return new WaitForSeconds(1.5f);

            // 카메라를 건축 격자 중앙으로 + 줌아웃해 전부 프레임에 (default 3.5 는 너무 좁음).
            var cam = Camera.main;
            if (cam != null) { cam.orthographicSize = 9f; cam.transform.position = new Vector3(6f, 3f, cam.transform.position.z); }
            yield return null;

            var bm = BuildManager.Instance;
            if (bm == null) { Debug.LogError("[AUDIT] BuildManager null"); Application.Quit(1); yield break; }

            int idx = 0;
            foreach (var (mode, cx, cy) in Builds)
            {
                idx++;
                bm.SetMode(mode);
                bool placed = bm.TryPlaceAt(cx, cy);
                bm.SetMode(BuildManager.Mode.Off);
                if (!placed)
                {
                    Debug.Log($"[AUDIT] {idx:00} {mode} → 배치 거부(TryPlaceAt false) at ({cx},{cy})");
                    continue;
                }
                // 방금 생긴 청사진 찾기 → 강제 funding + 완성.
                BlueprintEntity bp = FindBlueprintNear(new Vector2(cx + 0.5f, cy + 0.5f));
                if (bp == null) { Debug.Log($"[AUDIT] {idx:00} {mode} → 청사진 안 생김"); continue; }
                if (bp.needWood  > 0) bp.DepositWood(bp.needWood);
                if (bp.needStone > 0) bp.DepositStone(bp.needStone);
                bp.AddWork(bp.BuildSeconds + 1f);   // → Complete() 가 완성 엔티티 spawn
                yield return new WaitForSeconds(0.35f);

                // 완성 후 그 위치에 실제로 어떤 엔티티가 생겼는지 검사 (사라짐 버그 탐지).
                string found = WhatIsAt(new Vector2(cx + 0.5f, cy + 0.5f), mode);
                Debug.Log($"[AUDIT] {idx:00} {mode} @ ({cx},{cy}) → 완성결과: {found}");

                // #253 sprite 품질 검사 가능하도록 해당 건물로 카메라 클로즈업 후 캡처
                //  (이전엔 줌아웃 전경이라 건물이 20px 점으로 보여 sprite 판단 불가 — 도구 결함).
                if (cam != null) { cam.orthographicSize = 2.2f; cam.transform.position = new Vector3(cx + 0.5f, cy + 0.5f, cam.transform.position.z); }
                // #257 배치 시 생기는 ClickEffect(파란 X, lifetime 0.6s, sortingOrder 40)가 sprite
                //  위에 찍혀 '파란 십자 아티팩트'로 오인됐음 — 사라질 때까지(>0.6s) 기다린 뒤 캡처.
                yield return new WaitForSeconds(0.8f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"audit_{idx:00}_{mode}.png"));
                yield return null; yield return null;
            }

            // 전체 한 장 (모든 건축물 한눈에) — 다시 와이드로.
            if (cam != null) { cam.orthographicSize = 9f; cam.transform.position = new Vector3(6f, 3f, cam.transform.position.z); }
            yield return new WaitForSeconds(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "audit_ALL.png"));
            yield return new WaitForSeconds(0.5f);
            Debug.Log("[AUDIT] === FEATURE AUDIT 완료 ===");
            Application.Quit(0);
        }

        private static BlueprintEntity FindBlueprintNear(Vector2 p)
        {
            BlueprintEntity best = null; float bestSq = 2.5f * 2.5f;
            foreach (var b in Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                float sq = ((Vector2)b.transform.position - p).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            return best;
        }

        // 위치 p 근처에 어떤 완성 엔티티가 있는지 — Physics2D 대신 FindObjectsByType + 근접
        //  (콜라이더 없는 Floor 등도 정확히 탐지; OverlapCircle 은 콜라이더 의존이라 false-positive).
        private static string WhatIsAt(Vector2 p, BuildManager.Mode expected)
        {
            var names = new List<string>();
            CheckType<WallEntity>(p, "Wall", names);
            CheckType<DoorEntity>(p, "Door", names);
            CheckType<BedEntity>(p, "Bed", names);
            CheckType<StoveEntity>(p, "Stove", names);
            CheckType<LampEntity>(p, "Lamp", names);
            CheckType<BarricadeEntity>(p, "Barricade", names);
            CheckType<FenceEntity>(p, "Fence", names);
            CheckType<FloorEntity>(p, "Floor", names);
            CheckType<TableEntity>(p, "Table", names);
            // 미완성 청사진이 남아있나 (완성 실패)
            foreach (var b in Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
                if (b != null && ((Vector2)b.transform.position - p).sqrMagnitude < 0.81f) names.Add("(미완성 청사진)");
            if (names.Count == 0) return "× 사라짐 — 완성 엔티티 없음 (BUG)";
            return "○ " + string.Join(",", names);
        }

        private static void CheckType<T>(Vector2 p, string label, List<string> outNames) where T : Component
        {
            // 비활성/숨김(HideFlags) 포함 — 완성 엔티티가 비활성으로 생겨도 탐지(원인 구분용).
            foreach (var e in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (e == null) continue;
                if (((Vector2)e.transform.position - p).sqrMagnitude < 1.3f)
                {
                    bool active = e.gameObject.activeInHierarchy;
                    outNames.Add(active ? label : label + "(비활성!)");
                    return;
                }
            }
        }
    }
}
