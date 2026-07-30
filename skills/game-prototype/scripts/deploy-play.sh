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

# 2026-07-31 — 디렉터리 **자체**를 지우지 않는다.  Windows 에서는 탐색기·에디터·백신이
#  폴더 핸들을 잡고 있으면 `rm -rf "$DEST"` 가 "Device or resource busy" 로 실패하는데,
#  그때 내용물은 이미 지워진 뒤라 **배포본이 빈 채로 남는다**(실측으로 한 번 당했다).
#  내용만 비우면 핸들이 잡혀 있어도 안전하고, 결과는 동일하다.
mkdir -p "$DEST"
find "$DEST" -mindepth 1 -delete 2>/dev/null || true
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
print("  index.html <title> 교체됨")

# ── 캔버스가 브라우저 창을 채우게 (2026-07-31) ────────────────────────────
#  Unity 기본 템플릿의 데스크톱 경로는 캔버스를 **960x600 으로 고정**한다.
#  그 결과 공개 URL 을 열면 흰 여백 한가운데에 작은 상자로 게임이 뜬다 —
#  실측(1253px 뷰포트)에서 게임이 화면의 1/3 도 못 채웠다.  심사자의 첫인상이
#  "작은 창에서 도는 데모"가 되는데, 이건 게임 내용과 무관하게 완성도로 읽힌다.
#
#  Unity WebGL 템플릿 자체를 고치는 대신 여기서 후처리한다 — 템플릿 변경은
#  재빌드(수 분)를 요구하고, 배포 단계의 표현 문제는 배포 단계에서 푸는 것이
#  맞다.  <title> 교체와 같은 자리, 같은 이유다.
#
#  세 곳을 바꾼다:
#   ① 인라인 고정 크기 지정을 창 크기 추종으로 (resize 도 따라간다)
#   ② 컨테이너 중앙정렬(transform)을 좌상단 고정 + 100% 로
#   ③ Unity 푸터(로고/전체화면 버튼)를 캔버스 위 오버레이로 — 세로 공간을
#      뺏지 않게.  전체화면 버튼은 유용하므로 지우지 않고 띄운다.
desktop_fixed = '''        canvas.style.width = "960px";
        canvas.style.height = "600px";'''
desktop_fit = '''        // deploy-play.sh 주입 — 브라우저 창을 가득 채운다(고정 960x600 대체).
        var fitCanvas = function () {
          canvas.style.width = window.innerWidth + "px";
          canvas.style.height = window.innerHeight + "px";
        };
        fitCanvas();
        window.addEventListener("resize", fitCanvas);'''
if desktop_fixed in html:
    html = html.replace(desktop_fixed, desktop_fit, 1)
    print("  index.html 캔버스 고정크기 → 창 채우기")
else:
    # 템플릿이 바뀌어 패턴이 안 맞으면 조용히 넘어가지 않는다 — 다음 사람이
    #  "왜 다시 작아졌지"로 헤매지 않도록 배포 로그에 남긴다.
    print("  ⚠ index.html 캔버스 크기 지정 패턴 불일치 — 창 채우기 미적용")

fill_css = '''<style>
      /* deploy-play.sh 주입 — 캔버스 전체화면 레이아웃 */
      html, body { width: 100%; height: 100%; overflow: hidden; background: #14100c; }
      #unity-container.unity-desktop { left: 0; top: 0; transform: none;
                                       width: 100%; height: 100%; }
      #unity-canvas { display: block; width: 100%; height: 100%; }
      #unity-footer { position: fixed; right: 8px; bottom: 6px; z-index: 5;
                      opacity: 0.55; }
      #unity-footer:hover { opacity: 1; }
      #unity-logo-title-footer { display: none; }
      #unity-build-title { color: #e8dcc8; text-shadow: 0 1px 2px #000; }
    </style>
  </head>'''
if "</head>" in html:
    html = html.replace("</head>", fill_css, 1)
p.write_text(html, encoding="utf-8")
PY
fi

BYTES=$(du -sm "$DEST" | cut -f1)
echo "복사 완료: ${BYTES}MB"
echo
echo "다음: git add site/play && git commit && git push origin HEAD:main"
echo "      배포 URL → https://melons.github.io/MelonS-Agents/play/"
echo "      (Pages 워크플로는 main 의 site/** 변경에만 반응한다)"
