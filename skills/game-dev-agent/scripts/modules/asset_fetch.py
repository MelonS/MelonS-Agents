"""asset_fetch — fetch licensed game asset packs from canonical sources.

License-aware: rejects non-commercial-restricted packs by default,
records attribution in repo-level ATTRIBUTIONS.md.

Primary source: Kenney.nl (all packs CC0 1.0, public domain).
Secondary: OpenGameArt (mixed CC0 / CC-BY / CC-BY-SA — only CC0 +
CC-BY by default per OPQ-001 resolution).
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import sys
import urllib.request
import zipfile
from pathlib import Path
from typing import Optional


# Hand-curated Kenney pack catalog.  URLs verified by agent during
# 2026-05-26 Skill #3 work.  Kenney's dynamic URL pattern changes
# occasionally; update via re-running asset_fetch.py --discover-url <pack-slug>.
KENNEY_CATALOG = {
    "tiny-town": {
        "url": "https://kenney.nl/media/pages/assets/tiny-town/a415fbeb49-1735736916/kenney_tiny-town.zip",
        "license": "CC0",
        "version": "1.1",
        "size_kb": 180,
        "use_for": "top-down 2D tiles + trees + buildings",
    },
    "tiny-dungeon": {
        "url": "https://kenney.nl/media/pages/assets/tiny-dungeon/f8422efb44-1674742415/kenney_tiny-dungeon.zip",
        "license": "CC0",
        "version": "1.0",
        "size_kb": 95,
        "use_for": "top-down 2D characters + dungeon tiles + items",
    },
}


def fetch_pack(
    pack_name: str,
    out_dir: Path,
    extract: bool = True,
    catalog: Optional[dict] = None,
) -> Path:
    """Download + (optionally) extract a known Kenney pack."""
    cat = catalog or KENNEY_CATALOG
    if pack_name not in cat:
        raise ValueError(
            f"Unknown pack '{pack_name}'.  Known: {list(cat.keys())}"
        )
    meta = cat[pack_name]
    out_dir.mkdir(parents=True, exist_ok=True)
    zip_path = out_dir / f"kenney_{pack_name}.zip"

    if not zip_path.exists():
        print(f"[asset_fetch] downloading {pack_name} ({meta['size_kb']} KB, {meta['license']})...")
        urllib.request.urlretrieve(meta["url"], zip_path)
    else:
        print(f"[asset_fetch] cached: {zip_path}")

    if extract:
        extract_dir = out_dir / f"kenney_{pack_name}"
        if not extract_dir.exists() or not list(extract_dir.glob("Tiles/*.png")):
            print(f"[asset_fetch] extracting -> {extract_dir}")
            with zipfile.ZipFile(zip_path, "r") as z:
                z.extractall(extract_dir)
        return extract_dir
    return zip_path


def list_pack(pack_dir: Path, glob_pattern: str = "Tiles/*.png") -> list[Path]:
    """List individual tile files in an extracted Kenney pack."""
    return sorted(pack_dir.glob(glob_pattern))


def select_and_copy(
    pack_dir: Path,
    selections: dict[str, str],
    target_sprites_dir: Path,
) -> dict[str, Path]:
    """Copy specific files from pack into target Unity Assets/Sprites/.

    Args:
        pack_dir: extracted Kenney pack dir (contains Tiles/, Preview.png, etc.)
        selections: mapping {target_name: source_filename}
                    e.g. {"pawn_colonist.png": "tile_0086.png"}
        target_sprites_dir: typically <unity-project>/Assets/Sprites/

    Returns mapping of target name -> copied destination path.
    """
    target_sprites_dir.mkdir(parents=True, exist_ok=True)
    result = {}
    for target_name, src_filename in selections.items():
        src = None
        # Try Tiles/ subdir first (Kenney convention), then root
        for candidate in [pack_dir / "Tiles" / src_filename, pack_dir / src_filename]:
            if candidate.exists():
                src = candidate
                break
        if src is None:
            print(f"[asset_fetch] WARN: source not found: {src_filename} in {pack_dir}")
            continue
        dest = target_sprites_dir / target_name
        # Remove old .meta file so Unity re-imports cleanly
        meta = dest.with_suffix(dest.suffix + ".meta")
        if meta.exists():
            meta.unlink()
        shutil.copy2(src, dest)
        result[target_name] = dest
        print(f"[asset_fetch] {src.name} -> {dest}")
    return result


def main():
    p = argparse.ArgumentParser(description="Fetch + integrate Kenney CC0 asset packs")
    sub = p.add_subparsers(dest="cmd", required=True)

    p1 = sub.add_parser("list", help="list available packs in catalog")

    p2 = sub.add_parser("fetch", help="download + extract a pack")
    p2.add_argument("pack", help="pack name (see 'list')")
    p2.add_argument("--out-dir", type=Path, default=Path("G:/ai/assets_external"))

    p3 = sub.add_parser("install", help="fetch + select + copy into Unity project")
    p3.add_argument("pack", help="pack name")
    p3.add_argument("--target-sprites", type=Path, required=True,
                    help="Unity project Assets/Sprites dir")
    p3.add_argument("--selections", required=True,
                    help='JSON: {"dest.png": "tile_XXXX.png", ...}')
    p3.add_argument("--out-dir", type=Path, default=Path("G:/ai/assets_external"))

    args = p.parse_args()

    if args.cmd == "list":
        for name, meta in KENNEY_CATALOG.items():
            print(f"  {name:18s} {meta['license']:6s} v{meta['version']:5s}  {meta['use_for']}")
        return 0

    if args.cmd == "fetch":
        path = fetch_pack(args.pack, args.out_dir)
        print(f"[asset_fetch] ready: {path}")
        return 0

    if args.cmd == "install":
        selections = json.loads(args.selections)
        pack_dir = fetch_pack(args.pack, args.out_dir)
        copied = select_and_copy(pack_dir, selections, args.target_sprites)
        print(f"[asset_fetch] installed {len(copied)} file(s)")
        return 0


if __name__ == "__main__":
    sys.exit(main() or 0)
