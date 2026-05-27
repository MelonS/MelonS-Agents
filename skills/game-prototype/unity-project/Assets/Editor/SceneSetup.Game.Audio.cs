using UnityEngine;
using UnityEditor;
using System.IO;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10k - SceneSetup.cs AudioBank wiring 블록 extract.
    //   원본 SceneSetup.cs L134-160 (27 LOC).
    public static partial class SceneSetup
    {
        private static void GenerateAudioBank()
        {
            GameObject audioGo = new GameObject("AudioBank");
            AudioBank audioBank = audioGo.AddComponent<AudioBank>();
            AudioClip chopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/chop.wav");
            AudioClip selClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/select.wav");
            // Day 33: ambient BGM (30s loop)
            if (File.Exists("Assets/Audio/bgm_ambient.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/bgm_ambient.wav", ImportAssetOptions.ForceUpdate);
            AudioClip bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bgm_ambient.wav");
            // Day 80: 새 SFX
            if (File.Exists("Assets/Audio/hit.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/hit.wav", ImportAssetOptions.ForceUpdate);
            if (File.Exists("Assets/Audio/harvest.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/harvest.wav", ImportAssetOptions.ForceUpdate);
            if (File.Exists("Assets/Audio/wolf_howl.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/wolf_howl.wav", ImportAssetOptions.ForceUpdate);
            AudioClip hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/hit.wav");
            AudioClip harvestClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/harvest.wav");
            AudioClip howlClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/wolf_howl.wav");
            SerializedObject abSo = new SerializedObject(audioBank);
            if (chopClip != null) abSo.FindProperty("sfxChop").objectReferenceValue = chopClip;
            if (selClip != null) abSo.FindProperty("sfxSelect").objectReferenceValue = selClip;
            if (bgmClip != null) abSo.FindProperty("bgm").objectReferenceValue = bgmClip;
            if (hitClip != null) abSo.FindProperty("sfxHit").objectReferenceValue = hitClip;
            if (harvestClip != null) abSo.FindProperty("sfxHarvest").objectReferenceValue = harvestClip;
            if (howlClip != null) abSo.FindProperty("sfxWolfHowl").objectReferenceValue = howlClip;
            abSo.ApplyModifiedProperties();
        }
    }
}
