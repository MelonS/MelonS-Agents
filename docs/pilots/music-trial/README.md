# Music trial — extending the v6 pipeline to a new niche

**Date**: 2026-05-17 09:00–10:25 KST.

**Why**: after the operator watched the Hittites + Hydrogen v6 pilots, the read was honest — "도저히 판단이 안됨, 둘 다 다큐멘터리 톤이라 비교 안 됨, 형님 도메인 (음악+생성형AI) 쪽으로 가보자."  The pair (history × Bible vs deep science) was too tonally similar to test niche fit — and the operator's domain expertise (audio + generative vocal models) suggested music as a parallel pilot category they could fact-check directly.

**What ran**: four music topics, two languages each, through the existing v6 pipeline (Sonnet script → Kokoro / Yuna TTS → whisper.cpp + caption-correction + single-line split → ollama per-window keywords → Pexels B-roll → ffmpeg 9:16 stitch).  No code change — only `FACELESS_SCRIPT_OVERRIDE` injection.

## The four topics

| Topic | Hook (first 1–2 seconds) | Why it might work |
|---|---|---|
| **AI Music** | "An 8-word text prompt now produces a chart-quality, 3-minute vocal track in under 90 seconds." | Operator's professional domain; viral relevance; recent Suno/Udio lawsuit narrative |
| **Earworms** | "98 percent of people get a song stuck involuntarily at least once a week." | Universal experience anchor; well-documented neuroscience; concrete percentages |
| **Hatsune Miku** | "She fills stadiums. She has no vocal cords." | Strong specific contradiction; 16-year cultural arc; relevance to AI vocal models |
| **AutoTune** | "AutoTune was invented to find oil." | Counter-intuitive bare-statement hook; clean cause-and-effect arc; 1996 → Cher 1998 → T-Pain timeline |

## Renders

| Topic | EN mission | EN duration | KO mission | KO duration |
|---|---|---|---|---|
| AI Music | `faceless-aimusic-100014` | 68.6 s | `faceless-aimusic-ko-100743` | 48.4 s |
| Earworms | `faceless-earworms-101030` | (see metrics) | `faceless-earworms-ko-101202` | 45.1 s |
| Hatsune Miku | `faceless-miku-101305` | (see metrics) | `faceless-miku-ko-101512` | 48.2 s |
| AutoTune | `faceless-autotune-101615` | (see metrics) | `faceless-autotune-ko-101807` | 46.2 s |

mp4 files live in `records/missions/2026-05-17/...` (gitignored).  Caption-verify frames + per-window keyword JSONs are in [`screens/`](screens/).

## Scorecard (added to [`scorecard.md`](../scorecard.md))

| Pilot | Hook | Visual sync | Readability | Factual | Polish | **Total** |
|---|---|---|---|---|---|---|
| AutoTune EN | 10 | 8 | 9 | 10 | 8 | **45 / 50** |
| AutoTune KO | 10 | 8 | 9 | 10 | 7 | **44 / 50** |
| Earworms EN | 9 | 8 | 9 | 9 | 8 | **43 / 50** |
| Earworms KO | 9 | 8 | 9 | 9 | 7 | **42 / 50** |
| AI Music EN | 9 | 7 | 9 | 9 | 8 | **42 / 50** |
| AI Music KO | 9 | 7 | 9 | 9 | 7 | **41 / 50** |
| Miku EN | 9 | 6 | 9 | 9 | 8 | **41 / 50** |
| Miku KO | 9 | 6 | 9 | 9 | 7 | **40 / 50** |

AutoTune EN at 45/50 is the highest-scoring pilot in the entire scorecard (above Hittites EN v6's 44/50, the previous top).  The "AutoTune was invented to find oil" hook + the 1996→1998 narrative arc + a well-documented timeline all line up.

Music-niche average **41.75 / 50**, comparable to v6 average on history/science topics (~43.5).  The Sonnet pipeline produces consistent quality across topic categories — quality is bottlenecked on script generation, not domain.

## What this tells us about niche fit

This trial doesn't replace the operator's pick — it adds data to inform it.  Observations:

- **AutoTune** is the strongest individual pilot in the entire backlog so far.  The hook structure (1996 oil-prospecting algorithm → 1998 Cher accident → 10 billion dollars of pop music) is the kind of cause-and-effect story that earns scroll-stop in the first 1.5 seconds.
- **Earworms** has universal-experience anchor — every viewer has had a song stuck.  Lower production ceiling (B-roll for "song stuck in head" is abstract → keyword extraction lands on `headphones` / `brain activity` rather than specific imagery).
- **AI Music** is the operator's domain — they can fact-check it.  Strong recency (Suno launched March 2024, lawsuit June 2024) but trickier on B-roll: Pexels has no Suno logo, no specific lawsuit imagery; falls back to generic recording-studio shots.
- **Hatsune Miku** has the widest variance.  Hook ("She fills stadiums. She has no vocal cords.") is exceptional, but B-roll specificity is the lowest — Pexels has no licensed Miku imagery, so the visuals are dark-lit silhouettes + holographic-projection abstracts.  16-year cultural arc is the unique storyline but visually under-served.

If the operator's niche pick lands on **music**, the strongest entry-point format is the AutoTune-style **"surprising origin story" structure** — a specific invention, a counter-intuitive twist, a concrete cause-and-effect chain.  That format generalizes: synthesizer history (Moog), 808 drum machine, MIDI invention, Auto-Bach 4-chord-trick analysis.

## Pipeline observations

- **Sonnet script quality is topic-independent**.  Average score across 8 music renders matches the v6 score on history/science topics.  The cost-model.md routing lesson (Tier-1 for creative stages) generalizes.
- **B-roll keyword specificity correlates with topic specificity**.  AutoTune (specific people, dates, songs) → cleaner keywords.  Earworms (abstract psychological phenomenon) → broader keywords.  Pexels coverage is the ceiling on visual sync.
- **First-run failure mode**: 7 of 8 music renders did not run on the original background batch — only `aimusic EN` completed.  Re-run with simpler logic recovered the rest.  Suspect: an interaction between `set -uo pipefail` and the run.sh's internal pipe handling killed the parent bash after the first child finished.  Worked around by running in foreground sequentially.  Architectural fix-it candidate.

## Files

- [`*-script.txt`](.) — 8 Sonnet-generated scripts (KO scripts ≈ 770 chars / 60s narration target)
- [`screens/*-caption-verify.jpg`](screens/) — frame capture per render, single-line caption visible, Pexels License attribution top-left
- [`screens/*-keywords.json`](screens/) — per-window B-roll keyword choices, evidence each window picked a distinct context-appropriate term
- [`../scorecard.json`](../scorecard.json) — machine-readable source data for the chart at [`../../metrics/scorecard.png`](../../metrics/scorecard.png)
