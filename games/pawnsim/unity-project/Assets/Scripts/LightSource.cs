using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>밤에 어둠을 걷어내는 **광원 목록의 단일 출처**.
    ///
    /// 계기 (2026-08-01 운영자): "조명관련 오브젝트들 개선 및 추가해줘".
    ///
    /// 실측 진단: `NightOverlay.ScanLamps` 가 `LampEntity` **하나만** 찾고 있었다.
    ///  그래서 마당 한가운데 모닥불도, 불을 때는 부뚜막도 밤에 **빛을 내지 않는다** —
    ///  화면에는 주황 글로우 스프라이트만 떠 있고 주변 어둠은 그대로였다.
    ///  '불이 있는데 어둡다' 는 곧바로 어색함으로 읽힌다.
    ///
    /// 구조: 광원이 되고 싶은 물체가 이 컴포넌트를 붙이면 목록에 들어온다.
    ///  NightOverlay 는 타입을 나열하는 대신 이 목록만 읽는다 — 새 조명을 추가할 때
    ///  NightOverlay 를 고칠 필요가 없다(이 레포에서 반복된 '나열식 규칙' 함정 회피).
    /// </summary>
    [DisallowMultipleComponent]
    public class LightSource : MonoBehaviour
    {
        /// <summary>빛 반경 (칸).  등잔보다 모닥불이 넓다.</summary>
        [SerializeField] private float radiusTiles = 6.5f;
        /// <summary>불꽃 높이 (칸) — 빛의 중심이 바닥이 아니라 불꽃에 있다.</summary>
        [SerializeField] private float flameHeightTiles = 0.30f;
        /// <summary>밤에만 켜지는가.  화덕처럼 요리 중에만 켜지는 것은 false 로 두고
        ///  <see cref="ExternallyLit"/> 를 소유 컴포넌트가 제어한다.</summary>
        [SerializeField] private bool litAtNightOnly = true;

        /// <summary>소유 컴포넌트가 직접 제어할 때 쓰는 스위치 (기본 true).</summary>
        public bool ExternallyLit { get; set; } = true;

        public float RadiusTiles => radiusTiles;
        public float FlameHeightTiles => flameHeightTiles;

        public void Configure(float radius, float flameY, bool nightOnly = true)
        {
            radiusTiles = radius;
            flameHeightTiles = flameY;
            litAtNightOnly = nightOnly;
        }

        /// <summary>지금 빛나는가.</summary>
        public bool IsLit
        {
            get
            {
                if (!isActiveAndEnabled || !ExternallyLit) return false;
                if (!litAtNightOnly) return true;
                if (GameClock.Instance == null) return true;
                // 밤 곡선은 NightCurve 한 곳에만 있다 — 등잔·글로우 드라이버와 같은 값을
                //  써야 "불은 켜졌는데 어둠은 그대로" 같은 위상 어긋남이 안 생긴다.
                return LampEntity.NightFactor() >= 0.15f;
            }
        }

        // ── 목록 ────────────────────────────────────────────────────────
        private static readonly List<LightSource> _all = new List<LightSource>();

        /// <summary>살아 있는 광원들 (파괴된 것은 여기서 정리된다).</summary>
        public static List<LightSource> All
        {
            get
            {
                for (int i = _all.Count - 1; i >= 0; i--)
                    if (_all[i] == null) _all.RemoveAt(i);
                return _all;
            }
        }

        private void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        private void OnDisable() { _all.Remove(this); }
    }
}
