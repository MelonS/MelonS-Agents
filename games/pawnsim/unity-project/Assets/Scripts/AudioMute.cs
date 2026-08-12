using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>`-mute` 인자(또는 `MELONS_MUTE=1`)로 게임 소리를 끈다.
    ///
    /// 계기 (2026-08-08 운영자): *"7개가 켜지니깐 너무 시끄러운데 방법 없냐?"*
    ///  검증·촬영 스윕은 같은 빌드를 **여러 개 동시에** 띄운다(프레임 시점이 달라
    ///  순차로 돌리면 몇 배가 걸린다).  그러면 인스턴스마다 BGM·SFX 가 겹쳐 울린다.
    ///  Unity 스탠드얼론 플레이어에는 음소거 CLI 인자가 없어서 게임이 직접 받는다.
    ///
    /// `BeforeSceneLoad` 에 건다 — 씬이 열리면서 첫 소리가 나기 전에 꺼야 한다.
    /// </summary>
    public static class AudioMute
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            bool mute = System.Environment.GetEnvironmentVariable("MELONS_MUTE") == "1";
            if (!mute)
                foreach (var a in System.Environment.GetCommandLineArgs())
                    if (a == "-mute") { mute = true; break; }
            if (!mute) return;

            AudioListener.volume = 0f;
            Debug.Log("[Audio] -mute — 소리 끔");
        }
    }
}
