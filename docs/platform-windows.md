# Platform: Windows (best-effort tier)

> Windows is a **third-tier** support target — best-effort, validated on
> a single machine (RTX 4070 Ti SUPER + Win 11 Pro).  macOS remains the
> primary validation platform; Linux is best-effort; Windows joins as a
> peer best-effort tier with this document.
>
> Cross-platform direction (active 2026-05-25, per
> [project-crossplatform-strategy] memory):
> *capability detection + env vars, NOT OS branches.*  New code is
> Python-first; existing bash scripts are POSIX-compatible (git-bash).

## Why this exists

Operator pivoted from macOS to Windows on 2026-05-25 to use a stronger
GPU (RTX 4070 Ti SUPER, 16 GB VRAM) for local AI video generation.
The Mac handoff document at `docs/daily/2026-05-25-windows-pivot.md`
recommended WSL2 + Linux toolchain; the Windows session evaluated this
and chose **native Windows** instead because:

| Concern | Native Windows verdict |
|---|---|
| Existing bash scripts | git-bash (bundled with Git for Windows) runs them unmodified |
| ffmpeg with NVENC | BtbN's pre-built `ffmpeg-n8.1.1-...-win64-gpl-8.1` has CUDA, NVENC, libass, whisper, libplacebo |
| ComfyUI for local AI | Portable Windows release is the standard install (better support than WSL2 GPU passthrough) |
| File I/O speed | Native > WSL2 ↔ Windows boundary (relevant for 600+ clip renders) |
| Disk install footprint | ComfyUI portable ~6GB, WSL2 + Ubuntu ~15-30GB additional |

WSL2 remains a viable alternative; this doc only covers the native path.

## Quick install (G: drive recommended for storage capacity)

Tested install paths assume a fresh Windows 11 machine with Python 3.10+,
Git for Windows, and an NVIDIA driver supporting CUDA 12.6+.

### 1. Run the bundled setup script

From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/windows/setup-env.ps1
```

This creates `G:\tools\`, `G:\config\`, `G:\ai\models\` directories and
sets persistent user env vars:

- `YT_CRED_DIR`, `YT_SECRETS`, `YT_CACHE` (consumed by `scripts/yt-stats-collect.sh`, `scripts/yt-batch-upload.sh`)
- `HF_HOME`, `TRANSFORMERS_CACHE`, `TORCH_HOME`, `PIP_CACHE_DIR` (redirect away from C:)
- `FFMPEG_HWACCEL=nvenc` (consumed by Mix #2 build scripts)

Override the default drive by setting `MELONS_TOOLS_DIR` / `MELONS_CONFIG_DIR`
/ `MELONS_MODELS_DIR` before running.

### 2. Install the toolchain binaries

Drop these into `G:\tools\<name>\` and they'll be on PATH automatically
(setup script added the paths):

| Tool | Where to get | Filename in install dir |
|---|---|---|
| **ffmpeg + ffprobe** | [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases) — pick `*-win64-gpl-8.1.zip` | `ffmpeg.exe`, `ffprobe.exe` |
| **jq** | [jqlang/jq releases](https://github.com/jqlang/jq/releases) — `jq-windows-amd64.exe` | `jq.exe` (rename from above) |
| **yt-dlp** | [yt-dlp/yt-dlp releases](https://github.com/yt-dlp/yt-dlp/releases/latest) — `yt-dlp.exe` | `yt-dlp.exe` |
| **youtubeuploader** | [porjo/youtubeuploader releases](https://github.com/porjo/youtubeuploader/releases) — `*_Windows_amd64.zip` | `youtubeuploader.exe` |

Verify after install (new shell):

```powershell
Get-Command ffmpeg, ffprobe, jq, yt-dlp, youtubeuploader | Format-Table Name, Source
```

### 3. ComfyUI portable (for AI video work — Mix #2)

```powershell
# Download ComfyUI portable NVIDIA build from
# https://github.com/comfyanonymous/ComfyUI/releases (latest)
# Extract to G:\ai\ComfyUI_windows_portable\ (built-in 7z handled by Windows tar)
tar -xf ComfyUI_windows_portable_nvidia.7z -C G:\ai\
```

First boot:

```powershell
G:\ai\ComfyUI_windows_portable\run_nvidia_gpu.bat
```

Default URL: `http://127.0.0.1:8188`.  Manager is in
`custom_nodes/ComfyUI-Manager/` (clone separately if missing).

For Mix #2 work specifically, also download:

| Model | URL | Place under |
|---|---|---|
| LTX-Video 2B v0.9.5 | `https://huggingface.co/Lightricks/LTX-Video/resolve/main/ltx-video-2b-v0.9.5.safetensors` | `ComfyUI/models/checkpoints/` |
| T5-XXL fp8 (encoder) | `https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/t5xxl_fp8_e4m3fn.safetensors` | `ComfyUI/models/text_encoders/` |

