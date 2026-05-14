# Known limitations

## Captions not burned into video (PoC)

The Homebrew `ffmpeg` 8.1.1 bottle (default formula) is built without
`libass` (subtitles filter) and without `libfreetype/fontconfig`
(drawtext filter). Burning SRT into the MP4 therefore fails with
"No such filter" or "No option name near 'captions.srt'".

**Current behavior**: the pipeline ships a separate `captions.srt`
file alongside the MP4. Players that support SRT can load it
externally.

**Resolution paths** (any one is enough):

1. `brew tap homebrew-ffmpeg/ffmpeg && brew install homebrew-ffmpeg/ffmpeg/ffmpeg`
   — a community tap that builds ffmpeg with libass etc.
2. Install a static ffmpeg build (e.g. from https://www.osxexperts.net/)
   and point `FFMPEG_BIN` at it.
3. Build ffmpeg from source with `--enable-libass --enable-libfreetype
   --enable-libfontconfig`.

After picking one, swap `ffmpeg_burn_srt` in `agents/lib/ffmpeg.sh`
back to the subtitles filter.
