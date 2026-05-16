# Onboarding evidence

Files in this directory are proof that the clone-and-go path works on
a clean tree, not just on the maintainer's machine.  They exist
because `docs/goal.md` requires every goal to have a **deliverable
subgoal** — a concrete artifact that has to land for the goal to
count.

## `fresh-clone-log.txt`

Append-only log written by [`scripts/test-fresh-clone.sh`](../../scripts/test-fresh-clone.sh).
Each line records one end-to-end clone-and-go run:

```
<ISO timestamp>  PASS  remote=<url>  short=<path>  size=<MB>
<ISO timestamp>  FAIL  remote=<url>  reason="<one-line cause>"
```

What "PASS" means: the simulator cloned the repo into a temp dir,
ran `scripts/bootstrap.sh` (auto-creating `.env`, checking tools,
fetching the whisper model + ollama model), ran one highlight
mission against the Sintel CC-BY-3.0 trailer, and a non-trivial
`short.mp4` (≥ 1 MB) landed under the temp `records/` tree.

What it does **not** test: tool installation from scratch.  The
host must already have ffmpeg (with libass), whisper-cli, ollama
running, yt-dlp, and curl on PATH — the simulator only verifies
the path *after* the prerequisites are satisfied.  Bootstrap prints
explicit install commands for anything missing.

Re-run any time with:

```bash
scripts/test-fresh-clone.sh
```

Set `FRESH_CLONE_REMOTE` to test against a different fork or a
local path; set `FRESH_CLONE_KEEP=1` to leave the temp workdir in
place for inspection.
