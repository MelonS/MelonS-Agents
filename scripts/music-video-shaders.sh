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

# Quality-bar #2 (2026-05-22): per-genre shader restraint via blend
# with the un-shaded original.  MUSIC_VIDEO_SHADER_RATIO is set by
# scripts/music-video-genre.sh from the preset's shader_active_ratio
# field (default 1.0 = full strength, 0.0 = invisible, 0.5 = half-mixed).
# When ratio < 1.0, the final write is intercepted to a temp file and
# blended back toward the original at the end of this script.
SHADER_RATIO="${MUSIC_VIDEO_SHADER_RATIO:-1.0}"
RATIO_CMP=$(awk -v r="$SHADER_RATIO" 'BEGIN{print (r < 0.99)}')
SHADER_FINAL_DST="$DST"
if [[ "$RATIO_CMP" == "1" ]]; then
  DST="$(mktemp -t mvshader-blend-XXXX).mp4"
fi
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

  # ───── Stage-1 catalog expansion (2026-05-22 quality-bar #4) ─────

  light_leak)
    # Animated colored light bleed — Super-8 / cinematic accent.
    # Position drifts over time so each render's leak signature is
    # different.  Mood: cottagecore / jazz / citypop / lofi / shoegaze.
    # geq filter uses uppercase T for the timestamp (lowercase t is not
    # in the variable namespace inside geq expressions); pow() not ^.
    # The leak generator's duration is driven from the source's
    # ffprobe'd duration so it doesn't run past the source (otherwise
    # the blend node sees the longer leak stream and the encode
    # never terminates).
    LEAK_DUR=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error \
      -show_entries format=duration -of csv=p=0 "$SRC" 2>/dev/null | \
      awk '{printf "%.2f", $1 + 0.5}')
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -filter_complex "
        [0:v]format=yuv420p,setsar=1[base];
        color=c=0xFF99BB:size=540x960:rate=30:duration=${LEAK_DUR},
          geq=
            lum='180*exp(-(pow((X-(270+100*sin(T*0.3)))/120,2) + pow((Y-(480+150*cos(T*0.2)))/180,2)))':
            cb='128 + 80*exp(-(pow((X-(270+100*sin(T*0.3)))/120,2)))':
            cr='200 - 60*sin(T*0.15)',
          setsar=1[leak_small];
        [leak_small][base]scale2ref=w=iw:h=ih[leak_scaled][base2];
        [base2][leak_scaled]blend=all_mode=screen:all_opacity=0.40[out]
      " -map "[out]" -map 0:a -shortest \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  duotone)
    # Spotify-Wrapped-style two-color quantize.  Source desaturated,
    # then mapped to a magenta→cyan gradient by luminance.  Distinct
    # signature look for citypop / synthwave / vaporwave / pop-art kpop.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -filter_complex "
        [0:v]eq=saturation=0,format=yuv420p,
          geq=
            lum='lum(X,Y)':
            cb='90 + lum(X,Y)*0.40':
            cr='200 - lum(X,Y)*0.45',
          setsar=1[out]
      " -map "[out]" -map 0:a \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  vignette_pulse)
    # Vignette whose radius breathes on a slow sinusoid (≈ 0.25 Hz =
    # 15 bpm visual breathing, half the typical BPM so it doesn't
    # compete with cuts).  Subtle but cinematic.  Mood: kpop_ballad /
    # rnb / citypop / shoegaze.  vignette filter supports the angle
    # field as an expression in t directly, no eval=frame needed.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "vignette=angle='PI/4 + 0.18*sin(2*PI*0.25*t)':mode=forward,setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  # ───── Stage-2 catalog expansion (2026-05-22 quality-bar #4) ─────

  paper_grain)
    # Static paper / canvas texture overlay (distinct from flicker
    # film grain).  Time-invariant noise field, overlay-blended at low
    # opacity to add organic texture without movement.
    # Mood: cottagecore, jazz, classical, indie acoustic, lofi_hiphop.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "noise=alls=4:allf=u,format=yuv420p,setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  dust_speck)
    # Sparse floating dust particles — 8mm celluloid drift.  Built
    # from temporal noise (allf=t) thresholded high so most pixels are
    # zero; screen-blended over source so specks only appear in dark
    # regions.  Distinct from paper_grain's static texture.
    # Mood: jazz, lofi_hiphop, cottagecore, classical, citypop.
    SPECK_DUR=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error \
      -show_entries format=duration -of csv=p=0 "$SRC" 2>/dev/null | \
      awk '{printf "%.2f", $1 + 0.5}')
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -filter_complex "
        [0:v]format=yuv420p,setsar=1[base];
        color=c=black:size=540x960:rate=30:duration=${SPECK_DUR},
          noise=alls=80:allf=t,
          lutyuv=y='if(gt(val,235),val,0)',
          format=yuv420p,
          setsar=1[specks_small];
        [specks_small][base]scale2ref=w=iw:h=ih[specks][base2];
        [base2][specks]blend=all_mode=screen:all_opacity=0.5[out]
      " -map "[out]" -map 0:a -shortest \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  posterize)
    # Discrete tone steps — comic / pop-art look.  Uses lutyuv to
    # quantize luminance to 4 bands (0 / 64 / 128 / 192 / 255), with
    # chroma slightly boosted to compensate for the flat tone bands.
    # Mood: hyperpop, phonk, vaporwave, kpop_dance pop-art moments.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "lutyuv=y='if(gt(val,200),255,if(gt(val,128),192,if(gt(val,64),128,0)))',eq=saturation=1.6,setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  # ───── Stage-3 catalog expansion (2026-05-22 quality-bar #4) ─────

  trail_echo)
    # Motion ghosting via weighted temporal mix — previous frames
    # bleed into the current.  tmix weights bias the current frame at
    # 2x and the trailing 3 frames at 1x each.  Free temporal trails
    # without per-frame post-processing.
    # Mood: techno, house, hyperpop, drone, vaporwave.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -vf "tmix=frames=4:weights='2 1 1 1',setsar=1" \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  soft_bloom)
    # Quieter halation — same bloom mechanic but sigma halved and
    # screen opacity reduced.  For ballads where the warm-light glow
    # is wanted but full halation is too loud.
    # Mood: kpop_ballad, rnb, ambient slow, dreamcore.
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" \
      -filter_complex "
        [0:v]split[base][bright_in];
        [bright_in]eq=brightness=-0.20:contrast=1.3:saturation=1.05,gblur=sigma=10:steps=2[glow];
        [base][glow]blend=all_mode=screen:all_opacity=0.25,setsar=1[out]
      " -map "[out]" -map 0:a \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"
    ;;

  *)
    echo "❌ unknown effect: $EFFECT" >&2
    echo "   classic:        pond | breathing | halation | combo" >&2
    echo "   genre-coded:    scanline | chromatic_split | neon_edge | vhs | saturation_pulse | kaleidoscope" >&2
    echo "   beat-synced:    beat_burst | strobe | shake | color_burst | light_rays" >&2
    echo "   stage-1 expand: light_leak | duotone | vignette_pulse" >&2
    echo "   stage-2 expand: paper_grain | dust_speck | posterize" >&2
    echo "   stage-3 expand: trail_echo | soft_bloom" >&2
    exit 64
    ;;
