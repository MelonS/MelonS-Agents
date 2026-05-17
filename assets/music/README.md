# Royalty-free music pool

Tracks here are layered under faceless-short narration as background music.
The pipeline reads `FACELESS_BGM_FILE` env var; set it to one of the file
paths below (relative to repo root) to enable BGM on a given render.

## License requirement

**Every track in this directory must be commercial-use OK with no
attribution required.** That narrows the acceptable sources to:

- **Pixabay License** (most Pixabay Music tracks) — no attribution, commercial OK
- **CC0** (Public Domain dedication)

Tracks under CC-BY, CC-BY-SA, or any non-commercial license must NOT be
committed here. If a track requires attribution, the entire faceless-short
output inherits that requirement, which conflicts with the
single-attribution-line constraint of the render (Pexels source is the
only attribution slot).

## Tracks

The license matrix below is filled in as tracks are added. If you see a
row marked `???`, the license has not been verified yet — do not use
that track in production rendering until the row is completed.

| File | Source | License | Mood / use |
|---|---|---|---|
| _none yet_ | — | — | — |

## Re-verification

When adding a track from a new source, double-check:

1. Source page explicitly states the license.
2. The license name appears in the "license requirement" list above.
3. The download URL is the official source CDN, not a third-party mirror.

When in doubt, do not add the track.

## Usage from the pipeline

```bash
# Specific track
FACELESS_BGM_FILE=assets/music/<filename>.mp3 \
  ./agents/missions/faceless-short/run.sh <id> "<topic>"

# Override volume (default 0.15 ≈ -16 dB; range 0.0-1.0)
FACELESS_BGM_FILE=assets/music/<filename>.mp3 \
FACELESS_BGM_VOLUME=0.10 \
  ./agents/missions/faceless-short/run.sh <id> "<topic>"

# No BGM (default)
./agents/missions/faceless-short/run.sh <id> "<topic>"
```
