#!/usr/bin/env python3
"""yt-delete.py — delete a video from the channel by ID.

Exists so that REPLACING an uploaded video is a first-class, one-command
operation. Replacement order is not negotiable:

    delete the superseded video  ->  THEN upload the corrected one

Uploading the fix first leaves a duplicate on a live channel, which confuses
viewers, splits the algorithm's signal, and reads as spam. A scheduled/private
video counts — it is still on the channel.

Usage:
  python scripts/yt-delete.py <videoId> [<videoId> ...]
  python scripts/yt-delete.py --list          # recent uploads, to find the ID

Credentials: the same OAuth cache yt-batch-upload.sh uses.
"""

import json
import pathlib
import sys
import urllib.error
import urllib.parse
import urllib.request

CANDIDATE_DIRS = [
    pathlib.Path("G:/config/youtubeuploader"),
    pathlib.Path.home() / ".config" / "youtubeuploader",
]


def creds():
    import os
    for d in ([pathlib.Path(os.environ["YT_CONFIG_DIR"])]
              if os.environ.get("YT_CONFIG_DIR") else []) + CANDIDATE_DIRS:
        tok, sec = d / "request.token", d / "client_secrets.json"
        if tok.is_file() and sec.is_file():
            return (json.loads(tok.read_text(encoding="utf-8")),
                    json.loads(sec.read_text(encoding="utf-8")))
    sys.exit("[yt-delete] no OAuth cache found (request.token + client_secrets.json)")


def access_token():
    tok, sec = creds()
    k = "installed" if "installed" in sec else "web"
    body = urllib.parse.urlencode({
        "client_id": sec[k]["client_id"],
        "client_secret": sec[k]["client_secret"],
        "refresh_token": tok["refresh_token"],
        "grant_type": "refresh_token",
    }).encode()
    req = urllib.request.Request(
        "https://oauth2.googleapis.com/token", data=body,
        headers={"Content-Type": "application/x-www-form-urlencoded"})
    with urllib.request.urlopen(req, timeout=60) as r:
        fresh = json.loads(r.read())
    return fresh["access_token"], fresh.get("scope", "")


def api(at, url, method="GET"):
    req = urllib.request.Request(url, method=method,
                                 headers={"Authorization": f"Bearer {at}"})
    with urllib.request.urlopen(req, timeout=60) as r:
        raw = r.read()
        return r.status, (json.loads(raw) if raw else None)


def list_recent(at, n=15):
    _, ch = api(at, "https://www.googleapis.com/youtube/v3/channels"
                    "?part=contentDetails&mine=true")
    uploads = ch["items"][0]["contentDetails"]["relatedPlaylists"]["uploads"]
    _, pl = api(at, "https://www.googleapis.com/youtube/v3/playlistItems"
                    f"?part=snippet,status&maxResults={n}&playlistId={uploads}")
    for it in pl.get("items", []):
        s = it["snippet"]
        print(f"  {s['resourceId']['videoId']}  {s['publishedAt'][:16]}  "
              f"{s['title'][:64]}")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    at, scope = access_token()
    if "--list" in sys.argv:
        print(f"[yt-delete] scope: {scope}")
        list_recent(at)
        return
    if not args:
        sys.exit(__doc__)
    for vid in args:
        try:
            status, _ = api(at, f"https://www.googleapis.com/youtube/v3/videos?id={vid}",
                            method="DELETE")
            print(f"[yt-delete] {vid} -> HTTP {status} (deleted)")
        except urllib.error.HTTPError as e:
            print(f"[yt-delete] {vid} -> HTTP {e.code}: "
                  f"{e.read()[:300].decode('utf-8', 'replace')}")
            sys.exit(1)


if __name__ == "__main__":
    main()
