"""planner — natural-language game spec → task list.

Day 1 implementation: hard-coded templates per known genre.  Future
[OPQ-002]: invoke Claude API for arbitrary specs.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import List


@dataclass
class GenreSpec:
    genre: str
    sprites: List[str] = field(default_factory=list)
    scripts: List[str] = field(default_factory=list)
    scenes: List[str] = field(default_factory=list)
    systems: List[str] = field(default_factory=list)
    audio: List[str] = field(default_factory=list)
    days_estimate: int = 7


GENRE_TEMPLATES = {
    "rimworld-lite": GenreSpec(
        genre="rimworld-lite",
        sprites=["pawn_colonist", "tile_grass", "tree"],
        scripts=[
            "MainMenuController", "GameManager", "PawnEntity",
            "ClickSelector", "PawnMovement", "PawnNeeds",
            "PawnInfoPanel", "TreeEntity", "PawnChopper",
            "ResourceManager", "ResourceCounterUI", "PawnUtilityAI",
            "AIDirector", "EventLogUI", "SaveLoadManager",
            "GameSaveButtons", "AudioBank",
        ],
        scenes=["MainMenu", "Game"],
        systems=[
            "tilemap rendering", "pawn movement (no pathfind)",
            "needs decay", "tree chop action", "wood economy",
            "utility AI auto-task", "random events",
            "save/load JSON",
        ],
        audio=["chop SFX", "select SFX"],
        days_estimate=7,
    ),
    "vampire-survivors-lite": GenreSpec(
        genre="vampire-survivors-lite",
        sprites=["player", "enemy_small", "enemy_med", "projectile", "xp_gem"],
        scripts=[
            "PlayerMovement", "PlayerHealth", "AutoShooter",
            "Projectile", "EnemyEntity", "EnemyAI", "EnemySpawner",
            "WaveManager", "XPGem", "LevelUpUI", "WeaponUpgrade",
        ],
        scenes=["MainMenu", "Game"],
        systems=[
            "WASD movement", "auto-aim nearest enemy",
            "wave spawn timer", "XP pickup", "level-up choices",
            "HP + iframes",
        ],
        audio=["shoot SFX", "hit SFX", "level-up SFX", "BGM"],
        days_estimate=5,
    ),
    "suika-game-lite": GenreSpec(
        genre="suika-game-lite",
        sprites=["fruit_tier1", "fruit_tier2", "fruit_tier3", "fruit_tier4", "fruit_tier5"],
        scripts=[
            "DropController", "Fruit", "FruitMerger", "GameOver",
            "ScoreManager", "ScoreUI",
        ],
        scenes=["MainMenu", "Game"],
        systems=[
            "physics 2D", "click-to-drop", "collision-merge same-tier",
            "score on merge", "game-over on top-line overflow",
        ],
        audio=["drop SFX", "merge SFX", "BGM"],
        days_estimate=2,
    ),
}


def plan(spec_nl: str) -> GenreSpec:
    """Pick template by keyword match.  Day 1 stub."""
    nl = spec_nl.lower()
    if "rimworld" in nl or "colony" in nl or "pawn" in nl:
        return GENRE_TEMPLATES["rimworld-lite"]
    if "vampire survivors" in nl or "auto-shoot" in nl or "horde survivor" in nl:
        return GENRE_TEMPLATES["vampire-survivors-lite"]
    if "suika" in nl or "merge fruit" in nl or "watermelon game" in nl:
        return GENRE_TEMPLATES["suika-game-lite"]
    raise ValueError(
        f"No template matches '{spec_nl}'.  Day 1 planner knows: "
        f"{list(GENRE_TEMPLATES.keys())}.  Future iterations will use "
        f"Claude API for arbitrary specs (see [OPQ-002] in OPERATOR_QUEUE.md)."
    )


def print_plan(p: GenreSpec):
    print(f"=== Plan: {p.genre} ===")
    print(f"  Days estimate: {p.days_estimate}")
    print(f"  Sprites ({len(p.sprites)}): {', '.join(p.sprites)}")
    print(f"  Scripts ({len(p.scripts)}): {', '.join(p.scripts)}")
    print(f"  Scenes ({len(p.scenes)}): {', '.join(p.scenes)}")
    print(f"  Audio ({len(p.audio)}): {', '.join(p.audio)}")
    print(f"  Systems ({len(p.systems)}):")
    for s in p.systems:
        print(f"    - {s}")


if __name__ == "__main__":
    import sys
    nl = " ".join(sys.argv[1:]) if len(sys.argv) > 1 else "rimworld lite"
    p = plan(nl)
    print_plan(p)
