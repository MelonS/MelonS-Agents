#!/usr/bin/env bash
# Bootstrap & health check for a fresh clone.
#
# What it does, in order:
#   1. Ensure .env exists (auto-copies from .env.example if missing)
#   2. Verify every required CLI tool is installed (ffmpeg / ffprobe /
#      whisper-cli / ollama / yt-dlp) and print an OS-specific install
#      command for anything that's not.
#   3. Check that ffmpeg was built with libass (required for caption
#      burn-in).
#   4. Auto-download the whisper.cpp model if $WHISPER_MODEL is missing.
#   5. Verify the local Ollama server is reachable, and pull the
#      highlight model if it isn't already present.
#   6. Generate two synthetic test fixtures on macOS via `say`; skip
#      gracefully on other OSes and point at scripts/fetch-fixtures.sh.
#
# Exits non-zero if a mandatory tool is missing; prints actionable
# install hints alongside each failure.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# --- 1. .env present ----------------------------------------------------
if [[ ! -f .env ]]; then
  cp .env.example .env
  echo "✅ created .env from .env.example  (edit it later if you need to override defaults)"
fi

# Source env.sh — gives us `command -v` fallback for all *_BIN vars.
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh"

OS="$(uname -s)"

print_install_hint() {
  local tool="$1"
  case "$OS:$tool" in
    Darwin:ffmpeg|Darwin:ffprobe)
      echo "  → brew install ffmpeg-full  (the regular 'ffmpeg' formula no longer ships with libass)"
      echo "    env.sh auto-discovers /opt/homebrew/opt/ffmpeg-full/bin/ffmpeg — no .env edit needed" ;;
    Darwin:whisper-cli)
      echo "  → brew install whisper-cpp  (installs the whisper-cli binary)" ;;
    Darwin:ollama)
      echo "  → brew install ollama  or  https://ollama.com/download" ;;
    Darwin:yt-dlp)
      echo "  → brew install yt-dlp" ;;
    Darwin:aubiotrack|Darwin:aubioonset)
      echo "  → brew install aubio  (installs both aubiotrack and aubioonset — required by music-video mission for beat + drum-onset detection)" ;;
    Darwin:jq)
      echo "  → brew install jq  (required by metrics + Pexels JSON parsing)" ;;
    Linux:ffmpeg|Linux:ffprobe)
      echo "  → sudo apt install ffmpeg   (Debian/Ubuntu; the package includes libass)" ;;
    Linux:whisper-cli)
      echo "  → build from source: https://github.com/ggerganov/whisper.cpp" ;;
    Linux:ollama)
      echo "  → curl -fsSL https://ollama.com/install.sh | sh" ;;
    Linux:yt-dlp)
      echo "  → sudo apt install yt-dlp   or   pipx install yt-dlp" ;;
    Linux:aubiotrack|Linux:aubioonset)
      echo "  → sudo apt install aubio-tools  (Debian/Ubuntu)" ;;
    Linux:jq)
      echo "  → sudo apt install jq" ;;
    *)
      echo "  → install $tool from its upstream docs" ;;
  esac
}

