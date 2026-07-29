#!/usr/bin/env bash
# verify-deploy.sh — 공개 URL 이 **지금 HEAD** 를 서빙하는지 확인한다.
#
# 계기 (2026-07-29): 로컬 게이트 18/18 PASS 인데 배포본이 9커밋 뒤처져 있었다.
# 그날 한 작업이 공개 URL 에 하나도 없었는데 아무 것도 실패하지 않았다 —
# 제출물 ①의 실체는 URL 이고, 로컬 검증은 URL 에 대해 아무 말도 하지 않기 때문이다.
#
# 같은 날 두 번째 함정: `gh run list --limit 1` 만 보면 **직전 커밋의 실행**이
# completed/success 로 떠서 "배포됐다"고 착각한다.  headSha 를 HEAD 와 비교해야 한다.
#
# usage:
#   bash skills/game-prototype/scripts/verify-deploy.sh
#   bash skills/game-prototype/scripts/verify-deploy.sh --wait     # 워크플로 완료까지 대기
set -euo pipefail

URL="https://melons.github.io/MelonS-Agents/play/"
WF="pages.yml"
WAIT=0
[[ "${1:-}" == "--wait" ]] && WAIT=1

HEAD_SHA=$(git rev-parse HEAD)
echo "HEAD        : ${HEAD_SHA:0:8}"

# site/play 가 HEAD 시점의 빌드를 담고 있는가 (커밋 누락 탐지)
LAST_PLAY=$(git log -1 --format=%H -- site/play)
echo "site/play   : ${LAST_PLAY:0:8}"
if [[ "$LAST_PLAY" != "$HEAD_SHA" ]]; then
  BEHIND=$(git rev-list --count "$LAST_PLAY..$HEAD_SHA")
  if [[ "$BEHIND" -gt 0 ]]; then
    echo "⚠ 배포본이 ${BEHIND}커밋 뒤처짐 — deploy-play.sh 를 돌리고 커밋해야 한다."
    echo "  (소스만 바뀌고 site/play 가 그대로면 공개 URL 은 옛 게임을 계속 보여준다)"
  fi
fi

run_field() { gh run list --workflow="$WF" --limit 1 --json "$1" -q ".[0].$1"; }

if [[ "$WAIT" == "1" ]]; then
  echo "Pages 워크플로가 HEAD 로 완료될 때까지 대기..."
  until [[ "$(run_field headSha)" == "$HEAD_SHA" && "$(run_field status)" == "completed" ]]; do
    sleep 20
  done
fi

RUN_SHA=$(run_field headSha); RUN_ST=$(run_field status); RUN_CC=$(run_field conclusion)
echo "Pages run   : ${RUN_SHA:0:8}  $RUN_ST/$RUN_CC"

if [[ "$RUN_SHA" != "$HEAD_SHA" ]]; then
  echo "✗ 최신 Pages 실행이 HEAD 가 아니다 — 아직 배포 안 됨 (--wait 로 대기 가능)"
  exit 1
fi
if [[ "$RUN_CC" != "success" ]]; then
  echo "✗ Pages 배포 실패: $RUN_CC"
  exit 1
fi

# 실제로 서빙되는지 (워크플로 성공 ≠ 페이지 응답)
CODE=$(curl -s -o /dev/null -w "%{http_code}" "$URL")
echo "GET $URL → $CODE"
[[ "$CODE" == "200" ]] || { echo "✗ 공개 URL 이 200 이 아니다"; exit 1; }

echo "✓ 공개 URL 이 HEAD(${HEAD_SHA:0:8}) 를 서빙 중"
