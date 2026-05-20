# Shader-Song Mismatch Diagnosis — 5 ToddStudio Shorts (2026-05-20 batch)

**Trigger.** Operator report 2026-05-21 ~00:30 KST:
> "어떤 곡은 화면이 갑자기 띠용하는 쉐이더였나 머였나 그런게 나오면 이상해보임. 곡과 맞아야 함."

Diagnosis applies the rule table from
[`2026-05-21-music-shorts-formats-landscape.md`](2026-05-21-music-shorts-formats-landscape.md)
to the 5 shorts uploaded to ToddStudio on 2026-05-20.

---

## Current pipeline default treatment (applied to ALL 5 shorts)

From `agents/missions/music-video/run.sh` + `scripts/music-video-shaders.sh`:

- **Film grain** intensity 8 (always-on, baked into base)
- **Vignette** angle PI/5 (always-on)
- **Zoom-pulse** on every detected drum onset (`aubioonset`) — 0.08 amp,
  0.18s Gaussian bell width (always-on when onsets present)
- **Phrase-aligned cuts** every 12 beats (always-on)
- **Optional post-shader** (pond / breathing / halation / combo) — operator
  selects per-render, defaults to NONE if not specified

**Critical finding:** the same treatment is applied regardless of song genre /
tempo / texture.  The "띠용" the operator reports is most plausibly the
**zoom-pulse on drum onsets** — a 0.6s scale bell that fires at every
detected drum hit.  For songs the rule table says require *no glitch / no
sharp transitions* (ambient, lo-fi, jazz), these pulses read as intrusive
"바운스" / "띠용".

---

## Per-short verdict

### 1. Rain (lo-fi) — `2026-05-20/2100-rain-lofi.mp4`

| Axis | Current | Rule (research §Shader matching) | Verdict |
|---|---|---|---|
| Genre | Lo-fi hip-hop / chillhop | — | — |
| Cut density | Every 12 beats | 8-beat soft cuts | ≈ OK (close enough) |
| Filter stack | Grain + vignette + zoom-pulse | Warm grain + halation + slight VHS | ⚠️ Missing halation, missing VHS, **zoom-pulse FORBIDDEN** |
| Forbidden? | Zoom-pulse ON | Glitch FORBIDDEN | **❌ MISMATCH — zoom-pulse is glitch-equivalent** |

**Primary cause of "띠용" likely: this one.** Lo-fi production aesthetic is
structurally anti-glitch (Lofi Girl reference; consistent industry practice).
The drum-onset zoom-pulse violates the genre's visual contract.

**Fix:** zoom-pulse OFF for lo-fi.  Add halation (warm, low intensity).

### 2. Linen (minimal ambient) — `2026-05-21/0900-linen-minimal.mp4`

| Axis | Current | Rule | Verdict |
|---|---|---|---|
| Genre | Minimal ambient | — | — |
| Cut density | Every 12 beats (cuts present) | **None (slow zoom)** | ❌ **MISMATCH — cuts FORBIDDEN for ambient** |
| Filter stack | Grain + vignette + zoom-pulse | Grain + halation + vignette (cool / desat) | ⚠️ Missing halation, **zoom-pulse FORBIDDEN** |
| Forbidden? | Hard cuts ON, zoom-pulse ON | Hard cuts FORBIDDEN, glitch FORBIDDEN | ❌ Two violations |

**Worst mismatch of the 5.**  Ambient music expects perpetual stillness with
slow color/zoom drifts.  Our pipeline gives it 5+ hard cuts and drum-onset
zoom-pulses.  This is the same family of "곡과 안 맞는 띠용" the operator
flagged.

**Fix:** use `stillzoom` mode (single image / single long shot + Ken-Burns
zoom).  No cuts.  No zoom-pulse.  Add halation + cool LUT.

### 3. Arcade (synthwave) — `2026-05-21/2100-arcade-synthwave.mp4`

| Axis | Current | Rule | Verdict |
|---|---|---|---|
| Genre | Synthwave / retrowave | — | — |
| Cut density | Every 12 beats | 2-beat cuts | ⚠️ Too slow (should be much denser) |
| Filter stack | Grain + vignette + zoom-pulse | Scanlines + chromatic aberration + neon edge-glow | ❌ **All three current effects FORBIDDEN for synthwave** |
| Forbidden? | Grain ON, halation OFF (fine), soft-focus from grain | Grain / halation / soft-focus FORBIDDEN | ❌ **MISMATCH on grain** |

Synthwave wants sharp, neon, scanline aesthetic.  Our default soft grain +
vignette gives it a lo-fi look, which is the opposite genre signal.  The
viewer who scrolls past expecting synthwave gets a lo-fi feel instead.

