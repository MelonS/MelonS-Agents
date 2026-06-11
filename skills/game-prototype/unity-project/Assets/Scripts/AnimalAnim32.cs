using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 아트 v2 — 동물 32px 2프레임 보행 (운영자 2026-06-11 "동물도 32px로").
    /// Resources/animals32/animal32_{species}.png (64x32 = idle|walk, 측면 좌향)
    /// 를 런타임 슬라이스.  이동 중 idle↔walk 교차(트롯), 정지 시 idle.
    /// 원화가 좌향이라 flip 은 '우측 이동 시' — MotionFx 의 flip(우향 관례)을
    /// 인계받는다 (flipEnabled=false).  시트 없으면 조용히 비활성(기존 폴백).
    /// 종은 AnimalEntity.Configure/SetSpecies 가 Awake 뒤에 정해지므로 첫
    /// LateUpdate 에 lazy 해석한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimalAnim32 : MonoBehaviour
    {
        private static readonly System.Collections.Generic.Dictionary<string, Sprite[]> cache
            = new System.Collections.Generic.Dictionary<string, Sprite[]>();

        private SpriteRenderer sr;
        private AnimalEntity animal;
        private WolfEnemy wolf;
        private Sprite[] frames;   // [idle, walk]
        private bool inited;
        private bool flip;
        private float gaitClock;
        private Vector3 prevPos;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            animal = GetComponent<AnimalEntity>();
            wolf = GetComponent<WolfEnemy>();
            prevPos = transform.position;
        }

        private static Sprite[] Load(string species)
        {
            if (cache.TryGetValue(species, out var hit)) return hit;
            var tex = Resources.Load<Texture2D>($"animals32/animal32_{species}");
            Sprite[] arr = null;
            if (tex != null && tex.width >= 64)
            {
                arr = new Sprite[2];
                for (int i = 0; i < 2; i++)
                {
                    arr[i] = Sprite.Create(tex, new Rect(i * 32, 0, 32, 32),
                                           new Vector2(0.5f, 0.5f), 32f, 0,
                                           SpriteMeshType.FullRect);
                    arr[i].name = $"animal32_{species}_{(i == 0 ? "idle" : "walk")}";
                }
            }
            cache[species] = arr;
            return arr;
        }

        private void LateUpdate()
        {
            if (!inited)
            {
                inited = true;
                string species = wolf != null ? "wolf"
                    : animal != null ? animal.Species.ToString().ToLowerInvariant()
                    : null;
                frames = species != null ? Load(species) : null;
                if (frames == null) { enabled = false; return; }
                var fx = GetComponent<MotionFx>();
                if (fx != null) fx.flipEnabled = false;   // 좌향 원화 — flip 소유 인계
                sr.sprite = frames[0];
                prevPos = transform.position;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f || sr == null) return;
            Vector3 pos = transform.position;
            Vector2 delta = pos - prevPos;
            prevPos = pos;
            float speed = delta.magnitude / dt;

            if (Mathf.Abs(delta.x) > 0.0005f) flip = delta.x > 0f;   // 좌향 원화: 우측 이동 = flip

            if (speed > 0.15f)
            {
                gaitClock += dt * Mathf.Clamp(speed, 0.8f, 2.6f) * 5f;
                sr.sprite = frames[(int)gaitClock & 1];
            }
            else
            {
                gaitClock = 0f;
                sr.sprite = frames[0];
            }
            sr.flipX = flip;
        }
    }
}