# --- 2 + 3. tool versions + libass --------------------------------------
echo "=== tool versions ==="
missing=()
# AUBIO_TRACK_BIN / AUBIO_ONSET_BIN are looked up via PATH if not set in .env
: "${AUBIO_TRACK_BIN:=$(command -v aubiotrack || true)}"
: "${AUBIO_ONSET_BIN:=$(command -v aubioonset || true)}"
: "${JQ_BIN:=$(command -v jq || true)}"
for entry in "FFMPEG_BIN:ffmpeg" "FFPROBE_BIN:ffprobe" "WHISPER_CLI_BIN:whisper-cli" "OLLAMA_BIN:ollama" "YT_DLP_BIN:yt-dlp" "AUBIO_TRACK_BIN:aubiotrack" "AUBIO_ONSET_BIN:aubioonset" "JQ_BIN:jq"; do
  var="${entry%%:*}"
  tool="${entry##*:}"
  bin="${!var:-}"
  if [[ -z "$bin" ]]; then
    echo "❌ $tool — not found on PATH (\$$var resolves to empty)"
    print_install_hint "$tool"
    missing+=("$tool")
    continue
  fi
  if [[ ! -x "$bin" ]]; then
    echo "❌ $tool — $bin not executable"
    print_install_hint "$tool"
    missing+=("$tool")
    continue
  fi
  case "$tool" in
    ffmpeg)
      printf "✅ %-12s " "$tool"
      "$bin" -version | head -1
      if ! "$bin" -version 2>&1 | grep -q -- "libass"; then
        echo "  ⚠ this ffmpeg lacks libass — caption burn-in will fail."
        echo "  macOS fix:  brew install ffmpeg-full"
        echo "              (env.sh auto-discovers /opt/homebrew/opt/ffmpeg-full/bin/ffmpeg"
        echo "               on next run — no .env edit needed)"
        echo "  Linux fix:  install a build with libass; on most distros 'apt install ffmpeg'"
        echo "              includes it.  Or build from source --enable-libass."
        missing+=("ffmpeg-libass")
      fi
      ;;
    ffprobe)
      printf "✅ %-12s " "$tool"
      "$bin" -version | head -1
      ;;
    whisper-cli)
      printf "✅ %-12s " "$tool"
      ("$bin" --help 2>&1 | head -1) || true
      ;;
    ollama)
      printf "✅ %-12s " "$tool"
      "$bin" --version 2>&1 | head -1
      ;;
    yt-dlp)
      printf "✅ %-12s " "$tool"
      "$bin" --version 2>&1 | head -1
      ;;
    aubiotrack|aubioonset)
      # aubio CLIs don't support --version; print "installed at $bin" instead
      printf "✅ %-12s installed at %s\n" "$tool" "$bin"
      ;;
    jq)
      printf "✅ %-12s " "$tool"
      ("$bin" --version 2>&1 | head -1) || true
      ;;
  esac
done

# --- 4. whisper model auto-fetch ---------------------------------------
echo
echo "=== whisper.cpp model ==="
if [[ -f "$WHISPER_MODEL" ]]; then
  size_mb=$(( $(wc -c < "$WHISPER_MODEL" | tr -d ' ') / 1024 / 1024 ))
  echo "✅ $WHISPER_MODEL (${size_mb} MB)"
else
  echo "↓ $WHISPER_MODEL not found — running scripts/fetch-whisper-model.sh"
  if ! "$REPO_ROOT/scripts/fetch-whisper-model.sh"; then
    echo "❌ whisper model download failed"
    missing+=("whisper-model")
  fi
fi

# --- 5. ollama server + required models --------------------------------
echo
echo "=== ollama server ==="
if curl --silent --fail --max-time 2 "${OLLAMA_HOST}/api/tags" >/dev/null 2>&1; then
  echo "✅ reachable at $OLLAMA_HOST"

  echo
  echo "=== ollama models ==="
  if [[ -n "${OLLAMA_BIN:-}" && -x "$OLLAMA_BIN" ]]; then
    for m in "$OLLAMA_MODEL_HIGHLIGHT"; do
      if "$OLLAMA_BIN" list 2>/dev/null | awk 'NR>1{print $1}' | grep -qx "$m"; then
        echo "✅ $m pulled"
      else
        echo "↓ $m not pulled — running \`ollama pull $m\`"
        if ! "$OLLAMA_BIN" pull "$m"; then
          echo "  ⚠ pull failed; you may need to retry: ollama pull $m"
        fi
      fi
    done
  fi
else
  echo "⚠ no response at $OLLAMA_HOST"
  echo "  Start the server in a separate terminal:  ollama serve"
  echo "  or install + launch the desktop app: https://ollama.com/download"
fi

# --- records dir --------------------------------------------------------
echo
echo "=== records dir ==="
mkdir -p "$RECORDS_DIR"
echo "✅ $RECORDS_DIR (writable: $([[ -w "$RECORDS_DIR" ]] && echo yes || echo no))"

# --- music-video specific checks ---------------------------------------
echo
echo "=== music-video mission readiness ==="
if [[ -z "${PEXELS_API_KEY:-}" ]]; then
  echo "⚠ PEXELS_API_KEY is not set in .env"
  echo "  → free tier: sign up at https://www.pexels.com/api/  (200 req/hour, plenty for personal use)"
  echo "  → then set: PEXELS_API_KEY=<your_key>  in .env"
else
  echo "✅ PEXELS_API_KEY is set (length=${#PEXELS_API_KEY})"
fi

