# 차기작 기획 — 범선 시대 항해 무역 게임: 배-바다 이동 프로토타입

2026-08-12. `game-prototype`(PawnSim, 콜로니심) 제출 이후 다음 타이틀 검토.
운영자 요청: 배를 3D 바다 위에서 움직이는 프로토타입부터. 카메라 전환으로
2.5D 방식이 될지 풀3D가 될지는 나중에 정한다 — 그래서 이 프로토타입 자체가
"둘 다 씬 하나에서 토글해보고 어느 쪽이 맞는지 사용자가 직접 판단하는" 도구다.

**이번 프로토타입의 목적은 "재미"가 아니라 "이동감이 성립하는가" 확인이다.**
전투·무역·탐험·여러 척·항구는 전부 스코프 밖.

장르 정의: `skills/game-dev-agent/genres/naval-sail-prototype.yaml`
(콜로니심 장르 파일과 같은 스키마, planner.py 코드 변경 없이 로드됨)

> **2026-08-12 갱신 — Unity 프로젝트 위치 변경.** 아래 §2·§6·§9는 처음에
> `skills/game-prototype/unity-project/`(PawnSim과 같은 프로젝트) 기준으로
> 썼다. 같은 세션 후반에 별도 프로젝트로 분리했다 — 지금 실제 위치는
> **`games/naval-sail/unity-project/`**다. 분리 이유: 두 게임이
> `ProjectSettings/`를 공유하면 NAN2026 심사 종료(9/6)까지 건드리면 안 되는
> PawnSim 제출물에 영향을 줄 위험이 있었다. 아래 경로 언급은 당시 기록
> 그대로 남겨둔다 — 왜 이런 구조가 됐는지 이력이 지워지면 안 되기 때문.
## 1. OpenMMO에서 참고한 것

[전체 리서치](../skills/game-prototype/docs/external-research-openmmo-2026-08-12.md)의
연장. 이번엔 안 맞는다고 판단했던 지난 리서치와 달리 **실제로 맞는 레퍼런스다** —
그 게임은 3D 바다가 있고, 우리 새 프로토타입도 3D이기 때문이다 (지난 PawnSim은
2D라서 3D 전용 기법이 대부분 안 맞았던 것).

`doc/WATER_SYSTEM.md` 확인 내용:

- **파도**: 버텍스 셰이더에서 Gerstner wave 3개를 합산. Y축 변위만(X/Z 이동 없음
  — 타일 이음새 안 깨지게). 파장 20m/14m/9m + 세션당 랜덤 각도.
- **수심**: 65×65 하이트맵 DataTexture를 타일별로 셰이더에 넘겨 지형 높이 대비
  수면 깊이를 실시간 계산.
- **부력은 없다** — OpenMMO도 물 높이를 순수 렌더링에만 쓰고, 오브젝트를
  물리적으로 띄우는 코드는 없다. **우리도 직접 만들어야 한다.**
- 성능: 매 프레임 3-패스(굴절/반사/메인), 굴절·반사는 절반 해상도. LOD 없음
  (모든 타일 동일 복잡도) — 문서에 명시 안 됨, 확인 안 된 부분.

## 2. 기술 스택 — 확인 필요한 것부터

`skills/game-prototype/unity-project/Packages/manifest.json` 확인함 —
URP/HDRP 패키지가 없다. **현재 Built-in Render Pipeline으로 추정.**
(패키지 부재로 추정한 것이지, Graphics 설정을 직접 열어 확인한 건 아님 — 착수
전에 한 번 더 확인 필요.)

두 옵션:

| | Built-in RP 커스텀 셰이더(HLSL/CG) | URP 업그레이드 + Shader Graph |
|---|---|---|
| 장점 | 지금 프로젝트 그대로 사용 | 비주얼 작업이 노드 기반이라 나중에 다루기 쉬움 |
| 단점 | 셰이더 코드를 손으로 짜야 함 | 마이그레이션 비용, 콜로니심 쪽 2D 렌더링에 영향 줄 수 있음 |
| 이번 판단 | **채택** — 이동감 검증이 목적, 비주얼은 나중 | |

