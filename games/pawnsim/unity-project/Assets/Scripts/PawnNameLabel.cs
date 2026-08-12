using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Day 30: floating name label above the pawn.
    /// 운영자 피드백 2026-05-27: 2번째 라인에 status 표시.
    ///
    /// TOP-2 디클러터 (visual-polish-backlog 2026-06-11): 기존 불투명 다크
    /// 플레이트 + 상시 2줄은 화면 시각위계 1위를 라벨이 점유하는 문제
    /// ("검은 박스가 화면을 지배").  재구성:
    ///   - 플레이트 박스 제거 → 1px 오프셋 그림자 TextMesh (어느 지형 위에서도
    ///     판독, 화면은 안 가림)
    ///   - 활동(status) 줄은 선택된 림만 — 정보는 ComputeStatusLabel 로 항상
    ///     계산되고 CurrentActivity 가 노출하므로 하네스 프로브(pawnActivity/
    ///     selectedOnlyActivity)는 표시 여부와 무관하게 동작
    ///   - 줌 LOD: ortho ≤7 이름(+선택 시 활동) / 7~11 이름만 / >11 라벨 숨김
    ///   - 야간 감광: NightOverlay.CurrentDarkness01 로 알파 1→0.65
    /// </summary>
    public class PawnNameLabel : MonoBehaviour
    {
        // #199 A2 ortho + 1x1 pawn — 라벨을 HP 바(top 0.68) 바로 위로.
        //  순서(위→아래): name(1.06) > status(0.80) > HP 바(0.68) > mood 바(0.55) > 머리(0.5).
        [SerializeField] private Vector3 offset = new Vector3(0, 1.16f, 0);
        // 0.34 → 0.40 (2026-07-31): 활동 줄을 0.60배 → 0.85배로 키웠으므로 줄간격도 함께
        //  벌린다.  하나만 바꾸면 이름줄 아래쪽과 활동줄 윗쪽이 서로 파고든다.
        [SerializeField] private float statusGap = 0.52f;   // name↔status 줄간격 (#3.1)
        [SerializeField] private float fontSize = 64;
        // 2026-07-29 라이브 캡처 — 이름표가 **판독 불가**였다 ("지훈"이 뭉개진 얼룩).
        //  characterSize 0.05 × fontSize 64 는 월드 높이 ~0.32 유닛이고, 기본 줌
        //  (ortho 15, 1080p)에서 1 유닛 ≈ 36px 이므로 **화면상 ~11px**.  한글은 그
        //  크기에서 구조가 무너진다(획 사이가 1px 미만).  0.09 로 올려 ~20px 확보.
        //  offset/statusGap 도 커진 글자에 맞춰 함께 벌린다 — 하나만 바꾸면 이름줄과
        //  활동줄이 겹친다.
        [SerializeField] private float characterSize = 0.09f;
        // TOP-2 LOD 경계 (ortho size).  #카메라파리티: 기본줌 5.5→8 에 맞춰 재조정
        //  (기본 줌에서 이름이 보여야 한다).
        // (lodNameOnly 은 2026-07-31 제거 — 활동 줄이 전원 상시가 되면서 쓰이지 않는다.
        //  이유는 Update 의 showStatus 주석 참조.  죽은 설정값을 남겨 두면 다음 사람이
        //  "이 값을 바꾸면 뭔가 달라지겠지"라고 오해한다.)
        [SerializeField] private float lodHideAll = 17f;    // 이상: 라벨 전체 숨김

        private TextMesh nameTm;
        private TextMesh statusTm;
        private TextMesh[] nameShadowTms;
        private TextMesh[] statusShadowTms;
        private GameObject nameGo;
        private GameObject statusGo;

        /// 현재 머리위 행동 라벨(떠도는중/벌목/수면 등).  인스펙트 패널/하네스 프로브가
        /// 같은 정보를 읽는다.  TOP-2: 표시 여부와 무관하게 항상 계산값을 반환.
        public string CurrentActivity => lastComputedStatus;
        private string lastComputedStatus = "";

        private PawnEntity entity;
        private PawnNeeds needs;
        private PawnChopper chopper;
        private PawnHunter hunter;
        private PawnGatherer gatherer;
        private PawnCook cook;
        private PawnMovement movement;
        // 운영자 2026-06-02: "림이 머하는지 머리위에 항상" — 누락 워커 전부 배선.
        private PawnBuilder builder;
        private PawnHauler hauler;
        private PawnDoctor doctor;
        private PawnHarvester harvester;
        private PawnMiner miner;
        // 연구 작업 도장.  DoResearchAction 이 처음 연구를 잡을 때 폰에 붙으므로,
        //  Awake 시점엔 없을 수 있다 — ComputeStatusLabel 진입 때 한 번 더 잡는다.
        private PawnResearchWork researchWork;
        private PawnSchedule schedule;   // '여가' 슬롯 판정

        private float lastStatusUpdate;

        private void Awake()
        {
            // UI겹침 P2-7 — 동일 셀 수렴 시 이름끼리 100% 합동(order 30==30).
            //  결정적 스태거 (GetInstanceID 홀짝 금지 — 전부 짝수 가능).
            // 스태거 2단(0.18) → 3단(0.42) (2026-07-31).
            //  활동 줄이 전원 상시가 되면서 라벨이 1줄에서 2줄이 됐다.  라벨 한 덩이의
            //  높이는 이름줄 + statusGap(0.34) 이므로, 단 간격이 0.18 이면 옆 사람 라벨이
            //  자기 라벨 **한가운데를** 지나간다 — 실측 스크린샷에서 집 안에 모인 세 명의
            //  이름·활동 4줄이 서로 겹쳐 판독 불가한 덩어리가 됐다.
            //  간격을 한 덩이보다 크게 벌리고, 콜로니스트가 3명이므로 단도 3개로 둔다.
            // (고정 스태거는 2026-07-31 폐기 — 아래 ResolveOverlap 이 대신한다.
            //  고정 단은 멀리 떨어져 있어도 라벨이 공중에 떠 있고, 정작 같은 자리에
            //  모였을 때는 단이 같으면 그대로 겹쳤다.  '항상 조금 어긋나고 필요할 때는
            //  안 비키는' 최악의 조합이었다.)
            _labelStagger = 0f;
            s_labels.Add(this);
            // ⚠ 직렬화 덮어쓰기 방어 (2026-07-31 실측).  statusGap/offset 은
            //  [SerializeField] 라 **Pawn 프리팹에 저장된 옛 값이 코드 기본값을
            //  덮어쓴다** — 0.34 → 0.52 로 고쳐도 빌드에서는 그대로 0.34 였고,
            //  그래서 이름줄과 활동줄이 계속 겹쳤다.  TutorialOverlay 가 같은
            //  함정을 [NonSerialized] 로 끊어 둔 선례가 있다(그 파일 주석 참조).
            //  여기서는 프리팹 재베이크 없이 **코드가 단일 정본**이 되도록 값을
            //  런타임에 되박는다.
            statusGap = StatusGapConst;
            entity = GetComponent<PawnEntity>();
            needs = GetComponent<PawnNeeds>();
            chopper = GetComponent<PawnChopper>();
            hunter = GetComponent<PawnHunter>();
            gatherer = GetComponent<PawnGatherer>();
            cook = GetComponent<PawnCook>();
            movement = GetComponent<PawnMovement>();
            builder = GetComponent<PawnBuilder>();
            hauler = GetComponent<PawnHauler>();
            doctor = GetComponent<PawnDoctor>();
            harvester = GetComponent<PawnHarvester>();
            miner = GetComponent<PawnMiner>();
            // DoResearchAction 이 없으면 아직 안 붙어 있다 — Update 에서 늦게 잡는다.
            researchWork = GetComponent<PawnResearchWork>();

            string name = entity != null ? entity.PawnName : "Pawn";

            nameGo = new GameObject("NameLabel");
            nameGo.transform.SetParent(transform, false);
            nameGo.transform.localPosition = offset + new Vector3(0f, _labelStagger, 0f);   // P2-7 스태거
            nameTm = MakeText(nameGo, name, (int)fontSize, characterSize,
                              MelonS.GameProto.Core.UITheme.AccentGold, 30);
            nameShadowTms = MakeShadow(nameGo, nameTm, 29);

            // 2번째 라인: status — TOP-2: 선택 림만 표시 (계산은 항상).
            statusGo = new GameObject("StatusLabel");
            statusGo.transform.SetParent(transform, false);
            statusGo.transform.localPosition = new Vector3(offset.x, (offset.y + _labelStagger) - statusGap, offset.z);
            // 활동 줄 크기 0.7×0.85 → 0.85×1.0 (2026-07-31).
            //  TextMesh 의 체감 크기는 대략 fontSize × characterSize 에 비례한다.
            //   이전: 64×0.7 × 0.09×0.85 → 이름의 **0.60배**
            //  이름이 기본 줌(ortho 15, 1080p)에서 ~20px 이므로 활동 줄은 **~12px** 이었고,
            //  그 크기에서 한글은 획 사이가 1px 미만이라 구조가 무너진다.  실측 스크린샷에서
            //  "서연/떠도는중"이 판독 불가한 얼룩으로 찍혔다 — 읽을 수 없는 라벨은 정보가
            //  아니라 잡음이라, 상시 표시로 바꾼 의미가 통째로 사라진다.
            //   지금: 64×0.85 × 0.09 → 이름의 0.85배 ≈ 17px.  이름보다는 작아 위계는 남고,
            //   기본 줌에서 읽힌다.
            //  ⚠ 카메라(ortho 15)를 당기는 쪽으로 풀지 않는다 — SceneSetup.Game.Core 주석대로
            //   운영자가 레퍼런스 스샷과 대조해 두 차례 조정한 값이다.  글자를 키우는 게 맞다.
            statusTm = MakeText(statusGo, "", (int)(fontSize * 0.85f), characterSize,
                                MelonS.GameProto.Core.UITheme.TextSecondary, 31);
            statusShadowTms = MakeShadow(statusGo, statusTm, 29);
        }

        private static TextMesh MakeText(GameObject host, string text, int size,
                                         float charSize, Color color, int order)
        {
            var tm = host.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = size;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = host.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = order;
            // 한글 폰트 + 머티리얼.  없으면 **WebGL 에서 라벨이 통째로 비어 렌더된다**
            //  (UITheme.ApplyKoreanFont 주석 참조 — 공개 URL 실측으로 잡은 결함).
            MelonS.GameProto.Core.UITheme.ApplyKoreanFont(tm);
            return tm;
        }

        // TOP-2 — 플레이트 박스 대신 오프셋 그림자.  본문 TextMesh 의
        //  child 라 SetActive(LOD)/텍스트 동기화가 부모 단위로 같이 묶인다.
        // v3.2 가독성 (제미나이 리뷰 #5 동의, 2026-07-24): 1방향 → 4방향 아웃라인.
        //  잔디 위 금색 텍스트가 얇게 읽히던 문제 — 4방향이면 어느 지형 위에서도
        //  글자 형태가 닫힌다.  텍스트 갱신은 배열 전체에 (SetShadowText).
        private static TextMesh[] MakeShadow(GameObject parent, TextMesh src, int order)
        {
            // 2026-07-29: 오프셋이 글자 크기에 비해 과했다.  대각 4방향 ±0.03 은 획
            //  두께의 절반을 넘어 한글 **속공간(ㅇ·ㅎ의 구멍, ㅁ의 안쪽)을 메웠고**,
            //  그래서 이름이 얼룩으로 읽혔다.  외곽선은 글자를 닫아주되 속을 채우면
            //  안 된다 — 상하좌우 축 정렬 4방향으로 바꾸고 폭을 절반으로 줄인다.
            //  (대각선은 같은 폭에서 모서리를 더 뭉친다.)
            var offs = new[] {
                new Vector3(0.016f, 0f, 0f), new Vector3(-0.016f, 0f, 0f),
                new Vector3(0f, 0.016f, 0f), new Vector3(0f, -0.016f, 0f),
            };
            var arr = new TextMesh[offs.Length];
            for (int i = 0; i < offs.Length; i++)
            {
                var go = new GameObject("Shadow" + i);
                go.transform.SetParent(parent.transform, false);
                go.transform.localPosition = offs[i];
                var tm = go.AddComponent<TextMesh>();
                tm.text = src.text;
                tm.fontSize = src.fontSize;
                tm.characterSize = src.characterSize;
                tm.anchor = src.anchor;
                tm.alignment = src.alignment;
                tm.color = new Color(0.05f, 0.04f, 0.03f, 0.9f);
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = order;
                // 그림자도 독립 TextMesh 다 — 본문과 같은 폰트를 줘야 WebGL 에서
                //  외곽선만 사라지는(본문은 있는데 테두리가 없는) 어긋남이 안 난다.
                MelonS.GameProto.Core.UITheme.ApplyKoreanFont(tm);
                arr[i] = tm;
            }
            return arr;
        }

        private static void SetShadowText(TextMesh[] arr, string text)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] != null && arr[i].text != text) arr[i].text = text;
        }

        private void Start()
        {
            // GameManager 가 spawn 후 reflection 으로 pawnName 박는다 → Awake 시점엔 default.
            if (entity != null && nameTm != null && !string.IsNullOrEmpty(entity.PawnName))
            {
                nameTm.text = entity.PawnName;
                SetShadowText(nameShadowTms, entity.PawnName);
            }
        }

        private void Update()
        {
            // status 0.25s 마다 — every-frame 은 textmesh re-bake 비싸짐
            if (Time.time - lastStatusUpdate < 0.25f) return;
            lastStatusUpdate = Time.time;

            lastComputedStatus = ComputeStatusLabel();

            // 겹치면 위로 비킨다 (ResolveOverlap 주석 참조).  상태 갱신과 같은 0.25s
            //  주기라 추가 비용이 없고, 그 정도면 사람 눈에 즉각적으로 보인다.
            float lift = ResolveOverlap();
            if (!Mathf.Approximately(lift, _labelStagger))
            {
                _labelStagger = lift;
                if (nameGo != null)
                    nameGo.transform.localPosition = offset + new Vector3(0f, lift, 0f);
                if (statusGo != null)
                    statusGo.transform.localPosition =
                        new Vector3(offset.x, (offset.y + lift) - statusGap, offset.z);
            }

            // ── TOP-2 LOD + 선택 게이트 + 야간 감광 ──
            float ortho = Camera.main != null && Camera.main.orthographic
                ? Camera.main.orthographicSize : 5.5f;
            bool showName = ortho < lodHideAll;
            // ── 활동 줄을 **전원 상시** 표시로 (2026-07-31) ──────────────────────
            //  운영자: "플레이 영상을 보고 있으면 동작 하나하나에 의미가 있어야 하는데
            //  뭐 하고 있는건지 모르겠음."
            //
            //  실제로 화면에는 이름만 떠 있었다.  세 사람이 각자 다른 곳으로 걸어가는데,
            //  나무를 나르는 중인지 연구하러 가는 중인지 밥 먹으러 가는 중인지 구분할
            //  단서가 없다 — 그러면 자율 행동은 '의미 있는 노동'이 아니라 '랜덤 배회'로
            //  읽힌다.  간접 조작이 이 게임의 핵심 주장인데, 그 근거가 화면에 없었다.
            //
            //  정보는 이미 매 0.25초 계산되고 있었다(lastComputedStatus).  막고 있던 건
            //  두 개의 게이트뿐이다:
            //    · IsSelectedPawn()  — 선택해야만 보임.  영상·첫인상에서는 아무도 선택돼
            //      있지 않으므로 사실상 항상 숨김이었다.
            //    · ortho < lodNameOnly(10) — 기본 줌은 15 라 어차피 숨김이었다.
            //  이 파일 상단 주석의 `운영자 2026-06-02: "림이 머하는지 머리위에 항상"` 이
            //  원래 요구였고, 구현이 그 뒤 선택 전용으로 좁혀져 있었다.  요구대로 되돌린다.
            //
            //  클러터 우려는 낮다: 콜로니스트 3명이고, 라벨은 이미 불투명 플레이트가 아니라
            //  1px 그림자 TextMesh 다(TOP-2 디클러터 때 교체됨).  이름이 보이는 줌이면
            //  활동도 보인다 — 두 줄이 함께 나타나고 함께 사라진다.
            bool showStatus = showName;

            if (nameGo.activeSelf != showName) nameGo.SetActive(showName);
            string statusShown = showStatus ? lastComputedStatus : "";
            // 유휴는 적지 않는다 (2026-07-31).  운영자 요구는 "동작 하나하나에 의미가
            //  읽히게" 였는데, '떠도는중'은 의미가 없는 유일한 상태다.  게다가 셋이 한
            //  방에 모이면 이 문구들이 서로 겹쳐 판독 불가한 덩어리가 된다 — 정보가 없는
            //  글자가 정보가 있는 글자를 가리는 최악의 조합이다.
            //  비워 두면 **떠 있는 라벨은 전부 실제 노동**이 되어 화면이 스스로 설명한다.
            //  (유휴 여부는 선택 시 인포 패널에서 여전히 확인된다.)
            if (statusShown == "떠도는중") statusShown = "";
            if (statusTm.text != statusShown)
            {
                statusTm.text = statusShown;
                SetShadowText(statusShadowTms, statusShown);
            }

            // 야간엔 라벨도 살짝 가라앉는다 (풀밝기 라벨이 밤 분위기를 깨던 문제).
            float dark = NightOverlay.CurrentDarkness01;
            float a = Mathf.Lerp(1f, 0.65f, dark);
            var nc = nameTm.color; nc.a = a; nameTm.color = nc;
            var sc = statusTm.color; sc.a = a; statusTm.color = sc;
        }

        // (IsSelectedPawn + _selector/_marquee 캐시는 2026-07-31 제거 — 활동 줄이 전원
        //  상시가 되면서 '선택 여부'가 표시 조건에서 빠졌다.  선택 표현은 SelectionRing /
        //  MultiSelectionRings / InspectHighlight 가 이미 담당한다.)

        // 운영자 2026-06-02: 림이 "머하는지" 머리위에 항상 표시.  우선순위(위→아래):
        //  사망 > 징집 > 정신붕괴 > 수면 > 식사 > 휴식 > (작업 9종) > 이동 > 연구 > 유휴.
        private static int s_labelTier;
        private float _labelStagger;

        // ── 겹침 회피 (2026-07-31) ────────────────────────────────────────────
        //  운영자 지적: 집 안에 셋이 모이면 이름·활동 4줄이 서로 겹쳐 판독 불가.
        //  고정 스태거(단 3개)로는 안 됐다 — 같은 단끼리는 그대로 겹치고, 떨어져
        //  있을 때도 공중에 떠 있었다.
        //  대신 **겹칠 때만** 위로 비킨다: 자기보다 '앞선' 라벨(같은 화면 자리에 있고
        //  정렬 키가 작은 것)의 개수만큼 한 칸씩 올라간다.  결정적이라 깜빡이지 않고,
        //  떨어지면 자연히 0으로 돌아온다.
        //  n = 콜로니스트 수(3~6)라 O(n²) 는 무시할 만하다.
        private static readonly System.Collections.Generic.List<PawnNameLabel> s_labels =
            new System.Collections.Generic.List<PawnNameLabel>(16);

        // 6인 기준 재조정 (2026-08-01) — 콜로니스트가 3 → 6 으로 늘면서 같은 넓이에
        //  라벨 밀도가 두 배가 됐다.  겹침 판정 폭을 넓히고 올림 간격을 키운다.
        private const float OverlapDx = 1.9f;    // 라벨 폭 ≈ 두 칸 (활동 줄이 길다: '농작물 운반')
        private const float OverlapDy = 1.10f;   // 라벨 한 덩이 높이
        // 한 칸 올림 = **라벨 한 덩이 전체 높이**여야 한다.
        //  0.46 으로 뒀더니 statusGap(0.52)보다 작아서, 올라간 라벨의 활동줄이
        //  아래 라벨의 이름줄 자리에 그대로 떨어졌다(실측) — 비켰는데 여전히 겹친다.
        //  한 덩이 = statusGap(0.52) + 글자 높이(≈0.3).
        private const float LiftStep = 0.80f;

        // 이름줄↔활동줄 간격의 **코드 정본**.  글자 높이(≈0.5 월드유닛)보다 커야
        //  두 줄이 파고들지 않는다.  [SerializeField] 값은 Awake 에서 이 값으로 덮는다.
        private const float StatusGapConst = 0.62f;

        private void OnDestroy() { s_labels.Remove(this); }

        /// <summary>내 라벨이 몇 칸 올라가야 하는가.</summary>
        private float ResolveOverlap()
        {
            Vector3 me = transform.position;
            int lift = 0;
            for (int i = 0; i < s_labels.Count; i++)
            {
                var o = s_labels[i];
                if (o == null || o == this) continue;
                Vector3 p = o.transform.position;
                if (Mathf.Abs(p.x - me.x) > OverlapDx) continue;
                if (Mathf.Abs(p.y - me.y) > OverlapDy) continue;
                // 정렬 키: x 가 작은 쪽이 아래.  동률이면 인스턴스 ID 로 끊어
                //  두 라벨이 서로 '내가 위'라고 판단해 함께 올라가는 것을 막는다.
                if (p.x < me.x || (p.x == me.x && o.GetInstanceID() < GetInstanceID()))
                    lift++;
            }
            return lift * LiftStep;
        }

        private string ComputeStatusLabel()
        {
            if (entity == null) return "";
            // 늦게 붙는 컴포넌트 — 한 번 잡히면 그대로 유지 (0.25s 주기라 비용 무시 가능).
            if (researchWork == null) researchWork = GetComponent<PawnResearchWork>();
            if (entity.IsDead) return "사망";
            if (entity.IsDrafted) return "[징집]";
            if (needs != null && needs.IsBreaking) return "정신붕괴";
            // 전투는 전용 작업 컴포넌트가 없어(이동만 한다) HasTask 로 표시할 수 없다.
            //  DefendColonyAction 이 남긴 시각을 읽는다.  수면·식사보다 위 —
            //  적이 눈앞에 있는데 '산딸기 운반' 이 뜨면 화면이 상황을 설명하지 못한다.
            if (MelonS.GameProto.AI.DefendColonyAction.IsEngaged(gameObject)) return "전투";
            if (needs != null && needs.IsSleeping) return "수면";
            // 첫사이클 T17 — 취침 이동이 '떠도는중'으로 낙하해 '밤마다 림이 떠돈다'
            //  인식을 코드가 생산하던 것 (IsSleeping 케이스 '아래' — 수면 중 역회귀 방지).
            if (needs != null && (needs.HasAutoSleepOrder || needs.HasRestOrder)) return "취침 이동";
            if (needs != null && needs.IsEating) return "식사";
            if (needs != null && needs.IsForcedResting) return "휴식";
            // 작업 9종 — HasTask(이동 포함, 의도 단계부터 표시).
            if (builder != null && builder.HasTask)
                return builder.HasDeconstructTask ? "철거" : "건축";
            if (chopper != null && chopper.HasTask) return "벌목";
            if (miner != null && miner.HasTask) return "채굴";
            if (harvester != null && harvester.HasTask) return "수확";
            if (gatherer != null && gatherer.HasTask) return "채집";
            if (hunter != null && hunter.HasTask) return "사냥";
            if (cook != null && cook.HasTask) return "요리";
            // "운반"만으로는 무엇을 옮기는지 안 읽힌다 → "목재 운반" (HaulKindKr 주석 참조).
            if (hauler != null && hauler.HasTask)
            {
                string kind = hauler.HaulKindKr;
                return string.IsNullOrEmpty(kind) ? "운반" : $"{kind} 운반";
            }
            if (doctor != null && doctor.HasTask) return "치료";
            // 연구 — 위치 추정이 아니라 **작업 배정**을 읽는다 (PawnResearchWork 주석 참조).
            //  이전엔 "정지 + 벤치 반경"이라, 벤치 옆에서 한 발짝만 움직여도 '떠도는중'으로
            //  떨어졌다.  진행도는 오르는데 라벨은 떠돈다고 하는 화면이 실제로 찍혔다.
            if (researchWork != null && researchWork.IsResearching) return "연구";
            // 여가 시간대 (2026-07-31) — 일정표가 '여가'인 동안 모닥불가로 모이는 것은
            //  배회가 아니라 **일정에 따른 행동**이다.  이걸 '떠도는중'으로 적으면(그리고
            //  그건 아래에서 숨겨지므로 아무것도 안 적으면) 화면은 "왜 다들 노나"로 읽힌다.
            //  일정 때문이라는 걸 밝히면 그 장면에 이유가 생긴다 — 플레이어는 F4 로 바꿀 수 있다.
            if (schedule == null) schedule = GetComponent<PawnSchedule>();
            if (schedule != null && schedule.GetCurrentSlot() == TimeSlot.Joy) return "여가";
            return "떠도는중";
        }

        // (IsResearchingHere + bench 캐시는 2026-07-31 제거 — 위치 기반 추정을 버리고
        //  PawnResearchWork 도장을 읽는다.  죽은 코드를 남기면 다음 사람이 두 판정 중
        //  어느 쪽이 진짜인지 다시 헷갈린다.)
    }
}
