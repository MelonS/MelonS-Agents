# Shorts Production — 세션/저장소 핸드오프 (부트스트랩)

> 목적: **새 폴더·새 세션**(예: 영화쇼츠 프로젝트)이 이 repo만 클론하면 쇼츠를 **바로 제대로** 만들 수 있게 하는 단일 진입점.
> 원칙: **커밋 가능한 노하우는 전부 여기(repo)에** 둔다 — 메모리에만 두면 다른 폴더/세션에 안 넘어간다. 커밋 못 하는 건 오직 **시크릿 값**(API 키 등, `G:\config`에 로컬 보관).
> **새 채널/repo를 시작하려면** → [`new-channel-setup.md`](new-channel-setup.md) (얇은 소비자 패턴 + `CLAUDE.md` 템플릿).

## 0. 세 소스의 역할
| 소스 | 무엇 | 비고 |
|---|---|---|
| **이 repo** (github.com/MelonS/MelonS-Agents) | 툴(`scripts/`) + 문서(`docs/`) + 노하우 | PUBLIC — 시크릿 값 없음 |
| **로컬 인프라 드라이브** (이 PC = `G:\ai`) | ComfyUI+모델, `.venv` | git 안 됨(용량) |
| **시크릿 폴더** (이 PC = `G:\config`) ★ | API 키·OAuth 토큰 | git 안 됨. **새 세션 소스에 반드시 포함** |

## 1. 환경 (Windows PC 기준)
| 것 | 경로 / 방법 |
|---|---|
| ComfyUI | `<드라이브>\ai\ComfyUI_windows_portable` — `run_nvidia_gpu.bat` 기동, API `http://127.0.0.1:8188` |
| Python venv | `<repo>\.venv\Scripts\python.exe` (`PYTHONUTF8=1` 권장) |
| ffmpeg/ffprobe | `G:\tools\bin` (PATH) |
| 모델 | FLUX.1-schnell fp8 · Wan2.2 A14B GGUF+lightning LoRA · Wan2.2 5B · VAE · umt5 (ComfyUI/models) |
| 폰트 | 한글 `assets/fonts/BlackHanSans`(OFL, repo) · 영어 `C:\Windows\Fonts\ariblk.ttf`(Arial Black) → gitignore된 로컬 fontsdir로 복사해 사용 |
| ⚠️ Windows 함정 | 네이티브 파이썬은 `/g/` MSYS 경로 못 읽음 → `G:/` 사용. ffmpeg concat은 상대경로. → `docs/platform-windows.md` |

> **★ ComfyUI·모델은 "PC당 하나" 공유 싱글턴.** 모델 ~119GB(`ComfyUI/models`)는 **한 곳에만** 두고 모든 세션/프로젝트가 **같은 서버(`127.0.0.1:8188`)를 공유**한다 — 생성은 각 폴더가 아니라 그 서버가 하므로 **모델 복사·재다운 불필요**. 프로젝트별 ComfyUI 설치 금지(용량 N배 낭비). 새 세션은 **서버 기동 여부만 확인**하고 접속하면 됨.
>
> **동시 사용**: 여러 세션이 작업을 *제출*할 수 있으나 GPU 1개라 **큐로 직렬 처리**(진짜 병렬 생성 아님). 스크립트의 실패-시-서버재기동 로직 충돌을 막으려면 **서버 라이프사이클은 한 세션만** 관리한다.

## 2. 시크릿 (`<드라이브>\config`, git 밖 — 값은 절대 커밋 X)
- `config\elevenlabs\api.key` — EL 키. **권한 필요: TTS + Music + Sound Effects + Voices**(music 없으면 401).
- `config\youtubeuploader\{client_secrets.json, request.token}` — YT 업로드 OAuth. **토큰은 계정별** → 새 채널은 별도 OAuth 필요.
- 코드의 키 로더는 `ELEVENLABS_API_KEY` env → `ELEVENLABS_KEY_FILE` → `/g/config/...` → `G:/config/...` 순으로 찾음.

## 3. 제작 파이프라인
- **메인**: [`generative-shorts-pipeline.md`](generative-shorts-pipeline.md) — 스테이지 0~12·검증 루프·원칙·BGM 라이선스표
- 보조: [`wan22-generation-notes.md`](wan22-generation-notes.md)(Wan 필드노트) · [`content-shorts-pipeline.md`](content-shorts-pipeline.md)(뉴스/법률 게이트) · [`elevenlabs-tts-notes.md`](elevenlabs-tts-notes.md) · [`copyright-policy.md`](copyright-policy.md)

### 툴 체인 (`scripts/`)
`flux-still.py`(스틸) → `wan-a14b-i2v.py`(모션) → `elevenlabs-tts.py`(나레이션) → `elevenlabs-music.py`(BGM) → **`assemble-short.sh`**(concat+사이드체인 더킹+-14 LUFS+자막번인) → `yt-batch-upload.sh`(업로드) → `yt-scoreboard.py`(성과)

### EL 보이스 ID (v3, 한국어는 다국어로 소화)
George(英 스토리텔러) `JBFqnCBsd6RMkjVDRZzb` · Brian(중저음 따뜻) `nPczCjzI2devNBz1zQrb` · Daniel(英 방송) `onwK4e9ZLuTAKqWW03F9`

## 4. 운영 규칙 (필수)
- **팩트체크**: 실존 인물/사건은 다출처 교차검증 후 제작(오보=명예훼손). 검증된 수치만 대본에.
- **라이크니스**: 실제 영상/사진 0, 실루엣·상징물 위주 (실존인물). 뒤통수 클로즈업=대머리로 읽힘 주의.
- **BGM 라이선스**: `elevenlabs-music.py`(일반 유튜브 상업 OK; 광고/TV/게임은 추가 라이선스) 우선. Suno 무료=비상업 전용.
- **업로드**: 하루 ≤3개·시각 무충돌·시청자 시간대. 트렌드성=신선도>슬롯. **미리 비공개 업로드 후 예약공개**(즉시공개 인덱싱 함정 회피).
- **머니 방화벽**: 유료 API·외부 공개(업로드)는 운영자 확인.
- **드라이브**: C: 자제, G: 우선. · **IP 추상화**: 공개 repo엔 참고 IP 직접 노출 X.

## 5. 영화쇼츠 특화 ⚠️ 저작권 강주의
- 컨셉: **개봉 전** 예정작 소개(프리뷰). 런칭 예: 크리스토퍼 놀란 <오디세이>(2026-08-05 개봉).
- ⚠️ **영화는 실존인물 헌정보다 저작권이 훨씬 빡셈**: 예고편 클립·공식 스틸·포스터 = 스튜디오 저작권 + Content ID 직행. → **실제 예고편/포스터 사용 금지.** 뉴스/논평 프레임 + **생성 비주얼**(FLUX/Wan 분위기 재현)으로. `content-shorts-pipeline.md` 법률 게이트 적용.
- 채널은 **별도 운영자 계정** → YT OAuth 별도 세팅 필요(운영자 브리핑).

## 6. 관습 (앞으로)
**재사용 노하우가 생기면 이 문서 또는 관련 `docs/`에 반영·커밋한다.** 메모리는 프로젝트별로 분리되어 다른 세션에 안 넘어가므로, 노하우 저장소는 항상 repo다. 시크릿 값만 예외(`config/`).
