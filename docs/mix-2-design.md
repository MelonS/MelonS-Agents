# Mix #2 design — Windows + LTX-Video + NVENC

> Long-form YouTube music mix #2. Direct successor to Mix #1
> (`yt-mix-1-korean-lofi-rainy-seoul`, YT id `9SqgNBKk5JE`,
> 44 min, 2026-05-24).  Mix #1 operator complaint: **"화면이 너무
> 단조로움 심각할정도로"** (visual monotony, severe).  This document
> is the design for the second attempt — paused on Mac after
> operator decision to pivot to Windows + GPU + open-source local
> AI video.  Authored in the Windows session 2026-05-25 evening
> per autonomous-decisions log entry of the same date.

## TL;DR

| Field | Mix #1 (existing) | Mix #2 (this design) |
|---|---|---|
| Duration | 44 min (2685s) | ~40-45 min (similar) |
| Audio | 12 instrumental Suno tracks concatenated, no crossfade | 8-10 instrumental tracks, acrossfade 1-2s between tracks |
| Motion source | Pollinations.ai still + ffmpeg `zoompan` (fake Ken Burns) | **LTX-Video img2vid 5-sec clips** (real motion) |
| Encoder | libx264 medium CRF 22 @ 3.8 Mbps | **h264_nvenc CQ 19 @ 12-15 Mbps** |
| Visual diversity | Single style, ~150 stills, low variety | Sub-mood matrix × time-of-day progression × per-track style shift |
| Source images | Pollinations.ai (rate-limited 1/min, network dependent) | Local SDXL/Flux via ComfyUI (unlimited, free, faster) |
| AI hands | Sometimes visible (grotesque) | **Forbidden** — environment / silhouette / distance prompts only |
| YT upload | Direct public | **Private → 24h → Public** (algorithm sees pre-analyzed video) |
| Bitrate cap | None (libx264 chose) | NVENC capped at 15 Mbps, target 12 Mbps avg |

## Operator constraints (committed to memory and applied here)

### Music-video 6 directives (2026-05-22)

| # | Directive | Mix #2 application |
|---|---|---|
| 1 | B-roll 영상끼리 재사용 금지 | Mix #1 의 Pollinations 스틸 / clips 재사용 X. 모든 비주얼은 새로 생성. dedup registry (`records/youtube/broll-used.txt`) consult 필수. |
| 2 | shader 절제 (blanket apply 금지) | per-segment `shader_active_ratio` 적용. 평탄 구간은 shader off. 운영자 review 후 결정. |
| 3 | lyric sync ±200ms | n/a — Mix #2 는 instrumental. lyric overlay 없음. |
| 4 | shader catalog 확장 + 리서치 | 기존 23 shader catalog 충분. 추가 리서치 보류. |
| 5 | KR 가사 → KR 인물 화면 | n/a — instrumental. 하지만 visual theme = Korean lofi rainy seoul → 한국 환경/장소 우선. |
| 6 | EN 가사 → 글로벌 매칭 | n/a — instrumental. |

### AI hands avoid

Pollinations / Flux / SDXL 가 손을 렌더링하면 그로테스크 (6 손가락, 비틀린 관절).  Mix #2 의 모든 prompt 는:

- **금지 키워드**: "hand", "hands", "fingers", "reaching", "grasping", "holding object", "writing", "typing", "playing instrument" (악기 들고 있는 그림)
- **권장 대안**: "person silhouette from behind", "figure in distance", "empty interior", "rain on window", "neon street without people", "instrumental setup empty", "vinyl record close-up (no hands)", "pavement reflection", "rooftop city view", "subway interior empty"
- **검증 단계**: `mix2-build.py` 가 prompt 에서 금지 키워드 grep 으로 사전 차단 (negative prompt 에 자동 추가)
- **publish 전 게이트**: 손이 보이는 프레임 detect → flag → 운영자 review 필수

