# Music sources

This file tracks the provenance + license of each track sitting in
`assets/music/` (the audio files themselves are gitignored — see
[`README.md`](README.md)).

The intent is **transparency for the operator**: if a track here gets
used in a published short, the operator can look up its license
without re-checking the source.  Audit trail for license-question
incidents later.

---

## Tracks

### `Velvet Turntable1.mp3`

- **Source**: Suno AI (operator-generated, 2026-05-17)
- **Suno tier at generation**: free tier
- **License**: Suno Terms of Service — free-tier generations are
  licensed for **personal, non-commercial use**.  Commercial use
  (including monetized YouTube revenue) requires a Suno Pro / Premier
  subscription, retroactively applied or active at upload time.
- **License URL**: https://suno.com/terms
- **Operator confirmation**: track was confirmed "대만족" on the v5
  prototype (decision log entry "Operator pick — 2026-05-17").
- **Safe upload contexts**: non-monetized YouTube Shorts on a new
  channel (where monetization is structurally impossible without
  meeting 1k subs + 4k watch-hour or 10M Shorts-views thresholds).
  Operator decides per-upload whether the channel/use is "personal" or
  "commercial".
- **Not safe**: republishing as standalone audio, selling, or any use
  that monetizes the track itself.

### `Velvet Turntable2.mp3`

- **Source**: Suno AI (operator-generated, 2026-05-17)
- Same license terms as `Velvet Turntable1.mp3`.

### `Late Night Piano.mp3`

- **Source**: Suno AI (operator-generated, 2026-05-17 evening)
- **Suno tier at generation**: free tier (Custom Mode)
- **Style prompt**: "late night jazz lofi, soft piano, vinyl crackle, slow tempo, 60 BPM, mellow, intimate"
- **License**: Suno free-tier (personal / non-commercial; see Velvet Turntable1.mp3 entry).
- **Duration**: 118 s
- **Intended visual mood**: late-night cafe, dim warm lamps, piano-keys close-ups, raindrops on glass.

### `Rainy Bossa.mp3`

- **Source**: Suno AI (operator-generated, 2026-05-17 evening)
- **Suno tier at generation**: free tier (Custom Mode)
- **Style prompt**: "rainy day bossa nova lofi, brushed drums, warm tenor sax, smooth, 75 BPM, melancholic"
- **License**: Suno free-tier.
- **Duration**: 152 s
- **Intended visual mood**: rainy neon street, raindrops on window, jazz-cafe rainy day, wet pavement reflections.

### `Fireplace Acoustic.mp3`

- **Source**: Suno AI (operator-generated, 2026-05-17 evening)
- **Suno tier at generation**: free tier (Custom Mode)
- **Style prompt**: "cozy fireplace acoustic jazz, fingerpicked nylon guitar, soft upright bass, 70 BPM, warm, contemplative"
- **License**: Suno free-tier.
- **Duration**: 166 s
- **Intended visual mood**: fireplace warm glow, cozy cabin interior, candle warm light, snowy forest evening.

### `Urban Midnight.mp3`

- **Source**: Suno AI (operator-generated, 2026-05-17 evening)
- **Suno tier at generation**: free tier (Custom Mode)
- **Style prompt**: "urban midnight lofi hip-hop jazz, deep sub bass, muted trumpet, jazz drums, 85 BPM, noir, cinematic"
- **License**: Suno free-tier.
- **Duration**: 480 s
- **Intended visual mood**: tokyo neon night, urban rooftop, neon alleyway, midnight subway, traffic light streaks.

### `Tokyo Neon.mp3`

- **Source**: Suno AI (operator-generated, 2026-05-17 evening)
- **Suno tier at generation**: free tier (Custom Mode)
- **Style prompt**: "tokyo neon vibraphone bossa, soft vibes, dreamy, 65 BPM, nostalgic, japanese city pop influence"
- **License**: Suno free-tier.
- **Duration**: 233 s
- **Intended visual mood**: tokyo shibuya night, japanese neon signs, vibraphone close-ups, akihabara, japanese vending machine glow.

---

## How to add a track

1. Drop the audio file into `assets/music/` (gitignored).
2. Append a new section to this file with the same fields as above.
3. If the track has different terms (e.g. YouTube Audio Library is
   attribution-required for some tracks), spell out the attribution
   string verbatim so the upload step can include it in the video
   description.

## License-clean alternatives (for when Suno free-tier limits matter)

- **YouTube Audio Library** — `studio.youtube.com → Audio Library`.
  Free, commercial-use clean for YouTube uploads.  Attribution required
  for some tracks (clearly marked in the UI).
- **Pixabay Music** — `pixabay.com/music`.  Pixabay Content License,
  attribution recommended but not required.  Web-only download (no API).
- **Jamendo** — `jamendo.com`.  Many CC-licensed tracks; commercial
  use requires Jamendo Pro subscription for some.
- **Free Music Archive** — `freemusicarchive.org`.  Per-track licenses
  (CC0 / CC-BY / etc.) listed on track page.

The shortlist above is documented in [`README.md`](README.md) as the
recommended fetch order.
