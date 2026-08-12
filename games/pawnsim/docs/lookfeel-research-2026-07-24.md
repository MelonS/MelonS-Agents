# 룩앤필 획기적 개선 리서치 — 2026-07-24

운영자 지시: "전체 룩앤필을 어떻게 획기적으로 개선할지 리서치해줘".
같은 날 진행된 실행 라운드(`lookfeel-round-2026-07-24.md`)와 중복 없이, **그 다음 단계**의
기법을 임팩트/공수/WebGL 리스크로 평가한다.

- 오늘 완료분 (재제안 금지): 나무 실루엣 6종+종 5종(L1/L2), 풀 포기(L3), 흙 발자국(L4),
  메뉴 FLUX 키아트, EL SFX 세대교체(L8)
- 잔여 대기분 (이 문서가 실행 방법을 구체화): 물가 엣지(L5), 야간 광원 강화(L6),
  UI 팔레트(L7), 폰트(L9)

---

## 0. 현재 상태 진단 (코드 + 스크린샷 기준)

**렌더 파이프라인 = 빌트인.** `Packages/manifest.json` 에
`com.unity.render-pipelines.universal` **없음**. URP 2D Lights / Volume 포스트프로세싱 /
Shader Graph / URP 내장 Pixel Perfect Camera 모두 현재 사용 불가. 이게 이 문서의
가장 큰 갈림길이다 (§2).

**현행 라이팅** (`NightOverlay.cs` + `FlickerLight.cs`):
- NightOverlay = 160×160 텍스처를 **매 프레임 CPU 로 페인트**(SetPixels32+Apply)하는
  동적 라이트맵. 어둠 알파를 램프 반경만큼 걷어내고 촛불색을 입힘 + 벽/문 LOS 가림.
  구조 자체는 레퍼런스 콜로니심 방식과 같고 잘 만들어져 있으나:
  - **알파 오버레이는 "덮는" 방식**이라 밤에도 낮에도 화면의 채도·대비를 끌어올리지
    못한다 (탁해지기만 함). 색 재매핑(그레이딩)이 아니라 색 위에 색을 얹는 것.
  - 매 프레임 ~102KB 텍스처 업로드 — WebGL 에서 텍스처 업로드는 비싼 축에 속한다.
  - 해상도 160px 를 화면 전체에 늘리므로 빛 경계가 뭉개진 저해상 그라데이션.
- FlickerLight = 가산 오버레이 + 불티 파티클. 방향성/볼륨감 없음(노멀맵 불가 구조).

**이미 있는 것 (중복 제안 금지 인벤토리)**: BlobShadow(이동체 그림자),
TreeSwayDriver(나무 스웨이), PawnSpriteBob(걷기 밥/숨쉬기), VignetteOverlay(비네트),
WeatherParticleDriver(비), ParticleFx(벌목칩/채광파편 버스트), ClickEffect,
SelectionRing, CombatLungeDriver, FloatingText, 시간대별 어둠 색 커브(DARK_STOPS).

**스크린샷 진단** (`02_after_move.png`, 인게임 1440p):
1. **전체 톤이 균일하게 탁하다** — 잔디 그린과 흙 브라운이 채도 낮은 중간값에 몰려
   있고, 하이라이트/섀도우 대비가 없어 "머드" 느낌. 가장 시급.
2. **팔레트 불일치** — 스프라이트가 절차 생성(개별 gen 스크립트)이라 램프(색 계단)가
   에셋마다 제각각. 화면이 "한 게임"으로 안 묶임.
3. **화면이 정적** — 나무 스웨이는 미세해서 스틸에선 안 보이고, 대기(atmosphere)
   레이어(구름 그림자·부유 파티클)가 0.
4. **줌 스케일이 비정수** — 픽셀 밀도가 오브젝트마다 달라 보이는 프레임 존재
   (픽셀 퍼펙트 규율 부재).

