#!/usr/bin/env bash
# music-video-qa-anchor.sh — score a finished music-video mission's
# B-roll against the genre's lang_anchor.
#
# Quality-bar #5/#6 (2026-05-22): KR vocal renders should have ≥30%
# of their B-roll keywords matching a Korean/Asian anchor; EN should
# have ≥30% Western anchor; neutral genres are always considered
# anchor-compliant.
#
# Usage:
#   scripts/music-video-qa-anchor.sh <mission_dir> [--genre=NAME]
#
# Reads:
#   <mission_dir>/resources/clips/raw-<keyword>.mp4
#   (resolves --genre or falls back to detecting from short_id suffix
#    in the path — best-effort)
#
# Emits:
#   <mission_dir>/qa-anchor.json   structured verdict
#   stdout                          human-readable summary
#
# Exit codes:
#   0  PASS (≥30% anchor-matching or anchor=neutral)
#   1  WARN (<30% anchor-matching, but not zero)
#   2  FAIL (zero anchor-matching for non-neutral anchor)
#   64 usage error

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PRESETS="$REPO_ROOT/skills/music-video/data/genre-presets.yaml"

MDIR="${1:-}"
GENRE=""
for arg in "$@"; do
  case "$arg" in --genre=*) GENRE="${arg#*=}" ;; esac
done

if [[ -z "$MDIR" || ! -d "$MDIR" ]]; then
  echo "usage: $0 <mission_dir> [--genre=NAME]" >&2
  exit 64
fi

CLIPS="$MDIR/resources/clips"
if [[ ! -d "$CLIPS" ]]; then
  echo "❌ no clips/ dir under $MDIR" >&2
  exit 64
fi

# Resolve lang_anchor.  If --genre given, look up directly.  Else
# operator must provide; we can't reliably infer from path alone.
if [[ -z "$GENRE" ]]; then
  echo "WARN: --genre not supplied; defaulting to neutral (no scoring)" >&2
  ANCHOR="neutral"
else
  ANCHOR=$(yq -r ".genres.${GENRE}.lang_anchor // \"neutral\"" "$PRESETS" 2>/dev/null)
fi

# Build anchor keyword regex.
case "$ANCHOR" in
  ko)
    POS_RE='(korean|asian|seoul|japanese|tokyo|cjk|kbeauty)'
    NEG_RE='(american|european|western|los angeles|new york|london)'
    ;;
  en)
    POS_RE='(american|western|new york|los angeles|london|european|cinematic portrait|us flag)'
    NEG_RE='(korean|tokyo|seoul|cjk|japanese|asian street)'
    ;;
  mixed)
    POS_RE='(korean|asian|seoul|japanese|tokyo|citypop|street neon|asian|warm city)'
    NEG_RE=''
    ;;
  neutral|*)
    POS_RE=''
    NEG_RE=''
    ;;
esac

# Collect keywords from raw-*.mp4 filenames.
TOTAL=0
POS=0
NEG=0
declare -a KW_LIST
for f in "$CLIPS"/raw-*.mp4; do
  [[ -f "$f" ]] || continue
  bn=$(basename "$f" .mp4)
  kw="${bn#raw-}"
  # Convert underscores back to spaces
  kw_text=$(echo "$kw" | tr '_' ' ')
  KW_LIST+=("$kw_text")
  TOTAL=$(( TOTAL + 1 ))
  if [[ -n "$POS_RE" ]] && echo "$kw_text" | grep -qiE "$POS_RE"; then
    POS=$(( POS + 1 ))
  fi
  if [[ -n "$NEG_RE" ]] && echo "$kw_text" | grep -qiE "$NEG_RE"; then
    NEG=$(( NEG + 1 ))
  fi
done

if [[ "$TOTAL" -eq 0 ]]; then
  echo "❌ no raw clips found in $CLIPS" >&2
  exit 64
fi

RATIO=$(awk -v p="$POS" -v t="$TOTAL" 'BEGIN{printf "%.2f", p/t}')

# Decide verdict.
if [[ "$ANCHOR" == "neutral" ]]; then
  VERDICT="PASS"
  REASON="neutral anchor — no scoring"
  EXIT=0
elif (( POS == 0 )); then
  VERDICT="FAIL"
  REASON="zero anchor-matching keywords (anchor=$ANCHOR)"
  EXIT=2
elif awk -v r="$RATIO" 'BEGIN{exit !(r < 0.30)}'; then
  VERDICT="WARN"
  REASON="$POS/$TOTAL = ${RATIO} anchor-matching (below 30% threshold)"
  EXIT=1
else
  VERDICT="PASS"
  REASON="$POS/$TOTAL = ${RATIO} anchor-matching (≥30% threshold)"
  EXIT=0
fi

# Emit JSON verdict.
JSON="$MDIR/qa-anchor.json"
{
  echo "{"
  echo "  \"mission_dir\": \"$MDIR\","
  echo "  \"anchor\": \"$ANCHOR\","
  echo "  \"genre\": \"${GENRE:-unknown}\","
  echo "  \"total_clips\": $TOTAL,"
  echo "  \"anchor_match\": $POS,"
  echo "  \"contradicting\": $NEG,"
  echo "  \"anchor_ratio\": $RATIO,"
  echo "  \"verdict\": \"$VERDICT\","
  echo "  \"reason\": \"$REASON\","
  echo "  \"keywords\": ["
  for i in "${!KW_LIST[@]}"; do
    if (( i < ${#KW_LIST[@]} - 1 )); then
      printf '    "%s",\n' "${KW_LIST[$i]}"
    else
      printf '    "%s"\n' "${KW_LIST[$i]}"
    fi
  done
  echo "  ]"
  echo "}"
} > "$JSON"

# Human-readable.
echo "=== qa-anchor verdict ==="
echo "mission:  $(basename "$MDIR")"
echo "anchor:   $ANCHOR  (genre=$GENRE)"
echo "total:    $TOTAL clips"
echo "matching: $POS  (ratio $RATIO)"
[[ -n "$NEG_RE" ]] && echo "contra:   $NEG"
echo "verdict:  $VERDICT — $REASON"
echo "json:     $JSON"

exit "$EXIT"
