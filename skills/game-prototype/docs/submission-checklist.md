# NAN 2026 제출 체크리스트 — 무엇이 끝났고, 무엇이 운영자 손을 타는가

**마감: 2026-08-10** (신청서 + 사전과제 동시)
요강 정본: [`nan2026-positioning-2026-07-29.md`](nan2026-positioning-2026-07-29.md) §1

> **하나라도 누락되면 심사 대상에서 제외된다.**  그래서 이 문서는 '얼마나 잘
> 만들었나' 가 아니라 **'빠진 게 있나'** 만 본다.

---

## 요약 — 지금 상태 (2026-08-09 최종)

| # | 제출물 | 상태 | 막는 것 |
|---|---|---|---|
| — | **참가 신청서** (Google Forms) | ❓ 미확인 | **운영자 계정** — 미제출이면 나머지가 전부 무의미 |
| ① | 플레이 빌드 (GitHub Pages) + 소스 | ✅ **완료** (실물 URL 검증) | — |
| ② | 시연 영상 30~60초 | ✅ **완료** · ❌ 업로드 | **운영자 계정** |
| ③ | 게임 소개 PDF | ✅ 5쪽 (영상 링크만 공란) | ②에 종속 |
| ④ | AI 활용 기술 문서 PDF | ✅ **완료** 14쪽 | — |
| ⑤ | 팀원 롤 기술서 PDF | ❓ 개인/팀 미확인 | **운영자 판단** |

```
게이트      24/24 PASS
영상        58.26초 · 1920×1080 · H.264 + AAC · 소리 있음
③ PDF      5쪽 · 게임 화면 4장
④ PDF      14쪽 · 요강 3요소(구조·프롬프트·에셋 출처) 모두 수록
```

> **④ 에서 한 번 위험했던 것**: 요강이 요구하는 세 항목 중 "AI 대상 주요
> 프롬프트 및 지시 사항" 이 아예 없었다.  "하나라도 누락 시 심사 대상 제외"
> 조항이 걸리는 자리였고, 마감 당일에 §3 을 신설해 해소했다.
> 제출 전에 **요강 문구와 문서 목차를 한 줄씩 대조**할 것 — 잘 쓴 문서와
> 요구를 충족한 문서는 다르다.

### ① 플레이 빌드 — 검증 완료 (2026-08-09 재배포)

```
HEAD        : f9cb5c35
site/play   : f9cb5c35
Pages run   : f9cb5c35  completed/success
GET https://melons.github.io/MelonS-Agents/play/ → 200
webgl_smoke : PASS (부팅·렌더·콘솔 오류 0)
게이트      : 24/24 PASS
```

2026-08-09 재배포 이유: 자원 패널 가독성 수정(값 변화 강조색이 크림색 배경에 묻혀
그 줄이 1.2초간 사라지던 것, 석재 칩이 배경과 명도가 같아 상시 안 읽히던 것)이
플레이 링크에도 반영되어야 한다.

로컬 게이트 초록만으로는 부족하다 — 제출물의 실체는 **URL** 이고, 로컬 검증은
URL 에 대해 아무 말도 하지 않는다.  그래서 공개 URL 을 실제로 열어 확인했다.

### ② 시연 영상 — 파일 위치

```
skills/game-prototype/art-out/demo/pawnsim_demo_2026-08-09.mp4   ← 정본
  58.26초 · 1920x1080 · 30fps · H.264 + AAC (**소리 있음**)
  마을의 하루: 아침 일 → 밥·벌목 → 오후 증축 → 저녁 습격 → 밤 등불 → 다음 아침
```

요강 제약 충족: 30~60초 · 실제 플레이 화면 · AI 합성 없음.
**업로드 후 URL 을 알려주면** ③ 문서에 기입하고 PDF 를 다시 뽑는다.

