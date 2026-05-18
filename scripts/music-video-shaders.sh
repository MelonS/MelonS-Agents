#!/usr/bin/env bash
# Apply post-processing shader effects to a music-video mp4.
#
# The music-video mission produces a base 60s 9:16 short with v6 vintage
# treatment (film grain + vignette + zoom-pulse on glitch onsets) already
# baked in.  This script layers additional shader-style effects on top of
# that base output — pond-surface ripple, breathing zoom, warm-light
# halation, or a phrase-aware dynamic combo.
#
# Usage:
#   scripts/music-video-shaders.sh <effect> <input.mp4> <output.mp4>
#
# Effects:
#   pond        Animated water-surface displacement (3-component sin
#               wave field), max ±13 px → ~1.2% of 1080w.  Feels like
#               the whole screen IS a pond surface, gently sway.
#   breathing   5s-period gentle scale wave (upscale-only so crop never
#               under-runs), ±2.5%.  Soft "the image is breathing" feel.
#   halation    Warm light bloom around bright pixels — split + brightness-
#               threshold + gblur + screen blend at 0.30 opacity.  Adds an
#               80s-film warm glow over amber/neon regions.
#   combo       Pond + halation, both with phrase-aware envelope tied
#               to a default 95.8 BPM / 7.5s phrase boundary.  Pond is
#               off during intro/outro, full during climax (22.5-45s);
#               halation modulates 0.10 → 0.35 → 0.20 across the same
#               structure.  This is the validated end product.
#
# Caveats:
#   - Phrase-aware combo assumes a 95.8 BPM source (the Velvet Turntable
#     reference track).  For other tracks, edit the GATE_POND / OPACITY
#     expressions below to match the actual phrase boundary cadence
#     (BPM × 12 beats / 60).
#   - cel-shading (cartoon look) is intentionally NOT here — ffmpeg
#     native filters posterize luma/chroma independently which breaks
#     hue.  Real cel-shading needs GLSL shaders (mpv + libplacebo),
#     EbSynth, or AI stylization (Stable Diffusion + AnimateDiff /
#     RunwayML).  Out of scope for this ffmpeg pipeline.
#
# Time budget: pond / combo run a geq displacement-map generator at
# 540x960 (4× faster than at 1080x1920) then scale up before displace.
# Combo is the heaviest, ~3-5 min on a 60s 1080p source on M-series Mac.
# Breathing and halation are 60-90 s.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
source "$PWD/agents/lib/env.sh" 2>/dev/null || true

EFFECT="${1:-combo}"
SRC="${2:-}"
DST="${3:-}"

if [[ -z "$SRC" || -z "$DST" ]]; then
  echo "usage: $0 <pond|breathing|halation|combo> <input.mp4> <output.mp4>" >&2
  exit 64
fi
[[ -f "$SRC" ]] || { echo "❌ input not found: $SRC" >&2; exit 64; }
# §8 exception: `/opt/homebrew/bin/ffmpeg` (and `ffprobe` at the bottom of
# this script) appear ONLY as fallback values inside `${FFMPEG_BIN:-...}` /
# `${FFPROBE_BIN:-...}` parameter expansion — never as the resolved path.
# When `agents/lib/env.sh` sources cleanly above, $FFMPEG_BIN is set via
# its libass-aware discovery loop and the literal here is unreachable.
# Same documented-exception pattern as `agents/lib/env.sh` itself and
# `scripts/audit-run.sh`'s claude-CLI fallback.
[[ -x "${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}" ]] || { echo "❌ FFMPEG_BIN not executable" >&2; exit 1; }

