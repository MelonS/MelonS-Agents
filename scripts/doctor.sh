#!/usr/bin/env bash
# doctor.sh — repo-wide runtime health check.  Answers the question
# "is this machine ready to do work *right now*?" in under 2 seconds.
#
# Complements the auditor (which asks "is the code obeying the
# operator contract?", slow + Claude-driven).  This script is fast,
# Claude-free, and only inspects environment + filesystem + git
# state.  No tokens consumed.
#
# Usage:
#   bash scripts/doctor.sh           # full report
#   bash scripts/doctor.sh --quiet   # one-line summary
#   bash scripts/doctor.sh --json    # machine output
#
# Exit codes:
#   0  all PASS
#   1  one or more WARN (degraded but workable)
#   2  one or more FAIL (something is broken)

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

mode="full"
case "${1:-}" in
  --quiet) mode="quiet" ;;
  --json)  mode="json" ;;
  -h|--help)
    sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'
    exit 0
    ;;
esac

# Source .env if present so checks can see configured keys.
if [[ -f "$REPO_ROOT/.env" ]]; then
  set -o allexport
  # shellcheck disable=SC1091
  source "$REPO_ROOT/.env"
  set +o allexport
fi

# results[i] format: "STATUS\tNAME\tDETAIL"
# STATUS in {PASS, WARN, FAIL}
results=()
pass=0
warn=0
fail=0

record() {
  local status="$1" name="$2" detail="$3"
  results+=("${status}"$'\t'"${name}"$'\t'"${detail}")
  case "$status" in
    PASS) pass=$((pass + 1)) ;;
    WARN) warn=$((warn + 1)) ;;
    FAIL) fail=$((fail + 1)) ;;
  esac
}

# --- 1. CLI tool presence ---------------------------------------------------

check_cli() {
  local cmd="$1" required="$2"   # required = "required" | "optional"
  if command -v "$cmd" >/dev/null 2>&1; then
    record PASS "cli:$cmd" "$(command -v "$cmd")"
  elif [[ "$required" == "required" ]]; then
    record FAIL "cli:$cmd" "not on PATH"
  else
    record WARN "cli:$cmd" "not on PATH (optional)"
  fi
}

for c in bash git jq curl awk sed grep; do
  check_cli "$c" required
done

# claude CLI: required for missions + auditor, but absent on a fresh
# clone is recoverable — surface as WARN, not FAIL.
if command -v claude >/dev/null 2>&1; then
  record PASS "cli:claude" "$(command -v claude)"
