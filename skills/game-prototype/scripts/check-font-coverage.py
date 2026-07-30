#!/usr/bin/env python3
"""check-font-coverage.py — UI 문자열에 번들 폰트가 못 그리는 글자가 있는지 검사.

왜 필요한가: Unity 는 폰트에 없는 코드포인트를 **빈칸(tofu)** 으로 조용히 그린다.
예외도 경고도 없다.  2026-07-27 실측 사고: 건축 셸프의 제거 도구 글리프가 `✕`
(U+2715) 였는데 번들 3종 폰트 어디에도 없어 셀이 통째로 빈 상자로 보였다.

usage:
  python skills/game-prototype/scripts/check-font-coverage.py
exit 0 = 미지원 문자 없음.

전제: pip fontTools.  없으면 skip(exit 0) 하되 그 사실을 출력한다.
"""
from __future__ import annotations
import re
import sys
import unicodedata
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

REPO = Path(__file__).resolve().parents[3]
UP = REPO / "skills" / "game-prototype" / "unity-project"
FONTS = UP / "Assets" / "Resources" / "Fonts"
SCRIPTS = UP / "Assets" / "Scripts"

# C# 문자열 리터럴 (보간·이스케이프는 대충 걷어낸다 — 목적은 '이상한 글자' 탐지)
STR_RE = re.compile(r'"((?:[^"\\\n]|\\.)*)"')

# 검사 대상에서 빼는 것: ASCII 는 어떤 폰트에도 있고, 한글/CJK 는 한글 폰트가 커버한다.
def is_suspect(ch: str) -> bool:
    cp = ord(ch)
    if cp < 0x80:
        return False
    # 한글 음절/자모, CJK 기본
    if 0xAC00 <= cp <= 0xD7A3 or 0x1100 <= cp <= 0x11FF or 0x3130 <= cp <= 0x318F:
        return False
    if 0x4E00 <= cp <= 0x9FFF:
        return False
    return True


def main() -> int:
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        print("fontTools 미설치 — 검사 skip (pip install fonttools 하면 게이트가 활성화된다)")
        return 0

    if not FONTS.is_dir():
        print(f"폰트 폴더 없음: {FONTS}")
        return 0

    cmaps: dict[str, set[int]] = {}
    for p in sorted(list(FONTS.glob("*.ttf")) + list(FONTS.glob("*.otf"))):
        f = TTFont(str(p), fontNumber=0, lazy=True)
        s: set[int] = set()
        for t in f["cmap"].tables:
            s |= set(t.cmap.keys())
        cmaps[p.name] = s
    if not cmaps:
        print("번들 폰트를 못 찾음")
        return 0
    print(f"번들 폰트 {len(cmaps)}종: {', '.join(cmaps)}")

    # 어느 폰트에도 없으면 확실한 tofu.  일부에만 없으면 그 폰트를 쓰는 UI 에서 tofu.
    union = set().union(*cmaps.values())

    bad: list[tuple[str, int, str, str]] = []
    for cs in sorted(SCRIPTS.rglob("*.cs")):
        try:
            text = cs.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for i, line in enumerate(text.splitlines(), 1):
            if line.lstrip().startswith("//") or line.lstrip().startswith("///"):
                continue
            for m in STR_RE.finditer(line):
                for ch in m.group(1):
                    if not is_suspect(ch):
                        continue
                    cp = ord(ch)
                    missing_all = cp not in union
                    missing_some = [n for n, s in cmaps.items() if cp not in s]
                    if missing_all or missing_some:
                        bad.append((str(cs.relative_to(UP)), i, ch,
                                    "전체" if missing_all else "일부:" + ",".join(missing_some)))

    # ⚠ 2번째 검사를 **조기 반환보다 먼저** 돌린다.  처음엔 아래 tofu 리포트 뒤에
    #  두었는데, tofu 가 없으면 여기서 return 0 해 버려 TextMesh 검사가 아예 실행되지
    #  않았다 — 오늘 하루 종일 잡아 온 '조용한 미실행'을 검사 스크립트가 그대로 재현할
    #  뻔했다.  검사는 서로의 실행을 가로막으면 안 된다.
    rc_textmesh = check_worldspace_textmesh_fonts()

    if not bad:
        print("PASS — UI 문자열에 미지원 문자 없음.")
        return rc_textmesh

    # 같은 문자는 한 번만 요약해서 보여준다 (파일 수백 줄 쏟아내지 않게)
    by_char: dict[str, list[tuple[str, int, str]]] = {}
    for path, ln, ch, scope in bad:
        by_char.setdefault(ch, []).append((path, ln, scope))

    hard = {ch: v for ch, v in by_char.items() if v[0][2] == "전체"}
    soft = {ch: v for ch, v in by_char.items() if v[0][2] != "전체"}

    for title, group in (("모든 폰트에 없음 (확실한 tofu)", hard),
                         ("일부 폰트에 없음 (해당 폰트 사용처에서 tofu)", soft)):
        if not group:
            continue
        print(f"\n── {title}")
        for ch, hits in sorted(group.items()):
            try:
                nm = unicodedata.name(ch)
            except ValueError:
                nm = "?"
            print(f"  {ch!r} U+{ord(ch):04X} {nm} — {len(hits)}곳")
            for path, ln, scope in hits[:4]:
                print(f"      {path}:{ln}  [{scope}]")
            if len(hits) > 4:
                print(f"      … 외 {len(hits)-4}곳")

    if hard:
        print("\nFAIL — 모든 폰트에 없는 문자가 있다.  대체 문자로 교체할 것")
        print("       (예: ✕ U+2715 → × U+00D7).")
        return 1
    print("\n경고만 — 모든 폰트에 없는 문자는 없음.")
    return rc_textmesh


