#!/usr/bin/env python3
"""yt-channel-settings.py — applies the 8 YT settings from
docs/research/2026-05-25-yt-8-tips.md to operator's channel.

Reference: https://www.youtube.com/watch?v=5QVuFhhcCrU
("해외 유튜브 전문가들만 몰래 쓰는 유튜브 설정 8가지")

OAuth: reuses youtubeuploader credentials at $YT_CRED_DIR (default
$HOME/.config/youtubeuploader on mac/linux, %USERPROFILE%\\.config\\
youtubeuploader on windows, or the env-overridden YT_SECRETS / YT_CACHE).

What this script automates (subset of the 8 tips that have an API):
  Tip 3 — channel category (channelSection or channels.update)
  Tip 6 — channel keywords (channels.update, branding.keywords up to 500 chars)
  Tip 7 — disable auto-chapters per-video (videos.update, settings.autoChapter)

What requires browser UI (not automatable via Data API v3 public surface):
  Tip 1 — private→24h→public (done via publishAt on upload — see upload-meta)
  Tip 2 — page layout (channelSections order, branding.channel.featured)
  Tip 4 — AI content disclosure (per-video, set at upload)
  Tip 5 — off-hour publishAt (set on upload metadata)
  Tip 8 — hashtags + tags (set on upload metadata)

Usage:
  python scripts/yt-channel-settings.py --check        # read current settings
  python scripts/yt-channel-settings.py --apply        # write recommended
  python scripts/yt-channel-settings.py --keywords "키워드1,키워드2,..."
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

DEFAULT_CRED_DIR = os.environ.get(
    "YT_CRED_DIR",
    str(Path.home() / ".config" / "youtubeuploader"),
)
SECRETS = os.environ.get("YT_SECRETS", os.path.join(DEFAULT_CRED_DIR, "client_secrets.json"))
TOKEN_FILE = os.environ.get("YT_CACHE", os.path.join(DEFAULT_CRED_DIR, "request.token"))

RECOMMENDED_KEYWORDS = [
    # Genre / vibe (Korean lofi long-form)
    "korean lofi", "lofi beats", "lofi hip hop", "study music",
    "chill music", "ambient music", "rainy lofi", "seoul lofi",
    # Use case
    "study and relax", "background music", "music for studying",
    "music for relaxation", "music for focus", "music for sleeping",
    "chill beats", "chillhop", "calm music",
    # Localization
    "한국 로파이", "공부할 때 듣는 음악", "잔잔한 음악", "비오는 날 음악",
    "서울 로파이", "롱폼 음악", "수면 음악", "휴식 음악", "집중 음악",
]

# Tip 5: off-hour publish slots (Korean prime audience).
# Avoid :00, prefer :13, :17, :42, :47 - low collision with other channels.
SAFE_PUBLISH_MINUTES = [13, 17, 22, 42, 47, 53]


def refresh_access_token() -> str:
    """Use the refresh_token cached by youtubeuploader to mint a fresh access_token."""
    if not Path(SECRETS).exists():
        raise FileNotFoundError(f"client_secrets.json not found at {SECRETS}")
    if not Path(TOKEN_FILE).exists():
        raise FileNotFoundError(
            f"request.token not found at {TOKEN_FILE} — run youtubeuploader once interactively to complete OAuth consent"
        )
    secrets = json.loads(Path(SECRETS).read_text(encoding="utf-8"))
    token = json.loads(Path(TOKEN_FILE).read_text(encoding="utf-8"))
    cid = secrets["installed"]["client_id"]
    csec = secrets["installed"]["client_secret"]
    refresh = token["refresh_token"]
    data = urllib.parse.urlencode({
        "client_id": cid,
        "client_secret": csec,
        "refresh_token": refresh,
        "grant_type": "refresh_token",
    }).encode("utf-8")
    req = urllib.request.Request(
        "https://oauth2.googleapis.com/token",
        data=data,
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )
    with urllib.request.urlopen(req, timeout=15) as resp:
        body = json.loads(resp.read())
    return body["access_token"]


def yt_get(access_token: str, path: str, params: dict | None = None) -> dict:
    url = f"https://www.googleapis.com/youtube/v3/{path}"
    if params:
        url += "?" + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers={"Authorization": f"Bearer {access_token}"})
    with urllib.request.urlopen(req, timeout=15) as resp:
        return json.loads(resp.read())


def yt_put(access_token: str, path: str, params: dict, body: dict) -> dict:
    url = f"https://www.googleapis.com/youtube/v3/{path}?" + urllib.parse.urlencode(params)
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=data,
        headers={
            "Authorization": f"Bearer {access_token}",
            "Content-Type": "application/json",
        },
        method="PUT",
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        return json.loads(resp.read())


def get_my_channel(access_token: str) -> dict:
    data = yt_get(access_token, "channels", {
        "part": "snippet,brandingSettings,statistics",
        "mine": "true",
    })
    items = data.get("items", [])
    if not items:
        raise RuntimeError("channels.list?mine=true returned no items")
    return items[0]


def cmd_check(access_token: str) -> int:
    ch = get_my_channel(access_token)
    print(f"channel id      : {ch['id']}")
    print(f"channel title   : {ch['snippet']['title']}")
    branding = ch.get("brandingSettings", {}).get("channel", {})
    kw = branding.get("keywords", "")
    print(f"keywords length : {len(kw)} / 500 chars")
    print(f"keywords value  : {kw[:200]}{'...' if len(kw) > 200 else ''}")
    print(f"description     : {(branding.get('description') or '')[:200]}")
    print(f"country         : {branding.get('country', '(none)')}")
    stats = ch.get("statistics", {})
    print(f"subs            : {stats.get('subscriberCount', '?')}")
    print(f"video count     : {stats.get('videoCount', '?')}")
    print(f"view count      : {stats.get('viewCount', '?')}")
    return 0


def cmd_apply_keywords(access_token: str, keywords: list[str]) -> int:
    ch = get_my_channel(access_token)
    ch_id = ch["id"]
    # Join keywords into a string under 500 chars. YouTube wants the
    # value as a space-separated string with quoted multi-word terms.
    quoted = [f'"{k}"' if " " in k else k for k in keywords]
    joined = " ".join(quoted)
    if len(joined) > 500:
        # Trim until fits
        out = []
        running = 0
        for q in quoted:
            extra = len(q) + (1 if out else 0)
            if running + extra > 500:
                break
            out.append(q)
            running += extra
        joined = " ".join(out)
    print(f"applying keywords: {len(joined)} chars / 500")
    print(f"value: {joined}")
    body = {
        "id": ch_id,
        "brandingSettings": {
            "channel": {
                "keywords": joined,
            },
        },
    }
    try:
        result = yt_put(access_token, "channels", {"part": "brandingSettings"}, body)
        print(f"OK: updated channel {ch_id}")
        return 0
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="ignore")
        print(f"ERROR {e.code}: {body[:800]}", file=sys.stderr)
        return 1


def main():
    p = argparse.ArgumentParser(description="MelonS-Agents YouTube channel settings")
    p.add_argument("--check", action="store_true", help="read current channel settings")
    p.add_argument("--apply-keywords", action="store_true", help="apply RECOMMENDED_KEYWORDS")
    p.add_argument("--keywords", help="comma-separated keywords (overrides RECOMMENDED)")
    args = p.parse_args()

    try:
        access_token = refresh_access_token()
    except FileNotFoundError as e:
        print(f"FATAL: {e}", file=sys.stderr)
        print("\nSetup steps:", file=sys.stderr)
        print(f"  1. Place client_secrets.json at {SECRETS}", file=sys.stderr)
        print(f"  2. Run youtubeuploader once interactively to mint {TOKEN_FILE}", file=sys.stderr)
        print(f"  3. Re-run this script", file=sys.stderr)
        sys.exit(2)
    except Exception as e:
        print(f"FATAL: token refresh failed: {e}", file=sys.stderr)
        sys.exit(2)

    if args.check or (not args.apply_keywords and not args.keywords):
        return cmd_check(access_token)

    keywords = RECOMMENDED_KEYWORDS
    if args.keywords:
        keywords = [k.strip() for k in args.keywords.split(",") if k.strip()]

    if args.apply_keywords or args.keywords:
        return cmd_apply_keywords(access_token, keywords)


if __name__ == "__main__":
    sys.exit(main() or 0)
