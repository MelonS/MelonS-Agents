> 출처: 세이브/로드 무결성 4렌즈 감사 워크플로 wf_9daa2cb1 (37 에이전트, 확정 30건 — 적대 검증 통과분만).

# 세이브/로드 무결성 백로그 — 4렌즈 감사 합성 (검증 통과분)

## 1) 평결

'재시작 후 로드'는 현재 **건물 뼈대만 남는 반쪽 복원**이다. 완성 구조물 16종·폰(부위HP/스킬/트레잇)·나무·연구·게임시계·레이드 스케줄은 살아나지만, 경제의 실체가 통째로 빠진다 — wood/food 카운터는 복원되는데 물리 더미(WoodPile/StoneChunk/MeatPile)가 0개라 "카운터 = Σ InStockpile 더미" 불변식이 깨져 hauling이 유령 잔고 위에서 정지하고, stone/meals/fineMeals 카운터는 아예 0으로 리셋돼 석재 건축·비축 식사가 전량 증발한다. 폰 장비는 로드마다 랜덤 재롤(연구 보상 활 포함 소실), 지붕·날씨·진행 중 청사진·thought 스택도 사라진다. 같은 세션 F9도 안전하지 않다: 같은 프레임 Destroy→위치매칭 레이스로 트리 종·작물 성장도·벌목 지정이 비결정적으로 증발하고, 시체 재로드 시 '동료 사망' -15가 중복 가산되며, 청사진·밴딧·더미는 파괴 목록에 없어 저장 시점과 현재가 뒤섞인다. 요약: 저장 표면이 #버그헌트2의 '구조물 중심'에서 멈췄고, 경제·전투·환경 상태가 다음 확장 대상이다.

## 2) TOP-9 (임팩트÷노력 순)

### 1. [P0·S] 같은 프레임 Destroy→위치매칭 레이스 — 서브상태가 '곧 파괴될' 옛 엔티티에 적용
- **근거**: `GameSaveButtons.cs:90-113` (plain Destroy 루프) → 같은 호출스택 `:247` ApplyLoadedSubStates. `SaveLoadManager.cs:480-493` FindNearest가 strict `sq < bestSq`(490행)라 거리 0 동률 시 열거 순서 첫 후보(파괴 대기 중 옛 엔티티) 승리, 487행 null 가드는 파괴 대기 객체를 못 거름.
- **수정**: 트리/StructureTag/작물/스톡파일 4개 파괴 루프에서 Destroy 직전 `gameObject.SetActive(false)` 한 줄씩 추가 (FindObjectsByType 기본 호출은 비활성 제외). 폰 루프(91-92)는 위치매칭 안 하므로 불필요.
- **영향 범위**: 트리 종(407-417)·작물 성장도(434-444)·벌목 지정(450-460, doomed 나무에 ChopTarget 부착 후 소멸)이 매 F9마다 순서운에 따라 증발. 벽 재질/침대 품질/스톡파일 우선순위는 무사(아래 §4).
- **Effort**: S

### 2. [P0·S] 자원 카운터 stone/meals/fineMeals 미저장 (발견 4건 병합)
- **근거**: `SaveLoadManager.cs:104-105` (SaveData에 wood/food만), `:154-158` (Save 캡처 2종뿐), `GameSaveButtons.cs:116-120` (복원 2종뿐). `ResourceManager.cs:18/20/22`에 meals/stone/fineMeals 실존 — stone은 WallStone/FloorStone 비용(`BuildManager.cs:71/93`), meals/fineMeals는 식사 게이트(`PawnNeeds.cs:612`, 차감 739/750).
- **수정**: SaveData에 int 3개 추가 → Save()에서 캡처 → OnLoad 118-119행 옆에 대입. 복원 후 `OnChanged` 발화(또는 AddX 델타 사용)해 ResourceCounterUI 즉시 갱신. 구 세이브는 JsonUtility 기본값 0이라 가드 불필요.
- **증상 정밀화**: 재시작 로드 = 0 리셋(석재 건축 불능+식사 전멸), 같은 세션 F9 = 롤백 안 됨(스냅샷 비일관) — 양방향 파손.
- **Effort**: S

