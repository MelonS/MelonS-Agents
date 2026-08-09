using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>제출용 소개 영상 연출 — **마을의 하루**.
    ///
    /// 계기 (2026-08-08 운영자): 기존 데모 영상에 대해 *"머하는 게임인지도 모르겠고
    /// 쓰레기 영상 같음"*, 그리고 방향에 대해 *"게임을 소개해야지"*, *"사람의 관점에서
    /// 생각하라고"*.
    ///
    /// ── 기존 영상이 실패한 이유 (실측) ────────────────────────────────────
    ///  · 배율 ortho ≈ 14 — 주민 머리 위 **활동 라벨이 읽히지 않는다.**  게임이 화면에
    ///    이미 그리고 있는 정보("누가 지금 무엇을 하는가")가 해상도 아래로 사라졌다.
    ///    배율 비교 실측: 14=점 / 10=겨우 / **7=또렷** / 5=크지만 화면이 좁다.
    ///  · UI 가 화면의 15% (하단 버튼 5.1% + 튜토리얼 2.1% + 초상화 1.4% …).
    ///    튜토리얼 문구는 **플레이어용 안내**지 시청자용이 아니다.
    ///  · 55초 동안 사실상 1샷.  0초와 5초 프레임이 거의 같다.
    ///  · 위기가 없다 — 약탈자·늑대·폭풍이 게임에 다 있는데 영상엔 하나도 없었다.
    ///  · 결과: 공사 현장을 멀리서 찍은 CCTV.  얼굴도 하루도 없다.
    ///
    /// ── 그래서 무엇을 찍는가 ──────────────────────────────────────────────
    /// **마을의 하루**를 시간 순으로 따라간다.  새벽에 깨어나 일하고, 밥을 먹고,
    /// 집을 올리고, 밤에 불을 켜고 자고, 손님(약탈자)을 맞고, 다시 아침을 맞는다.
    /// 콜로니 심을 좋아하는 사람이 하는 말은 늘 "내 콜로니에 이런 일이 있었는데"다 —
    /// 지표가 아니라 **사건과 사람**이다.
    ///
    /// 조선 산골이라는 배경이 이 게임의 가장 강한 무기인데 멀리서 보면 그냥 점이다.
    /// 갓·기와·가마솥·장독대·이불이 보이는 거리까지 붙는다.
    ///
    /// ── 요강 준수 ─────────────────────────────────────────────────────────
    /// "실제 게임 플레이 장면을 중심으로 … AI 를 이용한 조작·합성이나 타인 영상의
    ///  도용은 불가.  실제 플레이 화면 그대로."
    /// 이 연출기는 **게임을 실제로 플레이시키고 카메라만 옮긴다.**  화면에 없는 것을
    /// 만들어 넣지 않는다.  자막은 텍스트 오버레이지 합성 영상이 아니다.
    ///
    /// `-trailer` 인자가 있을 때만 동작한다 — 평상시 플레이·게이트에는 영향 없음.
    /// </summary>
    public class TrailerDirector : MonoBehaviour
    {
        public static bool Enabled { get; private set; }

        /// <summary>`-trailerframes <디렉터리>` — **빌드가 직접 프레임을 덤프한다.**
        ///
        /// 왜 Unity Recorder 를 안 쓰는가 (실측 3회):
        ///  · 녹화 경로는 Unity *에디터* 배치모드라 게임이 **빌드와 다른 상태**로 돈다.
        ///    같은 연출인데 침대 3개(빌드 6개)·목재 270(빌드 1,600)·목표 450(빌드 550).
        ///  · uGUI 텍스트가 프레임에 안 들어갔다 — 자원·목표·토스트가 전부 **빈 박스**.
        ///  · 화면이 흐릿하다(렌더 해상도 불일치로 보이는 업스케일).
        ///  제출물의 실체는 빌드다.  그러니 빌드를 그대로 찍는다.
        ///
        /// `Time.captureFramerate` 를 쓰면 Unity 가 **가상 시간**으로 돈다 — 저장이
        ///  아무리 느려도 게임 시간은 프레임당 정확히 1/30 초씩 흐르므로, 연출 타이밍이
        ///  그대로 유지되고 결과 영상에 끊김이 없다.</summary>
        private static string frameDir;
        private const int CaptureFps = 30;

        // 배율 — 실측으로 고른 값(위 주석).  7 이 '라벨이 읽히면서 여러 명이 한 화면'.
        private const float Near = 6.0f;    // 얼굴·소품이 보이는 거리
        private const float Work = 7.5f;    // 여러 명의 활동 라벨이 동시에 읽히는 거리
        private const float Wide = 9.2f;    // 마을 전경 (10.5 는 화면 아래 1/3 이 빈 잔디였다)

        private Camera cam;
        private float camZ = -10f;
        private Text caption;
        private CanvasGroup capGroup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // 두 경로로 켠다.
            //  · `-trailer` — 빌드된 실행 파일 (프레임 확인용)
            //  · MELONS_TRAILER=1 — **녹화 경로**.  record-gameplay.py 는 Unity
            //    *에디터*를 배치모드로 띄우고 `-executeMethod` 로 플레이 모드에
            //    들어가기 때문에, 게임에 CLI 인자를 넘길 방법이 없다.  환경변수는
            //    자식 프로세스로 그대로 상속되므로 이쪽이 유일한 통로다.
            bool on = System.Environment.GetEnvironmentVariable("MELONS_TRAILER") == "1";
            var argv = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length; i++)
            {
                if (argv[i] == "-trailer") on = true;
                if (argv[i] == "-trailerframes" && i + 1 < argv.Length)
                { frameDir = argv[i + 1]; on = true; }
            }
            if (!on) return;
            Enabled = true;
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindFirstObjectByType<TrailerDirector>() != null) return;
                new GameObject("~TrailerDirector").AddComponent<TrailerDirector>();
                Debug.Log("[Trailer] 연출 시작 — 마을의 하루");
            });
        }

        private void Start() => StartCoroutine(Run());

        private float clockWatch = -1f;
        private int clockRewinds;

        /// <summary>게임 시계가 **뒤로 가면 되돌린다.**
        ///
        /// 녹화 경로(Unity 에디터 배치모드)에서만 `GameClock.GameSeconds` 가 컷 도중
        ///  시작값(06:00)으로 리셋되는 것을 관측했다 — 빌드 실행 12회에서는 한 번도
        ///  없었다.  로그: `오전 2일 9:10` 직후 `낮 1일 8:01`.  화면 위 "정착 N일째"가
        ///  영상 중간에 되감기는 셈이라 그대로 제출할 수 없다.
        ///
        /// 원인은 연출 밖(에디터 플레이 모드의 재초기화 계열)으로 보이지만, 소개 영상이
        ///  거기에 걸려 있으므로 **증상을 먼저 막는다.**  게임 규칙은 건드리지 않는다 —
        ///  시계를 앞으로 밀지 않고, 뒤로 간 것을 되돌려 놓기만 한다.
        ///  원인 추적은 `docs/known-limitations.md` 에 남긴다.</summary>
        private void LateUpdate()
        {
            var c = GameClock.Instance;
            if (c == null) return;
            if (clockWatch >= 0f && c.GameSeconds < clockWatch - 1f)
            {
                clockRewinds++;
                Debug.LogWarning($"[Trailer] 시계 되감김 감지 #{clockRewinds}: " +
                                 $"{clockWatch:F0} → {c.GameSeconds:F0} — 복원");
                c.SetGameSeconds(clockWatch);
            }
            clockWatch = c.GameSeconds;
        }

        // ── 연출 ────────────────────────────────────────────────────────────
        /// <summary>촬영 전 시뮬레이션 워밍업 — **실시간** 초.
        ///  이 구간은 녹화 파일 앞부분에 들어가고 `trim-trailer.py` 가 잘라낸다.</summary>
        private const float WarmupGameSeconds = 82_800f;   // 게임내 23시간 — 하루를 거의 한 바퀴
        /// <summary>폭주 방지용 상한 — **도달하면 그냥 진행한다.**
        ///
        /// 75초로 잡았다가 녹화본이 망가졌다: 빌드는 48초 만에 목표(게임내 23시간)에
        ///  닿는데, 녹화 경로(에디터 배치모드 + Recorder)는 프레임률이 절반이라 75초를
        ///  다 쓰고도 18시간밖에 못 갔다.  `Time.deltaTime` 기반이라 프레임이 적으면
        ///  시뮬레이션도 그만큼 덜 도는 것 — 결과는 **침대 3개·목재 270·석재 0** 인
        ///  덜 자란 마을에서 영상이 시작하는 것이었다(빌드는 침대 6개·목재 1,600).
        ///  같은 연출이 환경에 따라 다른 마을을 찍으면 검증이 무의미하다.
        ///  상한은 느린 환경도 목표에 닿게 넉넉히 두고, 빠른 환경은 어차피 조기 종료한다.</summary>
        private const float WarmupCapRealSeconds = 190f;
        private const float WarmupScale = 20f;

        private IEnumerator Run()
        {
            cam = Camera.main;
            if (cam == null) yield break;
            camZ = cam.transform.position.z;
            var cc = cam.GetComponent<CameraController>();
            if (cc != null) cc.enabled = false;

            HideGameplayUI();
            BuildCaption();
            BuildBlackout();
            SnapCam(VillageCenter(), Wide);

            yield return Warmup();
            StartCapture();
            yield return FadeIn();
            StartCoroutine(KeepBusy());          // 촬영 내내 일감을 끊기지 않게 공급

            Vector2 village = VillageCenter();
            // 총 길이 ≈ 55초.  요강은 30~60초이고, 컷 길이는 일부러 고르지 않게 둔다
            //  (3.6 / 3.0 / 4.5 / 2.0 / 6.5 …) — 같은 길이로 자르면 화면이 실제로
            //  바뀌어도 바뀐 것처럼 느껴지지 않는다.

            // ── 아침 07:00 — 이미 살아 있는 마을 ────────────────────────
            Beat("아침");
            yield return Hour(7.0f, 1.5f);
            SnapCam(village, Wide);
            yield return Say("조선의 어느 산골.  여섯 사람이 자리를 잡았다.", 2.8f);
            yield return MoveCam(village, Near, 3.0f);

            // ── 오전 — 각자 일하러 흩어진다 ──────────────────────────────
            Beat("오전");
            yield return Hour(9.5f, 1.5f);
            yield return MoveCam(BusiestPoint(), Work, 3.0f);
            yield return Say("일감만 정해주면, 나머지는 주민들이 알아서 한다.", 2.8f);
            yield return FollowBusiest(3.5f, Work);

            // ── 낮 — 가마솥과 밥상 ──────────────────────────────────────
            Beat("낮");
            yield return Hour(12.5f, 1.5f);
            yield return MoveCam(village + new Vector2(0.6f, -0.4f), Near, 2.5f);
            yield return Say("밥을 짓고, 나무를 베고, 벽을 세운다.", 3.0f);
            yield return Wait(0.3f);

            // ── 오후 — 마을이 커진다 ────────────────────────────────────
            Beat("오후");
            yield return Hour(16.0f, 1.5f);
            Vector2 site = PlaceExtension(village);        // 벽이 실제로 올라간다
            yield return MoveCam(site, Work, 2.5f);
            yield return Wait(1.0f);

            // ── 저녁 — 손님이 온다 (아직 아무도 자지 않는다) ───────────
            //
            // 운영자 2026-08-09: "잘떄 약탈자가 와서는 때리다가 스스로 죽는건
            //  대체 멀까? / 안잘때 와야 할듯 한데."  맞다 — 전원이 잠든 마을에
            //  약탈자가 들어오면 **아무도 맞서지 않는다.**  화면에는 자는 사람을
            //  때리는 장면과, 뒤늦게 깬 누군가에게 쓰러지는 장면만 남는다.
            //  그래서 습격을 **저녁**으로 당긴다.  다들 깨어 있고 마당에 나와 있어
            //  달려나가 맞서는 그림이 된다.
            Beat("습격");
            yield return Hour(19.0f, 1.5f);
            AIDirector.RaidsSuspended = false;
            AIDirector.ForceRaidNow();
            yield return Say("그리고 저녁, 약탈자가 들이닥친다.", 2.4f);
            Beat("추격시작");
            yield return FollowThreat(8.5f);
            Beat("추격끝");

            // ── 밤 — 불을 켜고 눕는다 ───────────────────────────────────
            Beat("밤");
            busyPaused = true;              // 일감을 끊어야 잠자리에 든다
            yield return Hour(22.0f, 1f);
            SetLabels(false);               // 여섯이 나란히 누우면 이름표가 뭉친다
            yield return MoveCam(VillageCenter(), 4.8f, 3.0f);
            yield return Say("밤이 오면 등불을 밝힌다.", 2.8f);
            yield return Wait(1.0f);

            // ── 다음 날 새벽 — 어제보다 커진 마을 ───────────────────────
            Beat("새벽");
            SetLabels(true);           // 다시 각자의 일과 — 이 컷은 이름과 활동이 보여야 한다
            yield return Hour(6.5f, 1f);
            yield return MoveCam(VillageCenter(), Wide, 2.5f);
            yield return Say("당신은 방향만 정한다.\n마을은 스스로 살아간다.", 2.6f);
            yield return Wait(0.4f);
            // 끝에도 암전을 남긴다 — 시작과 같은 이유다.  녹화는 연출보다 길게 돌리는데
            //  (환경에 따라 워밍업이 48~190초로 달라져 넉넉히 잡아야 한다), 끝 지점을
            //  밖에서 계산하면 또 틀린다.  `trim-trailer.py` 가 두 번째 암전의 시작을
            //  본편의 끝으로 읽는다.  마무리 페이드는 영상으로서도 자연스럽다.
            yield return FadeOut();
            Debug.Log($"[Trailer] 연출 종료 — 프레임 {frameIndex}장");
            capturing = false;
            if (frameDir != null)
            {
                yield return null;          // 마지막 캡처가 디스크에 닿을 틈
                yield return null;
                Application.Quit(0);
            }
        }

        /// <summary>촬영 전에 게임을 고배속으로 하루 돌린다.
        ///
        /// 왜 필요한가: 게임 시작 직후를 찍으면 자원 0·벽 없음·주민 전원 취침이라
        ///  첫 화면이 **빈 터**로 읽힌다.  첫 촬영이 정확히 그랬다.  소개 영상은 이미
        ///  돌아가는 마을을 보여줘야 한다.
        ///
        /// 정직성: 화면에 없는 것을 만들어 넣는 게 아니라 **게임을 실제로 더 플레이한**
        ///  것이다.  녹화 앞부분(빨리감기)은 편집으로 잘라낸다.</summary>
        private IEnumerator Warmup()
        {
            // 워밍업 중 습격이 나면 주민이 죽은 채로 촬영이 시작될 수 있다 — 막는다.
            AIDirector.EventsSuspended = true;
            ColonyAutoWork.GraceSecondsOverride = 1.5f;   // 유예는 실시간이라 고배속에도 안 줄어든다

            float prev = Time.timeScale;
            Time.timeScale = WarmupScale;

            // **실시간이 아니라 게임 시계로 끊는다.**  46초 고정으로 돌렸더니 빌드는
            //  2일 3시까지, 에디터(녹화 경로)는 1일 17시까지밖에 못 갔다 — 에디터가
            //  20배속을 프레임으로 따라가지 못하기 때문이다.  같은 연출이 환경에 따라
            //  다른 마을에서 시작하면 촬영 결과를 신뢰할 수 없다.  실시간 상한은
            //  폭주 방지용으로만 둔다.
            var c = GameClock.Instance;
            float target = c != null ? c.GameSeconds + WarmupGameSeconds : 0f;
            float t = 0f;
            while (t < WarmupCapRealSeconds && c != null && c.GameSeconds < target)
            {
                // 배속을 매 프레임 다시 건다 — 부팅 일시정지·튜토리얼 게이트 등
                //  timeScale 을 만지는 코드가 여럿이라, 한 번 설정하고 믿으면
                //  조용히 0 이 된 채로 상한까지 헛돈다(실제로 그랬다).
                if (!Mathf.Approximately(Time.timeScale, WarmupScale))
                    Time.timeScale = WarmupScale;
                t += Dt;
                yield return null;
            }
            Time.timeScale = prev;

            AIDirector.EventsSuspended = false;
            // 이벤트·이야기 카드는 살리고 **습격 일정만** 잠근다 — 클라이맥스 컷 전에
            //  자연 습격이 터져 버리면 정작 그 컷에 적이 없다(첫 촬영이 그랬다).
            AIDirector.RaidsSuspended = true;
            ColonyAutoWork.GraceSecondsOverride = -1f;

            Debug.Log($"[Trailer] 워밍업 종료 — {t:F0}s(실시간) x{WarmupScale} → " +
                      $"{(c != null ? c.Day : 0)}일차 {(c != null ? c.Hour : 0)}시");
        }

        // ── 카메라 ──────────────────────────────────────────────────────────
        private void SnapCam(Vector2 p, float size)
        {
            cam.transform.position = new Vector3(p.x, p.y, camZ);
            cam.orthographicSize = size;
        }

        private IEnumerator MoveCam(Vector2 to, float size, float dur)
        {
            Vector3 from = cam.transform.position;
            float s0 = cam.orthographicSize, t = 0f;
            while (t < dur)
            {
                t += Dt;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                cam.transform.position = Vector3.Lerp(from, new Vector3(to.x, to.y, camZ), k);
                cam.orthographicSize = Mathf.Lerp(s0, size, k);
                yield return null;
            }
        }

        /// <summary>가장 활발한 곳을 천천히 따라간다 — 주민이 흩어져 일할 때,
        ///  '무슨 일이 벌어지는 곳' 에 카메라가 있어야 화면이 산다.</summary>
        private IEnumerator FollowBusiest(float dur, float size)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Dt;
                Vector2 target = BusiestPoint();
                Vector3 p = cam.transform.position;
                Vector3 want = new Vector3(target.x, target.y, camZ);
                cam.transform.position = Vector3.Lerp(p, want, Dt * 0.9f);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, Dt * 1.2f);
                yield return null;
            }
        }

        /// <summary>습격을 따라간다 — 이 영상의 유일한 액션 컷.
        ///
        /// 첫 촬영의 실패: 카메라를 마을에 고정하고 5.5초를 기다렸더니, 앞 프레임엔
        ///  적이 아직 도착하지 않았고 뒷 프레임엔 이미 격퇴가 끝나 있었다.  **전투가
        ///  벌어진 몇 초를 화면이 통째로 놓쳤다.**  그래서 두 가지를 고친다 —
        ///  (1) 적과 마을이 **둘 다** 들어오도록 배율을 거리에 맞춰 잡고,
        ///  (2) 적이 아직 없으면 기다린다(격퇴 후 컷이 남으면 마을로 돌아온다).</summary>
        private IEnumerator FollowThreat(float dur)
        {
            float t = 0f;
            bool snapped = false;
            while (t < dur)
            {
                t += Dt;
                Vector2 village = VillageCenter();
                Vector2 threat = ThreatPoint(out bool any);

                // 적이 **아직 멀면 마을을 잡고 기다린다.**  둘 다 담으려고 배율을
                //  키우면 화면 대부분이 빈 들판이 되고, 마을도 약탈자도 구석에
                //  몰린다(실측: 저녁 습격 프레임이 그랬다).  기다리는 동안 화면에는
                //  주민들이 하던 일을 멈추고 움직이는 모습이 잡히므로 비어 있지 않다.
                float dist = any ? Vector2.Distance(threat, village) : 999f;
                bool close = any && dist <= 11f;
                Vector2 want = close ? Vector2.Lerp(threat, village, 0.35f) : village;
                float size = close ? Mathf.Clamp(dist * 0.70f + 3.5f, 6.5f, 9.5f) : 8.5f;

                // 적을 처음 발견한 순간엔 **스냅**한다.  부드럽게 따라가면 7초 컷
                //  안에 도착하지 못해, 전투가 끝날 때까지 화면 밖에 머문다.
                if (close && !snapped)
                {
                    snapped = true;
                    cam.transform.position = new Vector3(want.x, want.y, camZ);
                    cam.orthographicSize = size;
                }
                cam.transform.position = Vector3.Lerp(cam.transform.position,
                    new Vector3(want.x, want.y, camZ), Dt * 3.0f);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size,
                                                  Dt * 2.5f);
                yield return null;
            }
        }

        // ── 일감 ────────────────────────────────────────────────────────────
        /// <summary>촬영 내내 일감이 끊기지 않게 한다.
        ///
        /// 왜 필요한가 (실측): 워밍업으로 목재가 목표(550)를 넘자 `ColonyAutoWork` 가
        ///  자동 지정을 멈췄고 — 규칙대로다, 목표를 채웠으니 — 오전 10시 컷에서 주민
        ///  다섯이 모닥불 앞에 그냥 서 있었다.  바로 그 위에 "일감만 정해두면 누가
        ///  무엇을 할지는 주민이 정한다" 자막이 떴다.  **화면이 자막을 반박했다.**
        ///
        /// 그래서 연출이 플레이어 역할을 한다 — 플레이어가 하는 것과 같은 지정
        ///  경로(`MarkWorld` / `SimulateDragRect`)로 나무와 광맥을 찍고, 오후에 증축
        ///  청사진을 놓는다.  주민이 무엇을 할지는 여전히 주민이 정한다.</summary>
        /// <summary>밤 컷에서는 일감을 끊는다 — 계속 일감을 주면 주민이 잠자리에
        ///  들지 않아 "밤이 오면 등에 불을 켜고 눕는다" 자막 위로 **빈 이불 여섯 채**가
        ///  나온다(실측).  화면이 자막을 반박하는 두 번째 사례다.</summary>
        private static bool busyPaused;

        private IEnumerator KeepBusy()
        {
            while (true)
            {
                if (busyPaused) { yield return Wait(1.0f); continue; }
                Vector2 c = VillageCenter();
                var chop = TreeChopDesignation.Instance;
                if (chop != null && chop.MarkedCount < 2) MarkNearest<TreeEntity>(c, 3, chop);
                var mine = MineDesignation.Instance;
                if (mine != null && mine.MarkedCount < 2) MarkNearestVeins(c, 3, mine);
                yield return Wait(5.0f);
            }
        }

        private static void MarkNearest<T>(Vector2 center, int count, TreeChopDesignation chop)
            where T : Component
        {
            var all = FindObjectsByType<T>(FindObjectsSortMode.None);
            System.Array.Sort(all, (a, b) =>
                ((Vector2)a.transform.position - center).sqrMagnitude
                .CompareTo(((Vector2)b.transform.position - center).sqrMagnitude));
            int n = 0;
            foreach (var t in all)
            {
                if (n >= count) break;
                if (t == null) continue;
                Vector2 p = t.transform.position;
                // 반경을 넓게 잡는다 — 좁으면 집 둘레만 벗겨져 60초 만에
                //  마을이 그루터기밭이 된다(첫 촬영이 그랬다).
                if ((p - center).sqrMagnitude > 26f * 26f) break;   // 정렬돼 있으니 종료
                if (chop.MarkWorld(p) != null) n++;
            }
        }

        private static void MarkNearestVeins(Vector2 center, int count, MineDesignation mine)
        {
            var all = FindObjectsByType<StoneVeinEntity>(FindObjectsSortMode.None);
            System.Array.Sort(all, (a, b) =>
                ((Vector2)a.transform.position - center).sqrMagnitude
                .CompareTo(((Vector2)b.transform.position - center).sqrMagnitude));
            int n = 0;
            foreach (var v in all)
            {
                if (n >= count) break;
                if (v == null) continue;
                Vector2 p = v.transform.position;
                if ((p - center).sqrMagnitude > 22f * 22f) break;
                if (mine.SimulateDragRect(p - Vector2.one * 0.35f, p + Vector2.one * 0.35f) > 0) n++;
            }
        }

        /// <summary>마을 옆에 증축 청사진을 놓는다 — 주민이 자재를 나르고 벽을 세운다.
        ///  놓은 자리를 돌려주어 카메라가 그쪽을 잡게 한다.</summary>
        private static Vector2 PlaceExtension(Vector2 village)
        {
            var bm = BuildManager.Instance;
            if (bm == null) return village;

            // 마을 오른쪽으로 한 칸 띄운 자리부터 훑는다 — 집과 겹치면 거부되므로
            //  성공한 칸만 남는다(실패는 조용히 넘어간다, 플레이어 클릭과 같은 취급).
            int placed = 0;
            Vector2 first = village;
            bm.SetMode(BuildManager.Mode.Wall);
            for (int off = 4; off <= 12 && placed < 9; off++)
            {
                int cx = Mathf.FloorToInt(village.x) + off;
                for (int dy = -2; dy <= 2 && placed < 9; dy++)
                {
                    int cy = Mathf.FloorToInt(village.y) + dy;
                    if (!bm.TryPlaceAt(cx, cy)) continue;
                    if (placed == 0) first = new Vector2(cx + 0.5f, cy + 0.5f);
                    placed++;
                }
                if (placed > 0) break;      // 한 줄이면 충분하다 — 벽이 올라가는 게 보이면 된다
            }
            bm.SetMode(BuildManager.Mode.Off);
            Debug.Log($"[Trailer] 증축 청사진 {placed}칸");
            return placed > 0 ? first : village;
        }

        // ── 좌표 ────────────────────────────────────────────────────────────
        /// <summary>마을 중심 — **구조물 전체**의 무게중심.
        ///
        /// 처음엔 침대 평균을 썼는데, 그러면 카메라가 집 안 한 점을 잡아 화면 아래
        ///  절반이 빈 잔디가 됐다.  마을은 집만이 아니라 벽·작업대·비축지까지다.</summary>
        private static Vector2 VillageCenter()
        {
            Vector2 sum = Vector2.zero; int n = 0;
            foreach (var w in FindObjectsByType<WallEntity>(FindObjectsSortMode.None))
                if (w != null) { sum += (Vector2)w.transform.position; n++; }
            foreach (var b in FindObjectsByType<BedEntity>(FindObjectsSortMode.None))
                if (b != null) { sum += (Vector2)b.transform.position; n++; }
            return n > 0 ? sum / n : Vector2.zero;
        }

        /// <summary>일하는 주민이 가장 많이 모인 지점.</summary>
        private static Vector2 BusiestPoint()
        {
            var labels = FindObjectsByType<PawnNameLabel>(FindObjectsSortMode.None);
            Vector2 sum = Vector2.zero; int n = 0;
            foreach (var l in labels)
            {
                if (l == null) continue;
                string a = l.CurrentActivity ?? "";
                if (a.Length == 0 || a.Contains("떠도") || a.Contains("수면")) continue;
                sum += (Vector2)l.transform.position; n++;
            }
            // 활동 지점만 잡으면 카메라가 마을 밖으로 끌려간다 — 마을 쪽으로 당긴다.
            return n > 0 ? Vector2.Lerp(sum / n, VillageCenter(), 0.40f) : VillageCenter();
        }

        /// <summary>**마을에 가장 가까운** 위협의 위치.
        ///
        /// 처음엔 위협 전체의 무게중심을 썼는데, 약탈자가 흩어져 들어오면 그 평균은
        ///  아무도 없는 빈 들판이었다 — 카메라가 배율 13까지 빠져 약탈자가 점이 되고
        ///  정작 교전은 화면 가장자리에 걸렸다.  덮치기 직전인 놈 하나를 잡으면
        ///  카메라가 자연히 교전 지점에 선다.</summary>
        private static Vector2 ThreatPoint(out bool any)
        {
            Vector2 village = VillageCenter();
            Vector2 best = Vector2.zero;
            float bestSq = float.MaxValue;
            void Consider(Vector2 p)
            {
                float d = (p - village).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = p; }
            }
            foreach (var b in FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None))
                if (b != null && !b.IsDead) Consider(b.transform.position);   // 쓰러진 적은 제외
            foreach (var w in FindObjectsByType<WolfEnemy>(FindObjectsSortMode.None))
                if (w != null) Consider(w.transform.position);
            // 광기에 빠진 동물도 습격의 한 형태다 — 이걸 빼고 세면 멧돼지 떼가 왔을 때
            //  카메라가 "위협 없음"으로 판단해 빈 마을을 잡는다.
            foreach (var a in FindObjectsByType<AnimalEntity>(FindObjectsSortMode.None))
                if (a != null && a.IsManhunter) Consider(a.transform.position);
            any = bestSq < float.MaxValue;
            return any ? best : Vector2.zero;
        }

        // ── 시계 ────────────────────────────────────────────────────────────
        private float filmT0 = -1f;

        /// <summary>컷 로그 — 촬영 타임라인을 **로그로** 검증한다.
        ///  프레임을 시점 추측으로 찍어 뒤지면 한 번에 한 순간밖에 못 본다.</summary>
        private void Beat(string name)
        {
            if (filmT0 < 0f) filmT0 = Time.unscaledTime;
            int foes = 0;
            ThreatPoint(out bool anyFoe);
            foreach (var b in FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None))
                if (b != null && !b.IsDead) foes++;
            var c = GameClock.Instance;
            Debug.Log($"[Trailer] t={Time.unscaledTime - filmT0:F1}s 컷={name} " +
                      $"{(c != null ? c.Day : 0)}일 {(c != null ? c.Hour : 0)}:{(c != null ? c.Minute : 0):00} " +
                      $"약탈자={foes} 위협={anyFoe}");
        }

        /// <summary>**다음에 오는** 그 시각으로 옮기고, 주민이 반응할 틈을 준다.
        ///
        /// 두 가지 실패를 같이 막는다 —
        ///
        /// (1) **시계가 뒤로 간다.**  처음엔 "오늘의 그 시각"으로 옮겼는데, 워밍업이
        ///     끝난 시각이 실행마다 달라(빌드 2일 3시 / 에디터 1일 17시) 목표가 현재보다
        ///     이르면 하루가 되감겼다.  녹화본 로그가 `1일 17:47 → 1일 9:10 → 1일 22:33`
        ///     이었다 — 화면 위 "정착 N일째"가 앞뒤로 튀는 영상이 나온다.  이제 목표가
        ///     현재보다 이르면 다음 날로 넘겨 **단조 증가**를 보장한다.  마지막 새벽 컷이
        ///     자연히 다음 날이 되므로 `nextDay` 인자도 필요 없어졌다.
        ///
        /// (2) **시각과 행동이 어긋난다.**  시계만 점프시키면 05:41 인데 주민 여섯이
        ///     "취침 이동" 라벨을 달고 서 있다.  옮긴 뒤 짧게 돌려 각자 새 일과를
        ///     고르게 한다(배속이 있으니 실시간 0.6초면 게임내 수십 분이다).</summary>
        private IEnumerator Hour(float hour, float scale)
        {
            yield return Dip(0f, 1f, 0.18f);      // 촬영하며 어두워진다 (컷 전환)
            recording = false;                    // ── 여기부터 영상에 안 들어감 ──

            var c = GameClock.Instance;
            if (c != null)
            {
                float target = Mathf.Floor(c.GameSeconds / 86400f) * 86400f
                             + Mathf.Clamp(hour, 0f, 23.99f) * 3600f;
                if (target <= c.GameSeconds) target += 86400f;   // 뒤로는 절대 가지 않는다
                c.SetGameSeconds(target);
            }
            var tc = TimeController.Instance;

            // 촬영을 멈춘 동안 **고배속으로 충분히 돌린다.**  시계만 옮기면 주민이
            //  이전 시각의 행동을 그대로 하고 있어 화면과 시각이 어긋난다(첫 촬영에서
            //  05:41 인데 여섯이 "취침 이동" 라벨을 달고 서 있었다).  이 구간은
            //  영상에 안 들어가므로 넉넉히 줄 수 있다.
            if (tc != null) tc.SetScale(20f);
            yield return Wait(2.5f);

            if (tc != null) tc.SetScale(scale);    // 컷 본편은 **실제 속도**로
            yield return Wait(0.4f);               // 배속 전환이 프레임에 안 걸리게

            recording = true;                      // ── 다시 촬영 ──
            yield return Dip(1f, 0f, 0.20f);
        }

        /// <summary>화면 전체를 어둡게/밝게 (컷 전환용 dip-to-black).</summary>
        private IEnumerator Dip(float from, float to, float dur)
        {
            if (blackGroup == null) yield break;
            float t = 0f;
            while (t < dur)
            {
                t += Dt;
                blackGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            blackGroup.alpha = to;
        }

        // ── UI ──────────────────────────────────────────────────────────────
        /// <summary>시청자에게 필요 없는 UI 를 끈다.
        ///
        /// 기준 (Derek Lieu, "How Much HUD/UI to Show in a Game Trailer"):
        ///  *"Try turning it all off before capturing.  If it's still easy to understand
        ///   what is happening in the shot, then great!"*  다만 전략·경영 장르는
        ///  UI 자체가 셀링 포인트라 전부 끄지는 않는다 — **자원·목표 패널은 남긴다.**
        ///  버튼 안내(하단 바)·튜토리얼 문구·초상화 바는 조작하는 사람에게만 필요한
        ///  정보라 끈다.</summary>
        private static void HideGameplayUI()
        {
            string[] hide = { "GuiControlBar", "ColonistBar", "TutorialOverlay",
                              "TutorialCanvas", "SpeedPanel", "TabBar",
                              // 건축 팔레트 — 녹화본 좌하단에 그대로 남아 있었다.
                              //  플레이어가 클릭하는 도구지 시청자가 볼 것이 아니다.
                              "ArchitectMenu", "ArchitectPanel", "BuildMenu" };
            int n = 0;
            foreach (var name in hide)
            {
                var go = GameObject.Find(name);
                if (go != null) { go.SetActive(false); n++; }
            }
            // 이름이 안 잡히는 것들 — 컴포넌트로 한 번 더.
            foreach (var t in FindObjectsByType<TutorialOverlay>(FindObjectsSortMode.None))
                if (t != null) { t.gameObject.SetActive(false); n++; }
            foreach (var g in FindObjectsByType<GuiControlBar>(FindObjectsSortMode.None))
                if (g != null) { g.gameObject.SetActive(false); n++; }
            foreach (var c in FindObjectsByType<ColonistBar>(FindObjectsSortMode.None))
                if (c != null) { c.gameObject.SetActive(false); n++; }
            foreach (var am in FindObjectsByType<ArchitectMenu>(FindObjectsSortMode.None))
                if (am != null) { am.gameObject.SetActive(false); n++; }
            Debug.Log($"[Trailer] UI {n}개 숨김 (자원·목표 패널은 유지)");
        }

        /// <summary>주민 이름·활동 라벨을 켜고 끈다 (연출용 — 게임 로직은 안 건드린다).
        ///
        /// `PawnNameLabel.enabled = false` 만으로는 **안 꺼진다.**  라벨은 컴포넌트가
        ///  매 프레임 그리는 게 아니라 자식 `NameLabel`/`StatusLabel` GameObject 의
        ///  TextMesh 라서, 컴포넌트를 멈추면 마지막에 그려진 글자가 그대로 남는다.
        ///  (첫 촬영에서 밤 컷 라벨이 안 꺼진 이유가 이것이었다.)</summary>
        private static void SetLabels(bool on)
        {
            foreach (var l in FindObjectsByType<PawnNameLabel>(FindObjectsSortMode.None))
            {
                if (l == null) continue;
                if (on) l.enabled = true;
                foreach (Transform t in l.transform)
                    if (t.name == "NameLabel" || t.name == "StatusLabel")
                        t.gameObject.SetActive(on);
                if (!on) l.enabled = false;      // 끌 때는 나중에 — Update 가 되살리지 못하게
            }
        }

        // ── 프레임 덤프 ─────────────────────────────────────────────────────
        private int frameIndex;
        private bool capturing;

        /// <summary>지금 프레임을 **영상에 넣을지**.
        ///
        /// 운영자 2026-08-09: *"플레이를 빨리 빨리 틀게 아니라 플레이를 길게 잡고
        ///  영상 자체를 편집을해."*  맞는 지적이다 — 이전 판은 전 구간을 3배속으로
        ///  돌려 한 번에 찍었고, 그래서 **모든 장면이 빨리감기처럼** 보였다.
        ///
        /// 이제 컷 사이에서 촬영을 멈춘다.  멈춘 동안 시계를 옮기고 게임을 고배속으로
        ///  돌려 주민이 새 일과에 자리잡게 한 뒤, 다시 촬영을 켜고 **실제 속도로**
        ///  찍는다.  프레임과 오디오 모두 이 플래그를 따르므로, 결과물은 별도 편집
        ///  없이 이미 잘라 붙인 영상이 된다.</summary>
        private bool recording = true;

        /// <summary>연출이 쓰는 시간 간격.
        ///
        /// `Time.captureFramerate` 는 `Time.deltaTime` 만 고정 스텝으로 바꾸고
        ///  **`unscaledDeltaTime` 은 실제 경과 시간 그대로**다.  이걸 모르고 연출을
        ///  unscaledDeltaTime 으로 재다가, 프레임 저장이 느린 만큼 컷이 실시간으로만
        ///  흐르고 게임은 거의 정지한 영상이 나왔다 — 8.5초짜리 추격 컷에 프레임 35장,
        ///  게임 시간 5분.  캡처 중에는 **프레임 수가 시간**이다.</summary>
        private static float Dt =>
            Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Time.unscaledDeltaTime;

        private System.IO.FileStream wav;
        private int wavChannels;
        private int wavSampleRate;
        private long wavSampleBytes;

        private void StartCapture()
        {
            if (frameDir == null) return;
            System.IO.Directory.CreateDirectory(frameDir);
            // 이 지점 이후로 Unity 는 실시간을 버리고 **프레임당 1/30 초**로 돈다.
            //  저장이 느려도 연출 타이밍(Dt)이 흔들리지 않는다.
            Time.captureFramerate = CaptureFps;
            BoostMusicForCapture();
            StartAudioCapture();
            capturing = true;
            StartCoroutine(CaptureLoop());
            Debug.Log($"[Trailer] 프레임 덤프 시작 — {frameDir} @ {CaptureFps}fps");
        }

        /// <summary>게임 소리를 **프레임과 같은 시간축으로** 받아 WAV 로 쓴다.
        ///
        /// 계기 (2026-08-09 운영자): *"가장 핵심 제출용 영상에 sfx와 bgm이 안나옴."*
        ///  프레임을 PNG 로 덤프해 ffmpeg 로 합치면 그림만 남는다 — 소리는 어디에도
        ///  담기지 않는다.
        ///
        /// 나중에 BGM 을 얹는 방식은 쓰지 않는다.  이 게임의 소리는 도끼질·망치질·
        ///  가마솥 같은 **행동에 붙은 SFX** 가 대부분이라, 화면과 어긋나는 순간
        ///  "영상에 음악을 깐 것"이 되어 오히려 가짜로 보인다.
        ///
        /// `AudioRenderer` 는 Unity 의 **오프라인 오디오 렌더** 경로다 —
        ///  `captureFramerate` 로 가상 시간을 쓰는 동안에도 프레임당 정확한 샘플 수를
        ///  돌려주므로 그림과 소리가 어긋나지 않는다.  (실시간 마이크/루프백 녹음은
        ///  PNG 저장이 느린 만큼 소리가 앞서 나가 못 쓴다.)  Start() 를 부르면 스피커
        ///  출력이 꺼지므로 검증 스윕이 시끄러워지지도 않는다.</summary>
        /// <summary>촬영용으로 배경음을 키운다.
        ///
        /// 운영자 2026-08-09: *"시연 영상의 BGM 볼륨 너무 작음."*
        ///  게임 안에서 BGM 은 일부러 낮다 — 오래 켜 두는 소리라 크면 피곤하고,
        ///  도끼질·망치질 같은 **행동 소리**가 묻히면 안 된다.  그런데 60초짜리
        ///  영상에서는 반대다: 배경음이 안 들리면 그냥 조용한 영상이 된다.
        ///  게임 기본값은 그대로 두고 촬영 중에만 올린다.</summary>
        private static void BoostMusicForCapture()
        {
            int n = 0;
            foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (src == null || !src.loop) continue;   // 루프 = 배경음/앰비언트
                src.volume = Mathf.Clamp01(src.volume * 2.6f + 0.18f);
                n++;
            }
            Debug.Log($"[Trailer] 배경음 {n}개 볼륨 상향 (촬영용)");
        }

        private void StartAudioCapture()
        {
            wavChannels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            wavSampleRate = AudioSettings.outputSampleRate;
            string path = System.IO.Path.Combine(frameDir, "audio.wav");
            wav = new System.IO.FileStream(path, System.IO.FileMode.Create);
            WriteWavHeader(0);                       // 크기는 마지막에 되돌아와 채운다
            if (!AudioRenderer.Start())
            {
                Debug.LogWarning("[Trailer] AudioRenderer.Start 실패 — 무음으로 진행");
                wav.Dispose(); wav = null;
                return;
            }
            Debug.Log($"[Trailer] 오디오 캡처 시작 — {wavSampleRate}Hz {wavChannels}ch");
        }

        private void CaptureAudioFrame()
        {
            if (wav == null) return;
            int sc = AudioRenderer.GetSampleCountForCaptureFrame();
            if (sc <= 0) return;
            var buf = new NativeArray<float>(sc * wavChannels, Allocator.Temp);
            AudioRenderer.Render(buf);
            // float → 16bit PCM.  픽셀아트 게임 사운드에 32bit float 은 과하고,
            //  파일이 3배가 되며 플레이어 호환성만 나빠진다.
            var bytes = new byte[buf.Length * 2];
            for (int i = 0; i < buf.Length; i++)
            {
                short v = (short)(Mathf.Clamp(buf[i], -1f, 1f) * 32767f);
                bytes[i * 2] = (byte)(v & 0xFF);
                bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }
            wav.Write(bytes, 0, bytes.Length);
            wavSampleBytes += bytes.Length;
            buf.Dispose();
        }

        private void StopAudioCapture()
        {
            if (wav == null) return;
            AudioRenderer.Stop();
            wav.Seek(0, System.IO.SeekOrigin.Begin);
            WriteWavHeader(wavSampleBytes);          // 이제 실제 크기를 안다
            wav.Flush();
            wav.Dispose();
            wav = null;
            Debug.Log($"[Trailer] 오디오 캡처 종료 — {wavSampleBytes / 1024}KB");
        }

        private void WriteWavHeader(long dataBytes)
        {
            int byteRate = wavSampleRate * wavChannels * 2;
            void U32(uint v) { wav.Write(System.BitConverter.GetBytes(v), 0, 4); }
            void U16(ushort v) { wav.Write(System.BitConverter.GetBytes(v), 0, 2); }
            void Tag(string t) { foreach (char c in t) wav.WriteByte((byte)c); }
            Tag("RIFF"); U32((uint)(36 + dataBytes)); Tag("WAVE");
            Tag("fmt "); U32(16); U16(1); U16((ushort)wavChannels);
            U32((uint)wavSampleRate); U32((uint)byteRate);
            U16((ushort)(wavChannels * 2)); U16(16);
            Tag("data"); U32((uint)dataBytes);
        }

        /// <summary>렌더가 끝난 뒤 픽셀을 직접 읽어 저장한다.
        ///
        /// `ScreenCapture.CaptureScreenshot` 은 저장을 다음 프레임으로 미루는데,
        ///  매 프레임 부르면 앞선 요청이 조용히 버려진다 — 1,746 프레임을 요청해
        ///  879장만 남았다(정확히 절반).  동기 읽기는 느리지만 `captureFramerate`
        ///  덕분에 느린 것이 결과에 영향을 주지 않는다.</summary>
        private IEnumerator CaptureLoop()
        {
            var eof = new WaitForEndOfFrame();
            while (capturing)
            {
                yield return eof;
                if (!recording) continue;     // 컷 사이 — 영상에 넣지 않는다
                CaptureAudioFrame();          // 그림과 **같은 프레임**의 소리
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(
                    System.IO.Path.Combine(frameDir, $"f{frameIndex:D5}.png"),
                    tex.EncodeToPNG());
                Destroy(tex);
                frameIndex++;
            }
            StopAudioCapture();
        }

        // ── 암전 ────────────────────────────────────────────────────────────
        /// <summary>워밍업 구간을 검은 화면으로 덮는다.
        ///
        /// 두 가지를 동시에 해결한다 —
        ///  · 20배속 빨리감기는 영상에 넣을 수 없다.
        ///  · **어디서 잘라야 하는지를 영상이 스스로 알려준다.**  녹화 시작 시각과
        ///    게임 시작 시각의 오프셋은 실행마다 다르고 밖에서 알 방법이 없는데,
        ///    검은 구간이 있으면 `ffmpeg blackdetect` 가 정확한 지점을 찾아낸다.
        ///    초 단위를 손으로 맞추다 한 프레임씩 어긋나는 일이 없다.</summary>
        private CanvasGroup blackGroup;

        private void BuildBlackout()
        {
            var go = new GameObject("TrailerBlackout",
                typeof(Canvas), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
            var cv = go.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 6000;                     // 자막(5000)보다도 위
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            blackGroup = go.GetComponent<CanvasGroup>();
            blackGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            const float Dur = 0.8f;
            float t = 0f;
            while (t < Dur)
            {
                t += Dt;
                blackGroup.alpha = Mathf.SmoothStep(0f, 1f, t / Dur);
                yield return null;
            }
            blackGroup.alpha = 1f;
        }

        private IEnumerator FadeIn()
        {
            const float Dur = 1.2f;
            float t = 0f;
            while (t < Dur)
            {
                t += Dt;
                blackGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / Dur);
                yield return null;
            }
            blackGroup.alpha = 0f;
        }

        // ── 자막 ────────────────────────────────────────────────────────────
        private void BuildCaption()
        {
            var canvasGo = new GameObject("TrailerCaption",
                typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var cv = canvasGo.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 5000;                    // 무엇보다 위
            var sc = canvasGo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            capGroup = canvasGo.GetComponent<CanvasGroup>();
            capGroup.alpha = 0f;

            // 아래쪽 띠에 **어두운 그라디언트 판**을 먼저 깐다.
            //  첫 촬영에서 흰 글씨 + 검은 아웃라인만으로는 잔디 위에서 글자가 그대로
            //  사라졌다 — 밝기가 비슷한 배경 위에서는 외곽선이 대비를 만들지 못한다.
            //  (아트 가이드 §10 과 같은 이유: 밝기 충돌은 색이 아니라 밝기로 푼다.)
            var scrimGo = new GameObject("CaptionScrim", typeof(RectTransform), typeof(RawImage));
            scrimGo.transform.SetParent(canvasGo.transform, false);
            var srt = scrimGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 0.34f);
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            var raw = scrimGo.GetComponent<RawImage>();
            raw.texture = BottomFadeTexture();
            raw.color = Color.white;
            raw.raycastTarget = false;

            // 아래쪽 1/5 지점 — 게임 화면을 가리지 않으면서 시선이 자연히 닿는 곳.
            //
            // 운영자 2026-08-09: "자막인지 구별이 확실히 되게 하고 좀 더 화려하게".
            //  게임 화면에도 한글이 잔뜩 떠 있다(주민 이름·활동·토스트·목표 패널).
            //  자막이 그것들과 **같은 크기·같은 무게**면 시청자는 무엇이 설명이고
            //  무엇이 게임 UI 인지 구분하지 못한다.  그래서 세 가지로 갈라 놓는다 —
            //   · 크게 (46 → 58, 게임 안 어떤 글자보다 크다)
            //   · 두껍게 (외곽선 3px + 아래로 떨어지는 그림자)
            //   · 따뜻한 미색 (게임 UI 의 금빛/흰빛과 다른 톤)
            var go = new GameObject("Caption",
                typeof(RectTransform), typeof(Text), typeof(Outline), typeof(Shadow));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.07f, 0.06f);
            rt.anchorMax = new Vector2(0.93f, 0.26f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            caption = go.GetComponent<Text>();
            caption.font = UITheme.LoadKoreanFont(58);
            caption.fontSize = 58;
            caption.lineSpacing = 1.15f;
            caption.alignment = TextAnchor.LowerCenter;
            caption.color = new Color(1f, 0.975f, 0.92f, 1f);
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.verticalOverflow = VerticalWrapMode.Overflow;

            var ol = go.GetComponent<Outline>();
            ol.effectColor = new Color(0.03f, 0.02f, 0.02f, 0.95f);
            ol.effectDistance = new Vector2(3f, -3f);
            var sh = go.GetComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
            sh.effectDistance = new Vector2(0f, -6f);      // 바닥에 떨어지는 그림자
        }

        /// <summary>아래로 갈수록 어두워지는 1px 폭 세로 그라디언트.
        ///  단색 반투명 박스로 깔면 경계선이 보여 '자막 바'처럼 읽힌다 — 화면에
        ///  얹힌 판이 아니라 화면이 어두워진 것처럼 보여야 한다.</summary>
        private static Texture2D BottomFadeTexture()
        {
            const int H = 64;
            var tex = new Texture2D(1, H, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < H; y++)
            {
                float t = 1f - (y / (float)(H - 1));          // y=0 이 아래(가장 어둡다)
                float a = Mathf.SmoothStep(0f, 1f, t) * 0.74f;
                tex.SetPixel(0, y, new Color(0.02f, 0.02f, 0.03f, a));
            }
            tex.Apply();
            return tex;
        }

        /// <summary>자막 한 줄을 띄웠다 지운다 (페이드).  대사가 아니라 **안내**다 —
        ///  짧고, 화면이 이미 말하는 것을 반복하지 않는다.</summary>
        private IEnumerator Say(string text, float hold)
        {
            if (caption == null) { yield return Wait(hold); yield break; }
            caption.text = text;
            yield return Rise(0.42f);       // 아래에서 올라오며 나타난다
            yield return Wait(hold);
            yield return Fade(1f, 0f, 0.45f);
        }

        /// <summary>자막이 **아래에서 올라오며** 나타난다.
        ///  제자리 페이드는 배경이 복잡하면 언제 떴는지 모르고 지나간다 —
        ///  작은 움직임이 붙으면 눈이 먼저 그쪽으로 간다.</summary>
        private IEnumerator Rise(float dur)
        {
            var rt = caption.rectTransform;
            Vector2 baseMin = rt.offsetMin, baseMax = rt.offsetMax;
            float t = 0f;
            while (t < dur)
            {
                t += Dt;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                capGroup.alpha = k;
                float dy = (1f - k) * -26f;
                rt.offsetMin = baseMin + new Vector2(0f, dy);
                rt.offsetMax = baseMax + new Vector2(0f, dy);
                yield return null;
            }
            capGroup.alpha = 1f;
            rt.offsetMin = baseMin; rt.offsetMax = baseMax;
        }

        private IEnumerator Fade(float a, float b, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Dt;
                capGroup.alpha = Mathf.Lerp(a, b, Mathf.Clamp01(t / dur));
                yield return null;
            }
            capGroup.alpha = b;
        }

        // 연출 시간은 **실시간**이다 (배속과 무관해야 컷 길이가 설계대로 나온다).
        private static IEnumerator Wait(float sec)
        {
            float t = 0f;
            while (t < sec) { t += Dt; yield return null; }
        }
    }
}
