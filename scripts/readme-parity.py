#!/usr/bin/env python3
"""readme-parity.py — guard README.md <-> README.ko.md against silent drift.

The Korean README is a hand-maintained mirror of the English one, so it keeps
falling out of sync (stale numbers, a missing section, a forgotten link). This
is a deterministic structural + factual parity check, meant to run as a
pre-commit hook and/or in CI. It does NOT judge translation quality (that needs
a human or an LLM pass — see docs/ko-style-guide.md); it catches the mechanical
drift that a script can catch reliably.

Exit 0 = in parity. Exit 1 = drift found (details printed).

Usage:
    readme-parity.py                 # check the working-tree READMEs
    readme-parity.py <EN> <KO>       # check two explicit files — used by the
                                     # pre-commit hook to test the *staged*
                                     # content rather than the working tree
"""
import re
import sys
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
EN = ROOT / "README.md"
KO = ROOT / "README.ko.md"

if len(sys.argv) == 3:
    EN, KO = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
elif len(sys.argv) != 1:
    sys.exit("usage: readme-parity.py [EN_README KO_README]")


def norm_link(t: str) -> str:
    """Map a KO/EN language-variant asset path back to its base form so the two
    READMEs' link sets can be compared. e.g. foo.ko.md -> foo.md,
    intervention-ko.png / intervention-en.png -> intervention.png."""
    t = t.split("#", 1)[0].strip()
    t = re.sub(r"[-.](ko|en)\.", ".", t)
    return t


def local_links(text: str) -> set:
    out = set()
    for m in re.findall(r"\]\(([^)]+)\)", text):
        t = m.split("#", 1)[0].strip()
        if not t or t.startswith("http"):
            continue
        out.add(norm_link(t))
    return out


def sections(text):
    return re.findall(r"(?m)^##\s+(.*)$", text)


def count_images(text):
    return len(re.findall(r"!\[", text))


def count_table_rows(text):
    return len(re.findall(r"(?m)^\|", text))


def count_code_fences(text):
    return text.count("```") // 2


def hero_numbers(text):
    """The 'by the numbers' hero image alt-text is where key stats live in both
    languages; its integer multiset must match (this is what catches 19 vs 24)."""
    for line in text.splitlines():
        if "01-hero-stats" in line:
            return sorted(int(n) for n in re.findall(r"\d+", line))
    return None


def main():
    en, ko = EN.read_text(encoding="utf-8"), KO.read_text(encoding="utf-8")
    fails = []

    # 1. Section count parity (headings differ by language, but the count must match)
    ne, nk = len(sections(en)), len(sections(ko))
    if ne != nk:
        fails.append(f"section count: EN has {ne} '## ' headings, KO has {nk}")

    # 2. Image / table-row / code-fence count parity
    for name, fn in (("images", count_images), ("table rows", count_table_rows),
                     ("code blocks", count_code_fences)):
        e, k = fn(en), fn(ko)
        if e != k:
            fails.append(f"{name}: EN={e} KO={k}")

    # 3. Link-target parity (after mapping -ko/-en/.ko variants to a base form)
    le, lk = local_links(en), local_links(ko)
    if le != lk:
        only_en = sorted(le - lk)
        only_ko = sorted(lk - le)
        if only_en:
            fails.append("links only in EN: " + ", ".join(only_en))
        if only_ko:
            fails.append("links only in KO: " + ", ".join(only_ko))

    # 4. Hero-stat number parity (the load-bearing counts: 24 subagents, 23, 15, ...)
    he, hk = hero_numbers(en), hero_numbers(ko)
    if he is None or hk is None:
        fails.append("hero-stats alt-text not found in one or both READMEs")
    elif he != hk:
        fails.append(f"hero-stat numbers differ: EN={he} KO={hk}")

    if fails:
        print("README parity FAILED (EN <-> KO drift):")
        for f in fails:
            print("  - " + f)
        print("\nFix README.ko.md to match README.md, then re-run: "
              "python scripts/readme-parity.py")
        return 1

    print("README parity OK: EN <-> KO structural + stat parity holds.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
