#!/usr/bin/env python3
"""sync-readme-graph.py — README 의 실행 그래프 구조도를 코드에서 다시 뽑아 넣는다.

README.md(영어)·README.ko.md(한국어)·graph/README.md 안의 마커 사이 블록만
갱신한다:

    <!-- graph:shorts:begin -->  ...  <!-- graph:shorts:end -->
    <!-- graph:game:begin -->    ...  <!-- graph:game:end -->

구조도를 손으로 관리하면 반드시 낡는다(2026-07-26 감사: for-analysts.md 의
서브에이전트 수가 22/23/27 로 갈라져 있었다).  그래서 README 의 그림도
`graph/diagram.py` 가 실행 중인 그래프에서 뽑고, 이 스크립트가 그 결과를
파일에 밀어 넣는다.

    python scripts/sync-readme-graph.py            # 갱신
    python scripts/sync-readme-graph.py --check     # 어긋나면 exit 1 (훅/CI용)

langgraph 가 없는 인터프리터로 실행되면 저장소의 .venv 로 한 번 재실행한다.
그래도 없으면(클론 직후 등) --check 는 경고만 남기고 통과시킨다 — 커밋을
막을 근거가 아니라, 가진 도구로 확인할 수 없다는 뜻이기 때문이다.
"""
from __future__ import annotations

import argparse
import os
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
# (파일, 라벨 언어, 이 파일이 담는 블록들).  README 는 쇼츠 두 장만 싣고,
# 네 장 전부는 graph/README.md 에 둔다 — 랜딩 페이지가 다시 길어지지 않게.
TARGETS = (
    (ROOT / "README.md", "en", ("shorts", "game")),
    (ROOT / "README.ko.md", "ko", ("shorts", "game")),
    (ROOT / "graph" / "README.md", "ko", ("shorts", "game")),
)
_REEXEC_FLAG = "SYNC_README_GRAPH_REEXEC"


def _venv_python() -> pathlib.Path | None:
    for rel in ("Scripts/python.exe", "bin/python"):
        cand = ROOT / ".venv" / rel
        if cand.exists():
            return cand
    return None


def _ensure_langgraph(check_only: bool) -> bool:
    """langgraph 를 import 할 수 있는 인터프리터로 옮겨 탄다.  False = 포기."""
    try:
        import langgraph  # noqa: F401
        return True
    except ModuleNotFoundError:
        pass
    if os.environ.get(_REEXEC_FLAG) == "1":
        return False
    py = _venv_python()
    if py is None:
        return False
    env = dict(os.environ, **{_REEXEC_FLAG: "1", "PYTHONIOENCODING": "utf-8"})
    argv = [str(py), str(pathlib.Path(__file__).resolve())] + sys.argv[1:]
    raise SystemExit(subprocess.call(argv, env=env, cwd=str(ROOT)))


def _blocks(lang: str) -> dict[str, str]:
    sys.path.insert(0, str(ROOT))
    from graph import diagram

    return {
        "shorts": diagram.shorts_compact(lang),
        "game": diagram.game_compact(lang),
    }


def _replace(text: str, name: str, mermaid: str) -> tuple[str, bool]:
    begin, end = f"<!-- graph:{name}:begin -->", f"<!-- graph:{name}:end -->"
    i, j = text.find(begin), text.find(end)
    if i < 0 or j < 0 or j < i:
        raise SystemExit(f"마커를 찾지 못했다: {begin} / {end}")
    block = f"{begin}\n```mermaid\n{mermaid}\n```\n{end}"
    new = text[:i] + block + text[j + len(end):]
    return new, new != text


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="갱신하지 않고 어긋나면 exit 1")
    args = ap.parse_args()

    if not _ensure_langgraph(args.check):
        msg = "langgraph 없음 — 구조도 동기화를 건너뛴다 (pip install -r graph/requirements.txt)"
        print(f"[graph-sync] {msg}", file=sys.stderr)
        return 0 if args.check else 1

    drift = []
    for path, lang, names in TARGETS:
        text = original = path.read_text(encoding="utf-8")
        blocks = _blocks(lang)
        for name in names:
            text, _ = _replace(text, name, blocks[name])
        if text != original:
            drift.append(path.name)
            if not args.check:
                path.write_text(text, encoding="utf-8", newline="\n")

    if args.check:
        if drift:
            print("[graph-sync] README 구조도가 코드와 어긋났다: " + ", ".join(drift),
                  file=sys.stderr)
            print("[graph-sync] 고치기: python scripts/sync-readme-graph.py", file=sys.stderr)
            return 1
        print("[graph-sync] README 구조도가 코드와 일치한다")
        return 0

    print("[graph-sync] 갱신: " + (", ".join(drift) if drift else "변경 없음"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
