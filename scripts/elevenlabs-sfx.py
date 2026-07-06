#!/usr/bin/env python3
# elevenlabs-sfx.py — ElevenLabs Sound Effects(text->SFX) 생성기.
#   python elevenlabs-sfx.py "<prompt>" <duration_sec> <out.mp3> [prompt_influence]
# 키: $ELEVENLABS_API_KEY -> $ELEVENLABS_KEY_FILE -> /g/config/elevenlabs/api.key -> G:/config/...
import sys, os, json, urllib.request, urllib.error

def resolve_key():
    if os.environ.get("ELEVENLABS_API_KEY"): return os.environ["ELEVENLABS_API_KEY"].strip()
    kf = os.environ.get("ELEVENLABS_KEY_FILE")
    for p in [kf, "/g/config/elevenlabs/api.key", "G:/config/elevenlabs/api.key"]:
        if p and os.path.isfile(p):
            return open(p, encoding="utf-8").read().strip()
    sys.exit("no ElevenLabs key")

def main():
    prompt = sys.argv[1]
    dur = float(sys.argv[2]) if len(sys.argv) > 2 and sys.argv[2] not in ("", "auto") else None
    out = sys.argv[3]
    infl = float(sys.argv[4]) if len(sys.argv) > 4 else 0.35
    body = {"text": prompt, "prompt_influence": infl}
    if dur: body["duration_seconds"] = max(0.5, min(22.0, dur))
    req = urllib.request.Request(
        "https://api.elevenlabs.io/v1/sound-generation",
        data=json.dumps(body).encode(),
        headers={"xi-api-key": resolve_key(), "Content-Type": "application/json"},
        method="POST")
    try:
        data = urllib.request.urlopen(req, timeout=120).read()
    except urllib.error.HTTPError as e:
        sys.exit(f"HTTP {e.code}: {e.read().decode('utf-8','ignore')[:300]}")
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    open(out, "wb").write(data)
    print(f"OK {out} ({len(data)//1024}KB) :: {prompt[:60]}")

if __name__ == "__main__":
    main()
