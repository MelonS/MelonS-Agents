#!/usr/bin/env python3
"""ip-scrub.py — 공개 저장소에 타사 상표가 새는지 검사한다.

왜 필요한가: 이 저장소의 게임은 콜로니 심 장르의 프로토타입이다.  게임 메커닉
자체는 저작권 보호 대상이 아니지만, **우리 제품을 설명하면서 특정 상표를 반복
인용하면** 그 문장이 "클론"이라는 자백으로 인용되고 부정경쟁·상표 희석 주장에
빌미가 된다.  2026-08-01 점검에서 코드 주석 2곳과 아트 문서 2곳에 원작 게임명이
남아 있었다 — 규칙은 운영 계약과 에이전트 메모리에 있었는데 **집행하는 도구가
없어서** 새어 나온 것이다.  이 스크립트가 그 도구다.

    python scripts/ip-scrub.py            # 전체 추적 파일 스캔 (보고만)
    python scripts/ip-scrub.py --check    # 위반이 있으면 exit 1 (CI·훅용)
    python scripts/ip-scrub.py --staged   # 스테이징된 내용만 검사 (pre-commit)

**명목적 사용은 위반이 아니다.**  타사 제품을 *그 제품으로서* 지칭하는 것 —
귀속 표기, 판례 인용, 우리가 만든 영상의 주제 분류 — 은 정당하며 ALLOW 에 근거와
함께 등록한다.  금지 대상은 *우리 산출물을 남의 상표로 설명하는* 문장이다.
"""
from __future__ import annotations

import argparse
import fnmatch
import re
import subprocess
import sys

# Windows 콘솔 기본 코드페이지(cp949)에서 —·↳·한글이 섞인 출력이 죽는다.
for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, "reconfigure"):
        try:
            _stream.reconfigure(encoding="utf-8")
        except (ValueError, OSError):
            pass

# 우리 제품을 설명할 때 쓰면 안 되는 이름 — 프랜차이즈명과 제작자명.
BANNED: dict[str, str] = {
    r"rim\s?world|림월드": "콜로니 심 원작 게임명 — '정통 콜로니 심' 등 장르 표현으로",
    r"dwarf\s?fortress|드워프\s?포트리스": "동일 장르 원작 게임명",
    r"oxygen\s+not\s+included": "동일 장르 원작 게임명",
    r"prison\s+architect": "동일 장르 원작 게임명",
    r"stardew\s?valley|스타듀\s?밸리": "타 게임 상표",
    r"factorio|팩토리오": "타 게임 상표",
    r"\btynan\b": "타 게임 제작자 실명",
    r"대항해시대|uncharted\s+waters": "코에이(코에이 테크모) 항해 게임 시리즈명 — '항해 무역 시뮬레이션' 등 장르 표현으로",
}

# (경로 glob, 패턴 일부, 사유) — 명목적 사용으로 허용한다.
ALLOW: list[tuple[str, str, str]] = [
    ("ATTRIBUTIONS.md", ".*",
     "라이선스가 요구하는 귀속 표기 — 출처를 이름으로 밝히는 것이 의무"),
    ("scripts/ip-scrub.py", ".*",
     "이 스크립트 자신의 패턴 정의"),
    ("scripts/yt-scoreboard.py", "rim\\s?world|림월드",
     "우리가 만든 영상의 주제를 분류하는 키워드 — 타사 제품을 그 제품으로 지칭하는 명목적 사용"),
    ("skills/job-hunt/sources/*.sh", ".*",
     "스크래핑을 하지 않기로 한 근거로 판례·robots.txt 를 인용 — 사명 표기가 근거의 일부"),
    ("docs/audit/*.md", ".*",
     "감사 리포트는 발견 당시의 원문을 인용한다 — 사후 편집하면 이력이 훼손된다"),
]

_SKIP_SUFFIX = (".png", ".jpg", ".jpeg", ".gif", ".webp", ".mp4", ".mp3", ".wav",
                ".ttf", ".otf", ".woff", ".woff2", ".pdf", ".zip", ".safetensors",
                ".unityweb", ".wasm", ".data", ".meta", ".asset", ".prefab")


def _allowed(path: str, pattern: str) -> str | None:
    for glob, pat, reason in ALLOW:
        if fnmatch.fnmatch(path, glob) and (pat == ".*" or pat == pattern):
            return reason
    return None


def _tracked_files() -> list[str]:
    out = subprocess.run(["git", "ls-files"], capture_output=True, text=True, encoding="utf-8", errors="replace", check=True)
    return [p for p in out.stdout.splitlines() if not p.lower().endswith(_SKIP_SUFFIX)]


def _staged_files() -> list[str]:
    out = subprocess.run(["git", "diff", "--cached", "--name-only", "--diff-filter=ACM"],
                         capture_output=True, text=True, encoding="utf-8", errors="replace", check=True)
    return [p for p in out.stdout.splitlines() if not p.lower().endswith(_SKIP_SUFFIX)]


def _read(path: str, staged: bool) -> str:
    if staged:
        out = subprocess.run(["git", "show", f":{path}"], capture_output=True, text=True,
                         encoding="utf-8", errors="replace")
        return out.stdout if out.returncode == 0 else ""
    try:
        with open(path, encoding="utf-8", errors="ignore") as fh:
            return fh.read()
    except OSError:
        return ""


def scan(staged: bool) -> list[tuple[str, int, str, str, str]]:
    """[(path, lineno, pattern, line, reason_or_empty)] — reason 이 차면 허용된 건."""
    hits = []
    for path in (_staged_files() if staged else _tracked_files()):
        text = _read(path, staged)
        if not text:
            continue
        for lineno, line in enumerate(text.splitlines(), 1):
            for pattern in BANNED:
                if re.search(pattern, line, re.IGNORECASE):
                    hits.append((path, lineno, pattern, line.strip()[:120],
                                 _allowed(path, pattern) or ""))
    return hits


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="위반이 있으면 exit 1")
    ap.add_argument("--staged", action="store_true", help="스테이징된 내용만 검사")
    ap.add_argument("--show-allowed", action="store_true", help="허용된 건도 함께 출력")
    args = ap.parse_args()

    hits = scan(args.staged)
    violations = [h for h in hits if not h[4]]
    allowed = [h for h in hits if h[4]]

    if args.show_allowed and allowed:
        print("허용(명목적 사용):")
        for path, lineno, _pat, line, reason in allowed:
            print(f"  {path}:{lineno}  {line}\n      ↳ {reason}")
        print()

    if not violations:
        print(f"ip-scrub OK — 위반 0건 (허용 {len(allowed)}건)")
        return 0

    print("타사 상표가 우리 산출물 설명에 남아 있다:\n")
    for path, lineno, pat, line, _ in violations:
        print(f"  {path}:{lineno}")
        print(f"    {line}")
        print(f"    ↳ {BANNED[pat]}\n")
    print("장르 표현으로 바꾸거나, 명목적 사용이면 scripts/ip-scrub.py 의 ALLOW 에")
    print("경로와 사유를 함께 등록하라 (사유 없는 예외는 두지 않는다).")
    return 1 if args.check else 0


if __name__ == "__main__":
    sys.exit(main())
