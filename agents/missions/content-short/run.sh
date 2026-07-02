#!/usr/bin/env bash
# content-short — the 리서치 → 제작 ⇄ 법률 → 출시 deterministic spine.
#
# This is the bash half of the content-shorts pipeline (see
# docs/content-shorts-pipeline.md). It runs the reproducible stages:
#   produce  — wrap faceless-short with the profile + (idol) subject overlay
#   legal    — run scripts/legal-gate.sh (license + disclosure + merge verdict)
#   release  — gate on PASS, then build the upload package
# The JUDGMENT stages (web research, legal reasoning) are Claude subagents
# (research-team / legal-team) that produce the JSON this script consumes:
#   --research=<research.json>        (from research-team)
#   --legal-verdict=<verdict.json>    (from legal-team)
#
# Usage:
#   run.sh <short_id> --profile=<info|news|idol> \
#     [--stage=all|produce|legal|release] [--mission-dir=<dir>] [--topic="..."] \
#     [--subject=<id>] [--research=<research.json>] [--legal-verdict=<verdict.json>] \
#     [--platform=public|internal-demo] [--max-legal-iters=2]
#
# Examples:
#   run.sh hydrogen --profile=info --topic="Hydrogen — 75% of the universe"
#   run.sh quake0701 --profile=news --research=resources/research.json --stage=produce
#   run.sh ep12 --profile=idol --subject=<id> --topic="<artist> comeback single" --stage=all
#
# Exit: 0 ok · 1 legal REVISE · 2 legal BLOCK · 64 usage · 65 env · 70 render

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/log.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/attribution.sh" 2>/dev/null || true

# Force Python UTF-8 mode so unicode (Korean disclosures, em-dashes) survives
# stdout/file writes on Windows (cp949 default would raise UnicodeEncodeError).
# No-op on macOS/Linux. Cross-platform env pattern, same as FFMPEG_BIN discovery.
export PYTHONUTF8=1
# env.sh enables `set -e`; this orchestrator does its own per-stage error
# handling (captures rc, exits with a message). Relax -e so an expected
# non-zero (legal REVISE/BLOCK, optional-file cp, empty grep) doesn't abort
# the wrong way. We propagate intent via explicit exit codes.
set +e

PROFILES_YAML="$REPO_ROOT/config/content-short-profiles.yaml"
SUBJECT_DIR="$REPO_ROOT/config/subjects"

# ── args ──────────────────────────────────────────────────────────────────
SHORT_ID="${1:-}"; shift || true
PROFILE=""; STAGE="all"; TOPIC=""; RESEARCH=""; LEGAL_VERDICT=""
PLATFORM="public"; MAX_LEGAL_ITERS=2; MISSION_DIR=""; SUBJECT_OVERRIDE=""
for arg in "$@"; do
  case "$arg" in
    --profile=*)        PROFILE="${arg#*=}" ;;
    --stage=*)          STAGE="${arg#*=}" ;;
    --topic=*)          TOPIC="${arg#*=}" ;;
    --research=*)       RESEARCH="${arg#*=}" ;;
    --legal-verdict=*)  LEGAL_VERDICT="${arg#*=}" ;;
    --platform=*)       PLATFORM="${arg#*=}" ;;
    --max-legal-iters=*) MAX_LEGAL_ITERS="${arg#*=}" ;;
    --mission-dir=*)    MISSION_DIR="${arg#*=}" ;;
    --subject=*)        SUBJECT_OVERRIDE="${arg#*=}" ;;
    *) log_warn "ignoring unknown arg '$arg'" ;;
  esac
done

if [[ -z "$SHORT_ID" || -z "$PROFILE" ]]; then
  log_err "usage: $0 <short_id> --profile=<info|news|idol> [--stage=...] [--topic=...] [--research=...]"
  exit 64
fi
case "$PROFILE" in info|news|idol) ;; *) log_err "unknown profile '$PROFILE' (info|news|idol)"; exit 64 ;; esac

