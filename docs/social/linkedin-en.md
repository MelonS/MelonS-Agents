# LinkedIn snippets (EN) — daily project-progress posts

Operator posts 1–2 LinkedIn updates per day about this project's
progress.  This file is the ready-to-copy English snippet bank — pick
the freshest one each day, paste, post.

Tone (per `docs/operator-contract.md` writing-tone rule):
- Neutral, technical.  No marketing superlatives.  No personal-credentials
  framing ("X years of experience…").
- Concrete numbers over vague claims.
- No emoji.
- Short paragraphs — LinkedIn mobile readability.

Each snippet structure:
1. One-line hook ("see more" click-bait, but honest)
2. Context (1–2 paragraphs)
3. What was done / specific numbers
4. What was learned / what's next
5. Repo link
6. Hashtags

---

## #1 — Pipeline overview (gateway for new followers)

```
Started building a multi-agent short-form video pipeline a week ago.

Give it a topic prompt and it returns a 60-second 9:16 vertical short.

Pipeline:
- Claude Sonnet writes the script (subscription quota; zero new dollar spend)
- Kokoro TTS or macOS Yuna synthesizes the narration
- whisper.cpp transcribes for timing
- ollama (local 3B model) extracts one visual keyword per narration window (8 windows)
- Pexels Videos API fetches one B-roll clip per window
- ffmpeg crops 9:16, burns captions, adds source attribution

Runtime API tokens: 0. Marginal cost per video: $0.

32 missions run end-to-end so far. English + Korean dual tracks.
Three-layer reactive audit (post-commit hook + 15-min poll + daily baseline).
README, Korean mirror, scorecard, Sonnet trial — all public.

Repo: github.com/MelonS/MelonS-Agents

#MultiAgent #ClaudeCode #AIShorts #FacelessShorts #DevLog
```

---

## #2 — The Tier-1 routing lesson (today's most expensive learning)

```
Today's most expensive lesson — "you should have used Claude from the start."

That's what the operator said after watching four pilot videos.

The design principle was simple: "must run at $0 API cost."
So every stage — script generation, voice synthesis, B-roll selection,
rendering — used only local tools: llama3.2:3b, whisper.cpp, Kokoro TTS,
ffmpeg.

Result: scripts with weak first-5-second hooks viewers swipe away
from. "What if..." openers. Internal contradictions (hydrogen makes
up 10% of your body? Or 60%? Both true on different frames — but
the script mixed them.).

The actual problem was blanket-applying "Tier 2 (local) = default"
to *every* pipeline stage. Repeated mechanical stages (transcribe,
render) belong on local. But one-shot creative stages — the hook
line, the narrative beats — run ~500 tokens per call.  That's
operationally negligible against any monthly subscription quota, and
the quality of those 500 tokens drives the *next 60 seconds* of
viewing.

Routing only the script-generation stage to Claude Sonnet lifted
scores from 32/50 to 44/50 on the same topic.  Hook +6 points,
factual coherence +3.

Takeaway: "cost optimization" rules matter less than *where you
apply them*.  Apply everywhere and it stops being optimization — it
becomes a quality ceiling.

Repo: github.com/MelonS/MelonS-Agents/blob/main/docs/cost-model.md

#MultiAgent #ClaudeCode #AIPipeline #LLM #DevLog
```

---

## #3 — Three-layer reactive auditor

```
"Drift" is the quiet rot of a multi-agent system.

Until today this repo had one daily audit at 03:00 local time.
Code committed at 23:00 lived in an unaudited gap for four hours
before the operator-contract checker would notice.

Today I closed the gap with two new layers.

L1 — git post-commit hook.  Whenever a commit touches drift-risk
paths (agents/, .claude/agents/, config/, CLAUDE.md,
operator-contract.md), the hook fires `audit-run.sh contract` in
the background.  The first verdict lands ~30 seconds after the
commit.  Validated end-to-end: two real commits triggered it
today, both came back CLEAN.

L2 — 15-minute mission-anomaly poll.  A new blocker file or a QA-FAIL
burst (≥2 within 60 minutes) triggers a focused audit.  Otherwise
the poll reads a few files and exits — zero API tokens spent.

L3 (pre-existing) — daily 03:00 full sweep as baseline.

Three layers: immediate (L1), frequent (L2), baseline (L3).
The design pattern is NOT Observer (no long-running observables —
subagents are file-based, not in-process).  It's Reactor + Hook —
files as events, handlers fire on file-system changes.

Repo: github.com/MelonS/MelonS-Agents/blob/main/docs/audit/README.md

#MultiAgent #ClaudeCode #DevOps #AuditAutomation #DevLog
```

