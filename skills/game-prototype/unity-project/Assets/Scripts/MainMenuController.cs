using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Main menu screen — wires up Start button to load the Game scene.
    /// Attached to a root GameObject in MainMenu.unity that holds the
    /// Canvas + buttons.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private string gameSceneName = "Game";

        private void Awake()
        {
            Debug.Log("[MainMenu] Awake — wiring runtime listeners");
            WireListeners();
        }

        private void Start()
        {
            // Defensive — re-wire in case Awake order issues lost the listener
            WireListeners();

            // 운영자 fb (#137 v4): QA 가 MainMenu → Game scene 흐름 자동화.
            //  -autostart CLI arg 또는 -delay arg (AutoScreenshotter 가 쓰는) 보이면
            //  자동으로 Start Game 클릭 시뮬.
            string[] args = System.Environment.GetCommandLineArgs();
            bool autostart = false;
            bool menushot = false;
            foreach (var a in args)
            {
                if (a == "-autostart" || a == "-delay" || a == "-batchmode")
                    autostart = true;
                // 운영자 fb 2026-05-30 (BUG1 검증): -menushot 이면 MainMenu 에 머물러
                //  메뉴 화면을 캡처할 수 있게 autostart 억제.  AutoScreenshotter(메뉴 scene)
                //  가 캡처 후 quit 한다.
                if (a == "-menushot") menushot = true;
            }
            if (menushot)
            {
                Debug.Log("[MainMenu] -menushot detected, suppressing autostart (stay on menu for capture)");
                autostart = false;
            }
            if (autostart)
            {
                Debug.Log("[MainMenu] -autostart detected, auto-loading Game scene in 0.5s");
                StartCoroutine(AutoStartCoroutine());
            }
        }

        private System.Collections.IEnumerator AutoStartCoroutine()
        {
            yield return new WaitForSeconds(0.5f);
            OnStartClicked();
        }

        private void WireListeners()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartClicked);
                startButton.onClick.AddListener(OnStartClicked);
            }
            else Debug.LogWarning("[MainMenu] startButton not assigned");
            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitClicked);
                quitButton.onClick.AddListener(OnQuitClicked);
            }
        }

        // public so UnityEventTools can target it as a persistent listener
        public void OnStartClicked()
        {
            Debug.Log("[MainMenu] OnStartClicked — loading scene: " + gameSceneName);
            SceneManager.LoadScene(gameSceneName);
        }

        public void OnQuitClicked()
        {
            Debug.Log("[MainMenu] OnQuitClicked");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
