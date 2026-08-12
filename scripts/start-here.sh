#!/usr/bin/env bash
# start-here.sh — 처음 온 사람이 여는 단 하나의 문.
#
# 이 저장소에는 라인이 둘이고(쇼츠 제작 / 게임 제작) 에이전트가 27개다.
# 갈림길이 없으면 방문자는 README 183줄과 플러그인 목록 앞에서
# "그래서 뭘 해야 하지"로 끝난다.  이 스크립트는 질문 하나를 던지고
# 그 답에 맞는 경로로만 보낸다.
#
# 기존 scripts/first-touch.sh 는 손대지 않는다 — 그건 쇼츠 데모 마법사고,
# 여기서는 갈래가 정해진 뒤에 호출된다.
#
# 사용법:
#   bash scripts/start-here.sh          # 물어본다
#   bash scripts/start-here.sh shorts   # 바로 그 갈래로
#   bash scripts/start-here.sh game
#   bash scripts/start-here.sh graph
#   bash scripts/start-here.sh demo

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if [[ -t 1 ]]; then
  B=$'\033[1m'; C=$'\033[36m'; G=$'\033[32m'; DIM=$'\033[2m'; D=$'\033[0m'
else
  B=''; C=''; G=''; DIM=''; D=''
fi

say()  { printf '%s\n' "$*"; }
head2() { printf '\n%s%s%s\n' "$B" "$*" "$D"; }
cmd()  { printf '    %s%s%s\n' "$C" "$*" "$D"; }
note() { printf '    %s%s%s\n' "$DIM" "$*" "$D"; }

choice="${1:-}"

if [[ -z "$choice" ]]; then
  cat <<EOF

${B}MelonS-Agents${D} — 로컬에서 도는 멀티 에이전트 시스템

무엇을 하러 오셨나요?

  ${B}1${D}  영상 만들기      60초 세로 쇼츠 — 기획·생성·검수·법률 게이트까지
  ${B}2${D}  게임 만들기      콜로니 심 프로토타입 — 에이전트가 만들고 직접 플레이해 검증
  ${B}3${D}  파이프라인 보기   LangGraph 실행 그래프 — 게이트·재시도·재개 구조
  ${B}4${D}  그냥 구경        완성본 재생 (계정·API 키 불필요)

EOF
  printf '번호 (1-4): '
  read -r choice || choice=4
fi

case "$choice" in
  1|shorts|영상)
    head2 "① 영상 만들기 — 쇼츠 라인"
    say "  스틸을 만들고 자동 채점해서, 통과한 것만 영상화합니다."
    say "  영상화는 컷당 약 7분이라 그 앞에 문이 서 있습니다."
    head2 "먼저 환경 확인"
    cmd "bash scripts/doctor.sh"
    head2 "계정 없이 데모 한 편"
    cmd "bash scripts/first-touch.sh"
    head2 "그래프로 돌리기 (ComfyUI 필요)"
    cmd "python -m graph.shorts_graph run --spec graph/examples/shots.one.json --judge cli --thread ep12"
    note "→ 승인 대기(exit 3)에서 멈춥니다. 검수 시트를 보고:"
    cmd "python -m graph.shorts_graph resume --thread ep12 --approve"
    head2 "읽을 것"
    note "docs/generative-shorts-pipeline.md   제작 스테이지 전체"
    note "graph/README.ko.md                    게이트가 왜 거기 있는지"
    ;;

  2|game|게임)
    head2 "② 게임 만들기 — 프로토타입 라인"
    say "  PM이 작업을 발행하고, 코드·아트·사운드가 병렬로 돌고,"
    say "  Unity 빌드에서 직렬로 합류한 뒤 QA가 스크린샷으로 검증합니다."
    head2 "필요한 것"
    note "Unity 2022.3 이상 · Windows"
    head2 "시작"
    cmd "cat games/pawnsim/README.md"
    cmd "python skills/game-dev-agent/scripts/agent.py --help"
    head2 "읽을 것"
    note "skills/game-dev-agent/ARCHITECTURE.md   모듈 = 서브에이전트 구조"
    note ".claude/whiteboard.json                 병렬 레인 프로토콜"
    ;;

  3|graph|파이프라인)
    head2 "③ 파이프라인 보기 — LangGraph 실행 그래프"
    say "  그래프 정의가 곧 구조도입니다. 손으로 그린 게 아니라서 낡지 않습니다."
    head2 "설치"
    cmd "python -m venv .venv"
    cmd ".venv/Scripts/python -m pip install -r graph/requirements.txt   # Windows"
    cmd ".venv/bin/python     -m pip install -r graph/requirements.txt   # macOS/Linux"
    head2 "구조도 뽑기"
    cmd "python -m graph.shorts_graph diagram"
    head2 "모델 호출 0으로 배선만 확인"
    cmd "python -m graph.shorts_graph run --spec graph/examples/shots.example.json --mock --thread demo"
    head2 "읽을 것"
    note "graph/README.ko.md       왜 이런 모양인지 (3시간 vs 10초)"
    note "docs/langgraph-plan.md   단계별 계획과 실측치"
    ;;

  4|demo|구경|"")
    head2 "④ 그냥 구경 — 계정도 키도 필요 없음"
    cmd "bash scripts/first-touch.sh --check"
    note "→ 준비물만 점검합니다 (렌더 안 함)"
    head2 "완성본 보기"
    note "docs/demo/            데모 GIF·스크린샷"
    note "docs/samples/         샘플 산출물"
    head2 "무슨 시스템인지 30초 요약"
    note "로컬 도구(ffmpeg·whisper·ollama·ComfyUI)가 실제 작업을 하고,"
    note "에이전트는 순서·검수·게이트만 맡습니다. 런타임 API 비용 0."
    ;;

  *)
    say "모르는 선택입니다: $choice"
    say "1(영상) · 2(게임) · 3(파이프라인) · 4(구경) 중에서 고르세요."
    exit 64
    ;;
esac

printf '\n%s막히면:%s bash scripts/doctor.sh · docs/onboarding/\n\n' "$G" "$D"
