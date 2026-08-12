# NAN 2026 제출 기록 — 제출 완료 (2026-08-10)

**마감: 2026-08-10** (신청서 + 사전과제 동시) — 마감일에 전량 제출 완료
요강 정본: [`nan2026-positioning-2026-07-29.md`](nan2026-positioning-2026-07-29.md) §1

> 제출 전에는 '빠진 게 있나' 만 보는 문서였다.  제출 후에는 **무엇을 어떤 상태로
> 냈는지의 기록**이고, 본선에 올라갈 경우 그때의 출발점이 된다.

---

## 제출 완료 (2026-08-10)

| # | 제출물 | 상태 |
|---|---|---|
| — | 참가 신청서 (Google Forms) | ✅ **접수 완료** — `dl_nan@nhn.com` 확인 메일 (08-10 11:13) |
| ① | 플레이 빌드 + 소스 | ✅ https://melons.github.io/MelonS-Agents/play/ |
| ② | 시연 영상 30~60초 | ✅ https://youtu.be/3iAvYyzoQ3w (공개, 59.6초) |
| ③ | 게임 소개 PDF | ✅ 6쪽 (영상 링크 포함) |
| ④ | AI 활용 기술 문서 PDF | ✅ 12쪽 |
| ⑤ | 팀원 롤 기술서 PDF | — 개인 참가로 생략 |

```
게이트   25/25 PASS
영상     59.58초 · 1920x1080 · 30fps · H.264 + AAC
```

> **마감 후 유지해야 하는 것**  접수 마감 뒤에는 제출 링크를 바꿀 수 없다.
> 심사 종료까지 다음 셋을 그대로 둔다 — GitHub Pages 플레이 링크, YouTube
> 영상(공개), 저장소 공개 상태.  `site/play/` 를 갈아엎는 배포도 하지 않는다.

### 일정

```
08-22       참가팀 발표 (10팀)
09-04~06    본선 48시간 — 판교 플레이뮤지엄 (전일 참여 필수)
            산출물: 게임 프로토타입 + 에이전트 설계서 + 디렉팅 명세서
```

### ① 플레이 빌드 — 실물 URL 검증 완료

```
Pages run   : completed/success
GET https://melons.github.io/MelonS-Agents/play/ → 200
webgl_smoke : PASS (부팅·렌더·콘솔 오류 0)
```

로컬 게이트 초록만으로는 부족하다 — 제출물의 실체는 **URL** 이고, 로컬 검증은
Windows 실행 파일을 본 것이지 WebGL 에 대해 아무 말도 하지 않는다.  그래서
공개 URL 을 실제로 열어 확인했다.

### ② 시연 영상 — https://youtu.be/3iAvYyzoQ3w

```
59.58초 · 1920x1080 · 30fps · H.264 + AAC (소리 있음) · 공개
구성: 마을 전경 → 작업 우선순위 설정 → 벌목 지정과 주민 이동 → 각자 다른 작업
      → 방 증축 → 저녁 습격과 방어 → 밤 등불·취침 → 다음 날 아침
```

요강 제약 충족: 30~60초 · 실제 플레이 화면 · AI 합성 없음.
원본 파일은 `art-out/제출_NAN2026/PawnSim_02_시연영상.mp4`.
제작 방법과 그동안 밟은 함정은 [`trailer-production.md`](trailer-production.md).

### 게임 완성도 — 측정치 (2026-08-07)

```
재미 점수  78.1 / 100   (첫 측정 46.7)
  진행감 20.5/30 · 사건 19.3/25 · 활력 13.3/20 · 긴장 15.0/15 · 다양성 10/10
게이트    24종
```

기준과 방법은 [`fun-rubric.md`](fun-rubric.md).

---

## 이쪽에서 처리하는 것

### ① 플레이 빌드 재배포

```bash
# --method 는 **완전한 메서드 이름**이어야 한다.  "webgl" 로 적으면 그 문자열이
#  그대로 `-executeMethod webgl` 로 넘어가 조용히 실패한다 (2026-08-09 에 겪음).
python skills/game-dev-agent/scripts/agent.py integrate \
  --project skills/game-prototype/unity-project \
  --method MelonS.GameProto.EditorTools.BuildScript.BuildWebGL
bash skills/game-prototype/scripts/deploy-play.sh      # site/play/ 로 복사
git add site/play && git commit && git push            # Pages 워크플로가 반응
bash skills/game-prototype/scripts/verify-deploy.sh    # 실물 URL 확인
```

