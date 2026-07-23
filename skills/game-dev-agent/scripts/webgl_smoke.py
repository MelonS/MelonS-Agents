"""webgl_smoke - WebGL 빌드 브라우저 스모크 게이트 (2026-07-24 신설).

배경: 하네스 전체가 Windows exe 기준이라 WebGL 경로는 자동검증 0건이었다
(NHN 사전과제 제출 타깃이 웹인데도).  이 스크립트가 그 최소선:
빌드 서빙 → 헤드리스 Chromium 부팅 → 로드 완료 → 콘솔 에러 0 + 화면 어서션.

Usage:
  python webgl_smoke.py [--build-dir <webgl-build-dir>] [--port 8977] [--boot-sec 30]

  --build-dir 생략 시 builds/day-*-webgl 최신 폴더 자동 선택.
  exit 0 = PASS.  스크린샷/콘솔로그는 G:/ai/_repro_shots/webgl-smoke/ 에 증거 보존.

전제: pip playwright + chromium.  브라우저 경로는 $PLAYWRIGHT_BROWSERS_PATH,
미설정 시 알려진 로컬 설치 위치로 폴백.
"""
from __future__ import annotations
import argparse
import http.server
import json
import os
import socketserver
import sys
import threading
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")

import refactor_check as rc

BUILDS_DIR = rc.REPO / "skills" / "game-prototype" / "builds"
SHOTS_DIR = Path(os.environ.get("REPRO_SHOTS_DIR", "G:/ai/_repro_shots")) / "webgl-smoke"

# 브라우저 폴백 (env 우선 — CLAUDE.md env-driven paths 원칙)
_PW_FALLBACKS = [
    "G:/ai/_archive/_design_melons_readme/.pw-browsers",
]


def _ensure_browsers_path() -> None:
    if os.environ.get("PLAYWRIGHT_BROWSERS_PATH"):
        return
    for cand in _PW_FALLBACKS:
        if Path(cand).is_dir():
            os.environ["PLAYWRIGHT_BROWSERS_PATH"] = cand
            return


def _latest_webgl_build() -> Path | None:
    cands = sorted(BUILDS_DIR.glob("day-*-webgl"), key=lambda p: p.stat().st_mtime)
    return cands[-1] if cands else None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--build-dir")
    ap.add_argument("--port", type=int, default=8977)
    ap.add_argument("--boot-sec", type=int, default=30)
    args = ap.parse_args()

    build = Path(args.build_dir) if args.build_dir else _latest_webgl_build()
    if build is None or not (build / "index.html").exists():
        print(f"[webgl_smoke] FAIL: WebGL 빌드 없음 (build={build})")
        return 2
    SHOTS_DIR.mkdir(parents=True, exist_ok=True)
    print(f"[webgl_smoke] build={build}")

    handler = lambda *a, **kw: http.server.SimpleHTTPRequestHandler(
        *a, directory=str(build), **kw)
    httpd = socketserver.TCPServer(("127.0.0.1", args.port), handler)
    threading.Thread(target=httpd.serve_forever, daemon=True).start()

    _ensure_browsers_path()
    from playwright.sync_api import sync_playwright

    failures: list[str] = []
    console_errors: list[str] = []
    try:
        with sync_playwright() as pw:
            browser = pw.chromium.launch(headless=True)
            page = browser.new_page(viewport={"width": 1280, "height": 720})
            page.on("console",
                    lambda m: console_errors.append(m.text) if m.type == "error" else None)
            page.on("pageerror", lambda e: console_errors.append(str(e)))
            page.goto(f"http://127.0.0.1:{args.port}/", timeout=30_000)

            # 1) Unity 캔버스 존재
            page.wait_for_selector("#unity-canvas", timeout=15_000)
            # 2) 부팅 대기 (로더 progress bar 소멸 또는 boot-sec 상한)
            try:
                page.wait_for_selector("#unity-loading-bar",
                                       state="hidden", timeout=args.boot_sec * 1000)
            except Exception:
                failures.append(f"로딩바가 {args.boot_sec}s 내에 안 사라짐")
            page.wait_for_timeout(5_000)  # 첫 씬 안정화

            shot = SHOTS_DIR / "01_booted.png"
            page.screenshot(path=str(shot))
            print(f"[webgl_smoke] shot -> {shot}")

            # 3) 화면이 실제로 그려졌는가 (검정/단색 화면 방지)
            try:
                from PIL import Image
                im = Image.open(shot).convert("L")
                lo, hi = im.getextrema()
                if hi - lo < 30:
                    failures.append(f"화면 콘트라스트 부족 (극값 {lo}-{hi}) — 검정/단색 화면 의심")
            except ImportError:
                print("[webgl_smoke] (PIL 없음 — 콘트라스트 어서션 생략)")

            # 4) 콘솔 에러 0
            if console_errors:
                failures.append(f"콘솔 에러 {len(console_errors)}건: {console_errors[:3]}")

            (SHOTS_DIR / "console.json").write_text(
                json.dumps(console_errors, ensure_ascii=False, indent=1), encoding="utf-8")
            browser.close()
    finally:
        httpd.shutdown()

    if failures:
        print("[webgl_smoke] ━━━ FAIL ━━━")
        for f in failures:
            print(f"  ✗ {f}")
        return 1
    print("[webgl_smoke] OVERALL: PASS (부팅·렌더·콘솔0)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
