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

## Automated copyright filter — status

### Shipped (2026-05-15)

- [x] **Domain allowlist** — `config/copyright-allowlist.yaml` lists permissive domains (Blender, Xiph, Internet Archive, Wikimedia). `check_source_allowed` in `agents/lib/copyright.sh` is called at the top of every mission's `run.sh`; non-allowlisted URLs are refused with exit code 67. Local file paths bypass (fixture catalog handles them).
- [x] **Publish-gate hook** — `scripts/publish-gate.sh <mission-dir>` reads `outputs/SOURCES.txt` and refuses to greenlight publishing if the license is empty, `unknown`, `requires-per-item-probe`, or listed `publish_blocked: true` in the allowlist. Stub today; the moment a real `publish.sh` lands, it should call this as its first action.
- [x] **Strike-record log** — `append_strike(mission_id, url, reason)` in `agents/lib/copyright.sh` writes tab-separated rows to `records/strikes.log`.
- [x] **Strike-aware source rejection** — `check_source_allowed` consults the strike log *before* the allowlist. A URL with any row in `records/strikes.log` is refused (exit code 6), even if its domain is on the allowlist. The refusal message includes the original strike row so the operator can see when and why it was logged.
- [x] **License-string probe** — `probe_license(url, out_json)` in `agents/lib/copyright.sh` reads machine-readable license metadata from `archive.org/metadata/<id>` and `commons.wikimedia.org/w/api.php?action=query&prop=imageinfo&iiprop=extmetadata`. Maps CC license URLs / shortcodes onto canonical tags (e.g. `CC-BY-3.0`). `resolve_final_license(url, allowlist_license, mdir)` glues it into the mission flow: when the allowlist says `requires-per-item-probe`, the probe runs, populates `FIXTURE_LICENSE`, and writes `resources/license.json`. Verified end-to-end against `BigBuckBunny_124` on archive.org → CC-BY-3.0 → publish gate accepts.

### Still TODO
- [ ] **Audio-fingerprint check** — `chromaprint`/`fpcalc`-based detection of copyrighted soundtracks. Skipped for v1 because it needs a fingerprint database to compare against; without one the check is just CPU burn. Add this when we have a real takedown to learn from.
- [ ] **Logo / watermark detection** — frame-level check for other creators' logos in the area we'd overlay our own watermark. Heavy (needs OCR or a trained model); deferred until we hit the failure mode it would catch.
- [ ] **Per-platform reuse rules** — `config/copyright-allowlist.yaml` has a `publish_rules` section with per-license rules (`commercial_repost`, `require_attribution`, `share_alike`) but no code reads it yet beyond the binary publish-blocked check.

## Until those are in place

- All missions run as **internal demo / personal review only**. Do not upload outputs to public platforms.
- If you do want to publish a single output manually, open `outputs/SOURCES.txt` first and verify the license permits the redistribution you have in mind (CC-BY = credit required; CC-NC = no commercial repost; etc.).

## Why this lives in code, not in a wiki

A policy that lives only in docs gets forgotten the first time someone is in a hurry. The watermark is in the render path; `SOURCES.txt` is in the mission template; the fixture catalog rejects unlicensed entries at parse time. The TODOs above mark where that same "make it impossible to forget" treatment still needs to happen.
