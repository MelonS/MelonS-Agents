using System.Collections.Generic;
using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>지붕이 있는 건물이 **바깥 땅에 드리우는 그림자**.
    ///
    /// 계기 (2026-08-01 운영자): "지붕이 있는집은 그림자가 생겨야 하고".
    ///
    /// 지금까지 지붕은 `RoofOverlayRenderer` 가 그리는 **실내 그늘**뿐이었다 —
    /// 지붕 아래 칸을 어둡게 덮는 것.  그건 '안이 그늘지다' 만 말하고 '건물이 서
    /// 있다' 는 말하지 않는다.  나무 한 그루도 그림자를 드리우는데 집이 안 드리우면
    /// 건물만 땅에서 떠 보인다.
    ///
    /// 방법: 지붕 셀 집합을 **태양 반대 방향으로 밀어** 한 번 더 그린다.
    ///  · 방향·길이는 `SunShadowDriver` 와 같은 태양 계산을 쓴다 — 나무·주민 그림자와
    ///    같은 시각에 같은 쪽으로 누워야 광원이 하나로 읽힌다.
    ///  · 지붕 셀 자신과 겹치는 부분은 그리지 않는다 (건물 위에 자기 그림자가 얹히면
    ///    실내 그늘과 이중으로 어두워진다).
    ///  · 정렬은 지면 위·물체 아래 — 그림자는 밟고 지나가는 것이지 가리는 것이 아니다.
    ///
    /// 자가 부팅 + 풀링 — 이 레포의 다른 오버레이 렌더러와 같은 규약.
    /// </summary>
    public class RoofShadowRenderer : MonoBehaviour
    {
        // 그림자는 지면(0~3) 위, 월드 오브젝트(YSort 밴드 110~) 아래.
        private const int ShadowSortingOrder = 3;
        private const float RebuildInterval = 0.5f;

        // 건물 '높이' — 그림자 길이의 배수.  나무(1.4)보다 크게 잡아야 집처럼 보인다.
        private const float BuildingHeight = 2.2f;

        private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        private readonly HashSet<Vector2Int> roofSet = new HashSet<Vector2Int>();
        private GameObject root;
        private float nextRebuild;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindFirstObjectByType<RoofShadowRenderer>() != null) return;
                var go = new GameObject("~RoofShadowRenderer");
                go.AddComponent<RoofShadowRenderer>();
                Debug.Log("[Boot] RoofShadowRenderer 부착 — 건물 그림자");
            });
        }

        private void Awake()
        {
            root = new GameObject("RoofShadowQuads");
            root.transform.SetParent(transform, false);
        }

        private void LateUpdate()
        {
            var roof = RoofDesignation.Instance;
            if (roof == null) { Deactivate(); return; }

            // 태양이 없으면(밤) 그림자도 없다.
            if (!SunShadowDriver.TryGetSun(out Vector2 dir, out float lenF))
            {
                Deactivate();
                return;
            }

            if (Time.unscaledTime >= nextRebuild)
            {
                nextRebuild = Time.unscaledTime + RebuildInterval;
                roofSet.Clear();
                // `Roofed` 는 **지정된** 칸(설계도 포함)이고, `IsRoofed` 가 실제로 **덮인**
                //  칸이다.  설계도는 아직 지붕이 없으니 그림자도 없다 — 이걸 안 거르면
                //  짓기 전부터 땅에 그림자가 생겨 "무엇이 이미 서 있는지" 가 거짓말이 된다.
                foreach (var c in roof.Roofed)
                    if (roof.IsRoofed(c)) roofSet.Add(c);
            }
            if (roofSet.Count == 0) { Deactivate(); return; }

            // 그림자 오프셋 — 나무와 같은 태양 계산, 건물 높이만 다르다.
            Vector2 off = dir * (BuildingHeight * Mathf.Lerp(0.30f, 1.60f, lenF));
            // 알파도 해가 낮을수록 옅게 (나무 그림자와 같은 관계).
            float a = Mathf.Lerp(0.30f, 0.16f, lenF);

            int i = 0;
            foreach (var cell in roofSet)
            {
                // 자기 건물 위에 얹히는 부분은 건너뛴다 (실내 그늘과 이중 적용 방지).
                var landing = new Vector2Int(
                    Mathf.FloorToInt(cell.x + 0.5f + off.x),
                    Mathf.FloorToInt(cell.y + 0.5f + off.y));
                if (roofSet.Contains(landing)) continue;

                var sr = GetQuad(i++);
                sr.transform.position = new Vector3(cell.x + 0.5f + off.x, cell.y + 0.5f + off.y, 0f);
                sr.color = new Color(0.06f, 0.05f, 0.09f, a);
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
            for (; i < pool.Count; i++)
                if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
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
                var go = new GameObject($"RoofShadow_{pool.Count}");
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
            shared.name = "RoofShadowQuad";
            return shared;
        }
    }
}
