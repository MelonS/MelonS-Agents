#!/usr/bin/env bash
# Bootstrap & health check + synthetic fixture generation.
# Verifies tool versions, env wiring, and produces test fixtures
# (English + Korean synthetic lectures) under /tmp/smoke/.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if [[ ! -f .env ]]; then
  echo "❌ .env missing. Run: cp .env.example .env && \$EDITOR .env"
  exit 1
fi

set -a
# shellcheck disable=SC1091
source .env
set +a

echo "=== tool versions ==="
for var in FFMPEG_BIN OLLAMA_BIN; do
  bin="${!var:-}"
  if [[ -z "$bin" ]]; then echo "⚠  $var unset"; continue; fi
  if [[ ! -x "$bin" ]]; then echo "❌ $var=$bin (not executable)"; continue; fi
  printf "✅ %-12s " "$var"
  case "$var" in
    FFMPEG_BIN)  "$bin" -version | head -1 ;;
    OLLAMA_BIN)  "$bin" --version ;;
  esac
done

echo
echo "=== records dir ==="
RECORDS_DIR="${RECORDS_DIR:-./records}"
mkdir -p "$RECORDS_DIR"
echo "✅ $RECORDS_DIR (writable: $([[ -w $RECORDS_DIR ]] && echo yes || echo no))"

echo
echo "=== autonomy mode ==="
echo "AUTONOMY_MODE=${AUTONOMY_MODE:-false}  budget=\$${AUTONOMY_BUDGET_USD:-0}"

# Fixtures
echo
echo "=== synthetic test fixtures ==="
FIXTURE_DIR="${FIXTURE_DIR:-/tmp/smoke}"
mkdir -p "$FIXTURE_DIR"
gen_fixture() {
  local out="$1" voice="$2" script_text="$3"
  if [[ -f "$out" ]]; then
    echo "✅ exists: $out ($(du -h "$out" | awk '{print $1}'))"
    return
  fi
  # macOS `say` is the easiest local TTS.
  if ! command -v say >/dev/null 2>&1; then
    echo "⚠ macOS `say` not available; skipping $out"
    return
  fi
  local tmp_aiff; tmp_aiff="$(mktemp -t tts).aiff"
  local tmp_wav;  tmp_wav="$(mktemp -t tts).wav"
  printf '%s\n' "$script_text" | say -v "$voice" -r 175 -o "$tmp_aiff"
  "$FFMPEG_BIN" -y -loglevel error -i "$tmp_aiff" -ar 44100 "$tmp_wav"
  local dur; dur=$(ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 "$tmp_wav" | awk '{printf "%.0f",$1}')
  "$FFMPEG_BIN" -y -loglevel error -f lavfi -i "testsrc2=duration=${dur}:size=1920x1080:rate=24" \
    -i "$tmp_wav" -c:v libx264 -preset ultrafast -crf 28 -c:a aac -shortest "$out"
  rm -f "$tmp_aiff" "$tmp_wav"
  echo "✅ created: $out (${dur}s)"
}

EN_SCRIPT='Hello everyone, and thanks for joining today. Before I get into the main topic, let me give you a quick overview of what we will cover. We will talk about productivity, focus, and how habits beat motivation. I have been working on these ideas for years and I want to share what I have learned. Let me start with a short story. About a decade ago, I was completely overwhelmed. I had three deadlines, two side projects, and a calendar that looked like Tetris on the hardest difficulty. I was tired all the time. I drank too much coffee and slept too little. Now here is the first lesson, and this is the one that changed everything for me. Most failures look obvious in hindsight, but at the time they feel like brilliant ideas. The way to spot a bad idea early is to ask one friend who disagrees with you, and then actually listen to what they say. Do not argue, do not defend, just listen. You will catch yourself before the cliff. The second lesson is about habits. Habits beat motivation every single time. Motivation is a feeling. Habits are a system. If you want to change your life, build a system you can run on a Tuesday morning in February when it is cold and raining and your brain wants nothing more than to stay in bed. I will give you a concrete example. For two years I wrote three sentences every morning before checking my phone. Not three paragraphs, not three pages, three sentences. The bar was low enough that I could never make an excuse to skip it. And after two years I had written enough material for a small book. The third lesson is about who you spend your time with. Choose your peers more carefully than your problems. You will absorb the thinking patterns of the five people you spend the most time with. So make sure those five people are people you would actually want to become. Those are the three lessons. Pick one, work on it for ninety days, and you will be a different person. Thank you for listening.'

KO_SCRIPT='안녕하세요 여러분. 오늘은 짧지만 핵심적인 이야기를 세 가지 나누고 싶습니다. 그 전에 간단히 오늘 다룰 내용을 정리해 드리겠습니다. 생산성, 집중, 그리고 습관이 어떻게 동기를 이기는지에 대한 이야기입니다. 십여 년 동안 고민해 온 주제이고 제가 직접 겪은 경험을 토대로 말씀드리겠습니다. 먼저 짧은 이야기 하나로 시작하겠습니다. 약 십 년 전에 저는 완전히 압도되어 있었습니다. 세 개의 마감, 두 개의 사이드 프로젝트, 그리고 가장 어려운 난이도의 테트리스 같은 일정표를 가지고 있었죠. 항상 피곤했고 커피는 너무 많이 마셨고 잠은 너무 적게 잤습니다. 첫 번째 교훈은 이렇습니다. 대부분의 실패는 돌이켜보면 뻔해 보이지만 그 순간에는 천재적인 아이디어처럼 느껴집니다. 그래서 나쁜 아이디어를 빨리 알아채려면 당신과 의견이 다른 친구에게 물어보고 그 친구의 말을 끝까지 듣는 것이 가장 효과적입니다. 변명하지 말고, 방어하지 말고, 그냥 들으세요. 두 번째 교훈은 습관에 관한 것입니다. 동기보다 습관이 항상 이깁니다. 동기는 감정이고 습관은 시스템입니다. 인생을 바꾸고 싶다면 비가 오는 이월의 화요일 아침에도 작동하는 시스템을 만드세요. 구체적인 예를 드리겠습니다. 저는 이 년 동안 매일 아침 휴대폰을 보기 전에 세 문장을 썼습니다. 세 단락이나 세 페이지가 아니라 세 문장. 핑계를 댈 수 없을 만큼 낮은 기준이었죠. 그렇게 이 년이 지나니 작은 책 한 권 분량의 글이 쌓였습니다. 세 번째 교훈은 함께하는 사람에 관한 것입니다. 문제보다 사람을 더 신중하게 골라야 합니다. 가장 많이 시간을 보내는 다섯 명의 생각 방식을 당신은 결국 흡수합니다. 그러니 그 다섯 명이 당신이 되고 싶은 사람들인지 확인하세요. 오늘 세 가지를 들었으니 하나만 골라서 구십일 동안 매일 실천해 보세요. 들어주셔서 감사합니다.'

gen_fixture "$FIXTURE_DIR/lecture.mp4"    Samantha "$EN_SCRIPT"
gen_fixture "$FIXTURE_DIR/ko_lecture.mp4" Yuna     "$KO_SCRIPT"

echo
echo "✅ bootstrap ok"
echo "Next: ./agents/missions/highlight/run.sh $FIXTURE_DIR/lecture.mp4"