## 24/7 always-on operation

If the machine needs to run unattended for overnight renders, apply
these once (admin required for some):

```powershell
# As admin
powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  # High Performance
powercfg /change standby-timeout-ac 0
powercfg /change hibernate-timeout-ac 0
powercfg /change disk-timeout-ac 0
powercfg /hibernate off   # recovers ~16-25 GB on C:
# Disable USB selective suspend
powercfg /setacvalueindex SCHEME_CURRENT SUB_NONE 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0
powercfg /setactive SCHEME_CURRENT
# Block Windows Update auto-restart while logged in
New-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" -Name "NoAutoRebootWithLoggedOnUsers" -Value 1 -PropertyType DWord -Force
```

Optional but recommended:
- BIOS: "Restore on AC Power Loss" → Power On (auto-restart after power blip)
- UPS for short outage tolerance
- Auto-login if Windows Update or power event reboots the machine

## Capability differences vs macOS

| Capability | macOS | Linux | **Windows** |
|---|---|---|---|
| Mission execution (`agents/missions/*/run.sh`) | ✓ native bash | ✓ native bash | ✓ via git-bash |
| ffmpeg hwaccel default | `h264_videotoolbox` | `h264_nvenc` if NVIDIA, else `libx264` | `h264_nvenc` if NVIDIA, else `h264_qsv` if Intel, else `libx264` |
| TTS (faceless-short narration) | macOS `say` | `edge-tts` or `Kokoro-ONNX` | `edge-tts` (cross-platform) |
| Scheduler (auditor / yt-stats daily) | `launchd` plists | `systemd` timers | **Task Scheduler** (manual setup currently) |
| `bootstrap.sh` first-touch wizard | ✓ tested | best-effort | **not yet adapted** — use `scripts/windows/setup-env.ps1` |
| ComfyUI / local AI video (LTX-Video) | works but slower (no NVENC) | works on NVIDIA Linux | **primary platform** for this workload |

## Known limitations on Windows

- `bootstrap.sh` / `first-touch.sh` use macOS `say` for synthetic fixtures.  Windows users should skip the fixture generation and use real CC sources (Blender CDN / Wikimedia) directly.
- `launchd` plists in `scripts/com.melons.agents.*.plist.template` are macOS-specific.  Windows users must register equivalent Task Scheduler jobs (TODO: `scripts/windows/install-scheduler.ps1`).
- macOS `Yuna` voice (Korean fallback) is not available on Windows.  Use `edge-tts` with `ko-KR-SunHiNeural` voice instead (TTS_BACKEND=edge-tts).
- `h264_videotoolbox` is macOS-only.  Existing scripts should branch on `FFMPEG_HWACCEL` env var; until they do, Windows users may need to override the encoder flag manually.

## Adding a new Windows-specific script

Scripts that only run on Windows live under `scripts/windows/`.  Reasons
to add a script there rather than the top-level `scripts/`:

- Uses Task Scheduler / `schtasks` / Windows registry / `Set-ItemProperty`
- Uses PowerShell-only features (`.ps1`)
- Calls Windows-native binaries that have no POSIX equivalent

Scripts in the top-level `scripts/` directory must work on macOS + Linux
+ Windows (via git-bash for `.sh`, via Python interpreter for `.py`).

## Validation status

| Component | Validated on Windows 2026-05-25 |
|---|---|
| Native Python 3.10 + git + winget | ✓ Pre-installed |
| ffmpeg n8.1.1 (CUDA + NVENC + libass + libplacebo + whisper) | ✓ NVENC encode test passed |
| jq 1.7.1, yt-dlp 2026.03.17, youtubeuploader 1.25.5 | ✓ All on PATH |
| ComfyUI v0.22.0 (LTX-Video native nodes) | ✓ Booted, served on 127.0.0.1:8188 |
| LTX-Video 2B v0.9.5 (img2vid) | ✓ 4-sec clip rendered in 27s on 4070 Ti SUPER |
| YouTube OAuth (`yt-stats-collect.sh`) | ✗ Pending — operator-provided `client_secrets.json` from Mac |
| `agents/missions/music-video/run.sh` end-to-end via git-bash | ✗ Untested on Windows |
| `bootstrap.sh` / `first-touch.sh` | ✗ macOS-specific, Windows skip |

## See also

- [`platform-linux.md`](platform-linux.md) (TODO: mirror this for Linux — current Linux refs are scattered across README)
- [`mix-2-design.md`](mix-2-design.md) — Windows-only Mix #2 longform pipeline
- [`daily/2026-05-25-windows-pivot.md`](daily/2026-05-25-windows-pivot.md) — Mac→Windows pivot handoff
