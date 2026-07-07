# Generative Shorts Pipeline — 생성형 쇼츠 제작 파이프라인 v3

> 2026-07 실전(괴담 3편 + 우주 1편 + 호날두 헌정 1편/글로벌·영어)에서 확립. 로컬·무료 스택 기반(BGM·TTS는 ElevenLabs 유료 옵션).
> 새 세션 부트스트랩(환경·시크릿위치·툴체인): [`shorts-production-handoff.md`](shorts-production-handoff.md)
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
| 7 | TTS | **ElevenLabs v3**(감정태그, `FACELESS_TTS_PLAN`) / 백업 Typecast·edge | narration.wav + 문장 SRT(대본 정확·태그 제거) | 무드→보이스·감정태그 선정([`elevenlabs-tts-notes.md`](elevenlabs-tts-notes.md)) |
| 8 | BGM/SFX | **`elevenlabs-music.py`**(상업OK BGM)·`-sfx.py`·ffmpeg 합성 | BGM 베드 + SFX (**나레이션 밑 사이드체인 더킹**) | BGM 라이선스(아래) |
| 9 | 자막 | 검은고딕(OFL) 번인 | 잠금 원칙: 자막이 아니라 화면을 고침 | |
| 10 | 조립·렌더 | **`scripts/assemble-short.sh`** = concat+더킹+**-14 LUFS**+자막번인+nvenc | 최종 mp4 + QA(프레임시트·freezedetect) | cut-judge |
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

## 실존인물 헌정 / 글로벌 변형 (2026-07 호날두편)
- **팩트 우선**: 실존 인물·실제 사건은 **다출처 교차검증** 후 제작(오보=명예훼손 리스크). 대본엔 **검증된 수치만** 쓰고 소스 간 불일치 수치는 회피.
- **라이크니스 안전**: 얼굴 노출 대신 **실루엣·뒷모습·상징물**(등번호·유니폼·잔디 위 유니폼)만. 실제 영상/사진 0 → 저작권(중계권)·초상권·퍼블리시티 회피. 잔존리스크(암시)는 낮음.
- **대머리 함정**: 뒤통수 클로즈업 실루엣은 **대머리로 읽힌다** → 3/4 뒷모습·웅크림 등 **머리숱 보이는 구도**로. (A14B I2V가 정적 스틸을 서사 동작으로 살리기도 — 유니폼 스틸→선수가 손 얹는 장면)
- **컷 리듬**: 컷 수 = 나레이션 길이 ÷ ~5.06s(A14B 81f@16fps). 6컷보다 **9컷이 단조로움 완화**.
- **글로벌/영어**: 영어 나레이션은 **네이티브 보이스**(George=英 스토리텔러, 억양無), 자막 폰트 **Arial Black**(gitignore된 `records/*/fonts/`에 복사해 fontsdir — 공개 repo에 프로프라이어터리 폰트 커밋 금지). 트렌드성 소재는 **신선도 > 슬롯 최적화**(즉시~당일 공개; "즉시공개=인덱싱안됨"은 오해 — 미리 비공개 업로드로 처리완료 상태 만들면 됨).

## BGM 라이선스 정리
| 소스 | 상업 사용 | 자동화 |
|---|---|---|
| **`elevenlabs-music.py`** (EL Music) | ✅ 일반 유튜브 상업OK (광고/TV/영화/게임은 추가 라이선스) | ✅ API(키에 `music_generation` 권한 필요), 900cr/분 |
| Suno 무료 | ❌ 비상업만 (비수익 한정) | ❌ 공식 API 없음(웹 수동) |
| Suno Pro 재생성본 | ✅ | ❌ 웹 수동 |
> 수익화 채널 BGM은 `elevenlabs-music.py` 우선. 사이드체인 더킹은 `assemble-short.sh`가 처리.

## 모델 재고 (로컬)
FLUX.1-schnell fp8(스틸) · Wan2.2 A14B GGUF+lightx2v 260412(영상 기본) · Wan2.2 5B(경량)
· Wan2.1 FLF2V(예비) · RealESRGAN(업스케일) · ai-toolkit(캐릭터 LoRA 훈련)
