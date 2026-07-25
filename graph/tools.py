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
import shutil
import subprocess
import sys
import time

# 1x1 회색 PNG. mock 모드에서 "파일이 실제로 생겼는지"까지 검증하려고 진짜 PNG를 쓴다.
_MOCK_PNG = base64.b64decode(
    b"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk"
    b"+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
)


def force_utf8_stdout() -> None:
    """Windows 콘솔의 기본 코드페이지(cp949)에서 죽지 않게 stdout/stderr 를 UTF-8 로.

    이 그래프의 노드 이름·라벨·로그는 전부 한국어이고 구조도에는 `—`/`→` 가 섞인다.
    cp949 로는 인코딩이 안 돼서 `python -m graph.shorts_graph diagram` 이
    UnicodeEncodeError 로 죽었다(2026-07-26 실측). PYTHONIOENCODING 을 매번
    붙이라고 문서에 적는 대신 CLI 진입점에서 한 번 고정한다.
    """
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is not None:
            try:
                reconfigure(encoding="utf-8")
            except (ValueError, OSError):
                pass  # 파이프가 이미 닫혔거나 재설정 불가 — 출력은 그대로 시도한다


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


def gen_clip(
    image: pathlib.Path,
    prompt: str,
    out_path: pathlib.Path,
    *,
    seed: int = -1,
    server: str | None = None,
    mock: bool = False,
    timeout: int = 2400,
) -> float:
    """앵커 스틸 1장 → 모션 클립 1개 (~7분). 반환값은 걸린 초.

    실제 경로는 `scripts/wan-a14b-i2v.py` (Wan2.2 A14B + lightx2v LoRA).
    A14B는 T2V로 쓰면 무너지므로 앵커 스틸이 필수다 — 그래서 문을 통과한
    스틸만 여기 들어온다. 이 7분이 파이프라인 전체 비용의 92%다.
    """
    out_path.parent.mkdir(parents=True, exist_ok=True)
    t0 = time.time()

    if mock:
        out_path.write_bytes(_MOCK_PNG)      # 배선 검증용 자리표시자
        return round(time.time() - t0, 2)

    script = repo_root() / "scripts" / "wan-a14b-i2v.py"
    if not script.exists():
        raise ToolError([str(script)], 66, "", "wan-a14b-i2v.py 없음 — --mock 로 배선만 검증")

    cmd = [
        sys.executable, str(script),
        "--image", str(image),
        "--prompt", prompt,
        "--output", str(out_path),
        "--seed", str(seed),
    ]
    if server or os.environ.get("COMFYUI_URL"):
        cmd += ["--server", server or os.environ["COMFYUI_URL"]]

    run(cmd, timeout=timeout)
    if not out_path.exists():
        raise ToolError(cmd, 70, "", "스크립트는 성공했는데 %s 가 없다" % out_path)
    return round(time.time() - t0, 2)


def ffmpeg_bin() -> str:
    return os.environ.get("FFMPEG_BIN") or "ffmpeg"


def bash_bin() -> str:
    """POSIX 스크립트를 돌릴 bash.

    Windows 함정 하나 더: `bash`는 **WSL 스텁**이라 WSL이 없으면
    "Linux용 Windows 하위 시스템에 배포가 없습니다"만 찍고 rc=1로 죽는다.
    `python3`가 Store 스텁인 것과 같은 종류의 함정. Git Bash를 직접 찾는다.
    """
    if os.name != "nt":
        return "bash"
    cand = [os.environ.get("BASH_BIN"),
            r"C:\Program Files\Git\bin\bash.exe",
            r"C:\Program Files\Git\usr\bin\bash.exe",
            r"C:\Program Files (x86)\Git\bin\bash.exe"]
    for c in cand:
        if c and pathlib.Path(c).exists():
            return c
    found = shutil.which("bash")
    if found and "System32" not in found:      # System32\bash.exe = WSL 스텁
        return found
    raise ToolError(["bash"], 66, "", "Git Bash를 못 찾았다 — BASH_BIN 환경변수로 지정")


def concat_clips(clips: list[pathlib.Path], out_path: pathlib.Path, *, mock: bool = False) -> float:
    """컷들을 이어붙인다. 재인코딩 없이 stream copy.

    주의: concat 목록은 clips_dir 기준 **상대 경로**여야 한다 — Windows ffmpeg는
    MSYS 스타일 /g/... 경로를 못 읽는다 (assemble-short.sh 주석에 있는 함정).
    """
    out_path.parent.mkdir(parents=True, exist_ok=True)
    t0 = time.time()

    if mock:
        out_path.write_bytes(_MOCK_PNG)
        return round(time.time() - t0, 2)

    # concat 목록은 **목록 파일이 있는 디렉터리 기준 상대 경로**로 해석된다.
    # 클립은 clips/ 에, 최종본은 outputs/ 에 있으므로 목록도 clips/ 에 둔다.
    # (파일명만 쓰고 outputs/ 에 두면 "Impossible to open i01_r0.mp4" 로 죽는다.
    #  mock 은 파일을 읽지 않아 이 버그가 안 잡혔다 — 실물로만 나온다.)
    clips_dir = pathlib.Path(clips[0]).parent
    listing = clips_dir / "concat.txt"
    listing.write_text(
        "".join("file '%s'\n" % pathlib.Path(c).name for c in clips), encoding="utf-8"
    )
    # Windows ffmpeg 는 MSYS 스타일 /g/... 경로를 못 읽는다 → cwd 를 clips_dir 로
    # 잡고 목록은 파일명, 출력은 절대경로로 준다.
    run([ffmpeg_bin(), "-y", "-loglevel", "error", "-f", "concat", "-safe", "0",
         "-i", listing.name, "-c", "copy", str(out_path)],
        cwd=clips_dir, timeout=600)

    if not out_path.exists():
        raise ToolError(["ffmpeg"], 70, "", "concat은 성공했는데 %s 가 없다" % out_path)
    return round(time.time() - t0, 2)


