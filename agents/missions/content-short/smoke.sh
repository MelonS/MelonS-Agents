#!/usr/bin/env bash
# Smoke test for the content-shorts pipeline's DETERMINISTIC logic.
#
# Covers the parts that don't need ffmpeg/ollama/whisper/Pexels (those belong to
# the wrapped faceless-short core, already tested separately):
#   - profile + subject YAML reads (incl. narrator-voice resolution)
#   - scripts/legal-gate.sh verdict logic (REVISE fail-closed / PASS / BLOCK,
#     plus the idol fan-content + synthetic-disclosure path)
#   - scripts/research-screen.sh media-source license screening
#
# Runnable on any box with bash + python3 (no media tools needed).
#
# Usage:  agents/missions/content-short/smoke.sh
# Exit:   0 all pass · 1 one or more failed

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"
export PYTHONUTF8=1

PY="${PYTHON_BIN:-python3}"
command -v "$PY" >/dev/null 2>&1 || { echo "SKIP: no python3 (set PYTHON_BIN)"; exit 0; }
# Guard against the broken Windows App Store python3 stub.
"$PY" -c 'import sys; sys.exit(0)' >/dev/null 2>&1 || { echo "SKIP: python3 not functional"; exit 0; }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
export RECORDS_DIR="$TMP/records"; mkdir -p "$RECORDS_DIR"

PASS=0; FAIL=0
ok()   { echo "  ✓ $1"; PASS=$((PASS+1)); }
bad()  { echo "  ✗ $1"; FAIL=$((FAIL+1)); }
assert_eq() { [[ "$2" == "$3" ]] && ok "$1 ($2)" || bad "$1 (got '$2', want '$3')"; }

# Generate disclosures.txt from a profile EXACTLY as run.sh does, so the test
# stays in sync with config/content-short-profiles.yaml (no hardcoded strings
# that could drift from the profile's em-dash / Korean text).
gen_disclosures() { # profile as_of -> stdout
  "$PY" - config/content-short-profiles.yaml "$1" "$2" <<'PYEOF'
import sys, re
path, want, as_of = sys.argv[1], sys.argv[2], sys.argv[3]
text=open(path,encoding="utf-8").read()
cur=None; sect=None; lines=[]; in_list=False
for line in text.splitlines():
    if re.match(r"^profiles:", line): in_list=True; continue
    if not in_list: continue
    m=re.match(r"^  - id:\s*(.+)$", line)
    if m: cur=m.group(1).strip(); sect=None; continue
    if cur!=want: continue
    if re.match(r"^    disclosures:\s*$", line): sect="d"; continue
    if re.match(r"^    [a-z_]+:", line): sect=None
    m=re.match(r"^      -\s*(.+)$", line)
    if m and sect=="d":
        v=m.group(1).strip()
        mq=re.match(r'"((?:[^"\\]|\\.)*)"', v)
        v=mq.group(1) if mq else re.split(r"\s+#", v, 1)[0].strip()
        lines.append(v)
for l in lines: print(l.replace("{AS_OF_DATE}", as_of))
PYEOF
}

mkmission() { # dir license disclosure_profile
  local d="$1"; mkdir -p "$d/outputs"
  printf 'mission_id: smoke\nsource: https://www.pexels.com/\nattribution: Pexels\nlicense: %s\nrecorded_at: 2026-01-01T00:00:00Z\n' "$2" > "$d/outputs/SOURCES.txt"
  gen_disclosures "$3" "2026-01-01" > "$d/outputs/disclosures.txt"
}
verdict() { "$PY" -c 'import json,sys;print(json.load(open(sys.argv[1],encoding="utf-8"))["verdict"])' "$1/legal/legal-verdict.json" 2>/dev/null || echo NOFILE; }

