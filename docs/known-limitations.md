# Known limitations (resolved)

## ~~Captions not burned into video~~ → resolved

Now using a static ffmpeg build from evermeet.cx with libass enabled.
The brew bottle is intentionally minimal; the static binary lives at
`~/.local/opt/ffmpeg-static/ffmpeg` and is referenced via `FFMPEG_BIN`
in `.env`.

Fresh-machine install:

```bash
mkdir -p ~/.local/opt/ffmpeg-static
curl -fsSL -o /tmp/ff.zip  https://evermeet.cx/ffmpeg/getrelease/ffmpeg/zip
curl -fsSL -o /tmp/fp.zip  https://evermeet.cx/ffmpeg/getrelease/ffprobe/zip
unzip -o /tmp/ff.zip -d ~/.local/opt/ffmpeg-static
unzip -o /tmp/fp.zip -d ~/.local/opt/ffmpeg-static
chmod +x ~/.local/opt/ffmpeg-static/{ffmpeg,ffprobe}
xattr -d com.apple.quarantine ~/.local/opt/ffmpeg-static/{ffmpeg,ffprobe} || true
```

Then point `.env`:

```
FFMPEG_BIN=$HOME/.local/opt/ffmpeg-static/ffmpeg
FFPROBE_BIN=$HOME/.local/opt/ffmpeg-static/ffprobe
```
