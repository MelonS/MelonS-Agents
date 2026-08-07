# -*- coding: utf-8 -*-
"""check-scene-overrides.py — **씬에 굳어 있어 코드 수정이 먹지 않는 값**을 찾는다.

계기 (2026-08-07): 재미 점수의 '긴장' 축을 올리려고 `AIDirector.RaidGraceDays`
기본값을 2 → 1 로 고쳤는데, 두 번 측정해도 습격이 0건이었다.  원인은 코드가
아니었다 —

```
Assets/Scenes/Game.unity:
  RaidGraceDays: 2      ← 씬에 직렬화된 값이 코드 기본값을 덮는다
```

`[SerializeField]` 필드는 씬에 값이 저장돼 있으면 **코드의 초기값이 무시된다.**
그런데 실패가 조용하다 — 컴파일도 되고 게이트도 통과하고, 그냥 아무 일이 안
일어난다.  이 레포에서 같은 함정을 여러 번 밟았다:

  · 동물 스탯 · 광맥 스탯 · 수종 · 스폰 좌표 (씬 값이 코드보다 우선)
  · 가구 그림자 자식 (`m_RemovedGameObjects` 로 씬에서 제거돼 있었다)
  · 그리고 이번 `RaidGraceDays`

**사람 눈으로는 안 보인다.**  코드를 읽으면 1 이라고 쓰여 있기 때문이다.
그래서 검사로 잡는다.

검출 방법: `[SerializeField]`(및 public 필드)의 **코드 기본값**을 파싱하고,
씬 YAML 에 같은 이름으로 저장된 값과 비교해 **다르면 보고**한다.
다른 것 자체는 정상일 수 있다(의도적 씬 튜닝).  이 검사가 말하는 것은
"코드를 고쳐도 안 바뀌는 값이 여기 있다" 이고, 판단은 사람이 한다.

usage:
  python check-scene-overrides.py            # 차이 보고
  python check-scene-overrides.py --strict   # 차이가 있으면 exit 1 (게이트용)
"""
from __future__ import annotations
import sys
import re
from pathlib import Path
from collections import defaultdict

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
PROJ = HERE.parent / "unity-project"
SCRIPTS = PROJ / "Assets" / "Scripts"
SCENES = sorted((PROJ / "Assets" / "Scenes").glob("*.unity"))

# `[SerializeField] private float Foo = 1.5f;`  /  `public int Bar = 3;`
FIELD = re.compile(
    r"(?:\[SerializeField\]\s*)?(?:private|public|protected|internal)\s+"
    r"(?:readonly\s+)?(int|float|bool)\s+([A-Za-z_]\w*)\s*=\s*"
    r"(-?[\d.]+f?|true|false)\s*;")


def norm(v: str):
    v = v.strip().rstrip("f")
    if v == "true":
        return 1.0
    if v == "false":
        return 0.0
    try:
        return float(v)
    except ValueError:
        return None


def code_defaults():
    """스크립트별 { 필드명: 기본값 }.  같은 이름이 여러 파일에 있으면 파일 단위로 둔다."""
    out = {}
    for cs in SCRIPTS.rglob("*.cs"):
        if "/Tests/" in cs.as_posix() or "/Editor/" in cs.as_posix():
            continue
        txt = cs.read_text(encoding="utf-8", errors="replace")
        # `[SerializeField]` 가 붙은 것만 씬에 직렬화된다 — public 필드도 되지만
        #  MonoBehaviour 가 아닌 클래스의 상수까지 잡으면 잡음이 커진다.
        fields = {}
        for m in FIELD.finditer(txt):
            line_start = txt.rfind("\n", 0, m.start()) + 1
            prefix = txt[max(0, line_start - 120):m.start()]
            if "[SerializeField]" not in prefix and not m.group(0).lstrip().startswith("public"):
                continue
            v = norm(m.group(3))
            if v is not None:
                fields[m.group(2)] = v
        if fields:
            out[cs.stem] = fields
    return out


def scene_values():
    """씬별 [ (컴포넌트 블록 텍스트) ] → { 필드명: 값 } 목록."""
    out = defaultdict(list)
    for sc in SCENES:
        txt = sc.read_text(encoding="utf-8", errors="replace")
        for block in txt.split("\n--- !u!"):
            vals = {}
            for line in block.splitlines():
                m = re.match(r"\s{2}([A-Za-z_]\w*):\s*(-?[\d.]+|true|false)\s*$", line)
                if m:
                    v = norm(m.group(2))
                    if v is not None:
                        vals[m.group(1)] = v
            if vals:
                out[sc.name].append(vals)
    return out


def main() -> int:
    strict = "--strict" in sys.argv
    defaults = code_defaults()
    scenes = scene_values()

    # Unity 내장 필드(m_ 접두 등)는 비교 대상이 아니다.
    SKIP = {"m_ObjectHideFlags", "serializedVersion"}

    hits = []
    for scene, blocks in scenes.items():
        for vals in blocks:
            for script, fields in defaults.items():
                # 이 블록이 이 스크립트의 것인지: 필드 이름이 2개 이상 겹치면 그 스크립트로 본다
                common = [k for k in fields if k in vals and k not in SKIP]
                if len(common) < 2:
                    continue
                for k in common:
                    if abs(fields[k] - vals[k]) > 1e-6:
                        hits.append((scene, script, k, fields[k], vals[k]))

    if not hits:
        print("[scene-override] OK — 코드 기본값과 씬 값이 일치")
        return 0

    print(f"[scene-override] 코드 기본값과 다른 씬 값 {len(hits)}건")
    print("  (다른 것 자체는 정상일 수 있다 — 다만 **코드를 고쳐도 이 값은 안 바뀐다**)")
    cur = None
    for scene, script, k, code_v, scene_v in sorted(hits):
        if (scene, script) != cur:
            cur = (scene, script)
            print(f"\n  {scene} · {script}")
        print(f"    {k:28s} 코드 {code_v:<10g} 씬 {scene_v:<10g}")
    print("\n  고치려면 **씬 값을** 바꿔야 한다 (코드만 고치면 조용히 무시된다).")
    return 1 if strict else 0


if __name__ == "__main__":
    sys.exit(main())