### Vocal never cut

n/a — Mix #2 = instrumental.  하지만 `MUSIC_VIDEO_DURATION` 같은 자동 60s cap 절대 금지 (소스 트랙 풀길이 보존).

### Music video mode validated

기존 검증된 형식 = vocal + Pexels + 내레이션 X + 캡션 X.  Mix #2 는 별개 (instrumental + AI 비주얼) 라서 직접 적용 안 됨.  단, **내레이션 X + 캡션 X** 원칙은 유지 (가사 없으므로 자연스러움).

## Architecture

```
INPUT
  Mix #1 source music (.mp3 × 12 트랙) or new tracks
       │ (operator decision — default: reuse Mix #1 tracks)
       ▼
┌─ STEP 1. Audio analysis (cross-platform, Python)  ──────────────┐
│   • ffprobe: duration, BPM, structure                            │
│   • aubio onset + beat detection                                 │
│   • track boundaries (silence detection + crossfade points)      │
│   • output: tracks.json (per-track metadata)                     │
└──────────────────────────────────────────────────────────────────┘
       │
       ▼
┌─ STEP 2. Segment matrix generation  ────────────────────────────┐
│   • input: tracks.json + target duration                         │
│   • output: segments.json (~600 entries, 4-5s each)              │
│   • field: ts_start, duration, track_idx, sub_mood, time_of_day, │
│            shader_ratio, prompt_template, anchor_keywords         │
│   • diversity strategy:                                          │
│     - 7 sub-moods (neon-alley / subway / cafe / rooftop /        │
│       han-river / convenience / studio-window)                   │
│     - 3 time-of-day phases (evening → night → dawn)              │
│     - per-track style shift (rainy → snowy hint → spring         │
│       morning) over the 44 min                                   │
│   • generator: mix2-design-matrix.py                              │
└──────────────────────────────────────────────────────────────────┘
       │
       ▼
┌─ STEP 3. Source image generation (local SDXL via ComfyUI)  ─────┐
│   • input: segment row → composed prompt (sub_mood + time_of_day │
│     + style modifier + AI-hands-safe negative prompt)            │
│   • output: ~600 stills (1024x1820 or 1080x1920)                 │
│   • backend: ComfyUI on 127.0.0.1:8188                           │
│   • model: SDXL or Flux-schnell (local, no rate limit)           │
│   • generator: mix2-build.py (calls ComfyUI API)                 │
│   • throughput: ~3-5 sec per image on RTX 4070 Ti SUPER 16GB     │
│   • total: ~30-50 min for 600 stills                             │
└──────────────────────────────────────────────────────────────────┘
       │
       ▼
┌─ STEP 4. Image → motion clip (LTX-Video img2vid)  ──────────────┐
│   • input: still + same prompt → 5-sec real-motion clip          │
│   • backend: ComfyUI native LTXVImgToVideo node                  │
│   • model: ltx-video-2b-v0.9.5.safetensors + T5-XXL fp8 encoder  │
│   • resolution: 768x432 → upscale to 1920x1080 (NVENC)           │
│   • throughput: ~30-60 sec per 5s clip on 4070 Ti SUPER 16GB     │
│   • total: ~5-10 hours for 600 clips (overnight)                 │
│   • output: ~600 mp4 clips (5s each, h264 transient encode)      │
└──────────────────────────────────────────────────────────────────┘
       │
       ▼
┌─ STEP 5. Composition (ffmpeg)  ─────────────────────────────────┐
│   • concat all clips with xfade transitions (300ms dissolve)     │
│   • audio: acrossfade 1-2s between adjacent tracks               │
│   • per-segment grade profile (lofi_warm_grain base)             │
│   • shader chain restraint (halation 0.20 opacity, no blanket)   │
│   • final mux: audio + video → single mp4                        │
│   • encoder: h264_nvenc -cq 19 -preset p4 -b:v 12M -maxrate 15M  │
│   • output: outputs/publish/mix-2/yt-mix-2-<theme>-<date>.mp4    │
└──────────────────────────────────────────────────────────────────┘
       │
       ▼
┌─ STEP 6. Upload metadata (8 YT settings applied)  ──────────────┐
│   • title format: 같은 패턴 ("Korean Lofi ... Mix [44 min] ...") │
│   • description: track listing + AI 사용 명시 + 채널 링크         │
│   • tags: 500자 채움 (vidIQ API/web 자동, 또는 수동 큐레이션)     │
│   • hashtags: 3-6 (#koreanlofi #lofibeats #rainyseoul ...)       │
│   • category: Music                                              │
│   • AI content disclosure: YES (변경된 콘텐츠)                    │
│   • auto-chapter: OFF (정확도 낮음)                              │
│   • privacy: private → publishAt = +24h, 어중간한 시각 (07:13)    │
│   • output: outputs/publish/mix-2/upload-meta.json (youtubeuploader compatible) │
└──────────────────────────────────────────────────────────────────┘
```

