using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb (QA 너무 얕음): graphics 모드 30s 시뮬에서 자원 변화 자동 검증 필요.
    ///  5s 마다 wood/food/stone/meals 를 Player.log 에 dump.
    ///  refactor_check.py 가 log grep 으로 monotonic increase 검증.
    /// </summary>
    public class ResourceMonitorLogger : MonoBehaviour
    {
        private float lastLogTime = -10f;
        private const float Interval = 5f;
        private float startTime;

        public static void EnsureInScene()
        {
            if (Object.FindFirstObjectByType<ResourceMonitorLogger>() != null) return;
            var go = new GameObject("ResourceMonitorLogger");
            go.AddComponent<ResourceMonitorLogger>();
        }

        private void Awake()
        {
            startTime = Time.time;
        }

        private void Update()
        {
            if (Time.time - lastLogTime < Interval) return;
            lastLogTime = Time.time;
            var rm = ResourceManager.Instance;
            if (rm == null) return;
            float elapsed = Time.time - startTime;
            Debug.Log($"[ResMon] t={elapsed:F1}s wood={rm.wood} food={rm.food} stone={rm.stone} meals={rm.meals} fineMeals={rm.fineMeals}");
            // 수면루프 진단(2026-06-13) — 수면 곡선 실측: 림별 sleep 값 (5s 주기 동승).
            var sb = new System.Text.StringBuilder("[SleepMon]");
            foreach (var pn in UnityEngine.Object.FindObjectsByType<PawnNeeds>(FindObjectsSortMode.None))
            {
                if (pn == null) continue;
                var pe = pn.GetComponent<PawnEntity>();
                sb.Append($" {(pe != null ? pe.PawnName : pn.name)}={pn.sleep:F0}{(pn.IsSleeping ? "z" : "")}");
            }
            Debug.Log(sb.ToString());
        }
    }
}