esac

# Quality-bar #2 (2026-05-22): when SHADER_RATIO < 1.0, blend the
# shaded output back toward the un-shaded original so the effect reads
# as a soft accent rather than a constant overlay.  Final size still
# matches one render (the blend stage replaces the intermediate file).
#
# Two gate modes (MUSIC_VIDEO_SHADER_GATE):
#   uniform        (C.1 Phase 1)  uniformly attenuate effect across full
#                                 duration via blend opacity = ratio.
#   phrase_climax  (C.1 Phase 2)  shader active only in the middle
#                                 (RATIO × duration) window centered at
#                                 50% of runtime; outside the window
#                                 the original passes through unmodified.
#                                 Reads as "shader fires at the climax".
#   onsets         (C.1 Phase 3)  shader fires at each drum onset as a
#                                 short gaussian bell.  Density-controlled
#                                 by selecting every-Nth onset based on
#                                 ratio.  Requires MUSIC_VIDEO_SHADER_ONSETS
#                                 (or _BEATS as fallback) — a file of
#                                 timestamps in seconds, one per line.
#   beats          (C.1 Phase 3 alt)  same mechanism as onsets but reads
#                                 the beat track instead of drum-hit
#                                 onsets.  Reads more "regular".
# Default: uniform (back-compat).
SHADER_GATE="${MUSIC_VIDEO_SHADER_GATE:-uniform}"
if [[ "$RATIO_CMP" == "1" ]]; then
  if [[ "$SHADER_GATE" == "phrase_climax" ]]; then
    GATE_DUR=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error \
      -show_entries format=duration -of csv=p=0 "$DST" 2>/dev/null \
      | awk '{printf "%.3f", $1}')
    GATE_T0=$(awk -v d="$GATE_DUR" -v r="$SHADER_RATIO" 'BEGIN{printf "%.3f", d*((1-r)/2)}')
    GATE_T1=$(awk -v d="$GATE_DUR" -v r="$SHADER_RATIO" 'BEGIN{printf "%.3f", d*((1+r)/2)}')
    GATE_FADE="0.5"
    # all_expr: A = unshaded, B = shaded.  W = trapezoid envelope rising
    # over GATE_FADE, full inside [GATE_T0..GATE_T1], falling over
    # GATE_FADE.  Outside: W=0 (pure unshaded).
    OPACITY_EXPR="if(lt(T,${GATE_T0}-${GATE_FADE}),0,if(lt(T,${GATE_T0}),(T-(${GATE_T0}-${GATE_FADE}))/${GATE_FADE},if(lt(T,${GATE_T1}),1,if(lt(T,${GATE_T1}+${GATE_FADE}),(${GATE_T1}+${GATE_FADE}-T)/${GATE_FADE},0))))"
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" -i "$DST" \
      -filter_complex "[0:v][1:v]blend=all_expr='A*(1-(${OPACITY_EXPR}))+B*(${OPACITY_EXPR})'[v]" \
      -map "[v]" -map 0:a \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$SHADER_FINAL_DST"
    echo "  [phrase_climax] active window ${GATE_T0}s–${GATE_T1}s (of ${GATE_DUR}s)" >&2
  elif [[ "$SHADER_GATE" == "onsets" || "$SHADER_GATE" == "beats" || "$SHADER_GATE" == "drops" ]]; then
    # C.1 Phase 3: per-event gaussian-sum gating.
    # `drops` mode (research §6): use the audio-analyze drops file
    # (1-3 sustained-peak windows per track) instead of every onset.
    # Wider sigma (1.0s) per drop to make the shader fire as a
    # sustained climax accent rather than a brief bell.
    if [[ "$SHADER_GATE" == "drops" ]]; then
      EVENT_FILE="${MUSIC_VIDEO_SHADER_DROPS:-}"
    elif [[ "$SHADER_GATE" == "onsets" ]]; then
      EVENT_FILE="${MUSIC_VIDEO_SHADER_ONSETS:-${MUSIC_VIDEO_SHADER_BEATS:-}}"
    else
      EVENT_FILE="${MUSIC_VIDEO_SHADER_BEATS:-${MUSIC_VIDEO_SHADER_ONSETS:-}}"
    fi
    # Some tracks have sparse onsets (vocal-heavy / ballad / ambient).
    # If the chosen event source has too few events to make a useful
    # gating curve, transparently fall through to the BEATS file if it
    # has more — and if that's also sparse, degenerate gracefully back
    # to phrase_climax instead of leaving the video shaderless.
    if [[ -n "$EVENT_FILE" && -f "$EVENT_FILE" ]]; then
      _EVT_TOTAL_CHECK=$(grep -c . "$EVENT_FILE" 2>/dev/null || echo 0)
      if [[ "$_EVT_TOTAL_CHECK" -lt 5 && "$SHADER_GATE" == "onsets" \
            && -n "${MUSIC_VIDEO_SHADER_BEATS:-}" && -f "${MUSIC_VIDEO_SHADER_BEATS}" ]]; then
        _BEATS_TOTAL=$(grep -c . "$MUSIC_VIDEO_SHADER_BEATS" 2>/dev/null || echo 0)
        if [[ "$_BEATS_TOTAL" -ge 5 ]]; then
          echo "  [onsets] only $_EVT_TOTAL_CHECK onsets (likely vocal track) — switching to BEATS ($_BEATS_TOTAL events)" >&2
          EVENT_FILE="$MUSIC_VIDEO_SHADER_BEATS"
        fi
      fi
    fi
    # Drops mode tolerates as few as 1 event (a track may have just one
    # climax drop).  Onsets/beats need ≥5 to make a useful gating
    # curve (otherwise the gaussian sum is too sparse).
    if [[ "$SHADER_GATE" == "drops" ]]; then _MIN_EVENTS=1; else _MIN_EVENTS=5; fi
    if [[ -z "$EVENT_FILE" || ! -f "$EVENT_FILE" ]] || \
       [[ "$(grep -c . "$EVENT_FILE" 2>/dev/null || echo 0)" -lt "$_MIN_EVENTS" ]]; then
      echo "  [${SHADER_GATE}] event source has <${_MIN_EVENTS} entries — falling back to uniform" >&2
      "$FFMPEG_BIN" -y -loglevel warning -stats \
        -i "$SRC" -i "$DST" \
        -filter_complex "[0:v][1:v]blend=all_mode=normal:all_opacity=${SHADER_RATIO}[v]" \
        -map "[v]" -map 0:a \
        -c:v libx264 -preset medium -crf 22 -c:a copy "$SHADER_FINAL_DST"
    else
      # Select every-Nth event based on ratio.  Lower ratio → sparser
      # event firing (fewer, more isolated bells).  Hard cap at
      # SHADER_EVENT_CAP because ffmpeg's blend `all_expr` has a string
      # length budget and 150+ gaussian terms break it ("Could not open
      # encoder before EOF").  Cap defaults to 30, which is plenty for
      # a 60s render at ~0.3s sigma.
      EVT_TOTAL=$(grep -c . "$EVENT_FILE" 2>/dev/null || echo 0)
      EVT_CAP="${SHADER_EVENT_CAP:-30}"
      # drops mode: every drop is significant by definition (1-3 per
      # track), so stride=1 — fire on ALL drops regardless of ratio.
      # onsets/beats: stride from ratio, then widen to stay under cap.
      if [[ "$SHADER_GATE" == "drops" ]]; then
        STRIDE=1
      else
        STRIDE=$(awk -v r="$SHADER_RATIO" 'BEGIN{n = int(1/r + 0.5); print (n < 1 ? 1 : n)}')
        _PROJECTED=$(( EVT_TOTAL / STRIDE ))
        if [[ "$_PROJECTED" -gt "$EVT_CAP" ]]; then
          STRIDE=$(( EVT_TOTAL / EVT_CAP + 1 ))
        fi
      fi
      EVT_CSV=$(awk -v s="$STRIDE" '(NR-1) % s == 0 {print $1}' "$EVENT_FILE" | tr '\n' ' ')
      EVT_USED=$(echo "$EVT_CSV" | wc -w | tr -d ' ')
      # Gaussian sigma — wider bells for lower ratio (each event covers more).
      # `drops` mode uses a much wider sigma (~1.5s) so each drop reads as
      # a sustained climax accent rather than a brief bell.
      if [[ "$SHADER_GATE" == "drops" ]]; then
        SIGMA="1.5"
      else
        SIGMA=$(awk -v r="$SHADER_RATIO" 'BEGIN{printf "%.3f", 0.20 + r*0.30}')
      fi
      # Build sum: each gaussian = exp(-((T-t_i)/sigma)^2).  Cap at 1.0
      # via min() so opacity stays in [0,1].
      SUM_EXPR=""
      for t in $EVT_CSV; do
        term="exp(-((T-${t})/${SIGMA})*((T-${t})/${SIGMA}))"
        if [[ -z "$SUM_EXPR" ]]; then
          SUM_EXPR="$term"
        else
          SUM_EXPR="${SUM_EXPR}+${term}"
        fi
      done
      OPACITY_EXPR="min(1,${SUM_EXPR})"
      "$FFMPEG_BIN" -y -loglevel warning -stats \
        -i "$SRC" -i "$DST" \
        -filter_complex "[0:v][1:v]blend=all_expr='A*(1-(${OPACITY_EXPR}))+B*(${OPACITY_EXPR})'[v]" \
        -map "[v]" -map 0:a \
        -c:v libx264 -preset medium -crf 22 -c:a copy "$SHADER_FINAL_DST"
      echo "  [${SHADER_GATE}] $EVT_USED events fired (every ${STRIDE} of ${EVT_TOTAL}), σ=${SIGMA}s" >&2
    fi
  else
    "$FFMPEG_BIN" -y -loglevel warning -stats \
      -i "$SRC" -i "$DST" \
      -filter_complex "[0:v][1:v]blend=all_mode=normal:all_opacity=${SHADER_RATIO}[v]" \
      -map "[v]" -map 0:a \
      -c:v libx264 -preset medium -crf 22 -c:a copy "$SHADER_FINAL_DST"
  fi
  rm -f "$DST"
  DST="$SHADER_FINAL_DST"
fi

dur=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error -show_entries format=duration -of csv=p=0 "$DST" 2>/dev/null | awk '{printf "%.1f", $1}')
size=$(du -h "$DST" 2>/dev/null | awk '{print $1}')
echo "✓ ${EFFECT}: $DST (${dur}s, ${size}, ratio=${SHADER_RATIO})"
