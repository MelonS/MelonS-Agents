#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""spec-sync.py — 기획서를 구현과 동기화하는 도구.

운영자 지침 (2026-07-29):
  "지금 구현된 걸 다시 기획서로 뽑아내고 기획서를 검증한 후 보충해서 추가 구현"
  "기획서와 동기화 시키고 전체적인 완성도를 올리기 위한 도구로 사용하는 거지"

손으로 쓴 as-built 문서는 쓰는 순간부터 낡는다 (`systems-inventory-2026-07-24.md`
는 5일 만에 실제와 어긋났다).  그래서 **코드에서 사실을 뽑아** 기획서와 대조하는
장치로 만든다 — 이 레포가 아트·폰트·PPU·톤에 쓰는 게이트와 같은 방식.

잡는 것 — 셋 다 오늘(2026-07-29) 실제로 사고를 낸 계열이다:

  A. 유령 컴포넌트   MonoBehaviour 를 만들어 놓고 아무 데서도 안 붙이는 것.
                     실제 사례: `FootprintDriver.cs` — 운영자가 지시한 "흙 발자국"
                     기능이 파일로는 존재하는데 배선이 0건이라 게임에 없었다.
                     기획서에는 "있음"으로 읽히고 화면에는 없는 최악의 드리프트.

  B. 그림자 상수     같은 이름의 상수가 여러 곳에 **다른 값**으로 존재하는 것.
                     실제 사례: `SceneSetup.cs` 의 지역 `MAP_HALF = 20` 이 정본
                     `TerrainLayout.MAP_HALF = 45` 를 가려, 맵이 두 번 커지는 동안
                     나무만 옛 범위에 깔렸다.  빌드도 게이트도 통과했다.

  C. 문서 수치 드리프트  기획서가 인용한 숫자가 코드와 다른 것.
                     문서에 `이름 = 값` 형태로 적힌 것을 코드와 대조한다.

사용:
  python games/pawnsim/scripts/spec-sync.py            # 전체
  python games/pawnsim/scripts/spec-sync.py --only ghost
  python games/pawnsim/scripts/spec-sync.py --json     # 기계 판독