프로토타입 단계라 Built-in RP로 시작 — Gerstner wave 셰이더는 OpenMMO 파장값
(20/14/9m)을 시작점으로 손코딩한다.

## 3. 배 이동 설계

- Rigidbody 기반. 배 바닥 4점(뱃머리·고물·좌현·우현)에서 파도 높이를 CPU로
  샘플링(셰이더와 같은 Gerstner 공식을 C#으로도 계산) → 평균 Y로 배를 띄우고,
  4점 높이차로 피치·롤 계산.
- 조작: 전후진 스로틀 + 방향타(좌우). 관성 있는 가감속 — "가볍지 않은 배"
  느낌이 범선 항해 게임 장르의 핵심이라 즉각 반응형 컨트롤은 의도적으로 배제.
- v0 스코프 밖: 돛/바람 상호작용, 충돌(암초·타 선박), 정박.

## 4. 카메라 2.5D ↔ 3D 전환 설계

씬 하나에 카메라 리그 두 개를 두고 토글한다 — 최종 형태를 지금 정하지 않고,
둘 다 직접 플레이해보고 판단하기 위한 장치다.

- **2.5D**: 고정 피치각(45~60도 사이 조정 가능한 값), 배를 따라가는 좁은 FOV
  또는 직교(orthographic) 투영. 범선 항해 게임 장르에서 흔한 고정 부감 시점.
- **3D**: 배 뒤 추적 카메라, 자유 회전(우클릭 드래그), 원근 투영.
- 전환 트리거: 키 입력(예: Tab). 배 위치는 고정하고 카메라만 0.3~0.5초
  보간 전환 — 급전환 시 멀미감 방지.

## 5. 마일스톤 (프로토타입, 대략치 — 실측 아님)

| Day | 내용 | 상태 |
|---|---|---|
| 1 | Gerstner wave 셰이더 + 평면 바다 렌더 | **완료** (2026-08-12, 같은 세션) |
| 2 | 배 placeholder 모델 + CPU 높이 샘플링 부력 | **완료** |
| 3 | 배 이동 입력 + 관성 | **완료** (자동조종 경로로 물리는 검증, 실키보드는 미검증 — §8) |
| 4 | 카메라 리그 2종 + 전환 로직 | **완료** (실 Tab 키 입력은 미검증 — §8) |
| 5 | 폴리시 + repro 시나리오 1개 | 미착수 |

## 6. v0 실제 구현 — 파일 목록

`skills/game-prototype/unity-project/Assets/` 아래:

- `Shaders/OceanGerstner.shader` — §2 Gerstner 셰이더 (Built-in RP surface shader).
- `Scripts/Naval/OceanWaveSampler.cs` — CPU 쪽 동일 공식. 드리프트 방지를 위해
  하드코딩 대신 바다 Material에서 파라미터를 직접 읽는다.
- `Scripts/Naval/ShipBuoyancy.cs`, `ShipController.cs` — §3 부력·이동.
- `Scripts/Naval/CameraRig2Point5D.cs`, `CameraRig3D.cs`, `CameraModeSwitcher.cs` — §4.
- `Editor/NavalSceneSetup.cs`, `NavalBuildScript.cs` — PawnSim의 `SceneSetup.cs`/
  `BuildScript.cs`와 같은 패턴(batchmode `-executeMethod`로 씬을 코드에서
  재현). 별도 클래스·별도 씬(`Scenes/OceanPrototype.unity`)이라 PawnSim 쪽
  빌드에는 영향 없다.
- `Models/Naval/ship-pirate-medium.fbx` + `Textures/colormap.png` — Kenney
  Pirate Kit(CC0). §8 참고.

Built-in RP 확정: `Packages/manifest.json`에 URP/HDRP 없음 + `ProjectSettings/
GraphicsSettings.asset`의 `m_CustomRenderPipeline: {fileID: 0}` 로 직접 확인함
(전에는 패키지 부재로만 추정했던 것 — 이번에 실측 완료).

## 7. 검증 방식과 실제로 확인된 것

Unity batchmode로 씬 생성 → 빌드 → 실행 파일을 `-screenshot`/`-delay` 플래그로
띄워 `AutoScreenshotter`(PawnSim 기존 하네스, 그대로 재사용)가 스크린샷을
찍고 종료하게 했다. 코드가 컴파일된다는 것과 실제로 그렇게 동작한다는 것은
다른 얘기라서, 스크린샷 + `Player.log`를 실제로 읽어 확인했다:

- 바다: 스크린샷에서 파도 형태가 실제로 보임(정적 평면이 아님).
- 부력: `ShipBuoyancy`에 임시로 찍은 로그로 pitch -6.9~12.4°, roll -26.4~26°가
  파도 높이차를 따라 변하는 것을 확인(정적 텍스트로 "된다"고 주장하지 않고
  숫자로 확인 — observe-dont-speculate 원칙).
- 이동: `-autopilot`(키 입력 없이 스로틀·방향타를 시간에 따라 넣는 테스트
  훅) 경로로 pos.z 0→19.9, yaw(euler.y) 0→24°를 로그로 확인 — 스로틀 전진과
  방향타 회전이 실제로 배를 움직인다.

**실제로 발견하고 고친 버그 1건**: 3D 카메라 리그로 전환하면 검은 화면이었다.
스크린샷만 보고 "회전 계산이 잘못됐다"고 추측할 뻔했으나(비슷한 사례로 처음엔
2.5D 스크린샷의 배 모양도 버그로 오인했다가, 로그로 camPos/camEuler가 의도한
값에 정확히 수렴한 걸 확인하고서야 "긴 배를 부감으로 보면 세로로 길어 보이는
게 정상"이라는 걸 알았다), `CameraRig3D`에도 같은 진단 로그를 찍어서 원인을
확인했다 — `CameraModeSwitcher.ApplyState()`가 `GameObject.SetActive()`만
하고 `Camera.enabled`는 그대로 둬서, 씬 생성 시 꺼둔 카메라 컴포넌트가 계속
꺼진 채였다. 수정 후 재검증 완료.

## 8. 검증 못 한 것 (명시 — 거짓 검증 금지)

- **실제 키보드 입력 경로**: `-autopilot`은 `Input.GetAxis` 대신 코드로 값을
  주입하는 테스트 전용 우회 경로다. 실제 WASD/화살표 입력이 `ShipController`
  까지 제대로 연결되는지는 사람이 직접 플레이해야 확인된다.
- **Tab 키 카메라 전환**: `-forcecam3d`도 마찬가지로 시작 상태를 강제하는
  CLI 훅이다. 런타임에 Tab을 눌러 전환되는지는 미검증.
- ~~**3D 배 모델**: 박스 placeholder~~ → **해결(2026-08-12, 같은 세션 후속)**.
  운영자 피드백 "너무 허접한데 이건... 배 모양의 모델링부터 해야겠어"를 받고,
  유료 3D 생성(Meshy.ai/Tripo 등, money firewall 대상)이 아니라 **Kenney
  Pirate Kit**(CC0, kenney.nl/assets/pirate-kit, 다운로드에 로그인·결제 불필요)
  의 `ship-pirate-medium.fbx`로 교체했다. PawnSim도 이미 Kenney 팩(2D)을 쓰고
  있어서 같은 경로 — `game-artist` 에이전트의 "Kenney CC0 우선, SDXL은 최후
  수단" 우선순위와도 일치한다. 실측 치수(가로 4.8m·세로 9.96m[돛대 포함]·
  길이 10.6m)를 코드에서 읽어 부력 샘플점·카메라 거리를 동적으로 맞췄다 —
  하드코딩 대신 `Renderer.bounds`에서 계산(`NavalSceneSetup.SetupShip()`).
  라이선스: CC0, 상업적 이용 가능, 표기 의무 없음(권장 사항일 뿐).
  파일: `Assets/Models/Naval/ship-pirate-medium.fbx` + `Textures/colormap.png`.

## 9. 다음 세션 시작점

1. 운영자가 직접 플레이 — WASD 이동감 + Tab 카메라 전환 실검증.
2. §8의 3D 모델 생성 경로 결정 (로컬 오픈소스 vs 유료 API 승인).
3. `naval-sail-prototype.yaml`의 §5 Day 5(폴리시 + repro 시나리오)로 이어서.
