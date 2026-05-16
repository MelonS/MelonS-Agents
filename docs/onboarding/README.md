# Onboarding evidence

Files in this directory are proof that the clone-and-go path works on
a clean tree, not just on the maintainer's machine.  They exist
because `docs/goal.md` requires every goal to have a **deliverable
subgoal** — a concrete artifact that has to land for the goal to
count.

## `fresh-clone-log.txt`

Append-only log written by three reinforcement test scripts.  Each
script records its own `variant=` tag so the log can be filtered.

```
<ISO timestamp>  PASS  variant=basic                   remote=<url>  short=<path>  size=<MB>
<ISO timestamp>  PASS  variant=force-model-download    remote=<url>  short=<path>  size=<MB>  model_download=<MB>
<ISO timestamp>  PASS  variant=bootstrap-hints         asserts=<n>/<n>
<ISO timestamp>  PASS  variant=linux-docker            asserts=<n>/<n>  base=ubuntu:24.04
<ISO timestamp>  FAIL  variant=<v>                     reason="<one-line cause>"
```

**Variants**:

- `basic` — `scripts/test-fresh-clone.sh` — clones the public repo,
  runs bootstrap + one highlight mission, asserts `short.mp4` ≥ 1 MB.
  The standard reproducibility check.
- `force-model-download` — same script with
  `FRESH_CLONE_FORCE_MODEL_DOWNLOAD=1`.  Overrides `WHISPER_MODEL` to
  a fresh temp path inside the clone so bootstrap calls
  `fetch-whisper-model.sh` and actually downloads ggml-small.bin
  (~465 MB).  Validates the model-fetch path the basic variant
  skips when the host already has a cached model.
- `bootstrap-hints` — `scripts/test-bootstrap-hints.sh` — runs
  bootstrap under `env -i` with `PATH=/usr/bin:/bin` so the
  `command -v` discovery comes up empty for whisper-cli / ollama /
  yt-dlp.  Asserts each is flagged missing AND each gets the
  matching macOS install hint.  Does not require network beyond
  the initial clone.
- `linux-docker` — `scripts/test-fresh-clone-linux.sh` — runs
  bootstrap inside an `ubuntu:24.04` Docker container with
  apt-installed ffmpeg / yt-dlp / git / curl.  Asserts the bootstrap
  picks the Linux branch (no macOS install-hint leaks) and that
  apt-supplied ffmpeg's libass check passes.  Does NOT run a full
  mission inside the container (no ollama / whisper-cli available
  there).  Validates the Platform-support claim's Linux side.

What `PASS` means per variant is described above.  Re-run the
suite (or any subset) with:

```bash
scripts/test-fresh-clone.sh                          # basic
FRESH_CLONE_FORCE_MODEL_DOWNLOAD=1 \
  scripts/test-fresh-clone.sh                        # force-model-download
scripts/test-bootstrap-hints.sh                      # bootstrap-hints (macOS host)
scripts/test-fresh-clone-linux.sh                    # linux-docker (needs Docker daemon)
```

Useful env vars:
- `FRESH_CLONE_REMOTE` / `BOOTSTRAP_HINTS_REMOTE` /
  `LINUX_FRESH_CLONE_REMOTE` — override the clone URL (test a
  fork or a local path).
- `FRESH_CLONE_KEEP=1` — leave the temp workdir in place after
  the basic / force-model-download variants exit.