결론: "획기적" 개선은 **개별 스프라이트 추가가 아니라 화면 전체에 곱해지는
레이어**(그레이딩·팔레트·대기·조명)에서 나온다. Graveyard Keeper 개발사의 브레이크다운이
정확히 이 결론을 뒷받침한다 — 그 게임 룩의 뼈대는 (1) 시간대별 LUT 컬러 그레이딩
(2) 다이나믹 앰비언트+노멀맵 라이팅 (3) 바람/안개 셰이더였다.

---

## 1. 기법 카드

임팩트: 상/중/하 = 스크린샷 한 장이 달라 보이는 정도.
공수: 이 코드베이스(자기부착 드라이버 패턴, 절차 생성 에셋) 전제 일수.

### A. 컬러 — 화면 전체에 곱해지는 레이어

#### A1. 시간대별 LUT 컬러 그레이딩 ★ 최우선
- **무엇**: 화면 최종 색을 룩업 테이블(LUT)로 리맵하는 풀스크린 패스. 시간대별 LUT
  (한낮/황금시간/황혼/밤/새벽 5~10장)를 GameClock 진행도로 블렌드.
- **왜 획기적**: 알파 오버레이(현행)는 덮어서 탁해지지만, LUT 은 **픽셀 값 자체를
  재매핑**한다 — 한낮엔 채도·대비를 끌어올리고, 황혼엔 섀도우를 보라로 밀고
  하이라이트는 주황으로 남기는 식의 "영화 그레이딩"이 가능. Graveyard Keeper 가
  시간대별 LUT 10장으로 전체 무드를 만든 것이 장르 최고 사례. 진단 1번(탁함)의
  근본 해법이며, 아트 에셋을 한 장도 다시 그리지 않고 화면 전체가 달라진다.
- **적용법**: 빌트인에서 `OnRenderImage` + `Graphics.Blit` + 2D 스트립 LUT 셰이더
  (256×16 strip — 모바일/WebGL 폴백으로 검증된 방식, Unity User LUT 문서 참조).
  LUT 텍스처는 파이썬으로 절차 생성 가능(중립 LUT 에 커브 적용 스크립트) — 이
  프로젝트의 에셋 파이프라인과 정확히 맞는다. NightOverlay 는 **유지**하되 역할을
  "램프 리빌 라이트맵"으로 축소하고, 전역 어둠 틴트 부분을 LUT 로 이관.
- **임팩트 상 / 공수 1~2일 / WebGL 리스크 하** (풀스크린 1패스 + 텍스처 룩업 1회 —
  프래그먼트 비용 미미. 단, WebGL 에서 `OnRenderImage` 동작 확인 스모크 필수,
  실패 시 카메라 타깃 RT + 수동 블릿으로 대체).

#### A2. 마스터 팔레트 통일 (hue-shift ramp 재구축)
- **무엇**: 게임 전체가 쓰는 단일 마스터 팔레트(램프당 4~5색, 섀도우=쿨 시프트,
  하이라이트=웜 시프트)를 정의하고, 모든 절차 생성 스프라이트가 이 팔레트에서만
  색을 뽑도록 gen 스크립트를 정리 + 기존 PNG 일괄 최근접 색 매핑.
- **왜 획기적**: "아마추어 vs 프로 픽셀아트"를 가르는 단일 요인이 hue-shift 램프다.
  단순 명도 계단(현행 gen 스크립트 다수가 이 방식)은 회색빛 머드가 되고, 섀도우를
  블루/퍼플로, 하이라이트를 옐로/오렌지로 밀면 같은 32px 이라도 색이 "울린다".
  제약(팔레트 고정)이 곧 화면 통일감 — 진단 2번의 근본 해법.
- **적용법**: ① 마스터 팔레트 설계(잔디/흙/목재/석재/식생/스킨 램프 + UI 액센트 —
  L7 UI 팔레트와 **같은 소스**로 묶으면 월드와 UI 가 처음으로 한 색계가 된다)
  ② `_palette.py` 공유 모듈 → 전 gen 스크립트 import ③ 기존 PNG 리매핑 배치 스크립트
  + 전후 스크린샷 격리 채점 ④ 운영자 픽(아트 취향 게이트 — L7 절차와 동일하게).
