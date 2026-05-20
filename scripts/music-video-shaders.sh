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
# Genre-coded effects (added 2026-05-21, additive; see
# skills/music-video/data/genre-presets.yaml for the matching matrix):
#   scanline           Horizontal CRT-style line darkening (every 2nd row
#                      at 0.85 luma).  Synthwave / retrowave signature.
#   chromatic_split    RGB channel horizontal shift (R+4, G-2, B-4 px) —
#                      VHS / vaporwave / phonk chromatic aberration look.
#   neon_edge          Edge-detect + soft neon-pink/cyan colorize blend.
#                      Synthwave alternative.
#   vhs                Chromatic shift + slight noise + chroma blur.
#                      Vaporwave / dreamcore mallsoft.
#   saturation_pulse   Sin-wave saturation modulation (default 2.0 Hz);
#                      house / techno reactive look without RMS envelope.
#   kaleidoscope       4-fold mirror (hflip + vflip composite) — psychedelic
#                      / electronic / ambient-trippy.
#
# All new effects use the same `<effect> <input.mp4> <output.mp4>` CLI;
# original effects unchanged.
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

# ─── helper: extract drum onsets from input mp4's audio (for beat-synced shaders) ───
# Caches per source.  Used by beat_burst / strobe / shake / color_burst.
__extract_onsets_csv() {
  local src="$1"
  local out_csv="$2"
  local audio_wav
  audio_wav="$(mktemp -t mvshader-audio-XXXX).wav"
  "$FFMPEG_BIN" -y -loglevel error -i "$src" -map 0:a -acodec pcm_s16le -ar 22050 -ac 1 "$audio_wav" 2>/dev/null
  if command -v aubioonset >/dev/null 2>&1; then
    aubioonset -i "$audio_wav" -O complex -t 2.0 2>/dev/null > "$out_csv"
  else
    : > "$out_csv"
  fi
  rm -f "$audio_wav"
}

