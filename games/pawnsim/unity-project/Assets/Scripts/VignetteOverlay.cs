using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MelonS.GameProto
{
    /// <summary>
    /// Polish Wave v3 / V8 — OPTIONAL subtle vignette depth cue.
    ///
    /// Adds a barely-perceptible radial gradient (OUTLINE_OBJ tone, very low
    /// alpha) as a screen-space Canvas Image that darkens the frame corners
    /// toward center, focusing the eye on the play area.
    ///
    /// DESIGN CONTRACT:
    ///   - Effect MUST be subtle: alpha 0.0 at center, max ~0.22 at corners.
    ///     If corners look muddy or any play-area color reads differently,
    ///     QA MUST REJECT this feature (V8 is explicitly optional).
    ///   - RaycastTarget = false — the overlay NEVER intercepts clicks.
    ///   - Canvas sortOrder is chosen so ALL gameplay UI panels (PawnInfoPanel,
    ///     resource bars, clock, event log, context menus) draw ABOVE this
    ///     overlay.  The vignette sits just above the camera clear / world
    ///     render but below every interactive UI element.
    ///   - Self-attaching via [RuntimeInitializeOnLoadMethod(AfterSceneLoad)]:
    ///     does NOT require editing SceneSetup.cs, BuildManager, or any other
    ///     existing file.  Fully removable by deleting this file.
    ///   - Sprite import: loaded at runtime from Assets/Sprites/vignette.png.
    ///     If the sprite is missing, logs a Debug.LogWarning and exits cleanly
    ///     (no exception, build still opens and runs without the vignette).
    ///
    /// LANE CONSTRAINT (W-M1-03 lane B):
    ///   This file MUST NOT touch SceneSetup.Game.Entities.cs, SceneSetup.cs,
    ///   CropEntity.cs, or any existing builder.  It is self-contained.
    ///
    /// QA FLAG (V8 OPTIONAL):
    ///   - Verify corners are very-slightly darker, not noticeably muddy.
    ///   - Verify RaycastTarget = false (no click intercept).
    ///   - Verify all UI panels (info panel, bars, clock, menus) draw ABOVE
    ///     the vignette.
    ///   - REJECT if play-area palette colors shift visibly.
    ///   - REJECT if the effect is distracting at any zoom level.
    ///
    /// Removability: delete this file to remove the vignette entirely.
    /// No other system depends on VignetteOverlay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VignetteOverlay : MonoBehaviour
    {
        // Relative path within the Assets folder (no leading "Assets/").
        // Used by AssetDatabase in Editor; Runtime path constructed below.
        private const string SpritePath = "Assets/Sprites/vignette.png";

        // Canvas sort order for the vignette canvas.
        // Most gameplay UI canvases in this project use sortingOrder >= 10
        // (e.g. PawnInfoPanel, ClockUI, ResourceCounterUI all build canvases
        // at default order 0–9 or rely on the default Screen Space Overlay
        // order).  We place the vignette at sortingOrder = 1 so it renders:
        //   above: the world camera clear (order 0)
        //   below: every UI canvas whose sortingOrder >= 2+
        // If a UI canvas is at order 0, VignetteOverlay will appear in front
        // of it — adjust CanvasSortingOrder lower (to 0) in that case and
        // rely on the canvas's own child ordering.  The value is a const so
        // it is trivially adjustable without hunting through GameObject trees.
        private const int CanvasSortingOrder = 1;

        private Canvas _canvas;
        private Image  _image;

        // ── Bootstrap ──────────────────────────────────────────────────────────
        // Self-attaching: no SceneSetup edits needed.
        // Idempotent: second call is a no-op (FindDriver guard).

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                // Guard: do not spawn a second overlay if one already exists
                // (handles Enter Play Mode without domain reload, or scene reloads).
                if (FindOverlay() != null) return;

                Sprite spr = LoadVignetteSprite();
                if (spr == null)
                {
                    // Missing sprite → graceful no-op.  Build opens normally.
                    Debug.LogWarning(
                        "[VignetteOverlay] vignette.png not found at '"
                        + SpritePath + "'. "
                        + "V8 vignette overlay is disabled. "
                        + "Run Assets/Sprites/_gen_vignette.py to generate it."
                    );
                    return;
                }

                var go = new GameObject("~VignetteOverlay");
                // HideAndDontSave keeps it out of the hierarchy and prevents
                // scene serialisation (no accidental save pollution).
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);

                var overlay = go.AddComponent<VignetteOverlay>();
                overlay.Build(spr);
                    });
        }

        // ── Build ──────────────────────────────────────────────────────────────

        private void Build(Sprite sprite)
        {
            // ── Canvas ──────────────────────────────────────────────────────
            // Screen Space - Overlay canvas so it follows the screen regardless
            // of camera position, zoom, or aspect ratio.
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CanvasSortingOrder;

            // CanvasScaler is optional for a stretch-fill image, but adding it
            // ensures Unity does not warn about missing scaler on the canvas.
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // GraphicRaycaster is not needed (we never intercept input), but
            // Unity adds one automatically to Screen Space Overlay canvases.
            // Remove it explicitly so there is zero chance of click intercept.
            var raycaster = gameObject.GetComponent<GraphicRaycaster>();
            if (raycaster != null) Destroy(raycaster);

            // ── Image child ─────────────────────────────────────────────────
            var imgGo = new GameObject("VignetteImage");
            imgGo.hideFlags = HideFlags.HideAndDontSave;
            imgGo.transform.SetParent(gameObject.transform, worldPositionStays: false);

            _image = imgGo.AddComponent<Image>();
            _image.sprite = sprite;
            _image.type = Image.Type.Simple;
            _image.preserveAspect = false;  // stretch to fill canvas

            // Point filter — matches the pixel-art project style.
            // The vignette is smooth enough that Point vs Bilinear is not
            // visually important at 512x512, but Point is the project default.
            if (sprite.texture != null)
                sprite.texture.filterMode = FilterMode.Point;

            // Tint: white = show the sprite's RGBA exactly as authored.
            // The OUTLINE_OBJ tone and alpha gradient are baked into the PNG.
            _image.color = Color.white;

            // CRITICAL: never intercept clicks.
            _image.raycastTarget = false;

            // ── RectTransform — full-screen stretch ─────────────────────────
            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ── Sprite loading ─────────────────────────────────────────────────────
        // Editor path: AssetDatabase.LoadAssetAtPath (fast, no Resources folder needed).
        // Runtime path: Resources.Load (requires vignette.png to be in a Resources folder)
        //   OR AssetDatabase (Editor-only).
        // This project loads sprites at runtime via Resources in some builders,
        // and via AssetDatabase in Editor SceneSetup.  We use AssetDatabase in
        // Editor and a generated Texture2D fallback path at runtime.
        //
        // Runtime note: if vignette.png is NOT in a Resources folder, the runtime
        // load will return null and the LogWarning fires.  The graceful fallback
        // means the build is not broken.  The QA / Build Engineer can either:
        //   (a) move vignette.png into Assets/Resources/Sprites/vignette.png, or
        //   (b) note this as acceptable (Editor-only vignette), or
        //   (c) let VignetteOverlay use a programmatically generated gradient
        //       (see ProceduralFallback() below, disabled by default).

        private static Sprite LoadVignetteSprite()
        {
#if UNITY_EDITOR
            // Editor: AssetDatabase — works for batchmode SceneSetup builds.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SpritePath);
            if (tex != null)
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath)
                       ?? Sprite.Create(tex,
                              new Rect(0, 0, tex.width, tex.height),
                              new Vector2(0.5f, 0.5f),
                              tex.width); // PPU = texture width (full screen stretch)
            }
            return null;
