# Generative Shorts Pipeline — 생성형 쇼츠 제작 파이프라인 v3

> 2026-07 실전(괴담 시리즈 3편 + 우주 1편)에서 확립. 전 과정 로컬·무료 스택.
> 필드 노트: [`wan22-generation-notes.md`](wan22-generation-notes.md) · 뉴스/법률 게이트: [`content-shorts-pipeline.md`](content-shorts-pipeline.md)

## 스테이지

| # | 스테이지 | 도구 | 산출물 | 게이트 |
|---|---|---|---|---|
| 0 | 테마 선정 | 주제뱅크(`config/info-topic-bank.yaml`) / 운영자 | 테마+앵글 | 운영자 |
| 1 | 마스터 노트 | (문서) | `MASTER-NOTE.md` — 로그라인·훅·대본·팔레트·법적 프레임 | |
| **1.5** | **노트 검증** ★ | `note-judge` 에이전트 | 100점 채점(훅·구조·아크·참여·실현성·법) — 75미만 REVISE | **GO 필수** |
| 2 | 컷별 프롬프트 | (문서) | `prompts/cutNN.md` — STYLE/CHARACTER LOCK + CULTURE GUARD + IMAGE(START·END)/VIDEO(CAMERA·ACTION) 분리 | |
| 3 | 스틸 생성 | `scripts/flux-still.py` (FLUX.1-schnell) | 샷별 START 앵커 (768×1344, ~9s/장) | |
| 4 | **스토리보드** | 8컬럼×N샷 HTML | SHOT(+**시간**)·START·END·CAMERA·ACTION·DIALOGUE·SFX·MUSIC | |
| **4.5** | **스토리보드 검증** ★ | `still-judge` 에이전트(자동 채점, 75미만 자동 REGEN 루프) + **운영자** | 샷별 점수·타임코드가 붙은 보드 | **전샷 승인 필수** |
| 5 | I2V 영상화 | `wan-a14b-i2v.py` (A14B+260412) / FLF(`WanFirstLastFrameToVideo`) / 카메라 LoRA 스택 | 샷별 mp4 (704×1280·81f, ~7분/샷) | |
| 6 | 컷 연결 | ffmpeg | 원속·3~4s/샷·+2.5s 클론패딩·`sort -V` | |
| 7 | TTS | edge-tts (InJoon/SunHi) | narration.wav + `edge.srt`(대본 정확 자막) | |
| 8 | BGM/SFX | ffmpeg 합성 | 라이선스-프리 베드 + 타임스탬프 SFX | |
| 9 | 자막 | 검은고딕(OFL) 번인 | 잠금 원칙: 자막이 아니라 화면을 고침 | |
| 10 | 렌더 | nvenc | QA 프레임시트 | cut-judge |
| 11 | (해당 시) 법률 | `news-screen.sh` + `legal-gate.sh` | verdict PASS | fail-closed |
| 12 | 출시 | `yt-batch-upload.sh` | 예약 공개(하루≤3) + 20분 애널리틱스 샘플러 | 운영자 |

## 스테이지 4.5 — 스토리보드 검증 (부분 재생성 루프)

1. 스토리보드의 **샷별 시간**(예상 타임코드)과 START 스틸을 보며 샷 단위로 판정
2. `still-judge`가 전 샷 100점 채점(피사체30·구도20·무드20·캐릭터일관성20·결함10) →
   **75 미만은 prompt_fix 반영해 그 샷만 자동 재생성** (최대 3라운드) → 보드에 점수 표기
   - 비용: 스틸 재생성 ~9초/샷 (영상 단계 재작업 7분/샷의 1/50)
3. 반복 → **전 샷 승인 후에만** 5번(영상화) 진입
4. 자율(무인) 모드에서도 스토리보드는 영상화 **전** 필수 산출물 — 아침 검수용으로 남긴다

> 근거: 원패스(검증 생략) 제작은 약컷이 그대로 최종본에 실린다 — 사전 검증 3라운드를 거친
> 회차와 원패스 회차의 품질 차가 실측으로 확인됨.

## 원칙 (실측 기반)
- **싼 단계에서 실패시켜라**: REGEN은 스틸(9초)에서, 영상(7분)에서 하지 않는다
- **시트가 단일 진실 소스**: START 스틸=I2V 입력, ACTION=프롬프트 — 시트 수정=입력 수정
- **파일이 재현성**: fp8 비결정성 때문에 시드가 아니라 확정 앵커 파일을 보관
- **캐릭터 락**: 시리즈물은 캐릭터 시트+락 문장으로 디자인 고정 (hex 팔레트 포함)
- **대형 모델 전환 사이 서버 재기동** (Flux↔Wan 교차 행 방지)
- **슬로모 금지** 기본 (특수효과로만), 샷 길이 3~4초 원속
- **화면 오버레이 금지** 기본: 고지·출처표기는 영상 설명란에만 (구미호 메타 패턴 — 창작 고지·AI생성·합성음성 3줄). 화면 좌상단 번인은 `FACELESS_ATTRIBUTION_OVERLAY=1`일 때만. 우리가 쓰는 라이선스(Pexels License·FLUX schnell Apache-2.0·Wan·edge-tts) 모두 화면 표기 의무 없음. 100% 생성물을 "B-roll: Pexels"로 오귀속하던 버그도 이걸로 제거(2026-07-06)

## 모델 재고 (로컬)
FLUX.1-schnell fp8(스틸) · Wan2.2 A14B GGUF+lightx2v 260412(영상 기본) · Wan2.2 5B(경량)
· Wan2.1 FLF2V(예비) · RealESRGAN(업스케일) · ai-toolkit(캐릭터 LoRA 훈련)
