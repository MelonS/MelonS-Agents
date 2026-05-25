#!/usr/bin/env bash
# music-video-grade.sh — apply a per-genre base color grade to a
# rendered mp4.  Inserted between the base render and the post-shader
# stage so the shader operates on already-graded content.
#
# Per docs/research/2026-05-22-music-video-pro-practices.md §2, color
# grading is a genre-locked signature.  Pro practice locks a base
# grade per genre family; the shader is the accent on top.  The
# pipeline previously had only descriptive `lut_direction` strings;
# this script makes them executable.
#
# Profiles (per the research doc):
#   kr_warm_pastel        kpop_ballad / citypop / kpop_dance (warm
#                         pastel, raised gamma, gentle highlight roll)
#   hollywood_teal_orange uspop / synthwave alt (push shadows teal,
#                         highlights warm — Western cinematic default)
#   synthwave_neon        synthwave / vaporwave (high saturation, red
#                         lift, blue boost, vintage curve)
#   lofi_warm_grain       lofi_hiphop / jazz / shoegaze (desaturated,
#                         flat contrast, warm midtones, temporal noise)
#   city_pop_neon         citypop (saturated, blue shadows, pink mid)
#   rnb_low_key           rnb / jazz / shoegaze evening (red lift,
#                         crushed shadows, low saturation)
#   neutral               no grade (backward-compat default)
#
# Usage:
#   scripts/music-video-grade.sh <input.mp4> <output.mp4> <profile>

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true

SRC="${1:-}"
DST="${2:-}"
PROFILE="${3:-neutral}"

if [[ -z "$SRC" || -z "$DST" ]]; then
  echo "usage: $0 <input.mp4> <output.mp4> <profile>" >&2
  echo "profiles: kr_warm_pastel | hollywood_teal_orange | synthwave_neon |" >&2
  echo "          lofi_warm_grain | city_pop_neon | rnb_low_key | neutral" >&2
  exit 64
fi
[[ -f "$SRC" ]] || { echo "input not found: $SRC" >&2; exit 64; }

# Filter graph per profile.  Each FILTER is the -vf body.
case "$PROFILE" in
  kr_warm_pastel)
    FILTER="eq=saturation=0.85:contrast=0.95:gamma=1.05,colorbalance=rs=0.05:gs=0.0:bs=-0.05:rm=0.05:gm=0.02:bm=-0.05,curves=preset=lighter"
    ;;
  hollywood_teal_orange)
    FILTER="colorbalance=rs=0.10:gs=0.0:bs=-0.10:rm=0.05:gm=0.0:bm=0.0:rh=0.05:gh=0.0:bh=-0.05"
    ;;
  synthwave_neon)
    FILTER="eq=saturation=1.40:contrast=1.15,colorchannelmixer=rr=1.10:gb=0.15:bb=1.10,curves=preset=vintage"
    ;;
  lofi_warm_grain)
    FILTER="eq=saturation=0.75:contrast=0.92:brightness=-0.02,colorbalance=rm=0.04:bm=-0.04,noise=alls=6:allf=t+u"
    ;;
  city_pop_neon)
    FILTER="eq=saturation=1.30:contrast=1.10,colorbalance=rs=-0.05:bs=0.10:rm=0.10:bm=0.05"
    ;;
  rnb_low_key)
    FILTER="eq=saturation=0.80:contrast=1.10:brightness=-0.04,colorbalance=rm=0.08:gm=0.0:bm=-0.04:rh=0.05:gh=0.0:bh=0.0"
    ;;
  neutral|"")
    # No grade — fast stream-copy
    echo "[grade] profile=neutral — stream-copy (no grade applied)" >&2
    "${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}" -y -loglevel error \
      -i "$SRC" -c copy "$DST"
    echo "[grade] passthrough → $DST"
    exit 0
    ;;
  *)
    echo "unknown profile: $PROFILE" >&2
    exit 64
    ;;
esac

echo "[grade] profile=$PROFILE"
"${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}" -y -loglevel warning -stats \
  -i "$SRC" \
  -vf "$FILTER,setsar=1" \
  -c:v libx264 -preset medium -crf 22 -pix_fmt yuv420p -c:a copy "$DST"

if [[ ! -s "$DST" ]]; then
  echo "[grade] failed" >&2
  exit 2
fi

dur=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error -show_entries format=duration -of csv=p=0 "$DST" 2>/dev/null | awk '{printf "%.1f", $1}')
size=$(du -h "$DST" 2>/dev/null | awk '{print $1}')
echo "[grade] ✓ $PROFILE → $DST (${dur}s, $size)"
