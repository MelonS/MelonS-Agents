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
    # 2026-07-31 — "몇 커밋 뒤처졌나"가 아니라 **빌드에 들어가는 것이 바뀌었나**를 본다.
    #  문서·로드맵·시나리오 JSON 만 커밋해도 커밋 수는 늘어난다.  그걸 "배포 필요"로
    #  경고하면 다음 사람은 (a) 의미 없는 재배포를 하거나 (b) 경고에 둔감해진다 —
    #  둘 다 나쁘다.  둔감해지는 쪽이 특히 위험하다: 진짜로 뒤처졌을 때 못 알아본다.
    #  게임 빌드에 실제로 들어가는 경로만 센다.
    GAME_PATHS=(
      "skills/game-prototype/unity-project/Assets"
      "skills/game-prototype/unity-project/ProjectSettings"
      "skills/game-prototype/unity-project/Packages"
      "skills/game-prototype/scripts/deploy-play.sh"
    )
    CHANGED=$(git diff --name-only "$LAST_PLAY" "$HEAD_SHA" -- "${GAME_PATHS[@]}" | head -20)
    if [[ -n "$CHANGED" ]]; then
      echo "⚠ 배포본이 ${BEHIND}커밋 뒤처짐 — 그중 **게임 소스가 바뀌었다**:"
      echo "$CHANGED" | sed 's/^/    /'
      echo "  deploy-play.sh 를 돌리고 커밋할 것."
      echo "  (소스만 바뀌고 site/play 가 그대로면 공개 URL 은 옛 게임을 계속 보여준다)"
    else
      echo "· 배포본이 ${BEHIND}커밋 뒤처졌지만 게임 소스 변경은 없다 (문서/시나리오만) — 재배포 불필요."
    fi
  fi
fi

run_field() { gh run list --workflow="$WF" --limit 1 --json "$1" -q ".[0].$1"; }

# 배포가 반영됐는지 판단할 **기준 커밋**.
#  Pages 워크플로는 `site/**` 변경에만 반응한다.  그래서 문서·시나리오만 커밋하면
#  워크플로가 애초에 돌지 않고, HEAD 와 비교하면 영원히 ✗ 가 뜬다 — 그게 반복되면
#  경고에 둔감해지고, 진짜로 배포가 밀렸을 때 못 알아본다.
#  기준은 "site/play 를 마지막으로 바꾼 커밋"이다.  그 커밋이 배포됐으면 공개 URL 은
#  최신 빌드를 서빙하고 있는 것이 맞다 (게임 소스 변경 여부는 위 블록이 따로 본다).
TARGET_SHA="$LAST_PLAY"
[[ -z "$TARGET_SHA" ]] && TARGET_SHA="$HEAD_SHA"

if [[ "$WAIT" == "1" ]]; then
  echo "Pages 워크플로가 ${TARGET_SHA:0:8} 로 완료될 때까지 대기..."
  until [[ "$(run_field headSha)" == "$TARGET_SHA" && "$(run_field status)" == "completed" ]]; do
    sleep 20
  done
fi

RUN_SHA=$(run_field headSha); RUN_ST=$(run_field status); RUN_CC=$(run_field conclusion)
echo "Pages run   : ${RUN_SHA:0:8}  $RUN_ST/$RUN_CC"

if [[ "$RUN_SHA" != "$TARGET_SHA" ]]; then
  echo "✗ 최신 Pages 실행이 배포 대상(${TARGET_SHA:0:8}) 이 아니다 — 아직 배포 안 됨 (--wait 로 대기 가능)"
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

echo "✓ 공개 URL 이 최신 빌드(${TARGET_SHA:0:8}) 를 서빙 중"
