using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb "조명이 실제로 맵을 밝히는 효과 필요".
    ///
    /// LampGlowDriver 가 그리는 따뜻한 바닥 풀(glow pool)은 sortingOrder 8 —
    /// 즉 NightOverlay(25) 보다 아래에 깔린다.  그래서 밤이 되어 NightOverlay 가
    /// 맵을 어둡게 덮으면 바닥 풀이 어둠에 그대로 묻혀 "램프가 맵을 밝히는" 느낌이
    /// 전혀 안 난다.  이 드라이버는 그 문제를 해결한다:
    ///
    ///   NightOverlay(25) "위"(sortingOrder 26)에 램프마다 부드러운 가산(additive)
    ///   빛 원을 그려서, 밤 어둠을 램프 주변에서 실제로 걷어낸다.  the reference sim 의
    ///   조명처럼 — 어두운 밤에 램프 주변만 은은하게 밝아 보인다.
    ///
    /// LampGlowDriver 와의 관계(둘 다 유지):
    ///   - LampGlowDriver : 바닥에 깔리는 따뜻한 amber 풀(분위기). 어둠 아래.
    ///   - LampLight(이 파일): 어둠 위에서 빛을 더해 맵을 실제로 밝힘(가시성).
    ///   두 레이어가 합쳐져 "불빛이 어둠을 밀어낸다"는 콜로니심식 조명이 된다.
    ///
    /// 패턴(NightLightPoolDriver / LampGlowDriver / FlickerLight 와 동일):
    ///   - [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] — SceneSetup 편집 불필요.
    ///   - 숨김 DontDestroyOnLoad GameObject + Find idempotency 가드.
    ///   - GameClock / LampEntity 읽기 전용 폴링(결정적, 사이드이펙트 없음).
    ///   - Time.unscaledTime 델타만 사용, WaitForSeconds 없음 → 백그라운드 프리즈
    ///     위험 없음(긴 코루틴 자체가 없음).
    ///   - per-frame AudioBank 호출/무거운 연산 없음.  스프라이트/머티리얼은 Awake
    ///     에서 1회 절차적 생성 후 재사용.
    ///
    /// 가산 블렌드를 쓰는 이유:
    ///   NightOverlay 는 어두운 색을 알파 블렌드로 덮는다.  그 "위"에 가산으로
    ///   따뜻한 빛을 더하면, 램프 주변 픽셀의 밝기가 올라가 어둠이 걷힌 것처럼
    ///   보인다(맵이 실제로 밝아짐).  알파 블렌드였다면 어둠을 그저 다른 색으로
    ///   덮을 뿐 "밝아지는" 느낌이 안 난다.  그래서 여기서는 의도적으로 가산.
    ///   (LampGlowDriver 바닥 풀은 반대로 알파 블렌드가 맞다 — 거긴 밝히는 게
    ///   아니라 따뜻한 색을 깔기 때문.)
    ///
    /// >>> QA FLAG: 깊은 밤에 램프 주변이 부드럽게 밝아 보여야 한다(어둠이 걷힘).
    ///     낮에는 빛 원이 보이지 않는다.  맵을 팬/줌 해도 빛 원이 램프에 정확히
    ///     붙어 따라간다.  pawn 실루엣을 하얗게 씻어버리지 않는다(은은한 강도).
    /// </summary>
    public class LampLight : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Day/night 커브는 공용 NightCurve(LampEntity.cs) 로 통합(2026-06-01). //
        //  이전엔 드라이버마다 동일 스톱 배열을 복붙해 드리프트 위험이 있었다.  //
        //  이제 NightOverlay / LampGlowDriver / LampLight / LampEntity 가 같은   //
        //  곡선을 읽어 점등·소등 위상이 항상 일치한다.                          //
        // ------------------------------------------------------------------ //

        // ------------------------------------------------------------------ //
        //  외형 튜너블 (SerializeField 대신 const — 이 드라이버는 절차 생성    //
        //  GameObject 라 인스펙터가 없다. 디자이너 튜닝은 LampGlowDriver 와     //
        //  동일하게 코드 상단 한 곳에서.)                                       //
        // ------------------------------------------------------------------ //

        // #267 the reference sim 라이트 글로우 캡은 0.5.  가산이라 약간 높게 잡아도 어둠 위에서
        //  은은하게 밝히는 수준(하얗게 안 씀).
        private const float MaxLightIntensity = 0.62f;

        // 빛 원의 월드 지름(칸) = 전체 반경 4칸(=LightScale/2).  the reference sim 토치램프(전체
        //  반경 10, lit 6.5)를 작은 집 규모로 축소.  #267 falloff 를 the reference sim 공식
        //  (중심 1/d² 집중 + 선형, BuildProceduralLightSprite)으로 바꿔 "뿌연 안개"가
        //  아니라 중심이 또렷이 밝고 가장자리로 깔끔히 떨어지는 조명이 된다.
        private const float LightScale = 8.0f;

        // NightOverlay(25) "위", floating bars(29-31) "아래".  pawn(9-11) 위라서
        // 빛이 pawn 도 함께 밝히지만 가산이라 실루엣을 지우지 않고 비추기만 한다.
        private const int LightSortingOrder = 26;

        private const float LocalZ = 0.01f;

        // 새로 지은 램프를 잡기 위한 재스캔 주기.
        private const float ScanInterval = 1.5f;

        // ------------------------------------------------------------------ //
        //  런타임 상태                                                          //
        // ------------------------------------------------------------------ //

        private readonly Dictionary<LampEntity, SpriteRenderer> _lightMap =
            new Dictionary<LampEntity, SpriteRenderer>(8);

        private Sprite   _lightSprite;
        private Material _lightMat;
        private float    _nextScan;

        // ------------------------------------------------------------------ //
        //  Bootstrap (SceneSetup 편집 불필요)                                  //
        // ------------------------------------------------------------------ //

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // #268 비활성화 — 이 가산(additive) 빛 안개가 "조명이 뿌옇게만 보임"의
            //  원인이었다.  콜로니심식 조명은 NightOverlay 의 동적 라이트맵(램프 반경의
            //  어둠을 걷어 바닥이 드러남)으로 통합됐다.  중복·뿌연 안개 방지 위해 미스폰.
            return;