# ── 2번째 검사: 월드 TextMesh 가 번들 폰트를 쓰는가 ────────────────────────────
#
# 계기 (2026-07-31, 공개 URL 실측): Windows 빌드에서는 콜로니스트 이름·활동 라벨이
# 선명한데 **WebGL 에서는 하나도 렌더되지 않았다**.  UI(Canvas) 텍스트는 멀쩡했다.
#
# 원인: `PawnNameLabel`/`FloatingText` 가 `TextMesh` 에 **폰트를 지정하지 않고**
# Unity 기본 폰트(Arial/LegacyRuntime)를 썼다.  Windows 는 그 폰트에 없는 글리프를
# **OS 폰트로 대체**해 주므로 한글이 나온다.  WebGL 에는 OS 폰트가 없다 — 그래서
# 한글만 통째로 빈다.  즉 **제출 플랫폼에서만** 기능이 사라지는데, Windows 기준
# 재현 게이트는 22/22 초록이었다.
#
# 위 1번 검사(tofu)와 다른 실패다: 1번은 "폰트에 글자가 없다", 이건 "그 폰트를
# 애초에 안 쓴다".  둘 다 조용히 빈칸으로 끝나므로 같은 파일에서 함께 막는다.
TEXTMESH_RE = re.compile(r"AddComponent<TextMesh>\s*\(")
FONT_OK_RE = re.compile(r"ApplyKoreanFont|LoadKoreanFont|Resources\.Load<Font>")


def check_worldspace_textmesh_fonts() -> int:
    """`AddComponent<TextMesh>()` 를 하는 메서드가 번들 폰트를 지정하는지 본다.

    판정 범위는 **그 호출이 있는 메서드 본문 근처**(±40줄)로 잡는다.  C# 파서를
    붙이지 않는 대신 창을 넉넉히 둬서, '같은 메서드 안에서 폰트를 지정했는데
    못 봤다'는 거짓 양성이 나지 않게 한다.  놓치는 쪽(거짓 음성)이 시끄러운
    쪽보다 낫다 — 이 검사는 게이트를 막는 것이 목적이 아니라 눈에 띄게 하는 것이다.
    """
    offenders: list[tuple[str, int]] = []
    for cs in sorted(SCRIPTS.rglob("*.cs")):
        try:
            lines = cs.read_text(encoding="utf-8", errors="replace").splitlines()
        except OSError:
            continue
        for i, line in enumerate(lines):
            if not TEXTMESH_RE.search(line):
                continue
            lo, hi = max(0, i - 40), min(len(lines), i + 41)
            if not FONT_OK_RE.search("\n".join(lines[lo:hi])):
                offenders.append((str(cs.relative_to(UP)), i + 1))

    print("\n── 월드 TextMesh 폰트 검사")
    if not offenders:
        print("  PASS — 모든 TextMesh 생성부가 번들 폰트를 지정한다.")
        return 0
    print("  FAIL — 폰트를 지정하지 않는 TextMesh 생성부:")
    for path, ln in offenders:
        print(f"      {path}:{ln}")
    print("  Unity 기본 폰트는 Windows 에서만 OS 폰트로 한글을 대체한다 —")
    print("  WebGL(제출 타깃)에서는 그 텍스트가 통째로 비어 렌더된다.")
    print("  고치기: UITheme.ApplyKoreanFont(tm) 호출 (폰트 + 머티리얼 함께).")
    return 1


if __name__ == "__main__":
    sys.exit(main())
