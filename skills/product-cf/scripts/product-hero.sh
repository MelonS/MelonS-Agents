#!/usr/bin/env bash
# product-hero.sh — turn ONE product photo into a CF-style hero clip.
#
# The product is NEVER regenerated.  rembg cuts it out, then the camera
# moves around it (2.5D push-in) and a specular highlight sweeps the
# label.  Output is a 1080x1920 clip with the product label pixel-intact.
#
# Pipeline (sequential ffmpeg passes — each independently debuggable):
#   A. rembg            → product cutout PNG (alpha)
#   B. center+scale     → product on a transparent 1080x1920 canvas
#   C. background       → studio gradient (or supplied bg image), slow zoom
#   D. light-sweep      → moving gaussian highlight, grayscale clip
#   E. product motion   → push-in with alpha preserved + sweep screen-blended,
#                         masked back to the product alpha (glow never spills)
#   F. composite        → product over background + grain/vignette to match
#                         the music-video look
#
# Usage:
#   product-hero.sh <product_image> <out_clip.mp4> [duration_sec] [bg_image]
#
# Env:
#   PRODUCT_CF_VENV   rembg venv (default ~/.cache/melons-ai/venvs/product-cf)
#   FFMPEG_BIN        default ffmpeg
#   HERO_PRODUCT_W    product width as fraction of 1080 (default 0.66)
#   HERO_ZOOM         total push-in zoom factor (default 1.12)
#   HERO_SWEEP        1=label light-sweep on (default 1), 0=off
set -euo pipefail

PRODUCT_IMG="${1:?usage: product-hero.sh <product_image> <out_clip.mp4> [dur] [bg_image]}"
OUT="${2:?missing out_clip.mp4}"
DUR="${3:-4}"
BG_IMG="${4:-}"

FFMPEG_BIN="${FFMPEG_BIN:-ffmpeg}"
VENV="${PRODUCT_CF_VENV:-$HOME/.cache/melons-ai/venvs/product-cf}"
PRODUCT_W="${HERO_PRODUCT_W:-0.66}"
ZOOM="${HERO_ZOOM:-1.12}"
SWEEP="${HERO_SWEEP:-1}"

W=1080; H=1920; FPS=30
FRAMES=$(awk -v d="$DUR" -v f="$FPS" 'BEGIN{printf "%d", d*f}')
PW=$(awk -v w="$W" -v f="$PRODUCT_W" 'BEGIN{printf "%d", w*f}')

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
log(){ printf '[hero] %s\n' "$*" >&2; }

# ── A. cutout ───────────────────────────────────────────────────────
log "A/F  rembg cutout"
CUT="$TMP/cut.png"
if [[ -x "$VENV/bin/python" ]]; then
  "$VENV/bin/python" - "$PRODUCT_IMG" "$CUT" <<'PY'
import sys
from rembg import remove
from PIL import Image
inp, out = sys.argv[1], sys.argv[2]
img = Image.open(inp).convert("RGBA")
res = remove(img)            # returns RGBA with background removed
bbox = res.getbbox()         # tight crop to the product (drop transparent margins)
if bbox:
    res = res.crop(bbox)
res.save(out)
PY
else
  log "WARN: no rembg venv — assuming input already has alpha"
  cp "$PRODUCT_IMG" "$CUT"
fi

# ── B. center + scale + reflection onto transparent canvas ──────────
# Fit the product into a PW×MAXH box (so a tall can never overflows), anchor
# it a touch above centre, and lay a low-opacity vflipped reflection beneath
# it for grounding (the classic product-shot tell).
MAXH=$(awk -v h="$H" 'BEGIN{printf "%d", h*0.70}')
YOFF=100  # product sits this many px above exact centre, leaving room for reflection
log "B/F  center+scale product (fit ${PW}x${MAXH}) + reflection"
CANVAS="$TMP/canvas.png"
"$FFMPEG_BIN" -y -loglevel error -i "$CUT" -filter_complex "
  color=0x00000000:s=${W}x${H},format=rgba[bg];
  [0:v]format=rgba,scale=${PW}:${MAXH}:force_original_aspect_ratio=decrease[p];
  [p]split=2[pm][pr];
  [pr]vflip,colorchannelmixer=aa=0.16[prf];
  [bg][prf]overlay=x=(W-w)/2:y=(H+h)/2-${YOFF}:format=auto[b1];
  [b1][pm]overlay=x=(W-w)/2:y=(H-h)/2-${YOFF}:format=auto[o]
" -map "[o]" -frames:v 1 "$CANVAS"

