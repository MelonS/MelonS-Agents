using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 사람-리듬 #8·#9 (human-play-gap-2026-06-12.md) — 무인 런(소크/쇼케이스) 카메라
    /// 디렉터.  사람 관전자는 "가장 흥미로운 림"을 따라다닌다 (전투 > 치료 > 건설 >
    /// 채취 노동 > 기타).  일정 주기로 최고 점수 림을 골라 CameraController.RequestFocus
    /// 로 부드럽게 팔로우.
    ///
    /// 활성화는 ReproHarness 의 `camdirector` op 로만 — 일반 플레이에선 생성되지 않고,
    /// 시나리오의 명령 구간(worldclick 재투영이 카메라 이동에 민감)에서는 꺼 둔다.
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        private static CameraDirector _instance;
        public static CameraDirector Instance => _instance;

        private const float FollowSec = 12f;     // 림 하나를 따라가는 시간
        private bool active;
        private float nextPickAt = -1f;
        private PawnEntity lastPick;

        public static CameraDirector EnsureInScene()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("CameraDirector");
            _instance = go.AddComponent<CameraDirector>();
            return _instance;
        }

        private void Awake() { _instance = this; }

        public void SetActive(bool on)
        {
            active = on;
            nextPickAt = -1f;   // 켜자마자 첫 픽
            Debug.Log($"[CamDirector] active={on}");
        }

        private void Update()
        {
            if (!active) return;
            // 일시정지 중에도 관전 로테이션은 의미 없음 — 시간 멈춤 = 화면 정지.
            if (Time.timeScale == 0f) return;
            if (Time.unscaledTime < nextPickAt) return;

            var pick = PickMostInteresting();
            if (pick != null)
            {
                var cc = Object.FindFirstObjectByType<CameraController>();
                if (cc != null)
                {
                    cc.RequestFocus(pick);
                    Debug.Log($"[CamDirector] follow {pick.PawnName} ({Describe(pick)})");
                }
                lastPick = pick;
            }
            nextPickAt = Time.unscaledTime + FollowSec;
        }

        /// <summary>전투 > 치료 > 건설(지붕 포함) > 채취 노동 > 운반/요리 > 그 외.
        /// 동점이면 직전 팔로우 림을 피해서 로테이션이 생기게.</summary>
        private PawnEntity PickMostInteresting()
        {
            PawnEntity best = null;
            float bestScore = float.MinValue;
            foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                if (p == null || p.IsDead) continue;
                float s = Score(p);
                if (p == lastPick) s -= 15f;   // 로테이션 유도
                if (s > bestScore) { bestScore = s; best = p; }
            }
            return best;
        }

        private static float Score(PawnEntity p)
        {
            if (p.IsDrafted) return 100f;
            var doctor = p.GetComponent<PawnDoctor>();
            if (doctor != null && doctor.HasTask) return 80f;
            var builder = p.GetComponent<PawnBuilder>();
            if (builder != null && builder.HasTask) return 60f;
            var miner = p.GetComponent<PawnMiner>();
            if (miner != null && miner.HasTask) return 50f;
            var chopper = p.GetComponent<PawnChopper>();
            if (chopper != null && chopper.HasTask) return 50f;
            var hauler = p.GetComponent<PawnHauler>();
            if (hauler != null && hauler.HasTask) return 40f;
            var cook = p.GetComponent<PawnCook>();
            if (cook != null && cook.HasTask) return 35f;
            var needs = p.GetComponent<PawnNeeds>();
            if (needs != null && needs.IsSleeping) return 5f;
            return 10f;
        }

        private static string Describe(PawnEntity p)
        {
            if (p.IsDrafted) return "전투";
            if (p.GetComponent<PawnDoctor>()?.HasTask == true) return "치료";
            if (p.GetComponent<PawnBuilder>()?.HasTask == true) return "건설";
            if (p.GetComponent<PawnMiner>()?.HasTask == true) return "채광";
            if (p.GetComponent<PawnChopper>()?.HasTask == true) return "벌목";
            if (p.GetComponent<PawnHauler>()?.HasTask == true) return "운반";
            if (p.GetComponent<PawnCook>()?.HasTask == true) return "요리";
            return "일상";
        }
    }
}
