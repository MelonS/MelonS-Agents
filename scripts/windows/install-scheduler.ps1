# install-scheduler.ps1 — Windows counterpart of scripts/install-scheduler.sh
# (launchd is macOS-only; primary production moved to this Windows box on
# 2026-05-25, which left the auditor's L3 daily baseline dark for 39 days —
# see docs/audit/2026-07-03-all.md [critical]).
#
# Registers Task Scheduler jobs (current user, no elevation needed):
#   MelonS-auditor-daily — L3 daily all-focus audit at 03:00
#     (L1 = git post-commit hook via scripts/install-hooks.sh, per-checkout.
#      L2 15-min anomaly poll is intentionally NOT registered on Windows:
#      each poll is a full claude CLI invocation; at 96/day the cost/value
#      is poor vs L1+L3 coverage. Revisit if unattended autonomous runs
#      return to this machine.)
#
# install uses schtasks.exe pointed at the spaceless run-audit.cmd wrapper
# (direct /TR quoting of "Program Files" + nested bash -lc is unreliable
# across locales, and Register-ScheduledTask CIM binding proved flaky here);
# status/remove use the ScheduledTasks module cmdlets, which are stable.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\windows\install-scheduler.ps1          # install
#   powershell -ExecutionPolicy Bypass -File scripts\windows\install-scheduler.ps1 status   # show
#   powershell -ExecutionPolicy Bypass -File scripts\windows\install-scheduler.ps1 remove   # uninstall
param([string]$Action = "install")

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)  # scripts/windows -> repo root
$GitBash  = "C:\Program Files\Git\usr\bin\bash.exe"
$TaskName = "MelonS-auditor-daily"
# The task command is scripts/windows/run-audit.cmd — a spaceless wrapper —
# because schtasks /TR quoting of "Program Files" + nested bash -lc strings
# is unreliable, and Register-ScheduledTask CIM binding proved flaky here.
$RunCmd = Join-Path $RepoRoot "scripts\windows\run-audit.cmd"

switch ($Action) {
  "install" {
    if (-not (Test-Path $GitBash)) { throw "Git Bash not found at $GitBash" }
    if (-not (Test-Path $RunCmd)) { throw "wrapper not found: $RunCmd" }
    schtasks /Create /TN $TaskName /SC DAILY /ST 03:00 /TR $RunCmd /F | Out-Null
    Write-Host "installed: $TaskName (daily 03:00) -> $RunCmd"
    schtasks /Query /TN $TaskName /FO LIST | Select-String -Pattern ":"
  }
  "status" {
    $t = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($t) {
      $i = $t | Get-ScheduledTaskInfo
      Write-Host "[$TaskName] State=$($t.State) NextRun=$($i.NextRunTime) LastRun=$($i.LastRunTime) LastResult=$($i.LastTaskResult)"
    } else { Write-Host "[$TaskName] NOT installed" }
  }
  "remove" {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "removed: $TaskName"
  }
  default { throw "unknown action: $Action (install|status|remove)" }
}
