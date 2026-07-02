#!/usr/bin/env bash
# news-screen.sh — deterministic news-safety screen for a research.json.
#
# The 리서치팀 (research-team) tags a news candidate with category + risk_flags;
# this script proves the parts bash CAN prove against
# config/news-category-tiers.yaml, before any render money is spent:
#
#   C1 rot-words   : 속보/실시간/방금… in hook/angle/topic/script_seed → BLOCK
#                    (they become false within 48h — date facts absolutely)
#   C2 red flags   : risk_flags[] ∩ red_risk_flags → BLOCK (fail-closed)
#   C3 category    : research.json "category" in red tier → BLOCK,
#                    yellow tier → WARN, green → pass, missing/unknown → WARN
#   C4 recency     : recency.ok != true → BLOCK (news profile only)
#   C5 sourcing    : any claim with 0 fact_source_urls → BLOCK; 1 → WARN
#                    (deterministic proxy for the 2-source / official-primary rule)
#
# The JUDGMENT half (defamation nuance, 비방 목적, official-primary quality)
# stays with legal-team — this screen can only block earlier, never pass later.
#
# Usage:   scripts/news-screen.sh <research.json> [--profile=news|info] [--in-place]
# Output:  summary to stderr; with --in-place, stamps `news_screen` into the JSON.
# Exit:    0 pass · 3 warnings · 4 blocked · 64 usage
set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/log.sh" 2>/dev/null || { log_warn(){ echo "⚠ $*" >&2; }; log_info(){ echo "  $*" >&2; }; log_err(){ echo "✗ $*" >&2; }; }
export PYTHONUTF8=1

RESEARCH="${1:-}"; PROFILE="news"; IN_PLACE=0
shift 2>/dev/null || true
for arg in "$@"; do
  case "$arg" in
    --profile=*) PROFILE="${arg#*=}" ;;
    --in-place)  IN_PLACE=1 ;;
  esac
done
TIERS="$REPO_ROOT/config/news-category-tiers.yaml"
[[ -f "$RESEARCH" ]] || { log_err "usage: $0 <research.json> [--profile=news] [--in-place]"; exit 64; }
[[ -f "$TIERS" ]] || { log_err "missing $TIERS"; exit 64; }

OUT="$(python3 - "$RESEARCH" "$TIERS" "$PROFILE" <<'PYEOF'
import json, re, sys

research_path, tiers_path, profile = sys.argv[1], sys.argv[2], sys.argv[3]
d = json.load(open(research_path, encoding="utf-8"))
yaml_text = open(tiers_path, encoding="utf-8").read()

# Minimal YAML pulls (same no-dependency convention as the smoke tests):
# top-level "key:" list sections of "  - item" lines, and "key: value" scalars.
def yaml_list(section):
    items, in_sect = [], False
    for line in yaml_text.splitlines():
        if re.match(rf"^{re.escape(section)}:\s*(#.*)?$", line):
            in_sect = True; continue
        if in_sect:
            m = re.match(r"^\s+-\s*([^#]+?)\s*(#.*)?$", line)
            if m: items.append(m.group(1).strip()); continue
            if line.strip() and not line.startswith(" "): break
    return items

def tier_list(name):
    items, in_tiers, in_name = [], False, False
    for line in yaml_text.splitlines():
        if re.match(r"^tiers:\s*(#.*)?$", line): in_tiers = True; continue
        if in_tiers:
            if line.strip() and not line.startswith(" "): break
            m = re.match(rf"^\s\s{re.escape(name)}:\s*(#.*)?$", line)
            if m: in_name = True; continue
            if re.match(r"^\s\s[a-z]+:", line): in_name = False
            m = re.match(r"^\s+-\s*([^#]+?)\s*(#.*)?$", line)
            if m and in_name: items.append(m.group(1).strip())
    return items

def yaml_int(key, default):
    m = re.search(rf"^\s*{re.escape(key)}:\s*(\d+)", yaml_text, re.M)
    return int(m.group(1)) if m else default

rot_words   = yaml_list("rot_words")
red_flags   = set(yaml_list("red_risk_flags"))
green, yellow, red = set(tier_list("green")), set(tier_list("yellow")), set(tier_list("red"))
min_block   = yaml_int("min_sources_block", 1)
min_warn    = yaml_int("min_sources_warn", 2)

findings, verdict = [], "pass"
def hit(level, check, msg):
    global verdict
    findings.append({"level": level, "check": check, "msg": msg})
    if level == "block": verdict = "block"
    elif level == "warn" and verdict != "block": verdict = "warn"

# C1 rot-words in narrative fields
text_fields = {k: str(d.get(k, "")) for k in ("hook", "angle", "topic", "script_seed")}
for field, text in text_fields.items():
    for w in rot_words:
        if w and w in text:
            hit("block", "rot-words", f"'{w}' in {field} — use an absolute date instead")

# C2 red risk_flags (fail-closed)
for f in d.get("risk_flags", []) or []:
    if str(f).strip() in red_flags:
        hit("block", "red-risk-flag", f"risk_flags contains '{f}'")

# C3 category tier
cat = str(d.get("category", "")).strip()
if not cat:
    hit("warn", "category", "no category tag — research-team should tag one from news-category-tiers.yaml")
elif cat in red:
    hit("block", "category", f"category '{cat}' is RED tier")
elif cat in yellow:
    hit("warn", "category", f"category '{cat}' is YELLOW tier — needs ≥2 sources / official primary + as-of date")
elif cat not in green:
    hit("warn", "category", f"category '{cat}' not in tiers — treat as yellow until tiered")

# C4 recency (news only)
if profile == "news":
    rec = d.get("recency") or {}
    if rec.get("ok") is not True:
        hit("block", "recency", f"recency.ok={rec.get('ok')!r} — stale or unstamped news")

# C5 per-claim sourcing floor
for i, c in enumerate(d.get("claims", []) or []):
    n = len(c.get("fact_source_urls", []) or [])
    txt = str(c.get("text", ""))[:40]
    if n < min_block:
        hit("block", "sourcing", f"claim[{i}] '{txt}…' has {n} sources (<{min_block})")
    elif n < min_warn:
        hit("warn", "sourcing", f"claim[{i}] '{txt}…' single-source — must be an official primary")

d["news_screen"] = {"verdict": verdict, "profile": profile, "findings": findings}
print(json.dumps(d, ensure_ascii=False, indent=2))
print(json.dumps({"verdict": verdict, "findings": findings}, ensure_ascii=False), file=sys.stderr)
PYEOF
)"
RC=$?
[[ $RC -ne 0 ]] && { log_err "news-screen: python screen failed"; exit 64; }

VERDICT="$(printf '%s' "$OUT" | python3 -c 'import json,sys;print(json.load(sys.stdin)["news_screen"]["verdict"])')"
if [[ $IN_PLACE -eq 1 ]]; then
  printf '%s\n' "$OUT" > "$RESEARCH"
else
  printf '%s\n' "$OUT"
fi

N_BLOCK="$(printf '%s' "$OUT" | python3 -c 'import json,sys;d=json.load(sys.stdin);print(sum(1 for f in d["news_screen"]["findings"] if f["level"]=="block"))')"
N_WARN="$(printf '%s' "$OUT" | python3 -c 'import json,sys;d=json.load(sys.stdin);print(sum(1 for f in d["news_screen"]["findings"] if f["level"]=="warn"))')"
log_info "news-screen: verdict=$VERDICT (block=$N_BLOCK warn=$N_WARN)"
case "$VERDICT" in
  pass)  exit 0 ;;
  warn)  exit 3 ;;
  block) exit 4 ;;
esac