else
  # try the canonical fallback locations the auditor uses
  found=""
  for candidate in "$HOME"/.nvm/versions/node/*/bin/claude "$HOME"/.local/bin/claude /opt/homebrew/bin/claude /usr/local/bin/claude; do
    [[ -x "$candidate" ]] && { found="$candidate"; break; }
  done
  if [[ -n "$found" ]]; then
    record WARN "cli:claude" "found at $found but not on \$PATH"
  else
    record WARN "cli:claude" "not on PATH or known fallback paths"
  fi
fi

# Skill-specific tools — optional, only matter when running those skills.
for c in ffmpeg ffprobe aubiotrack aubioonset ollama yt-dlp whisper-cli; do
  check_cli "$c" optional
done

# --- 2. ffmpeg libass support ----------------------------------------------
# music-video needs libass for subtitle burn-in.  Homebrew default
# `ffmpeg` ships without libass; the operator uses the `ffmpeg-full`
# keg as a fallback (see agents/lib/env.sh).  Check both.

ffmpeg_libass_bin=""
for cand in "${FFMPEG_BIN:-}" /opt/homebrew/opt/ffmpeg-full/bin/ffmpeg /usr/local/opt/ffmpeg-full/bin/ffmpeg "$(command -v ffmpeg 2>/dev/null || true)"; do
  [[ -z "$cand" ]] && continue
  [[ -x "$cand" ]] || continue
  # Capture filter list to a variable instead of piping into grep -q,
  # because grep -q closes the pipe and ffmpeg receives SIGPIPE; with
  # pipefail enabled (set above), that propagates as a "no match" even
  # when the filter line is present.
  filters_out="$("$cand" -hide_banner -filters 2>/dev/null || true)"
  if printf '%s\n' "$filters_out" | grep -qE '[[:space:]]ass[[:space:]]'; then
    ffmpeg_libass_bin="$cand"
    break
  fi
done

if [[ -n "$ffmpeg_libass_bin" ]]; then
  record PASS "ffmpeg-libass" "available via $ffmpeg_libass_bin"
elif command -v ffmpeg >/dev/null 2>&1; then
  record WARN "ffmpeg-libass" "no libass-enabled ffmpeg found — music-video subtitle burn-in will fail"
fi

# --- 3. Ollama reachable ----------------------------------------------------

OLLAMA_HOST_URL="${OLLAMA_HOST:-http://127.0.0.1:11434}"
# Some configs set OLLAMA_HOST without scheme (e.g., "127.0.0.1:11434")
case "$OLLAMA_HOST_URL" in
  http://*|https://*) ;;
  *) OLLAMA_HOST_URL="http://${OLLAMA_HOST_URL}" ;;
esac
if curl -sfm 1 "${OLLAMA_HOST_URL}/api/version" >/dev/null 2>&1; then
  record PASS "ollama" "reachable at $OLLAMA_HOST_URL"
else
  record WARN "ollama" "not reachable at $OLLAMA_HOST_URL (run \`ollama serve\` if you need it)"
fi

# --- 4. .env presence -------------------------------------------------------

if [[ -f "$REPO_ROOT/.env" ]]; then
  record PASS ".env" "present"
else
  record WARN ".env" "missing — copy .env.example → .env (some skills will use defaults)"
fi

# --- 5. Required env keys ---------------------------------------------------
# Track per-key state; missing = WARN (skill works without, just degraded).
check_env_key() {
  local key="$1" purpose="$2"
  local val="${!key:-}"
  if [[ -n "$val" ]]; then
    record PASS "env:$key" "set ($purpose)"
  else
    record WARN "env:$key" "unset ($purpose)"
  fi
}

check_env_key PEXELS_API_KEY     "music-video B-roll fetch"
check_env_key WANTED_API_KEY     "job-hunt 원티드 live HTTP"
check_env_key SARAMIN_KEY        "job-hunt 사람인 live HTTP"
check_env_key ANTHROPIC_API_KEY  "Tier-1 Claude-call helpers (optional — Max plan via CLI works too)"

# --- 6. launchd schedulers (macOS only) ------------------------------------

if [[ "$(uname)" == "Darwin" ]] && command -v launchctl >/dev/null 2>&1; then
  expected=("com.melons.agents.queue" "com.melons.agents.auditor" "com.melons.agents.audit-poll" "com.melons.agents.disk-watch")
  loaded="$(launchctl list 2>/dev/null | awk 'NR>1 {print $3}' | grep -E '^com\.melons\.agents\.' || true)"
  missing=()
  for e in "${expected[@]}"; do
    if ! grep -qFx "$e" <<<"$loaded"; then
      missing+=("$e")
    fi
  done
  if (( ${#missing[@]} == 0 )); then
    record PASS "schedulers" "all 4 launchd jobs loaded"
  else
    record WARN "schedulers" "missing: ${missing[*]} (run scripts/install-scheduler.sh install all)"
  fi
fi

# --- 7. Audit alert state --------------------------------------------------

ALERT="$REPO_ROOT/docs/audit/CURRENT-ALERT.md"
if [[ -f "$ALERT" ]]; then
  verdict="$(grep -m1 -E '^\*\*Verdict\*\*:' "$ALERT" 2>/dev/null | sed -E 's/^\*\*Verdict\*\*:[[:space:]]*//' | awk '{print $1}')"
  record WARN "audit-alert" "${verdict:-NON_CLEAN} — see docs/audit/CURRENT-ALERT.md"
else
  record PASS "audit-alert" "no alert (last audit verdict was CLEAN)"
fi

# --- 8. Git working tree + sync --------------------------------------------

if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  dirty="$(git status --porcelain 2>/dev/null | head -1)"
  if [[ -n "$dirty" ]]; then
    record WARN "git-tree" "uncommitted changes ($(git status --porcelain | wc -l | tr -d ' ') files)"
  else
    record PASS "git-tree" "clean"
  fi

  branch="$(git rev-parse --abbrev-ref HEAD)"
  upstream="$(git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null || true)"
  if [[ -n "$upstream" ]]; then
    ahead="$(git rev-list --count "${upstream}..HEAD" 2>/dev/null || echo "?")"
    behind="$(git rev-list --count "HEAD..${upstream}" 2>/dev/null || echo "?")"
    if [[ "$ahead" == "0" && "$behind" == "0" ]]; then
      record PASS "git-sync" "$branch synced with $upstream"
    else
      record WARN "git-sync" "$branch  ahead:$ahead  behind:$behind  ($upstream)"
    fi
  else
    record WARN "git-sync" "$branch has no upstream"
  fi
else
  record FAIL "git-tree" "not inside a git work tree"
fi

