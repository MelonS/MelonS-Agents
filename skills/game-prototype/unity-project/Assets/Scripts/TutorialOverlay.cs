using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 74 — Tutorial overlay.  Shows a sequence of short tips during
    /// the first ~90 real seconds of play.  Each tip fades in 0.5s, holds,
    /// fades out 0.5s.  Player can skip current tip with Space or Esc.
    /// Tips are Korean strings hardcoded here.
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        // 행동 기반 온보딩(2026-06-13) — 시간 대신 게임 상태로 단계 전진.
        //  일시정지 시작과 정합(시간 기반은 둘러보는 동안 만료됨) + 첫 사이클
        //  전체(저장→집→농장) 안내.  각 단계는 플레이어가 실제로 해야 넘어간다.
        public enum Gate { Unpause, Stockpile, Chop, House, Farm, Done }

        [System.Serializable]
        public struct Tip
        {
            public float startTime;   // (레거시 — 미사용, 직렬화 호환 유지)
            public float duration;    // (레거시)
            public string text;
            public Gate gate;
        }

        // 운영자 피드백 2026-05-27: tip 9개 × 7초 = 72초 동안 화면 가림.  3개로 압축.
        // 키보드 안내는 GuiControlBar 의 버튼 hint 가 cover.
        //
        // [NonSerialized] 인 이유 (2026-07-27, 실측으로 확인한 함정):
        //  이 필드가 public 이면 Unity 가 **씬에 값을 직렬화**하고, 씬에서 로드될 때
        //  아래 초기화식을 **덮어쓴다**.  즉 여기 문구/게이트를 고쳐도 빌드에는 반영되지
        //  않는다 — 실제로 이 함정에 걸려, 문구 수정과 Gate.Chop 신설이 배포본에서
        //  전혀 적용되지 않은 채 "고쳤다"고 판단할 뻔했다(수정 전/후 스크린샷을 나란히
        //  비교해서야 발견).  씬 리베이크는 Game.unity 를 통째로 다시 쓰는 위험한 작업이라,
        //  직렬화를 끊어 **코드가 단일 정본**이 되게 한다.
        [System.NonSerialized]
        public Tip[] tips = new Tip[]
        {
            new Tip { gate = Gate.Unpause,
                      // 2026-08-01 — ▶ 는 **화면에 없는 버튼**이었다.  우상단의 ▶ 는
                      //  클릭 불가한 상태 표시이고, 실제 재개 조작은 하단 우측 [1x] 버튼이다.
                      //  첫 10초에 존재하지 않는 버튼을 누르라고 하면 그 자체로 '고장난 게임'이다.
                      //  같은 사건을 '▶ / Space / 1배속' 세 이름으로 부르던 것도 통일한다.
                      text = "일시정지 상태입니다.  맵을 둘러본 뒤 \nSpace 또는 오른쪽 아래 [1x] 를 누르세요." },
            // ① 을 '저장구역 만들기' → '벌목 지정'으로 교체 (2026-07-27).
            //  시작 저장구역이 생기면서 Gate.Stockpile 이 게임 시작 즉시 충족돼, 이 단계가
            //  화면에 뜨자마자 건너뛰어졌다(평가자: "일시정지 상태인데 이미 ②번이 떠 있음").
            //  빈 단계를 남기는 대신, **생산이 멈추는 진짜 원인**을 가르치는 단계로 바꾼다:
            //  초기 목재 300 이 창고로 들어간 뒤에는 새 일감이 없어 콜로니가 정지하는데,
            //  그걸 푸는 첫 행동이 벌목 지정이다.
            new Tip { gate = Gate.Chop,
                      text = "① 일감 지정:  나무를 우클릭 → 벌목 \n지정한 만큼만 일합니다 — 여러 그루를 찍어보세요." },
            new Tip { gate = Gate.House,
                      text = "② 집:  건축 → 구조 → 목재 벽으로 방을 짓고 \n문·가구(침대)를 놓으세요." },
            // 문구 모순 제거 (2026-07-31).  시작 정착지에는 이미 밭이 깔려 있는데
            //  "농사 지을 땅을 지정하세요"가 떠 있었다 — 화면에 밭이 보이는 채로 밭을
            //  만들라고 하니 플레이어는 자기가 뭘 놓쳤는지 찾게 된다.  지금 있는 밭을
            //  인정하고 '넓히는' 행동으로 바꾼다 (게이트는 그대로 ZoneCellCount>0).
            new Tip { gate = Gate.Farm,
                      text = "③ 농장:  건축 → 구역 → 경작 \n밭을 더 넓히면 식량이 늘어납니다." },
            new Tip { gate = Gate.Done,
                      // 문구 정직화 (2026-07-27).  기존: "이제 콜로니스트가 알아서 일합니다."
                      //  → 화면에서는 아무도 일하지 않는 상태에서 이 문장이 떴다.  플레이어가
                      //  일감(벌목/채광/건설)을 지정하지 않으면 폰이 대기하는 건 장르 정상 동작이지만,
                      //  게임이 "알아서 한다"고 약속해 버리면 **게임이 자기 상태를 오인하는 것**으로
                      //  읽힌다 (가상 유저 평가에서 반복 지적: "게임이 자기 자신에 대해 거짓말").
                      //  약속 대신 다음 행동을 지시한다.
                      // 2026-07-31 — 마지막 팁이 ①과 같은 말을 반복하고 있었다
                      //  ("나무를 우클릭 = 벌목").  ① 이 '저장구역'에서 '벌목 지정'으로
                      //  바뀐 뒤(2026-07-27) 이 문구만 그대로 남은 것이다.  마지막 안내는
                      //  **다음에 무엇을 보고 움직일지**를 넘겨주는 자리다:
                      //   · 방향은 좌상단 정착 목표가 준다 (튜토리얼이 끝나도 남는 UI)
                      //   · 그리고 곧 올 압력(습격)을 예고해 준비할 이유를 만든다
                      text = "좌상단 정착 목표를 모두 채우면 정착 성공입니다.\n밤엔 침대에서 자고, 며칠 뒤 습격이 옵니다 — 무기를 준비하세요." },
        };

        [SerializeField] private Image bg;
        [SerializeField] private Text tipText;
        // #UI-restyle U8 — the banner is now a bordered panel (border Image + fill child +
        //   text).  We fade the whole subtree via a CanvasGroup instead of tweening one Image
        //   alpha, so the warm border/fill/text fade together as one panel.
        [SerializeField] private CanvasGroup group;

        private int currentTipIdx = -1;
        private float currentTipFadeTime;
        private bool currentVisible;
        private float skipUntil = -1f;

        private void Start()
        {
            bootTime = Time.realtimeSinceStartup;   // Gate.Unpause 정착 창 기준점
            if (group != null) group.alpha = 0f;
            // legacy single-image fade fallback (only if no CanvasGroup wired)
            if (group == null && bg != null) { var c = bg.color; c.a = 0f; bg.color = c; }
            if (group == null && tipText != null) { var c = tipText.color; c.a = 0f; tipText.color = c; }

            // #38 게이트 플레이키 진범(2026-06-10) — 튜토리얼 배너가 첫 ~18초 동안 상단
            //  ~230px 밴드의 월드 클릭을 통째로 먹었다.  CanvasGroup 알파 페이드는 raycast 를
            //  끄지 않아 'alpha 0 인데도 차단'(보이지 않는 차단자).  안내 배너는 어떤 입력도
            //  소비할 이유가 없으므로 서브트리 전체를 비차단으로 강제 (씬 베이크와 무관하게
            //  런타임 1회 sweep — 향후 재베이크에도 안전).
            if (group != null) { group.blocksRaycasts = false; group.interactable = false; }
            foreach (var g in GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;
        }

        private void Update()
        {
            if (Time.timeScale <= 0.01f) sawPause = true;   // 정지 시작 감지(Unpause gate 용)
            // Skip current tip
            // 행동 기반 진행: 현재 단계의 gate 가 충족되면 다음 단계로.  완료(stepIdx
            //  == tips.Length)면 영구 종료.  Space/ESC = 현재 단계 건너뛰기(수동 전진).
            if (stepIdx >= tips.Length) { FadeOut(); ApplyFade(); return; }
            // Chop 단계 기준선: 이 단계에 **처음 들어온 순간의** 지정 수를 기록해 두고,
            //  그보다 늘어야 통과로 친다 (시작 지정이 게이트를 대신 충족해 단계가
            //  건너뛰어지는 것을 막는다 — GateSatisfied 의 Gate.Chop 주석 참조).
            if (tips[stepIdx].gate == Gate.Chop && chopBaselineStep != stepIdx)
            {
                chopBaselineStep = stepIdx;
                chopBaseline = TreeChopDesignation.Instance != null
                    ? TreeChopDesignation.Instance.GetMarkedTreePositions().Count : 0;
            }
            bool manualSkip = (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) && currentVisible;
            bool gateMet = GateSatisfied(tips[stepIdx].gate);
            // Done 단계는 표시 후 6초 뒤 자동 종료.
            if (tips[stepIdx].gate == Gate.Done && currentVisible
                && Time.realtimeSinceStartup - currentTipFadeTime > 6f) gateMet = true;

            // ── 게이트 타임아웃 (2026-07-31) ──────────────────────────────────
            //  행동 기반 진행에는 "플레이어가 그 행동을 영영 안 하면?"이라는 출구가 없었다.
            //  실측: 데모 4분 내내 ③ 농장 배너가 화면 하단 중앙에 떠 있었다.  Gate.Farm 은
            //  GrowZoneDesignation.ZoneCellCount>0 을 요구하는데 시작 밭은 CropEntity 로
            //  직접 깔려서 이 카운트에 안 잡힌다 — 즉 **아무리 기다려도 안 넘어가는 단계**가
            //  월드를 계속 가리고 있었다.  심사자 눈에는 고장난 튜토리얼로 읽힌다.
            //
            //  안내는 권유지 관문이 아니다.  일정 시간이 지나면 다음으로 넘긴다.
            //  진행 시간은 **게임이 실제로 돌 때만** 센다 — 플레이어가 일시정지하고 맵을
            //  둘러보는 동안 팁이 소진되면 안 되기 때문이다(게임 시작이 일시정지 상태다).
            if (currentVisible && Time.timeScale > 0.01f)
                tipLiveTime += Time.unscaledDeltaTime;
            bool timedOut = tipLiveTime > GateTimeoutSec;

            if (manualSkip || gateMet || timedOut)
            {
                stepIdx++;
                if (stepIdx >= tips.Length) { FadeOut(); ApplyFade(); return; }
            }
            if (stepIdx != currentTipIdx)
            {
                currentTipIdx = stepIdx;
                if (tipText != null) tipText.text = tips[currentTipIdx].text;
                currentTipFadeTime = Time.realtimeSinceStartup;
                tipLiveTime = 0f;   // 새 단계 = 타임아웃 시계 리셋
                FadeIn();
            }
            // Smooth fade
            if (group != null)
            {
                // whole bordered-panel fade (border + fill + text) — warm panel reads as one.
                // UI 가림 다이어트 (2026-07-24) → **판독성 우선으로 재조정 (2026-07-27)**.
                //  기존: 25초 후 알파 0.35.  의도는 "맵을 계속 가리지 않기"였으나, 초보자는
                //  한 단계에 25초를 쉽게 넘기므로 결과적으로 **안내가 대부분의 시간을 읽을 수
                //  없는 상태로** 떠 있었다.  가상 유저 평가에서 반복 지적:
                //   - 11세 페르소나: "글씨가 반투명이라 뒤에 나무가 비쳐요. 읽다가 포기했어요"
                //   - QA: 10장 중 4장이 판독 불가 상태로 포착 (게임내 약 40분 구간)
                //   - UX 실측: 불투명 시 대비 6.2:1 → 페이드 시 2.0:1
                //  튜토리얼은 이 게임의 **유일한 안내 수단**이라 가림보다 판독성이 우선이다.
                //  딤 자체는 유지하되(장시간 방치 시 시야 확보) 읽을 수 있는 선까지만.
                bool stale = Time.realtimeSinceStartup - currentTipFadeTime > 40f;
                float target = currentVisible ? (stale ? 0.80f : 0.96f) : 0f;
                group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 2f);
            }
            else
            {
                // legacy fallback (single Image + Text alpha) for any unmigrated wiring.
                if (bg != null)
                {
                    float target = currentVisible ? 0.78f : 0f;
                    var c = bg.color;
                    c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 2f);
                    bg.color = c;
                }
                if (tipText != null)
                {
                    float target = currentVisible ? 1f : 0f;
                    var c = tipText.color;
                    c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 2f);
                    tipText.color = c;
                }
            }
        }

        private void FadeIn()  { currentVisible = true; }
        private void FadeOut() { currentVisible = false; }

        private int stepIdx = 0;   // 행동 기반 현재 단계

        // 현재 단계가 **게임이 도는 동안** 화면에 떠 있던 누적 시간(초).  게이트 타임아웃용.
        private float tipLiveTime;
        // 45초 — 한 단계를 이해하고 시도하기엔 넉넉하고, 안 할 사람을 붙잡아 두기엔 충분히 짧다.
        private const float GateTimeoutSec = 45f;

        private bool GateSatisfied(Gate g)
        {
            switch (g)
            {
                case Gate.Unpause:
                    // 운영자 2026-07-24 "일시정지 아닌데 안내": sawPause 요구를 없앴다 —
                    //  정지를 안 거친 세션(하네스 등)에서 sawPause 가 영원히 false 라
                    //  1배속 구동 중에도 '일시정지' 팁이 고정되던 버그 때문이었다.
                    //
                    // 2026-07-31 — 그 수정이 반대쪽 구멍을 냈다.  `GameManager.PauseAtStart`
                    //  는 **1프레임 기다린 뒤** timeScale 을 0 으로 만든다(TimeController
                    //  부트 대기).  그런데 이 Update 는 그 전 프레임에 이미 돌면서 기본값
                    //  timeScale=1 을 보고 "시작했다"고 판정한다 → 이 단계가 **매번 100%
                    //  건너뛰어졌다**.
                    //  공개 URL 실측: 게임이 멈춰 있는 화면에서 하단 배너가 "① 나무를
                    //  우클릭 → 벌목"을 지시하고 있었다.  지시대로 찍어도 아무도 움직이지
                    //  않으니, 첫 30초에 "고장난 게임"으로 읽힌다.
                    //
                    //  양쪽을 다 만족시키려면 **정지가 걸릴 기회를 준 뒤에** 판정해야 한다:
                    //   · 부팅 직후 짧은 창 동안은 아직 판단하지 않는다(정지가 오는 중일 수 있다)
                    //   · 그 창 안에 정지를 봤다면(sawPause) 진짜 재개될 때까지 기다린다
                    //   · 창이 지나도록 정지가 없었다면 = 정지를 안 쓰는 세션 → 용무 종료
                    if (!sawPause && Time.realtimeSinceStartup - bootTime < BootSettleSec)
                        return false;
                    return Time.timeScale > 0.01f;
                case Gate.Stockpile:
                    return Object.FindObjectsByType<StockpileZoneEntity>(FindObjectsSortMode.None).Length > 0;
                case Gate.Chop:
                    // **이 단계에 들어온 시점 대비 증가**했을 때만 통과한다.
                    //  단순히 "지정이 1개 이상"으로 하면, 시작 벌목 지정(GameManager 가 첫 화면
                    //  생동감을 위해 6그루를 미리 찍는다)이 게이트를 즉시 충족시켜 이 단계가
                    //  또 건너뛰어진다 — 저장구역 때와 정확히 같은 함정이다.
                    //  기준선과 비교하면 "플레이어가 직접 하나 더 찍었는가"를 본다.
                    //  ReproHarness 의 chopDesignations 프로브와 같은 소스를 쓴다(게이트와
                    //  테스트가 다른 걸 세면 "테스트는 통과하는데 화면은 안 넘어간다"가 생긴다).
                    if (TreeChopDesignation.Instance == null) return false;
                    return TreeChopDesignation.Instance.GetMarkedTreePositions().Count > chopBaseline;
                case Gate.House:
                    // **이 단계에 들어온 시점 대비 늘었을 때만** 통과한다.
                    //  2026-07-31 실측(심사자 흐름 재현): 기존 조건은 "벽이 하나라도
                    //  있으면"이었는데 **시작 정착지의 집에 이미 벽이 있다** → 이 단계가
                    //  화면에 뜨자마자 충족돼, 안내가 ①에서 곧장 ③으로 넘어갔다.
                    //  즉 튜토리얼이 **건축을 한 번도 가르치지 않았다**.
                    //  저장구역(2026-07-27)·벌목(같은 날)에 이어 세 번째 같은 함정이다 —
                    //  시작 정착지가 생기면서 "존재하면 통과" 류 게이트가 전부 무력화됐는데
                    //  하나씩 발견되고 있다.  기준선 비교로 "플레이어가 새로 지었는가"를 본다.
                    //  청사진은 완공되면 사라지므로 **벽 수 + 청사진 수**의 합을 센다
                    //  (짓는 중 → 완공 사이에 합계가 줄지 않게).
                    if (houseBaselineStep != stepIdx)
                    {
                        houseBaselineStep = stepIdx;
                        houseBaseline = HouseProgressCount();
                    }
                    return HouseProgressCount() > houseBaseline;
                case Gate.Farm:
                    return GrowZoneDesignation.Instance != null
                        && GrowZoneDesignation.Instance.ZoneCellCount > 0;
                case Gate.Done:
                    return false;   // 시간 경과로만(위에서 처리)
            }
            return false;
        }

        private bool sawPause;
        // 부팅 정착 창 — Gate.Unpause 주석 참조.  PauseAtStart 는 1프레임 뒤에 정지를
        //  걸지만, 저사양/WebGL 첫 프레임이 길어질 수 있어 넉넉히 잡는다.  이 창 동안
        //  안내는 '일시정지' 문구를 유지하므로 사용자 입장에서 손해가 없다.
        private const float BootSettleSec = 1.5f;
        private float bootTime;
        // Gate.Chop 기준선 (위 Update 의 진입 감지에서 설정).  -1 = 아직 진입 안 함.
        private int chopBaseline;
        private int chopBaselineStep = -1;
        // Gate.House 기준선 — 같은 이유(시작 정착지가 조건을 미리 충족).
        private int houseBaseline;
        private int houseBaselineStep = -1;

        /// <summary>'집짓기 진척' 지표 = 완공된 벽 + 진행 중 청사진.
        ///  청사진은 완공되면 사라지고 벽이 늘어나므로, 둘을 합쳐야 짓는 도중에
        ///  합계가 줄어 게이트가 도로 닫히는 일이 없다.</summary>
        private static int HouseProgressCount()
            => Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None).Length
             + Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None).Length;

        private void ApplyFade()
        {
            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, currentVisible ? 0.96f : 0f, Time.unscaledDeltaTime * 2f);
        }

        public void SetRefs(Image bgImg, Text txt)
        {
            bg = bgImg;
            tipText = txt;
        }

        // #UI-restyle U8 overload — bordered-panel version (border Image + fill + CanvasGroup).
        public void SetRefs(Image bgImg, Text txt, CanvasGroup cg)
        {
            bg = bgImg;
            tipText = txt;
            group = cg;
        }
    }
}
