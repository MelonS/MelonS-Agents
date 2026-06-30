# content-short mission

The deterministic spine of the content-shorts pipeline
(리서치 → 제작 ⇄ 법률 → 출시). Drives `faceless-short` per a profile, runs the
legal gate, and builds the release package. The judgment stages (web research,
legal reasoning) are Claude subagents that produce the JSON this consumes.

See `docs/content-shorts-pipeline.md` for the full architecture and the
`research.json` / `legal-verdict.json` data contract.

## Stages

```
run.sh <short_id> --profile=<info|news|idol> [--stage=all|produce|legal|release] \
  [--mission-dir=<existing dir>] [--topic="..."] [--research=<research.json>] \
  [--legal-verdict=<verdict.json>] [--platform=public|internal-demo] [--max-legal-iters=2]
```

The four teams share **one** mission dir. A produce run prints `MISSION_DIR=<path>`;
thread it back with `--mission-dir` to every later stage. `legal` and `release`
are decoupled — they operate on the produced artifacts and **refuse**
(`require_produced`) if no `outputs/short.mp4` exists in the dir.

| stage   | needs `--mission-dir`? | does |
|---------|:---:|------|
| produce | no (mints it) | faceless render with the profile (+ idol subject overlay); writes `outputs/short.mp4`, `SOURCES.txt`, `disclosures.txt`; prints `MISSION_DIR=` |
| legal   | yes | `scripts/legal-gate.sh` — `guard_publish` + disclosure presence, merged (worse-of) with the legal-team verdict → `legal/legal-verdict.json` (exit 0 PASS / 1 REVISE / 2 BLOCK) |
| release | yes | gated on PASS — `gen-upload-metadata.sh` + thumbnail + `PUBLISH-CHECKLIST.md` in `release/` |
| all     | no (mints it) | produce → legal(deterministic) → release(if PASS); headless one-shot |

## Profiles
`config/content-short-profiles.yaml` (info / news / idol).
Subject (idol): `config/subjects/<id>.yaml` (real ones gitignored; `example.yaml` tracked).

## Quick examples
```bash
# evergreen explainer, headless one-shot (produce→legal→release in one dir)
run.sh skyblue --profile=info --topic="Why the sky is blue" --stage=all

# agent-orchestrated, threaded across stages:
run.sh quake0701 --profile=news --research=r.json --stage=produce   # prints MISSION_DIR=<M>
run.sh quake0701 --profile=news --stage=legal  --mission-dir=<M> --legal-verdict=<M>/legal/subagent-verdict.json
run.sh quake0701 --profile=news --stage=release --mission-dir=<M>   # only on PASS

# idol/real-artist, headless one-shot (--subject selects a local subject file)
run.sh ep12 --profile=idol --subject=<id> --topic="<artist> comeback single release" --stage=all
```

## Reused building blocks
- `agents/missions/faceless-short/run.sh` (render core — never edited)
- `agents/lib/copyright.sh`, `agents/lib/attribution.sh` (the legal primitives)
- `scripts/legal-gate.sh`, `scripts/research-screen.sh`, `scripts/subject-overlay.sh`
- `scripts/gen-upload-metadata.sh` (release copy)