- **임팩트 상 / 공수 2~3일 / WebGL 리스크 없음** (순수 에셋 단계, 런타임 비용 0).

#### A3. 디더링 그라데이션 (곁들임)
- **무엇**: 조명 경계·황혼 그라데이션에 오더드(Bayer) 디더를 섞어 밴딩 제거 + 레트로 질감.
- **적용법**: NightOverlay 페인트 루프에서 알파 양자화 + 4×4 Bayer 행렬. A1 LUT 셰이더에
  노이즈 디더 한 줄로도 가능.
- **임팩트 하~중 / 공수 0.5일 / 리스크 없음**. 단독으론 약하고 A1/조명 작업에 얹을 때 가치.

### B. 대기(atmosphere) — 정지 화면을 살아있게

#### B1. 구름 그림자 스크롤 ★ 가성비 최고
- **무엇**: 커다란 소프트 노이즈 텍스처(멀티플라이 블렌드, 알파 0.08~0.15)가 맵 위를
  천천히(~0.3타일/s) 흘러가는 레이어 1~2장. 바람 방향과 동기화.
- **왜 획기적**: 톱다운 심 장르에서 "화면이 살아있다"는 인상을 만드는 검증된 최저비용
  기법. 스틸 스크린샷 한 장에도 명암 변화가 찍히고, 플레이 중엔 맵 전체가 호흡한다.
  현행 화면의 "균일 조명 평면" 느낌(진단 3번)을 곧바로 깬다.
- **적용법**: 파이썬으로 512² 펄린 노이즈 PNG 1장 생성 → 자기부착 드라이버
  (WeatherParticleDriver 패턴)가 카메라 추종 대형 SpriteRenderer 2장을 다른 속도/스케일로
  스크롤. 멀티플라이 머티리얼(Sprites/Default 틴트 흑색+알파로도 근사 가능). 흐린 날씨엔
  밀도 증가(WeatherController 연동), 밤엔 페이드아웃(NightOverlay.CurrentDarkness01 참조).
- **임팩트 상 / 공수 0.5~1일 / WebGL 리스크 하** (오버드로 1~2레이어, 반투명 풀스크린
  스프라이트 2장 수준 — 기존 NightOverlay 도 같은 급 오버드로를 이미 쓰고 있음).

#### B2. 앰비언트 파티클 스위트 (계절·시간대별)
- **무엇**: 낮=꽃가루/민들레 씨앗 부유·바람에 날리는 잎, 밤(여름)=**반딧불**,
  겨울=눈. 화면당 수십 개 상한의 저밀도 부유 입자.
- **왜 획기적**: 비(있음) 외에 대기 입자가 0 인 상태 → 첫 도입 체감이 큼. 특히
  반딧불은 밤 램프 라이팅과 시너지가 나는 장르 시그니처 컷.
- **적용법**: WeatherParticleDriver 패턴 그대로 — 카메라 추종 ParticleSystem, 코드 생성,
  계절(GameClock)+시간대 게이트. 반딧불은 2~3px 웜 그린 점 + 알파 사인 펄스.
  ParticleFx.SharedMaterial 재사용.
- **임팩트 중상 / 공수 1일 / WebGL 리스크 하** (maxParticles 상한 두면 무시 가능).

#### B3. 전역 바람 시스템 (스웨이 통합 + 돌풍)
- **무엇**: TreeSwayDriver 의 개별 사인 스웨이를 전역 WindManager 로 승격 — 돌풍이
  맵을 **가로질러 지나가면** 나무→풀 포기→작물이 위치 순서대로 시간차 반응
  (위상 = 월드좌표·바람방향 내적).
- **왜 획기적**: 개별 진동(현행)은 "각자 떨림", 파도형 전파는 "바람이 분다"로 읽힌다.
  Graveyard Keeper 가 밀밭 버텍스 셰이버로 만든 효과의 스프라이트 버전. 오늘 추가된
  풀 포기(L3)가 흔들림에 합류하면 바닥 생명감이 완성된다.
