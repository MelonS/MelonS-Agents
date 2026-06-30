#!/usr/bin/env bash
# legal-gate.sh — the 법률팀 (legal-team) deterministic gate.
#
# Two halves of the legal review meet here:
#   1. DETERMINISTIC checks bash can prove (run here):
#        - media-license       → agents/lib/copyright.sh::guard_publish
#        - required-disclaimer  → required disclosure lines present
#        - synthetic-disclosure → synthetic-narration disclosure present
#        - fan-content-disclaimer → "unofficial / not affiliated" line present (idol)
#   2. JUDGMENT checks that need a reasoning model (supplied by the
#      legal-team subagent as --external-verdict):
#        - fact-accuracy, defamation, unverifiable, trademark-ip,
#          portrait-publicity-rights, media-rights-reuse (idol)
#
# This script MERGES the two into the profile's `required_checks` set and
# computes the final verdict. A required judgment check with no external
# determination is treated as REVISE (fail-closed) — we never PASS a check
# we did not actually run.
#
# Usage:
#   scripts/legal-gate.sh <mission_dir> --profile=<info|news|idol> \
#     [--platform=public|internal-demo] [--external-verdict=<file.json>]
#
# Output:
#   <mission_dir>/legal/legal-verdict.json   (merged, authoritative)
# Exit: 0 PASS · 1 REVISE · 2 BLOCK · 64 usage · 65 bad mission dir

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/log.sh" 2>/dev/null || {
  log_step(){ echo "→ $*"; }; log_ok(){ echo "✓ $*"; }
  log_info(){ echo "  $*"; }; log_warn(){ echo "⚠ $*" >&2; }; log_err(){ echo "❌ $*" >&2; }
}
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/copyright.sh" 2>/dev/null || true
# UTF-8 mode: Korean checks/disclosures must survive Windows cp949 stdout.
export PYTHONUTF8=1
# env.sh turns on `set -e`; this gate INTENTIONALLY inspects non-zero return
# codes (guard_publish exits 4-8 by design), so relax it back — we do our own
# error handling and propagate the verdict via the final exit code.
set +e

MDIR="${1:-}"; shift || true
PROFILE=""; PLATFORM="public"; EXTERNAL_VERDICT=""
for arg in "$@"; do
  case "$arg" in
    --profile=*)          PROFILE="${arg#*=}" ;;
    --platform=*)         PLATFORM="${arg#*=}" ;;
    --external-verdict=*) EXTERNAL_VERDICT="${arg#*=}" ;;
    *) log_warn "legal-gate: ignoring unknown arg '$arg'" ;;
  esac
done

if [[ -z "$MDIR" || -z "$PROFILE" ]]; then
  log_err "usage: $0 <mission_dir> --profile=<info|news|idol> [--platform=...] [--external-verdict=...]"
  exit 64
fi
if [[ ! -d "$MDIR/outputs" ]]; then
  log_err "mission dir missing outputs/: $MDIR"
  exit 65
fi
# If a verdict was named but can't be read, say so loudly — otherwise the
# subagent's judgment is silently dropped and every judgment check fail-closes
# to REVISE, which looks like "the legal team rejected it" when really the file
# path was wrong.
if [[ -n "$EXTERNAL_VERDICT" && ! -f "$EXTERNAL_VERDICT" ]]; then
  log_warn "external verdict not found: '$EXTERNAL_VERDICT' — judgment checks will fail-closed to REVISE"
fi

PROFILES_YAML="$REPO_ROOT/config/content-short-profiles.yaml"
LEGAL_DIR="$MDIR/legal"
mkdir -p "$LEGAL_DIR"
SOURCES_TXT="$MDIR/outputs/SOURCES.txt"
DISCLOSURES_TXT="$MDIR/outputs/disclosures.txt"
VERDICT_OUT="$LEGAL_DIR/legal-verdict.json"

log_step "법률팀 gate — profile=$PROFILE platform=$PLATFORM"

# ── Deterministic check 1: media-license (guard_publish) ─────────────────
MEDIA_STATUS="fail"; MEDIA_EVIDENCE="SOURCES.txt missing"
if [[ -f "$SOURCES_TXT" ]]; then
  if declare -f guard_publish >/dev/null 2>&1; then
    GP_OUT="$(guard_publish "$SOURCES_TXT" "$PLATFORM" 2>&1)"; GP_RC=$?
  else
    GP_OUT="copyright.sh not sourced"; GP_RC=99
  fi
  if [[ $GP_RC -eq 0 ]]; then
    MEDIA_STATUS="pass"; MEDIA_EVIDENCE="guard_publish exit 0 (platform=$PLATFORM)"
  else
    MEDIA_STATUS="fail"; MEDIA_EVIDENCE="guard_publish exit $GP_RC: ${GP_OUT##*$'\n'}"
  fi
