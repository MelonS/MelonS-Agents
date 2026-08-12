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
        [SerializeField] private Button optionsButton;   // 설정 통합 (SettingsMenu 열기)
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
            // #217 - 실제 버튼 클릭 경로를 그대로 재현(persistent+runtime 리스너 모두 발동)해야
            //  운영자가 겪는 2중 로드 같은 버그를 QA 가 잡는다.  OnStartClicked 직접 호출은
            //  단일 발동이라 그 버그를 가렸었다.
            if (startButton != null) startButton.onClick.Invoke();
            else OnStartClicked();
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
            if (optionsButton != null)
            {
                optionsButton.onClick.RemoveListener(OnOptionsClicked);
                optionsButton.onClick.AddListener(OnOptionsClicked);
            }
        }

        // #217 운영자 fb (실제 로그 증거): "OnStartClicked — loading scene: Game" 가 2번
        //  찍힘 = Game 씬 2중 로드.  원인: 시작 버튼에 (a) 씬에 baked 된 persistent
        //  UnityEvent 리스너 + (b) WireListeners 가 매번 Add 하는 runtime 리스너 가 둘 다
        //  걸려 클릭 1회에 OnStartClicked 가 2회 발동.  2중 로드는 GameManager/GameClock/
        //  ResourceManager 싱글톤을 중복 생성 → dedup 자가파괴로 GameClock.Instance=null →
        //  ClockUI 가 06:00 에 영구 고정 + HUD 가 빈 ResourceManager 를 읽어 목재 0 (운반은
        //  되는데 카운터/시계가 죽음).  -autostart 경로는 OnStartClicked 1회라 멀쩡했다.
        //  FIX: 로드 1회 가드 (몇 개의 리스너가 발동하든 씬은 한 번만 로드).
        private bool _loading;

        // public so UnityEventTools can target it as a persistent listener
        public void OnStartClicked()
        {
            if (_loading) { Debug.Log("[MainMenu] OnStartClicked 중복 무시 (이미 로딩 중)"); return; }
            _loading = true;
            Debug.Log("[MainMenu] OnStartClicked — loading scene: " + gameSceneName);
            SceneManager.LoadScene(gameSceneName);
        }

        // public so UnityEventTools can bake it as a persistent listener
        public void OnOptionsClicked()
        {
            Debug.Log("[MainMenu] OnOptionsClicked — opening SettingsMenu");
            SettingsMenu.Open();
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
