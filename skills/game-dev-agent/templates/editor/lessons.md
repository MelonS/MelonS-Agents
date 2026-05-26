# Editor scaffold — lessons learned, baked into the templates above

Each lesson here corresponds to a real bug that surfaced during a
prototype build.  The templates above encode the fix; this file
explains *why*.

## 1. Sprite import race (PawnSim Day 7, 2026-05-26)

**Symptom**: invisible world.  Camera renders, UI renders, but no
sprites visible despite SpriteRenderer components being present.

**Root cause**: `SceneSetup.GenerateGame()` called
`AssetDatabase.LoadAssetAtPath<Sprite>(...)` on PNG files before the
TextureImporter had run.  Without `.meta` files, the loader returns
null, and that null sprite ref gets baked into the saved scene.
Subsequent builds preserve the null.

**Fix in template**: `ForceImportAllAssets()` runs BEFORE any
load-asset call, with `AssetDatabase.ImportAsset(path, ForceUpdate)` +
`TextureImporter.SaveAndReimport()`.  Sprite paths are listed at the
top of `SceneSetup.cs.tmpl` for explicit enumeration.

## 2. Skybox leak in 2D scenes (Suika Day 1, 2026-05-26)

**Symptom**: 2D scene shows a gradient sky behind the gameplay.

**Root cause**: `Camera.clearFlags` defaults to `Skybox`.  Setting
`backgroundColor` alone doesn't replace the skybox.

**Fix in template**: `cam.clearFlags = CameraClearFlags.SolidColor`
explicit in `GenerateGame()`.

## 3. 5-second auto-quit on double-click (PawnSim, 2026-05-27)

**Symptom**: operator double-clicks the .exe, plays for ~4s, .exe
exits.  Felt like a crash.

**Root cause**: `AutoScreenshotter.delaySeconds` defaulted to 2.5 and
`Start()` unconditionally called `StartCoroutine(CaptureAndQuit())`,
so without CLI args the harness would still fire.

**Fix in template**: `AutoScreenshotter.Start()` only schedules the
capture coroutine if BOTH `-screenshot` AND `-delay` CLI args were
passed.  Default = no-op, game runs forever.

## 4. Per-frame audio buzz (PawnSim, 2026-05-27)

**Symptom**: "이상한 사운드" while pawns chop trees.

**Root cause**: `TreeEntity.TakeChopDamage()` called `PlayChop()`
every frame (60x/sec for 4 seconds = 240 overlapping plays = buzz).

**Fix in template**: audio-callsite throttling pattern documented
in `templates/cs/audio-throttled-caller.cs.tmpl` (Phase 1.3 will
fold this into a reusable game-system primitive).

## 5. Component GetInstanceID() tiebreaker bug (Suika Day 2, 2026-05-26)

**Symptom**: `OnCollisionStay2D` merge logic never fired on
same-tier fruit collisions.

**Root cause**: comparing `GetInstanceID()` (FruitMerger component
ID, always > any GO ID because Components are created after GameObjects)
against `collision.gameObject.GetInstanceID()` (GO ID, lower) → both
sides returned, no side ever processed the merge.

**Fix in template**: physics-merger pattern documented in
`templates/cs/physics-merger.cs.tmpl` (Phase 1.3) — must compare
`gameObject.GetInstanceID()` on both sides.

## 6. OnCollisionEnter2D miss after spawn-grace (Suika Day 2)

**Symptom**: pre-spawned entities resting against each other never
trigger merge.

**Root cause**: Enter fires once on first contact (still in grace
period → guard returns), then Stay would re-fire but only
`OnCollisionEnter2D` was hooked.

**Fix in template**: physics-merger template uses BOTH
`OnCollisionEnter2D` AND `OnCollisionStay2D` for the same TryMerge.

## 7. Singleton subscription race (Suika Day 2)

**Symptom**: UI text never updates from Singleton events.

**Root cause**: `MyUI.OnEnable()` subscribes to
`Singleton.Instance.OnChanged`, but Unity Awake order doesn't
guarantee Singleton.Awake ran first → `Instance` was null →
subscription silently no-op'd.

**Fix in template**: singleton-subscriber template (Phase 1.3)
polls via Update instead of event subscription, with a
`lastShown != current` change guard.
