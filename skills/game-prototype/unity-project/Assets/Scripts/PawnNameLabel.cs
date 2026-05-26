using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Day 30: floating name label above the pawn.
    /// Generates a TextMesh child on Awake.  Updates rotation to face
    /// camera + position to stay above pawn head.</summary>
    public class PawnNameLabel : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0, 0.7f, 0);
        [SerializeField] private float fontSize = 48;
        [SerializeField] private float characterSize = 0.04f;

        private TextMesh tm;

        private void Awake()
        {
            var entity = GetComponent<PawnEntity>();
            string name = entity != null ? entity.PawnName : "Pawn";

            var go = new GameObject("NameLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            tm = go.AddComponent<TextMesh>();
            tm.text = name;
            tm.fontSize = (int)fontSize;
            tm.characterSize = characterSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.92f, 0.85f, 0.95f);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 30;
        }

        private void Start()
        {
            // GameManager 가 spawn 후 reflection 으로 pawnName 박는다 → Awake 시점엔 default.
            //  Start 에서 한 번 더 가져와서 라벨 텍스트 갱신.
            var entity = GetComponent<PawnEntity>();
            if (entity != null && tm != null && !string.IsNullOrEmpty(entity.PawnName))
            {
                tm.text = entity.PawnName;
            }
        }
    }
}
