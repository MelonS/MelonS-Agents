# Mission: long-form video → 9:16 highlight short

Pipeline:

1. **Resourcer** — `yt-dlp` downloads source video to `resources/source.mp4`.
2. **Resourcer** — `whisper.cpp` transcribes audio to `resources/transcript.json`.
3. **Editor** — `ollama` (`llama3.2:3b`) reads transcript, picks a 30–60s
   span via [`select-highlight.prompt.md`](select-highlight.prompt.md).
   Output: `resources/selection.json` (`{start, end, reason}`).
4. **Editor** — `ffmpeg` cuts the span, crops to 9:16 with blurred bg,
   burns SRT captions. Output: `outputs/short.mp4`.
5. **QA** — `ffprobe` verifies duration ∈ [30, 60], resolution = 1080x1920,
   audio stream present, captions burned. Output: `qa-report.md`.

Run:

```bash
./agents/missions/highlight/run.sh <source_url_or_local_path>
```

Outputs land in `$RECORDS_DIR/missions/$(date +%F)/highlight-<timestamp>/`.

## Acceptance criteria (default)

- [ ] `outputs/short.mp4` exists
- [ ] duration ∈ [30, 60] seconds
- [ ] resolution = 1080x1920 (9:16)
- [ ] audio stream present
- [ ] captions burned in (visual check — QA records a thumbnail)
- [ ] file size < 50 MB
