#!/usr/bin/env python3
# yt-analytics-query.py — 영상별 상세 지표(리텐션·평균시청·트래픽소스) 조회.
#   python yt-analytics-query.py <videoId> [videoId ...]
import json, os, sys, urllib.request, urllib.parse
from pathlib import Path

# Same env contract as yt-analytics-auth.py — the machine-specific default is
# only a fallback (see .env.example: YT_CONFIG_DIR).
CONF = Path(os.environ.get("YT_CONFIG_DIR", "G:/config/youtubeuploader"))
cs = json.loads((CONF/"client_secrets.json").read_text(encoding="utf-8"))["installed"]
tok = json.loads((CONF/"analytics.token").read_text(encoding="utf-8"))
data = urllib.parse.urlencode({
    "client_id": cs["client_id"], "client_secret": cs["client_secret"],
    "refresh_token": tok["refresh_token"], "grant_type": "refresh_token",
}).encode()
AT = json.loads(urllib.request.urlopen("https://oauth2.googleapis.com/token", data=data).read())["access_token"]

def q(params):
    url = "https://youtubeanalytics.googleapis.com/v2/reports?" + urllib.parse.urlencode(params)
    r = urllib.request.Request(url, headers={"Authorization": f"Bearer {AT}"})
    return json.loads(urllib.request.urlopen(r).read())

DR = {"startDate": "2026-01-01", "endDate": "2026-07-07"}

def core(vid):
    d = q({**DR, "ids": "channel==MINE", "filters": f"video=={vid}",
           "metrics": "views,estimatedMinutesWatched,averageViewDuration,averageViewPercentage,likes,shares,subscribersGained,comments"})
    hdr = [h["name"] for h in d["columnHeaders"]]
    row = d["rows"][0] if d.get("rows") else [0]*len(hdr)
    return dict(zip(hdr, row))

def traffic(vid):
    d = q({**DR, "ids": "channel==MINE", "filters": f"video=={vid}",
           "dimensions": "insightTrafficSourceType", "metrics": "views", "sort": "-views"})
    return [(r[0], r[1]) for r in d.get("rows", [])]

for vid in sys.argv[1:]:
    c = core(vid)
    print(f"\n=== {vid} ===")
    print(f"  조회수 {int(c['views'])} | 평균시청 {c['averageViewDuration']}s | "
          f"리텐션 {c['averageViewPercentage']:.1f}% | 좋아요 {int(c['likes'])} | "
          f"공유 {int(c['shares'])} | 구독전환 {int(c['subscribersGained'])} | 댓글 {int(c['comments'])}")
    tr = traffic(vid)
    tot = sum(v for _, v in tr) or 1
    print("  트래픽 소스:", ", ".join(f"{t}={v}({100*v/tot:.0f}%)" for t, v in tr))