- 배포 URL: https://melons.github.io/MelonS-Agents/play/
- **주의**: `deploy-play.sh` 는 빌드 폴더를 자동 선택한다.  날짜 폴더를 하드코딩하면
  자정을 넘겼을 때 어제 빌드를 배포해 "고쳤는데 반영이 안 된다"가 된다.
- **검증은 반드시 실물 URL 로.**  로컬 게이트 초록은 Windows 실행 파일을 본 것이고
  제출물은 WebGL 이다 — 두 플랫폼이 갈라지는 지점(폰트 대체·스레드·타이밍)에서는
  초록불이 오히려 눈을 가린다.

### ② 시연 영상 (30~60초)

```bash
# 빌드가 직접 프레임을 덤프한다 — Unity Recorder 는 **쓰지 않는다**
# (에디터 경로가 게임을 다른 상태로 돌리고 uGUI 텍스트가 프레임에서 빠진다)
# -mute 를 주면 안 된다 — 그림만 남고 게임 소리가 통째로 빠진다.
"$(python skills/game-dev-agent/scripts/latest_build.py)" \
  -autostart -trailerframes "G:\ai\_frames" \
  -logFile "G:\ai\_fr.log" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0

# 오디오를 먼저 방송 표준으로 정규화한다.
#  그대로 합치면 피크가 0dB 에 닿아 깨진다(촬영용으로 배경음을 올리기 때문).
ffmpeg -y -i G:/ai/_frames/audio.wav \
  -af "loudnorm=I=-15:TP=-1.5:LRA=11" -ar 48000 G:/ai/_audio_norm.wav

ffmpeg -y -framerate 30 -i "G:/ai/_frames/f%05d.png" -i G:/ai/_audio_norm.wav \
  -map 0:v -map 1:a -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p \
  -c:a aac -b:a 192k -shortest -movflags +faststart \
  skills/game-prototype/art-out/demo/pawnsim_demo_<날짜>.mp4

grep '\[Trailer\] t=' G:/ai/_fr.log     # 컷 타임라인 검증
```

연출·함정·검증 방법 정본: [`trailer-production.md`](trailer-production.md).

요강 제약:
- **30~60초**, 실제 플레이 장면 중심
- **AI 조작·합성 및 타 영상 도용 불가** — 실제 화면 그대로
- 첫 10초가 빌드를 열어볼지 결정한다 → 메뉴·로딩 금지, 살아있는 마을부터

### ③④ PDF 출력

```bash
python skills/game-prototype/scripts/md2print.py --all   # md → 인쇄용 HTML
python skills/game-prototype/scripts/html2pdf.py         # HTML → PDF
# → art-out/submission/*.pdf
```

`<!--internal-->` 블록은 인쇄본에서 자동 제거된다 (심사자용이 아닌 메모).

---

## 제출 직전 최종 점검

- [x] 신청서 제출 완료 (확인 메일 수신)
- [x] 플레이 링크 실물 확인 (GET 200 · webgl_smoke PASS)
- [x] 영상 링크가 공개다
- [x] PDF 에 `<!--internal-->` 잔여 없음
- [x] 저장소 공개 · 커밋 기록 유지
- [x] ④ 에 외부 에셋 출처·라이선스 명시 (요강 필수)
- [x] ④ 에 AI 대상 주요 프롬프트 및 지시 사항 (요강 필수 — 한 번 빠졌던 항목)
- [x] 영상에 소리가 들린다 (프레임만 합치면 무음이 된다)
- [x] 제출 링크가 심사 종료까지 살아 있어야 한다 — **접수 마감 후 변경 불가**

> 심사 계정 `dl_gameai_reviewer@nhn.com` 초대는 **비공개 저장소일 때만** 필요하다.
> 이 저장소는 공개이므로 해당 없음.

---

## 본선 일정 (선정 시)

```
08-22  참가팀 발표 (10팀)
09-04~06  본선 48시간 — 판교 플레이뮤지엄
          전일 참여 필수, 온라인 불가
          산출물: 게임 프로토타입 + 에이전트 설계서 + 디렉팅 명세서
```

**이 일정을 비울 수 있는지 미리 확인할 것.**  선정되고 못 가면 아무 의미가 없다.
