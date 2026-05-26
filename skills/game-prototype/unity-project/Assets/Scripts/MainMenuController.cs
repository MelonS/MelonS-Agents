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
            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(OnStartClicked);
            }
            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(OnQuitClicked);
            }
        }

        private void OnStartClicked()
        {
            SceneManager.LoadScene(gameSceneName);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
