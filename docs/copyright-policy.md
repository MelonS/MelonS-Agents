# Copyright & Attribution Policy

Every short produced by this system either (a) credits its source on the rendered video itself or (b) refuses to render. There is no "publish first, sort it out later" mode.

## What ships with every output

1. **Burned source-attribution watermark** — top-left corner of the rendered short.
   - Pulled from `source_attribution` (config/fixtures.yaml) or derived from the input URL when the source is fetched live.
   - Implemented in `ffmpeg_render_short` (see `agents/lib/ffmpeg.sh`); cannot be silently disabled.
2. **`outputs/SOURCES.txt`** — machine-readable record of the source URL, license, and the attribution string that was burned in. Each mission folder has its own.
3. **`summary.md`** — human-readable mission summary now ends with a `## Source & license` block.

## Fixture-level constraints (already enforced)

- `config/fixtures.yaml` rejects any entry without a `license` field.
- The downloader (`scripts/fetch-fixtures.sh`) only follows links the catalog already vetted; ad-hoc URLs require adding them to the catalog first.

## TODO — automated copyright filter (not yet implemented)

When we start fetching from broader sources (user-supplied URLs, social platforms), the following gates need to land before publishing is wired up:

- [ ] **Domain allowlist** — reject sources outside a known-permissive set (Blender open movies, CC-licensed archives, user's own uploads) until reviewed.
- [ ] **License-string probe** — for sources that expose machine-readable license metadata (Wikimedia, Internet Archive, Vimeo CC channel), pull it and store under `resources/license.json`.
- [ ] **Audio-fingerprint check** — run a local fingerprinter (e.g., chromaprint/`fpcalc`) and refuse to render if the soundtrack matches a known commercial dataset. Avoids re-uploading a copyrighted song over neutral footage.
- [ ] **Logo / watermark detection** — refuse renders where the source frame already carries another creator's logo/handle in the area we'd overlay; surfaces a warning so the user can pick a different source.
- [ ] **Per-platform reuse rules** — separate "okay for archival re-edit" from "okay to repost commercially"; today everything is treated as "internal demo only".
- [ ] **Publish-gate hook** — block any future `publish.sh` from running on a mission whose `outputs/SOURCES.txt` is missing a `license:` line.
- [ ] **Strike-record log** — if a published short ever gets a takedown notice, append the mission id + URL + reason to `records/strikes.log` so the same source is auto-rejected next time.

## Until those are in place

- All missions run as **internal demo / personal review only**. Do not upload outputs to public platforms.
- If you do want to publish a single output manually, open `outputs/SOURCES.txt` first and verify the license permits the redistribution you have in mind (CC-BY = credit required; CC-NC = no commercial repost; etc.).

## Why this lives in code, not in a wiki

A policy that lives only in docs gets forgotten the first time someone is in a hurry. The watermark is in the render path; `SOURCES.txt` is in the mission template; the fixture catalog rejects unlicensed entries at parse time. The TODOs above mark where that same "make it impossible to forget" treatment still needs to happen.