- **적용법**: WindManager 싱글턴(방향+강도+돌풍 커브) → TreeSwayDriver·풀 포기
  스캐터·CropEntity 가 위상 오프셋으로 구독. B1 구름 스크롤 방향과 동기화하면
  일관성 보너스.
- **임팩트 중 / 공수 1일 / WebGL 리스크 없음** (기존 스웨이와 동일 비용 구조).

#### B4. 물 셰이더 (L5 물가 엣지의 완성형)
- **무엇**: 물 타일에 ① 흐르는 노이즈 왜곡/색 변조 ② 픽셀 스펙큘러 글린트(반짝임)
  ③ 기슭 폼(거품) 라인 애니메이션.
- **왜 획기적**: 물은 톱다운 맵의 자연 시선 집중점 — 정지 단색 물(현행)과 반짝이는
  물의 차이가 맵 전체 인상을 좌우. 커뮤니티에 검증된 레시피 다수(Cyanilux 2D water
  breakdown 등 — 픽셀화 UV + 노이즈 텍스처 조합).
- **적용법**: 빌트인 커스텀 스프라이트 셰이더 1개(수동 CG — Shader Graph 없이 가능,
  픽셀화 UV 로 노이즈 샘플). L5 엣지 밴드 타일이 먼저 있어야 폼 라인이 설 자리가
  생기므로 L5 후속으로. 글린트만 먼저라면 파티클로도 근사 가능(공수 0.5일 축소판).
- **임팩트 중상(물 있는 맵 한정) / 공수 1~2일 / WebGL 리스크 하** (물 타일 면적만큼의
  프래그먼트 비용).

### C. 게임 필(juice) — 만졌을 때의 반응

#### C1. 나무 쓰러짐 연출 ★ 반복 노출 최다 이벤트
- **무엇**: 벌목 완료 시 나무가 기울며 **쓰러지는** 회전 트윈(0.6~0.9s, EaseInQuad)
  + 잎 파티클 버스트 + 착지 먼지 + 쿵 SFX(EL 세대교체분 활용) + 1px 카메라 킥.
- **왜 획기적**: 벌목은 콜로니심에서 초반 수백 번 반복되는 이벤트 — 여기 juice 를
  넣으면 게임 전체가 "반응하는 게임"으로 체감된다. 현재는 칩 파티클 후 소멸(팝 아웃).
- **적용법**: TreeEntity 파괴 경로에 연출 시퀀스 삽입 — 스프라이트 차일드만 회전
  (PawnSpriteBob 의 루트-트랜스폼 불가침 규약 준수), 완료 후 기존 드롭 로직 호출.
  ParticleFx.Burst 재사용 2회(잎/먼지).
- **임팩트 중상 / 공수 0.5~1일 / 리스크 없음**.

#### C2. 트윈/이징 표준화 패스 (UI + 월드)
- **무엇**: EaseOutBack/OutCubic 등 이징 유틸 1파일 → ① UI 패널 슬라이드+페이드 인
  ② 리소스 카운터 증감 펀치(스케일 1→1.15→1) ③ 알림 팝 ④ 건축 완성 스케일 팝
  ⑤ 아이템 드롭 바운스.
- **왜 획기적**: "juice it or lose it" 원칙의 본체 — 상태가 **즉시 스냅**하는 화면(현행
  UI 대부분)과 이징으로 도착하는 화면의 프로덕션 체감 차이는 크고, 비용은 코드뿐.
  심 장르에선 과장 없는 120~200ms 단발이 정답(과한 juice 는 관리 UX 를 해침 —
  Wayline "juice problem" 논지).
- **적용법**: `Tween.cs` 정적 유틸(코루틴 없는 수동 보간, 언스케일드 타임) → 각 UI 에
  점진 적용. 한 배치에 2~3군데씩 소량 출하(전면 교체 금지 — UI 전례 규약).
- **임팩트 중상(누적형) / 공수 1~2일 / 리스크 없음**.