fi
log_info "media-license: $MEDIA_STATUS — $MEDIA_EVIDENCE"

# ── Deterministic check 2/3: required disclosure lines present ───────────
# run.sh writes outputs/disclosures.txt (one required line per row, with
# {AS_OF_DATE} already substituted). Each profile's required disclosure lines
# come from config/content-short-profiles.yaml.
DISCLOSURE_STATUS="pass"; DISCLOSURE_EVIDENCE="all required disclosure lines present"
SYNTH_STATUS="n/a"; SYNTH_EVIDENCE="profile has no synthetic-disclosure requirement"

# ── Merge everything in python (read profile reqs + external verdict) ────
python3 - "$PROFILES_YAML" "$PROFILE" "$DISCLOSURES_TXT" "$EXTERNAL_VERDICT" \
         "$MEDIA_STATUS" "$MEDIA_EVIDENCE" "$VERDICT_OUT" <<'PY'
import sys, json, re, os

prof_yaml, profile, disclosures_txt, ext_path, media_status, media_evidence, out_path = sys.argv[1:8]

# --- minimal flat-YAML read of the requested profile -----------------------
def read_profile(path, want):
    text = open(path, encoding="utf-8").read()
    blocks, cur, in_list = [], None, False
    for line in text.splitlines():
        if re.match(r"^profiles:", line):
            in_list = True; continue
        if not in_list:
            continue
        m = re.match(r"^  - id:\s*(.+)$", line)
        if m:
            if cur: blocks.append(cur)
            cur = {"id": m.group(1).strip(), "required_checks": [], "disclosures": []}
            cur["_section"] = None
            continue
        if cur is None:
            continue
        if re.match(r"^    required_checks:\s*$", line):
            cur["_section"] = "required_checks"; continue
        if re.match(r"^    disclosures:\s*$", line):
            cur["_section"] = "disclosures"; continue
        m = re.match(r"^      -\s*(.+)$", line)
        if m and cur.get("_section"):
            val = m.group(1).strip().strip('"')
            cur[cur["_section"]].append(val); continue
        m = re.match(r"^    ([a-z_]+):\s*(.*)$", line)
        if m:
            cur["_section"] = None
            cur[m.group(1)] = m.group(2).strip().strip('"')
    if cur: blocks.append(cur)
    for b in blocks:
        if b["id"] == want:
            return b
    return None

prof = read_profile(prof_yaml, profile) or {"id": profile, "required_checks":
        ["media-license", "fact-accuracy", "required-disclaimer"], "disclosures": []}
required = prof.get("required_checks", [])

# --- disclosure presence (deterministic) -----------------------------------
present = ""
if disclosures_txt and os.path.isfile(disclosures_txt):
    present = open(disclosures_txt, encoding="utf-8").read()

def disclosure_present(line):
    # {AS_OF_DATE} is already substituted in disclosures.txt; match on the
    # static prefix so the date doesn't break the comparison.
    stem = re.split(r"\{|\}", line)[0].strip()
    return stem and stem in present

missing = [d for d in prof.get("disclosures", []) if not disclosure_present(d)]
disclosure_status = "pass" if not missing else "fail"
disclosure_evidence = ("all required disclosure lines present" if not missing
                       else "missing: " + " | ".join(missing))

# synthetic-disclosure: idol/real-subject profiles must carry a synthetic-
# narration disclosure line (the narrator voice is AI, even though the subject
# is a real person — viewers must not mistake it for the artist speaking).
synth_required = "synthetic-disclosure" in required
synth_status = "n/a"
synth_evidence = "profile has no synthetic-disclosure requirement"
if synth_required:
    has_synth = ("AI 합성" in present) or ("Synthetic AI" in present) or ("synthetic" in present.lower())
    synth_status = "pass" if has_synth else "fail"
    synth_evidence = ("synthetic-narration disclosure present" if has_synth
                      else "synthetic-narration disclosure line missing")

# fan-content-disclaimer: real-subject (idol/artist) profiles must carry an
# "unofficial fan-made, not affiliated" line so viewers + the agency can tell
# this apart from official content.
fan_required = "fan-content-disclaimer" in required
fan_status = "n/a"
fan_evidence = "profile has no fan-content-disclaimer requirement"
if fan_required:
    low = present.lower()
    has_fan = ("비공식 팬" in present) or ("팬 제작" in present) or ("unofficial fan" in low) or ("fan-made" in low) or ("not affiliated" in low)
    fan_status = "pass" if has_fan else "fail"
    fan_evidence = ("fan-content disclaimer present" if has_fan
                    else "fan-content disclaimer (unofficial / not affiliated) missing")

