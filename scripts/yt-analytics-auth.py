#!/usr/bin/env python3
"""yt-analytics-auth.py — one-time OAuth for YouTube Analytics (read-only).

The uploader token (request.token) is scoped to youtube.upload only, so
Analytics API / comments read return 403. This mints a SEPARATE token with
read scopes and saves it next to the uploader credentials.

Flow: installed-app + localhost redirect. Run it, click the printed URL,
approve in the browser — the local listener catches the code automatically.

Usage:  python3 scripts/yt-analytics-auth.py
Saves:  <config dir>/analytics.token  (JSON: refresh_token etc.)
"""
import http.server
import json
import os
import secrets
import sys
import threading
import urllib.parse
import urllib.request
from pathlib import Path

CONF = Path(os.environ.get("YT_CONFIG_DIR", "G:/config/youtubeuploader"))
SECRETS = CONF / "client_secrets.json"
OUT = CONF / "analytics.token"
SCOPES = "https://www.googleapis.com/auth/yt-analytics.readonly https://www.googleapis.com/auth/youtube.readonly"
PORT = 8765

cs = json.loads(SECRETS.read_text(encoding="utf-8"))["installed"]
state = secrets.token_urlsafe(16)
redirect = f"http://127.0.0.1:{PORT}"
auth_url = (
    "https://accounts.google.com/o/oauth2/v2/auth?"
    + urllib.parse.urlencode({
        "client_id": cs["client_id"], "redirect_uri": redirect,
        "response_type": "code", "scope": SCOPES,
        "access_type": "offline", "prompt": "consent", "state": state,
    })
)

code_holder = {}

class H(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        q = urllib.parse.parse_qs(urllib.parse.urlparse(self.path).query)
        if q.get("state", [""])[0] == state and "code" in q:
            code_holder["code"] = q["code"][0]
            body = "<h2>인증 완료 — 터미널로 돌아가세요. 이 탭은 닫아도 됩니다.</h2>"
        else:
            body = "<h2>인증 실패/취소</h2>"
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.end_headers()
        self.wfile.write(body.encode("utf-8"))
        threading.Thread(target=self.server.shutdown, daemon=True).start()

    def log_message(self, *a):  # quiet
        pass

srv = http.server.HTTPServer(("127.0.0.1", PORT), H)
print("아래 URL을 브라우저에서 열어 승인하세요 (ToddStudio 채널 계정으로):\n")
print(auth_url + "\n")
sys.stdout.flush()
srv.serve_forever()  # returns after callback shuts it down

code = code_holder.get("code")
if not code:
    sys.exit("no code received")
data = urllib.parse.urlencode({
    "code": code, "client_id": cs["client_id"], "client_secret": cs["client_secret"],
    "redirect_uri": redirect, "grant_type": "authorization_code",
}).encode()
tok = json.loads(urllib.request.urlopen(
    urllib.request.Request("https://oauth2.googleapis.com/token", data=data)
).read())
OUT.write_text(json.dumps(tok, indent=2), encoding="utf-8")
print(f"저장됨: {OUT}  (refresh_token {'있음' if tok.get('refresh_token') else '없음!'})")
