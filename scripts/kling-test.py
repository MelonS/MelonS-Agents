#!/usr/bin/env python3
"""
KlingAI API connectivity test.
Generates JWT, calls a minimal endpoint, prints response.
Does NOT consume credits (only auth + list test).
"""
import os
import sys
import time
import json
import jwt
import requests
from pathlib import Path

CREDS_FILE = Path.home() / ".config" / "kling" / "credentials"
BASE_URL = "https://api.klingai.com"

def load_creds():
    if not CREDS_FILE.exists():
        sys.exit(f"missing {CREDS_FILE}")
    creds = {}
    for line in CREDS_FILE.read_text().splitlines():
        if "=" in line:
            k, v = line.split("=", 1)
            creds[k.strip()] = v.strip()
    return creds["KLING_ACCESS_KEY"], creds["KLING_SECRET_KEY"]

def make_jwt(access_key, secret_key):
    now = int(time.time())
    payload = {
        "iss": access_key,
        "exp": now + 1800,  # 30 min
        "nbf": now - 5,
    }
    headers = {"alg": "HS256", "typ": "JWT"}
    return jwt.encode(payload, secret_key, algorithm="HS256", headers=headers)

def main():
    ak, sk = load_creds()
    token = make_jwt(ak, sk)
    print(f"[INFO] JWT generated (length {len(token)})")

    # Try a query endpoint — list account tasks (read-only, free)
    # KlingAI docs: GET /v1/videos/text2video lists tasks
    url = f"{BASE_URL}/v1/videos/text2video"
    headers = {
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json",
    }
    params = {"pageNum": 1, "pageSize": 1}

    print(f"[INFO] testing GET {url}")
    try:
        r = requests.get(url, headers=headers, params=params, timeout=15)
    except Exception as e:
        sys.exit(f"[FAIL] request error: {e}")

    print(f"[RESP] HTTP {r.status_code}")
    print(f"[BODY] {json.dumps(r.json() if r.headers.get('content-type','').startswith('application/json') else r.text[:500], ensure_ascii=False, indent=2)}")

if __name__ == "__main__":
    main()
