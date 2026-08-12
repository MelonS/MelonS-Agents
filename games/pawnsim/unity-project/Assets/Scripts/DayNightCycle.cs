using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 9: camera background color follows time-of-day.
    /// Polls GameClock.Instance.DayProgress every frame.
    ///
    /// Color stops:
    ///   00:00 deep night    (0.08, 0.10, 0.18)
    ///   04:00 deep night    (0.08, 0.10, 0.18)
    ///   05:30 dawn purple   (0.30, 0.20, 0.32)
    ///   06:30 sunrise warm  (0.55, 0.42, 0.32)
    ///   08:00 morning       (0.42, 0.55, 0.40)
    ///   12:00 noon bright   (0.55, 0.65, 0.50)
    ///   17:00 afternoon     (0.50, 0.55, 0.40)
    ///   18:30 sunset orange (0.55, 0.38, 0.25)
    ///   20:00 dusk purple   (0.25, 0.20, 0.30)
    ///   22:00 night         (0.10, 0.12, 0.20)
    ///   24:00 deep night    (0.08, 0.10, 0.18)
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        private struct Stop { public float t; public Color c; public Stop(float t, Color c){ this.t=t; this.c=c; } }

        private static readonly Stop[] STOPS = new Stop[]
        {
            new Stop(0.00f, new Color(0.08f, 0.10f, 0.18f)),
            new Stop(0.17f, new Color(0.08f, 0.10f, 0.18f)),  // 04:00
            new Stop(0.23f, new Color(0.30f, 0.20f, 0.32f)),  // 05:30
            new Stop(0.27f, new Color(0.55f, 0.42f, 0.32f)),  // 06:30
            new Stop(0.33f, new Color(0.42f, 0.55f, 0.40f)),  // 08:00
            new Stop(0.50f, new Color(0.55f, 0.65f, 0.50f)),  // 12:00 noon
            new Stop(0.71f, new Color(0.50f, 0.55f, 0.40f)),  // 17:00
            new Stop(0.77f, new Color(0.55f, 0.38f, 0.25f)),  // 18:30
            new Stop(0.83f, new Color(0.25f, 0.20f, 0.30f)),  // 20:00
            new Stop(0.92f, new Color(0.10f, 0.12f, 0.20f)),  // 22:00
            new Stop(1.00f, new Color(0.08f, 0.10f, 0.18f)),
        };

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
        }

        private void Update()
        {
            if (cam == null) return;
            if (GameClock.Instance == null) return;
            float t = GameClock.Instance.DayProgress;
            Color c = SampleStops(t);
            // Day 22: weather multiplier (storm darkens scene)
            if (WeatherController.Instance != null)
            {
                float m = WeatherController.Instance.ColorMultiplier();
                c = new Color(c.r * m, c.g * m, c.b * m, c.a);
            }
            cam.backgroundColor = c;
        }

        private static Color SampleStops(float t)
        {
            t = Mathf.Repeat(t, 1f);
            for (int i = 0; i < STOPS.Length - 1; i++)
            {
                if (t >= STOPS[i].t && t <= STOPS[i + 1].t)
                {
                    float local = Mathf.InverseLerp(STOPS[i].t, STOPS[i + 1].t, t);
                    return Color.Lerp(STOPS[i].c, STOPS[i + 1].c, local);
                }
            }
            return STOPS[0].c;
        }
    }
}
