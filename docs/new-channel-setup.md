# 새 채널 셋업 — "얇은 소비자" 패턴

새 채널 = 새 repo/폴더. **파이프라인을 복사하지 말 것**(복사=drift). 이 엔진(MelonS-Agents)을 **참조만** 한다.

## 새 repo가 볼 것 = 딱 2개 (엔진에 있음, 1벌)
- **읽기**: [`docs/shorts-production-handoff.md`](shorts-production-handoff.md) ← **단일 진입점**. 환경·시크릿위치·툴체인·규칙·파이프라인을 전부 링크한다. 새 세션은 이거 하나만 읽으면 나머지로 이어진다.
- **사용**: `scripts/` ← 정본 툴 (`flux-still.py` · `wan-a14b-i2v.py` · `elevenlabs-tts.py` · `elevenlabs-music.py` · `assemble-short.sh` · `yt-batch-upload.sh` · `yt-scoreboard.py`)

이 PC에서 엔진 위치 = `G:\ai\MelonS-Agents` (= github.com/MelonS/MelonS-Agents). ComfyUI·모델·`.venv` = `G:\ai` 공유. 시크릿 = `G:\config`.

## 새 채널 repo에 넣을 유일한 파일 = `CLAUDE.md` (아래 템플릿)
채널별로 `{{ }}`만 채운다. 파이프라인·규칙은 엔진 참조라 다시 안 쓴다. Claude Code가 세션 시작 시 자동 로드.

```markdown
# Project: {{repo}} — {{채널 한줄 설명}}

{{채널 목적/포맷}}. 로컬 작업폴더 = {{경로}}.

## ★ 먼저 읽을 것 (제작 노하우 정본 — 이 repo엔 없음, 엔진 참조)
- `G:\ai\MelonS-Agents\docs\shorts-production-handoff.md` ← 부트스트랩(제일 먼저)
- `G:\ai\MelonS-Agents\docs\generative-shorts-pipeline.md` ← 파이프라인(스테이지 0~12)
- 툴 = `G:\ai\MelonS-Agents\scripts\`. 산출물 = 이 폴더 `records/`(gitignore).

## ComfyUI (공유 — 재설치 금지)
모델 ~119GB 이미 있음(`G:\ai\ComfyUI_windows_portable`). 서버 `127.0.0.1:8188` 공유. 안 떠있으면 `run_nvidia_gpu.bat`만. GPU 1개라 동시 배치는 큐 직렬, 서버 재기동은 한 세션만.

## 시크릿 (G:\config)
`elevenlabs\api.key`(TTS·Music·SFX·Voices) · `youtubeuploader\`. ⚠️ **채널별 YT OAuth 별도**(계정 다르면 새 로그인).

## 규칙
팩트체크(다출처) · 머니 방화벽(유료 API·유튜브 공개는 운영자 "고") · 저장 G:드라이브 · **재사용 노하우는 docs 커밋** · BGM=`elevenlabs-music.py`(상업OK), Suno무료=비상업 · 매 응답 끝 상태 푸터.

## 채널 전용 ⚠️
{{채널별 저작권/포맷 주의 — 예: 영화=예고편/포스터 실제사용 금지, 생성비주얼+뉴스프레임}}

## 첫 작업
{{첫 에피소드 컨셉/시안 → 운영자 승인 → 제작 → 공개는 "고"}}
```

## 요약
- 엔진 1벌(MelonS-Agents), 채널 N개(각 repo). 채널은 **파일1(handoff) + 폴더1(scripts)** 참조 + 자기 `CLAUDE.md` 1장.
- 복사 ❌ / submodule(선택, 오버킬) / **경로 참조 ✅**.
