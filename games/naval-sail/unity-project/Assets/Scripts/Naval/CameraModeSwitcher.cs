using UnityEngine;

namespace MelonS.GameProto.Naval
{
    /// <summary>
    /// Tab 키로 2.5D <-> 3D 카메라 리그를 토글한다. 배 위치는 그대로, 카메라만
    /// 바뀐다 — 씬 하나에서 두 시점을 직접 플레이해보고 어느 쪽이 맞는지
    /// 사용자가 판단하기 위한 장치다.
    /// </summary>
    public class CameraModeSwitcher : MonoBehaviour
    {
        public GameObject rig2Point5D;
        public GameObject rig3D;
        public KeyCode switchKey = KeyCode.Tab;

        private bool using3D;

        private void Start()
        {
            // 검증용 CLI 훅 — batchmode 스크린샷 검증 때 3D 리그로 시작해서
            // 두 리그 다 렌더가 되는지 확인한다 (실제 Tab 키 입력 경로와는 별개).
            foreach (string a in System.Environment.GetCommandLineArgs())
            {
                if (a == "-forcecam3d") using3D = true;
            }
            ApplyState();
        }

        private void Update()
        {
            if (Input.GetKeyDown(switchKey))
            {
                using3D = !using3D;
                ApplyState();
                Debug.Log($"[CameraModeSwitcher] mode -> {(using3D ? "3D" : "2.5D")}");
            }
        }

        private void ApplyState()
        {
            if (rig2Point5D != null)
            {
                rig2Point5D.SetActive(!using3D);
                var cam = rig2Point5D.GetComponent<Camera>();
                if (cam != null) cam.enabled = !using3D;
            }
            if (rig3D != null)
            {
                rig3D.SetActive(using3D);
                var cam = rig3D.GetComponent<Camera>();
                if (cam != null) cam.enabled = using3D;
            }
        }
    }
}
