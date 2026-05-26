using UnityEngine;

namespace MelonS.GameProto
{
    public class InputController : MonoBehaviour
    {
        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                GameManager.Instance.TryMove(0);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                GameManager.Instance.TryMove(1);
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                GameManager.Instance.TryMove(2);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                GameManager.Instance.TryMove(3);
        }
    }
}
