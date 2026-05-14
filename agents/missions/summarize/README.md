# Mission: long-form video → structured Korean+English summary

Pipeline:

1. **Resourcer** — `yt-dlp` downloads source video (or copy local).
2. **Resourcer** — `whisper.cpp` transcribes audio → `transcript.json`.
3. **Editor** — `ollama` reads the transcript and produces a structured
   summary with: TL;DR, 3-5 key points, optional Korean translation if
   the source was English (and vice versa).
4. **QA** — verifies summary file exists, contains required sections,
   and stays under a length ceiling.

Run:

```bash
./agents/missions/summarize/run.sh <url_or_local_path>
```

Outputs land in `$RECORDS_DIR/missions/$(date +%F)/summarize-<timestamp>/`.

## Acceptance criteria

- [ ] `outputs/summary.md` exists
- [ ] contains TL;DR section
- [ ] contains "Key points" section with ≥3 bullets
- [ ] when source is non-English, contains the original language section
  AND an English mirror (or vice versa)
- [ ] file size < 50 KB (text only)