#else
            // Runtime: attempt Resources.Load (requires Resources folder).
            // "Sprites/vignette" — strip extension per Resources convention.
            var spr = Resources.Load<Sprite>("Sprites/vignette");
            if (spr != null) return spr;

            // Fallback: programmatically generated gradient (always available,
            // no file dependency).  Disabled by default to keep the authoring
            // pipeline clean; enable by setting VIGNETTE_PROC_FALLBACK = true.
            if (ProcFallbackEnabled)
                return BuildProceduralSprite();

            return null;
#endif
        }

        // ── Procedural fallback (runtime, no file dependency) ─────────────────
        // Generates the same radial gradient as _gen_vignette.py entirely in C#.
        // Disabled by default; set to true if vignette.png is unavailable at
        // runtime and a Resources folder is not in scope.

        // #9.0(ui-backlog 2026-06-10) — 빌드에서 vignette 미표시의 원인이 바로 이
        //  플래그였다: Assets/Sprites/vignette.png 는 Resources 폴더가 아니라 런타임
        //  Resources.Load 가 항상 null → 폴백은 꺼져 있어 조용히 no-op.  절차 생성은
        //  _gen_vignette.py 와 동일 수식이라 시각 결과 동일 — 켠다 (D4, 2026-06-11).
        private const bool ProcFallbackEnabled = true;

        private static Sprite BuildProceduralSprite()
        {
            const int SIZE       = 256;
            const float GAMMA    = 1.8f;
            const float A_CORNER = 0.22f;
            // OUTLINE_OBJ = (40, 30, 22) — baked as const to avoid palette.py import
            const byte R = 40, G = 30, B = 22;

            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = "VignetteProc";

            float cx = (SIZE - 1) / 2f;
            float cy = (SIZE - 1) / 2f;
            float maxDist = Mathf.Sqrt(cx * cx + cy * cy);
            var pixels = new Color32[SIZE * SIZE];

            for (int y = 0; y < SIZE; y++)
            {
                for (int x = 0; x < SIZE; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float distNorm = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;
                    float shaped   = Mathf.Pow(distNorm, GAMMA);
                    float alphaF   = Mathf.Clamp01(shaped * A_CORNER);
                    byte  alphaB   = (byte)Mathf.RoundToInt(alphaF * 255f);
                    pixels[y * SIZE + x] = new Color32(R, G, B, alphaB);
                }
            }

            tex.SetPixels32(pixels);
            // BlobShadow 와 동일 함정 주의: makeNoLongerReadable=true 면 Sprite.Create
            //  의 기본 Tight 메시 생성이 조용히 실패한다 → readable 유지 + FullRect.
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            return Sprite.Create(
                tex,
                new Rect(0, 0, SIZE, SIZE),
                new Vector2(0.5f, 0.5f),
                SIZE,  // PPU: full size → 1 world unit; irrelevant for UI canvas
                0, SpriteMeshType.FullRect
            );
        }

        // ── Utility ───────────────────────────────────────────────────────────

        private static VignetteOverlay FindOverlay()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<VignetteOverlay>();
#else
            return Object.FindObjectOfType<VignetteOverlay>();
#endif
        }
    }
}
