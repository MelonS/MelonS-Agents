using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// D3 몰입 레이어 (design-immersion-2026-06-11) — 절차 파티클 버스트.
    /// 작업 피드백이 FloatingText 뿐(타격감 0)이던 것을 물리 파편으로 보강:
    /// 벌목 나무칩 / 채광 돌파편 / 완파 시 큰 버스트.  아트 자산 0 —
    /// 8x8 흰 텍스처 + Sprites/Default(전 SpriteRenderer 가 써서 빌드에 항상
    /// 포함) 머티리얼을 1회 생성해 공유, 버스트마다 1회용 ParticleSystem GO
    /// (stopAction=Destroy 로 자체 정리, 호출 빈도가 타격 SFX 스로틀과 묶여
    /// 있어 풀링 불필요).
    /// </summary>
    public static class ParticleFx
    {
        private static Material mat;

        /// <summary>FlickerLight 불티 등 외부 상시 ParticleSystem 도 같은 머티리얼 공유.</summary>
        public static Material SharedMaterial => GetMat();

        /// <summary>pos 에서 color 파편 count 개를 사방으로 튀긴다.
        ///  gravity 1.4 = 칩 포물선 낙하, 0 근처 = 먼지처럼 떠오름/부유.</summary>
        public static void Burst(Vector3 pos, Color color, int count = 6,
                                 float speed = 1.8f, float size = 0.09f, float life = 0.5f,
                                 float gravity = 1.4f, int sortingOrder = 11)
        {
            var go = new GameObject("ParticleFx");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);  // 설정 전 자동재생 방지

            var main = ps.main;
            main.duration = life;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.3f);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.08f;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = GetMat();
            psr.sortingOrder = sortingOrder;      // 기본 11 = 본체(10) 위로 파편이 튄다

            ps.Play();
        }

        private static Material GetMat()
        {
            if (mat != null) return mat;
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var px = new Color32[64];
            for (int i = 0; i < 64; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            mat = new Material(Shader.Find("Sprites/Default"));
            mat.mainTexture = tex;
            return mat;
        }
    }
}
