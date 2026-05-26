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
