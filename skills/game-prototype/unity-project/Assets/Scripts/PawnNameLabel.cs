using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Day 30: floating name label above the pawn.
    /// 운영자 피드백 2026-05-27: 2번째 라인에 status 표시
    ///   (벌목중/이동중/사냥중/요리중/휴식/징집됨). 게임이 살아있어 보임.
    /// </summary>
    public class PawnNameLabel : MonoBehaviour
    {
        // #199 A2 ortho + 1x1 pawn — 라벨을 HP 바(top 0.68) 바로 위로.
        //  #ui백로그 3.1(2026-06-10): name(0.98)/status(0.83) 줄간격 0.15 는 글리프 반높이
        //  합(~0.25)보다 좁아 두 줄이 ~0.10wu 겹쳤다 (스크린샷: '떠도는중' 상단이 이름에
        //  가려 판독 곤란).  간격 0.26 으로 벌리고 name 을 1.06 으로 동반 상향.
        //  순서(위→아래): name(1.06) > status(0.80) > HP 바(0.68) > mood 바(0.55) > 머리(0.5).
        [SerializeField] private Vector3 offset = new Vector3(0, 1.06f, 0);
        [SerializeField] private float statusGap = 0.26f;   // name↔status 줄간격 (#3.1)
        [SerializeField] private float fontSize = 64;
        [SerializeField] private float characterSize = 0.05f;

        private TextMesh nameTm;
        private TextMesh statusTm;

        /// 현재 머리위 행동 라벨(떠도는중/벌목/수면 등).  인스펙트 패널이 같은 정보를 읽어 표시.
        public string CurrentActivity => statusTm != null ? statusTm.text : "";
        private SpriteRenderer plate;          // #UI-restyle U3 — dark plate behind name+status
        private string lastPlateName = null;
        private string lastPlateStatus = null;
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

        // "연구" 판정용 bench 캐시(모든 라벨 공유, 2s 갱신).  연구는 per-pawn 태스크가
        //  없고 bench 근접+activeTech 로만 감지 → idle 분기에서만 조회(비용 게이트).
        private static ResearchBench[] _benchCache;
        private static float _nextBenchScan = -10f;

        private float lastStatusUpdate;
        private int plateRefitFrames = 4;   // re-fit plate a few frames so TextMesh bounds bake

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

            // #UI-restyle U3 — semi-transparent dark plate behind name+status so the
            //   label is readable over ANY terrain (was naked text on grass).  Crisp
            //   point-filtered sprite; warm-dark UITheme tone; sits BEHIND the text.
            var plateGo = new GameObject("NamePlate");
            plateGo.transform.SetParent(transform, false);
            // centered between name (offset.y) and status (offset.y - statusGap)
            plateGo.transform.localPosition = new Vector3(offset.x, offset.y - statusGap * 0.5f, offset.z);
            plate = plateGo.AddComponent<SpriteRenderer>();
            plate.sprite = PlateSprite;
            // UITheme.PanelBg tone; opaque enough for contrast over bright grass.
            plate.color = new Color(0.118f, 0.086f, 0.063f, 0.82f);  // warm dark brown a0.82
            plate.sortingOrder = 29;  // just under the text (text = 30)

            var nameGo = new GameObject("NameLabel");
            nameGo.transform.SetParent(transform, false);
            nameGo.transform.localPosition = offset;
            nameTm = nameGo.AddComponent<TextMesh>();
            nameTm.text = name;
            nameTm.fontSize = (int)fontSize;
            nameTm.characterSize = characterSize;
            nameTm.anchor = TextAnchor.MiddleCenter;
            nameTm.alignment = TextAlignment.Center;
            // #UI-restyle U3 — gold name (matches AccentGold) on the dark plate.
            nameTm.color = MelonS.GameProto.Core.UITheme.AccentGold;
            var nameMr = nameGo.GetComponent<MeshRenderer>();
            if (nameMr != null) nameMr.sortingOrder = 30;

            // 2번째 라인: status (작은 글씨, 살짝 아래)
            var statusGo = new GameObject("StatusLabel");
            statusGo.transform.SetParent(transform, false);
            // #3.1 — 줄간격은 statusGap (글리프 겹침 방지)
            statusGo.transform.localPosition = new Vector3(offset.x, offset.y - statusGap, offset.z);
            statusTm = statusGo.AddComponent<TextMesh>();
            statusTm.text = "";
            statusTm.fontSize = (int)(fontSize * 0.7f);
            statusTm.characterSize = characterSize * 0.85f;
            statusTm.anchor = TextAnchor.MiddleCenter;
            statusTm.alignment = TextAlignment.Center;
            // #UI-restyle U3 — muted cream status (matches TextSecondary).
            statusTm.color = MelonS.GameProto.Core.UITheme.TextSecondary;
            var statusMr = statusGo.GetComponent<MeshRenderer>();
            // #3.1 — name(30)보다 위(31): 잔존 겹침 시에도 '활동'이 항상 읽히게 (행동
            //  라벨이 이름보다 정보 가치가 높음 — 이름은 색/플레이트로도 식별됨).
            if (statusMr != null) statusMr.sortingOrder = 31;

            ResizePlate();  // initial sizing for the name
        }

        // crisp point-filtered 2x2 white quad shared by all plates
        private static Sprite _plateSprite;
        private static Sprite PlateSprite
        {
            get
            {
                if (_plateSprite == null)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                    tex.filterMode = FilterMode.Point;   // crisp, matches pixel art
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.Apply();
                    _plateSprite = Sprite.Create(tex, new Rect(0,0,2,2), new Vector2(0.5f,0.5f), 2f);
                    _plateSprite.name = "NamePlateWhite";
                }
                return _plateSprite;
            }
        }

        // Size the plate to enclose the longer of name/status.  TextMesh exposes its
        // baked extents via the MeshRenderer.bounds (in world units); reading it gives
        // an exact fit (advance-width guessing for CJK fonts is unreliable).  Bounds
        // bake one frame after a text change, so ResizePlate is also re-called next
        // frame from Update; we union both lines + a small padding margin.
        private void ResizePlate()
        {
            if (plate == null) return;
            string nm = nameTm != null ? nameTm.text : "";
            string st = statusTm != null ? statusTm.text : "";
            bool hasAny = !string.IsNullOrEmpty(nm) || !string.IsNullOrEmpty(st);
            plate.enabled = hasAny;
            if (!hasAny) return;

            float wName = TextWorldWidth(nameTm);
            float wStat = TextWorldWidth(statusTm);
            float w = Mathf.Max(wName, wStat) + characterSize * 1.6f;   // + horizontal padding
            // #3.1 — 높이도 폭과 같은 bounds 실측: 매직 공식(3.4*cs+0.05)은 이름 위쪽
            //  ~0.12wu 를 플레이트 밖에 노출시켰다.  두 줄 union + 패딩으로 정확히 덮는다.
            float minY = float.MaxValue, maxY = float.MinValue;
            var nMr = nameTm != null ? nameTm.GetComponent<MeshRenderer>() : null;
            var sMr = statusTm != null ? statusTm.GetComponent<MeshRenderer>() : null;
            if (nMr != null && !string.IsNullOrEmpty(nm)) { minY = Mathf.Min(minY, nMr.bounds.min.y); maxY = Mathf.Max(maxY, nMr.bounds.max.y); }
            if (sMr != null && !string.IsNullOrEmpty(st)) { minY = Mathf.Min(minY, sMr.bounds.min.y); maxY = Mathf.Max(maxY, sMr.bounds.max.y); }
            if (maxY > minY)
            {
                float pad = characterSize * 0.6f;
                var pt = plate.transform;
                pt.position = new Vector3(pt.position.x, (maxY + minY) * 0.5f, pt.position.z);
                pt.localScale = new Vector3(Mathf.Max(w, 0.30f), (maxY - minY) + pad, 1f);
            }
            else
            {
                plate.transform.localScale = new Vector3(Mathf.Max(w, 0.30f),
                                                         characterSize * 3.4f + 0.05f, 1f);
            }
        }

        // Exact rendered width of a TextMesh from its MeshRenderer bounds (world units).
        // Falls back to a CJK-aware char estimate before the first bake.
        private float TextWorldWidth(TextMesh tm)
        {
            if (tm == null || string.IsNullOrEmpty(tm.text)) return 0f;
            var mr = tm.GetComponent<MeshRenderer>();
            if (mr != null && mr.bounds.size.x > 0.001f) return mr.bounds.size.x;
            // pre-bake fallback (rarely hit): CJK ~1.0, latin ~0.58, * empirical scale
            float u = 0f;
            foreach (char c in tm.text) u += (c > 0x2E80) ? 1.0f : 0.58f;
            return u * tm.characterSize * (tm.fontSize / 64f) * 4.2f;
        }

        private void Start()
        {
            // GameManager 가 spawn 후 reflection 으로 pawnName 박는다 → Awake 시점엔 default.
            //  Start 에서 한 번 더 가져와서 라벨 텍스트 갱신.
            if (entity != null && nameTm != null && !string.IsNullOrEmpty(entity.PawnName))
            {
                nameTm.text = entity.PawnName;
                ResizePlate();
            }
        }

        private void Update()
        {
            // TextMesh bounds bake a frame or two after text is set — re-fit the plate
            //  for the first few frames so it snaps to the real glyph extents.
            if (plateRefitFrames > 0)
            {
                plateRefitFrames--;
                ResizePlate();
            }
            // status 0.25s 마다 — every-frame 은 textmesh re-bake 비싸짐
            if (Time.time - lastStatusUpdate < 0.25f) return;
            lastStatusUpdate = Time.time;
            statusTm.text = ComputeStatusLabel();
            // #UI-restyle U3 — re-fit the plate only when the displayed text changed.
            if (nameTm.text != lastPlateName || statusTm.text != lastPlateStatus)
            {
                lastPlateName = nameTm.text;
                lastPlateStatus = statusTm.text;
                ResizePlate();
            }
        }

        // 운영자 2026-06-02: 림이 "머하는지" 머리위에 항상 표시.  우선순위(위→아래):
        //  사망 > 징집 > 정신붕괴 > 수면 > 식사 > 휴식 > (작업 9종) > 이동 > 연구 > 유휴.
        //  needs/생존 상태가 작업보다 우선, 작업이 단순 이동보다 우선(의도 표시).
        //  마지막 유휴 fallback 으로 절대 빈칸이 되지 않게 한다("항상").
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
            //  (이동 중엔 연구가 아니므로 정지 조건으로 게이트 → bench 옆 통과 오라벨 방지.)
            bool moving = movement != null && movement.IsMoving;
            if (!moving && IsResearchingHere()) return "연구";
            // 작업이 없으면 림은 idleWanderRadius 안을 어슬렁거린다(PawnUtilityAI WanderAction).
            //  걷든 잠깐 멈추든 결국 무목적 배회 → 운영자 2026-06-02 요청대로 "떠도는중"으로 통일
            //  (이전 "이동"+"유휴" 분리는 wander-걸음/멈춤 사이 깜빡임 + "유휴"=아무것도 안 함 오해).
            return "떠도는중";
        }

        // 정지한 림이 활성 연구 bench 반경 안이면 "연구".  ResearchManager.activeTech
        //  가 있어야(진행 중) 빈 bench 옆 idle 을 오라벨하지 않는다.  bench 스캔은 이
        //  분기(작업/이동 없는 idle)에서만 → 2s 캐시로 비용 게이트.
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
