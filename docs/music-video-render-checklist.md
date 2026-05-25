# Music Video Render Checklist

영상 batch render 시작 전 / 후 / 도중 sanity 체크. 30초 scan 가능.

배경: 2026-05-24 batch 에서 `ambient` genre 를 vocal 곡 (편의점)
에 매핑해서 stillzoom + 60초 cap 으로 잘림 + 동시에 다른 곡은
`yuv444p` 코덱으로 인코딩되어 QuickTime 재생 불가.  운영자가
"체크리스트 만들자" 요청하여 작성.

---

## A. Batch design — render 시작 BEFORE

각 row 별로 1초씩 확인.  매트릭스 작성 후 launch 전 1회 dry-scan.

- [ ] **vocal 곡이면 vocal-safe genre 만 사용**.  아래 표 참조.
- [ ] **`MUSIC_VIDEO_DURATION` = 실제 mp3 duration (ffprobe 로 측정)**.
  default 60초 트리거 막기.  `awk '{printf "%d", $1}'` 로 정수화.
- [ ] **shader_gate ∈ {uniform, onsets, beats, drops}**.
  `phrase_climax` 절대 금지 (80억 회 픽셀 계산, 곡당 1시간+).
- [ ] **lyric 파일 경로 실제 존재** (`--with-lyrics=...` 쓰면).
  파일 line count > 5 인지 확인.
- [ ] **`LYRICS_BILINGUAL=0` 기본**.  운영자가 global push 의도
  명시할 때만 1.
- [ ] **shader 이름이 scripts/music-video-shaders.sh 에 정의된
  것 사용**.  invalid name 이면 silent skip.
- [ ] **밤늦은 시간/장기 batch면 throttle 인지**.
  cpulimit 80% + nice 19 = 실제 속도 60%.  10편 = 3-4시간 가능.

## B. Genre 안전성 표

| Genre | vocal-safe? | duration cap | cut_mode | 비고 |
|-------|------------|--------------|----------|------|
| `kpop_ballad`  | ✓ | none | cuts | KR vocal 기본 |
| `kpop_dance`   | ✓ | none | cuts | KR upbeat |
| `rnb`          | ✓ | none | cuts | EN/KR R&B |
| `uspop`        | ✓ | none | cuts | EN pop |
| `citypop`      | ✓ | none | cuts | mixed lang |
| `lofi_hiphop`  | ✓ | none | cuts | lofi w/ vocals OK |
| **`ambient`**  | **✗** | **60s** | **stillzoom** | **instrumental 전용** |
| **`drone`**    | **✗** | **60s** | **stillzoom** | **instrumental 전용** |
| `shoegaze`, `jazz`, `synthwave`, `vaporwave`, `phonk`, `hyperpop`, `house`, `techno`, `classical`, `cottagecore`, `dreamcore` | YAML 보고 결정 | varies | varies | 사용 전 preset 직접 확인 |

확신 없으면 `yq '.genres.<name>' skills/music-video/data/genre-presets.yaml` 로 실제 정의 확인.

## C. Output validation — render 직후

각 mp4 별 자동 또는 수동 확인.

- [ ] `ffprobe video duration ≈ audio duration` (±1초 이내).
  mismatch 면 BUG → 원인 파악 후 재렌더.
- [ ] `pix_fmt == yuv420p` (NOT yuv444p, NOT yuvj420p).
  yuv444p 은 archival, 일반 player 재생 불가.
- [ ] 파일 크기가 듀레이션 대비 합리적 (60초 ≈ 10-50MB,
  3분 ≈ 30-100MB).  너무 작으면 비디오 누락, 너무 크면 grade
  단계 안 거침.
- [ ] QuickTime 또는 macOS Preview 에서 실제 재생 확인.
- [ ] 첫 1초가 영상으로 나오는지 (검은 화면 또는 첫 frame freeze 검출).

## D. Batch-specific — 3편 이상 한 번에

- [ ] launch 전 design 매트릭스 markdown 작성 + scan.
  열: 곡명, mp3, duration, genre, shader, gate, lyric_file.
- [ ] **dry-scan**: 매트릭스 훑으며 금지 조합 검출.
  - vocal + ambient/drone = FAIL
  - phrase_climax 단 1개라도 = FAIL
  - lyric_file 미존재 + `--with-lyrics` = FAIL
- [ ] 첫 곡 render 후 stage별 timing 측정.  예상 대비 2× 초과면
  **halt → 원인 파악 → 디자인 수정**.  sunk-cost로 진행 금지.
- [ ] watcher 등 자동 alert 설정 (선택, 길이 1h+ batch 일 때).

## E. Red flag — 즉시 halt

다음 상황 중 하나라도 발생하면 batch 중단하고 진단.

- **시간**: 첫 곡 render 가 예상 2× 초과
- **duration**: video < audio (∀ frame 끊김) 또는 audio < source mp3
  (vocal 잘림)
- **size**: 60초+ 영상이 5MB 미만 (비디오 없을 가능성)
- **codec**: ffprobe pix_fmt 가 yuv444p
- **stillzoom**: 같은 frame 만 보이는 영상
  (genre 잘못 골랐을 가능성)

---

## 사용법

batch render 직전 이 파일 열고 A + B 통과시키기.
batch 완료 후 C 통과시키기.

연결된 memory: [[vocal-never-cut]] (genre 매핑 함정 사례).
관련 코드:
- 장르 정의: `skills/music-video/data/genre-presets.yaml`
- 렌더 entry: `scripts/music-video-auto.sh`
- shader/gate: `scripts/music-video-shaders.sh`
- lyric overlay: `scripts/music-video-lyrics.sh`
- duration probe: 단순 `ffprobe -v error -show_entries format=duration -of csv=p=0 <mp3>`