#### C3. 이벤트 피드백 (플래시·마이크로 셰이크)
- **무엇**: ① 피격 화이트 플래시 1~2프레임(스프라이트 틴트 계약 존중 — 차일드
  오버레이 방식, FlickerLight 패턴) ② 건물 완파/나무 착지/습격 시작 시 1~2px
  스크린셰이크(감쇠 사인, 200ms).
- **왜/주의**: 타격감의 최저비용 코어. 단 심 장르 규율 — 셰이크는 **대형 이벤트
  한정 + 진폭 1~2px + 설정 토글**. 상시/전투 매타격 셰이크는 부적합(§4).
  히트스톱은 다수 폰 동시 시뮬과 충돌하므로 도입하지 않는다(§4).
- **임팩트 중 / 공수 0.5~1일 / 리스크 없음**.

### D. 픽셀 규율·화면 선명도

#### D1. 픽셀 퍼펙트 카메라 (정수 스케일 + 스냅)
- **무엇**: 레퍼런스 해상도 기준 정수 배율 렌더 + 스프라이트 픽셀 그리드 스냅.
  줌은 정수 스텝(×2/×3/×4…)으로 스냅하거나, 비정수 줌 허용 시 저해상 RT 에 렌더 후
  sharp-bilinear 업스케일.
- **왜 획기적**: 진단 4번 — 현재 비정수 줌에서 픽셀 밀도가 뒤섞여 아트가 실제보다
  싸 보인다. 픽셀 밀도 통일은 "화질이 좋아졌다"로 체감되는 규율 항목. 모든 픽셀아트
  상용작의 기본기.
- **적용법**: 빌트인 유지 시 `com.unity.2d.pixel-perfect` 패키지(빌트인 전용 버전 존재)
  또는 CameraController 에 정수 스냅 줌 직접 구현(의존성 0 — 이쪽 권장). URP 전환 시엔
  URP 내장 Pixel Perfect Camera 로 대체. 주의: NightOverlay 등 화면 커버 계산이
  orthographicSize 를 읽는 곳 회귀 확인, UI 스케일과 분리.
- **임팩트 중상 / 공수 1일 / WebGL 리스크 중** (디바이스 픽셀 비율(DPR)·캔버스 크기
  대응 검증 필요 — WebGL 캔버스 CSS 스케일과 이중 스케일링 나면 오히려 흐려짐).

#### D2. 소프트 블룸 — 밤 광원 한정
- **무엇**: 휘도 threshold 블룸을 **밤에만**(CurrentDarkness01 게이트) 램프/불꽃
  글로우에 적용. 낮 강도 0.
- **왜/주의**: 어둠 속 램프가 "번지는" 것은 아늑함의 핵심 신호. 단 픽셀아트 전면
  상시 블룸은 취향이 갈리는 대표 이펙트라(§4) 밤 한정+저강도로만.
- **적용법**: A1 과 같은 풀스크린 패스 체인에 다운샘플 2패스 블룸 추가(빌트인
  OnRenderImage). URP 전환 시엔 Volume Bloom 으로 공짜 대체.
- **임팩트 중 / 공수 1~2일 / WebGL 리스크 중** (다운샘플 패스 2~3회 = 필레이트 —
  저사양 노트북 웹에서 프레임 확인 필수. A1 단독 출하 후 별도 게이트로).

#### D3. NightOverlay 개선 (URP 안 갈 경우의 현실 트랙)
- **무엇**: 현행 CPU 라이트맵 유지 개선 — ① TEX 160→256~320(빛 경계 선명)
  ② 더티 플래그: 램프/시간 변화 없으면 리페인트 스킵(매 프레임 업로드 제거 —
  WebGL 성능 직결) ③ 창문 누광·새벽 골든 아워 강조 등 연출 스탑 추가.
- **임팩트 중 / 공수 1일 / WebGL 리스크 개선됨** (현행보다 업로드 빈도 감소).
  L6(야간 광원 강화)의 빌트인 버전 실행안.

