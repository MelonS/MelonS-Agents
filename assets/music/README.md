# Music pool — LOCAL ONLY, never committed

Music tracks dropped here are read by the faceless-short pipeline as
background music under the narration.  **No audio file in this
directory is committed to the repo.**  `.gitignore` blocks every audio
file under `assets/music/` regardless of format.

## Why local-only

The repo is public + promoted.  "Free to use in your video" (e.g.
YouTube Audio Library) and "free to redistribute the file" are
different licenses.  Most free-music sources grant the first but not
the second.  Rather than verify redistribution rights per track,
we keep the audio off the repo entirely.

This means:

- Each operator drops their own tracks into their **local** clone.
- Tracks do not transfer when someone forks or clones the repo.
- The pipeline configuration (`FACELESS_BGM_FILE` env var) is the
  only thing that crosses repo / operator boundaries.

## Where to get tracks

In order of recommendation for a local operator:

1. **YouTube Audio Library** — `studio.youtube.com → Audio Library`.
   Filter by "Attribution not required."  Free, commercial use OK
   inside YouTube videos.  Highest-quality free-music selection.
2. **Pixabay Music** — `pixabay.com/music`.  Free, Pixabay License,
   no attribution required for use.
3. **Suno (paid)** — `suno.com`, Pro $8/mo.  AI-generated, commercial
   use OK on paid tiers.  Useful when curated free libraries don't
   have what you want.
4. **Operator's own purchased / created music** — anything you have
   personal rights to use.

## Usage from the pipeline

```bash
# Specific track (relative to repo root)
FACELESS_BGM_FILE=assets/music/<filename>.mp3 \
  ./agents/missions/faceless-short/run.sh <id> "<topic>"

# Override volume (default 0.15 ≈ -16 dB; range 0.0–1.0)
FACELESS_BGM_FILE=assets/music/<filename>.mp3 \
FACELESS_BGM_VOLUME=0.10 \
  ./agents/missions/faceless-short/run.sh <id> "<topic>"

# No BGM (default — current behavior with no BGM file set)
./agents/missions/faceless-short/run.sh <id> "<topic>"
```

The pipeline accepts mp3, wav, ogg, flac, m4a — whatever ffmpeg can
read.  If the track is shorter than the narration, it loops via
`ffmpeg -stream_loop -1`.  If longer, it's truncated to narration
length via `amix duration=first`.
