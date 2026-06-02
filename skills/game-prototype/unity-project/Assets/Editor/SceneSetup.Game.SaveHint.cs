using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10f - SceneSetup.cs SaveLoad buttons (bottom-left) + ControlHint (bottom-center) extract.
    //   원본 SceneSetup.cs L630-668 (40 LOC).
    public static partial class SceneSetup
    {
        private static void GenerateSaveLoadButtons(
            GameObject canvasGo, Color colPanel, Color colTextPrimary, Font uiFont)
        {
            // #245 운영자 fb "왼쪽 밑에 s l 버튼 제거" — 좌하단 *시각* Save/Load 버튼은 안 만든다.
            // #44 회귀 수정: 과거엔 여기서 통째로 return 해 GameSaveButtons *호스트 자체* 가
            //  씬에서 사라졌고, 그 컴포넌트가 F5/F9 핫키 + 저장/복원 로직의 유일한 소유자라
            //  저장/불러오기가 완전히 죽어 있었다(설정 메뉴 행도 못 뜸).  → 시각 버튼 없이
            //  *로직 전용 호스트* 만 생성한다.  F5/F9(Update) 동작 + 설정 메뉴가 OnSave/OnLoad
            //  를 직접 호출(아래 SettingsMenu) + pawnPrefab/treeSprite 복원 ref 보존.
            GameObject saveHost = new GameObject("SaveLoadButtons");
            saveHost.transform.SetParent(canvasGo.transform, false);
            saveHost.AddComponent<RectTransform>();  // 캔버스 자식 표준(레이아웃엔 미관여)

            GameSaveButtons gsb = saveHost.AddComponent<GameSaveButtons>();
            SerializedObject gsbSo = new SerializedObject(gsb);
            // saveButton/loadButton 는 의도적으로 null(시각 버튼 없음) — Awake 의 onClick 배선은
            //  null-guard 가 있고, F5/F9 + 설정 메뉴 직접 호출 경로는 버튼이 없어도 동작한다.
            gsbSo.FindProperty("pawnPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PawnPrefabPath);
            gsbSo.FindProperty("treeSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/tree.png");
            gsbSo.ApplyModifiedProperties();
        }

        private static void GenerateControlHint(
            GameObject canvasGo, Color colTextMuted, Font uiFont)
        {
            GameObject hintGo = new GameObject("ControlHint");
            hintGo.transform.SetParent(canvasGo.transform, false);
            Text hintText = hintGo.AddComponent<Text>();
            hintText.text = "WASD/휠/123/Space · B:벽5 · F:바닥1 · G:문3 · T:화덕10 · 1일=4분(1x)";
            hintText.font = uiFont;
            hintText.fontSize = 18;
            Color hintCol = colTextMuted; hintCol.a = 0.75f;
            hintText.color = hintCol;
            hintText.alignment = TextAnchor.MiddleCenter;
            RectTransform hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(640, 20);
            hintRt.anchoredPosition = new Vector2(0, 14);
        }
    }
}
