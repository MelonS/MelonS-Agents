using System;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 8 polish: 1x / 2x / 4x time scale + Pause (Spacebar).
    /// 1/2/3 keys = 1x/2x/4x.  Space = toggle pause.
    /// </summary>
    public class TimeController : MonoBehaviour
    {
        public static TimeController Instance { get; private set; }

        private float lastNonPauseScale = 1f;
        public bool IsPaused => Time.timeScale == 0f;
        public float CurrentScale => Time.timeScale;
        public event Action<float> OnScaleChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Time.timeScale = 1f;
            lastNonPauseScale = 1f;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetScale(1f);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SetScale(2f);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SetScale(4f);
            else if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
        }

        public void SetScale(float s)
        {
            Time.timeScale = Mathf.Max(0f, s);
            if (s > 0f) lastNonPauseScale = s;
            OnScaleChanged?.Invoke(s);
        }

        public void TogglePause()
        {
            if (IsPaused) SetScale(lastNonPauseScale);
            else SetScale(0f);
        }
    }
}
