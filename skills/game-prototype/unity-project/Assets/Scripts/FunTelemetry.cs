using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>재미를 **숫자로** 남기는 계측기.
    ///
    /// 계기 (2026-08-02 운영자): "유저의 재미를 점수화 시켜서 재미에 대한 점수를
    /// 올려야 할 거 같아."
    ///
    /// 이 레포는 이미 톤(`check-art-tone.py`)·에셋 드리프트(`check-asset-drift.py`)를
    /// 수치로 판정한다.  재미만 감상으로 남아 있었고, 그래서 같은 문제를 세 번 다르게
    /// 진단했다 — 7/30 "영상 2점", 7/31 "읽히지 않는다", 8/02 "10분간 숫자가 안 움직인다".
    /// 셋 다 같은 것을 다른 말로 본 것이다.  잴 수 있으면 한 번에 볼 수 있다.
    ///
    /// ── 무엇을 재는가 ──────────────────────────────────────────────────────
    /// 상태를 주기적으로 찍고, **사건은 상태 변화에서 유도한다.**  시스템마다 콜백을
    /// 박으면 훅이 늘 때마다 계측이 갈라진다(이 레포의 반복 함정).  여기서는
    ///   · 목표 달성 수가 늘면        → 진행 사건
    ///   · 살아있는 적이 0→N, N→0     → 위협 발생 / 해소
    ///   · 구조물 수가 늘면            → 건설 완료
    ///   · 연구 완료 수가 늘면        → 연구 사건
    /// 로 읽는다.  계측 대상이 늘어도 이 파일만 본다.
    ///
    /// ── 언제 도는가 ────────────────────────────────────────────────────────
    /// `-funscore &lt;경로&gt;` 인자가 있을 때만.  평상시 플레이에는 아무 비용도 없다.
    /// 출력은 JSONL — 한 줄이 한 표본이고, 점수 계산은 `scripts/fun-score.py` 가 한다.
    /// **판정 규칙을 게임 안에 두지 않는 이유**: 루브릭은 자주 바뀌는데 게임은 자주
    /// 빌드할 수 없다.  게임은 사실만 남기고, 해석은 밖에서 한다.
    /// </summary>
    public class FunTelemetry : MonoBehaviour
    {
        private const float SampleInterval = 5f;   // 실초

        private string outPath;
        private float next;
        private readonly StringBuilder buf = new StringBuilder(1 << 16);

        // 직전 표본 — 사건을 상태 변화에서 유도하기 위해 들고 있는다.
        private int prevObjectives = -1, prevStructures = -1, prevResearch = -1, prevThreats = -1;
        private int prevDirectorEvents = -1;
        private readonly HashSet<string> everSeenActivities = new HashSet<string>();
        /// <summary>활동 라벨별 누적 표본 수.  세션 끝에 한 줄로 남긴다.</summary>
        private readonly Dictionary<string, int> histogram = new Dictionary<string, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string path = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-funscore") { path = args[i + 1]; break; }
            if (string.IsNullOrEmpty(path)) return;

            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindFirstObjectByType<FunTelemetry>() != null) return;
                var go = new GameObject("~FunTelemetry");
                go.AddComponent<FunTelemetry>().outPath = path;
                Debug.Log($"[Fun] 계측 시작 → {path}");
            });
        }

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + SampleInterval;
            Sample();
        }

        private void OnApplicationQuit() { WriteHistogram(); Flush(); }
        private void OnDestroy() { WriteHistogram(); Flush(); }

        private void Sample()
        {
            var res = ResourceManager.Instance;
            var clock = GameClock.Instance;

            int objectives = DoneObjectives();
            int structures = CountStructures();
            int research = DoneResearch();
            int threats = LiveThreats();
            int dirEvents = AIDirector.EventCount;

            // 활동 라벨 — 유휴 비율과 종류 수가 '살아 있는가'의 대리 지표다.
            int pawns = 0, idle = 0;
            var kinds = new HashSet<string>();
            foreach (var lbl in FindObjectsByType<PawnNameLabel>(FindObjectsSortMode.None))
            {
                if (lbl == null) continue;
                pawns++;
                string a = lbl.CurrentActivity ?? "";
                if (a.Length == 0 || a.Contains("떠도")) idle++;
                else { kinds.Add(a); everSeenActivities.Add(a); }
                // 활동별 표본 수 — '유휴 46%' 가 나왔을 때 **무엇을 하느라 안 하는지**
                //  를 알려면 비율만으로는 부족하다.  라벨 분포가 있으면 "요리만 돌고
                //  건설이 0" 같은 편향이 바로 보인다.
                string key = a.Length == 0 ? "(없음)" : a;
                histogram.TryGetValue(key, out int c);
                histogram[key] = c + 1;
            }

            var ev = new List<string>();
            if (prevObjectives >= 0 && objectives > prevObjectives) ev.Add("objective");
            if (prevStructures >= 0 && structures > prevStructures) ev.Add("build");
            if (prevResearch >= 0 && research > prevResearch) ev.Add("research");
            // 디렉터 사건(습격·잔잔한 이벤트) — 화면에 알림으로 뜨므로 심사자에게는
            //  '무슨 일이 일어났다' 로 읽힌다.  상태(누적 카운터)로 읽어 훅을 피한다.
            if (prevDirectorEvents >= 0 && dirEvents > prevDirectorEvents)
                for (int k = prevDirectorEvents; k < dirEvents; k++) ev.Add("director");
            if (prevThreats >= 0 && threats > prevThreats) ev.Add("threat_start");
            if (prevThreats > 0 && threats == 0) ev.Add("threat_clear");
            prevObjectives = objectives; prevStructures = structures;
            prevResearch = research; prevThreats = threats; prevDirectorEvents = dirEvents;

            var sb = new StringBuilder(256);
            sb.Append('{');
            F(sb, "t", Time.unscaledTime); sb.Append(',');
            F(sb, "gameDay", clock != null ? clock.Day : 0); sb.Append(',');
            F(sb, "gameHour", clock != null ? clock.Hour : 0); sb.Append(',');
            F(sb, "wood", res != null ? res.wood : 0); sb.Append(',');
            F(sb, "stone", res != null ? res.stone : 0); sb.Append(',');
            F(sb, "food", res != null ? res.food : 0); sb.Append(',');
            F(sb, "meals", res != null ? res.meals : 0); sb.Append(',');
            F(sb, "value", ColonyValue()); sb.Append(',');
            F(sb, "objectives", objectives); sb.Append(',');
            F(sb, "structures", structures); sb.Append(',');
            F(sb, "research", research); sb.Append(',');
            F(sb, "threats", threats); sb.Append(',');
            F(sb, "dirEvents", dirEvents); sb.Append(',');
            F(sb, "pawns", pawns); sb.Append(',');
            F(sb, "idle", idle); sb.Append(',');
            F(sb, "actKinds", kinds.Count); sb.Append(',');
            F(sb, "actEver", everSeenActivities.Count); sb.Append(',');
            sb.Append("\"events\":[");
            for (int i = 0; i < ev.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(ev[i]).Append('"');
            }
            sb.Append("]}");
            buf.Append(sb).Append('\n');

            // 표본이 쌓이면 흘려 보낸다 — 크래시해도 그때까지는 남는다.
            if (buf.Length > 8192) Flush();
        }

        /// <summary>세션 끝에 활동 분포를 한 줄 더 남긴다 (`kind`:"histogram").
        ///  표본 줄과 섞이지 않도록 `kind` 로 구분한다 — 채점기는 이 줄을 건너뛴다.</summary>
        private void WriteHistogram()
        {
            if (histogram.Count == 0) return;
            var sb = new StringBuilder(256);
            sb.Append("{\"kind\":\"histogram\",\"activities\":{");
            bool first = true;
            foreach (var kv in histogram)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(kv.Key.Replace("\"", "")).Append("\":").Append(kv.Value);
            }
            sb.Append("}}");
            buf.Append(sb).Append('\n');
            histogram.Clear();
        }

        private void Flush()
        {
            if (buf.Length == 0 || string.IsNullOrEmpty(outPath)) return;
            try { System.IO.File.AppendAllText(outPath, buf.ToString()); }
            catch (System.Exception e) { Debug.LogWarning($"[Fun] 기록 실패: {e.Message}"); }
            buf.Length = 0;
        }

        private static void F(StringBuilder sb, string k, float v)
            => sb.Append('"').Append(k).Append("\":")
                 .Append(v.ToString("0.##", CultureInfo.InvariantCulture));

        // ── 상태 읽기 ──────────────────────────────────────────────────────
        private static int DoneObjectives()
        {
            var co = ColonyObjectives.Instance;
            return co != null ? co.DoneCount : 0;
        }

        private static int DoneResearch()
        {
            var rm = ResearchManager.Instance;
            return rm != null ? rm.CompletedCount : 0;
        }

        private static int LiveThreats()
        {
            int n = 0;
            foreach (var b in FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None))
                if (b != null) n++;
            foreach (var w in FindObjectsByType<WolfEnemy>(FindObjectsSortMode.None))
                if (w != null) n++;
            return n;
        }

        /// <summary>구조물 수 — '마을이 자랐는가'의 가장 직접적인 지표.</summary>
        private static int CountStructures()
        {
            int n = 0;
            foreach (var w in FindObjectsByType<WallEntity>(FindObjectsSortMode.None)) if (w != null) n++;
            foreach (var b in FindObjectsByType<BedEntity>(FindObjectsSortMode.None)) if (b != null) n++;
            foreach (var d in FindObjectsByType<DoorEntity>(FindObjectsSortMode.None)) if (d != null) n++;
            return n;
        }

        // 정착지 가치 — 습격 규모 산정이 쓰는 값과 **같은 것**을 쓴다 (AIDirector).
        //  HUD 우상단 "가치" 와도 같은 출처라 계측이 화면과 갈라지지 않는다.
        private static float ColonyValue() => AIDirector.WealthSnapshot();
    }
}
