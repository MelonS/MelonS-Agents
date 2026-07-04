@echo off
rem Task Scheduler entrypoint — one stats sample per invocation.
set REPO=%~dp0..\..
"C:\Program Files\Git\usr\bin\bash.exe" -lc "cd \"$(cygpath -u '%REPO%')\" && bash scripts/yt-stats-sample.sh >> records/_blackhole/sampler.log 2>&1"
