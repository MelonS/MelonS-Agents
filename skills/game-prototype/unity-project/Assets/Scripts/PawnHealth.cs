using System.Collections.Generic;
using UnityEngine;
using MelonS.GameProto.Data;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 45 — RimWorld-style body part health system.
    /// 6 parts: head, torso, leftArm, rightArm, leftLeg, rightLeg.
    /// Each part has maxHp and currentHp.  Damage routes to a chosen part
    /// (random by default, weighted: torso 35%, legs 20%, arms 20%, head 5%).
    /// Effects:
    ///   - head HP=0  → death
    ///   - torso HP=0 → death
    ///   - head HP<30% → downed (consciousness loss)
    ///   - leg damage → movement speed multiplier (linear)
    ///   - arm damage → manipulation speed multiplier (linear)
    /// Bleeding: per-part bleed rate accumulates over time, drains overall.
    /// Bandaged parts stop bleeding.
    /// </summary>
    public class PawnHealth : MonoBehaviour
    {
        public enum PartId { Head=0, Torso=1, LeftArm=2, RightArm=3, LeftLeg=4, RightLeg=5 }

        [System.Serializable]
        public class BodyPart
        {
            public PartId id;
            public string nameKr;
            public int maxHp;
            public int hp;
            public float bleedRate;   // HP/sec drain when bleeding
            public bool bandaged;
            public bool isVital;       // head/torso — death if hp == 0
            public BodyPart(PartId id, string nameKr, int max, bool vital)
            {
                this.id = id; this.nameKr = nameKr;
                this.maxHp = max; this.hp = max;
                this.bleedRate = 0f; this.bandaged = false;
                this.isVital = vital;
            }
        }

        public BodyPart[] parts;
        public bool IsDead { get; private set; }
        public bool IsDowned { get; private set; }
        public float TotalHpRatio { get; private set; }   // 0..1 — for floating bar

        // 운영자 fb (2026-05-31): 죽었을 때 이미지 변화 없음 + 상태별 처리.
        //  PoseState 는 PawnPoseDriver / PawnEntity 가 읽어 시각화하는 단일 상태 신호.
        //  - Dead   → 회색조 corpse + 90° 쓰러짐 + 바/이름 숨김 + walk-bob 정지
        //  - Downed → 의식불명(누움 tint), 아직 살아있음
        //  - Alive  → 기존 standing/sleeping/drafted 유지
        public enum PoseState { Alive = 0, Downed = 1, Dead = 2 }
        public PoseState State
        {
            get
            {
                if (IsDead) return PoseState.Dead;
                if (IsDowned) return PoseState.Downed;
                return PoseState.Alive;
            }
        }
        // 상태 전환을 polling 으로 감지하는 driver 를 위한 버전 카운터 (싱글톤 구독
        //  race 회피 — bug-pattern #7: 이벤트 구독 대신 driver 가 State 를 매 프레임 read).
        public int StateVersion { get; private set; }

        // 자식 "PawnSpriteBob" GameObject 이름 (SceneSetup.Pawn 와 동일).  죽은 림이
        //  walk-bob/idle-breathe 안 하도록 이 자식의 PawnSpriteBob 컴포넌트를 끈다.
        //  (PawnSpriteBob 은 다른 lane 파일 → 편집 금지.  컴포넌트 enabled 만 게이트.)
        private const string BodyChildName = "PawnSpriteBob";

        // R3: 6 body parts 데이터 외부화 - HealthPartsConfig SO 참조
        [SerializeField] private HealthPartsConfig partsConfig;

        private float lastBleedTick = -10f;

        private void Awake()
        {
            if (partsConfig == null) partsConfig = HealthPartsConfig.CreateDefault();
            // R3: SO 의 PartDef[] 에서 BodyPart 인스턴스 생성
            int n = partsConfig.parts.Length;
            parts = new BodyPart[n];
            for (int i = 0; i < n; i++)
            {
                var def = partsConfig.parts[i];
                parts[i] = new BodyPart(def.id, def.nameKr, def.maxHp, def.isVital);
            }
            RecomputeAggregates();
        }

        private void Update()
        {
            if (IsDead) return;
            // Bleed ticks once per second
            if (Time.time - lastBleedTick > 1f)
            {
                lastBleedTick = Time.time;
                bool anyBleed = false;
                foreach (var p in parts)
                {
                    if (p.bandaged) continue;
                    if (p.bleedRate <= 0f) continue;
                    anyBleed = true;
                    // Spread bleed damage across vital parts proportionally —
                    //  bleed drains the source part itself first then random
                    //  redistribution to torso/head if part is below 30%.
                    p.hp = Mathf.Max(0, p.hp - Mathf.CeilToInt(p.bleedRate));
                    // Heal-rate slow: bleeding parts slowly stop bleeding over time
                    p.bleedRate = Mathf.Max(0f, p.bleedRate - 0.05f);
                }
                if (anyBleed) RecomputeAggregates();
                CheckDeath();
            }
        }

        /// <summary>
        /// Apply damage to a random body part (weighted) or to a specified one.
        /// Returns the affected part.  Sets bleedRate proportional to damage.
        /// </summary>
        public BodyPart TakeDamage(int dmg, PartId? preferPart = null)
        {
            if (IsDead || dmg <= 0) return null;
            BodyPart target = preferPart.HasValue ? GetPart(preferPart.Value) : PickRandomPart();
            target.hp = Mathf.Max(0, target.hp - dmg);
            // Wound bleed: bigger damage on smaller part = relatively worse bleed
            float baseBleed = dmg * 0.25f;
            if (target.id == PartId.Head || target.id == PartId.Torso) baseBleed *= 1.5f;
            target.bleedRate += baseBleed;
            // Cap bleed so a single mega-hit doesn't kill in seconds
            target.bleedRate = Mathf.Min(target.bleedRate, 3.0f);
            RecomputeAggregates();
            CheckDeath();
            return target;
        }

        public void Bandage(PartId id)
        {
            var p = GetPart(id);
            if (p == null) return;
            p.bleedRate = 0f;
            p.bandaged = true;
        }

        public void HealAll(int amount)
        {
            foreach (var p in parts)
            {
                p.hp = Mathf.Min(p.maxHp, p.hp + amount);
            }
            RecomputeAggregates();
        }

        private BodyPart PickRandomPart()
        {
            // R3: SO 의 weight 사용 - default 5/35/20/20/10/10 합 100
            int wH = partsConfig.weightHead;
            int wT = partsConfig.weightTorso;
            int wLL = partsConfig.weightLeftLeg;
            int wRL = partsConfig.weightRightLeg;
            int wLA = partsConfig.weightLeftArm;
            int wRA = partsConfig.weightRightArm;
            int total = wH + wT + wLL + wRL + wLA + wRA;
            if (total <= 0) total = 1;
            int roll = Random.Range(0, total);
            int acc = 0;
            acc += wH;  if (roll < acc) return parts[(int)PartId.Head];
            acc += wT;  if (roll < acc) return parts[(int)PartId.Torso];
            acc += wLL; if (roll < acc) return parts[(int)PartId.LeftLeg];
            acc += wRL; if (roll < acc) return parts[(int)PartId.RightLeg];
            acc += wLA; if (roll < acc) return parts[(int)PartId.LeftArm];
            return parts[(int)PartId.RightArm];
        }

        public BodyPart GetPart(PartId id)
        {
            return parts[(int)id];
        }

        public float MovementSpeedMultiplier()
        {
            // Each leg contributes 50%.  Lost leg = 50% speed.  Both legs 0 = crawl.
            float lf = parts[(int)PartId.LeftLeg].hp  / (float)parts[(int)PartId.LeftLeg].maxHp;
            float rf = parts[(int)PartId.RightLeg].hp / (float)parts[(int)PartId.RightLeg].maxHp;
            return Mathf.Max(0.10f, 0.5f * lf + 0.5f * rf);
        }

        public float WorkSpeedMultiplier()
        {
            // Each arm contributes 50%.  Both lost = 10% (other limbs / mouth).
            float la = parts[(int)PartId.LeftArm].hp  / (float)parts[(int)PartId.LeftArm].maxHp;
            float ra = parts[(int)PartId.RightArm].hp / (float)parts[(int)PartId.RightArm].maxHp;
            return Mathf.Max(0.10f, 0.5f * la + 0.5f * ra);
        }

        public string SummaryKr()
        {
            // Compact health summary for UI tooltips.
            var s = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                s.Append($"{p.nameKr}:{p.hp}/{p.maxHp}");
                if (p.bleedRate > 0f) s.Append("출혈");
                if (p.bandaged) s.Append("붕대");
                s.Append("  ");
            }
            return s.ToString().TrimEnd();
        }

        private void RecomputeAggregates()
        {
            int totalMax = 0; int totalCur = 0;
            foreach (var p in parts) { totalMax += p.maxHp; totalCur += p.hp; }
            TotalHpRatio = totalMax > 0 ? (float)totalCur / totalMax : 0f;
        }

        private void CheckDeath()
        {
            PoseState prev = State;

            BodyPart head  = parts[(int)PartId.Head];
            BodyPart torso = parts[(int)PartId.Torso];
            if (head.hp <= 0 || torso.hp <= 0)
            {
                if (!IsDead)
                {
                    IsDead = true;
                    IsDowned = true;
                    Debug.Log($"[PawnHealth] {gameObject.name} 사망 — 머리={head.hp} 몸통={torso.hp}");
                    // Disable AI/work components on death.  GetComponents on the ROOT
                    //  catches PawnMovement/AI/worker scripts (all root-attached).
                    //  SpriteRenderer 는 MonoBehaviour 아니라 자동 제외됨.
                    //  PawnFloatingBars 는 여기서 끄지 않고 PawnPoseDriver 가 바/이름을
                    //   숨긴다(아래 StopBodyBob 와 함께 단일 시각화 lane 유지).
                    foreach (var mb in GetComponents<MonoBehaviour>())
                    {
                        if (mb == this) continue;
                        if (mb is PawnFloatingBars) continue;
                        // PawnPoseDriver 는 죽은 뒤에도 corpse 90° 회전을 ease 로
                        //  적용하고 바/이름을 숨겨야 하므로 비활성화에서 제외.
                        if (mb is PawnPoseDriver) continue;
                        mb.enabled = false;
                    }
                    // 죽은 림이 계속 walk-bob/idle-breathe 하던 버그 fix:
                    //  PawnSpriteBob 컴포넌트는 자식 GameObject 에 있어 위 root-only
                    //  loop 가 못 끈다.  자식의 컴포넌트만 직접 게이트(다른 lane 파일
                    //  소스는 건드리지 않음 — enabled 만 set).
                    StopBodyBob();
                }
                if (State != prev) StateVersion++;
                return;
            }
            // Downed: head <30% triggers unconsciousness (alive)
            IsDowned = (head.hp < head.maxHp * 0.3f);
            if (State != prev) StateVersion++;
        }

        // 자식 "PawnSpriteBob" 의 PawnSpriteBob 컴포넌트를 비활성화해 죽은 림의
        //  walk-bob/idle-breathe 를 멈춘다.  PawnPoseDriver 가 corpse 90° 회전을
        //  적용한 뒤 bob 이 계속 돌면 자식 localPosition.Y 를 덮어써 충돌하므로,
        //  상태 게이트는 여기 state-source 에서 한다.
        private void StopBodyBob()
        {
            Transform body = transform.Find(BodyChildName);
            if (body == null) return;
            var bob = body.GetComponent<PawnSpriteBob>();
            if (bob != null) bob.enabled = false;
        }
    }
}