### E. 조명 대격변 — URP 트랙 (§2 의 결정 이후)

#### E1. URP 2D Lights 전환
- **무엇**: Global Light 2D(시간대 색/강도) + Point Light 2D(램프·화덕·모닥불) +
  ShadowCaster2D(벽 — 현행 CPU LOS 코드 삭제) 기반 GPU 2D 라이팅.
- **왜 획기적**: 조명 해상도가 화면 네이티브가 되고(현행 160px 맵), 부드러운 falloff·
  색 혼합·그림자·노멀맵 반응이 엔진 표준으로 확보된다. "조명이 다른 게임 급"이
  되는 단일 최대 레버. Unity 도 픽셀게임용 pixelated light 옵션을 2D Renderer 에 제공.
- **적용법·비용**: §2 상세. 요점 — 이 프로젝트는 씬을 코드로 생성하고 머티리얼이
  사실상 Sprites/Default 뿐이라 **일반 프로젝트보다 전환이 쉽다**.
- **임팩트 상 / 공수 3~5일+회귀 QA / WebGL 리스크 중** (blend style 1개 제한 등
  최적화 규칙 준수 시 웹 사례 다수. 단 저사양 웹 실측 게이트 필수).

#### E2. 스프라이트 노멀맵 (E1 후속)
- **무엇**: 주요 스프라이트(나무 캐노피·벽·바위·가구)에 노멀맵 → 램프 방향에 따라
  입체로 음영. Graveyard Keeper 시그니처("4방향 라이트 드로잉→노멀맵").
- **적용법**: 절차 생성 파이프라인 강점 — gen 스크립트가 이미 실루엣/로브 구조를
  아니까 높이맵→노멀맵을 **자동 파생** 가능(수작업 0). URP Sprite-Lit 머티리얼에 연결.
- **임팩트 중상(밤 램프 주변 한정, 낮엔 거의 안 보임) / 공수 2~3일 / 리스크 하**.
  32px 스케일에선 은은한 게 정답 — 과하면 §4 플라스틱 함정.

---

## 2. 갈림길: URP 전환 vs 빌트인 유지

| | 빌트인 유지 | URP(2D Renderer) 전환 |
|---|---|---|
| 가능 | A1 LUT, A2 팔레트, A3, B1~B4, C1~C3, D1(패키지/자작), D2(자작 블룸), D3 | 좌측 전부 + E1 2D Lights, E2 노멀맵, Volume 포스트(블룸/그레이딩 공짜), 내장 Pixel Perfect, Shader Graph |
| 조명 상한 | CPU 라이트맵 개선(D3)까지 | 네이티브 해상도 GPU 라이팅 + 그림자 + 노멀맵 |
| 전환 비용 | 0 | 3~5일: 패키지 설치 → 2D Renderer 에셋 → RP Converter → 수동 수정 2곳(FlickerLight 의 `Particles/Standard Unlit` Shader.Find, ParticleFx/BlobShadow 등 `Sprites/Default` → URP 등가) → NightOverlay 를 Light2D 재작성 → **전 QA 스크린샷 파이프라인 재검증** |
| 전환 리스크 | — | 헤드리스/배치모드 스크린샷 QA(AutoScreenshotter)와 URP 호환, WebGL 빌드 사이즈 소폭 증가, 기존 틴트 계약(선택/HP/야간)과 Light2D 상호작용 회귀 |
| WebGL | 검증됨(현행) | 사례 많음. blend style 1개·라이트 수 제한·섀도우 캐스터 수 관리 필수 |

**판단**: 이 프로젝트는 프리팹/씬 의존이 거의 없고(코드 생성) 커스텀 셰이더가 사실상
0 이라 전환 난도는 낮은 편. 그러나 QA 스크린샷 인프라 재검증 비용이 실공수의 절반이다.
**권장 시퀀스 = 빌트인에서 A1/A2/B1 등 화면 전체 레버를 먼저 뽑고(1~2주 내 체감 극대화),
조명이 다시 병목으로 지목되는 시점에 E1 을 단독 마일스톤(기능 동결 배치)으로 전환.**
A1 LUT 셰이더·D1 정수 줌·B 계열은 URP 전환 후에도 전부 그대로 살아남는다(매몰 없음).

