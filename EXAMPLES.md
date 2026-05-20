# Examples

Concrete recipes for the two skills shipped in this repository.
[`README.md`](README.md) is the overview; this file is the
copy-paste cookbook.

---

## Setup once

```bash
git clone https://github.com/MelonS/MelonS-Agents.git
cd MelonS-Agents
./scripts/bootstrap.sh           # verifies tools, fetches models
```

Optional: `scripts/install-claude-permissions.sh` if you want the
v0.3.0 batch-permission UX in Claude Code (no per-tool prompts).

---

## `music-video` — recipes

The mission: music file in → 60-second 9:16 vertical short out.

### Zero-account demo (~2 min from clone to playable mp4)

No Pexels signup, no Suno round-trip, no `.env` edits.  Bundled
CC-BY Blender clips + Kevin MacLeod tracks.

```bash
MUSIC_VIDEO_DEMO_MODE=1 ./agents/missions/music-video/run.sh demo
```

Output: `records/missions/<YYYY-MM-DD>/music-video-demo-<HHMMSS>/outputs/short.mp4`

### Full Pexels + your own music

```bash
# 1) Put a music file under assets/music/
cp ~/Downloads/my-track.mp3 assets/music/

# 2) Add a CC-BY-attributable entry to assets/music/SOURCES.md
$EDITOR assets/music/SOURCES.md

# 3) Set PEXELS_API_KEY in .env
echo 'PEXELS_API_KEY=your-key-here' >> .env

# 4) Run with mood keywords
./agents/missions/music-video/run.sh my-mission \
    assets/music/my-track.mp3 \
    "rainy street, jazz cafe, vinyl, wet pavement"
```

### Custom B-roll cache (v0.3.0+)

Skip Pexels entirely by pointing at a pre-staged directory of
9:16 vertical clips:

```bash
MUSIC_VIDEO_BROLL_DIR=$HOME/my-broll \
    ./agents/missions/music-video/run.sh mission-id \
    assets/music/track.mp3 "any keywords"
```

### AI-anime generated B-roll (v0.3.0+)

Procedurally generate stylized anime clips locally:

```bash
./scripts/fetch-ai-anime-broll.sh                    # ~10-20 min, GPU-bound
MUSIC_VIDEO_BROLL_DIR=/tmp/anime-gen/clips \
    ./agents/missions/music-video/run.sh anime-test \
    assets/music/track.mp3 "neon, rain, city"
```

### Post-shader pass

After a render, apply the v6 vintage post-shaders:

```bash
./scripts/music-video-shaders.sh combo \
    records/missions/<date>/<mission>/outputs/short.mp4 \
    outputs/publish/short-combo.mp4
```

Available shaders: `pond` · `breathing` · `halation` · `combo`
(phrase-aware pond + halation envelope).

---

## `job-hunt` — recipes

The skill: filters (직군 + 지역 + 키워드) in → markdown digest of
Korean job postings out.

### v2 short-keyword UX (recommended)

Just pass a `--seed` — the skill expands it to the full role family
via `config/role-synonyms.yaml`:

```bash
skills/job-hunt/scripts/run.sh --seed "Problem Solver"
skills/job-hunt/scripts/run.sh --seed "Forward Deployed"
skills/job-hunt/scripts/run.sh --seed "Applied AI Engineer"
skills/job-hunt/scripts/run.sh --seed "Generalist"
# All four above route to the same problem-solver family and expand
# to ~24 equivalent titles used across companies (FDE, FDE,
# Applied AI Engineer, Solutions Engineer, Founding Engineer,
# Growth PM, AI Product Manager, Generalist, 문제 해결사, …).
```

Unknown seed → exit 2 with an actionable error pointing at
`config/role-synonyms.yaml`.  Add a family or a synonym entry to
extend coverage.

### Mock-fallback test (no network, no API keys)

```bash
skills/job-hunt/scripts/run.sh --seed "Problem Solver" --dry-run
```

Output: `/tmp/.../jobs/<YYYY-MM-DD>/digest.md`

Expected: 3 postings (the Problem Solver family entries in the
`_mock` fixture).  Drop the `--seed` to get the default 8 postings
across all categories.

### Advanced — hand-edited filter (no seed)

```bash
cp skills/job-hunt/config/filters.example.yaml \
   skills/job-hunt/config/filters.yaml
$EDITOR skills/job-hunt/config/filters.yaml      # categories, regions, keywords
skills/job-hunt/scripts/run.sh                   # no --seed
```

`filters.yaml` is gitignored by default (operator-specific).

### Production run (still mock-fallback by default)

```bash
skills/job-hunt/scripts/run.sh
```

Output: `./records/jobs/<YYYY-MM-DD>/digest.md` (path comes
from `output.records_root` in filters.yaml).

### Single source override

```bash
skills/job-hunt/scripts/run.sh --sources=kr-wanted
```

### List available source plugins + their mode

```bash
skills/job-hunt/scripts/run.sh --list-sources
```

Sample output:

