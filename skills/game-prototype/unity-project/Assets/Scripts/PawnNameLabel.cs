using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Day 30: floating name label above the pawn.
    /// 운영자 피드백 2026-05-27: 2번째 라인에 status 표시
    ///   (벌목중/이동중/사냥중/요리중/휴식/징집됨). 게임이 살아있어 보임.
    /// </summary>
    public class PawnNameLabel : MonoBehaviour
    {
        // #199 A2 ortho 3.5 + 1x1 pawn — 라벨을 HP 바(top 0.68) 바로 위로 내림.
        //  순서(위→아래): name(0.98) > status(0.80) > HP 바(0.68) > mood 바(0.55) > 머리(0.5).
        //  카메라 ~1.7x 줌-인 → characterSize 0.08 → 0.05 로 축소해도 동일하게 읽힘 (plan §5).
        [SerializeField] private Vector3 offset = new Vector3(0, 0.98f, 0);  // bar(0.68) + status(0.80) 위
        [SerializeField] private float fontSize = 64;
        [SerializeField] private float characterSize = 0.05f;

        private TextMesh nameTm;
        private TextMesh statusTm;
        private PawnEntity entity;
        private PawnNeeds needs;
        private PawnChopper chopper;
        private PawnHunter hunter;
        private PawnGatherer gatherer;
        private PawnCook cook;
        private PawnMovement movement;

        private float lastStatusUpdate;

        private void Awake()
        {
            entity = GetComponent<PawnEntity>();
            needs = GetComponent<PawnNeeds>();
            chopper = GetComponent<PawnChopper>();
            hunter = GetComponent<PawnHunter>();
            gatherer = GetComponent<PawnGatherer>();
            cook = GetComponent<PawnCook>();
            movement = GetComponent<PawnMovement>();

            string name = entity != null ? entity.PawnName : "Pawn";

            var nameGo = new GameObject("NameLabel");
            nameGo.transform.SetParent(transform, false);
            nameGo.transform.localPosition = offset;
            nameTm = nameGo.AddComponent<TextMesh>();
            nameTm.text = name;
            nameTm.fontSize = (int)fontSize;
            nameTm.characterSize = characterSize;
            nameTm.anchor = TextAnchor.MiddleCenter;
            nameTm.alignment = TextAlignment.Center;
            nameTm.color = new Color(0.95f, 0.92f, 0.85f, 0.95f);
            var nameMr = nameGo.GetComponent<MeshRenderer>();
            if (nameMr != null) nameMr.sortingOrder = 30;

            // 2번째 라인: status (작은 글씨, 살짝 아래)
            var statusGo = new GameObject("StatusLabel");
            statusGo.transform.SetParent(transform, false);
            // #199 A2: characterSize 0.08→0.05 로 줄었으니 줄간격도 0.18→0.15 로 (status 0.83).
            statusGo.transform.localPosition = new Vector3(offset.x, offset.y - 0.15f, offset.z);
            statusTm = statusGo.AddComponent<TextMesh>();
            statusTm.text = "";
            statusTm.fontSize = (int)(fontSize * 0.7f);
            statusTm.characterSize = characterSize * 0.85f;
            statusTm.anchor = TextAnchor.MiddleCenter;
            statusTm.alignment = TextAlignment.Center;
            statusTm.color = new Color(0.75f, 0.85f, 0.95f, 0.90f);
            var statusMr = statusGo.GetComponent<MeshRenderer>();
            if (statusMr != null) statusMr.sortingOrder = 30;
        }

        private void Start()
        {
            // GameManager 가 spawn 후 reflection 으로 pawnName 박는다 → Awake 시점엔 default.
            //  Start 에서 한 번 더 가져와서 라벨 텍스트 갱신.
            if (entity != null && nameTm != null && !string.IsNullOrEmpty(entity.PawnName))
            {
                nameTm.text = entity.PawnName;
            }
        }

        private void Update()
        {
            // status 0.25s 마다 — every-frame 은 textmesh re-bake 비싸짐
            if (Time.time - lastStatusUpdate < 0.25f) return;
            lastStatusUpdate = Time.time;
            statusTm.text = ComputeStatusLabel();
        }

        private string ComputeStatusLabel()
        {
            if (entity == null) return "";
            if (entity.IsDead) return "사망";
            if (entity.IsDrafted) return "[징집]";
            if (needs != null && needs.IsSleeping) return "수면";
            if (needs != null && needs.IsBreaking) return "정신붕괴";
            if (chopper != null && chopper.HasTask) return "벌목";
            if (hunter != null && hunter.HasTask) return "사냥";
            if (gatherer != null && gatherer.HasTask) return "채집";
            if (cook != null && cook.HasTask) return "요리";
            if (movement != null && movement.IsMoving) return "이동";
            return "";
        }
    }
}
