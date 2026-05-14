# Mission: long-form video → N short-form highlights

Takes one long video and produces up to N standalone 9:16 shorts.
Useful for daily content pipelines (one weekly episode → 3-5 TikTok /
Reels / YouTube Shorts).

## Pipeline

1. **Resourcer** — `yt-dlp` / copy source.
2. **Resourcer** — `whisper.cpp` transcribes audio.
3. **Editor** — `ollama` picks `N` highlight windows from the transcript
   (default 3).
4. **Editor** — for each pick, `ffmpeg_render_short` renders a captioned
   9:16 MP4. Each lands as `outputs/short-NN.mp4` with sibling
   `outputs/short-NN.srt`.
5. **QA** — every short passes the highlight-mission acceptance
   criteria.

## Run

```bash
./agents/missions/shorts-batch/run.sh <url_or_path> [N]
```

Outputs land in `$RECORDS_DIR/missions/$(date +%F)/shorts-batch-<ts>/`.
