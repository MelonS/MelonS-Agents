#!/usr/bin/env bash
# Consistent logging helpers. Source after env.sh.

log_info()  { printf '\033[36m[%s]\033[0m %s\n' "$(date +%H:%M:%S)" "$*" >&2; }
log_ok()    { printf '\033[32m[%s]\033[0m ✓ %s\n' "$(date +%H:%M:%S)" "$*" >&2; }
log_warn()  { printf '\033[33m[%s]\033[0m ⚠ %s\n' "$(date +%H:%M:%S)" "$*" >&2; }
log_err()   { printf '\033[31m[%s]\033[0m ✗ %s\n' "$(date +%H:%M:%S)" "$*" >&2; }
log_step()  { printf '\n\033[1;35m[%s] ▸ %s\033[0m\n' "$(date +%H:%M:%S)" "$*" >&2; }
