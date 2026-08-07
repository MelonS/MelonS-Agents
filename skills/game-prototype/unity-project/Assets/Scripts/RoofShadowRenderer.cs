using System.Collections.Generic;
using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>건물이 땅에 눕히는 그림자 — **벽 외곽선 하나를 통째로** 투영한다.
    ///
    /// 계기 (2026-08-01 운영자 "지붕이 있는집은 그림자가 생겨야 하고"),
    /// 재작업 (2026-08-02 운영자 "건물그림자 제대로 그린거 맞아? 좀 이상한거 같은데"
    /// → "레퍼런스에서 지붕그림자 그리는 방식 참고해서").
    ///
    /// ── 레퍼런스가 실제로 하는 방식 (개발자 블로그 원문) ────────────────────
    ///   "shadows from the sun that fade in and out based on **time of day**,
    ///    **move across the ground**, **don't shade indoors**, come from people and
    ///    small objects as well as **large structures**, project to **varying
    ///    heights**, and change color slightly to **complement the color of the sky**"
    ///   (Ludeon 개발 블로그, Sun shadows, 2013-08)
    ///
    /// 여기서 읽어야 할 것은 **그림자를 던지는 주체가 지붕이 아니라 물체**라는 점이다.
    /// 지붕은 오히려 '그림자를 받지 않는 곳'(don't shade indoors)으로만 쓰인다.
    ///
    /// ── 이 파일이 두 번 틀렸던 기록 ────────────────────────────────────────
    ///  1차: **지붕 다각형**을 태양 반대로 밀어 그렸다.  지붕 다각형 = 건물 발자국이라
    ///       밀어낸 칸이 대부분 건물 자신 위에 떨어져 걸러진다.  로그로 확인:
    ///       `지붕칸=21 그림자=9장` — 남는 건 벽 뒤에 가리는 조각뿐이라 08시에만
    ///       보이고 11/14/17시엔 사라졌다(4시각 스크린샷 실측).
    ///  2차: 밑동 테두리(접지선)로 축소했다.  네 시각 모두 보이긴 했지만 그건
    ///       '서 있다'까지고 '그림자가 드리웠다'는 아니다.
    ///
    /// ── 그래서 지금 방식 ───────────────────────────────────────────────────
    /// 캐스터는 **벽/문 칸의 집합**이다 (= 건물의 실루엣).  그걸 태양 반대 방향으로
    /// 쓸어 눕힌다.  지붕은 '실내 판정'에만 쓴다.
    ///
    /// `WallEntity` 주석이 2026-07-31 에 이미 정답을 적어 두었다 — 그때는 벽마다
    /// **개별 스프라이트 그림자**를 붙였다가 "집 한 채가 사각 그림자 수십 개의
    /// 지저분한 덩어리"가 되어 되돌렸고, 주석은 *"방 단위 외곽선을 뽑아 하나로
    /// 눕혀야 하는데 그건 별도 작업"* 이라고 남겼다.  이 파일이 그 별도 작업이다.
    /// 겹침이 더러워지는 원인은 **알파가 쌓이는 것**이었으므로, 칸 단위로 중복을
    /// 제거하면(`drawn`) 몇 겹이 겹치든 농도가 균일한 한 덩어리가 된다.
    ///
    /// 색과 농도는 `SunShadowDriver` 가 나무 그림자에 쓰는 값을 **그대로 받아 쓴다**.
    /// 따로 정하면 같은 화면에서 나무 그림자와 집 그림자의 색이 갈려 광원이 둘로 보인다.
    /// </summary>
    public class RoofShadowRenderer : MonoBehaviour
    {
        // 그림자는 지면(0~3) 위, 월드 오브젝트(YSort 밴드 110~) 아래.
        private const int ShadowSortingOrder = 3;
        private const float RebuildInterval = 0.5f;

        /// <summary>벽 높이 (칸).  투영 길이 = 이 값 x 태양 고도 계수 — 나무와 **같은 공식**.
        ///
        /// 1.15 → 1.7 (2026-08-02).  칸 단위로 찍는 그림자는 길이가 1칸 남짓이면
        ///  **대각선이 살아남지 못한다** — 세로 오프셋이 0.5 미만이라 전부 0으로
        ///  반올림돼 수평 띠가 된다(운영자 "대각선으로 생겨야할 상황에 제대로 안되는거
        ///  같은데").  나무(1.4)보다 조금 크게 두어 계단이 두 칸 이상 생기게 한다.</summary>
        private const float WallHeight = 1.7f;

        private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        private readonly HashSet<Vector2Int> casters = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> drawn = new HashSet<Vector2Int>();
        private GameObject root;
        private float nextRebuild;
        private int lastCasterCount = -1;
        private bool lastHadShadow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindFirstObjectByType<RoofShadowRenderer>() != null) return;
                var go = new GameObject("~BuildingShadowRenderer");
                go.AddComponent<RoofShadowRenderer>();
                Debug.Log("[Boot] 건물 그림자 — 벽 외곽선 투영");
            });
        }

        private void Awake()
        {
            root = new GameObject("BuildingShadowQuads");
            root.transform.SetParent(transform, false);
        }

        private void LateUpdate()
        {
            // 밤이면 그림자가 없다 (레퍼런스: fade in and out based on time of day).
            if (!SunShadowDriver.TryGetSun(out Vector2 dir, out float lenF)
                || SunShadowDriver.ShadowAlphaMul <= 0.01f)
            {
                Deactivate();
                return;
            }

            if (Time.unscaledTime >= nextRebuild)
            {
                nextRebuild = Time.unscaledTime + RebuildInterval;
                RebuildCasters();
            }
            if (casters.Count == 0) { Deactivate(); return; }

            var roof = RoofDesignation.Instance;

            // 나무와 **같은 공식** — 길이 ∝ 높이 x 태양 고도 계수.
            Vector2 full = dir * (WallHeight * Mathf.Lerp(0.30f, 1.60f, lenF));
            // 세로를 누르지 않는다.  `SunShadowDriver` 가 _ShearY 에 0.55 를 쓰는 것은
            //  **서 있는 스프라이트의 윗변을 미는 전단**이라 성격이 다르다.  여기서는
            //  땅에 눕는 투영이므로 태양 벡터를 그대로 따라야 하고, 실제로 나무 그림자를
            //  확대해 보면 08시 좌상 / 11시 위 / 17시 우상으로 **세로 성분이 살아 있다**.
            //  0.55 를 곱하던 1차 구현은 그 성분을 0.34칸으로 줄였고, 정수 칸 반올림에서
            //  전부 0이 되어 어느 시각에도 수평 띠만 나왔다.

            // 광선을 **0.35칸 간격**으로 훑는다.  간격이 성기면 대각선 중간 칸이 비어
            //  점선처럼 끊긴다 (1차 구현은 3스텝뿐이라 계단이 아예 안 생겼다).
            int steps = Mathf.Clamp(Mathf.CeilToInt(full.magnitude / 0.35f), 1, 24);

            float alpha = 0.30f * Mathf.Clamp01(SunShadowDriver.ShadowAlphaMul);
            Color tint = SunShadowDriver.ShadowTint;

            drawn.Clear();
            int i = 0;
            for (int s = 1; s <= steps; s++)
            {
                Vector2 off = full * (s / (float)steps);
                foreach (var cell in casters)
                {
                    var landing = new Vector2Int(
                        Mathf.RoundToInt(cell.x + off.x),
                        Mathf.RoundToInt(cell.y + off.y));
                    if (casters.Contains(landing)) continue;          // 벽 자신 위
                    if (roof != null && roof.IsRoofed(landing)) continue;  // 실내는 안 어둡게
                    if (!drawn.Add(landing)) continue;                // 알파 누적 금지

                    var sr = GetQuad(i++);
                    sr.transform.position = new Vector3(landing.x + 0.5f, landing.y + 0.5f, 0f);
                    var c = tint; c.a = alpha;
                    sr.color = c;
                    if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                }
            }
            for (; i < pool.Count; i++)
                if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);

            if (casters.Count != lastCasterCount || (i > 0) != lastHadShadow)
            {
                lastCasterCount = casters.Count;
                lastHadShadow = i > 0;
                Debug.Log($"[BuildingShadow] 벽칸={casters.Count} 그림자={i}장 "
                          + $"길이={full.magnitude:F2}칸");
            }
        }

        /// <summary>캐스터 = 지금 서 있는 벽·문 칸.  매 프레임 찾지 않고 0.5초마다
        ///  갱신한다 (건물은 자주 바뀌지 않는다).</summary>
        private void RebuildCasters()
        {
            casters.Clear();
            foreach (var w in FindObjectsByType<WallEntity>(FindObjectsSortMode.None))
            {
                if (w == null) continue;
                casters.Add(new Vector2Int(Mathf.FloorToInt(w.transform.position.x),
                                           Mathf.FloorToInt(w.transform.position.y)));
            }
            foreach (var d in FindObjectsByType<DoorEntity>(FindObjectsSortMode.None))
            {
                if (d == null) continue;
                casters.Add(new Vector2Int(Mathf.FloorToInt(d.transform.position.x),
                                           Mathf.FloorToInt(d.transform.position.y)));
            }
        }

        private void Deactivate()
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && pool[i].gameObject.activeSelf)
                    pool[i].gameObject.SetActive(false);
        }

        private SpriteRenderer GetQuad(int i)
        {
            while (pool.Count <= i)
            {
                var go = new GameObject($"BuildingShadow_{pool.Count}");
                go.transform.SetParent(root.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ShadeSprite();
                sr.sortingOrder = ShadowSortingOrder;
                go.SetActive(false);
                pool.Add(sr);
            }
            return pool[i];
        }

        private static Sprite shared;
        private static Sprite ShadeSprite()
        {
            if (shared != null) return shared;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            shared = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            shared.name = "BuildingShadowQuad";
            return shared;
        }
    }
}