## File layout (new code lands here)

```
G:\ai\MelonS-Agents\
├── docs/
│   ├── mix-2-design.md           ← this file
│   └── platform-windows.md       ← Windows tier docs (Task C)
├── scripts/
│   ├── mix2-design-matrix.py     ← Step 2: segment matrix generator
│   ├── mix2-build.py             ← Step 3-5: orchestrator
│   ├── ltx-img2vid.py            ← Step 4: single image → 5s clip via LTX
│   ├── mix2-compose.sh           ← Step 5 ffmpeg invocation (POSIX bash, git-bash compatible)
│   ├── yt-channel-settings.py    ← Task G: 8 YT settings automation
│   └── windows/
│       └── setup-env.ps1         ← Task C: Windows env setup formalized
└── outputs/publish/mix-2/        ← gitignored, output landing path
    ├── stills/<segment>.jpg
    ├── clips/<segment>.mp4
    ├── yt-mix-2-<theme>-<date>.mp4  ← final
    └── upload-meta.json
```

## Sub-mood × time-of-day matrix (7 × 3 = 21 cells)

Each cell maps to a per-segment prompt template family.  Track sequencing
should progress through the matrix to maximize visual variation.

|        | evening (warm orange) | night (deep blue / neon) | dawn (cool pale) |
|--------|----------------------|--------------------------|-------------------|
| **neon-alley**       | neon-alley dusk        | neon-alley deep night        | neon-alley first light  |
| **subway**           | subway-platform evening | subway-empty night           | subway-empty dawn        |
| **cafe**             | cafe-window dusk        | cafe-warm-lamp night         | cafe-window dawn         |
| **rooftop**          | rooftop golden-hour     | rooftop city-lights night    | rooftop dawn-fog         |
| **han-river**        | han-river sunset        | han-river-bridge night       | han-river morning-mist   |
| **convenience**      | conv-store dusk         | conv-store fluorescent night | conv-store dawn          |
| **studio-window**    | studio-vinyl dusk       | studio-headphones night      | studio-dawn-light        |

Prompt template example (`neon-alley night`):
```
neon-lit narrow alley in Seoul, deep blue night atmosphere,
wet pavement reflecting magenta and cyan signs, rain drizzle,
empty street (no people, no hands visible), shallow depth of field,
cinematic lofi mood, 35mm film grain, vertical composition
NEGATIVE: hand, hands, fingers, reaching, person, face, animal, text
```

## Cross-platform considerations

Per [project-crossplatform-strategy] memory, all new Mix #2 code is:

- **Python primary** (mix2-*.py) — cross-platform native
- **Bash POSIX** (mix2-compose.sh) — git-bash compatible on Windows
- **NO** `videotoolbox` / `say` / `launchd` / mac-only refs
- env-driven paths: `$FFMPEG_BIN`, `$RECORDS_DIR`, `$COMFYUI_URL`
- NVENC selected via `FFMPEG_HWACCEL=nvenc` env var (default detection: `ffmpeg -hwaccels`)