music_count=$(find assets/music -maxdepth 1 -type f \( -name '*.mp3' -o -name '*.wav' -o -name '*.m4a' -o -name '*.flac' -o -name '*.ogg' \) 2>/dev/null | wc -l | tr -d ' ')
if [[ "$music_count" -eq 0 ]]; then
  echo "⚠ assets/music/ has no audio files yet"
  echo "  → the music-video mission needs an mp3/wav/m4a/flac/ogg input"
  echo "  → operator generates tracks on Suno (free tier), Pixabay Music, YT Audio Library, etc."
  echo "  → drop the file in assets/music/ (gitignored — never committed)"
  echo "  → see assets/music/README.md for the source shortlist and assets/music/SOURCES.md for the per-track license trail"
else
  echo "✅ assets/music/ has $music_count audio file(s) ready"
fi

echo
echo "=== autonomy mode ==="
echo "AUTONOMY_MODE=${AUTONOMY_MODE:-false}  budget=\$${AUTONOMY_BUDGET_USD:-0}"

# --- 6. Synthetic test fixtures (macOS only) ---------------------------
echo
echo "=== synthetic test fixtures ==="
FIXTURE_DIR="${FIXTURE_DIR:-/tmp/smoke}"
mkdir -p "$FIXTURE_DIR"

if [[ "$OS" != "Darwin" ]]; then
  echo "ℹ  skipping macOS-only \`say\`-based synthetic fixtures."
  echo "   For a real CC video to test against, run:"
  echo "     scripts/fetch-fixtures.sh"
  echo "   then pass the resulting file to a mission, e.g.:"
  echo "     ./agents/missions/highlight/run.sh /tmp/smoke/sintel_1080p.mp4"
