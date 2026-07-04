#!/usr/bin/env bash
set -uo pipefail
cd "$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CSV=records/_blackhole/stats-timeline.csv
[[ -f "$CSV" ]] || echo "ts,videoId,views,likes,comments" > "$CSV"
CFG=/g/config/youtubeuploader/client_secrets.json; TOK=/g/config/youtubeuploader/request.token
JQ=$(command -v jq || echo /g/tools/jq/jq)
CID=$($JQ -r '.installed.client_id' "$CFG"); CSEC=$($JQ -r '.installed.client_secret' "$CFG"); RT=$($JQ -r '.refresh_token' "$TOK")
AT=$(curl -s -X POST https://oauth2.googleapis.com/token --data-urlencode "client_id=$CID" --data-urlencode "client_secret=$CSEC" --data-urlencode "refresh_token=$RT" --data-urlencode "grant_type=refresh_token" | $JQ -r '.access_token // empty')
IDS="y0AMs6tAOos,HQcSJvbSyFA,9rGISwIPe24,Sjay5Aao8UI,WckBzqa8gUs,t47ndYsI7-g,Hi99fP5rKf4,CzwPVRJchFg,Bn4_hIsFqW4,9KZdPq8DFeM,tEgGcX-oZHc,mZ9rrPijCOY,PCJ70mB_Dp8"
TS=$(date '+%Y-%m-%dT%H:%M:%S')
curl -s "https://www.googleapis.com/youtube/v3/videos?part=statistics&id=$IDS" -H "Authorization: Bearer $AT" \
  | $JQ -r --arg ts "$TS" '.items[] | "\($ts),\(.id),\(.statistics.viewCount),\(.statistics.likeCount // 0),\(.statistics.commentCount // 0)"' >> "$CSV"
