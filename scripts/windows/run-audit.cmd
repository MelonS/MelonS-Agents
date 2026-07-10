@echo off
rem run-audit.cmd - Task Scheduler entrypoint for the L3 daily audit.
rem Exists so the scheduled task's command is a single spaceless path
rem (schtasks /TR quoting of "Program Files" + nested bash -lc is fragile).
rem Resolves the repo from its own location; logs to records/ (gitignored).
set REPO=%~dp0..\..
"C:\Program Files\Git\usr\bin\bash.exe" -lc "cd \"$(cygpath -u '%REPO%')\" && bash scripts/audit-run.sh all >> records/audit-scheduler.log 2>&1"
