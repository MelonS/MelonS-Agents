using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #audit3 #0/#1 — 박스(마퀴) 다중 선택된 콜로니스트 발밑에도 펄스 링을 표시.
    ///
    /// 기존 SelectionRing 은 ClickSelector.CurrentSelection(단일 선택) 하나에만 링을
    /// 그려, 마퀴로 여러 명을 선택하면 색 tint 만 바뀌고 단일 선택과 시각이 달랐다.
    /// 이 컴포넌트는 MarqueeSelector.CurrentMultiSelection 을 매 프레임 폴링해, 선택된
    /// pawn 수만큼 링 SpriteRenderer 를 풀링(부족하면 생성, 남으면 숨김)하고 각 pawn
    /// 발밑에 SelectionRing 과 동일한 펄스/색(노랑·drafted=cyan)으로 그린다.
    ///
    /// 안전성: 순수 시각(콜라이더 없음), per-frame 이지만 풀 재사용으로 할당 0,
    /// MarqueeSelector 없거나 멀티선택 0 이면 전부 숨김.  Self-bootstrap(GameManager).
    /// SelectionRing 과 sprite/스케일/펄스 파라미터를 공유해 일관성 유지.
    /// </summary>
    public class MultiSelectionRings : MonoBehaviour
    {
        private static MultiSelectionRings _instance;
        private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>(32);
        private Sprite ringSprite;
        private MarqueeSelector cachedMarquee;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var go = new GameObject("MultiSelectionRings");
            _instance = go.AddComponent<MultiSelectionRings>();
        }

        private void Awake()
        {
            ringSprite = SelectionRing.SharedRingSprite();
        }

        private SpriteRenderer GetOrCreate(int i)
        {
            while (pool.Count <= i)
            {
                var go = new GameObject("MultiSelRing_" + pool.Count);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ringSprite;
                sr.sortingOrder = 12;  // TOP-6 — SelectionRing 과 동일 (브래킷은 본체 위)
                sr.color = new Color(0.94f, 0.94f, 0.90f, 0f);
                pool.Add(sr);
            }
            return pool[i];
        }

        private void Update()
        {
            if (cachedMarquee == null) cachedMarquee = Object.FindFirstObjectByType<MarqueeSelector>();
            var ms = cachedMarquee;

            // 멀티선택이 없으면 전부 숨김(2명 미만이면 단일 SelectionRing 이 담당).
            IReadOnlyList<PawnEntity> sel = (ms != null && ms.HasMultiSelection) ? ms.CurrentMultiSelection : null;
            int count = sel != null ? sel.Count : 0;

            // 펄스 — SelectionRing 과 동일한 위상/속도(1Hz)로 단일·멀티가 함께 숨쉰다.
            float pulse = 0.78f + 0.18f * Mathf.Sin(Time.time * Mathf.PI * 2f);
            const float scale = 1.5f;   // TOP-6 — 스케일 펄스 제거

            int shown = 0;
            for (int i = 0; i < count; i++)
            {
                var p = sel[i];
                if (p == null || p.IsDead) continue;
                var sr = GetOrCreate(shown);
                var pos = p.transform.position;
                sr.transform.position = new Vector3(pos.x, pos.y, pos.z);  // TOP-6 몸 중심
                sr.transform.localScale = new Vector3(scale, scale, 1f);
                Color c = p.IsDrafted ? new Color(0.4f, 0.85f, 1f) : new Color(0.94f, 0.94f, 0.90f);
                c.a = pulse;
                sr.color = c;
                if (!sr.enabled) sr.enabled = true;
                shown++;
            }
            // 남는 풀 항목 숨김.
            for (int i = shown; i < pool.Count; i++)
                if (pool[i] != null && pool[i].enabled) pool[i].enabled = false;
        }
    }
}