---

## 3. 톱10 실행 순서 (임팩트 대비 공수 순, 오늘 완료분 제외)

| # | 항목 | 카드 | 공수 | 임팩트 | 파이프라인 | 비고 |
|---|---|---|---|---|---|---|
| 1 | 시간대별 LUT 컬러 그레이딩 | A1 | 1~2일 | 상 | 빌트인 OK | 탁함의 근본 해법. L6 의 "전역 컬러 그레이딩" 실행안 |
| 2 | 구름 그림자 스크롤 | B1 | 0.5~1일 | 상 | 빌트인 OK | 가성비 최고. 스틸에도 찍힘 |
| 3 | 마스터 팔레트 통일(hue-shift) | A2 | 2~3일 | 상 | 에셋 단계 | L7 UI 팔레트와 같은 소스로 통합. 운영자 픽 게이트 |
| 4 | 나무 쓰러짐 연출 | C1 | 0.5~1일 | 중상 | 빌트인 OK | 반복 노출 최다 이벤트 juice |
| 5 | 앰비언트 파티클(반딧불·꽃가루·잎) | B2 | 1일 | 중상 | 빌트인 OK | 밤 반딧불 = 시그니처 컷 |
| 6 | 픽셀 퍼펙트 정수 줌 | D1 | 1일 | 중상 | 빌트인 OK | WebGL DPR 검증 필수 |
| 7 | 트윈/이징 표준화 패스 | C2 | 1~2일 | 중상 | 빌트인 OK | 소량 배치 분할, UI 전면 교체 금지 규약 준수 |
| 8 | 전역 바람(돌풍 전파) | B3 | 1일 | 중 | 빌트인 OK | 풀 포기(L3)·구름(B1)과 시너지 |
| 9 | 물 셰이더(글린트+폼) | B4 | 1~2일 | 중상 | 빌트인 OK | L5 물가 엣지 후속 |
| 10 | **URP 2D Lights 전환** | E1(→E2, D2) | 3~5일 | 상 | **전환 필요** | 단독 마일스톤. 노멀맵·Volume 블룸 후속 잠금 해제 |

이벤트 피드백(C3)·디더(A3)·NightOverlay 개선(D3)은 위 항목들의 곁들임/폴백으로 흡수.
1~2번만 출하돼도 "다른 게임" 소리가 나오는 구성이 목표 — 3번(팔레트)까지가
스크린샷 기준 체감의 8할.

---

## 4. 하지 말 것 (장르/아트 부적합)

