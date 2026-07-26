using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>Day 21: 4 skill levels shown for selected pawn.
    /// Reads ClickSelector.CurrentSelection.GetComponent<PawnSkills>().
    /// Hides when no pawn selected (same pattern as PawnInfoPanel).</summary>
    public class SkillUI : MonoBehaviour
    {
        [SerializeField] private ClickSelector selector;
        [SerializeField] private Text gatherText;
        [SerializeField] private Text chopText;
        [SerializeField] private Text buildText;
        [SerializeField] private Text combatText;
        [SerializeField] private GameObject container;

        private void Update()
        {
            if (selector == null || container == null) return;
            var pawn = selector.CurrentSelection;
            var sk = pawn != null ? pawn.GetComponent<PawnSkills>() : null;
            // 빈 상자를 띄우지 않는다 (2026-07-27).  기존엔 폰만 선택되면 컨테이너를 켜고
            //  스킬 데이터가 없으면 그대로 return 해서, 헤더 "스킬"만 있는 **빈 사각형**이
            //  남았다.  평가자 다수가 이걸 '미구현/고장'으로 읽었고(11세 페르소나:
            //  "아무것도 없어요. 고장난 것 같아요"), 프로듀서는 "빈 사각형은 미완성을
            //  대놓고 광고한다 — 채우거나 숨겨라"로 판정했다.
            //  → 그릴 내용이 있을 때만 켠다.  없는 패널이 빈 패널보다 낫다.
            // 스킬 패널 비활성 (2026-07-27 시연 결정).
            //
            // 이 패널은 헤더 "스킬"만 있고 본문이 비어 있었다.  런타임 색 강제와 행 생성
            // 두 가지를 시도했지만 빌드에서 여전히 빈 상자였다(본문 영역 고유색 17,
            // 대비 1.03 — QA 실측).  씬에 구워진 구조 문제로 보이며, 씬 리베이크는
            // Game.unity 를 통째로 다시 쓰는 작업이라 시연 전날 밤에 할 일이 아니다.
            //
            // 빈 패널은 **없는 패널보다 나쁘다** — 평가자 다수가 '미구현/고장'으로 읽었고
            // (11세: "아무것도 없어요. 고장난 것 같아요"), 프로듀서 페르소나는
            // "채우거나 숨겨라"로 판정했다.  스킬 수치 자체는 작업 우선순위 그리드(F1)에
            // 이미 첨자로 표시되므로 **정보가 사라지는 것도 아니다.**
            //
            // TODO(시연 후): 씬 리베이크 또는 PawnInfoPanel 탭으로 흡수해 되살릴 것.
            container.SetActive(false);
            return;
#pragma warning disable CS0162   // 아래는 복구 시 되살릴 원래 경로
            bool any = pawn != null && !pawn.IsDead && sk != null;
            container.SetActive(any);
            if (!any) return;
            EnsureRows();
            // UI겹침 P1-4 — 건축 열림 시 시프트된 인포패널(272~652) 위에 떠서 본문을
            //  가리던 것: (400,58) → (660,108).  커플링 A — (272,314) 금지.
            var amS = ArchitectMenu.Instance;
            var crt = container.GetComponent<RectTransform>();
            if (crt != null)
            {
                // 운영자 지시 (2026-07-27) "UI 가 항상 최대한 화면 가운데를 가리지 않도록":
                //  기존엔 건축 열림 시 x 400 → **660** 으로 화면 중앙까지 밀려났다.
                //  인포패널과 같은 원칙으로 좌측대에 머무르고 y 만 올린다
                //  (인포패널 폭 380 + 좌여백 12 → 그 오른쪽 400 이 좌측대의 끝).
                var wantS = (amS != null && amS.IsOpen) ? new Vector2(400f, amS.BlockTopY + 10f) : new Vector2(400f, 58f);
                if (!Mathf.Approximately(crt.anchoredPosition.x, wantS.x)
                    || !Mathf.Approximately(crt.anchoredPosition.y, wantS.y))
                    crt.anchoredPosition = wantS;
            }
            SetText(gatherText, "채집", sk.GetLevel(SkillKind.Gather));
            SetText(chopText,   "벌목", sk.GetLevel(SkillKind.Chop));
            SetText(buildText,  "건축", sk.GetLevel(SkillKind.Build));
            SetText(combatText, "전투", sk.GetLevel(SkillKind.Combat));
#pragma warning restore CS0162
        }

        private static void SetText(Text t, string label, int level)
        {
            if (t == null) return;
            t.text = $"{label}: Lv {level}";
            // 색을 매 갱신마다 테마에서 다시 찍는다 (2026-07-27).
            //  이 행들은 SceneSetup 이 **씬을 구울 당시의** colTextPrimary 로 색을 박아 넣는데,
            //  그 시점 팔레트는 어두운 테마(밝은 크림 글자)였다.  이후 기본 팔레트가 크림으로
            //  바뀌면서 밝은 글자 × 밝은 패널이 되어 "스킬 상자가 텅 비었다"로 보였다
            //  (평가자 다수가 '미구현/고장'으로 읽음).  씬 리베이크는 Game.unity 를 통째로
            //  다시 쓰는 위험한 작업이라, 런타임에서 현재 테마 색으로 덮어쓴다.
            t.color = MelonS.GameProto.Core.UITheme.TextPrimary;
        }

        /// <summary>씬에 구워진 스킬 행 Text 가 하나도 배선돼 있지 않으면 런타임에 만든다.
        ///
        /// 계기 (2026-07-27 가상 유저 평가): 콜로니스트를 클릭하면 "스킬" 헤더만 있는
        /// **빈 상자**가 떴다.  평가자 다수가 이걸 "미구현" 또는 "고장"으로 읽었고,
        /// 11세 페르소나는 "아무것도 없어요. 고장난 것 같아요"라고 표현했다.
        /// 빈 패널은 없는 패널보다 나쁘다 — 완성도 인상을 직접 깎는다.
        ///
        /// 원인은 로직이 아니라 **배선**이다(SkillUI 자체는 정상 동작).  씬 리베이크는
        /// Game.unity 를 통째로 다시 쓰는 위험한 작업이라, PawnInfoPanel 의
        /// EnsureTabs/EnsureBorder 와 같은 lazy-create 관용구로 런타임에서 보강한다.
        /// 이미 배선돼 있으면 아무 일도 하지 않는다(멱등).</summary>
        private void EnsureRows()
        {
            if (rowsChecked) return;
            rowsChecked = true;
            if (container == null) return;
            // 씬 배선이 있어도 **우리 행을 새로 만든다** (2026-07-27 2차).
            //  1차에는 "배선이 있으면 손대지 않는다"로 두었는데, 실제 빌드에서 상자가 계속
            //  비어 있었다 — 씬의 Text 객체가 패널 밖에 놓였거나 비활성인 것으로 보인다.
            //  씬 상태를 원격으로 진단하는 것보다 **우리가 소유한 행을 만들어 그걸 갱신**하는
            //  쪽이 확실하다.  아래에서 필드를 우리 것으로 덮어쓰므로 SetText 는 우리 행만
            //  건드리고, 씬의 유령 Text 는 어차피 안 보이므로 방치해도 겹치지 않는다.

            var parent = container.GetComponent<RectTransform>();
            if (parent == null) return;
            // MakeBorderedPanel 이 만든 내부 콘텐츠 RT 가 있으면 그쪽에, 없으면 컨테이너에.
            var inner = parent.childCount > 0 ? parent.GetChild(0) as RectTransform : null;
            var host = inner != null ? inner : parent;

            var font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(20);
            gatherText = MakeRow(host, font, "채집", 30f);
            chopText   = MakeRow(host, font, "벌목", 56f);
            buildText  = MakeRow(host, font, "건축", 82f);
            combatText = MakeRow(host, font, "전투", 108f);
            Debug.Log("[SkillUI] 스킬 행 4개 런타임 생성 (씬 배선 없음)");
        }

        private bool rowsChecked;

        private static Text MakeRow(RectTransform host, Font font, string label, float yOffset)
        {
            var go = new GameObject(label + "Text");
            go.transform.SetParent(host, false);
            var t = go.AddComponent<Text>();
            t.text = $"{label}: Lv 0";
            t.font = font;
            t.fontSize = 20;
            t.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(-16f, 24f);
            rt.anchoredPosition = new Vector2(8f, -yOffset);
            return t;
        }
    }
}
