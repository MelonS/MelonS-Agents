using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// G2 콜로니스트 배경 한 줄 (게임성 감사 2026-07-24 — "클릭한 림의 서사 0줄"
    /// 첫인상 갭).  표시 전용: 이름 해시로 결정론 선택 → 세이브/로드 무관하게
    /// 같은 림은 항상 같은 사연.  특성이 있으면 특성 결이 맞는 줄을 우선.
    /// </summary>
    public static class PawnBackstory
    {
        private static readonly string[] Generic =
        {
            "무너진 북쪽 정착지에서 홀로 걸어 내려왔다.",
            "빚을 피해 이름을 바꾸고 변경까지 흘러왔다.",
            "상단 호위로 일하다 이 골짜기에 눌러앉기로 했다.",
            "흉년에 고향을 떠나 새 땅을 찾아 나선 길이었다.",
            "떠돌이 목수의 막내 — 연장 다루는 법을 어깨너머로 배웠다.",
            "옛 광산 마을 출신 — 갱도가 닫히자 갈 곳이 없어졌다.",
            "숲지기의 아이로 자라 나무 곁이 제일 편하다.",
            "강 하류의 어부였다 — 그물 대신 도끼를 잡게 됐다.",
        };

        private static string TraitLine(PawnTraits.Trait t) => t switch
        {
            PawnTraits.Trait.Cheerful     => "떠돌이 악단에서 노래하던 사람 — 악기는 잃었지만 콧노래는 남았다.",
            PawnTraits.Trait.Lazy         => "귀족 집안 셋째로 자라 몸 쓰는 일이 아직 낯설다.",
            PawnTraits.Trait.Industrious  => "새벽 장터에서 잔뼈가 굵었다 — 손이 비면 불안한 사람.",
            PawnTraits.Trait.Bloodthirsty => "국경 수비대 출신 — 활시위 소리에 마음이 오히려 가라앉는다.",
            PawnTraits.Trait.Frail        => "오랜 병치레 끝에 요양하러 왔다가 눌러앉았다.",
            _ => null,
        };

        /// <summary>림의 한 줄 사연.  이름 해시 결정론 — 호출마다 동일.</summary>
        public static string GetLine(PawnEntity pawn)
        {
            if (pawn == null) return "";
            var traits = pawn.GetComponent<PawnTraits>();
            // 특성 결 우선 (첫 특성 기준), 없으면 이름 해시로 일반 풀에서.
            if (traits != null && traits.ActiveTraits != null && traits.ActiveTraits.Count > 0)
            {
                string tl = TraitLine(traits.ActiveTraits[0]);
                if (tl != null) return tl;
            }
            int h = 0;
            string name = pawn.PawnName ?? "";
            for (int i = 0; i < name.Length; i++) h = h * 31 + name[i];   // 플랫폼 불변 해시
            return Generic[Mathf.Abs(h) % Generic.Length];
        }
    }
}
