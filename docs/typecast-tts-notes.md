# Typecast TTS — 감정 지향 나레이션 (파이프라인 스테이지 7)

> 2026-07-06 채택. 운영자 결정: **TTS 엔진 = Typecast**, 보이스·감정은 매 편 **스토리보드 무드에 맞춰 선정**(에이전트가 자율 결정).
> 도구: [`scripts/typecast-tts.py`](../scripts/typecast-tts.py) · 통합: `agents/lib/tts.sh`(FACELESS_TTS_PLAN 브랜치)

## 왜 Typecast인가
- edge-tts는 **밋밋한 단일 낭독**이고 **비공식(상업 라이선스 공백)**. Typecast는 **문장별 감정**(속삭임↔외침)이 되고, 유료 티어에 **상업권**이 붙는다.
- 실측: edge vs Typecast 비교에서 감정 낙차가 콘텐츠 몰입에 유의미(이무기 엔딩 "조용히 눈을 감다→뱀이다! 외침").

## 핵심 개념
- **감정은 요청당 1개.** 한 나레이션의 감정 아크는 **문장별로 쪼개 합성 후 이어붙임**(도구가 자동 처리).
- **플랜(voice_id + 문장별 emotion)이 곧 창작 결정.** 매 편 마스터 노트 무드를 읽고 플랜을 작성한다 = "알아서 선정".
- 도구 산출: `narration.wav`(44.1k 스테레오) + **문장 단위 SRT**(정확 타이밍, ASR 드리프트 0) → `.edge.srt`로 넘겨 기존 캡션 경로가 그대로 사용.

## 감정 7종 (ssfm-v30)
`normal` · `happy` · `sad` · `angry` · `whisper` · `toneup`(톤↑/고조) · `tonedown`(톤↓/차분·무겁게)

## 보이스 로스터 (한국어, 이름 기준 선별 — 오디오 재검증 대상)
| 용도 | 보이스 | voice_id |
|---|---|---|
| 남성 저음(괴담/고발) | Byunghun | `tc_694395d43f2c8d9d43e9a897` |
| 남성 저음 대체 | Sanghyun | `tc_69fc0cff784968297fb45daa` |
| 남성 긴장/분노 | Kangil | `tc_68d4b115f0486108a7eefb37` |
| 남성 명료(뉴스/정보) | Juwan | `tc_69e0462f3e5413d26878521e` |
| 여성 감성(슬픔/드라마) | Moonjung | `tc_68f9c6a72f0f04a417bb136f` |
| 여성 속삭임(ASMR/힐링) | Hyoeun | `tc_691d49ccc47926d741f15913` |
| 여성 밝음(키즈/전래) | Daeun | `tc_692799c46508f6b9468c54c7` |
| 여성 표준 | Seohyeon | `tc_69f2e455ea79fd197aa0476f` |
> 전체 1,129 보이스. `curl -H "X-API-KEY: $KEY" https://api.typecast.ai/v1/voices` 로 조회. 언어/성별 메타는 미제공 → 이름 휴리스틱. 억양 어색하면 교체.

## 장르 → 보이스·감정 팔레트 (선정 기준)
- **괴담/호러**: 남성 저음(Byunghun) · 기저 `tonedown` + 훅/반전에서 `whisper`→`toneup`. 예: 이무기 엔딩.
- **과학/정보**(데이터상 강함): Juwan 또는 여성 또렷 · 기저 `normal` + 놀라운 수치에서 `toneup`. 뉴스는 "속보!" `toneup`.
- **슬픈 사연/드라마**: Moonjung · 기저 `sad`.
- **ASMR/힐링**: Hyoeun · 전체 `whisper`.
- **키즈/전래동화**(3D 라인): Daeun · `happy`→`toneup`.
- **분노/고발**: Kangil · `tonedown`→`angry` 고조.

**감정 아크 원칙**: 기저 감정 1개를 깔고, **훅·클라이맥스·반전에서만** 전환. 문장마다 감정 튀기 금지(부자연). 3~4문장에 1회 전환이 자연스럽다.

## 사용법
```bash
# 1) 플랜 작성 (마스터 노트 무드 → voice_id + 문장별 emotion)
#    records/_<id>/voice-plan.json
# 2) 파이프라인에 주입 (플랜이 스크립트를 대체)
FACELESS_TTS_PLAN=records/_imugi/voice-plan.json \
LAYOUT_DRAWTEXT_FONTFILE=assets/fonts/BlackHanSans-Regular.ttf \
LAYOUT_FONT_NAME="Black Han Sans" \
bash agents/missions/faceless-short/run.sh <id> "<theme>"

# 단독 생성(비교/테스트):
python scripts/typecast-tts.py --plan plan.json --out narration.wav
```

### plan.json
```json
{
  "voice_id": "tc_694395d43f2c8d9d43e9a897",
  "gap": 0.28,
  "segments": [
    {"text": "천 년을 기다린 뱀을, 당신의 한마디로 떨어뜨릴 수 있습니다.", "emotion": "tonedown"},
    {"text": "당신이라면, 조용히 눈을 감으시겠습니까.",                     "emotion": "whisper"},
    {"text": "아니면, 뱀이다! 하고 외치시겠습니까.",                       "emotion": "toneup"}
  ]
}
```

## 인증 (키는 커밋 금지)
- 키: `/g/config/typecast/api.key` (repo 밖) 또는 `TYPECAST_API_KEY` env. `X-API-KEY` 헤더.
- 네이티브 Windows 파이썬은 POSIX `/g/`를 못 읽음 → 도구가 `G:/config/...` 폴백 보유, tts.sh는 bash로 키를 읽어 env로 주입.

## ⚠️ money firewall — 유료 티어 필요
- **무료티어 = 월 5분 다운로드**(≈편당 62초 → 월 4~5편). 통합 검증엔 충분, **지속 프로덕션엔 부족**.
- 실생산: **Lite $15 / Pro $33(2시간)** — 결제는 운영자 승인 사항. 유료 티어에 **상업 라이선스**(무료판은 출처표기 의무).
- 무료 대안 백업: Google Chirp 3 HD(월 100만 자 무료·상업권, 단 감정 지시 약함) — edge-tts 라이선스 공백만 메우는 용도.

## 폴백
`FACELESS_TTS_PLAN` 미설정 또는 Typecast 실패 시 → 기존 Kokoro/edge-tts 경로로 자동 폴백(무중단).
