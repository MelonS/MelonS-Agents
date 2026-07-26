#!/usr/bin/env bash
# deploy-play.sh — 최신 WebGL 빌드를 site/play/ 로 복사해 GitHub Pages 배포 대상에 올린다.
#
# 배경: .github/workflows/pages.yml 은 main 브랜치의 site/** 변경에만 반응한다.
# 빌드 산출물(skills/game-prototype/builds/)은 gitignore 대상이라 그대로는 배포되지 않는다.
# 이 스크립트가 그 사이를 잇는다.
#
# 사용:  bash skills/game-prototype/scripts/deploy-play.sh [빌드폴더]
#        빌드폴더 생략 시 builds/day-*-webgl 중 **가장 최근 수정본**을 자동 선택.
#
# 주의: 날짜 스탬프 폴더를 하드코딩하지 말 것 — 자정을 넘기면 어제 빌드를 배포해
# "고쳤는데 반영이 안 된다"는 거짓 검증이 된다.  그래서 기본값이 자동 선택이다.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BUILDS="$REPO/skills/game-prototype/builds"
DEST="$REPO/site/play"

if [[ $# -ge 1 ]]; then
  SRC="$1"
else
  SRC="$(ls -dt "$BUILDS"/day-*-webgl 2>/dev/null | head -1 || true)"
fi

if [[ -z "${SRC:-}" || ! -f "$SRC/index.html" ]]; then
  echo "✗ WebGL 빌드를 못 찾았다 (찾은 경로: ${SRC:-없음})" >&2
  echo "  먼저 빌드: Unity.exe -batchmode -quit -nographics -projectPath ... \\" >&2
  echo "            -executeMethod MelonS.GameProto.EditorTools.BuildScript.BuildWebGL" >&2
  exit 2
fi

echo "빌드 원본 : $SRC"
echo "배포 대상 : $DEST"

rm -rf "$DEST"
mkdir -p "$DEST"
cp -r "$SRC"/. "$DEST"/

# Unity 기본 템플릿은 <title>Unity WebGL Player | ...</title> 를 쓴다.  면접·심사에서
# 탭 제목이 "Unity WebGL Player" 로 보이면 완성도 인상이 깎이므로 게임 이름으로 교체.
if [[ -f "$DEST/index.html" ]]; then
  python - "$DEST/index.html" <<'PY'
import re, sys, pathlib
p = pathlib.Path(sys.argv[1])
html = p.read_text(encoding="utf-8", errors="replace")
html = re.sub(r"<title>.*?</title>", "<title>PawnSim — 콜로니 심 프로토타입</title>",
              html, count=1, flags=re.S)
p.write_text(html, encoding="utf-8")
print("  index.html <title> 교체됨")
PY
fi

BYTES=$(du -sm "$DEST" | cut -f1)
echo "복사 완료: ${BYTES}MB"
echo
echo "다음: git add site/play && git commit && git push origin HEAD:main"
echo "      배포 URL → https://melons.github.io/MelonS-Agents/play/"
echo "      (Pages 워크플로는 main 의 site/** 변경에만 반응한다)"