# --- 9. Disk space ----------------------------------------------------------

if command -v df >/dev/null 2>&1; then
  # Parse free GB from df -k (POSIX-portable across macOS / Linux).
  avail_kb="$(df -k "$REPO_ROOT" | awk 'NR==2 {print $4}')"
  if [[ -n "$avail_kb" ]]; then
    avail_gb=$((avail_kb / 1024 / 1024))
    if (( avail_gb < 5 )); then
      record FAIL "disk" "${avail_gb}G free — critical (<5G)"
    elif (( avail_gb < 15 )); then
      record WARN "disk" "${avail_gb}G free — low (<15G)"
    else
      record PASS "disk" "${avail_gb}G free"
    fi
  fi
fi

# --- 10. Per-skill activation status ---------------------------------------

shopt -s nullglob
for status_script in skills/*/scripts/status.sh; do
  skill_name="$(basename "$(dirname "$(dirname "$status_script")")")"
  if [[ -x "$status_script" ]]; then
    summary="$(bash "$status_script" --quiet 2>/dev/null || true)"
    if [[ -n "$summary" ]]; then
      record PASS "skill:$skill_name" "$summary"
    else
      record WARN "skill:$skill_name" "status.sh present but returned no summary"
    fi
  fi
done
shopt -u nullglob

# --- 11. Skill manifest drift ---------------------------------------------

if [[ -x "$REPO_ROOT/scripts/audit-skill-drift.sh" ]]; then
  drift_quiet="$("$REPO_ROOT/scripts/audit-skill-drift.sh" --quiet 2>/dev/null || true)"
  drift_count="$(echo "$drift_quiet" | grep -oE '[0-9]+ findings' | head -1 | awk '{print $1}')"
  if [[ "$drift_count" == "0" ]]; then
    record PASS "skill-drift" "no drift"
  elif [[ -n "$drift_count" ]]; then
    record WARN "skill-drift" "$drift_count findings (run scripts/audit-skill-drift.sh)"
  fi
fi

# --- Output ----------------------------------------------------------------

overall="PASS"
exit_code=0
if (( fail > 0 )); then
  overall="FAIL"
  exit_code=2
elif (( warn > 0 )); then
  overall="WARN"
  exit_code=1
fi

case "$mode" in
  quiet)
    printf 'doctor: %s (%d pass / %d warn / %d fail)\n' "$overall" "$pass" "$warn" "$fail"
    ;;
  json)
    printf '{"overall":"%s","pass":%d,"warn":%d,"fail":%d,"checks":[' \
      "$overall" "$pass" "$warn" "$fail"
    sep=''
    for r in "${results[@]}"; do
      IFS=$'\t' read -r status name detail <<<"$r"
      # naive escape — values shouldn't contain quotes
      esc_detail="${detail//\"/\'}"
      printf '%s{"status":"%s","name":"%s","detail":"%s"}' "$sep" "$status" "$name" "$esc_detail"
      sep=','
    done
    printf ']}\n'
    ;;
  full)
    # Color helpers — only when stdout is a TTY.
    if [[ -t 1 ]]; then
      C_PASS=$'\033[32m'
      C_WARN=$'\033[33m'
      C_FAIL=$'\033[31m'
      C_RST=$'\033[0m'
      C_DIM=$'\033[2m'
    else
      C_PASS=""; C_WARN=""; C_FAIL=""; C_RST=""; C_DIM=""
    fi

    printf '\n'
    printf '  doctor — repo health  %s(%s)%s\n' "$C_DIM" "$(date '+%Y-%m-%d %H:%M:%S')" "$C_RST"
    printf '  ─────────────────────────────────────────────────────────────────\n\n'

    for r in "${results[@]}"; do
      IFS=$'\t' read -r status name detail <<<"$r"
      case "$status" in
        PASS) icon="${C_PASS}✓${C_RST}" ;;
        WARN) icon="${C_WARN}⚠${C_RST}" ;;
        FAIL) icon="${C_FAIL}✗${C_RST}" ;;
      esac
      printf '  %s  %-22s  %s\n' "$icon" "$name" "$detail"
    done

    printf '\n  ─────────────────────────────────────────────────────────────────\n'
    case "$overall" in
      PASS) ov_color="${C_PASS}" ;;
      WARN) ov_color="${C_WARN}" ;;
      FAIL) ov_color="${C_FAIL}" ;;
    esac
    printf '  Overall: %s%s%s    pass:%d  warn:%d  fail:%d\n\n' \
      "$ov_color" "$overall" "$C_RST" "$pass" "$warn" "$fail"
    ;;
esac

exit "$exit_code"
