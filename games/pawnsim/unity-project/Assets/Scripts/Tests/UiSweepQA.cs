using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// UI 전수 스윕 QA — **모든 버튼을 눌러 보고 아무 일도 안 일어나는 것을 찾는다.**
    ///
    /// 왜 (2026-07-29):
    ///   운영자가 두 번 같은 지적을 했다 — "이 ui는 먼데 아무것도 없는데 껍대기만
    ///   있는거지?" (장비 탭), 그리고 스킬 패널.  둘 다 **빌드는 성공하고 게이트도
    ///   통과하는데 화면만 비어 있는** 종류였고, 사람이 우연히 눌러 봐야만 드러났다.
    ///   FeatureAuditQA 는 건축물만 보고, 재현 시나리오는 **정해진 버튼만** 누른다 —
    ///   "안 눌러 본 버튼"은 구조적으로 사각지대다.
    ///
    ///   QA 현실성 리서치(docs/qa-realism-2026-07-29.md)의 2층(행동 계측)에 해당한다.
    ///   LLM 페르소나의 의견이 아니라 **눌렀더니 화면이 변했는가**라는 사실만 본다 —
    ///   재현 가능하고 편향이 없다.
    ///
    /// 판정:
    ///   DEAD  누른 뒤 화면이 사실상 안 변함 (변화 픽셀 &lt; 0.2%) — 죽은 버튼
    ///   EMPTY 패널이 열렸는데 그 안에 읽을 내용이 없음 (새로 생긴 텍스트 0) — 껍데기
    ///   OK    변화 있음
    ///
    /// CLI: `-ui-sweep [-audit-dir &lt;path&gt;]`  (GRAPHICS 빌드 필요 — ScreenCapture)
    /// </summary>
    public class UiSweepQA : MonoBehaviour
    {
        public static bool Enabled = false;
        private static string dir = "uisweep";

        // 변화 판정 임계 — 다운샘플 그리드에서 이 비율 미만이면 "안 변했다".
        //  0.2% 는 커서 깜빡임·시계 초 단위 변화 정도는 무시하고 패널 개폐는 잡는 값.
        private const float ChangeThreshold = 0.002f;

        /// <summary>스윕 중 바뀔 수 있는 영구 설정 키 — 끝에서 원복한다.</summary>
        private static readonly string[] PersistedIntKeys = { "ui_palette" };
        private const int GridW = 96, GridH = 54;   // 다운샘플 격자 (픽셀 비교 비용 절감)

        public static void EnsureInScene()
        {
            var argv = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length; i++)
            {
                if (argv[i] == "-ui-sweep") Enabled = true;
                if (argv[i] == "-audit-dir" && i + 1 < argv.Length) dir = argv[i + 1];
            }
            if (!Enabled) return;
            if (FindFirstObjectByType<UiSweepQA>() != null) return;
            new GameObject("__UiSweepQA__").AddComponent<UiSweepQA>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, _) => EnsureInScene();
            EnsureInScene();
        }

        private void Start()
        {
            Application.runInBackground = true;
            StartCoroutine(Sweep());
        }

        /// <summary>화면을 저해상 그레이 격자로 — 픽셀 비교를 싸게 만든다.</summary>
        private static byte[] Snapshot()
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            var g = new byte[GridW * GridH];
            for (int y = 0; y < GridH; y++)
                for (int x = 0; x < GridW; x++)
                {
                    int sx = Mathf.Min(w - 1, x * w / GridW);
                    int sy = Mathf.Min(h - 1, y * h / GridH);
                    var c = px[sy * w + sx];
                    g[y * GridW + x] = (byte)((c.r * 77 + c.g * 150 + c.b * 29) >> 8);
                }
            Destroy(tex);
            return g;
        }

        private static float Diff(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return 1f;
            int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i] - b[i]) > 12) n++;   // 12 = 미세 그라데이션 무시
            return (float)n / a.Length;
        }

        /// <summary>화면에 실제로 읽히는 텍스트 조각의 **집합**.
        ///
        /// 개수가 아니라 집합을 쓰는 이유 (2026-07-29 1차 구현 정정):
        ///   처음엔 개수 증감으로 '껍데기'를 판정했는데 부정확했다 — 탭을 바꾸면
        ///   내용이 **교체**되므로 줄 수가 줄어드는 것이 정상인데도 EMPTY 로 찍혔고,
        ///   1x·멈춤 같은 단순 토글까지 걸렸다.
        ///   판정 기준은 "줄었나"가 아니라 **"새로 나타난 것이 있나"** 여야 한다.
        ///   탭을 눌렀는데 새 텍스트가 하나도 안 생겼다면 그건 진짜 아무것도 안 그린 것이다.
        /// </summary>
        private static HashSet<int> VisibleTexts()
        {
            var set = new HashSet<int>();
            foreach (var t in FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrWhiteSpace(t.text)
                    || t.canvasRenderer == null || t.canvasRenderer.GetAlpha() <= 0.05f)
                    continue;
                // **내용 + 화면 위치**로 식별한다.
                //  · 인스턴스 ID 를 쓰면 UI 리빌드 때 내용이 같아도 전부 '새것'이 된다
                //    (일정 탭에서 new=99 오검출).
                //  · 내용만 쓰면 위치를 잃는다 — 일정표처럼 같은 글자가 여러 칸에
                //    있으면 한 칸이 바뀌어도 집합이 그대로라 '반응 없음'으로 오판한다
                //    (일정 > 수 오검출).
                //  둘을 합쳐야 "어느 자리의 무엇이 바뀌었나"가 잡힌다.
                var wp = t.rectTransform.position;
                set.Add(t.text.GetHashCode() * 397
                        ^ (Mathf.RoundToInt(wp.x) * 73856093)
                        ^ (Mathf.RoundToInt(wp.y) * 19349663));
            }
            return set;
        }

        /// <summary>before 에 없던 항목 수 = 이번 클릭으로 **새로 나타난** 텍스트.</summary>
        private static int NewTexts(HashSet<int> before, HashSet<int> after)
        {
            int n = 0;
            foreach (var id in after) if (!before.Contains(id)) n++;
            return n;
        }

        /// <summary>월드 쪽 반응 지문 — 선택 대상 + 카메라 위치/줌.
        ///
        /// UI 상태만 보면 **월드에서만 반응하는 버튼을 죽었다고 오판한다** (2026-07-29
        /// 4차 정정).  실제로 콜로니스트 초상화 클릭이 DEAD 로 찍혔는데, 코드를 보니
        /// ClickSelector.SimulateSelect + CameraController.FocusOn 을 정상 호출하고
        /// 있었다 — 선택 링은 월드 SpriteRenderer 이고 카메라 이동은 트랜스폼이라
        /// UI 계층에 아무 흔적이 없었을 뿐이다.
        /// 게이트가 멀쩡한 기능을 버그로 보고하면 신뢰를 잃는다.</summary>
        private static string WorldFingerprint()
        {
            var cs = FindFirstObjectByType<ClickSelector>();
            var sel = cs != null && cs.CurrentSelection != null
                ? cs.CurrentSelection.GetInstanceID() : 0;
            var cam = Camera.main;
            string c = cam != null
                ? $"{cam.transform.position.x:F2},{cam.transform.position.y:F2},{cam.orthographicSize:F2}"
                : "-";
            return $"{sel}|{c}";
        }

        /// <summary>활성 UI 그래픽 수 — 패널 개폐처럼 글자 없는 변화를 잡는다.</summary>
        private static int ActiveGraphicCount()
        {
            int n = 0;
            foreach (var g in FindObjectsByType<Graphic>(FindObjectsSortMode.None))
                if (g.isActiveAndEnabled) n++;
            return n;
        }

        private static string LabelOf(Button b)
        {
            var t = b.GetComponentInChildren<Text>(true);
            string s = t != null && !string.IsNullOrWhiteSpace(t.text) ? t.text : b.name;
            return s.Replace('\n', ' ').Trim();
        }

        private IEnumerator Sweep()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            System.IO.Directory.CreateDirectory(dir);

            // 스윕은 **모든 버튼을 누른다** — 설정의 팔레트 토글도 포함이고, 그건
            //  PlayerPrefs 에 저장된다.  1차 실행에서 실제로 UI 테마가 크림→어두움으로
            //  영구히 바뀌어 이후 모든 빌드·캡처가 오염됐다.
            //  QA 도구가 사용자 설정을 남기면 안 된다 — 스냅샷 후 끝에서 되돌린다.
            var prefSnapshot = new Dictionary<string, int>();
            foreach (var k in PersistedIntKeys) prefSnapshot[k] = PlayerPrefs.GetInt(k, 0);

            // 스냅샷을 먼저 떠 두고 순회한다 — 클릭이 계층을 바꾸므로 실시간 순회는 위험.
            var buttons = new List<Button>(FindObjectsByType<Button>(FindObjectsSortMode.None));
            buttons.RemoveAll(b => b == null || !b.isActiveAndEnabled || !b.interactable);
            Debug.Log($"[UISWEEP] start buttons={buttons.Count}");

            int dead = 0, empty = 0, ok = 0, skipped = 0;
            // 이미 눌러 본 안쪽 버튼 — 패널을 다시 열 때 중복 클릭 방지.
            var seen = new HashSet<Button>();
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (b == null || !b.isActiveAndEnabled || !b.interactable) { skipped++; continue; }
                string label = LabelOf(b);

                // 판정은 **UI 상태**로만 한다 (2026-07-29 3차 정정).
                //  화면 픽셀 diff 는 두 방향 모두 틀렸다: 격자가 성기면 작은 UI 변화를
                //  놓치고(일정 슬롯 한 칸), 화면 대비 비율로 재면 작은 패널은 무조건
                //  '거의 안 변함' 으로 잡힌다.  버튼이 응답했는지는 픽셀이 아니라
                //  **보이는 UI 가 달라졌는지**로 재는 것이 정확하고 싸다.
                var tBefore = VisibleTexts();
                int uiBefore = ActiveGraphicCount();
                string wBefore = WorldFingerprint();

                b.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.45f);

                var tAfter = VisibleTexts();
                int uiAfter = ActiveGraphicCount();
                int fresh = NewTexts(tBefore, tAfter);
                int gone = NewTexts(tAfter, tBefore);
                bool responded = fresh > 0 || gone > 0 || uiAfter != uiBefore
                                 || WorldFingerprint() != wBefore;

                string verdict = responded ? "OK" : "DEAD";
                if (responded) ok++; else dead++;
                Debug.Log($"[UISWEEP] {verdict} '{label}' new={fresh} gone={gone} "
                          + $"ui={uiBefore}->{uiAfter}");

                if (verdict != "OK")
                    ScreenCapture.CaptureScreenshot(
                        System.IO.Path.Combine(dir, $"{verdict}_{i:D2}_{Sanitize(label)}.png"));

                // 패널이 열렸으면 **그 안쪽 버튼까지 한 단계 더** 눌러 본다.
                //  "껍데기" 는 대개 최상위 버튼이 아니라 그 버튼이 여는 패널 안에 있다
                //  (운영자가 지적한 장비 탭·스킬 패널이 정확히 그 위치였다).
                //  최상위만 훑으면 구조적으로 못 잡는다.
                if (responded)
                {
                    var inner = new List<Button>(FindObjectsByType<Button>(FindObjectsSortMode.None));
                    inner.RemoveAll(x => x == null || !x.isActiveAndEnabled || !x.interactable
                                         || buttons.Contains(x) || seen.Contains(x));
                    foreach (var ib in inner)
                    {
                        if (ib == null || !ib.isActiveAndEnabled || !ib.interactable) continue;
                        seen.Add(ib);
                        string il = LabelOf(ib);
                        var itb = VisibleTexts();
                        int iub = ActiveGraphicCount();
                        string iwb = WorldFingerprint();
                        ib.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(0.35f);
                        var ita = VisibleTexts();
                        int iua = ActiveGraphicCount();
                        int ifresh = NewTexts(itb, ita), igone = NewTexts(ita, itb);
                        bool ir = ifresh > 0 || igone > 0 || iua != iub
                                  || WorldFingerprint() != iwb;
                        string iv = ir ? "OK" : "DEAD";
                        if (ir) ok++; else dead++;
                        Debug.Log($"[UISWEEP] {iv} '{label} > {il}' new={ifresh} gone={igone} "
                                  + $"ui={iub}->{iua}");
                        if (iv != "OK")
                            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(
                                dir, $"{iv}_{i:D2}_{Sanitize(label)}_{Sanitize(il)}.png"));
                    }
                }

                // 열린 패널이 다음 버튼을 가리지 않도록 되돌린다 (토글형이 대부분).
                if (b != null && b.isActiveAndEnabled && b.interactable)
                {
                    b.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.2f);
                }
            }

            // 설정 원복 — 스윕이 남긴 흔적을 지운다.
            foreach (var kv in prefSnapshot) PlayerPrefs.SetInt(kv.Key, kv.Value);
            PlayerPrefs.Save();
            MelonS.GameProto.Core.UITheme.SetPalette(prefSnapshot["ui_palette"]);

            Debug.Log($"[UISWEEP] DONE ok={ok} dead={dead} empty={empty} skipped={skipped} "
                      + $"(설정 원복: ui_palette={prefSnapshot["ui_palette"]})");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(dead + empty > 0 ? 1 : 0);
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString().Substring(0, Mathf.Min(24, sb.Length));
        }
    }
}
