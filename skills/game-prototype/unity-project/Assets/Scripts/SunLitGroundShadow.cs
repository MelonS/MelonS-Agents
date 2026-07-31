using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>가구의 접지 그림자를 **보장하고** 태양 구동에 편입한다.
    ///
    /// 계기 (2026-08-01 운영자): "모든 그림자의 방향이 적당한지 확인해봤어?"
    ///
    /// 확인해 보니 두 가지가 겹쳐 있었다.
    ///  ① 나무(전단 셰이더)와 주민·동물(BlobShadow)은 시각에 따라 방향·길이·농도가
    ///     함께 움직이는데, 가구는 그 경로에 들어 있지 않았다.
    ///  ② 더 근본적으로, **가구에는 그림자가 아예 없었다.**  프리팹에는 `GroundShadow`
    ///     자식이 있는데 씬의 인스턴스 8개(침대 6·화덕·연구대)가 전부
    ///     `m_RemovedGameObjects` 오버라이드로 그 자식을 지운 상태였다.  씬에만 남은
    ///     흔적이라 코드를 아무리 읽어도 보이지 않고, 로그도 남지 않는다 —
    ///     이 레포에서 반복된 '직렬화가 코드를 이긴' 유형이다.
    ///
    /// 그래서 이 컴포넌트는 자식을 **찾고, 없으면 만든다**.  씬 상태가 어떻든 결과가
    /// 같아지므로, 다음에 누가 씬에서 또 지워도 조용히 사라지지 않는다.
    /// (GameManager 가 `spawnPositions` 를 Start 에서 되박는 것과 같은 방어.)
    /// </summary>
    [DisallowMultipleComponent]
    public class SunLitGroundShadow : MonoBehaviour
    {
        private const string ShadowChildName = "GroundShadow";

        // 프리팹 생성 시점(SceneSetup.AttachGroundShadow)에 맞춰 넣는 값 —
        //  가구마다 크기·발밑 위치가 다르다.
        [SerializeField] private float yOffset = -0.5f;
        [SerializeField] private float width = 0.9f;
        [SerializeField] private float alpha = 0.55f;

        /// <summary>프리팹 빌드 단계에서 호출 — 런타임 재생성 시 같은 모양이 나오도록.</summary>
        public void Configure(float y, float w, float a)
        {
            yOffset = y; width = w; alpha = a;
        }

        private void Start()
        {
            var child = transform.Find(ShadowChildName);
            if (child != null)
            {
                BlobShadow.RegisterExisting(child, alpha);
                return;
            }
            // 씬 오버라이드로 지워졌거나 프리팹이 낡았다 — 코드가 직접 만든다.
            //  BlobShadow.Attach 는 자식 이름을 "BlobShadow" 로 만들고 태양 등록까지
            //  한 번에 끝낸다 (주민·동물이 쓰는 바로 그 경로 — 같은 규칙 = 같은 그림).
            BlobShadow.Attach(gameObject, width, yOffset, alpha);
        }
    }
}
