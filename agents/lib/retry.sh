#!/usr/bin/env bash
# Shared QA-feedback retry primitives.
#
# Per-mission flow:
#   1. Run the model-driven stage(s) — selection, summarization, etc.
#   2. Run QA.
#   3. If VERDICT=FAIL and attempts remain, the next iteration prepends a
#      "previous attempt failed" block to the model prompt with the
#      qa-report.md contents inline.  Cap is QA_RETRY_MAX (default 2 → up
#      to 3 total attempts including the first).
#   4. After the cap is exhausted, write a halt log to
#      records/blockers/<ISO-date>/<mission-id>.md so the failure is
#      surfaced without polluting records/missions/.
#
# The library is intentionally minimal: each mission decides what its
# "model-driven stage" is and how to fold QA_FEEDBACK into its prompt.
# This file owns only the bookkeeping (counters, feedback extraction,
# blocker writer) so the per-mission run.sh files stay readable.

: "${QA_RETRY_MAX:=2}"   # number of *retries* after the first attempt — 0 disables.

# Read the most recent QA report from a mission directory and return the
# section a re-prompt should care about (verdict + acceptance criteria).
# Echoes nothing if no report exists yet.
qa_extract_feedback() {
  local report="$1"
  [[ -f "$report" ]] || return 0
  # Strip the stray "stage_mark" leak that used to land at the top of
  # highlight's qa-report.md (harmless if absent).
  awk '
    /^stage_mark/ { next }
    /^## (Selection|Source)/ { exit }
    { print }
  ' "$report"
}

# Write a blocker record under $RECORDS_DIR/blockers/<ISO-date>/.  Called
# once the retry budget is exhausted.  Per CLAUDE.md the records/ tree is
# gitignored so this stays local — but the mission's own records still
# include qa-report.md, so the failure trail is preserved.
qa_write_blocker() {
  local mission_id="$1" mdir="$2" attempts="$3"
  local blockers_dir="$RECORDS_DIR/blockers/$(date +%Y-%m-%d)"
  mkdir -p "$blockers_dir"
  local out="$blockers_dir/$mission_id.md"
  {
    echo "# Blocker — $mission_id"
    echo
    echo "**Mission dir**: \`$mdir\`"
    echo "**Attempts**: $attempts (QA_RETRY_MAX=$QA_RETRY_MAX)"
    echo "**Halted at**: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
    echo
    echo "## Final QA report"
    echo
    if [[ -f "$mdir/qa-report.md" ]]; then
      cat "$mdir/qa-report.md"
    else
      echo "_(no qa-report.md produced)_"
    fi
  } > "$out"
  echo "$out"
}

# Render the prompt-fragment that a mission can prepend to its model
# prompt on retry attempts. Empty on attempt 1 (no prior report yet) or
# when QA_FEEDBACK env var is unset / empty.
qa_feedback_block() {
  [[ -n "${QA_FEEDBACK:-}" ]] || return 0
  cat <<MDEOF

---

PREVIOUS ATTEMPT FAILED. The QA verdict was FAIL with these criteria:

$QA_FEEDBACK

Take this feedback into account and produce a different result that
passes all FAIL criteria. Do not repeat the same selection / output.

---

MDEOF
}