# ── resolve profile fields (flat-YAML reader) ───────────────────────────────
prof_field() {
  # prof_field <id> <key> — scalar field of a profile block
  python3 - "$PROFILES_YAML" "$1" "$2" <<'PY'
import sys, re
path, want, key = sys.argv[1], sys.argv[2], sys.argv[3]
text = open(path, encoding="utf-8").read()
cur=None; found=None; in_list=False
for line in text.splitlines():
    if re.match(r"^profiles:", line): in_list=True; continue
    if not in_list: continue
    m=re.match(r"^  - id:\s*(.+)$", line)
    if m:
        cur=m.group(1).strip()
        continue
    if cur==want:
        m=re.match(rf"^    {re.escape(key)}:\s*(.*)$", line)
        if m:
            v=m.group(1)
            mq=re.match(r'\s*"((?:[^"\\]|\\.)*)"', v)   # fully-quoted value
            if mq: v=mq.group(1)
            else:  v=re.split(r"\s+#", v, 1)[0].strip()  # strip inline comment
            # collapse YAML '>' folded scalars to empty so callers use defaults
            if v in (">", "|"): v=""
            print(v); sys.exit(0)
print("");
PY
}

VOICE="$(prof_field "$PROFILE" voice)";          [[ -z "$VOICE" ]] && VOICE="am_michael"
NUM_BROLL="$(prof_field "$PROFILE" num_broll)";   [[ -z "$NUM_BROLL" ]] && NUM_BROLL=8
SUBJECT="$(prof_field "$PROFILE" subject)"
# --subject overrides the profile default (your real subject files are local;
# the committed default is the generic `example`).
[[ -n "$SUBJECT_OVERRIDE" ]] && SUBJECT="$SUBJECT_OVERRIDE"
RECENCY_DAYS="$(prof_field "$PROFILE" recency_days)"; [[ -z "$RECENCY_DAYS" ]] && RECENCY_DAYS=0