**Fix:** new shader preset `synthwave` — scanlines + chromatic aberration +
neon edge-glow + hot-pink/cyan LUT.  Cut density doubled or tripled (every
4 or 6 beats).  Zoom-pulse OK at 0.5× current amp (synthwave tolerates
glitch).

### 4. Coastline (tropical house) — `2026-05-22/0900-coastline-summer.mp4`

| Axis | Current | Rule | Verdict |
|---|---|---|---|
| Genre | Tropical house | — | — |
| Cut density | Every 12 beats | 1–2 beat cuts | ⚠️ Should be denser |
| Filter stack | Grain + vignette + zoom-pulse | Saturation pulse + edge glow | ⚠️ Wrong family |
| Forbidden? | Grain ON, soft focus from grain | Heavy grain / soft focus FORBIDDEN | ⚠️ Soft-grain mismatch |

House music is hi-fi clean; our lo-fi-leaning default fights the genre.

**Fix:** reduce grain to 2 (was 8), drop vignette to PI/3 (softer), keep
zoom-pulse (house tolerates it).  Optionally add `saturation_pulse` shader
keyed on the kick.

### 5. Noir (jazz) — `2026-05-22/2100-noir-detective.mp4`

| Axis | Current | Rule | Verdict |
|---|---|---|---|
| Genre | Jazz / noir | — | — |
| Cut density | Every 12 beats | 4–8 beat soft cuts | ≈ OK |
| Filter stack | Grain + vignette + zoom-pulse | Grain + halation + low-key vignette | ⚠️ Missing halation, **zoom-pulse questionable** |
| Forbidden? | Zoom-pulse ON | Glitch FORBIDDEN; neon / scanlines FORBIDDEN | ⚠️ Borderline — jazz tolerates *some* visual energy but not glitch |

**Best-matched of the 5** for current defaults, but still missing the
hallmark warm halation that codes "jazz noir" specifically, and the
zoom-pulse is borderline.

**Fix:** add halation (warm, low-intensity 0.20 opacity), keep grain,
deepen vignette (low-key cinematography), kill zoom-pulse.

---

## Summary scorecard

| # | Track | Genre | Current verdict | Severity |
|---|---|---|---|---|
| 1 | Rain | Lo-fi | Zoom-pulse forbidden by genre | **HIGH** — primary "띠용" cause |
| 2 | Linen | Ambient | Cuts + zoom-pulse both forbidden | **HIGH** — worst mismatch overall |
| 3 | Arcade | Synthwave | Wrong filter family (grain instead of scanlines) | **MEDIUM** — feels lo-fi instead of synthwave |
| 4 | Coastline | Tropical house | Soft-grain mismatch | **LOW-MEDIUM** |
| 5 | Noir | Jazz | Missing halation, borderline zoom-pulse | **LOW** |

**Root cause:** pipeline applies one default treatment regardless of genre.
The zoom-pulse + grain default works for *some* genres (jazz-leaning, mid-
tempo lo-fi without strict purist constraints) and breaks for others (lo-fi
purist, ambient, synthwave).

**Fix architecture (proposed):** introduce a `genre` parameter to the
pipeline.  Genre maps to a *preset bundle* that overrides cut density,
filter stack, LUT direction, and shader selection.  Defaults preserved for
back-compat; new behavior gated by `MUSIC_VIDEO_GENRE=<name>` env var.

---

## Next steps (implementation plan)

1. `skills/music-video/data/genre-presets.yaml` — declarative preset table
   for 12+ genre families (see research §Synthesized rule table).
2. `scripts/music-video-shaders.sh` — extend with new effects:
   `scanline`, `neon_edge`, `vhs`, `saturation_pulse`, `chromatic_split`,
   `kaleidoscope`, `datamosh`, `stillzoom`.  Additive — existing
   `pond`/`breathing`/`halation`/`combo` unchanged.
3. `agents/missions/music-video/run.sh` — gate zoom-pulse + cut density +
   grain intensity on `MUSIC_VIDEO_GENRE`.  No genre = unchanged
   (backwards compatible).
4. `scripts/genre-detect.sh` — optional helper: read music file metadata or
   accept hand-tagged genre, output preset selection.
5. `skills/music-video/SKILL.md` — document genre flag, preset list,
   matching principles excerpt.

**Operator decision needed (logged for morning brief):**
- Retroactively fix uploaded 5 shorts (regenerate + re-upload as v2)?
  Or apply to next batch only?
- First preset to ship: ambient stillzoom (covers worst mismatch — Linen)
  OR synthwave (covers Arcade, highest-visibility genre disparity)?