```
PLUGIN                 MODE         LIVE-FLAG VAR
---------------------- ------------ ---------------------------
_mock                  mock         (none — always mock)
kr-jobkorea            mock         JH_JOBKOREA_LIVE
kr-programmers         mock         JH_PROGRAMMERS_LIVE
kr-saramin             mock         JH_SARAMIN_LIVE
kr-wanted              mock         JH_WANTED_LIVE
```

### Flip a plugin to live HTTP (operator-validated)

```bash
# kr-wanted (requires partner API key)
WANTED_API_KEY=<token> JH_WANTED_LIVE=1 \
    skills/job-hunt/scripts/run.sh --sources=kr-wanted

# kr-programmers (no auth)
JH_PROGRAMMERS_LIVE=1 \
    skills/job-hunt/scripts/run.sh --sources=kr-programmers

# kr-saramin (requires SARAMIN_KEY partner key)
SARAMIN_KEY=<token> JH_SARAMIN_LIVE=1 \
    skills/job-hunt/scripts/run.sh --sources=kr-saramin
```

Before flipping any plugin live for the first time, read its
`fetch_postings()` comment block in
`skills/job-hunt/sources/<name>.sh` for the operator-validation
curl step.

### Full test suite

```bash
skills/job-hunt/tests/run-all.sh        # 63 checks total
# smoke: 32 / edge-cases: 26 / schema-validation: 5
```

### v2 intelligent enrichment (scaffold mode by default)

Each utility module reads a posting + operator profile and emits
a tailored output.  All four ship in scaffold mode (no Claude
call) so the prompt + context can be reviewed before activating.
Set the matching `JH_*_LIVE=1` env var to issue the live call.

```bash
# Per-posting fit scoring, integrated into the orchestrator:
skills/job-hunt/scripts/run.sh --seed "Problem Solver" --fit-score
#   scaffold: each posting in digest shows "Fit: _scaffold mode_"
#   live (with JH_FIT_SCORE_LIVE=1 + operator-profile.md):
#     each posting shows "Fit: 73/100 — rationale" + strengths · gaps

# Standalone per-posting cover-letter draft:
echo '<posting-json>' | skills/job-hunt/scripts/cover-letter-draft.sh
#   live: JH_COVER_LETTER_LIVE=1, optional JH_COVER_TONE=formal|neutral|casual
#   output: 200-300 word markdown letter, evidence-first, no stock phrases

# Company brief:
skills/job-hunt/scripts/company-research.sh "레브잇"
skills/job-hunt/scripts/company-research.sh --posting=path/to/posting.json "Hackle"
#   live: JH_COMPANY_RESEARCH_LIVE=1
#   output: One-liner / Product / Team / Recent signals / Eng-culture /
#           Risk factors / Verification-recommended sections

# Interview prep:
echo '<posting-json>' | skills/job-hunt/scripts/interview-prep.sh
#   live: JH_INTERVIEW_PREP_LIVE=1, optional JH_PREP_STAGE=phone-screen|tech|onsite
#   output: Likely questions / Talking points / Gap mitigation /
#           Questions to ask / Day-of checklist

# Pipe a digest's postings through fit-score → enrich entire digest:
jq -c '.postings[]' records/jobs/<date>/index.json | while read posting; do
  echo "$posting" | JH_FIT_SCORE_LIVE=1 skills/job-hunt/scripts/fit-score.sh
done > scored.jsonl
```

Setup for v2 utilities (one-time):

```bash
cp skills/job-hunt/config/operator-profile.example.md \
   skills/job-hunt/config/operator-profile.md
$EDITOR skills/job-hunt/config/operator-profile.md
# operator-profile.md is gitignored (per-machine, personal context)
```

---

## Multi-skill workflows

### Daily routine — both skills run on launchd

```bash
# job-hunt at 09:00 KST
launchctl bootstrap gui/$(id -u) scripts/com.melons.agents.job-hunt.plist

# music-video daily uploader (already wired)
launchctl bootstrap gui/$(id -u) scripts/com.melons.agents.music-video.plist
```

(Plist templates rendered per-machine by
`scripts/install-claude-local.sh`.  See
[`docs/skills/job-hunt.md`](docs/skills/job-hunt.md)
"Scheduling recurring runs" section.)

### Claude Code slash-command invocation

Once `.claude/skills` symlink is installed, both skills register
automatically:

```
/music-video assets/music/track.mp3 "jazz, vinyl, lounge"
/job-hunt
```

---

## Plugin installation (alternative to git clone)

Once registered on a Claude Code plugin marketplace, the entire
repo's skill set is one-command installable:

```
/plugin install melons-agents-skills
```

Marketplace metadata lives at
[`.claude-plugin/marketplace.json`](.claude-plugin/marketplace.json).

---

## Verification

Every recipe above is exercised by one of:

- [`scripts/test-demo-mode.sh`](scripts/test-demo-mode.sh) — fresh
  clone + bootstrap + music-video demo render; appends PASS/FAIL
  to `docs/onboarding/demo-mode-log.txt`.
- [`skills/job-hunt/tests/run-all.sh`](skills/job-hunt/tests/run-all.sh)
  — 57 structural + functional checks on job-hunt.
- [`skills/music-video/tests/smoke.sh`](skills/music-video/tests/smoke.sh)
  — 11 structural smoke checks on music-video.

Run all three before merging substantive changes.
