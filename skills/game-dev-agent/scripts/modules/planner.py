"""planner — natural-language game spec → genre spec (loaded from YAML).

Genre templates live in `skills/game-dev-agent/genres/*.yaml`.  Adding a
new genre = adding a new YAML file.  NO code change.  This is what
makes the agent capable of building **any** game, not just the 3
hard-coded ones from earlier iterations.

Schema documented at the top of each YAML file (see
`genres/colony-sim-lite.yaml`).

Future [OPQ-002]: when `ANTHROPIC_API_KEY` is set, planner falls back
to Claude API for arbitrary specs that don't keyword-match any
existing YAML — Claude proposes a new YAML, operator approves, saved
to disk, becomes a permanent genre.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Optional, Dict, Any

try:
    import yaml
except ImportError as e:
    raise SystemExit(
        "planner requires PyYAML.  Install via: pip install PyYAML"
    ) from e


GENRES_DIR = Path(__file__).resolve().parent.parent.parent / "genres"


@dataclass
class GenreSpec:
    name: str
    description: str = ""
    match_keywords: List[str] = field(default_factory=list)
    vision: Dict[str, str] = field(default_factory=dict)
    team: List[str] = field(default_factory=list)
    sprites: List[str] = field(default_factory=list)
    scripts: List[str] = field(default_factory=list)
    scenes: List[str] = field(default_factory=list)
    systems: List[str] = field(default_factory=list)
    audio: List[str] = field(default_factory=list)
    days_estimate: int = 7
    # backward-compat alias for old code that read .genre
    @property
    def genre(self) -> str:
        return self.name


def load_all_genres(genres_dir: Optional[Path] = None) -> Dict[str, GenreSpec]:
    """Discover + parse every *.yaml in genres/.  Returns {slug: GenreSpec}."""
    gdir = genres_dir or GENRES_DIR
    if not gdir.exists():
        raise FileNotFoundError(f"genres dir not found: {gdir}")
    out: Dict[str, GenreSpec] = {}
    for path in sorted(gdir.glob("*.yaml")):
        with open(path, "r", encoding="utf-8") as f:
            data = yaml.safe_load(f) or {}
        slug = data.get("name") or path.stem
        out[slug] = GenreSpec(
            name=slug,
            description=data.get("description", ""),
            match_keywords=[str(k) for k in data.get("match_keywords", [])],
            vision=dict(data.get("vision", {})),
            team=list(data.get("team", [])),
            sprites=list(data.get("sprites", [])),
            scripts=list(data.get("scripts", [])),
            scenes=list(data.get("scenes", [])),
            systems=list(data.get("systems", [])),
            audio=list(data.get("audio", [])),
            days_estimate=int(data.get("days_estimate", 7)),
        )
    return out


def plan(spec_nl: str, genres_dir: Optional[Path] = None) -> GenreSpec:
    """Pick a genre by keyword match against the natural-language spec.

    Day-1 implementation: substring match.  Future [OPQ-002]: when no
    keyword matches, fall back to Claude API for novel genre creation.
    """
    catalog = load_all_genres(genres_dir)
    nl = spec_nl.lower()
    for slug, gs in catalog.items():
        for kw in gs.match_keywords:
            # YAML can auto-cast bare keywords like "2048" to int;
            # str() defensively before .lower().
            if str(kw).lower() in nl:
                return gs
    raise ValueError(
        f"No genre YAML matches '{spec_nl}'.  Known genres: "
        f"{sorted(catalog.keys())}.  Add a new genre by dropping a "
        f"YAML into {GENRES_DIR} (no code change).  Or wait for "
        f"Claude-API fallback (OPQ-002)."
    )


def print_plan(p: GenreSpec):
    print(f"=== Plan: {p.name} ===")
    if p.description:
        print(f"  {p.description}")
    if p.vision:
        print(f"  Vision:")
        for k, v in p.vision.items():
            print(f"    {k}: {v}")
    print(f"  Days estimate: {p.days_estimate}")
    if p.team:
        print(f"  Team ({len(p.team)}): {', '.join(p.team)}")
    print(f"  Sprites ({len(p.sprites)}): {', '.join(p.sprites)}")
    print(f"  Scripts ({len(p.scripts)}): {', '.join(p.scripts)}")
    print(f"  Scenes ({len(p.scenes)}): {', '.join(p.scenes)}")
    print(f"  Audio ({len(p.audio)}): {', '.join(p.audio)}")
    print(f"  Systems ({len(p.systems)}):")
    for s in p.systems:
        print(f"    - {s}")


if __name__ == "__main__":
    import sys
    nl = " ".join(sys.argv[1:]) if len(sys.argv) > 1 else "colony-sim-lite"
    p = plan(nl)
    print_plan(p)