#pragma warning disable CS0162
            // 게임 씬 게이트: MainMenu 에서는 절대 스폰하지 않는다.
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindDriver() != null) return;

                var go = new GameObject("~LampLight");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                go.AddComponent<LampLight>();
            });
        }

        private static LampLight FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<LampLight>();
#else
            return Object.FindObjectOfType<LampLight>();
#endif
        }

        // ------------------------------------------------------------------ //
        //  Lifecycle                                                           //
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            _lightSprite = BuildProceduralLightSprite();
            _lightMat    = CreateAdditiveLightMaterial();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + ScanInterval;
                ScanLamps();
            }

            float dayAlpha  = ComputeNightAlpha();
            float intensity = dayAlpha * MaxLightIntensity;

            foreach (var kv in _lightMap)
            {
                var lamp = kv.Key;
                var sr   = kv.Value;
                if (sr == null) continue;

                bool isLit = (lamp != null) && lamp.IsLit;
                float finalA = isLit ? intensity : 0f;

                if (lamp != null)
                {
                    // 빛 중심을 불꽃 머리 쪽으로 살짝 올려 "불꽃에서 빛이 난다"
                    // 느낌을 준다 (LampGlowDriver 와 동일한 FlameHeightCells).
                    sr.transform.position = new Vector3(
                        lamp.transform.position.x,
                        lamp.transform.position.y + LampEntity.FlameHeightCells,
                        lamp.transform.position.z + LocalZ);
                }

                Color c = sr.color;
                c.a = finalA;
                sr.color = c;
            }
        }

        private void OnDestroy()
        {
            foreach (var kv in _lightMap)
            {
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            }
            _lightMap.Clear();
        }

        // ------------------------------------------------------------------ //
        //  Lamp scan                                                           //
        // ------------------------------------------------------------------ //

        private void ScanLamps()
        {
            // 죽은 엔트리 제거(램프 철거/파괴).
            var toRemove = new List<LampEntity>(4);
            foreach (var kv in _lightMap)
            {
                if (kv.Key == null || kv.Value == null)
                    toRemove.Add(kv.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                var dead = toRemove[i];
                if (_lightMap.TryGetValue(dead, out var sr) && sr != null)
                    Destroy(sr.gameObject);
                _lightMap.Remove(dead);
            }

#if UNITY_2023_1_OR_NEWER
            var lamps = Object.FindObjectsByType<LampEntity>(FindObjectsSortMode.None);
#else
            var lamps = Object.FindObjectsOfType<LampEntity>();
#endif
            for (int i = 0; i < lamps.Length; i++)
                EnsureLight(lamps[i]);
        }

        private void EnsureLight(LampEntity lamp)
        {
            if (lamp == null) return;
            if (_lightMap.ContainsKey(lamp)) return;

            // 램프가 아니라 이 드라이버 GO 의 자식으로 둔다(엔티티 파일 미편집).
            // Update 가 매 프레임 램프 위치로 재배치.
            var go = new GameObject("LampLightDisc");
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = new Vector3(
                lamp.transform.position.x,
                lamp.transform.position.y + LampEntity.FlameHeightCells,
                lamp.transform.position.z + LocalZ);
            go.transform.localScale = Vector3.one * LightScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _lightSprite;
            sr.material     = _lightMat;
            sr.sortingOrder = LightSortingOrder;
            sr.color = new Color(1f, 1f, 1f, 0f);  // 밤이 깊어지며 페이드 인

            _lightMap[lamp] = sr;
        }

        // ------------------------------------------------------------------ //
        //  Day/night 보간 (GameClock 읽기 전용)                                //
        // ------------------------------------------------------------------ //

        private static float ComputeNightAlpha()
        {
            if (GameClock.Instance == null) return 0f;
            // 공용 NightCurve: 한낮 0 … 한밤 1.  IsLit 게이트가 임계 미만이면 어차피
            // 0 으로 꺼지므로, 여기선 밝기 변조만 담당.
            return NightCurve.Sample(GameClock.Instance.DayProgress);
        }

        // ------------------------------------------------------------------ //
        //  스프라이트 + 머티리얼 (절차 생성 — 에셋 의존 없음)                  //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// 64x64 부드러운 방사형 빛 원.  중심은 따뜻한 흰빛(거의 흰색에 가까운
        /// 따뜻한 톤)으로, 가산 블렌드 시 어둠을 걷어내며 밝아 보이게 한다.
        /// 가장자리는 cos^2 falloff 로 매우 부드럽게 0 으로 떨어져 경계가 안 보인다.
        /// 32 대신 64 px — 가산 빛은 경계 밴딩이 더 눈에 띄어서 해상도를 올림.
        /// </summary>
        private static Sprite BuildProceduralLightSprite()
        {
            const int SIZE = 64;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;  // 부드러운 빛 — Point 아님
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = "LampLightDisc_Proc";

            float cx = SIZE * 0.5f;
            float cy = SIZE * 0.5f;
            float rMax = SIZE * 0.5f;

            var pixels = new Color32[SIZE * SIZE];
            for (int y = 0; y < SIZE; y++)
            {
                for (int x = 0; x < SIZE; x++)
                {
                    float dx   = (x + 0.5f) - cx;
                    float dy   = (y + 0.5f) - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist >= rMax)
                    {
                        pixels[y * SIZE + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float t = dist / rMax;                       // 0 중심, 1 가장자리
                    // #267 the reference sim 글로우 falloff 공식 적용 (reference-simwiki Torch lamp):
                    //   r=반경(칸), d=거리+1, a=1-d/r(선형), bq=1/d²(중심집중),
                    //   f=a+(bq-a)*0.4.  중심값으로 정규화 → 또렷한 코어 + 깔끔한 감쇠.
                    //   (이전 1-smoothstep 은 균일하게 흐려 "뿌옇게" 보였음.)
                    float rTiles    = LightScale * 0.5f;
                    float distTiles = t * rTiles;
                    float d  = distTiles + 1f;
                    float aL = 1f - d / rTiles;
                    float bq = 1f / (d * d);
                    float f  = aL + (bq - aL) * 0.4f;
                    // 중심(거리0 → d=1) 정규화값
                    float ac = 1f - 1f / rTiles;
                    float fc = ac + (1f - ac) * 0.4f;
                    float falloff = Mathf.Clamp01(f / Mathf.Max(0.0001f, fc));

                    // #267 촛불색: 오렌지/붉은 따뜻한 불꽃색.  중심 밝은 오렌지 → 가장자리
                    //  짙은 적황.  너무 어둡지 않게(혈색 아님) 불빛 톤.
                    byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 214f, t));
                    byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(168f,  92f, t));
                    byte b = (byte)Mathf.RoundToInt(Mathf.Lerp( 86f,  40f, t));
                    byte a = (byte)Mathf.RoundToInt(falloff * 255f);

                    pixels[y * SIZE + x] = new Color32(r, g, b, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);

            return Sprite.Create(
                tex,
                new Rect(0, 0, SIZE, SIZE),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 64f);
        }

        /// <summary>
        /// 가산(additive) 머티리얼 — SrcAlpha / One.  어둠 위에 빛을 "더해"
        /// 맵을 밝게 만든다.  알파 블렌드(LampGlowDriver) 와는 의도적으로 반대:
        /// 거긴 따뜻한 색을 깔기 위함, 여긴 어둠을 걷어내기 위함.
        /// Shader.Find 실패 시 InternalErrorShader 로 폴백(배치모드 안전).
        /// </summary>
        private static Material CreateAdditiveLightMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");

            var mat = new Material(shader) { name = "LampLightAdditiveMat" };

            // 가산 블렌드: SrcAlpha / One.
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f);

            return mat;
        }
    }
}
