#!/usr/bin/env python3
"""저작권 민감 단어 제거 (운영자 요청 2026-06-02).
대상: skills/game-prototype 의 .cs(Scripts/Editor) + .md, 루트 README.md/docs.
제외: Library/, builds/, Temp/, obj/, *.meta.
- 트레이드마크 RimWorld/림월드 → 일반 장르 표현
- 제작자/스튜디오 Tynan / Ludeon → 일반 표현
- 위키 URL rimworldwiki.com → '장르 위키'
- 스토리텔러 캐릭터명 Cassandra/Phoebe/Randy → Steady/Calm/Chaos (enum 멤버 일괄 rename)
"""
import re, sys, os, glob

ROOT = "G:/ai/MelonS-Agents"
GP = os.path.join(ROOT, "skills/game-prototype")

# (pattern, repl, ignorecase) — 순서 중요(긴/특수 형태 먼저)
RULES = [
    # 1) 위키 URL (bare RimWorld 규칙보다 먼저)
    (r'https?://(?:www\.)?rimworldwiki\.com/\S*', '콜로니심 장르 위키', True),
    (r'rimworldwiki\.com/\S*', '콜로니심 장르 위키', True),
    (r'rimworldwiki\.com', '콜로니심 장르 위키', True),
    # 2) 제작자/스튜디오
    (r'RimWorld\s*\(Ludeon Studios\)', '콜로니 심 장르', False),
    (r'Ludeon Studios', 'the studio', False),
    (r'Ludeon', 'the studio', False),
    (r'Tynan Sylvester', 'the designer', False),
    (r'Tynan', 'the designer', False),
    # 3) 해시태그 (bare 규칙보다 먼저)
    (r'#RimWorld', '#콜로니심', True),
    (r'#림월드', '#콜로니심', True),
    # 4) RimWorld 컴파운드
    (r'RimWorld[- ]vanilla', 'vanilla colony-sim', True),
    (r'RimWorld[- ]?fidelity', 'genre fidelity', True),
    (r'RimWorld[- ]?style', 'colony-sim-style', True),
    (r'RimWorld[- ]?like', 'colony-sim-like', True),
    (r'RimWorld[- ]lite', 'colony-sim-lite', True),
    (r'RimWorld\s*바닐라', '바닐라 콜로니심', False),
    (r'RimWorld\s*정합', '장르 정합', False),
    (r'RimWorld\s*풍', '콜로니심풍', False),
    (r'RimWorld\s*식', '콜로니심식', False),
    (r'림월드\s*풍', '콜로니심풍', False),
    (r'림월드\s*바닐라', '바닐라 콜로니심', False),
    (r'림월드\s*식', '콜로니심식', False),
    (r'림월드', '레퍼런스 콜로니심', False),
    # 5) bare RimWorld (나머지)
    (r'RIMWORLD', 'THE REFERENCE SIM', False),
    (r'RimWorld', 'the reference sim', False),
    (r'Rimworld', 'the reference sim', False),
    (r'rimworld', 'reference-sim', False),
    # 6) 스토리텔러 캐릭터명 (whole-word)
    (r'\bCassandra\b', 'Steady', False),
    (r'\bPhoebe\b', 'Calm', False),
    (r'\bRandy\b', 'Chaos', False),
]

def gather():
    files = []
    for base, pats in [
        (os.path.join(GP, "unity-project/Assets/Scripts"), ["**/*.cs"]),
        (os.path.join(GP, "unity-project/Assets/Editor"), ["**/*.cs"]),
        (os.path.join(GP, "unity-project/Assets/Sprites"), ["*.py"]),
        (os.path.join(GP, "unity-project/Assets/Audio"), ["*.py"]),
        (os.path.join(GP, "unity-project/Assets/Scenes"), ["*.unity"]),
        (GP, ["*.md", "docs/**/*.md"]),
        (ROOT, ["README.md"]),
    ]:
        for pat in pats:
            files += glob.glob(os.path.join(base, pat), recursive=True)
    # 제외
    out = []
    for f in files:
        n = f.replace("\\", "/")
        if any(x in n for x in ["/Library/", "/builds/", "/Temp/", "/obj/", ".meta"]):
            continue
        out.append(f)
    return sorted(set(out))

def main():
    dry = "--apply" not in sys.argv
    files = gather()
    total = 0
    changed = []
    for f in files:
        try:
            with open(f, "r", encoding="utf-8") as fh:
                txt = fh.read()
        except (UnicodeDecodeError, IsADirectoryError):
            continue
        orig = txt
        cnt = 0
        for pat, repl, ic in RULES:
            flags = re.IGNORECASE if ic else 0
            txt, n = re.subn(pat, repl, txt, flags=flags)
            cnt += n
        if cnt:
            changed.append((f, cnt))
            total += cnt
            if not dry:
                with open(f, "w", encoding="utf-8") as fh:
                    fh.write(txt)
    mode = "DRY-RUN" if dry else "APPLIED"
    print(f"=== {mode}: {len(changed)} files, {total} replacements ===")
    for f, c in sorted(changed, key=lambda x: -x[1]):
        print(f"  {c:4d}  {f.replace(ROOT+'/','')}")

if __name__ == "__main__":
    main()