else
  gen_fixture() {
    local out="$1" voice="$2" script_text="$3"
    if [[ -f "$out" ]]; then
      echo "✅ exists: $out ($(du -h "$out" | awk '{print $1}'))"
      return
    fi
    if ! command -v say >/dev/null 2>&1; then
      echo "⚠ macOS \`say\` not available; skipping $out"
      return
    fi
    local tmp_aiff tmp_wav
    tmp_aiff="$(mktemp -t tts).aiff"
    tmp_wav="$(mktemp -t tts).wav"
    printf '%s\n' "$script_text" | say -v "$voice" -r 175 -o "$tmp_aiff"
    "$FFMPEG_BIN" -y -loglevel error -i "$tmp_aiff" -ar 44100 "$tmp_wav"
    local dur
    dur=$("$FFPROBE_BIN" -v error -show_entries format=duration -of default=nw=1:nk=1 "$tmp_wav" | awk '{printf "%.0f",$1}')
    "$FFMPEG_BIN" -y -loglevel error -f lavfi -i "testsrc2=duration=${dur}:size=1920x1080:rate=24" \
      -i "$tmp_wav" -c:v libx264 -preset ultrafast -crf 28 -c:a aac -shortest "$out"
    rm -f "$tmp_aiff" "$tmp_wav"
    echo "✅ created: $out (${dur}s)"
  }

  EN_SCRIPT='Hello everyone, and thanks for joining today. Before I get into the main topic, let me give you a quick overview of what we will cover. We will talk about productivity, focus, and how habits beat motivation. I have been working on these ideas for years and I want to share what I have learned. Let me start with a short story. About a decade ago, I was completely overwhelmed. I had three deadlines, two side projects, and a calendar that looked like Tetris on the hardest difficulty. I was tired all the time. I drank too much coffee and slept too little. Now here is the first lesson, and this is the one that changed everything for me. Most failures look obvious in hindsight, but at the time they feel like brilliant ideas. The way to spot a bad idea early is to ask one friend who disagrees with you, and then actually listen to what they say. Do not argue, do not defend, just listen. You will catch yourself before the cliff. The second lesson is about habits. Habits beat motivation every single time. Motivation is a feeling. Habits are a system. If you want to change your life, build a system you can run on a Tuesday morning in February when it is cold and raining and your brain wants nothing more than to stay in bed. I will give you a concrete example. For two years I wrote three sentences every morning before checking my phone. Not three paragraphs, not three pages, three sentences. The bar was low enough that I could never make an excuse to skip it. And after two years I had written enough material for a small book. The third lesson is about who you spend your time with. Choose your peers more carefully than your problems. You will absorb the thinking patterns of the five people you spend the most time with. So make sure those five people are people you would actually want to become. Those are the three lessons. Pick one, work on it for ninety days, and you will be a different person. Thank you for listening.'

  KO_SCRIPT='안녕하세요 여러분. 오늘은 짧지만 핵심적인 이야기를 세 가지 나누고 싶습니다. 그 전에 간단히 오늘 다룰 내용을 정리해 드리겠습니다. 생산성, 집중, 그리고 습관이 어떻게 동기를 이기는지에 대한 이야기입니다. 십여 년 동안 고민해 온 주제이고 제가 직접 겪은 경험을 토대로 말씀드리겠습니다. 먼저 짧은 이야기 하나로 시작하겠습니다. 약 십 년 전에 저는 완전히 압도되어 있었습니다. 세 개의 마감, 두 개의 사이드 프로젝트, 그리고 가장 어려운 난이도의 테트리스 같은 일정표를 가지고 있었죠. 항상 피곤했고 커피는 너무 많이 마셨고 잠은 너무 적게 잤습니다. 첫 번째 교훈은 이렇습니다. 대부분의 실패는 돌이켜보면 뻔해 보이지만 그 순간에는 천재적인 아이디어처럼 느껴집니다. 그래서 나쁜 아이디어를 빨리 알아채려면 당신과 의견이 다른 친구에게 물어보고 그 친구의 말을 끝까지 듣는 것이 가장 효과적입니다. 변명하지 말고, 방어하지 말고, 그냥 들으세요. 두 번째 교훈은 습관에 관한 것입니다. 동기보다 습관이 항상 이깁니다. 동기는 감정이고 습관은 시스템입니다. 인생을 바꾸고 싶다면 비가 오는 이월의 화요일 아침에도 작동하는 시스템을 만드세요. 구체적인 예를 드리겠습니다. 저는 이 년 동안 매일 아침 휴대폰을 보기 전에 세 문장을 썼습니다. 세 단락이나 세 페이지가 아니라 세 문장. 핑계를 댈 수 없을 만큼 낮은 기준이었죠. 그렇게 이 년이 지나니 작은 책 한 권 분량의 글이 쌓였습니다. 세 번째 교훈은 함께하는 사람에 관한 것입니다. 문제보다 사람을 더 신중하게 골라야 합니다. 가장 많이 시간을 보내는 다섯 명의 생각 방식을 당신은 결국 흡수합니다. 그러니 그 다섯 명이 당신이 되고 싶은 사람들인지 확인하세요. 오늘 세 가지를 들었으니 하나만 골라서 구십일 동안 매일 실천해 보세요. 들어주셔서 감사합니다.'

  gen_fixture "$FIXTURE_DIR/lecture.mp4"    Samantha "$EN_SCRIPT"
  gen_fixture "$FIXTURE_DIR/ko_lecture.mp4" Yuna     "$KO_SCRIPT"
fi

# --- final verdict ------------------------------------------------------
echo
if (( ${#missing[@]} > 0 )); then
  echo "❌ bootstrap finished with missing pieces: ${missing[*]}"
  echo "   Install them using the hints above, then re-run scripts/bootstrap.sh."
  exit 1
fi
echo "✅ bootstrap ok"
echo
echo "Next steps:"
echo ""
echo "  [music-video, the current showcase]"
echo "  1) drop a music file into assets/music/"
echo "     (free options: Suno, Pixabay Music, YouTube Audio Library, FMA"
echo "      — see assets/music/README.md)"
echo "  2) ./agents/missions/music-video/run.sh upload1 \"assets/music/<your_track>.mp3\""
echo "  3) ./scripts/music-video-shaders.sh combo \\"
echo "       records/missions/\$(date +%Y-%m-%d)/music-video-upload1-*/outputs/short.mp4 \\"
echo "       outputs/publish/my-first-short.mp4"
echo ""
echo "  [v1 highlight baseline — works without a music file]"
echo "  ./agents/missions/highlight/run.sh https://download.blender.org/durian/trailer/sintel_trailer-1080p.mp4"
if [[ "$OS" == "Darwin" && -f "$FIXTURE_DIR/lecture.mp4" ]]; then
  echo "  ./agents/missions/highlight/run.sh $FIXTURE_DIR/lecture.mp4"
fi
