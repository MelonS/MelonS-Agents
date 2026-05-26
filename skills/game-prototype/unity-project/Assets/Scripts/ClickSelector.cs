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
        public PawnEntity CurrentSelection => currentSelection;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
            // Left click = select
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;
                Collider2D hit = Physics2D.OverlapPoint(mouseWorld);
                PawnEntity pawn = (hit != null) ? hit.GetComponent<PawnEntity>() : null;
                if (pawn != null) Select(pawn); else ClearSelection();
            }

            // Day 48: R key toggles drafted on selected pawn
            if (Input.GetKeyDown(KeyCode.R) && currentSelection != null)
            {
                currentSelection.SetDrafted(!currentSelection.IsDrafted);
            }

            // Right click = move OR chop OR attack (drafted) for selected pawn
            if (Input.GetMouseButtonDown(1) && currentSelection != null)
            {
                Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;
                Collider2D rhit = Physics2D.OverlapPoint(mouseWorld);

                // Day 48: drafted pawn — right-click on enemy/animal = attack/hunt
                if (currentSelection.IsDrafted)
                {
                    if (rhit != null)
                    {
                        BanditEnemy bandit = rhit.GetComponent<BanditEnemy>();
                        AnimalEntity animal = rhit.GetComponent<AnimalEntity>();
                        WolfEnemy wolf = rhit.GetComponent<WolfEnemy>();
                        if (bandit != null)
                        {
                            currentSelection.DraftedAttackTarget = bandit;
                            currentSelection.DraftedHuntTarget   = null;
                            currentSelection.DraftedWolfTarget   = null;
                            Debug.Log($"[Draft] {currentSelection.PawnName} → 적 공격");
                            return;
                        }
                        if (wolf != null)
                        {
                            currentSelection.DraftedWolfTarget   = wolf;
                            currentSelection.DraftedAttackTarget = null;
                            currentSelection.DraftedHuntTarget   = null;
                            Debug.Log($"[Draft] {currentSelection.PawnName} → 늑대 공격");
                            return;
                        }
                        if (animal != null)
                        {
                            currentSelection.DraftedHuntTarget   = animal;
                            currentSelection.DraftedAttackTarget = null;
                            currentSelection.DraftedWolfTarget   = null;
                            Debug.Log($"[Draft] {currentSelection.PawnName} → 동물 사냥");
                            return;
                        }
                    }
                    // Otherwise: manual movement (no chop while drafted)
                    PawnMovement mvD = currentSelection.GetComponent<PawnMovement>();
                    if (mvD != null) mvD.SetTarget(new Vector2(mouseWorld.x, mouseWorld.y));
                    currentSelection.ManualMoveUntil = Time.time + 5f;
                    return;
                }

                // Non-drafted: existing chop/move + Day 68 crop harvest + Stretch Trade/Tame
                TreeEntity tree = (rhit != null) ? rhit.GetComponent<TreeEntity>() : null;
                CropEntity crop = (rhit != null) ? rhit.GetComponent<CropEntity>() : null;
                TraderEntity trader = (rhit != null) ? rhit.GetComponent<TraderEntity>() : null;
                AnimalEntity animalC = (rhit != null) ? rhit.GetComponent<AnimalEntity>() : null;
                if (trader != null)
                {
                    bool ok = trader.TryTrade();
                    Debug.Log($"[Trade] success={ok}");
                    return;
                }
                if (animalC != null)  // 비-drafted 시 동물 우클릭 = 길들이기 시도
                {
                    bool ok = animalC.TryTame();
                    Debug.Log($"[Tame] success={ok}");
                    return;
                }
                if (crop != null && crop.IsRipe)
                {
                    int food = crop.Harvest();
                    Debug.Log($"[Harvest] +{food} 식량");
                    return;
                }
                if (tree != null)
                {
                    PawnChopper chopper = currentSelection.GetComponent<PawnChopper>();
                    if (chopper != null) chopper.SetTreeTarget(tree);
                }
                else
                {
                    PawnChopper chopper = currentSelection.GetComponent<PawnChopper>();
                    if (chopper != null) chopper.ClearTask();
                    PawnMovement mv = currentSelection.GetComponent<PawnMovement>();
                    if (mv != null) mv.SetTarget(new Vector2(mouseWorld.x, mouseWorld.y));
                    // 수동 이동 명령 → AI 5초 skip (즉시 override 방지)
                    currentSelection.ManualMoveUntil = Time.time + 5f;
                }
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