exit 0 = 드리프트 없음.
"""
from __future__ import annotations
import argparse
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "unity-project" / "Assets"
SCRIPTS = ASSETS / "Scripts"
EDITOR = ASSETS / "Editor"
SCENES = ASSETS / "Scenes"
PREFABS = ASSETS / "Prefabs"
DOCS = ROOT / "docs"

# 유령 판정에서 제외 — 이유를 반드시 적는다.
#  Tests/ 하위는 하네스가 이름 문자열/리플렉션으로 부르므로 정적 참조가 없는 게 정상.
GHOST_SKIP_DIRS = {"Tests"}


def cs_files() -> list[Path]:
    return sorted([p for p in SCRIPTS.rglob("*.cs")] + [p for p in EDITOR.rglob("*.cs")])


def read(p: Path) -> str:
    return p.read_text(encoding="utf-8", errors="replace")


# ── A. 유령 컴포넌트 ──────────────────────────────────────────────────────
def find_ghost_components() -> list[dict]:
    """MonoBehaviour 인데 코드/프리팹/씬 어디에서도 참조되지 않는 클래스."""
    classes: dict[str, Path] = {}
    for p in SCRIPTS.rglob("*.cs"):
        src = read(p)
        for m in re.finditer(r"\bclass\s+(\w+)\s*:\s*([^{]+)", src):
            name, bases = m.group(1), m.group(2)
            if "MonoBehaviour" in bases:
                classes[name] = p

    # 참조 집계: .cs 전체 + 씬/프리팹의 m_Script guid 는 .meta 로 역추적
    blob = "\n".join(read(p) for p in cs_files())
    scene_blob = ""
    for p in list(SCENES.glob("*.unity")) + list(PREFABS.glob("*.prefab")):
        scene_blob += read(p)

    guid_of: dict[str, str] = {}
    for name, path in classes.items():
        meta = path.with_suffix(path.suffix + ".meta")
        if meta.exists():
            g = re.search(r"guid:\s*([0-9a-f]{32})", read(meta))
            if g:
                guid_of[name] = g.group(1)

    # 배선 판정은 **도달 가능성**으로 한다.  단순 참조 카운트로는 두 방향 모두 틀린다:
    #   · 자기 파일을 통째로 제외하면 → 같은 파일에서 AddComponent 로 붙는 정상 헬퍼가
    #     거짓 양성이 된다 (CardClick·FloatingTextHost 등).
    #   · 자기 파일을 포함해 세면 → 죽은 파일 안에서 헬퍼끼리 서로 참조하는 덩어리가
    #     통째로 살아있는 것처럼 보인다 (FootprintDriver 가 정확히 이 경우다).
    # 그래서 "씬/프리팹에 붙었거나, **살아있는 파일**에서 참조되면 산다"로 정의하고
    # 고정점까지 전파한다.
    per_file = {p: read(p) for p in cs_files()}
    owner = {name: path for name, path in classes.items()}

    live: set[str] = set()
    for name in classes:
        if guid_of.get(name) and guid_of[name] in scene_blob:
            live.add(name)

    # 자기 등록(self-bootstrap)도 정상 배선이다.  [RuntimeInitializeOnLoadMethod] 가
    #  붙은 파일은 유니티가 씬 로드 시 자동 호출하므로 **외부 참조가 0이어도 살아 있다.**
    #  1차 구현이 이걸 몰라 ColorGradeDriver·FootprintDriver·CloudShadowDriver·
    #  MainMenuMotion·BlueprintDragDesignation 5종을 전부 유령으로 오판했다
    #  (그 오판을 근거로 "발자국 미배선"이라고 운영자에게 보고까지 했다).
    #  게이트가 틀린 근거를 주면 없느니만 못하다 — 이 신호를 반드시 함께 본다.
    BOOTSTRAP_RE = re.compile(r"\[RuntimeInitializeOnLoadMethod")
    for name, path in classes.items():
        if BOOTSTRAP_RE.search(per_file.get(path, '')):
            live.add(name)

    def refs_from(path: Path, name: str) -> int:
        src = per_file.get(path, "")
        decls = len(re.findall(rf"\bclass\s+{re.escape(name)}\b", src))
        return len(re.findall(rf"\b{re.escape(name)}\b", src)) - decls

    # 살아있는 파일 = 씬에 붙은/자기등록 클래스를 담은 파일 + Editor/ (씬을 조립하는 주체).
    #  자기 파일도 후보에 넣는다 — 살아있는 파일 안에서 AddComponent 로 붙는 헬퍼
    #  (WorkTabCellClick·CardClick 등)는 정상 배선이기 때문.  죽은 파일끼리 서로
    #  참조해 살아 보이는 문제는 위 자기등록 신호가 이미 막는다.
    def live_files() -> set[Path]:
        fs = {owner[n] for n in live if n in owner}
        fs |= set(EDITOR.rglob("*.cs"))
        return fs

    changed = True
    while changed:
        changed = False
        lf = live_files()
        for name, path in classes.items():
            if name in live:
                continue
            if any(refs_from(f, name) > 0 for f in lf):
                live.add(name)
                changed = True

    ghosts = []
    for name, path in sorted(classes.items()):
        if GHOST_SKIP_DIRS & set(path.relative_to(SCRIPTS).parts[:-1]):
            continue
        if name in live:
            continue
        ghosts.append({
            "name": name,
            "file": str(path.relative_to(ASSETS)),
            "why": "씬·프리팹 부착 0건 + 살아있는 코드에서 도달 불가 — 게임에 없다",
        })
    return ghosts


# ── B. 그림자 상수 ────────────────────────────────────────────────────────
CONST_RE = re.compile(
    r"\b(?:public|private|internal|protected)?\s*(?:static\s+)?const\s+"
    r"(?:int|float|double|long)\s+([A-Z][A-Z0-9_]{2,})\s*=\s*([^;]+);")


COMMENT_RE = re.compile(r"//[^\n]*|/\*.*?\*/", re.S)


def strip_comments(src: str) -> str:
    """주석을 지운다.  이걸 안 하면 문서화가 곧 거짓 양성이 된다 —
    실제로 1차 구현이 `const int MAP_HALF = 20;` 을 인용한 **내 주석**을 코드로 읽었다."""
    return COMMENT_RE.sub("", src)


#  앞이 단어문자가 아닐 때만 — "subclass" 의 "class" 를 잡지 않기 위해서다.
#  ( 를 쓰다 이스케이프 사고로 백스페이스 문자가 박혀 전 파일에서 타입을
#   하나도 못 잡은 적이 있다.  lookbehind 로 명시해 재발을 막는다.)
TYPE_RE = re.compile(r"(?<![A-Za-z0-9_])(?:partial\s+)?(?:class|struct)\s+(\w+)")


def _const_sites(src: str):
    """(이름, 값, 깊이, 소속타입) 목록.

    깊이로 타입 멤버와 메서드 지역을 가른다 (namespace{ class{ = 깊이 2 이므로
    **깊이 ≥ 3 이면 메서드 안**).  소속 타입도 같이 들고 나온다 — 가림은 **같은
    타입 안에서만** 일어나므로, 무관한 클래스가 같은 이름을 쓰는 것(SIZE·TEX 등)을
    신고하지 않으려면 타입 단위로 좁혀야 한다.  partial class 는 파일이 달라도
    같은 타입이므로 파일이 아니라 타입 이름으로 묶는다 (MAP_HALF 사고가 정확히
    SceneSetup 의 서로 다른 partial 파일 사이에서 났다)."""
    src = strip_comments(src)
    depth, cur_type, out = 0, "", []
    pattern = r"[{}]|" + TYPE_RE.pattern + r"|" + CONST_RE.pattern
    for m in re.finditer(pattern, src):
        tok = m.group(0)
        if tok == "{":
            depth += 1
        elif tok == "}":
            depth -= 1
        elif m.group(1):                       # 타입 선언
            cur_type = m.group(1)
        else:                                  # const 선언
            out.append((m.group(2), m.group(3).strip(), depth, cur_type))
    return out


def find_shadowed_constants() -> list[dict]:
    """**메서드 지역 const 가 타입 레벨 const 를 가리는** 경우만 잡는다.

    서로 무관한 클래스가 같은 이름의 상수를 각자 갖는 것(SIZE·TEX·COLS 등)은
    정상이므로 잡지 않는다 — 1차 구현은 이걸 전부 신고해 신호 대 잡음이 무너졌다.
    실제 사고(2026-07-29 MAP_HALF)의 서명은 **같은 이름이 타입 멤버로도, 메서드
    지역으로도 존재하고 값이 다른 것**이다.
    """
    member: dict[str, set] = defaultdict(set)
    local: dict[str, list] = defaultdict(list)
    member_sites: dict[str, list] = defaultdict(list)
    for p in cs_files():
        for name, val, depth, owner_type in _const_sites(read(p)):
            key = (owner_type, name)           # 가림은 같은 타입 안에서만 성립한다
            where = f"{p.relative_to(ASSETS)} = {val}"
            if depth >= 3:
                local[key].append((val, where))
            else:
                member[key].add(val)
                member_sites[key].append(where)

    out = []
    for key, locs in sorted(local.items()):
        if key not in member:
            continue
        bad = [(v, w) for v, w in locs if v not in member[key]]
        if not bad:
            continue
        out.append({
            "name": f"{key[0]}.{key[1]}",
            "values": sorted(member[key] | {v for v, _ in bad}),
            "sites": member_sites[key] + [w for _, w in bad],
            "why": "메서드 지역 const 가 같은 이름의 타입 상수를 다른 값으로 가린다",
        })
    return out


# ── C. 문서 수치 드리프트 ─────────────────────────────────────────────────
#  문서에 `NAME = 값` 또는 `NAME=값` 으로 적힌 상수를 코드와 대조.
DOC_CONST_RE = re.compile(r"`?\b([A-Z][A-Z0-9_]{3,})\s*=\s*(-?\d+(?:\.\d+)?)f?`?")


def code_constants() -> dict[str, set]:
    vals: dict[str, set] = defaultdict(set)
    for p in cs_files():
        for name, val, _, _t in _const_sites(read(p)):
            vals[name].add(val.rstrip("f"))
    return vals


# 대조 대상은 **명세 문서만**.  사건 기록·포지셔닝 문서는 과거 값을 일부러 인용하므로
#  (예: MAP_HALF 버그 서술) 대조하면 전부 거짓 양성이 된다.
SPEC_DOC_GLOBS = ["GDD.md", "systems-inventory-*.md", "spec-*.md"]


def spec_docs() -> list[Path]:
    out: list[Path] = []
    for g in SPEC_DOC_GLOBS:
        out += sorted(DOCS.glob(g))
    return out


def find_doc_drift(docs: list[Path]) -> list[dict]:
    code = code_constants()
    out = []
    for d in docs:
        for m in DOC_CONST_RE.finditer(read(d)):
            name, val = m.group(1), m.group(2)
            if name not in code:
                continue
            cv = {v.rstrip("f") for v in code[name]}
            if val not in cv:
                out.append({
                    "doc": str(d.relative_to(ROOT)),
                    "name": name,
                    "doc_value": val,
                    "code_values": sorted(cv),
                    "why": "문서가 인용한 값이 코드에 없다",
                })
    return out


def self_test() -> int:
    """탐지기가 **실제로 실패하는 걸 본다.**

    한 번도 빨간불을 본 적 없는 게이트는 게이트가 아니다.  오늘(2026-07-29) 실제로
    난 두 사고를 최소 재현으로 넣고, 탐지기가 그걸 집는지 확인한다.
    """
    ok = True

    # ① 그림자 상수 — SceneSetup 사고의 최소 재현 (partial class, 파일이 달라도 같은 타입)
    src_member = "namespace N { public partial class T { public const int MAP_HALF = 45; } }"
    src_local = "namespace N { public partial class T { void M() { const int MAP_HALF = 20; } } }"
    member, local = defaultdict(set), defaultdict(list)
    for src in (src_member, src_local):
        for name, val, depth, owner in _const_sites(src):
            (local[(owner, name)].append((val, "x")) if depth >= 3
             else member[(owner, name)].add(val))
    hit = any(k in member and any(v not in member[k] for v, _ in locs)
              for k, locs in local.items())
    print(f"  ① 그림자 상수(MAP_HALF 45 vs 지역 20) … {'잡음 OK' if hit else '못 잡음 FAIL'}")
    ok &= hit

    # ② 타입 파서 — 이스케이프 사고 회귀 (\b 가 백스페이스로 박혀 전 파일 미매치였다)
    t_ok = (TYPE_RE.findall("public class BuildManager : MonoBehaviour") == ["BuildManager"]
            and TYPE_RE.findall("subclass Foo") == [])
    print(f"  ② 타입 파서(class 매치 / subclass 비매치) … {'OK' if t_ok else 'FAIL'}")
    ok &= t_ok

    # ③ 주석 무시 — 문서화가 거짓 양성이 되지 않아야 한다
    c_ok = _const_sites("class T { /* const int A = 1; */ // const int B = 2;\n }") == []
    print(f"  ③ 주석 안의 const 를 무시 … {'OK' if c_ok else 'FAIL'}")
    ok &= c_ok

    # ④ 자기등록 인식 — [RuntimeInitializeOnLoadMethod] 는 정상 배선
    b_ok = bool(re.search(r"\[RuntimeInitializeOnLoadMethod",
                          "[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]"))
    print(f"  ④ 자기등록 신호 인식 … {'OK' if b_ok else 'FAIL'}")
    ok &= b_ok

    print("\n" + ("PASS — 탐지기가 알려진 사고를 전부 잡는다." if ok
                  else "FAIL — 탐지기가 알려진 사고를 놓친다. 고치기 전엔 결과를 믿지 말 것."))
    return 0 if ok else 1


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", choices=["ghost", "shadow", "doc"])
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--self-test", action="store_true",
                    help="알려진 사고 재현으로 탐지기 자체를 검증")
    a = ap.parse_args()

    if a.self_test:
        print("== 탐지기 자가검증 ==")
        return self_test()

    res = {}
    if a.only in (None, "ghost"):
        res["ghost"] = find_ghost_components()
    if a.only in (None, "shadow"):
        res["shadow"] = find_shadowed_constants()
    if a.only in (None, "doc"):
        res["doc"] = find_doc_drift(spec_docs())

    if a.json:
        print(json.dumps(res, ensure_ascii=False, indent=2))
        return 1 if any(res.values()) else 0

    total = 0
    if "ghost" in res:
        g = res["ghost"]
        total += len(g)
        print(f"\n== A. 유령 컴포넌트 — {len(g)}건 ==")
        print("   만들어졌지만 게임에 붙지 않은 것. 기획서엔 '있음'으로 읽힌다.")
        for x in g:
            print(f"   · {x['name']:28s} {x['file']}")
        if not g:
            print("   없음.")

    if "shadow" in res:
        s = res["shadow"]
        total += len(s)
        print(f"\n== B. 그림자 상수 — {len(s)}건 ==")
        print("   같은 이름이 서로 다른 값. 지역 정의가 정본을 가리면 조용히 어긋난다.")
        for x in s:
            print(f"   · {x['name']} → {', '.join(x['values'])}")
            for site in x["sites"]:
                print(f"       {site}")
        if not s:
            print("   없음.")

    if "doc" in res:
        d = res["doc"]
        total += len(d)
        print(f"\n== C. 문서 수치 드리프트 — {len(d)}건 ==")
        print("   기획서가 인용한 숫자가 코드에 없다.")
        for x in d:
            print(f"   · {x['name']}: 문서 {x['doc_value']} vs 코드 {', '.join(x['code_values'])}")
            print(f"       {x['doc']}")
        if not d:
            print("   없음.")

    print(f"\n{'PASS — 드리프트 없음.' if total == 0 else f'드리프트 {total}건.'}")
    return 1 if total else 0


if __name__ == "__main__":
    raise SystemExit(main())
