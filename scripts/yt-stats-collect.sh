#!/usr/bin/env bash
# yt-stats-collect.sh — Phase 1 YouTube stats collector for ToddStudio.
#
# Pulls public + private metrics for every video on the channel's uploads
# playlist (default; override with YT_UPLOADS_PLAYLIST), writes one row per
# video to:
#
#   records/youtube/stats/<ISO-date>.csv         — flat tabular snapshot
#   records/youtube/stats/<ISO-date>-raw.json    — full API responses
#
# Designed to run idempotently (re-running on the same day overwrites that
# day's snapshot).  Cron / launchd-friendly.
#
# OAuth: reuses the youtubeuploader credentials at
# ~/.config/youtubeuploader/{client_secrets.json,request.token}.  The token
# is refreshed in-place per call; existing scope (`youtube` full) already
# covers videos.list and playlistItems.list.  No new consent required.
#
# Quota cost (Data API v3, per call):
#   - playlistItems.list  → 1 unit per 50 videos
#   - videos.list (50 IDs/call, parts=snippet,statistics,status) → 1 unit
# Total: ~2 units per run for our channel (well under the 10k/day cap).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CRED_DIR="${YT_CRED_DIR:-$HOME/.config/youtubeuploader}"
SECRETS="$CRED_DIR/client_secrets.json"
TOKEN_FILE="$CRED_DIR/request.token"
OUT_DIR="${YT_STATS_DIR:-$REPO_ROOT/records/youtube/stats}"
DATE="${YT_STATS_DATE:-$(date +%Y-%m-%d)}"
# Channel auto-discovered via OAuth (channels.list?mine=true); override
# only if collecting stats for a channel other than the OAuth user's.
PLAYLIST="${YT_UPLOADS_PLAYLIST:-}"

mkdir -p "$OUT_DIR"

if [[ ! -f "$SECRETS" || ! -f "$TOKEN_FILE" ]]; then
  echo "ERROR: missing OAuth files at $CRED_DIR" >&2
  exit 1
fi

# --- refresh access token --------------------------------------------------
CID=$(jq -r '.installed.client_id' "$SECRETS")
CSEC=$(jq -r '.installed.client_secret' "$SECRETS")
REFRESH=$(jq -r '.refresh_token' "$TOKEN_FILE")
ACCESS=$(curl -sf -X POST https://oauth2.googleapis.com/token \
  -d "client_id=$CID" -d "client_secret=$CSEC" \
  -d "refresh_token=$REFRESH" -d "grant_type=refresh_token" \
  | jq -r '.access_token')

if [[ -z "$ACCESS" || "$ACCESS" == "null" ]]; then
  echo "ERROR: token refresh failed" >&2
  exit 2
fi

# --- auto-discover uploads playlist if not pinned --------------------------
if [[ -z "$PLAYLIST" ]]; then
  PLAYLIST=$(curl -sf -H "Authorization: Bearer $ACCESS" \
    "https://www.googleapis.com/youtube/v3/channels?part=contentDetails&mine=true" \
    | jq -r '.items[0].contentDetails.relatedPlaylists.uploads // empty')
  if [[ -z "$PLAYLIST" ]]; then
    echo "ERROR: could not auto-discover uploads playlist (channels.list?mine=true returned empty)" >&2
    exit 3
  fi
fi

# --- enumerate uploads playlist (paginate) ---------------------------------
PAGE_TOKEN=""
VIDEO_IDS=()
while :; do
  url="https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId=$PLAYLIST"
  [[ -n "$PAGE_TOKEN" ]] && url="$url&pageToken=$PAGE_TOKEN"
  page=$(curl -sf -H "Authorization: Bearer $ACCESS" "$url")
  while IFS= read -r id; do VIDEO_IDS+=("$id"); done < <(echo "$page" | jq -r '.items[].contentDetails.videoId')
  PAGE_TOKEN=$(echo "$page" | jq -r '.nextPageToken // empty')
  [[ -z "$PAGE_TOKEN" ]] && break
done

N=${#VIDEO_IDS[@]}
echo "[yt-stats] enumerated $N videos on $PLAYLIST"

if [[ "$N" -eq 0 ]]; then
  echo "WARN: no videos found, exit" >&2
  exit 0
fi

# --- videos.list in batches of 50 -----------------------------------------
RAW="$OUT_DIR/$DATE-raw.json"
CSV="$OUT_DIR/$DATE.csv"

: > "$RAW.tmp"
for ((i=0; i<N; i+=50)); do
  batch=("${VIDEO_IDS[@]:i:50}")
  ids=$(IFS=,; echo "${batch[*]}")
  curl -sf -H "Authorization: Bearer $ACCESS" \
    "https://www.googleapis.com/youtube/v3/videos?part=snippet,statistics,status,contentDetails&id=$ids" \
    | jq -c '.items[]' >> "$RAW.tmp"
done

# Wrap rows into a single JSON array for the raw snapshot
jq -s '{collected_at: now | todate, channel_uploads_playlist: env.YT_PLAYLIST, count: length, videos: .}' \
  --arg YT_PLAYLIST "$PLAYLIST" \
  "$RAW.tmp" > "$RAW"
rm -f "$RAW.tmp"

# --- CSV (flat) ------------------------------------------------------------
{
  printf 'date\tvideo_id\ttitle\tpublished_at\tprivacy\tpublish_at\tduration\tview_count\tlike_count\tcomment_count\tfavorite_count\n'
  jq -r --arg DATE "$DATE" '
    .videos[] |
    [
      $DATE,
      .id,
      (.snippet.title // ""),
      (.snippet.publishedAt // ""),
      (.status.privacyStatus // ""),
      (.status.publishAt // ""),
      (.contentDetails.duration // ""),
      (.statistics.viewCount // "0"),
      (.statistics.likeCount // "0"),
      (.statistics.commentCount // "0"),
      (.statistics.favoriteCount // "0")
    ] | @tsv
  ' "$RAW"
} > "$CSV"

echo "[yt-stats] wrote $CSV ($N rows)"
echo "[yt-stats] raw → $RAW"

# Operator-facing summary (top 5 by views)
echo
echo "=== top 5 by views (collected $DATE) ==="
sort -t$'\t' -k8 -nr "$CSV" | head -6 | awk -F'\t' 'NR==1{print; next} {printf "%s  %sv %sl %sc  %s\n", $2, $8, $9, $10, substr($3,1,60)}' | tail -n +2