- **CRT/스캔라인/포스퍼 마스크** — 레트로 아케이드 감성. 아늑한 콜로니 관리 톤과
  충돌하고 장시간 UI 판독성을 해침. (에뮬레이터 커뮤니티에서도 "픽셀 크리스프가
  목표면 마스크는 빼라"가 정설.)
- **xBR/HQx 계열 업스케일 필터** — 픽셀아트 형태 자체를 뭉개는 재해석. 32px 아이덴티티
  훼손. 업스케일은 정수 배율 + sharp-bilinear 까지만(D1).
- **히트스톱(전역 시간 정지)** — 다수 폰이 동시에 움직이는 심에서 전역 프리즈는
  버그로 읽힘. juice 는 대상 스프라이트 로컬(플래시·런지)로만.
- **상시/과한 스크린셰이크** — 관리 게임은 화면을 오래 읽는 장르. 대형 이벤트 한정
  1~2px + 설정 토글 없이는 금지. ("과장 피드백이 관리 UX 를 해친다"는 juice 비판론이
  정확히 이 장르에 해당.)
- **크로마틱 애버레이션·필름 그레인·렌즈 더트·DOF·모션블러** — 픽셀 판독성 파괴 +
  WebGL 필레이트 낭비. 도입 논의 자체를 스킵.
- **전면 상시 블룸** — 픽셀아트에서 호불호 최상위. 밤 광원 한정(D2)으로만.
- **노멀맵 과용(전 스프라이트 강한 볼륨)** — 32px 에서 과한 노멀맵은 "플라스틱 피규어"
  느낌. 밤 램프 반경의 은은한 음영까지만(E2).
- **개별 에셋 리터칭으로 룩 올리기** — 스프라이트 하나 예뻐져도 화면 전체 톤이
  탁하면 체감 0. 전역 레버(§1 A/B) 먼저가 이 리서치의 결론.

---

## 5. 출처

- [Graveyard Keeper: How the graphics effects are made — Game Developer](https://www.gamedeveloper.com/programming/graveyard-keeper-how-the-graphics-effects-are-made) (시간대별 LUT 10장, 노멀맵 4방향 드로잉, 바람 버텍스 셰이더, 다중 그림자)
- [Under the hood Graveyard Keeper — Sudonull 미러](https://sudonull.com/post/10024-Under-the-hood-Graveyard-Keeper-How-graphic-effects-are-implemented)
- [Unity Manual: Introduction to 2D lighting in URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/Lights-2D-intro.html) · [Optimize 2D lights](https://docs.unity3d.com/6000.1/Documentation/Manual/urp/2d-lights-optimize-methods.html) (blend style 수 = 렌더 텍스처 수)
- [Unity Manual: Web performance considerations](https://docs.unity3d.com/Manual/webgl-performance.html) · [Web graphics APIs](https://docs.unity3d.com/Manual/webgl-graphics.html)
- [Unity Manual: Migrating Built-In → URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/upgrading-from-birp.html) · [2D 에셋 컨버터](https://docs.unity.cn/6000.6/Documentation/Manual/urp-2d-convert-assets.html)
- [Unity Manual: Pixel Perfect Camera (URP)](https://docs.unity3d.com/6000.7/Documentation/Manual/urp/2d-pixelperfect-ref.html) · [2D Pixel Perfect 패키지(빌트인용)](https://docs.unity3d.com/Packages/com.unity.2d.pixel-perfect@1.0/manual/index.html)
- [Unity Manual: User LUT (2D strip LUT — 저사양 폴백)](https://docs.unity3d.com/2018.3/Documentation/Manual/PostProcessing-UserLut.html) · [Full Screen Shaders in Built-in — GameDevBill](https://gamedevbill.com/full-screen-shaders-in-unity/) · [How video games use LUTs — frost.kiwi](https://blog.frost.kiwi/WebGL-LUTS-made-simple/)
- [Pixelblog — Color Palettes (SLYNYRD)](https://www.slynyrd.com/blog/2018/1/10/pixelblog-1-color-palettes) · [GameDev.net Color Palettes](https://gamedev.net/tutorials/visual-arts/color-palettes-r4964/) (hue-shift 램프, 45° 시프트 팔레트 구축)
- [Disney 12 Animation Principles Applied to Games — GameJuice](https://gamejuice.co.uk/articles/disney-12-animation-principles-games) · [Juice it or Lose it](https://gamejuice.co.uk/resources/juice-it-or-lose-it) · [The "Juice" Problem — Wayline](https://www.wayline.io/blog/the-juice-problem-how-exaggerated-feedback-is-harming-game-design) (과장 피드백 비판 — 심 장르 규율 근거)
- [2D Water Shader Breakdown — Cyanilux](https://www.cyanilux.com/tutorials/2d-water-shader-breakdown/) · [jess-hammer/2d-pixel-water-shader (GitHub)](https://github.com/jess-hammer/2d-pixel-water-shader)
- [Fog for Top-Down Games — kvachev.com](https://kvachev.com/blog/posts/fog-for-topdown-games/) (스크롤 펄린 노이즈 레이어)
- [Retro Game Corps — Shaders/Overlays 가이드](https://retrogamecorps.com/2024/09/01/guide-shaders-and-overlays-on-retro-handhelds/) (CRT vs 클린 업스케일 취향 정리 — §4 근거)
