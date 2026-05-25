# MelonS-Agents Windows setup — env vars + tool path registration.
#
# Idempotent: safe to re-run.  Run once per Windows machine after cloning.
# Equivalent to macOS/Linux `agents/lib/env.sh` auto-detection — but on
# Windows we persist the choices via [Environment]::SetEnvironmentVariable
# at User scope so all future shells (PowerShell, cmd, git-bash) pick them up.
#
# Why this file exists:
#   - macOS/Linux users `source .env` or rely on $HOME/.config conventions.
#   - Windows has no equivalent default — env vars must be set explicitly.
#   - Without G: drive redirects, HuggingFace + pip caches default to
#     %USERPROFILE% on C: drive which fills up fast for AI workflows.
#
# What it does:
#   1. Picks a "tools" directory (default G:\tools, fallback C:\tools).
#   2. Picks a "config" directory for OAuth credentials.
#   3. Picks a "models" directory for HuggingFace / Torch / Transformers cache.
#   4. Sets persistent User env vars (no admin required).
#   5. Adds tool dirs to PATH if not present.
#
# Run:  powershell -ExecutionPolicy Bypass -File scripts/windows/setup-env.ps1
# Override defaults:
#   $env:MELONS_TOOLS_DIR  = "D:\tools"
#   $env:MELONS_CONFIG_DIR = "D:\config"
#   $env:MELONS_MODELS_DIR = "D:\ai\models"

$ErrorActionPreference = "Stop"

# ---- Defaults (override via env before running) -----------------------------
function Pick-Dir {
  param([string]$EnvVar, [string]$Default)
  $val = [Environment]::GetEnvironmentVariable($EnvVar)
  if ([string]::IsNullOrEmpty($val)) { return $Default }
  return $val
}

$ToolsDir   = Pick-Dir "MELONS_TOOLS_DIR"  "G:\tools"
$ConfigDir  = Pick-Dir "MELONS_CONFIG_DIR" "G:\config"
$ModelsDir  = Pick-Dir "MELONS_MODELS_DIR" "G:\ai\models"
$PipCache   = Pick-Dir "MELONS_PIP_CACHE"  "G:\ai\pip-cache"

