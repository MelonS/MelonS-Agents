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
        [SerializeField] private Vector3 offset = new Vector3(0, 1.06f, 0);
        [SerializeField] private float statusGap = 0.26f;   // name↔status 줄간격 (#3.1)
        [SerializeField] private float fontSize = 64;
        [SerializeField] private float characterSize = 0.05f;
        // TOP-2 LOD 경계 (ortho size)
        [SerializeField] private float lodNameOnly = 7f;    // 이상: 활동 줄 숨김
        [SerializeField] private float lodHideAll = 11f;    // 이상: 라벨 전체 숨김

        private TextMesh nameTm;
        private TextMesh statusTm;
        private TextMesh nameShadowTm;
        private TextMesh statusShadowTm;
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

        // "연구" 판정용 bench 캐시(모든 라벨 공유, 2s 갱신).
        private static ResearchBench[] _benchCache;
        private static float _nextBenchScan = -10f;

        private float lastStatusUpdate;
        private static ClickSelector _selector;          // 선택 림 판정 (전 라벨 공유)
        private static MarqueeSelector _marquee;

        private void Awake()
        {
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

            string name = entity != null ? entity.PawnName : "Pawn";

            nameGo = new GameObject("NameLabel");
            nameGo.transform.SetParent(transform, false);
            nameGo.transform.localPosition = offset;
            nameTm = MakeText(nameGo, name, (int)fontSize, characterSize,
                              MelonS.GameProto.Core.UITheme.AccentGold, 30);
            nameShadowTm = MakeShadow(nameGo, nameTm, 29);

            // 2번째 라인: status — TOP-2: 선택 림만 표시 (계산은 항상).
            statusGo = new GameObject("StatusLabel");
            statusGo.transform.SetParent(transform, false);
            statusGo.transform.localPosition = new Vector3(offset.x, offset.y - statusGap, offset.z);
            statusTm = MakeText(statusGo, "", (int)(fontSize * 0.7f), characterSize * 0.85f,
                                MelonS.GameProto.Core.UITheme.TextSecondary, 31);
            statusShadowTm = MakeShadow(statusGo, statusTm, 29);
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
            return tm;
        }

        // TOP-2 — 플레이트 박스 대신 1px(0.03wu) 오프셋 그림자.  본문 TextMesh 의
        //  child 라 SetActive(LOD)/텍스트 동기화가 부모 단위로 같이 묶인다.
        private static TextMesh MakeShadow(GameObject parent, TextMesh src, int order)
        {
            var go = new GameObject("Shadow");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(0.03f, -0.03f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = src.text;
            tm.fontSize = src.fontSize;
            tm.characterSize = src.characterSize;
            tm.anchor = src.anchor;
            tm.alignment = src.alignment;
            tm.color = new Color(0.05f, 0.04f, 0.03f, 0.9f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = order;
            return tm;
        }

        private void Start()
        {
            // GameManager 가 spawn 후 reflection 으로 pawnName 박는다 → Awake 시점엔 default.
            if (entity != null && nameTm != null && !string.IsNullOrEmpty(entity.PawnName))
            {
                nameTm.text = entity.PawnName;
                nameShadowTm.text = entity.PawnName;
            }
        }

        private void Update()
        {
            // status 0.25s 마다 — every-frame 은 textmesh re-bake 비싸짐
            if (Time.time - lastStatusUpdate < 0.25f) return;
            lastStatusUpdate = Time.time;

            lastComputedStatus = ComputeStatusLabel();

            // ── TOP-2 LOD + 선택 게이트 + 야간 감광 ──
            float ortho = Camera.main != null && Camera.main.orthographic
                ? Camera.main.orthographicSize : 5.5f;
            bool showName = ortho < lodHideAll;
            bool showStatus = showName && ortho < lodNameOnly && IsSelectedPawn();

            if (nameGo.activeSelf != showName) nameGo.SetActive(showName);
            string statusShown = showStatus ? lastComputedStatus : "";
            if (statusTm.text != statusShown)
            {
                statusTm.text = statusShown;
                statusShadowTm.text = statusShown;
            }

            // 야간엔 라벨도 살짝 가라앉는다 (풀밝기 라벨이 밤 분위기를 깨던 문제).
            float dark = NightOverlay.CurrentDarkness01;
            float a = Mathf.Lerp(1f, 0.65f, dark);
            var nc = nameTm.color; nc.a = a; nameTm.color = nc;
            var sc = statusTm.color; sc.a = a; statusTm.color = sc;
        }

        private bool IsSelectedPawn()
        {
            if (entity == null) return false;
            if (_selector == null) _selector = Object.FindFirstObjectByType<ClickSelector>();
            if (_selector != null && _selector.CurrentSelection == entity) return true;
            if (_marquee == null) _marquee = Object.FindFirstObjectByType<MarqueeSelector>();
            if (_marquee != null)
            {
                var multi = _marquee.CurrentMultiSelection;
                for (int i = 0; i < multi.Count; i++)
                    if (multi[i] == entity) return true;
            }
            return false;
        }

        // 운영자 2026-06-02: 림이 "머하는지" 머리위에 항상 표시.  우선순위(위→아래):
        //  사망 > 징집 > 정신붕괴 > 수면 > 식사 > 휴식 > (작업 9종) > 이동 > 연구 > 유휴.
        private string ComputeStatusLabel()
        {
            if (entity == null) return "";
            if (entity.IsDead) return "사망";
            if (entity.IsDrafted) return "[징집]";
            if (needs != null && needs.IsBreaking) return "정신붕괴";
            if (needs != null && needs.IsSleeping) return "수면";
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
            if (hauler != null && hauler.HasTask) return "운반";
            if (doctor != null && doctor.HasTask) return "치료";
            // 연구: per-pawn 태스크가 없으므로 정지 상태에서 활성 연구 + bench 근접으로 감지.
            bool moving = movement != null && movement.IsMoving;
            if (!moving && IsResearchingHere()) return "연구";
            return "떠도는중";
        }

        // 정지한 림이 활성 연구 bench 반경 안이면 "연구".
        private bool IsResearchingHere()
        {
            var rm = ResearchManager.Instance;
            if (rm == null || rm.activeTech == null || rm.activeTech.completed) return false;
            if (Time.time >= _nextBenchScan || _benchCache == null)
            {
                _benchCache = Object.FindObjectsByType<ResearchBench>(FindObjectsSortMode.None);
                _nextBenchScan = Time.time + 2f;
            }
            if (_benchCache == null) return false;
            Vector2 me = transform.position;
            foreach (var b in _benchCache)
            {
                if (b == null) continue;
                if (Vector2.Distance(me, b.transform.position) <= b.Radius) return true;
            }
            return false;
        }
    }
}
