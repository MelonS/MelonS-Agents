#!/usr/bin/env bash
# shot-plan.sh — pre-render intent layer between phrase detection
# and B-roll fetch.  Director-discipline analogue: every working
# music-video director writes a per-segment shot plan before the
# crew shoots.  Our pipeline currently goes
#
#   keywords → Pexels API → 8 clips → onset-align
#
# with nothing inspectable in between.  This script adds the
# missing intermediate JSON.
#
# Plan row schema:
#   segment_idx      0-based index
#   t_start, t_end   seconds
#   keyword          Pexels query for this segment
#   emotion          intro / build / hook / climax / outro
#   cut_behaviour    cut | hold | crossfade
#   motif_slot       1 if recurring chorus motif, else 0
#   hook_position    1 if within first 5s (Hong Won-ki rule)
#   shader_intensity 0.0–1.0 envelope hint
#
# Operator inspects + downstream stages consume.  Plan is OPT-IN —
# pipeline unchanged unless MUSIC_VIDEO_USE_SHOT_PLAN=1.
#
# Usage:
#   scripts/shot-plan.sh --keywords "a,b,c" --genre kpop_ballad --duration 60 [--out PATH]

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

KEYWORDS=""
GENRE="lofi_hiphop"
DURATION=60
OUT_PATH="-"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --keywords=*) KEYWORDS="${1#*=}"; shift ;;
    --keywords)   KEYWORDS="${2:-}"; shift 2 ;;
    --genre=*)    GENRE="${1#*=}"; shift ;;
    --genre)      GENRE="${2:-}"; shift 2 ;;
    --duration=*) DURATION="${1#*=}"; shift ;;
    --duration)   DURATION="${2:-}"; shift 2 ;;
    --out=*)      OUT_PATH="${1#*=}"; shift ;;
    --out)        OUT_PATH="${2:-}"; shift 2 ;;
    -h|--help)
      sed -n '2,24p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *) echo "unknown flag: $1" >&2; exit 64 ;;
  esac
done

if [[ -z "$KEYWORDS" ]]; then
  echo "usage: $0 --keywords \"a,b,c\" --genre NAME --duration N [--out PATH]" >&2
  exit 64
fi

PRESETS="$REPO_ROOT/skills/music-video/data/genre-presets.yaml"
[[ -f "$PRESETS" ]] || { echo "presets file missing: $PRESETS" >&2; exit 2; }

CUT_DENSITY=$(yq -r ".genres.${GENRE}.cut_density // \"moderate\"" "$PRESETS" 2>/dev/null)
LANG_ANCHOR=$(yq -r ".genres.${GENRE}.lang_anchor // \"neutral\"" "$PRESETS" 2>/dev/null)
SHADER_RATIO=$(yq -r ".genres.${GENRE}.shader_active_ratio // 1.0" "$PRESETS" 2>/dev/null)

case "$CUT_DENSITY" in
  continuous) SEG_COUNT=1 ;;
  sparse)     SEG_COUNT=$(( DURATION / 6 + 1 )) ;;
  moderate)   SEG_COUNT=$(( DURATION / 3 + 1 )) ;;
  dense)      SEG_COUNT=$(( DURATION ))           ;;
  *)          SEG_COUNT=$(( DURATION / 5 + 1 )) ;;
esac

KEYWORDS="$KEYWORDS" GENRE="$GENRE" CUT_DENSITY="$CUT_DENSITY" \
  DURATION="$DURATION" SEG_COUNT="$SEG_COUNT" LANG_ANCHOR="$LANG_ANCHOR" \
  SHADER_RATIO="$SHADER_RATIO" OUT_PATH="$OUT_PATH" python3 - <<'PY'
import json, os

kws = [k.strip() for k in os.environ["KEYWORDS"].split(",") if k.strip()]
genre = os.environ["GENRE"]
density = os.environ["CUT_DENSITY"]
duration = int(os.environ["DURATION"])
seg_count = max(1, int(os.environ["SEG_COUNT"]))
lang_anchor = os.environ["LANG_ANCHOR"]
shader_ratio = float(os.environ["SHADER_RATIO"])

def emotion_for(t_frac):
    if t_frac < 0.10:  return "intro"
    if t_frac < 0.30:  return "build"
    if t_frac < 0.55:  return "hook"
    if t_frac < 0.80:  return "climax"
    return "outro"

def cut_behaviour_for(t_frac, density):
    if density == "continuous":
        return "hold"
    if density == "sparse":
        return "hold" if 0.10 < t_frac < 0.30 else "cut"
    if density == "dense":
        return "cut"
    return "hold" if t_frac >= 0.85 else "cut"

def shader_for(t_frac):
    if t_frac < 0.10:  return 0.5
    if t_frac < 0.55:  return 0.7
    if t_frac < 0.80:  return 1.0
    return 0.4

def keyword_for(i, seg_count):
    if i == 0:
        return kws[0]
    if i % 3 == 0:
        return kws[0]
    if len(kws) > 1:
        return kws[1 + (i % (len(kws) - 1))]
    return kws[0]

plan = []
for i in range(seg_count):
    t_start = round(i * duration / seg_count, 3)
    t_end = round((i + 1) * duration / seg_count, 3)
    t_mid_frac = (t_start + t_end) / 2 / duration
    is_motif = 1 if (i > 0 and i % 3 == 0) else 0
    plan.append({
        "segment_idx": i,
        "t_start": t_start,
        "t_end": t_end,
        "keyword": keyword_for(i, seg_count),
        "emotion": emotion_for(t_mid_frac),
        "cut_behaviour": cut_behaviour_for(t_mid_frac, density),
        "motif_slot": is_motif,
        "hook_position": 1 if t_end <= 5.0 else 0,
        "shader_intensity": round(shader_for(t_mid_frac) * shader_ratio, 3),
    })

doc = {
    "version": "1.0",
    "genre": genre,
    "duration_s": duration,
    "cut_density": density,
    "lang_anchor": lang_anchor,
    "segment_count": seg_count,
    "shader_ratio_base": shader_ratio,
    "segments": plan,
}

out_path = os.environ["OUT_PATH"]
if out_path == "-":
    print(json.dumps(doc, indent=2, ensure_ascii=False))
else:
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"[shot-plan] wrote {out_path} ({seg_count} segments, density={density}, anchor={lang_anchor})")
PY