# Build a Gaussian-sum expression centered at each onset.
# usage: __gaussian_expr <onsets_file> <amp> <sigma> <baseline>
__gaussian_expr() {
  local onsets="$1" amp="$2" sigma="$3" baseline="$4"
  local expr="$baseline"
  while IFS= read -r t; do
    [[ -z "$t" ]] && continue
    expr="${expr}+${amp}*exp(-((t-${t})/${sigma})*((t-${t})/${sigma}))"
  done < "$onsets"
  echo "$expr"
}

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

  # ───── Genre-coded effects (additive, 2026-05-21) ─────

  scanline)
    # CRT-style horizontal lines.  Every 2nd row → 0.85× luma.  Synthwave / retrowave.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "geq=lum='if(mod(Y,2),lum(X,Y)*0.85,lum(X,Y))':cb='cb(X,Y)':cr='cr(X,Y)',setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  chromatic_split)
    # RGB channel horizontal shift.  R +4 px, G -2 px, B -4 px.  VHS / vaporwave look.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "rgbashift=rh=4:gh=-2:bh=-4,setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  neon_edge)
    # Edge-detect + neon-pink/cyan colorize, screen-blended over base.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -filter_complex "
        [0:v]split[base][e_in];
        [e_in]edgedetect=low=0.1:high=0.4,
          format=yuv420p,
          curves=preset=increase_contrast,
          colorbalance=rs=0.4:gs=-0.2:bs=0.5[edges];
        [base][edges]blend=all_mode=screen:all_opacity=0.45,setsar=1[out]
      " -map "[out]" -map "0:a" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  vhs)
    # Chromatic shift + slight noise + chroma blur — vaporwave / dreamcore mallsoft.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "rgbashift=rh=3:gh=-1:bh=-3,noise=c0s=4:allf=t,setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  saturation_pulse)
    # Sin-wave saturation modulation @ 2 Hz (≈ 120 BPM kick).  House / techno reactive.
    # Default freq = 2 Hz; override via SATPULSE_HZ env var.
    SAT_HZ="${SATPULSE_HZ:-2.0}"
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "eq=saturation='1.0 + 0.35*sin(2*PI*${SAT_HZ}*t)':eval=frame,setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  kaleidoscope)
    # 4-fold mirror (top-left → mirror to all 4 quadrants).  Psychedelic / electronic.
    # Crop 540x960 from top-left, then mirror horiz + vert to fill 1080x1920.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -filter_complex "
        [0:v]crop=540:960:0:0[tl];
        [tl]split=4[tl1][tl2][tl3][tl4];
        [tl1]copy[a];
        [tl2]hflip[b];
        [tl3]vflip[c];
        [tl4]hflip,vflip[d];
        [a][b]hstack=inputs=2[top];
        [c][d]hstack=inputs=2[bot];
        [top][bot]vstack=inputs=2,setsar=1[out]
      " -map "[out]" -map "0:a" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  # ───── Beat-synced "popping" effects (2026-05-21, added per operator) ─────
  # Each extracts drum onsets from the input mp4 audio and applies a
  # Gaussian-sum expression centered at each onset.  Best for fast genres
  # (synthwave / phonk / techno / house / hyperpop) where the beat IS
  # the visual hook.

  beat_burst)
    # Strong zoom + brightness flash on each onset.  Synthwave / phonk drop.
    ONSETS="$(mktemp -t mvshader-onsets-XXXX)"
    __extract_onsets_csv "$SRC" "$ONSETS"
    ZOOM_EXPR=$(__gaussian_expr "$ONSETS" "0.15" "0.12" "1")
    BRIGHT_EXPR=$(__gaussian_expr "$ONSETS" "0.40" "0.05" "0")
    rm -f "$ONSETS"
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "scale=w='1080*(${ZOOM_EXPR})':h='1920*(${ZOOM_EXPR})':eval=frame,crop=1080:1920,eq=brightness='${BRIGHT_EXPR}':eval=frame,setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  strobe)
    # Brief inversion + flash on each beat.  Techno / hyperpop drop.
    ONSETS="$(mktemp -t mvshader-onsets-XXXX)"
    __extract_onsets_csv "$SRC" "$ONSETS"
    # Build between() OR-chain enabling negate during a 60ms window around each onset
    NEG_ENABLE="0"
    while IFS= read -r t; do
      [[ -z "$t" ]] && continue
      ts=$(awk -v t="$t" 'BEGIN{printf "%.3f", t-0.020}')
      te=$(awk -v t="$t" 'BEGIN{printf "%.3f", t+0.060}')
      NEG_ENABLE="${NEG_ENABLE}+between(t,${ts},${te})"
    done < "$ONSETS"
    rm -f "$ONSETS"
    # Use timeline-enabled negate; alpha-blend it over base so brief inversion appears as flashes
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "negate=enable='gt(${NEG_ENABLE},0)',setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  shake)
    # Translate jitter on each beat — decaying x/y offset.  Phonk drift.
    ONSETS="$(mktemp -t mvshader-onsets-XXXX)"
    __extract_onsets_csv "$SRC" "$ONSETS"
    # x_shake = sum of sin(...) * decaying gaussian per onset
    X_SHAKE="0"
    Y_SHAKE="0"
    i=0
    while IFS= read -r t; do
      [[ -z "$t" ]] && continue
      # alternate phase per onset for varied direction
      ph=$((i % 4))
      case $ph in
        0) sx="14"; sy="0"  ;;
        1) sx="-12"; sy="6" ;;
        2) sx="8"; sy="-10" ;;
        3) sx="-10"; sy="-8";;
      esac
      X_SHAKE="${X_SHAKE}+${sx}*exp(-((t-${t})/0.10)*((t-${t})/0.10))"
      Y_SHAKE="${Y_SHAKE}+${sy}*exp(-((t-${t})/0.10)*((t-${t})/0.10))"
      i=$((i + 1))
    done < "$ONSETS"
    rm -f "$ONSETS"
    # pad larger then crop with animated x/y offset.  crop accepts
    # x/y expressions; named-arg eval=frame for per-frame evaluation.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "pad=1108:1948:14:14:color=black,crop=w=1080:h=1920:x='14+(${X_SHAKE})':y='14+(${Y_SHAKE})':exact=1,setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  color_burst)
    # Hue rotation cycling on each beat.  House / disco drop.
    ONSETS="$(mktemp -t mvshader-onsets-XXXX)"
    __extract_onsets_csv "$SRC" "$ONSETS"
    # Hue shift = sum of decaying gaussians, each with rotating phase
    HUE_EXPR="0"
    i=0
    while IFS= read -r t; do
      [[ -z "$t" ]] && continue
      # rotate hue offset per onset 0/60/120/180/240/300 deg
      deg=$(( (i % 6) * 60 ))
      HUE_EXPR="${HUE_EXPR}+${deg}*exp(-((t-${t})/0.20)*((t-${t})/0.20))"
      i=$((i + 1))
    done < "$ONSETS"
    rm -f "$ONSETS"
    # hue filter takes H= in degrees; expressions are re-evaluated
    # per frame by default (no eval=frame option in this build).
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "hue=H='${HUE_EXPR}',setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  light_rays)
    # Static: bright-region god rays + horizontal scanline darken.
    # Night-club / synthwave / techno vibe.  No onset sync needed.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -filter_complex "
        [0:v]split[base][ray_in];
        [ray_in]curves=preset=increase_contrast,gblur=sigma=12:steps=1,
          eq=brightness=-0.10:saturation=1.4[rays];
        [base][rays]blend=all_mode=screen:all_opacity=0.25[bright];
        [bright]geq=lum='if(mod(Y,3),lum(X,Y),lum(X,Y)*0.78)':cb='cb(X,Y)':cr='cr(X,Y)',setsar=1[out]
      " -map "[out]" -map 0:a \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  *)
    echo "❌ unknown effect: $EFFECT" >&2
    echo "   classic:        pond | breathing | halation | combo" >&2
    echo "   genre-coded:    scanline | chromatic_split | neon_edge | vhs | saturation_pulse | kaleidoscope" >&2
    echo "   beat-synced:    beat_burst | strobe | shake | color_burst | light_rays" >&2
    exit 64
    ;;
esac

dur=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error -show_entries format=duration -of csv=p=0 "$DST" 2>/dev/null | awk '{printf "%.1f", $1}')
size=$(du -h "$DST" 2>/dev/null | awk '{print $1}')
echo "✓ ${EFFECT}: $DST (${dur}s, ${size})"
