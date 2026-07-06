#!/usr/bin/env python3
# yt-scoreboard.py — 채널 전 영상을 테마별 리텐션·SHORTS%·조회로 스코어보드.
import csv, json, urllib.request, urllib.parse, statistics as st
from pathlib import Path

CONF = Path("G:/config/youtubeuploader")
cs = json.loads((CONF/"client_secrets.json").read_text(encoding="utf-8"))["installed"]
tk = json.loads((CONF/"analytics.token").read_text(encoding="utf-8"))
AT = json.loads(urllib.request.urlopen("https://oauth2.googleapis.com/token", data=urllib.parse.urlencode({
    "client_id": cs["client_id"], "client_secret": cs["client_secret"],
    "refresh_token": tk["refresh_token"], "grant_type": "refresh_token"}).encode()).read())["access_token"]

def q(params):
    u = "https://youtubeanalytics.googleapis.com/v2/reports?" + urllib.parse.urlencode(params)
    return json.loads(urllib.request.urlopen(urllib.request.Request(u, headers={"Authorization": f"Bearer {AT}"})).read())

DR = {"startDate": "2026-01-01", "endDate": "2026-07-07", "ids": "channel==MINE"}
# 영상별 핵심 지표
m = q({**DR, "dimensions": "video", "sort": "-views", "maxResults": 200,
       "metrics": "views,averageViewPercentage,averageViewDuration,shares,subscribersGained,likes"})
hdr = [h["name"] for h in m["columnHeaders"]]
vidx = hdr.index("video")
met = {r[vidx]: dict(zip(hdr, r)) for r in m.get("rows", [])}
# 영상별 SHORTS 트래픽 (개별 조회 — 조합쿼리 미지원)
shorts, totv = {}, {}
for vid in met:
    try:
        tt = q({**DR, "filters": f"video=={vid}", "dimensions": "insightTrafficSourceType", "metrics": "views"})
        for r in tt.get("rows", []):
            src, v = r[0], int(r[1])
            totv[vid] = totv.get(vid, 0) + v
            if src == "SHORTS": shorts[vid] = v
    except Exception:
        pass

# 공개영상 + 제목 + 공개조회 (CSV)
pub = {}
for r in csv.DictReader(open("records/youtube/stats/2026-07-06.csv", encoding="utf-8"), delimiter='\t'):
    if r.get("privacy") == "public":
        try: pv = int(float(r.get("views", 0) or 0))
        except: pv = 0
        pub[r["video_id"]] = (r.get("title", ""), pv)

def theme(t):
    music = ["lo-fi","lofi","synth-pop","city pop","ballad","발라드","jazz","R&B","slow jam",
             "indie","noir","k-pop dance","k-ballad","office ballad","folk","city-pop","랜식"]
    if any(k in t for k in ["구미호","이무기","장산범","괴담"]): return "괴담"
    if any(k in t for k in ["디아블로","Diablo","RimWorld","colony","콜로니","-40","Live-Action",
                             "Trader","Starved","Marauder","Raid","Pawn","pawn","게임"]): return "게임"
    if any(k in t for k in ["리센느","RESCENE","QWER","프리티","카라","케이팝","아이돌"]): return "K-pop"
    if any(k in t for k in ["블랙홀","고양이","0.1초","심리","심해","우주","태양","중력","서울2126",
                             "미래","로봇","휴머노이드","프린스턴","과학","뉴스","왓이프"]): return "과학정보"
    if any(k in t for k in music): return "뮤직비디오"
    return "기타"

# 공개영상만, 공개조회>=20 (죽은 중복·테스트 제외), analytics 조인
rows = []
for vid, (ti, pv) in pub.items():
    if pv < 20: continue
    d = met.get(vid)
    rp = float(d["averageViewPercentage"]) if d else None
    sp = (100*shorts.get(vid, 0)/totv[vid]) if totv.get(vid) else None
    rows.append((theme(ti), rp, sp, pv, ti, vid))

# 영상별 표 (리텐션 내림차순, 미집계는 뒤)
print("=== 영상별 (공개·조회20+) — 리텐션순 ===")
print(f"{'리텐션':>5} {'SHORTS':>6} {'조회':>6}  테마      제목")
for th_, rp, sp, pv, ti, vid in sorted(rows, key=lambda x: (x[1] is not None, x[1] or 0), reverse=True):
    rs = f"{rp:.0f}%" if rp is not None else "  —"
    ss = f"{sp:.0f}%" if sp is not None else "  —"
    print(f"{rs:>5} {ss:>6} {pv:>6}  {th_:<8} {ti[:40]}")

# 테마 요약 (집계된 것만)
def med(xs): return st.median(xs) if xs else 0
print("\n=== 테마 요약 (리텐션 중앙값순) ===")
print(f"{'테마':<8}{'수':>3}{'조회합':>8}{'조회중앙':>8}{'리텐션中':>8}{'SHORTS中':>9}")
agg = {}
for th_, rp, sp, pv, ti, vid in rows:
    agg.setdefault(th_, []).append((rp, sp, pv))
out = []
for th_, xs in agg.items():
    rps = [r for r, s, p in xs if r is not None]
    sps = [s for r, s, p in xs if s is not None]
    pvs = [p for r, s, p in xs]
    out.append((med(rps), th_, len(xs), sum(pvs), int(med(pvs)), med(rps), med(sps)))
for mrp, th_, n, tv, mv, _, msp in sorted(out, key=lambda x: -x[0]):
    print(f"{th_:<8}{n:>3}{tv:>8}{mv:>8}{mrp:>7.0f}%{msp:>8.0f}%")
print("\n※ 리텐션中=영상 자체 품질(관객무관), SHORTS中=알고리즘 피드 배포%")
print("※ '—'=analytics 집계지연(최근영상). 구미호·심해 등 최신은 1~2일 뒤 반영")
