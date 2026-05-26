using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Game scene bootstrap — spawns initial pawn(s) and holds global
    /// game state references.  Day 1 = single pawn at origin.  Later
    /// days will own colonist list, needs system, AI Director hookup.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Day 1 spawn")]
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private Vector2 initialSpawnPos = Vector2.zero;

        private void Start()
        {
            if (pawnPrefab != null)
            {
                Instantiate(pawnPrefab, initialSpawnPos, Quaternion.identity);
                Debug.Log("[GameManager] Day 1: spawned colonist at " + initialSpawnPos);
            }
            else
            {
                Debug.LogWarning("[GameManager] pawnPrefab not assigned");
            }
        }
    }
}