# ── C. background (studio gradient or supplied image), slow zoom ─────
# Build a STILL bg frame first, then a single loop+zoompan pass.  (zoompan's
# d= is frames-per-INPUT-frame, so feeding it a multi-frame clip explodes
# the frame count — always feed a single still.)
log "C/F  background motion"
BG="$TMP/bg.mp4"; BGSTILL="$TMP/bgstill.png"
if [[ -n "$BG_IMG" && -f "$BG_IMG" ]]; then
  "$FFMPEG_BIN" -y -loglevel error -i "$BG_IMG" \
    -vf "scale=${W}:${H}:force_original_aspect_ratio=increase,crop=${W}:${H}" -frames:v 1 "$BGSTILL"
else
  # soft studio backdrop: dark vertical gradient
  "$FFMPEG_BIN" -y -loglevel error -f lavfi \
    -i "gradients=s=${W}x${H}:c0=0x0c1014:c1=0x1c242c:x0=540:y0=420:x1=540:y1=1920:nb_colors=2" \
    -frames:v 1 "$BGSTILL"
fi
# Feed exactly ONE still frame; zoompan's d= generates all output frames.
# (Do NOT use -loop 1 -t here — that yields many input frames and zoompan
#  emits d frames PER input frame, exploding the count.)
"$FFMPEG_BIN" -y -loglevel error -i "$BGSTILL" \
  -vf "zoompan=z='min(zoom+0.0005,1.06)':d=${FRAMES}:s=${W}x${H}:fps=${FPS},format=yuv420p" \
  -an "$BG"

# ── D. light-sweep (grayscale moving gaussian band) ─────────────────
# geq is per-pixel and slow; render at half-res and upscale (a soft glow
# loses nothing).  W/H below are geq's own builtins (= 540x960 here).
SWEEP_CLIP="$TMP/sweep.mp4"
if [[ "$SWEEP" == "1" ]]; then
  log "D/F  label light-sweep"
  "$FFMPEG_BIN" -y -loglevel error -f lavfi -i "color=c=black:s=540x960:r=${FPS}:d=${DUR}" \
    -vf "geq=lum='200*exp(-pow((X-(W*(mod(T*0.55,1.7)-0.35)))/(W*0.09)\,2))':cb=128:cr=128,scale=${W}:${H}" \
    -pix_fmt yuv420p "$SWEEP_CLIP"
fi

# ── E. product motion: push-in (alpha-preserved) + sweep screen ─────
log "E/F  product push-in + sweep"
PRODZ="$TMP/prodz.mov"
ZSTEP=$(awk -v z="$ZOOM" -v fr="$FRAMES" 'BEGIN{printf "%.6f", (z-1)/fr}')
if [[ "$SWEEP" == "1" ]]; then
  "$FFMPEG_BIN" -y -loglevel error -i "$CANVAS" -i "$SWEEP_CLIP" -filter_complex "
    [0:v]format=rgba,split=2[c1][c2];
    [c1]alphaextract,zoompan=z='min(zoom+${ZSTEP},${ZOOM})':d=${FRAMES}:s=${W}x${H}:fps=${FPS}[ma];
    [c2]zoompan=z='min(zoom+${ZSTEP},${ZOOM})':d=${FRAMES}:s=${W}x${H}:fps=${FPS}[mc];
    [mc][1:v]blend=all_mode=screen:all_opacity=0.30[ml];
    [ml][ma]alphamerge[pz]
  " -map "[pz]" -c:v qtrle -an "$PRODZ"
else
  "$FFMPEG_BIN" -y -loglevel error -i "$CANVAS" -filter_complex "
    [0:v]format=rgba,split=2[c1][c2];
    [c1]alphaextract,zoompan=z='min(zoom+${ZSTEP},${ZOOM})':d=${FRAMES}:s=${W}x${H}:fps=${FPS}[ma];
    [c2]zoompan=z='min(zoom+${ZSTEP},${ZOOM})':d=${FRAMES}:s=${W}x${H}:fps=${FPS}[mc];
    [mc][ma]alphamerge[pz]
  " -map "[pz]" -c:v qtrle -an "$PRODZ"
fi

# ── F. composite product over bg + match-look grain/vignette ────────
log "F/F  composite + grain/vignette"
mkdir -p "$(dirname "$OUT")"
"$FFMPEG_BIN" -y -loglevel error -i "$BG" -i "$PRODZ" -filter_complex "
  [0:v][1:v]overlay=0:0:format=auto[c];
  [c]vignette=PI/5,noise=alls=6:allf=t+u[v]
" -map "[v]" -t "$DUR" -r "$FPS" -pix_fmt yuv420p -c:v libx264 -crf 18 "$OUT"

log "done → $OUT"