echo "== profile reads =="
assert_eq "idol subject narrator voice (example subject)" \
  "$(awk '$0 ~ /^  voice:/ {l=$0;sub(/^[^:]*:[[:space:]]*/,"",l);if(l~/^"/){sub(/^"/,"",l);sub(/".*$/,"",l)}else{sub(/[[:space:]]*#.*$/,"",l);sub(/[[:space:]]+$/,"",l)}print l;exit}' config/subjects/example.yaml)" \
  "ko-KR-SunHiNeural"

echo "== legal-gate verdicts =="
mkmission "$TMP/A" pexels-license info
bash scripts/legal-gate.sh "$TMP/A" --profile=info --platform=public >/dev/null 2>&1
assert_eq "info, no judgment -> REVISE (fail-closed)" "$(verdict "$TMP/A")" "REVISE"

mkmission "$TMP/B" pexels-license info
printf '{"iteration":1,"checks":[{"id":"fact-accuracy","status":"pass","evidence":"ok"},{"id":"unverifiable","status":"warn","evidence":"ok"}],"required_fixes":[]}' > "$TMP/B.json"
bash scripts/legal-gate.sh "$TMP/B" --profile=info --platform=public --external-verdict="$TMP/B.json" >/dev/null 2>&1
assert_eq "info + passing judgment -> PASS" "$(verdict "$TMP/B")" "PASS"

mkmission "$TMP/C" unknown info
bash scripts/legal-gate.sh "$TMP/C" --profile=info --platform=public >/dev/null 2>&1
assert_eq "license=unknown -> BLOCK" "$(verdict "$TMP/C")" "BLOCK"

mkmission "$TMP/D" pexels-license info
bash scripts/legal-gate.sh "$TMP/D" --profile=idol --platform=public >/dev/null 2>&1
assert_eq "idol, missing fan/synthetic disclaimers -> REVISE" "$(verdict "$TMP/D")" "REVISE"

mkmission "$TMP/E" pexels-license idol
printf '{"iteration":2,"checks":[{"id":"fact-accuracy","status":"pass","evidence":"ok"},{"id":"unverifiable","status":"pass","evidence":"ok"},{"id":"defamation","status":"pass","evidence":"official info only"},{"id":"portrait-publicity-rights","status":"pass","evidence":"no member imagery"},{"id":"media-rights-reuse","status":"pass","evidence":"generic B-roll only, no group audio"}],"required_fixes":[]}' > "$TMP/E.json"
bash scripts/legal-gate.sh "$TMP/E" --profile=idol --platform=public --external-verdict="$TMP/E.json" >/dev/null 2>&1
assert_eq "idol, full clearance -> PASS" "$(verdict "$TMP/E")" "PASS"

# worse-of-both: deterministic required-disclaimer=pass (lines present) but the
# subagent marks required-disclaimer=fail (e.g. medical 'not advice' missing).
# The merge must keep the WORSE (fail) → REVISE, not let det override to pass.
mkmission "$TMP/F" pexels-license info
printf '{"iteration":1,"checks":[{"id":"fact-accuracy","status":"pass","evidence":"ok"},{"id":"unverifiable","status":"pass","evidence":"ok"},{"id":"required-disclaimer","status":"fail","evidence":"medical topic needs a not-advice line"}],"required_fixes":[{"target":"disclosure","instruction":"add not-advice line","blocking":true}]}' > "$TMP/F.json"
bash scripts/legal-gate.sh "$TMP/F" --profile=info --platform=public --external-verdict="$TMP/F.json" >/dev/null 2>&1
assert_eq "required-disclaimer worse-of (det pass + subagent fail) -> REVISE" "$(verdict "$TMP/F")" "REVISE"

echo "== research-screen media license screening =="
cat > "$TMP/r.json" <<'JSON'
{ "profile":"info","topic":"t","angle":"a","hook":"h",
  "fact_sources":[{"url":"https://apnews.com/x","title":"T","publisher":"AP","date":"2026-01-01","kind":"news","key_facts":["f"]}],
  "media_sources":[
    {"url":"https://videos.pexels.com/video-files/1/c.mp4","intended_use":"w1"},
    {"url":"https://random.example.com/c.mp4","intended_use":"w2"}],
  "claims":[{"text":"c","fact_source_urls":["https://apnews.com/x"],"confidence":"high"}],
  "recency":{"required_within_days":3,"newest_source_date":"2026-01-01","ok":true},
  "visual_terms":["t one"],"risk_flags":[] }
JSON
bash scripts/research-screen.sh "$TMP/r.json" --in-place >/dev/null 2>&1
RSC=$?
assert_eq "research-screen exit (one blocked)" "$RSC" "3"
assert_eq "pexels media -> allowed" \
  "$("$PY" -c 'import json,sys;print(json.load(open(sys.argv[1],encoding="utf-8"))["media_sources"][0]["license_screen"])' "$TMP/r.json")" "allowed"
assert_eq "example.com media -> blocked" \
  "$("$PY" -c 'import json,sys;print(json.load(open(sys.argv[1],encoding="utf-8"))["media_sources"][1]["license_screen"])' "$TMP/r.json")" "blocked"
assert_eq "fact_source untouched (not license-gated)" \
  "$("$PY" -c 'import json,sys;d=json.load(open(sys.argv[1],encoding="utf-8"));print("license_screen" in d["fact_sources"][0])' "$TMP/r.json")" "False"

echo "== stage orchestration (--mission-dir threading, no re-render) =="
# Fake an ALREADY-PRODUCED mission (skip the faceless render — not under test here).
PM="$TMP/PM/content-info-orch-000000"; mkdir -p "$PM/outputs" "$PM/legal" "$PM/resources"
echo "dummy-mp4" > "$PM/outputs/short.mp4"
printf 'mission_id: orch\nsource: https://www.pexels.com/\nattribution: Pexels\nlicense: pexels-license\nrecorded_at: 2026-01-01T00:00:00Z\n' > "$PM/outputs/SOURCES.txt"
gen_disclosures info 2026-01-01 > "$PM/outputs/disclosures.txt"
printf '{"iteration":1,"checks":[{"id":"fact-accuracy","status":"pass","evidence":"ok"},{"id":"unverifiable","status":"pass","evidence":"ok"}],"required_fixes":[]}' > "$PM/legal/subagent-verdict.json"

# legal stage on the produced dir → PASS, and must NOT have re-rendered (the
# dummy short.mp4 stays byte-identical: no faceless run replaced it).
bash agents/missions/content-short/run.sh orch --profile=info --stage=legal \
  --mission-dir="$PM" --legal-verdict="$PM/legal/subagent-verdict.json" --platform=public >/dev/null 2>&1
assert_eq "stage=legal on produced dir -> PASS" "$(verdict "$PM")" "PASS"
assert_eq "stage=legal did NOT re-render (short.mp4 untouched)" "$(cat "$PM/outputs/short.mp4")" "dummy-mp4"

# legal stage with no produced short → require_produced refuses (exit 65),
# instead of the old behavior of silently re-running produce.
EMPTYD="$TMP/EMPTY"; mkdir -p "$EMPTYD"
bash agents/missions/content-short/run.sh orch --profile=info --stage=legal --mission-dir="$EMPTYD" >/dev/null 2>&1
assert_eq "stage=legal without produced short -> refuse (65)" "$?" "65"

# release stage on the PASS dir → assembles the package without re-rendering.
bash agents/missions/content-short/run.sh orch --profile=info --stage=release --mission-dir="$PM" >/dev/null 2>&1
assert_eq "stage=release builds PUBLISH-CHECKLIST" \
  "$([[ -f "$PM/release/PUBLISH-CHECKLIST.md" ]] && echo yes || echo no)" "yes"

echo
echo "== content-short smoke: $PASS passed, $FAIL failed =="
[[ $FAIL -eq 0 ]]