# Subject narrator voice: profile voice=="subject" → read the synthetic narrator
# voice from the subject file (config/subjects/<id>.yaml). This is the NARRATOR,
# never a real member's voice.
SUBJECT_VOICE=""
if [[ "$VOICE" == "subject" || -n "$SUBJECT" ]]; then
  SFILE="$SUBJECT_DIR/${SUBJECT:-$PROFILE}.yaml"
  if [[ -f "$SFILE" ]]; then
    SUBJECT_VOICE="$(awk '$0 ~ /^  voice:/ {
        line=$0; sub(/^[^:]*:[[:space:]]*/,"",line)
        if (line ~ /^"/) { sub(/^"/,"",line); sub(/".*$/,"",line) }
        else { sub(/[[:space:]]*#.*$/,"",line); sub(/[[:space:]]+$/,"",line) }
        print line; exit }' "$SFILE")"
  fi
  [[ -n "$SUBJECT_VOICE" ]] && VOICE="$SUBJECT_VOICE"
  [[ "$VOICE" == "subject" ]] && VOICE="af_heart"   # last-resort default
fi

# ── mission workspace ──────────────────────────────────────────────────────
# The four teams share ONE mission folder. The orchestrator (content-director)
# captures the dir from a produce run (see the `MISSION_DIR=` line printed
# below) and threads it back via --mission-dir to every later stage so legal /
# release / re-render all operate on the SAME produced artifacts. Without
# --mission-dir each invocation mints a fresh dated dir (headless --stage=all).
if [[ -n "$MISSION_DIR" ]]; then
  MDIR="$MISSION_DIR"
  MISSION_ID="$(basename "$MDIR")"
else
  MISSION_ID="content-${PROFILE}-${SHORT_ID}-$(date +%H%M%S)"
  MDIR="$RECORDS_DIR/missions/$(date +%Y-%m-%d)/$MISSION_ID"
fi
mkdir -p "$MDIR/resources" "$MDIR/outputs" "$MDIR/legal" "$MDIR/release"
log_step "mission: $MDIR (profile=$PROFILE voice=$VOICE stage=$STAGE)"
# Machine-greppable so the orchestrator can capture the dir to thread:
echo "MISSION_DIR=$MDIR"

AS_OF_DATE="$(date +%F)"

# Guard for stages that need an already-produced mission (legal/release run on
# existing artifacts; they must NOT silently re-render). Call before those stages.
require_produced() {
  if [[ ! -f "$MDIR/outputs/short.mp4" ]]; then
    log_err "stage='$STAGE' needs an already-produced mission, but $MDIR/outputs/short.mp4 is absent."
    log_err "  Run produce first, then pass --mission-dir=<that dir> to this stage"
    log_err "  (the produce run prints 'MISSION_DIR=<path>'). Or use --stage=all for a headless one-shot."
    exit 65
  fi
}

# ════════════════════════════════════════════════════════════════════════════
# STAGE: produce
# ════════════════════════════════════════════════════════════════════════════
stage_produce() {
  log_step "[제작팀] produce — faceless core + profile=$PROFILE"

  # Build the topic/seed for faceless from research.json or --topic.
  local seed_override="" topic="$TOPIC" terms_file=""
  if [[ -n "$RESEARCH" && -f "$RESEARCH" ]]; then
    cp "$RESEARCH" "$MDIR/resources/research.json"
    local r_topic r_angle r_seed
    r_topic="$(python3 -c 'import json,sys;d=json.load(open(sys.argv[1],encoding="utf-8"));print(d.get("topic",""))' "$RESEARCH")"
    r_angle="$(python3 -c 'import json,sys;d=json.load(open(sys.argv[1],encoding="utf-8"));print(d.get("angle",""))' "$RESEARCH")"
    r_seed="$(python3 -c 'import json,sys;d=json.load(open(sys.argv[1],encoding="utf-8"));print(d.get("script_seed",""))' "$RESEARCH")"
    [[ -z "$topic" ]] && topic="${r_angle:-$r_topic}"
    if [[ -n "$r_seed" ]]; then
      seed_override="$MDIR/resources/script-seed.txt"
      printf '%s\n' "$r_seed" > "$seed_override"
      log_info "using research script_seed as FACELESS_SCRIPT_OVERRIDE"
    fi
    # Curated visual search terms → faceless uses these instead of guessing
    # per-window terms with the local 3B model (which drifts to off-topic stock
    # like "sword fight"/"clock tower" on Korean input). One term per line.
    terms_file="$MDIR/resources/visual-terms.txt"
    python3 -c 'import json, sys
d = json.load(open(sys.argv[1], encoding="utf-8"))
print("\n".join(t for t in d.get("visual_terms", []) if str(t).strip()))' "$RESEARCH" > "$terms_file" 2>/dev/null
    if [[ -s "$terms_file" ]]; then
      log_info "curated visual terms: $(grep -c . "$terms_file") line(s) (override model extraction)"
    else
      terms_file=""
    fi
    # Screen media sources (defense in depth; research-team should have already)
    if [[ -x "$REPO_ROOT/scripts/research-screen.sh" ]]; then
      "$REPO_ROOT/scripts/research-screen.sh" "$MDIR/resources/research.json" --in-place || \
        log_warn "research-screen flagged blocked media sources (producer falls back to Pexels search)"
    fi
    # News-safety screen (deterministic: category tier / rot-words / red
    # risk_flags / recency / sourcing floor, per config/news-category-tiers.yaml).
    # BLOCK aborts before any render money is spent; warnings flow to legal-team.
    if [[ "$PROFILE" == "news" && -x "$REPO_ROOT/scripts/news-screen.sh" ]]; then
      "$REPO_ROOT/scripts/news-screen.sh" "$MDIR/resources/research.json" --profile=news --in-place
      local ns_rc=$?
      if [[ $ns_rc -eq 4 ]]; then
        log_err "news-screen BLOCK — red tier / rot-word / unsourced claim (see research.json .news_screen)"
        exit 2
      fi
      [[ $ns_rc -eq 3 ]] && log_warn "news-screen warnings — legal-team must clear them before release"
    fi
  fi
  if [[ -z "$topic" ]]; then
    log_err "produce: no topic — pass --topic or --research with a topic/angle"; exit 64
  fi

  # Drive faceless-short. It mints its own mission dir; we capture and import.
  local fl_log="$MDIR/resources/faceless.log"
  log_info "delegating render to faceless-short (voice=$VOICE broll=$NUM_BROLL)"
  if [[ ! -x "$REPO_ROOT/agents/missions/faceless-short/run.sh" ]]; then
    log_err "faceless-short/run.sh not found/executable"; exit 70
  fi
  # Env assignments must reach faceless as a REAL environment, not a
  # ${var:+VAR=...} word (a parameter-expansion that produces "VAR=val" is NOT
  # recognized as an assignment prefix — it would become the command name). Use
  # an array + env(1) so the conditional FACELESS_SCRIPT_OVERRIDE is set correctly.
  local -a fl_env=(FACELESS_VOICE="$VOICE" FACELESS_NUM_BROLL="$NUM_BROLL")
  [[ -n "$seed_override" ]] && fl_env+=(FACELESS_SCRIPT_OVERRIDE="$seed_override")
  [[ -n "$terms_file" ]] && fl_env+=(FACELESS_VISUAL_TERMS="$terms_file")
  env "${fl_env[@]}" \
    bash "$REPO_ROOT/agents/missions/faceless-short/run.sh" "$SHORT_ID" "$topic" >"$fl_log" 2>&1
  local fl_rc=$?
  if [[ $fl_rc -ne 0 ]]; then
    log_err "faceless render failed (rc=$fl_rc). Tail:"
    tail -n 25 "$fl_log" >&2
    exit 70
  fi

  # Import faceless outputs into our mission dir. faceless mints
  # <RECORDS_DIR>/missions/<date>/faceless-<SHORT_ID>-<HHMMSS>/; find the newest
  # such dir (robust to spaces / ANSI in the log, unlike grepping the path).
  local fl_mdir=""
  fl_mdir="$(find "$RECORDS_DIR/missions/$(date +%Y-%m-%d)" -maxdepth 1 -type d -name "faceless-${SHORT_ID}-*" 2>/dev/null | sort | tail -1)"
  if [[ -z "$fl_mdir" || ! -f "$fl_mdir/outputs/short.mp4" ]]; then
    # Fallback: parse the render path from the log (handles odd RECORDS_DIR dates).
    local fl_out; fl_out="$(grep -aoE '/[^[:space:]]*short\.mp4' "$fl_log" | tail -1)"
    [[ -n "$fl_out" && -f "$fl_out" ]] && fl_mdir="$(cd "$(dirname "$fl_out")/.." && pwd)"
  fi
  if [[ -z "$fl_mdir" || ! -f "$fl_mdir/outputs/short.mp4" ]]; then
    log_err "could not locate faceless render output"; tail -n 25 "$fl_log" >&2; exit 70
  fi
  cp "$fl_mdir/outputs/short.mp4" "$MDIR/outputs/short.mp4"
  [[ -f "$fl_mdir/resources/script.txt" ]]   && cp "$fl_mdir/resources/script.txt" "$MDIR/resources/script.txt"
  [[ -f "$fl_mdir/resources/captions.srt" ]] && cp "$fl_mdir/resources/captions.srt" "$MDIR/resources/captions.srt"
  [[ -f "$fl_mdir/outputs/caption-verify.jpg" ]] && cp "$fl_mdir/outputs/caption-verify.jpg" "$MDIR/outputs/caption-verify.jpg"
  log_ok "imported faceless render from $fl_mdir"

  # ── Subject overlay (idol) — builds on the faceless output ───────────────
  # Channel branding + fan-content + AI-narration disclaimers burned on-video.
  if [[ "$PROFILE" == "idol" || -n "$SUBJECT" ]]; then
    local subject_id="${SUBJECT:-example}"
    local overlaid="$MDIR/outputs/short-subject.mp4"
    if "$REPO_ROOT/scripts/subject-overlay.sh" "$subject_id" "$MDIR/outputs/short.mp4" "$overlaid"; then
      mv "$overlaid" "$MDIR/outputs/short.mp4"
      log_ok "applied $subject_id subject overlay"
    else
      log_warn "subject overlay failed — keeping un-overlaid render (legal will flag missing disclosure)"
    fi
  fi

  # ── SOURCES.txt for the license gate (default render path = Pexels only) ─
  # faceless B-roll is 100% Pexels (videos.pexels.com → pexels-license). If a
  # future render reuses allowed non-Pexels media, record the most-restrictive
  # license here instead (guard_publish reads the first `license:` line).
  cat > "$MDIR/outputs/SOURCES.txt" <<EOF
mission_id: $MISSION_ID
source: https://www.pexels.com/
attribution: Pexels (various photographers) — pexels.com
license: pexels-license
recorded_at: $(date -u +%Y-%m-%dT%H:%M:%SZ)
EOF

  # ── disclosures.txt — required lines (legal checks presence) ─────────────
  : > "$MDIR/outputs/disclosures.txt"
  python3 - "$PROFILES_YAML" "$PROFILE" "$AS_OF_DATE" >> "$MDIR/outputs/disclosures.txt" <<'PY'
import sys, re
path, want, as_of = sys.argv[1], sys.argv[2], sys.argv[3]
text=open(path,encoding="utf-8").read()
cur=None; sect=None; lines=[]; in_list=False
for line in text.splitlines():
    if re.match(r"^profiles:", line): in_list=True; continue
    if not in_list: continue
    m=re.match(r"^  - id:\s*(.+)$", line)
    if m: cur=m.group(1).strip(); sect=None; continue
    if cur!=want: continue
    if re.match(r"^    disclosures:\s*$", line): sect="d"; continue
    if re.match(r"^    [a-z_]+:", line): sect=None
    m=re.match(r"^      -\s*(.+)$", line)
    if m and sect=="d":
        v=m.group(1).strip()
        mq=re.match(r'"((?:[^"\\]|\\.)*)"', v)
        v=mq.group(1) if mq else re.split(r"\s+#", v, 1)[0].strip()
        lines.append(v)
for l in lines:
    print(l.replace("{AS_OF_DATE}", as_of))
PY
  log_info "disclosures: $(wc -l < "$MDIR/outputs/disclosures.txt") required line(s)"

  # metrics for the dashboard
  local dur size_mb
  dur="$("$FFPROBE_BIN" -v error -show_entries format=duration -of default=nw=1:nk=1 "$MDIR/outputs/short.mp4" 2>/dev/null | awk '{printf "%.1f",$1}')"
  size_mb=$(( $(wc -c < "$MDIR/outputs/short.mp4") / 1024 / 1024 ))
  cat > "$MDIR/metrics.json" <<EOF
{ "profile": "$PROFILE", "short_id": "$SHORT_ID", "output_duration_s": ${dur:-0},
  "size_mb": $size_mb, "voice": "$VOICE", "subject": "${SUBJECT:-}", "stage": "produce" }
EOF
  log_ok "produce complete → $MDIR/outputs/short.mp4 (${dur:-?}s, ${size_mb}MB)"
}

# ════════════════════════════════════════════════════════════════════════════
# STAGE: legal  (deterministic gate; merges legal-team subagent verdict)
# ════════════════════════════════════════════════════════════════════════════
stage_legal() {
  log_step "[법률팀] legal-gate — platform=$PLATFORM"
  local ext_arg=""
  [[ -n "$LEGAL_VERDICT" && -f "$LEGAL_VERDICT" ]] && ext_arg="--external-verdict=$LEGAL_VERDICT"
  "$REPO_ROOT/scripts/legal-gate.sh" "$MDIR" --profile="$PROFILE" --platform="$PLATFORM" $ext_arg
  return $?
}

# ════════════════════════════════════════════════════════════════════════════
# STAGE: release  (only on PASS)
# ════════════════════════════════════════════════════════════════════════════
stage_release() {
  log_step "[출시팀] release — build upload package"
  local vfile="$MDIR/legal/legal-verdict.json" verdict="MISSING"
  [[ -f "$vfile" ]] && verdict="$(python3 -c 'import json,sys;print(json.load(open(sys.argv[1],encoding="utf-8")).get("verdict","MISSING"))' "$vfile" 2>/dev/null || echo MISSING)"
  if [[ "$verdict" != "PASS" ]]; then
    log_err "release refused — legal verdict is '$verdict' (need PASS). Run legal stage / apply fixes first."
    return 2
  fi

  # Upload metadata (reuse faceless metadata generator — expects resources/script.txt)
  if [[ -x "$REPO_ROOT/scripts/gen-upload-metadata.sh" && -f "$MDIR/resources/script.txt" ]]; then
    "$REPO_ROOT/scripts/gen-upload-metadata.sh" "$MDIR" 2>/dev/null && \
      log_info "upload metadata generated" || log_warn "gen-upload-metadata had issues (non-fatal)"
  fi

  # Thumbnail (first strong frame ~1.5s in)
  "$FFPROBE_BIN" -v error "$MDIR/outputs/short.mp4" >/dev/null 2>&1 && \
    "$FFMPEG_BIN" -y -loglevel error -ss 1.5 -i "$MDIR/outputs/short.mp4" -frames:v 1 "$MDIR/release/thumbnail.jpg" 2>/dev/null || true

  # Assemble the release package
  cp "$MDIR/outputs/short.mp4"       "$MDIR/release/short.mp4" 2>/dev/null || true
  cp "$MDIR/outputs/SOURCES.txt"     "$MDIR/release/SOURCES.txt" 2>/dev/null || true
  cp "$MDIR/outputs/disclosures.txt" "$MDIR/release/disclosures.txt" 2>/dev/null || true
  cp "$MDIR/outputs/upload-metadata.md" "$MDIR/release/upload-metadata.md" 2>/dev/null || true
  cp "$vfile" "$MDIR/release/legal-verdict.json" 2>/dev/null || true

  cat > "$MDIR/release/PUBLISH-CHECKLIST.md" <<EOF
# Publish checklist — $MISSION_ID

Profile: **$PROFILE**   ·   Legal verdict: **$verdict**   ·   As-of: $AS_OF_DATE

- [ ] Watch \`short.mp4\` end-to-end once (captions legible, no clipped audio)
- [ ] Confirm every disclosure line below is visible on-video or in the caption:
$(sed 's/^/      - /' "$MDIR/outputs/disclosures.txt" 2>/dev/null)
- [ ] Title / description / tags copied from \`upload-metadata.md\` per platform
- [ ] Attribution preserved (SOURCES.txt) — Pexels credit retained
$( [[ "$PROFILE" == "news" ]] && echo "- [ ] 'As of $AS_OF_DATE' stamp present (news recency)" )
$( [[ "$PROFILE" == "idol" ]] && echo "- [ ] Fan-content (비공식 팬 제작) + AI-narration disclaimers burned in; NO member imagery / group audio used without rights" )
- [ ] Upload MANUALLY via each platform's UI (no auto-upload; public URLs stay out of the repo)

Generated by agents/missions/content-short/run.sh (출시팀 stage).
EOF
  log_ok "release package → $MDIR/release/"
}

# ── dispatch ────────────────────────────────────────────────────────────────
# Stages are decoupled: legal/release operate on an ALREADY-produced mission
# (require_produced), they do NOT re-render. The orchestrator threads one
# --mission-dir across produce → legal → release. `all` is the headless one-shot
# that runs every stage in a single freshly-minted dir.
RC=0
case "$STAGE" in
  produce) stage_produce ;;
  legal)   require_produced; stage_legal; RC=$? ;;
  release) require_produced; stage_release; RC=$? ;;
  all)
    stage_produce
    stage_legal; RC=$?
    if [[ $RC -eq 0 ]]; then
      stage_release; RC=$?
    else
      log_warn "legal verdict not PASS (rc=$RC) — skipping release. See $MDIR/legal/legal-verdict.json"
      log_warn "  (the agent-orchestrated loop applies the legal fix list, re-runs produce with the"
      log_warn "   SAME --mission-dir, then re-gates; headless 'all' stops here.)"
    fi
    ;;
  *) log_err "unknown stage '$STAGE'"; exit 64 ;;
esac

echo
log_step "content-short [$PROFILE/$SHORT_ID] done — mission: $MDIR (rc=$RC)"
exit $RC