---

## #4 — Scorecard self-evaluation (the evolution thumbnails can't show)

```
"Thumbnails alone don't tell me what's improving."

Operator feedback — they could see new video frames in the README
but not the evolution from v4 to v5 to v6.

A single capture doesn't capture progress.  Neither does "looks
good" or "looks better than yesterday".  Those aren't measurements.

So I built a five-axis scorecard.

1. Hook strength — does the first 5 seconds earn the next 5?
2. Visual ↔ caption sync — does the B-roll match the words being spoken?
3. Caption readability — single-line cues, no opaque-box overlap
4. Factual coherence — no '10% vs 63%' contradictions in one breath
5. Production polish — TTS prosody, screen-fill, attribution legibility

Honest disclosure: scores are assigned by Claude (the LLM
self-evaluating), not by a viewer panel.  They are a structured
progress signal until real platform watch-time data replaces them.

Results:
- Hittites EN: v4 26/50 → v5 32/50 → v6 44/50
- Hydrogen EN: v5 28/50 → v6 43/50

"It got better" becomes "Hook +6, Factual coherence +3, here is
which dimension moved" — operator-readable evolution signal.  Most
of the v5 → v6 lift came from the two axes the operator surfaced
as broken.

Chart, JSON source data, and axis definitions are all public in the
repo.

Repo: github.com/MelonS/MelonS-Agents/blob/main/docs/pilots/scorecard.md

#MultiAgent #SelfEvaluation #DevLog #ShortForm
```

---

## #5 — The single-line caption fix (small change, big perceptual lift)

```
"When caption boxes overlap, it just looks broken."

That was the operator's first note after watching four pilot videos.

Short-form captions are nearly the only information channel on
mobile (most playback is muted).  But libass with BorderStyle=3
(opaque box per line) plus two-line captions creates a visual
where the boxes from adjacent cues graze each other.  The brain
reads it as "overlap" even when there technically isn't one.

The fix: a 154-line Python post-processor (split-long-captions.py)
inserted between caption correction and the libass render stage.

- Cues longer than 28 characters split first at natural punctuation
  (commas, em-dashes, periods) — these match speech pauses so the
  cut doesn't read as awkward.
- Anything still long falls back to greedy word-split.
- One-character connectors ("a", "the", "and", Korean particles)
  that would orphan at end-of-line get pulled to the next chunk.
- Sub-second cues merge into the previous sibling.

Result: re-rendered the same scripts + same B-roll, and the
caption-readability score jumped from 4/10 to 9/10.  No script
change, no model swap.  Just the post-processor.

Small fix, large perceptual lift.

Repo: github.com/MelonS/MelonS-Agents/blob/main/scripts/split-long-captions.py

#MultiAgent #DevLog #ShortForm #MobileUX
```

---

## Usage tips

- Post one snippet per day (LinkedIn algorithm prefers spacing over
  bursts).
- The first line is the "see more" trigger — that one line has to
  hold the hook.
- Snippets look long but LinkedIn collapses past ~3 lines on mobile;
  paste the whole thing.
- 3–5 hashtags is the algorithmically friendly count.
- Adding a discussion-prompting question at the end ("Where have
  you hit a similar routing problem?") lifts engagement noticeably.

## Adding new snippets

When a new major milestone lands, append a new `## #N — <title>` block
near the top of this file.  Once the operator uses one, add a
`<!-- used YYYY-MM-DD -->` comment beneath the heading so we don't
recycle it.
