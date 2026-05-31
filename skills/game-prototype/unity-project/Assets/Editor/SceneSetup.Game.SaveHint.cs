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
            // #245 운영자 fb "왼쪽 밑에 s l 버튼 제거" — 좌하단 Save/Load 버튼 패널을 만들지
            //  않는다.  저장/불러오기는 설정 메뉴(ESC) / F5·F9 핫키로 계속 가능.
            return;
#pragma warning disable CS0162
            GameObject saveBtnPanel = new GameObject("SaveLoadButtons");
            saveBtnPanel.transform.SetParent(canvasGo.transform, false);
            RectTransform sbpRt = saveBtnPanel.AddComponent<RectTransform>();
            sbpRt.anchorMin = new Vector2(0f, 0f);
            sbpRt.anchorMax = new Vector2(0f, 0f);
            sbpRt.pivot = new Vector2(0f, 0f);
            sbpRt.sizeDelta = new Vector2(92, 40);
            sbpRt.anchoredPosition = new Vector2(12, 12);

            GameObject saveBtnGo = CreateIconButton(saveBtnPanel.transform, "SaveBtn", "S", new Vector2(0, 0), colPanel, colTextPrimary, uiFont);
            GameObject loadBtnGo = CreateIconButton(saveBtnPanel.transform, "LoadBtn", "L", new Vector2(48, 0), colPanel, colTextPrimary, uiFont);

            GameSaveButtons gsb = saveBtnPanel.AddComponent<GameSaveButtons>();
            SerializedObject gsbSo = new SerializedObject(gsb);
            gsbSo.FindProperty("saveButton").objectReferenceValue = saveBtnGo.GetComponent<Button>();
            gsbSo.FindProperty("loadButton").objectReferenceValue = loadBtnGo.GetComponent<Button>();
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