## Risks + mitigations

| Risk | Mitigation |
|---|---|
| LTX-Video render slow on 600 clips | overnight batch, checkpoint-resume in mix2-build.py |
| OOM at LTX + T5 + VAE simultaneous load on 16GB | T5-XXL fp8 (4.7GB) instead of fp16 (9.5GB); VRAM monitoring |
| AI hand artifacts slipping through | grep negative prompt enforcement + frame-level detection (TBD) |
| Suno-generated source music = "Reused Content" yellow icon | Same as Mix #1 — Material impact $0 pre-YPP. Acceptable. |
| Operator returns mid-render | each segment is self-contained; can pause and resume |
| 600 stills generation hits disk full | G: drive has 899 GB free; ample headroom |
| ffmpeg concat with 600 clips audio sync drift | use `xfade` filter not raw concat; audio mux with `-async 1` |

## Open decisions (operator confirms or redirects)

These are autonomous defaults; operator can override any without re-design:

1. **Source music** = Mix #1 의 12 트랙 reuse (no new Suno generation needed).  
   Alternative: new Suno round trip (manual, 운영자 액션 필요).
2. **Theme** = same "Korean Lofi Rainy Seoul" (SEO continuity).
3. **Track count** = reduce 12 → 8-10 (less concat fatigue, more dwell per track).
4. **Length** = ~40-45 min (similar to Mix #1, fits algorithm long-form pattern).
5. **AI image backend** = local Flux-schnell via ComfyUI (fastest, no rate limit) OR keep Pollinations as fallback option.
6. **First upload target** = Private + publishAt T+24h (per 8 YT tips #1) at 07:13 KST or similar off-hour.

## Success criteria (Mix #2 "good" definition)

Mix #2 ships successfully when:

- [ ] Final mp4 exists at `outputs/publish/mix-2/yt-mix-2-<theme>-<date>.mp4`
- [ ] Duration matches source audio (±1s tolerance)
- [ ] `pix_fmt yuv420p` (not yuv444p)
- [ ] Bitrate ≥ 10 Mbps average (NVENC target 12 Mbps)
- [ ] No frame contains visible hands (operator visual check first 60s + random 5 samples)
- [ ] Visual progression visible across 44 min (sub-mood matrix exhausted)
- [ ] B-roll uniqueness — no Mix #1 source images reused
- [ ] Track transitions smooth (acrossfade visible in waveform)
- [ ] Operator subjective verdict ≥ "확실히 Mix #1 보다 나아졌다"

## Connection to existing system

- Reuses [`scripts/music-video-shaders.sh`](../scripts/music-video-shaders.sh) catalog (halation, light_leak, vignette_pulse) in restraint mode
- Reuses [`scripts/music-video-grade.sh`](../scripts/music-video-grade.sh) `lofi_warm_grain` grade profile
- Does NOT use `agents/missions/music-video/run.sh` directly — that mission is built for 60s shorts.  Mix #2 is its own pipeline.
- Compatible with [`scripts/yt-stats-collect.sh`](../scripts/yt-stats-collect.sh) (once OAuth setup) for post-upload metrics

## Next steps after this design

1. Build `mix2-design-matrix.py` (segment matrix generator)
2. Build `ltx-img2vid.py` (ComfyUI API client for one image → one clip)
3. Build `mix2-build.py` (orchestrator)
4. Build `mix2-compose.sh` (ffmpeg final encode)
5. Test render: first 5 minutes of Mix #2 as proof-of-concept
6. Run full 44 min render overnight
7. Operator review + upload via yt-batch-upload.sh

---

*Authored 2026-05-25 in Windows pivot session.  Updates expected
as ground truth shifts from operator feedback.*
