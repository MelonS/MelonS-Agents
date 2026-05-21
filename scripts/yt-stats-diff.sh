#!/usr/bin/env bash
# yt-stats-diff.sh — compare two snapshots from yt-stats-collect.sh.
#
# Usage:
#   scripts/yt-stats-diff.sh                   # today vs yesterday
#   scripts/yt-stats-diff.sh 2026-05-22 2026-05-23
#
# Output: per-video view/like/comment deltas (sorted by view delta desc).
# Useful for "what gained traction in the last N days" quick reads.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIR="${YT_STATS_DIR:-$REPO_ROOT/records/youtube/stats}"

YESTERDAY="${1:-$(date -v-1d +%Y-%m-%d 2>/dev/null || date -d 'yesterday' +%Y-%m-%d)}"
TODAY="${2:-$(date +%Y-%m-%d)}"

A="$DIR/$YESTERDAY.csv"
B="$DIR/$TODAY.csv"

if [[ ! -f "$A" ]]; then
  echo "ERROR: $A not found" >&2
  exit 1
fi
if [[ ! -f "$B" ]]; then
  echo "ERROR: $B not found (run yt-stats-collect.sh first)" >&2
  exit 1
fi

echo "delta: $YESTERDAY → $TODAY"
echo

# Build a JSON delta + emit a sorted text table
python3 - "$A" "$B" <<'PY'
import csv, sys
prev = {}
for row in csv.DictReader(open(sys.argv[1]), delimiter='\t'):
    prev[row['video_id']] = row

now_rows = list(csv.DictReader(open(sys.argv[2]), delimiter='\t'))

out = []
for row in now_rows:
    p = prev.get(row['video_id'])
    if not p:
        # new video appeared today
        dv, dl, dc = int(row['view_count']), int(row['like_count']), int(row['comment_count'])
        out.append((dv, dl, dc, row['video_id'], row['title'][:60], 'NEW'))
        continue
    dv = int(row['view_count']) - int(p['view_count'])
    dl = int(row['like_count']) - int(p['like_count'])
    dc = int(row['comment_count']) - int(p['comment_count'])
    if dv == 0 and dl == 0 and dc == 0:
        continue
    out.append((dv, dl, dc, row['video_id'], row['title'][:60], ''))

out.sort(reverse=True)
print(f'{"+Δv":>5} {"+Δl":>4} {"+Δc":>3}  {"id":11}  title')
for dv, dl, dc, vid, title, tag in out:
    tag = f' [{tag}]' if tag else ''
    print(f'{dv:>+5} {dl:>+4} {dc:>+3}  {vid:11}  {title}{tag}')
if not out:
    print('(no deltas)')
PY