### 3. [P0·M] 바닥 더미 3종(WoodPile/StoneChunk/MeatPile) 미저장·미파괴 (발견 3건 병합)
- **근거**: `WoodPileEntity.cs:17-25`, `StoneChunkEntity.cs:12-13`, `MeatPileEntity.cs:12-28` — SaveData에 대응 리스트 없음, OnLoad 파괴 목록(90-113)에도 없음. #자원모델 단일화(`BuildManager.cs:646-653`) 이후 더미가 곧 실물 인벤토리: 건축 가용량 합산 `BuildManager.cs:567-581`, 불변식 '카운터 = Σ InStockpile 더미' `ResourceManager.cs:64-68`/`PawnHauler.cs:429-430`. 시작 자원도 물리 더미(`GameManager.cs:227` 목재 6×50, `:243-245` 간편식 — SaveExists 가드 없음 → 재시작+로드 시 중복 확정).
- **수정**: `PileSave{kind, position, amount, inStockpile, label, lifetime/decayPerDay, durability}` 리스트 추가(3종 FindObjectsByType 캡처). OnLoad에서 3종 전부 Destroy 후 각 static Spawn 헬퍼로 재생성 + InStockpile/durability 세터. **필수**: 재구성 후 ResourceManager 카운터를 Σ InStockpile 더미로 재동기화(유령 잔고 차단). 주의: StoneChunk는 durability 없음(부패 없음), MeatPile은 label("간편식")·decayPerDay 보존 필수(미보존 시 비부패식이 생고기 속도로 부패).
- **Effort**: M

### 4. [P1·S] PawnEquipment 미직렬화 — 로드마다 무기/방어구 랜덤 재롤 (발견 3건 병합)
- **근거**: `PawnEquipment.cs:95-114` Awake가 매 Instantiate마다 재롤(랜덤 Equip 102-105행, 모자 50%/무기 70%), `SaveLoadManager.cs:8-34` PawnSave에 장비 필드 전무, OnLoad는 폰 파괴(91-92)→Instantiate(126)로 재롤 발동. 연쇄: 연구 완료 복원(`GameSaveButtons.cs:252-263`)이 completed 직접 set이라 simple_bow 활 지급(`ResearchManager.cs:152-167`)도 재발화 안 됨 → 사격 게이트(`PawnUtilityAI.cs:494-498`)가 IsUnlocked && HasRangedWeapon 둘 다 요구해 '연구 완료했는데 원거리 불능'.
- **수정**: PawnSave에 슬롯별 Catalog 인덱스 `int[4]`(미장착 = -1 센티널) 추가 — Catalog는 static 고정 배열이라 인덱스 안정. OnLoad에서 Instantiate 직후 equipped.Clear() 후 저장 인덱스로 Equip/-1은 Unequip, 이후 SyncThoughtBaseline 재호출. 구 세이브는 기존 skillLevels 길이-가드 패턴 재사용. 이 수정으로 연구 보상 활 소실도 함께 해결.
- **Effort**: S

### 5. [P1·S] 시체 재로드 시 사망 사이드이펙트 재발화 — '동료 사망' -15 중복 가산
- **근거**: `GameSaveButtons.cs:141` RestorePartState → `PawnHealth.cs:114` CheckDeath 무조건 호출 → 새 인스턴스는 IsDead=false라 최초사망 분기(`PawnHealth.cs:268-313`) 재실행: 사망 알림(282행) + 전체 브로드캐스트 AddThought("동료 사망")(306-309행). 먼저 스폰된 생존 림은 이미 SyncThoughtBaseline(156행) 후라 저장 mood(-15 포함) 위에 -15 델타가 또 얹힘.
- **수정**: RestorePartState에 quiet 플래그 추가 — 복원 경로에서 알림/동료사망 브로드캐스트/FloatingText 스킵. 다운 상태 알림 분기(319-325행)도 같은 플래그로 커버.
- **Effort**: S