> 같은 폴더의 `pawnsim_demo_2026-08-07.mp4` / `_2026-08-08.mp4` 는 **쓰지 않는다.**
> 08-07 은 운영자가 반려한 원거리 1샷이고, 08-08 은 Unity Recorder 로 찍어
> **uGUI 텍스트가 통째로 빠진** 실패본이다.  제작 방법과 그 이유는
> [`trailer-production.md`](trailer-production.md).

### 게임 완성도 — 측정치 (2026-08-07)

```
재미 점수  78.1 / 100   (첫 측정 46.7)
  진행감 20.5/30 · 사건 19.3/25 · 활력 13.3/20 · 긴장 15.0/15 · 다양성 10/10
게이트    24종
```

기준과 방법은 [`fun-rubric.md`](fun-rubric.md).

**❓ 두 개가 진짜 위험이다.**  ①③④ 는 도구가 다 갖춰져 있어 시간만 쓰면 되지만,
신청서와 개인/팀 여부는 운영자만 답할 수 있고 **모르면 준비 자체가 헛돈다.**

---

## 운영자만 할 수 있는 것 (돌아오면 이것부터)

### 1. 참가 신청서 제출 여부 확인 — **최우선**

사전과제를 아무리 잘 만들어도 신청이 없으면 심사 대상이 아니다.
Google Forms 가 신청 경로이고 8/10 마감이다.  **미제출이면 다른 모든 것에 우선한다.**

### 2. 개인 참가인가 팀인가

- **개인** → ⑤ 생략.  더 할 일 없음.
- **2~3인** → ⑤ 팀원 롤 기술서 PDF 가 **필수**.  없으면 심사 제외.
  (역할 배분 초안은 운영자가 정해야 한다 — 실재하지 않는 팀원을 지어낼 수 없다.)

### 3. YouTube 업로드

영상 파일은 이쪽에서 만들어 둔다.  업로드는 계정이 필요하다.
업로드 후 URL 을 알려주면 ③ 문서에 기입하고 PDF 를 다시 뽑는다.

> ⚠ 이미 올린 영상을 교체할 때는 **삭제 후 업로드** 순서를 지킬 것.
> 중복 업로드는 계정에 좋지 않다.

### 4. (해소됨) 사운드 녹음 설정

예전에는 윈도우 "스테레오 믹스" 를 켜야 게임 소리를 담을 수 있었다.
지금은 게임이 `AudioRenderer` 로 **프레임과 같은 시간축에서 직접** 오디오를
렌더하므로 운영자가 할 일이 없다.

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

- [ ] 신청서 제출 완료
- [ ] 플레이 링크가 **다른 브라우저/시크릿 창**에서 열린다 (캐시 없이)
- [ ] 영상 링크가 **공개 또는 미등록**이다 (비공개면 심사자가 못 본다)
- [ ] PDF 3종(또는 2종)에 `<!--internal-->` 잔여가 없다
- [ ] 저장소가 공개이고 커밋 기록이 남아 있다
- [ ] ④ 에 외부 에셋 출처·라이선스가 명시돼 있다 (요강 필수)
- [ ] ④ 에 **AI 대상 주요 프롬프트 및 지시 사항**이 있다 (요강 필수 — 한 번 빠졌던 항목)
- [ ] 영상에 **소리가 들린다** (프레임만 합치면 무음이 된다)
- [ ] 심사 계정 `dl_gameai_reviewer@nhn.com` 접근 가능 (비공개 저장소일 때만)
- [ ] 제출 링크가 심사 종료까지 살아 있다 — **접수 마감 후 변경 불가**

---

## 본선 일정 (선정 시)

```
08-22  참가팀 발표 (10팀)
09-04~06  본선 48시간 — 판교 플레이뮤지엄
          전일 참여 필수, 온라인 불가
          산출물: 게임 프로토타입 + 에이전트 설계서 + 디렉팅 명세서
```

**이 일정을 비울 수 있는지 미리 확인할 것.**  선정되고 못 가면 아무 의미가 없다.
