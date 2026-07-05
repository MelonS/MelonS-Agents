# ElevenLabs v3 TTS — 감정 지향 나레이션 (파이프라인 스테이지 7, 정본)

> 2026-07-06 채택·통합 완료. 운영자 청취 확정("EL2_v3_emotion 완전 만족"). **Starter $6/월 구독 = 상업 사용 OK.**
> 도구: [`scripts/elevenlabs-tts.py`](../scripts/elevenlabs-tts.py) · 통합: `agents/lib/tts.sh`(FACELESS_TTS_PLAN, engine=elevenlabs 기본) · Typecast는 백업([`typecast-tts-notes.md`](typecast-tts-notes.md)).

## 왜 일레븐랩스 v3
- **감정 표현력이 압도적**(운영자 실청취: Typecast·edge 대비 "몇 배"). 우리 주력 괴담=드라마틱이라 감정=몰입.
- **v3는 인라인 감정 태그**(`[whispers]`, `[shouts]` …)로 **한 텍스트에 감정 아크**를 심음 → 문장 안 쪼개고 **1회 호출**(Typecast 문장분할보다 깔끔).
- 한국어 발음: 영어권 premade 보이스가 multilingual로 한국어 읽음 → 운영자 판정 "발음 문제없음".

## 핵심 개념
- **감정 태깅이 곧 창작 결정.** 매 편 마스터 노트 무드를 읽고 대본에 태그를 심는다 = "알아서 선정".
- 도구는 `/with-timestamps` 엔드포인트로 **문자 정렬**을 받아: audio→`narration.wav`(44.1k 스테레오) + **문장 SRT**(정확 타이밍).
- ⚠️ 정렬에는 태그 문자(`[whispers]`)도 포함됨 → 도구가 **자막에서 태그 자동 제거**(시청자엔 안 보임). 검증 완료.

## 보이스 로스터 (premade, 한국어 multilingual)
| 용도 | 보이스 | voice_id | 성격 |
|---|---|---|---|
| **기본(나레이션)** | **George** ✅승인 | `JBFqnCBsd6RMkjVDRZzb` | Warm, Captivating Storyteller |
| 남성 저음(괴담) | Charlie | `IKne3meq5aSn9XLyUdCD` | Deep, Confident |
| 남성 신뢰(정보) | Eric | `cjVigY5qzO86Huf0OWal` | Smooth, Trustworthy |
| 남성 음산(호러) | Callum | `N2lVS1w4EtoT3dr4eOWO` | Husky Trickster |
| 남성 뉴스/정보 | River | `SAz9YHcvj6GT2YYXdXww` | Relaxed, Informative |
| 여성 성숙(드라마) | Sarah | `EXAVITQu4vr4xnSDxMaL` | Mature, Reassuring |
| 여성 정보/교육 | Alice | `Xb7hH8MSUJpSbSDYk0k2` | Clear, Engaging Educator |
> 전체 21 premade. `curl -H "xi-api-key: $KEY" https://api.elevenlabs.io/v1/voices`. 시그니처 목소리 클로닝은 Creator 티어 + "음성" 쓰기권한 필요(추후).

## 감정 태그 (v3) — 자주 쓰는 것
`[whispers]` 속삭임 · `[shouts]` 외침 · `[sighs]` 한숨 · `[sad]` 슬픔 · `[nervous]` 긴장 · `[excited]` 고조 · `[crying]` 울음 · `[laughs]` 웃음 · `[pause]` 쉼 · `[curious]` 호기심
> v3 태그는 모델 해석형(100% 보장 아님). 문장 앞에 배치, 남발 금지 — 3~4문장에 1회 전환이 자연스럽다.

## 장르 → 보이스·태그 팔레트
- **괴담/호러**: George(또는 Callum) · 기저 낮게 + 반전에서 `[whispers]`→`[shouts]`. (이무기 엔딩 검증)
- **과학/정보**(데이터상 강함): River/Alice · 대체로 중립 + 놀라운 사실에서 `[excited]`.
- **슬픈 사연/드라마**: Sarah · `[sighs]`, `[sad]`.
- **뉴스**: River · 활기 + "속보!" `[excited]`.
- **키즈/전래**(3D 라인): Laura/Jessica · `[laughs]`, `[excited]`.

## 사용법
```bash
# 1) 플랜 작성 (마스터노트 무드 → voice_id + 감정태그 심은 대본)
#    records/_<id>/voice-plan.json
# 2) 파이프라인 주입 (플랜이 스크립트 대체, engine 기본 elevenlabs)
FACELESS_TTS_PLAN=records/_imugi/voice-plan.json \
LAYOUT_DRAWTEXT_FONTFILE=assets/fonts/BlackHanSans-Regular.ttf \
LAYOUT_FONT_NAME="Black Han Sans" \
bash agents/missions/faceless-short/run.sh <id> "<theme>"

# 단독(비교/테스트):
python scripts/elevenlabs-tts.py --plan plan.json --out narration.wav
```

### plan.json
```json
{
  "engine": "elevenlabs",
  "voice_id": "JBFqnCBsd6RMkjVDRZzb",
  "model_id": "eleven_v3",
  "text": "천 년을 기다린 뱀을... 있습니다. [whispers] 당신이라면, 조용히 눈을 감으시겠습니까. [shouts] 아니면, 뱀이다! 하고 외치시겠습니까.",
  "voice_settings": {"stability": 0.4, "similarity_boost": 0.8, "style": 0.5}
}
```

## 인증 (키 커밋 금지)
- 키: `/g/config/elevenlabs/api.key`(repo 밖) 또는 `ELEVENLABS_API_KEY` env. `xi-api-key` 헤더.
- 키 권한: **텍스트 음성 변환=접근 + 음성=읽음**(최소). 클로닝 시 음성=작성 추가.
- 네이티브 Win 파이썬 POSIX `/g/` 못 읽음 → 도구가 `G:/config/...` 폴백, tts.sh는 bash로 읽어 env 주입.

## ⚠️ 크레딧
- Starter = 월 30,000 크레딧(v2 1크레딧/자 기준 ≈33편). v3 소모율·태그 과금은 대시보드에서 모니터. 볼륨↑ 시 Creator $22(121k).
- 상업 라이선스 = Starter 이상 포함(무료판은 API 보이스 자체가 402 차단).

## 폴백
`FACELESS_TTS_PLAN` 미설정 또는 실패 → Kokoro/edge-tts 자동 폴백(무중단). engine=typecast 지정 시 Typecast 백업 사용.
