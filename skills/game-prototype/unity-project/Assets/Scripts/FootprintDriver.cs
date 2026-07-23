using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace MelonS.GameProto
{
    /// <summary>
    /// L4 흙 발자국 (운영자 2026-07-24 "흙에는 발자국이 잠시 남는다던지").
    /// 림이 흙(Dirt) 타일 위를 걸으면 보폭마다 좌/우 교대 발자국 데칼을 남기고
    /// 8초에 걸쳐 페이드 소멸.  풀(상한 64) 재사용 = 성능 가드, "잠시"가 핵심
    /// (영구 누적 금지).  PawnFacing 의 lane-safe 자기부착 패턴 동일 적용.
    /// </summary>
    [DisallowMultipleComponent]
    public class FootprintDriver : MonoBehaviour
    {
        private const float StrideLen = 0.55f;   // 발자국 간 이동 거리
        private const float FadeSec = 8f;
        private const float SideOffset = 0.07f;  // 좌/우 발 교대 오프셋

        private Vector3 lastPos;
        private float acc;
        private bool leftFoot;

        private void Start() { lastPos = transform.position; }

        private void Update()
        {
            Vector3 p = transform.position;
            float d = (p - lastPos).magnitude;
            lastPos = p;
            if (d < 0.0005f) { return; }   // 정지 — 누적 유지(재출발 시 자연스럽게)
            acc += d;
            if (acc < StrideLen) return;
            acc = 0f;
            leftFoot = !leftFoot;
            if (!IsOnDirt(p)) return;
            Vector3 dir = (p - transform.position).sqrMagnitude > 0.0001f
                ? (p - transform.position).normalized : Vector3.up;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f) * (leftFoot ? SideOffset : -SideOffset);
            FootprintPool.Spawn(p + perp + new Vector3(0f, -0.28f, 0f));
        }

        private static bool IsOnDirt(Vector3 worldPos)
        {
            Tilemap tm = PawnMovement.GroundTilemap;
            if (tm == null) return false;
            TileBase t = tm.GetTile(tm.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0)));
            return t != null && t.name.StartsWith("Dirt");
        }

        /// <summary>발자국 데칼 풀 — sortingOrder 1 (지형 위·데칼 레벨, 폰 9+ 아래).</summary>
        private static class FootprintPool
        {
            private const int Cap = 64;
            private static readonly SpriteRenderer[] pool = new SpriteRenderer[Cap];
            private static readonly float[] born = new float[Cap];
            private static int next;
            private static Sprite sprite;
            private static Transform root;

            public static void Spawn(Vector3 pos)
            {
                if (root == null)
                {
                    root = new GameObject("__FootprintPool__").transform;
                    var pump = root.gameObject.AddComponent<FadePump>();
                    pump.hideFlags = HideFlags.DontSave;
                }
                int i = next; next = (next + 1) % Cap;
                if (pool[i] == null)
                {
                    var go = new GameObject("fp");
                    go.transform.SetParent(root, false);
                    pool[i] = go.AddComponent<SpriteRenderer>();
                    pool[i].sprite = MakeSprite();
                    pool[i].sortingOrder = 1;
                }
                born[i] = Time.time;
                pool[i].transform.position = pos;
                pool[i].color = new Color(0.24f, 0.16f, 0.10f, 0.32f);   // 젖은 흙 톤
                pool[i].gameObject.SetActive(true);
            }

            private static Sprite MakeSprite()
            {
                if (sprite != null) return sprite;
                // 4x3 px 타원형 얼룩 — PPU 32 기준 ~0.12 유닛.  코드 생성이라 에셋 불요.
                var tex = new Texture2D(4, 3, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color32 on = new Color32(255, 255, 255, 255);
                Color32 off = new Color32(0, 0, 0, 0);
                tex.SetPixels32(new[] {
                    off, on, on, off,
                    on,  on, on, on,
                    off, on, on, off,
                });
                tex.Apply();
                sprite = Sprite.Create(tex, new Rect(0, 0, 4, 3), new Vector2(0.5f, 0.5f), 32f);
                return sprite;
            }

            /// <summary>풀 전체 페이드 — 프레임당 1루프 (풀 호스트에 상주).</summary>
            private class FadePump : MonoBehaviour
            {
                private void Update()
                {
                    for (int i = 0; i < Cap; i++)
                    {
                        var sr = pool[i];
                        if (sr == null || !sr.gameObject.activeSelf) continue;
                        float age = Time.time - born[i];
                        if (age >= FadeSec) { sr.gameObject.SetActive(false); continue; }
                        var c = sr.color;
                        c.a = 0.32f * (1f - age / FadeSec);
                        sr.color = c;
                    }
                }
            }
        }

        // ── lane-safe 자기부착 (PawnFacing 패턴) ──────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (s, _) => EnsureDriverHost();
            EnsureDriverHost();
        }

        private static void EnsureDriverHost()
        {
            if (Object.FindFirstObjectByType<AttachPump>() == null)
            {
                var go = new GameObject("__FootprintAttach__");
                go.AddComponent<AttachPump>();
            }
        }

        /// <summary>주기 스캔으로 FootprintDriver 없는 폰에 부착 (idempotent).</summary>
        private class AttachPump : MonoBehaviour
        {
            private float nextScan;
            private void Update()
            {
                if (Time.unscaledTime < nextScan) return;
                nextScan = Time.unscaledTime + 2f;
                foreach (var pm in Object.FindObjectsByType<PawnMovement>(FindObjectsSortMode.None))
                    if (pm != null && pm.GetComponent<FootprintDriver>() == null)
                        pm.gameObject.AddComponent<FootprintDriver>();
            }
        }
    }
}