### 6. [P1·S] 지붕(RoofDesignation) 셀 미저장·미클리어
- **근거**: `RoofDesignation.cs:113` private Dictionary가 유일한 지붕 상태, 공개 접근자 `Roofed`(187행) 있는데 SaveData에 지붕 리스트 없음, OnLoad도 클리어 안 함(F9 시 저장 이후 지붕 잔존, 재시작 시 전부 소실).
- **수정**: SaveData에 `List<Vector2> roofCells`(Roofed 캡처) 추가. 로드 시 전체 Erase 후 복원용 오버로드로 재지정 — 단 `DesignateCell`(314행) 직접 호출 금지(playBlip/fx가 셀당 발화해 파일 자체의 버그패턴 #4 방화벽 위반): playBlip:false, fx:false, doneTime=Time.time(즉시 BUILT) 시그니처 추가.
- **Effort**: S

### 7. [P1·S(최소)] 밴딧 미파괴 + 레이드 스킵 치트
- **근거**: OnLoad 파괴 목록(`GameSaveButtons.cs:91-113`)에 BanditEnemy 없음(MonoBehaviour 직접 상속이라 PawnEntity 루프에 안 잡힘 — `BanditEnemy.cs:18`). RestoreRaidState(`:271-272` → `AIDirector.cs:147-151`)는 스케줄만 복원. 습격 중 저장→재시작→로드 시 밴딧 0인데 lastRaidDay 복원 + 간격 게이트(220행, 3일)로 그 레이드 통째 증발 = 무료 스킵, raidCount는 선증가(244행)라 난이도만 누적. 같은 세션 F9는 미래의 밴딧이 복원된 폰들을 즉시 공격.
- **수정(최소)**: OnLoad 파괴 블록에 BanditEnemy + ArrowProjectile 일괄 Destroy 추가 — 시간 역설 제거. 완전 직렬화 여부는 §3 운영자 결정.
- **Effort**: S(최소수선) / M(완전 직렬화)

### 8. [P1·M] 진행 중 청사진(BlueprintEntity) 미저장·미파괴 (발견 4건 병합)
- **근거**: `BlueprintEntity.cs:23-24` collectedWood/Stone, `:28` Progress(private set) — SaveData에 리스트 없음(완성물 StructureTag만 `SaveLoadManager.cs:281-285`), 파괴 목록에도 없음. 청사진은 StructureTag 미부착(bare GameObject 스폰 `BuildManager.cs:637-644`; 태그는 SpawnFinished 389-390에서 완성물만). 재시작 = 주문+투입 자재 영구 소실(자재는 pickup 시 더미 Destroy — `PawnHauler.cs:432/531`), 같은 세션 F9 = 잔존 청사진+카운터 롤백으로 실질 자재 복제.
- **수정**: `BlueprintSave{mode, position, collectedWood, collectedStone, progress}` 추가. OnLoad에서 전부 Destroy 후 재생성 — 단 PrefabFor는 private(`BuildManager.cs:395`)이므로 public `SpawnBlueprint(mode, pos)` 헬퍼 신설(637-644 경로 미러: Floor류 2s/기본 5s, PaysWithStone 분기 포함), Progress 복원용 `RestoreState()` 세터 추가(DepositX는 need 클램프라 자재 복원에 재사용 가능).
- **Effort**: M (최소수선 = 파괴만 추가 S — §3 결정 필요)

### 9. [P2·S 묶음] 환경/폰 보조 상태 4종 — 각각 한나절 미만
| 항목 | 근거 | 수정 | Effort |
|---|---|---|---|
| AIDirector 이벤트 스케줄 미재조정 — 로드 직후 이벤트 1발 즉발(사기-10/자원 드랍 등 실효과) 또는 장시간 침묵 | `AIDirector.cs:57, 178-181, 189-194`; 시계 복원만 하는 `GameSaveButtons.cs:267-272`. #버그헌트3이 레이드만 고치고 이벤트 스케줄러 누락 | public `RescheduleEvents()`(ScheduleNext 래핑) 추가, SetGameSeconds 직후 호출 | S |
| WeatherController 미저장 — 재시작 시 폭풍→맑음, F9 시 미래 StormUntil로 폭풍 비정상 연장 | `WeatherController.cs:16-17, 53-56` (StormUntil private set, ForceStorm은 풀듀레이션 고정) | SaveData에 kind+잔여초, `RestoreState(kind, remainSec)` 신설. 최소패치는 OnLoad에서 ForceClear() | S |
| PawnThoughts.active 미직렬화 — 활성 thought 역델타 영영 안 와 mood 잔차 누적 | `PawnThoughts.cs:24, 139-150`; `GameSaveButtons.cs:156` SyncThoughtBaseline이 빈 스택을 '적용됨' 고정 | PawnSave에 thought {label, offset, 남은초} 배열, SyncThoughtBaseline 직전 AddThought 복원(label dedupe로 장비 thought와 충돌 없음) | S |
| 스톡파일 마커 재시작 후 비가시 — null 마커면 SpriteRenderer 자체 미생성, 우선순위 틴트까지 소실 | `GameSaveButtons.cs:102-113, 239`; `StockpileZoneEntity.cs:163-167` | 신규 코드 대신 기존 `StockpileDesignation.ZoneSprite()`(384-395행) public화해 Spawn의 null 폴백으로 사용 | S |

**후순위 (P2 하단)**: 베리덤불 잔량(`BerryBushEntity.cs:42, 59` 만재 리셋 = 식량 복제 치트; BushSave+세터+`RegrowthScheduler.cs:73-81` 재enqueue, S) · 경작 지정 셀(`GrowZoneDesignation.cs:130`; 미파종 셀만 손실 — public 게터 신설+`DesignateCell`(297행) 재마킹, **재구성 완료 후** 호출해야 IsPlantableCell 점유 검사가 유효, S) · 동물 길들임(`AnimalEntity.cs:72-76`; AnimalSave+RestoreState — SetSpecies가 Hp 리셋(57행)하므로 **이후** 복원+파란 틴트 재적용, M).

## 3) 운영자 결정 필요 (밸런스/설계)

1. **시작 자원 더미 중복 정책** — #3 수정의 전제. GameManager 시작 스캐터(`GameManager.cs:207-249`, 가드 없음)를 'SaveExists 시 스킵'할지 '로드 시 전량 파괴 후 재구성'할지. 신규게임 첫인상과 직결되는 흐름 설계.
2. **레이드 중 로드 의미론** — 밴딧 완전 직렬화(레이드가 로드 후 재개) vs 최소수선(파괴 = 저장-재시작이 무료 레이드 스킵으로 남되 escalation은 누적). 난이도 곡선 결정.
3. **청사진 최소수선 수위** — 파괴만(같은 세션 시간역설 차단, 재시작 시 진행 공사 포기 허용) vs 완전 복원(M). 프로토 단계에서 투입 자재 손실을 용인할지.
4. **경작 구역의 일회성 설계** — 현재 PruneZone(`GrowZoneDesignation.cs:461-478`)이 파종 즉시 지정을 드롭해 수확→자동 재파종 루프 자체가 없음. 영구 농장 구역으로 바꿀지는 세이브 수정과 독립된 설계 결정.
5. **늑대 게이트** — `AIDirector.cs:46` WolvesEnabled=false로 현재 스폰 불가. 파괴 루프에 WolfEnemy 포함은 게이트 해제 대비 선제 조치일 뿐, 해제 시점은 별도 결정.

## 4) 이미 돌아가는 것 — 건드리지 말 것

- **완성 구조물 16종 단일 재구성 경로** — StructureTag 저장(`SaveLoadManager.cs:281-285`) → SpawnFinished 재구성(`GameSaveButtons.cs:216-220`, `BuildManager.cs:359-393`). "빌드 결과 == 로드 재구성 결과" 보장 유효.
- **벽 재질·침대 품질** — SpawnFinished가 mode에서 직접 세팅(`BuildManager.cs:374-388`) → #1 doomed-매칭 레이스의 영향권 밖 (적대 검증으로 무혐의 확정).
- **스톡파일 우선순위** — `GameSaveButtons.cs:239`가 Spawn 인자로 직접 전달 → ApplyLoadedSubStates의 위치매칭은 중복일 뿐 무해 (무혐의 확정).
- **채광 지정(mineMarks)** — StoneVeinEntity는 OnLoad에서 파괴되지 않아 doomed 쌍둥이가 애초에 없음(`SaveLoadManager.cs:462-473`). 정상.
- **FindNearest used-set 1:1 매칭** 자체(`SaveLoadManager.cs:480-493`) — 과거 데이터 손상을 고친 유효한 설계. 문제는 알고리즘이 아니라 #1의 호출 타이밍뿐.
- **폰 복원 체인** — 부위HP/출혈/붕대 + SyncHpFromHealth(`GameSaveButtons.cs:141-144`), 이름 시드 트레잇 결정성(ReRollFromName, 137행), 스킬 length-가드. 정상.
- **mood 재가산 차단** — MarkTraitsApplied + SyncThoughtBaseline(#버그헌트4, 155-156행). 장비 thought 한정으로 정확히 동작 — #4/#5 수정 시 이 패턴 위에 얹을 것, 교체 금지.
- **게임시계 + 레이드 스케줄러 복원** — #276 + #버그헌트3(`GameSaveButtons.cs:267-272`, `AIDirector.cs:147-151`). 정상 — 누락은 이벤트 스케줄러 쪽뿐.
- **ReservationManager.Clear()**(`GameSaveButtons.cs:88`) — orphaned 예약 방지. 정상.
- **청사진 파괴 시 예약 참조 안전성** — Unity destroyed-null 동치로 ReservedBy/HaulReservedBy 자동 무해화(`BlueprintEntity.cs:30-31, 39`) — #7/#8의 파괴-추가 수정에 예약 후처리 불필요 (검증 확인).
- **구 세이브 하위호환 패턴** — JsonUtility 기본값 + 빈 리스트/배열 길이 가드. 신규 필드 전부 이 패턴 준수하면 마이그레이션 코드 불필요.