def legal_gate(mission_dir: pathlib.Path, profile: str, *, platform: str = "public",
               external_verdict: pathlib.Path | None = None) -> tuple[int, str]:
    """scripts/legal-gate.sh 를 그대로 호출한다. 재작성하지 않는다.

    이 게이트는 이미 fail-closed로 정확하다 — 결정론 체크(bash가 증명 가능한 것)와
    판단 체크(모델이 판정한 것)를 프로필의 required_checks 위에서 병합하고,
    **미실행 필수 체크는 REVISE로 떨어뜨린다.** 그래프는 부르고 종료 코드만 읽는다.

    반환: (rc, stdout).  rc = 0 PASS · 1 REVISE · 2 BLOCK · 64/65 사용법·입력 오류
    """
    script = repo_root() / "scripts" / "legal-gate.sh"
    cmd = [bash_bin(), str(script), str(mission_dir),
           "--profile=%s" % profile, "--platform=%s" % platform]
    if external_verdict:
        cmd.append("--external-verdict=%s" % external_verdict)

    proc = subprocess.run(
        cmd, cwd=str(repo_root()), capture_output=True, text=True,
        encoding="utf-8", errors="replace", timeout=600,
        env={**os.environ, "PYTHONUTF8": "1"},
    )
    # 0/1/2 는 전부 정상적인 판정 결과다 — 예외로 던지면 안 된다.
    return proc.returncode, (proc.stdout or "") + (proc.stderr or "")


def launch_and_capture(
    exe: pathlib.Path,
    shot: pathlib.Path,
    *,
    delay: float = 8.0,
    extra_args: list[str] | None = None,
    grace: float = 90.0,
) -> tuple[bool, str]:
    """게임 exe 를 띄우고 AutoScreenshotter 가 PNG 를 쓸 때까지 기다린다.

    `skills/game-dev-agent/scripts/modules/qa.py::launch_and_capture` 의 계약을
    그대로 따른다. **subprocess.run 을 쓰면 안 된다** — 게임이 스스로 종료하지
    않으면 통째로 타임아웃으로 죽는다(실측 300초). 대신 Popen 으로 띄우고
    파일이 생기는지 폴링한 뒤 직접 종료시킨다.
    """
    if shot.exists():
        shot.unlink()
    shot.parent.mkdir(parents=True, exist_ok=True)

    cmd = [str(exe), "-delay", str(delay), "-screenshot", str(shot)] + list(extra_args or [])
    proc = subprocess.Popen(cmd)
    deadline = time.time() + delay + grace
    captured = False

    while time.time() < deadline:
        if shot.exists() and shot.stat().st_size > 1024:
            captured = True
            break
        if proc.poll() is not None:          # 게임이 먼저 끝남
            break
        time.sleep(0.5)

    if proc.poll() is None:                   # 아직 살아 있으면 정리
        proc.terminate()
        try:
            proc.wait(timeout=10)
        except subprocess.TimeoutExpired:
            proc.kill()

    if captured:
        return True, "스크린샷 %s (%.0f KB)" % (shot.name, shot.stat().st_size / 1024)
    if shot.exists():
        return False, "스크린샷이 너무 작다 (%d bytes) — 화면이 안 떴을 수 있다" % shot.stat().st_size
    return False, "스크린샷 미생성 — %.0fs 대기, 게임이 화면에 못 뜬 듯" % (delay + grace)


def extract_frames(
    video: pathlib.Path,
    out_dir: pathlib.Path,
    *,
    count: int = 3,
    mock: bool = False,
) -> list[str]:
    """컷 심사용 프레임 추출. 심사위원은 이 jpg들을 실제로 열어 채점한다.

    `scripts/judge-frames.py` 기본값이 3장이다. 이게 편당 실행 토큰의 최대
    덩어리라 늘릴 때는 비용을 같이 봐야 한다 (컷당 1장 ≈ 1.3K 토큰).
    """
    out_dir.mkdir(parents=True, exist_ok=True)

    if mock:
        paths = []
        for i in range(count):
            p = out_dir / ("f%02d.jpg" % i)
            p.write_bytes(_MOCK_PNG)
            paths.append(str(p))
        return paths

    script = repo_root() / "scripts" / "judge-frames.py"
    out = run([sys.executable, str(script), "--video", str(video),
               "--out-dir", str(out_dir), "--count", str(count)], timeout=300)
    paths = [ln.strip() for ln in out.splitlines() if ln.strip().lower().endswith((".jpg", ".jpeg", ".png"))]
    if not paths:                                   # stdout 형식이 바뀐 경우 대비
        paths = sorted(str(p) for p in out_dir.glob("*.jpg"))
    if not paths:
        raise ToolError(["judge-frames.py"], 70, out, "프레임이 하나도 안 나왔다")
    return paths
