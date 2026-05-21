# Review queue

> Lever 3 from
> [`docs/research/2026-05-22-intervention-reduction.md`](../../docs/research/2026-05-22-intervention-reduction.md).

When a mission renders a new artifact (music-video / faceless-short
mp4, AI-generated thumbnail, etc.) the agent enqueues an entry here
**instead of** pinging the operator to "review this".  The operator
drains the queue on their own cadence — daily, weekly, or whenever
they're in the right mood for taste decisions — by running:

```bash
./scripts/review-queue-digest.sh
```

That emits `docs/review-digest.md`, a single-page contact sheet with
one row per pending artifact (path, preview frame, source mood
keywords, render-time metrics).  Operator skims, picks approve /
reject / archive on each row.

Why: per-artifact pings interrupt the operator multiple times per day
and force context switches.  A batched digest collapses ten taste
decisions into one sitting.  Same total decision count, ~10x fewer
intervention events on the
[`docs/metrics/intervention.png`](../../docs/metrics/intervention.png)
chart.

## Layout

```
outputs/review-queue/
├── README.md          (this file, committed)
├── .gitkeep           (committed)
├── pending/           (gitignored — per-machine pending list)
│   └── <YYYY-MM-DD>-<mission_id>.json
└── decided/           (gitignored — operator verdicts)
    └── <YYYY-MM-DD>-<mission_id>.json
```

Each `.json` entry shape:

```json
{
  "mission_id": "music-video-citypop-night1-020317",
  "mission_type": "music-video",
  "artifact_path": "records/missions/.../outputs/short.mp4",
  "preview_jpg":   "records/missions/.../outputs/preview-frame.jpg",
  "queued_at":     "2026-05-22T02:03:17Z",
  "mood_keywords": ["tokyo night", "shibuya rain", "neon street"],
  "music_file":    "vocal-citypop-kr-late-train.mp3",
  "duration_s":    60.2,
  "size_bytes":    71234567,
  "reason":        "auto-enqueued by music-video mission post-render"
}
```

## Scripts

- [`scripts/review-queue-add.sh`](../../scripts/review-queue-add.sh) —
  appends a pending entry.  Called from mission post-render hooks;
  also callable manually.
- [`scripts/review-queue-digest.sh`](../../scripts/review-queue-digest.sh)
  — renders the pending queue to `docs/review-digest.md` as a
  contact sheet markdown.  Idempotent; rerun whenever the operator
  wants a fresh view.
- [`scripts/review-queue-decide.sh`](../../scripts/review-queue-decide.sh)
  — moves a `pending/` entry to `decided/` with operator verdict
  (approve / reject / archive).  Lightweight CLI.

The queue is **local-only** — entries are personal taste artifacts
that don't belong in the public repo.  README.md and .gitkeep are
the only committed files.
