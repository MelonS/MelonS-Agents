# 제출 후 정리 — 무엇을 지우고 무엇을 남기는가

**계기**: NAN 2026 사전과제 제출 완료 (2026-08-10).  작업 중 쌓인 임시 산출물이
3.5GB 였다.  다음에도 같은 판단을 반복하지 않도록 기준을 적어 둔다.

---

## 지우면 안 되는 것

제출물의 실체는 **파일이 아니라 URL** 이다.  접수 마감 후에는 제출 링크를
변경할 수 없으므로, 링크가 가리키는 것을 건드리면 되돌릴 방법이 없다.

| 대상 | 이유 |
|---|---|
| `site/play/` | 제출한 플레이 링크가 이 디렉터리를 서빙한다.  재배포도 하지 않는다 |
| `builds/day-X-<날짜>-webgl` 중 **배포 중인 것** | `site/play/` 의 원본.  어느 것이 배포본인지는 `verify-deploy.sh` 가 커밋 해시로 알려준다 |
| 최신 Windows 빌드 1개 | 재현 시나리오와 촬영이 이걸 쓴다 |
| YouTube 영상 (공개 상태) | 제출한 영상 링크 |
| 저장소 공개 상태 | 요강이 소스 공개를 요구한다 |
| `repro-scenarios/_*.json` | 게이트에서 제외되지만 측정 도구다 |

## 지워도 되는 것

**재생성 가능한가**가 유일한 기준이다.

| 대상 | 재생성 방법 |
|---|---|
| `G:/ai/_frames/` (촬영 PNG 시퀀스) | 촬영 한 번 (약 4분) |
| `G:/ai/_repro_shots/` | 게이트가 매번 덮어쓴다 |
| 확인용으로 뽑은 낱장 이미지 | `ffmpeg -ss` 한 줄 |
| 낡은 빌드 폴더 | `agent.py integrate --method build` |
| 중간 산출 mp4/wav | 촬영 → 합성 재실행 |

## 아카이브로 남기는 것

지우지도 방치하지도 않고 **왜 그렇게 됐는지와 함께** 남긴다.

`art-out/아카이브_NAN2026/영상이력/` 에 시연 영상 네 판을 실패 사유가 읽히는
이름으로 뒀다.

```
01_반려_원거리1샷.mp4        배율이 멀어 활동 라벨이 안 읽혔다
02_실패_UI텍스트누락.mp4     Unity Recorder 가 게임을 다른 상태로 돌렸다
03_전투버그판.mp4            주민에게 전투 행동이 없어 적을 무시했다
04_제출본.mp4                제출한 것
```

파일만 남기면 몇 달 뒤에 "이게 뭐였지"가 된다.  같은 폴더 `README.txt` 에
각 판이 왜 폐기됐는지 적었다.

---

## 실제 절차 (2026-08-10 기준)

```powershell
# 1) 임시 산출물 — Git Bash 의 rm -rf 는 파일이 수천 개면 매우 느리다.
#    PowerShell Remove-Item 을 쓴다.
Remove-Item G:\ai\_frames, G:\ai\_repro_shots, G:\ai\yt_pawnsim -Recurse -Force

# 2) 낡은 빌드 — 유지 목록을 먼저 정하고 그 밖을 지운다.
$keep = @('day-X-2026-08-10', 'day-X-2026-08-09-webgl')   # 최신 + 배포본
Get-ChildItem $builds -Directory | Where-Object { $keep -notcontains $_.Name } |
  ForEach-Object { Remove-Item $_.FullName -Recurse -Force }
```

**정리 후 반드시 확인한다.**  지운 것이 도구를 깨뜨렸는지 도구 자신에게 묻는다.

```bash
python skills/game-dev-agent/scripts/latest_build.py --check    # 빌드 인식
python skills/game-dev-agent/scripts/latest_build.py --webgl    # 배포본 인식
bash skills/game-prototype/scripts/verify-deploy.sh             # 실물 URL
python skills/game-dev-agent/scripts/repro_all.py               # 게이트 전체
```

실측 결과: 3.5GB 확보(임시 2.3GB + 빌드 1.2GB), 네 검사 모두 통과.

> 정리는 "용량을 줄이는 일"이 아니라 **다음 사람이 무엇을 믿어도 되는지 정하는
> 일**이다.  남긴 것은 전부 근거가 있어야 하고, 지운 것은 전부 다시 만들 수
> 있어야 한다.
