using UnityEngine;
using UnityEngine.SceneManagement;

namespace MelonS.GameProto
{
    /// <summary>
    /// 룩앤필 배치1 (2026-07-24): 시간대별 전역 컬러 그레이딩.
    /// NightOverlay(어둠을 "덮는" 레이어)와 상보 — 이쪽은 채도/대비/틴트를 조율해
    /// 한낮은 쨍하게, 황혼은 앰버, 밤은 딥블루+저채도, 새벽은 청보라로.
    /// GameClock.DayProgress 스톱 보간, Camera.main 에 self-bootstrap.
    /// 셰이더 미지원/로드 실패 시 무동작(안전 측) — Blit 원본 통과.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ColorGradeDriver : MonoBehaviour
    {
        private struct GStop
        {
            public float t;          // DayProgress 0~1 (0=자정)
            public float sat, con;
            public Color tint, lift;
            public GStop(float t, float sat, float con, Color tint, Color lift)
            { this.t = t; this.sat = sat; this.con = con; this.tint = tint; this.lift = lift; }
        }

        // 스톱 타임라인 — NightOverlay DARK_STOPS 와 같은 시계 (0.27~06:30 일출,
        //  0.5=정오, 0.77~18:30 일몰, 0.83~20:00 황혼).  값은 은은하게 — 픽셀아트
        //  팔레트를 존중하고 "필터 낀 화면"이 되지 않게 (리서치 '하지 말 것' 준수).
        private static readonly GStop[] STOPS =
        {
            new GStop(0.00f, 0.86f, 1.02f, new Color(0.86f, 0.90f, 1.06f), new Color(0.010f, 0.014f, 0.030f)),  // 자정 딥블루
            new GStop(0.23f, 0.90f, 1.02f, new Color(0.94f, 0.90f, 1.04f), new Color(0.012f, 0.008f, 0.022f)),  // 새벽 청보라
            new GStop(0.29f, 1.04f, 1.03f, new Color(1.06f, 0.98f, 0.90f), new Color(0.014f, 0.008f, 0.000f)),  // 일출 앰버
            // 주간 채도 (2026-07-27): 1.10 부스트 → 0.94 로.  가상 유저 평가에서 아트 디렉터가
            //  픽셀 실측으로 지적 — 잔디 H68 S58 V64 / 모래 H46 S58 V93 으로 **색상각 차이가 22°인데
            //  둘 다 채도 58** 이라 화면 전체가 산성 라임톤이 되고, 지형이 명도 최상단을 독점해
            //  캐릭터(V12)가 나무(V13)와 같은 밴드로 묻혔다.  반면 새벽 스톱(0.90)은 "절제된 웜
            //  하모니"로 호평 — 즉 정답 레퍼런스가 이미 이 배열 안에 있었고, 주간만 반대로 가 있었다.
            //  채도를 내리면 (a) 산성톤 해소 (b) 지형이 명도 최상단에서 내려와 캐릭터 위계가
            //  자동 회복 (c) 인게임 톤이 타이틀 키아트의 웜 저채도 쪽으로 이동 — 스프라이트는
            //  한 장도 새로 그리지 않는다.  대비도 1.05→1.03 으로 함께 완화.
            new GStop(0.42f, 0.94f, 1.03f, new Color(1.015f, 1.005f, 0.985f), Color.black),                     // 한낮
            new GStop(0.62f, 0.94f, 1.03f, new Color(1.015f, 1.005f, 0.985f), Color.black),                     // 오후
            new GStop(0.77f, 1.06f, 1.04f, new Color(1.09f, 0.97f, 0.87f), new Color(0.020f, 0.008f, 0.000f)),  // 일몰 골든아워
            new GStop(0.83f, 0.96f, 1.03f, new Color(1.00f, 0.90f, 0.98f), new Color(0.014f, 0.006f, 0.020f)),  // 황혼 보라
            new GStop(0.90f, 0.86f, 1.02f, new Color(0.86f, 0.90f, 1.06f), new Color(0.010f, 0.014f, 0.030f)),  // 밤 딥블루
            new GStop(1.00f, 0.86f, 1.02f, new Color(0.86f, 0.90f, 1.06f), new Color(0.010f, 0.014f, 0.030f)),  // 자정 랩
        };

        private Material _mat;

        private void Awake()
        {
            // Resources 로드 = 빌드 포함 보장 (머티리얼 무참조 셰이더는 스트립되는 함정).
            var sh = Resources.Load<Shader>("Shaders/ColorGrade");
            if (sh == null) sh = Shader.Find("MelonS/ColorGrade");
            if (sh != null && sh.isSupported) _mat = new Material(sh);
            if (_mat == null) enabled = false;
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null) { Graphics.Blit(src, dst); return; }
            float t = GameClock.Instance != null ? GameClock.Instance.DayProgress : 0.5f;
            int hi = 1;
            while (hi < STOPS.Length - 1 && STOPS[hi].t < t) hi++;
            var a = STOPS[hi - 1]; var b = STOPS[hi];
            float s = Mathf.InverseLerp(a.t, b.t, t);
            _mat.SetFloat("_Saturation", Mathf.Lerp(a.sat, b.sat, s));
            _mat.SetFloat("_Contrast", Mathf.Lerp(a.con, b.con, s));
            _mat.SetColor("_Tint", Color.Lerp(a.tint, b.tint, s));
            _mat.SetColor("_Lift", Color.Lerp(a.lift, b.lift, s));
            Graphics.Blit(src, dst, _mat);
        }

        // ── self-bootstrap: Game 씬 메인 카메라에 부착 ──────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (sc, _) => Ensure();
            Ensure();
        }

        private static void Ensure()
        {
            if (SceneManager.GetActiveScene().name != "Game") return;
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<ColorGradeDriver>() == null)
                cam.gameObject.AddComponent<ColorGradeDriver>();
                Debug.Log("[Boot] ColorGradeDriver 부착");
        }
    }
}