# Fallback to C:\ if G:\ doesn't exist
if (-not (Test-Path "G:\")) {
  $ToolsDir  = $ToolsDir  -replace "^G:", "C:"
  $ConfigDir = $ConfigDir -replace "^G:", "C:"
  $ModelsDir = $ModelsDir -replace "^G:", "C:"
  $PipCache  = $PipCache  -replace "^G:", "C:"
  Write-Host "G: drive not found — falling back to C:" -ForegroundColor Yellow
}

Write-Host "=== MelonS-Agents Windows env setup ===" -ForegroundColor Cyan
Write-Host "Tools  : $ToolsDir"
Write-Host "Config : $ConfigDir"
Write-Host "Models : $ModelsDir"
Write-Host ""

# ---- Create directories -----------------------------------------------------
$dirs = @(
  $ToolsDir,
  $ConfigDir,
  (Join-Path $ConfigDir "youtubeuploader"),
  $ModelsDir,
  (Join-Path $ModelsDir "huggingface"),
  (Join-Path $ModelsDir "huggingface\transformers"),
  (Join-Path $ModelsDir "torch"),
  $PipCache
)
foreach ($d in $dirs) {
  if (-not (Test-Path $d)) {
    New-Item -ItemType Directory -Path $d -Force | Out-Null
    Write-Host "  created: $d"
  }
}

# ---- Persistent env vars (User scope) ---------------------------------------
function Set-UserEnv {
  param([string]$Name, [string]$Value)
  $current = [Environment]::GetEnvironmentVariable($Name, "User")
  if ($current -eq $Value) {
    Write-Host "  $Name already set"
  } else {
    [Environment]::SetEnvironmentVariable($Name, $Value, "User")
    Write-Host "  $Name = $Value" -ForegroundColor Green
  }
  # Apply to current session too
  Set-Item -Path "Env:$Name" -Value $Value
}

Write-Host ""
Write-Host "=== Setting user env vars ===" -ForegroundColor Cyan

# YouTube tooling (compat with scripts/yt-stats-collect.sh + yt-batch-upload.sh)
$ytCred = Join-Path $ConfigDir "youtubeuploader"
Set-UserEnv "YT_CRED_DIR"        $ytCred
Set-UserEnv "YT_SECRETS"         (Join-Path $ytCred "client_secrets.json")
Set-UserEnv "YT_CACHE"           (Join-Path $ytCred "request.token")

# HuggingFace + Torch caches (redirect away from %USERPROFILE%)
Set-UserEnv "HF_HOME"            (Join-Path $ModelsDir "huggingface")
Set-UserEnv "TRANSFORMERS_CACHE" (Join-Path $ModelsDir "huggingface\transformers")
Set-UserEnv "TORCH_HOME"         (Join-Path $ModelsDir "torch")
Set-UserEnv "PIP_CACHE_DIR"      $PipCache

# Hardware accel preference (consumed by mix2-build.py + future ffmpeg wrappers)
Set-UserEnv "FFMPEG_HWACCEL"     "nvenc"

# TTS backend (n/a for music-video work but defines the cross-platform contract)
$existingTts = [Environment]::GetEnvironmentVariable("TTS_BACKEND", "User")
if ([string]::IsNullOrEmpty($existingTts)) {
  Set-UserEnv "TTS_BACKEND"      "edge-tts"
}

# ---- Add tool dirs to PATH --------------------------------------------------
Write-Host ""
Write-Host "=== Tool PATH registration ===" -ForegroundColor Cyan

$toolSubdirs = @(
  (Join-Path $ToolsDir "ffmpeg"),
  (Join-Path $ToolsDir "jq"),
  (Join-Path $ToolsDir "yt-dlp"),
  (Join-Path $ToolsDir "youtubeuploader")
)

$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ([string]::IsNullOrEmpty($userPath)) { $userPath = "" }
$added = @()
foreach ($t in $toolSubdirs) {
  if ($userPath -like "*$t*") {
    Write-Host "  PATH already contains $t"
  } else {
    if ([string]::IsNullOrEmpty($userPath)) { $userPath = $t } else { $userPath = "$userPath;$t" }
    $added += $t
  }
}
if ($added.Count -gt 0) {
  [Environment]::SetEnvironmentVariable("PATH", $userPath, "User")
  foreach ($a in $added) { Write-Host "  added: $a" -ForegroundColor Green }
  # Apply to current session
  $env:PATH = "$($env:PATH);" + ($added -join ";")
}

# ---- Tool availability summary ---------------------------------------------
Write-Host ""
Write-Host "=== Tool availability check ===" -ForegroundColor Cyan
$tools = @{
  "ffmpeg"          = "GPU encode + libass + whisper (n8.1+ recommended)"
  "ffprobe"         = "(bundled with ffmpeg)"
  "jq"              = "JSON parsing in YT scripts (1.6+)"
  "yt-dlp"          = "YouTube source / metadata download (2024+)"
  "youtubeuploader" = "OAuth upload binary (porjo/youtubeuploader 1.25+)"
  "python"          = "3.10+ — Mix #2 pipeline + ComfyUI integration"
  "git"             = "any modern version"
}
foreach ($k in $tools.Keys) {
  $cmd = Get-Command $k -ErrorAction SilentlyContinue
  if ($cmd) {
    Write-Host ("  OK  {0,-18}  {1}" -f $k, $cmd.Source) -ForegroundColor Green
  } else {
    Write-Host ("  MISS {0,-18}  {1}" -f $k, $tools[$k]) -ForegroundColor Yellow
  }
}

Write-Host ""
Write-Host "Setup complete. Restart shells to pick up PATH changes." -ForegroundColor Cyan
Write-Host "Next: see docs/platform-windows.md for tool install pointers."
