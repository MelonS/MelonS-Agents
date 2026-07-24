"""Tier-2 어댑터 — 기존 스크립트를 부르는 유일한 통로.

원칙: **그래프는 아무것도 직접 만들지 않는다.** ComfyUI 호출, ffmpeg 렌더, 법률
검사는 전부 `scripts/`·`agents/` 아래 기존 코드가 이미 하고 있고, 검증도 끝났다.
그래프는 순서·재시도·게이트만 책임진다. 그래서 subprocess 호출은 전부 이 파일을
지나가게 해두었다 — 나중에 "어디서 외부 프로세스를 부르는가"를 한 파일만 보면 안다.

Windows 주의: 이 레포의 bash 스크립트는 `python3`를 쓰지만, Windows에서 `python3`는
Microsoft Store 스텁이라 조용히 아무것도 안 한다. 여기서는 항상 `sys.executable`
(= 지금 그래프를 돌리고 있는 바로 그 파이썬)을 쓴다.
"""

from __future__ import annotations

import base64
import os
import pathlib
import subprocess
import sys
import time

# 1x1 회색 PNG. mock 모드에서 "파일이 실제로 생겼는지"까지 검증하려고 진짜 PNG를 쓴다.
_MOCK_PNG = base64.b64decode(
    b"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk"
    b"+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
)


def repo_root() -> pathlib.Path:
    """graph/ 의 부모 = 레포 루트."""
    return pathlib.Path(__file__).resolve().parent.parent


def records_dir() -> pathlib.Path:
    """$RECORDS_DIR 규약을 그대로 따른다 (기본 ./records, gitignored)."""
    raw = os.environ.get("RECORDS_DIR", "./records")
    p = pathlib.Path(raw)
    return p if p.is_absolute() else (repo_root() / p).resolve()


def checkpoint_path() -> pathlib.Path:
    """체크포인트 DB도 데이터이므로 records/ 아래에 둔다 (코드/데이터 분리 규칙)."""
    d = records_dir() / "graph"
    d.mkdir(parents=True, exist_ok=True)
    return d / "checkpoints.sqlite"


class ToolError(RuntimeError):
    def __init__(self, cmd: list[str], rc: int, out: str, err: str):
        self.cmd, self.rc, self.out, self.err = cmd, rc, out, err
        tail = (err or out or "").strip().splitlines()[-5:]
        super().__init__("rc=%d  %s\n%s" % (rc, " ".join(cmd[:3]), "\n".join(tail)))


def run(cmd: list[str], timeout: int = 900, cwd: pathlib.Path | None = None) -> str:
    """외부 프로세스 실행. 실패하면 ToolError."""
    proc = subprocess.run(
        cmd,
        cwd=str(cwd or repo_root()),
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
        env={**os.environ, "PYTHONUTF8": "1"},
    )
    if proc.returncode != 0:
        raise ToolError(cmd, proc.returncode, proc.stdout, proc.stderr)
    return proc.stdout


def gen_still(
    prompt: str,
    out_path: pathlib.Path,
    *,
    seed: int = 1234,
    width: int = 768,
    height: int = 1344,
    server: str | None = None,
    mock: bool = False,
    timeout: int = 600,
) -> float:
    """스틸 1장 생성. 반환값은 걸린 초.

    실제 경로는 `scripts/zimage-still.py` (Z-Image Turbo / ComfyUI). 기본 ~9초.
    mock=True면 ComfyUI 없이 배선만 검증한다 — GPU 없는 머신·CI에서 쓴다.
    """
    out_path.parent.mkdir(parents=True, exist_ok=True)
    t0 = time.time()

    if mock:
        out_path.write_bytes(_MOCK_PNG)
        return round(time.time() - t0, 2)

    script = repo_root() / "scripts" / "zimage-still.py"
    if not script.exists():
        raise ToolError([str(script)], 66, "", "zimage-still.py 없음 — --mock 로 배선만 검증하거나 경로 확인")

    cmd = [
        sys.executable, str(script),
        "--prompt", prompt,
        "--output", str(out_path),
        "--width", str(width),
        "--height", str(height),
        "--seed", str(seed),
    ]
    if server or os.environ.get("COMFYUI_URL"):
        cmd += ["--server", server or os.environ["COMFYUI_URL"]]

    run(cmd, timeout=timeout)
    if not out_path.exists():
        raise ToolError(cmd, 70, "", "스크립트는 성공했는데 %s 가 없다" % out_path)
    return round(time.time() - t0, 2)
