using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Listens to left-mouse clicks and selects PawnEntity instances
    /// under the cursor via 2D collider raycast.  Day 1 supports single
    /// selection only.  Click on empty ground = clear selection.
    /// </summary>
    public class ClickSelector : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;

        private PawnEntity currentSelection;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            Collider2D hit = Physics2D.OverlapPoint(mouseWorld);
            PawnEntity pawn = (hit != null) ? hit.GetComponent<PawnEntity>() : null;

            if (pawn != null)
            {
                Select(pawn);
            }
            else
            {
                ClearSelection();
            }
        }

        private void Select(PawnEntity pawn)
        {
            if (currentSelection == pawn) return;
            if (currentSelection != null) currentSelection.SetSelected(false);
            currentSelection = pawn;
            currentSelection.SetSelected(true);
        }

        private void ClearSelection()
        {
            if (currentSelection == null) return;
            currentSelection.SetSelected(false);
            currentSelection = null;
        }
    }
}
