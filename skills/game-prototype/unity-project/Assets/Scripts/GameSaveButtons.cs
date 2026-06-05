using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 6 in-game Save / Load buttons (top-left).  Save serializes
    /// pawn+tree+resource state; Load destroys current state then
    /// re-instantiates from save file.
    /// </summary>
    public class GameSaveButtons : MonoBehaviour
    {
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private GameObject treePrefabRef;  // optional: a fallback tree prefab
        [SerializeField] private Sprite treeSprite;

        // #44(2026-06-05): GameSaveButtons 가 Game.unity 씬에 사전 배치돼 있지 않아(grep 0개)
        //  F5/F9 핫키와 설정메뉴 '저장/불러오기' 행이 모두 무효였다 → 인게임 save/load 전체가
        //  접근 불가(완성해 둔 save/load 로직이 사용 자체가 안 됨).  GameManager 가 다른 UI 처럼
        //  EnsureInScene 으로 런타임 생성 + pawnPrefab/treeSprite 배선한다(코드베이스 EnsureInScene 패턴).
        public static void EnsureInScene(GameObject pawnPrefab, Sprite treeSprite)
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<GameSaveButtons>(FindObjectsInactive.Include);
            if (existing != null) { existing.ConfigureRefs(pawnPrefab, treeSprite); return; }
            var go = new GameObject("GameSaveButtons");
            var gsb = go.AddComponent<GameSaveButtons>();
            gsb.ConfigureRefs(pawnPrefab, treeSprite);
            Debug.Log("[GameSaveButtons] 런타임 생성 — F5/F9 + 설정메뉴 저장행 활성화");
        }

        /// <summary>로드 시 pawn/tree 재생성에 쓸 ref 배선(런타임 생성 경로).</summary>
        public void ConfigureRefs(GameObject pawn, Sprite tree)
        {
            if (pawn != null) pawnPrefab = pawn;
            if (tree != null) treeSprite = tree;
        }

        private void Awake()
        {
            if (saveButton != null)
            {
                saveButton.onClick.RemoveAllListeners();
                saveButton.onClick.AddListener(OnSave);
            }
            if (loadButton != null)
            {
                loadButton.onClick.RemoveAllListeners();
                loadButton.onClick.AddListener(OnLoad);
            }
        }

        private void Update()
        {
            // Hotkeys: F5 = save, F9 = load
            if (Input.GetKeyDown(KeyCode.F5)) OnSave();
            if (Input.GetKeyDown(KeyCode.F9)) OnLoad();
        }

        // #44 public 화: 설정 메뉴(저장/불러오기 행)가 시각 버튼 없이도 이 캐논 로직을
        //  직접 호출할 수 있게 한다.  F5/F9(Update) + onClick 배선도 그대로 사용.
        public void OnSave()
        {
            SaveLoadManager.Save();
        }

        public void OnLoad()
        {
            SaveData data = SaveLoadManager.Load();
            if (data == null) return;

            // #audit2 #13 — 로드 직전 전체 예약 초기화.  아래에서 현재 pawn/tree 를 Destroy
            //  하고 새로 spawn 하는데, 파괴되는 엔티티들이 ReservationManager 에 잡아둔 예약이
            //  남으면 respawn 후 죽은 owner/타겟을 가리키는 orphaned 예약이 되어, 새 pawn 이
            //  그 자원을 영구히 '예약됨'으로 보고 작업을 못 잡을 수 있다.  깨끗이 reset.
            MelonS.GameProto.AI.ReservationManager.Clear();

            // Destroy current pawns + trees
            foreach (var p in FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                Destroy(p.gameObject);
            foreach (var t in FindObjectsByType<TreeEntity>(FindObjectsSortMode.None))
                Destroy(t.gameObject);
            // #버그헌트2(2026-06-04): 플레이어 완성 구조물/작물/스톡파일도 파괴 후 1:1 재구성.
            //  이전엔 이들을 파괴/재생성하지 않아, 같은 세션 F9 는 우연히 동작(엔티티 잔존)했지만
            //  게임 재시작(새 씬) 후 로드 시 재구성 코드가 없어 전부 소실됐다(CRITICAL).
            foreach (var s in FindObjectsByType<StructureTag>(FindObjectsSortMode.None))
                Destroy(s.gameObject);
            foreach (var c in FindObjectsByType<CropEntity>(FindObjectsSortMode.None))
                Destroy(c.gameObject);
            // 스톡파일 zone 파괴 전에 마커 스프라이트를 캡처(재구성 시 시각 유지; 같은 세션 F9).
            //  재시작(씬에 zone 없음)이면 null → 기능은 동작하나 마커 미표시(차후 폴리시).
            Sprite stockMarker = null;
            foreach (var z in FindObjectsByType<StockpileZoneEntity>(FindObjectsSortMode.None))
            {
                if (stockMarker == null)
                {
                    var zsr = z.GetComponent<SpriteRenderer>();
                    if (zsr != null) stockMarker = zsr.sprite;
                }
                Destroy(z.gameObject);
            }

            // Restore resources
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.wood = data.wood;
                ResourceManager.Instance.food = data.food;
            }

            // Re-spawn pawns
            foreach (var ps in data.pawns)
            {
                if (pawnPrefab == null) continue;
                GameObject p = Instantiate(pawnPrefab, ps.position, Quaternion.identity);
                PawnEntity entity = p.GetComponent<PawnEntity>();
                if (entity != null)
                {
                    var nameField = typeof(PawnEntity).GetField("pawnName",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (nameField != null) nameField.SetValue(entity, ps.name);
                }
                // #트레잇 결정성: 로드 시에도 이름으로 트레잇 재시드 → 같은 이름이면 저장 전과
                //  동일 트레잇(save/load 결정성; 이전엔 클론 이름 시드라 비결정적이었음).
                p.GetComponent<PawnTraits>()?.ReRollFromName(ps.name);
                // #save-load 완성 — 부위별 HP/출혈/붕대 복원.  순서 중요: ReRollFromName 이
                //  트레잇 maxHp 를 먼저 확정한 뒤, 실제 부상 상태(hp/출혈/붕대 + 사망/다운)를
                //  덮어쓴다.  구 세이브(배열 없음)는 RestorePartState 가 길이 가드로 스킵.
                p.GetComponent<PawnHealth>()?.RestorePartState(ps.partHp, ps.partBleed, ps.partBandaged);
                // #버그헌트2(2026-06-04): 부위 HP 복원 후 PawnEntity.Hp 를 PawnHealth 와 동기화.
                //  Awake 가 풀피로 깔아둬, 죽지 않은 부상 폰은 다음 피격 전까지 HP바가 풀피로 표시됐다.
                entity?.SyncHpFromHealth();
                PawnNeeds needs = p.GetComponent<PawnNeeds>();
                if (needs != null)
                {
                    needs.food = ps.food;
                    needs.sleep = ps.sleep;
                    needs.mood = ps.mood;
                    // #버그헌트4(2026-06-05): 복원 mood 에 트레잇 baseline·thought 효과가 이미
                    //  녹아 있으므로(저장 당시 가산분) 로드 직후 재가산을 막는다.  안 하면 매 로드마다
                    //  트레잇 baseline(Cheerful+10 등) + 장비 thought(좋은옷차림) 가 다시 얹혀 mood 가
                    //  누적 표류/점프했다.
                    needs.MarkTraitsApplied();
                    p.GetComponent<PawnThoughts>()?.SyncThoughtBaseline();
                }
                // #audit2 #17 — 스킬 level+xp 복원(Instantiate 후 Awake 가 기본 레벨을
                //  깔아둔 뒤 덮어쓴다).  구 세이브 호환 위해 길이 가드.
                var sk = p.GetComponent<PawnSkills>();
                if (sk != null && sk.entries != null && ps.skillLevels != null && ps.skillXp != null
                    && ps.skillLevels.Length == sk.entries.Length
                    && ps.skillXp.Length == sk.entries.Length)
                {
                    for (int i = 0; i < sk.entries.Length; i++)
                    {
                        if (sk.entries[i] == null) continue;
                        sk.entries[i].level = ps.skillLevels[i];
                        sk.entries[i].xp = ps.skillXp[i];
                    }
                }
                // #audit2 #15 — 징집 상태 복원.
                if (entity != null && ps.drafted) entity.SetDrafted(true);
            }

            // Re-spawn trees (use treeSprite reference if no prefab)
            foreach (var ts in data.trees)
            {
                GameObject t;
                if (treePrefabRef != null)
                {
                    t = Instantiate(treePrefabRef, ts.position, Quaternion.identity);
                }
                else
                {
                    t = new GameObject("Tree");
                    t.transform.position = ts.position;
                    var sr = t.AddComponent<SpriteRenderer>();
                    if (treeSprite != null) sr.sprite = treeSprite;
                    sr.sortingOrder = 5;
                    var col = t.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(1.5f, 1.5f);
                    t.AddComponent<TreeEntity>();
                }
            }

            // #버그헌트2(2026-06-04): 플레이어 완성 구조물 재구성 — 모든 벽/침대/문/화덕/램프/
            //  울타리/바리케이드/바닥을 BuildManager.SpawnFinished(mode,pos)로 복원(빌드 완료와 동일
            //  경로 → 재질/품질/등록/footprint 일관).  재시작 후 로드 시 구조물 소실(CRITICAL) fix.
            if (data.structures != null && BuildManager.Instance != null)
            {
                foreach (var s in data.structures)
                    BuildManager.Instance.SpawnFinished((BuildManager.Mode)s.mode, s.position);
            }

            // #버그헌트2(2026-06-04): 작물 재구성(성장도는 아래 ApplyLoadedSubStates 가 위치매칭 복원).
            //  CropEntity.Awake 가 stage 스프라이트/콜라이더를 세팅한다.
            if (data.crops != null)
            {
                foreach (var cs in data.crops)
                {
                    var cgo = new GameObject("Crop");
                    cgo.transform.position = cs.position;
                    cgo.AddComponent<SpriteRenderer>();
                    cgo.AddComponent<CropEntity>();
                }
            }

            // #버그헌트2(2026-06-04): 스톡파일 zone 재구성(우선순위는 ApplyLoadedSubStates 가 복원).
            if (data.stockpiles != null)
            {
                foreach (var ss in data.stockpiles)
                    StockpileZoneEntity.Spawn(ss.position, stockMarker, (StockpilePriority)ss.priority);
            }

            // #276 서브상태 복원 — 침대 품질/스톡파일 우선순위/벽 재질/트리 종.  Save()는
            //  이미 직렬화하나 OnLoad 가 이 호출을 빠뜨려 매 로드마다 default 로 리셋됐다
            //  (V13SubStateRoundTripTest 가 FLAG 한 미배선).  엔티티 spawn 이후 호출.
            //  #버그헌트2: 위 재구성으로 엔티티가 존재하므로 위치매칭 서브상태(작물 성장/스톡파일
            //  우선순위/트리 종)가 정상 적용된다.
            SaveLoadManager.ApplyLoadedSubStates(data);

            // #버그헌트2(2026-06-04): 연구 진행도 복원 — 재시작/씬 재로드 시 BuildTree 가 모든 tech 를
            //  0/미완료로 재생성해 완료했던 unlock(원시활·개선화덕 등)이 전부 풀리던 것 fix.  동일 id
            //  tech 에 currentPoints/completed 매칭 복원 + activeTech 재설정(구 세이브는 빈 리스트라 스킵).
            var rmgr = ResearchManager.Instance;
            if (rmgr != null && rmgr.techs != null && data.research != null && data.research.Count > 0)
            {
                foreach (var rs in data.research)
                    foreach (var t in rmgr.techs)
                        if (t != null && t.id == rs.id)
                        { t.currentPoints = rs.currentPoints; t.completed = rs.completed; break; }
                rmgr.activeTech = null;
                if (!string.IsNullOrEmpty(data.activeTechId))
                    foreach (var t in rmgr.techs)
                        if (t != null && t.id == data.activeTechId) { rmgr.activeTech = t; break; }
            }

            // #276 게임 시계 복원 — 로드 시 0 으로 리셋되면 AIDirector 레이드 스케줄이
            //  전부 어긋난다(밤/낮·이벤트 타이밍 파손).  저장된 GameSeconds 로 복원.
            if (GameClock.Instance != null) GameClock.Instance.SetGameSeconds(data.gameSeconds);

            // #버그헌트3(2026-06-04): 레이드 스케줄러 상태 복원 — 시계만 복원하던 #276 과 짝.
            //  안 하면 로드 직후 첫 06:00 에 즉시 레이드 + 난이도(escalation) 초반 리셋.
            var aidir = FindFirstObjectByType<AIDirector>();
            if (aidir != null) aidir.RestoreRaidState(data.raidLastDay, data.raidCount);

            Debug.Log($"[SaveLoad] restored: {data.pawns.Count} pawns, {data.trees.Count} trees + 서브상태 + 시계 {data.gameSeconds:F0}s");
        }
    }
}
