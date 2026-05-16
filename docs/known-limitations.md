# Known limitations (resolved log)

A short log of historical issues that have been resolved.  Kept so
that future-me (or a stranger reading the repo) can see what
problems have already been worked through and what the current
state is.

## ~~Captions not burned into video~~ → resolved

Caption burn-in needs an ffmpeg built with `--enable-libass`.  This
has gone through two solutions; the current default is the second.

### Current default (2026-05-16 onward) — `ffmpeg-full` from Homebrew

Homebrew split the `ffmpeg` formula: the regular bottle no longer
includes libass, and the libass-enabled build moved to a separate
keg-only formula called `ffmpeg-full`.  `agents/lib/env.sh` now
walks a candidate list and prefers any ffmpeg whose `-version`
mentions libass — including the keg path
`/opt/homebrew/opt/ffmpeg-full/bin/ffmpeg` — without requiring an
`.env` override.

Install:

```bash
brew install ffmpeg-full
# (env.sh auto-discovers the keg path; no .env edit needed)
```

Verify:

```bash
scripts/bootstrap.sh   # the libass check passes and the rest of
                       # the bootstrap continues
```

### Alternative — static build from evermeet.cx

Still a valid alternative if you want a self-contained binary that
doesn't pull in the ffmpeg-full keg's ~46 dependencies:

```bash
mkdir -p "$HOME/.local/opt/ffmpeg-static"
curl -fsSL -o /tmp/ff.zip  https://evermeet.cx/ffmpeg/getrelease/ffmpeg/zip
curl -fsSL -o /tmp/fp.zip  https://evermeet.cx/ffmpeg/getrelease/ffprobe/zip
unzip -o /tmp/ff.zip -d "$HOME/.local/opt/ffmpeg-static"
unzip -o /tmp/fp.zip -d "$HOME/.local/opt/ffmpeg-static"
chmod +x "$HOME/.local/opt/ffmpeg-static/"{ffmpeg,ffprobe}
xattr -d com.apple.quarantine "$HOME/.local/opt/ffmpeg-static/"{ffmpeg,ffprobe} || true
```

Then point `.env` at it explicitly:

```
FFMPEG_BIN=$HOME/.local/opt/ffmpeg-static/ffmpeg
FFPROBE_BIN=$HOME/.local/opt/ffmpeg-static/ffprobe
```

The explicit `.env` override skips env.sh's auto-discovery.

### Detection

`scripts/bootstrap.sh` runs an `ffmpeg -version | grep libass`
check on whichever ffmpeg env.sh resolved to.  If libass is
missing, bootstrap exits non-zero and prints the OS-specific
install hint (the `brew install ffmpeg-full` line on macOS, the
distro `apt install ffmpeg` line on Linux).
