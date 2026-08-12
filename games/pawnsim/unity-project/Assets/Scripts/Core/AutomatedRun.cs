namespace MelonS.GameProto.Core
{
    /// <summary>이 실행이 **사람이 아니라 하네스**가 띄운 것인지 한 곳에서 판정한다.
    ///
    /// 계기 (2026-08-01 정합성 리뷰 #1 — 이 레포에서 가장 비싼 무음 실패):
    ///  `GameManager.Start()` 가 부팅 직후 `PauseAtStart()` 로 `timeScale = 0` 을 건다
    ///  (사람이 맵을 둘러본 뒤 직접 시작하게 하는 의도된 동작).  그런데 이 호출이
    ///  `-testmode`/`-integration` **인자 파싱보다 먼저** 있었다.  모든 하네스의 첫 문장은
    ///  스케일드 `WaitForSeconds` 라 timeScale 0 에서는 **영원히 깨어나지 않는다**.
    ///  즉 통합/유닛/롱플레이/오토QA 10개가 실행되자마자 조용히 멈춰 있었고,
    ///  리포트 JSON 이 아예 생성되지 않는데도 아무도 알아채지 못했다.
    ///  유일하게 살아 있던 건 `-repro` 뿐이다 — 그것만 pause 를 면제받고 있었기 때문이다.
    ///
    /// 왜 하드코딩 목록을 한 파일에 모으나:
    ///  면제 조건이 `!ReproHarness.Enabled` 처럼 **한 하네스 이름**으로 적혀 있으면,
    ///  새 하네스를 추가한 사람은 자기 것이 왜 안 도는지 알 방법이 없다(로그도 없다).
    ///  판정을 여기 모아 두면 플래그 한 줄 추가로 끝나고, 목록 자체가 문서가 된다.</summary>
    public static class AutomatedRun
    {
        /// <summary>하네스 활성화 인자 — 하나라도 있으면 사람이 보고 있지 않은 실행이다.</summary>
        private static readonly string[] HarnessArgs =
        {
            "-testmode",            // Tests/TestRunner (유닛)
            "-integration",         // Tests/IntegrationTestRunner
            "-repro",               // Tests/ReproHarness (시나리오)
            "-longplay",            // Tests/LongPlaySurvivalRunner
            "-ui-scenario",         // Tests/UIScenarioRunner
            "-ui-sweep",            // Tests/UiSweepQA
            "-pawndiag",            // PawnDiagnostics
            "-build-qa",            // BuildAutoQA
            "-build-click-qa",      // BuildClickAutoQA
            "-architect-click-qa",  // ArchitectClickAutoQA
            "-pawn-action-qa",      // PawnActionAutoQA
            "-feature-audit",       // FeatureAuditQA
            "-screenshot",          // AutoScreenshotter (무인 캡처)
        };

        private static bool cached;
        private static bool value;

        /// <summary>하네스 실행이면 true.  부팅 일시정지·튜토리얼 등 **사람 대상 연출**을
        /// 건너뛰는 판정에 쓴다.  인자는 프로세스 수명 내 불변이라 1회만 읽는다.</summary>
        public static bool Active
        {
            get
            {
                if (cached) return value;
                cached = true;
                var argv = System.Environment.GetCommandLineArgs();
                foreach (var a in argv)
                {
                    foreach (var h in HarnessArgs)
                    {
                        if (a != h) continue;
                        value = true;
                        UnityEngine.Debug.Log($"[AutomatedRun] 하네스 실행 감지 ({h}) — 부팅 일시정지 생략");
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
