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
            // W-M2-01 Lane A: M2 SFX slots (wiki Dim2 #1/#2/#4)
            if (File.Exists("Assets/Audio/build.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/build.wav", ImportAssetOptions.ForceUpdate);
            if (File.Exists("Assets/Audio/alert.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/alert.wav", ImportAssetOptions.ForceUpdate);
            if (File.Exists("Assets/Audio/ambient.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/ambient.wav", ImportAssetOptions.ForceUpdate);
            AudioClip buildClip   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/build.wav");
            AudioClip alertClip   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/alert.wav");
            AudioClip ambientClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/ambient.wav");
            // W-M3-01 Lane D: M3 SFX slot (wiki Dim2 #7 — pick-on-stone, not chop thunk)
            if (File.Exists("Assets/Audio/mine.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/mine.wav", ImportAssetOptions.ForceUpdate);
            AudioClip mineClip    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/mine.wav");
            // W-M3-02 Lane D: M3 SFX slot (wiki Dim2 #9 — rain loop bed for storm weather)
            if (File.Exists("Assets/Audio/rain.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/rain.wav", ImportAssetOptions.ForceUpdate);
            AudioClip rainClip    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/rain.wav");
            // W-M3-03 Lane A: M3 SFX slot (wiki Dim2 #8 — danger/tension music during raid)
            if (File.Exists("Assets/Audio/danger.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/danger.wav", ImportAssetOptions.ForceUpdate);
            AudioClip dangerClip  = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/danger.wav");
            SerializedObject abSo = new SerializedObject(audioBank);
            if (chopClip != null)    abSo.FindProperty("sfxChop").objectReferenceValue    = chopClip;
            if (selClip != null)     abSo.FindProperty("sfxSelect").objectReferenceValue  = selClip;
            if (bgmClip != null)     abSo.FindProperty("bgm").objectReferenceValue        = bgmClip;
            if (hitClip != null)     abSo.FindProperty("sfxHit").objectReferenceValue     = hitClip;
            if (harvestClip != null) abSo.FindProperty("sfxHarvest").objectReferenceValue = harvestClip;
            if (howlClip != null)    abSo.FindProperty("sfxWolfHowl").objectReferenceValue = howlClip;
            // M2 new slots — wiki #1 build, #2 alert, #4 ambient
            if (buildClip != null)   abSo.FindProperty("sfxBuild").objectReferenceValue   = buildClip;
            if (alertClip != null)   abSo.FindProperty("sfxAlert").objectReferenceValue   = alertClip;
            if (ambientClip != null) abSo.FindProperty("sfxAmbient").objectReferenceValue = ambientClip;
            // M3 new slot — wiki #7 mine pick-on-stone
            if (mineClip != null)    abSo.FindProperty("sfxMine").objectReferenceValue    = mineClip;
            // M3 new slot — wiki #9 rain loop (W-M3-02 Lane D)
            if (rainClip != null)    abSo.FindProperty("rainLoop").objectReferenceValue   = rainClip;
            // M3 new slot — wiki #8 danger music (W-M3-03 Lane A)
            if (dangerClip != null)  abSo.FindProperty("dangerBgm").objectReferenceValue  = dangerClip;
            abSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