case "$EFFECT" in
  pond)
    # Static (always-on) pond surface, ~13 px max displacement.
    XMAP="128 + 7*sin(X*0.018 + Y*0.008 + T*1.8) + 4*sin(X*0.006 + Y*0.022 - T*1.15 + 1.0) + 2*sin(X*0.04 - Y*0.02 + T*3.4 + 2.3)"
    YMAP="128 + 7*sin(X*0.008 + Y*0.018 - T*1.6) + 4*sin(X*0.022 + Y*0.006 + T*1.3 + 1.5) + 2*sin(X*0.02 - Y*0.04 + T*3.1 + 0.7)"
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -filter_complex "
        [0:v]format=yuv420p,setsar=1[v];
        nullsrc=size=540x960:rate=30:duration=60,format=yuv420p,
          geq=lum='${XMAP}':cb=128:cr=128,
          scale=1080:1920:flags=bicubic,setsar=1[xm];
        nullsrc=size=540x960:rate=30:duration=60,format=yuv420p,
          geq=lum='${YMAP}':cb=128:cr=128,
          scale=1080:1920:flags=bicubic,setsar=1[ym];
        [v][xm][ym]displace=edge=smear[out]
      " -map "[out]" -map "0:a" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  breathing)
    # Always-upscale 0~5% ramp, 5s period (so crop never under-runs).
    EXPR="(1 + 0.05*(0.5 + 0.5*sin(2*PI*t/5)))"
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "scale=w='trunc(1080*${EXPR}/2)*2':h='trunc(1920*${EXPR}/2)*2':eval=frame,crop=1080:1920,setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  halation)
    # Warm bloom around brights, screen blend 30% opacity.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -filter_complex "
        [0:v]split[base][glow_in];
        [glow_in]eq=brightness=-0.25:contrast=1.6:saturation=1.2,gblur=sigma=22:steps=2[glow];
        [base][glow]blend=all_mode=screen:all_opacity=0.30,setsar=1[out]
      " -map "[out]" -map "0:a" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  combo)
    # Phrase-aware dynamic combo (pond + halation with envelopes).
    # Defaults assume 95.8 BPM, 7.5 s phrases.
    GATE_POND="(clip((T-15)/7.5, 0, 1) - clip((T-45)/7.5, 0, 0.7))"
    OPACITY="(0.10 + 0.25*clip((T-15)/7.5, 0, 1) - 0.15*clip((T-45)/7.5, 0, 1))"
    XB="7*sin(X*0.018 + Y*0.008 + T*1.8) + 4*sin(X*0.006 + Y*0.022 - T*1.15 + 1.0) + 2*sin(X*0.04 - Y*0.02 + T*3.4 + 2.3)"
    YB="7*sin(X*0.008 + Y*0.018 - T*1.6) + 4*sin(X*0.022 + Y*0.006 + T*1.3 + 1.5) + 2*sin(X*0.02 - Y*0.04 + T*3.1 + 0.7)"
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -filter_complex "
        [0:v]format=yuv420p,setsar=1[base];
        nullsrc=size=540x960:rate=30:duration=60,format=yuv420p,
          geq=lum='128 + ${GATE_POND}*(${XB})':cb=128:cr=128,
          scale=1080:1920:flags=bicubic,setsar=1[xm];
        nullsrc=size=540x960:rate=30:duration=60,format=yuv420p,
          geq=lum='128 + ${GATE_POND}*(${YB})':cb=128:cr=128,
          scale=1080:1920:flags=bicubic,setsar=1[ym];
        [base][xm][ym]displace=edge=smear[ponded];
        [ponded]split[hb][hg];
        [hg]eq=brightness=-0.25:contrast=1.6:saturation=1.2,gblur=sigma=22:steps=2[glow];
        [hb][glow]blend=all_expr='A + (255-A)*B/255 * ${OPACITY}',setsar=1[out]
      " -map "[out]" -map "0:a" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  *)
    echo "❌ unknown effect: $EFFECT (use pond | breathing | halation | combo)" >&2
    exit 64
    ;;
esac

dur=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error -show_entries format=duration -of csv=p=0 "$DST" 2>/dev/null | awk '{printf "%.1f", $1}')
size=$(du -h "$DST" 2>/dev/null | awk '{print $1}')
echo "✓ ${EFFECT}: $DST (${dur}s, ${size})"