# --- deterministic check results -------------------------------------------
det = {
    "media-license":        {"status": media_status, "evidence": media_evidence},
    "required-disclaimer":  {"status": disclosure_status, "evidence": disclosure_evidence},
}
if synth_required:
    det["synthetic-disclosure"] = {"status": synth_status, "evidence": synth_evidence}
if fan_required:
    det["fan-content-disclaimer"] = {"status": fan_status, "evidence": fan_evidence}

# --- external (subagent) judgment checks -----------------------------------
ext_checks = {}
ext_fixes = []
ext_iter = 1
if ext_path and os.path.isfile(ext_path):
    try:
        ext = json.load(open(ext_path, encoding="utf-8"))
        for c in ext.get("checks", []):
            if c.get("id"):
                ext_checks[c["id"]] = {"status": c.get("status", "unknown"),
                                       "evidence": c.get("evidence", "")}
        ext_fixes = ext.get("required_fixes", [])
        ext_iter = ext.get("iteration", 1)
    except Exception as e:
        ext_checks["_parse_error"] = {"status": "fail", "evidence": f"external verdict unreadable: {e}"}

# --- merge over the profile's required set ---------------------------------
# When BOTH the deterministic gate and the subagent assessed the same check
# (e.g. required-disclaimer: the gate proves the disclosure LINES are present,
# the subagent judges whether a medical/financial 'not advice' line is also
# needed), keep the WORSE of the two — neither side may silently override the
# other's failure. The deterministic result is a floor, not a ceiling.
_RANK = {"fail": 0, "unknown": 1, "warn": 2, "pass": 3, "n/a": 3}
def worse(a, b):
    ra, rb = _RANK.get(a.get("status"), 1), _RANK.get(b.get("status"), 1)
    chosen, other = (a, b) if ra <= rb else (b, a)
    ev, oev = chosen.get("evidence", ""), other.get("evidence", "")
    if oev and oev not in ev:
        ev = (ev + " | also: " + oev).strip(" |")
    return {"status": chosen.get("status"), "evidence": ev}

merged = []
for cid in required:
    if cid in det and cid in ext_checks:
        merged.append({"id": cid, **worse(det[cid], ext_checks[cid])})
    elif cid in det:
        merged.append({"id": cid, **det[cid]})
    elif cid in ext_checks:
        merged.append({"id": cid, **ext_checks[cid]})
    else:
        # required but nobody determined it → fail-closed
        merged.append({"id": cid, "status": "unknown",
                       "evidence": "no determination (subagent did not assess) — fail-closed to REVISE"})

# carry any extra external checks not in the required set (informational)
for cid, c in ext_checks.items():
    if cid.startswith("_"):
        merged.append({"id": cid, **c}); continue
    if cid not in required:
        merged.append({"id": cid, **c, "informational": True})

# --- compute verdict --------------------------------------------------------
blocking = [c for c in merged if not c.get("informational")]
def is_pass(c): return c["status"] in ("pass", "warn", "n/a")
fails   = [c for c in blocking if c["status"] == "fail"]
unknown = [c for c in blocking if c["status"] == "unknown"]

# BLOCK if a hard, unfixable failure: media-license fail (struck/forbidden) or
# defamation fail. Otherwise REVISE if any fail/unknown. Else PASS.
hard_block_ids = {"media-license", "defamation"}
hard = [c for c in fails if c["id"] in hard_block_ids]
if hard and not ext_fixes:
    verdict = "BLOCK"
elif fails or unknown:
    verdict = "REVISE"
else:
    verdict = "PASS"

result = {
    "verdict": verdict,
    "iteration": ext_iter,
    "profile": profile,
    "checks": [{k: v for k, v in c.items()} for c in merged],
    "required_fixes": ext_fixes,
    "deterministic_source": "scripts/legal-gate.sh",
}
os.makedirs(os.path.dirname(out_path), exist_ok=True)
json.dump(result, open(out_path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)

# stdout summary for the shell
print(verdict)
for c in blocking:
    print(f"  [{c['status']:7}] {c['id']}: {c['evidence']}")
PY
PY_RC=$?

if [[ $PY_RC -ne 0 ]]; then
  log_err "legal-gate: merge step failed (python exit $PY_RC)"
  exit 2
fi

VERDICT="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["verdict"])' "$VERDICT_OUT" 2>/dev/null || echo BLOCK)"
log_step "verdict: $VERDICT → $VERDICT_OUT"

case "$VERDICT" in
  PASS)   log_ok "법률팀 PASS — clear for 출시팀";              exit 0 ;;
  REVISE) log_warn "법률팀 REVISE — fix list returned to 제작팀"; exit 1 ;;
  *)      log_err "법률팀 BLOCK — cannot publish";              exit 2 ;;
esac
