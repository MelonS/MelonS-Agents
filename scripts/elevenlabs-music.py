#!/usr/bin/env python3
# elevenlabs-music.py — ElevenLabs Music(text->BGM) 생성기.  (elevenlabs-sfx.py의 음악판)
#   python elevenlabs-music.py "<prompt>" <length_sec|auto> <out.mp3> [--vocal|--instrumental] [seed]
# 키: $ELEVENLABS_API_KEY -> $ELEVENLABS_KEY_FILE -> /g/config/elevenlabs/api.key -> G:/config/...
# 비용: ~900 credits/분 (~$0.15/분).  라이선스: API 생성물은 일반 콘텐츠(수익화 유튜브 포함) 상업 OK,
#       단 광고/TV/영화/게임/기업배포는 추가 라이선스(elevenlabs.io/eleven-music-v1-terms).
# 키에 music_generation 권한 필요(없으면 401 missing_permissions).
import sys, os, json, urllib.request, urllib.error

def resolve_key():
    if os.environ.get("ELEVENLABS_API_KEY"): return os.environ["ELEVENLABS_API_KEY"].strip()
    kf = os.environ.get("ELEVENLABS_KEY_FILE")
    for p in [kf, "/g/config/elevenlabs/api.key", "G:/config/elevenlabs/api.key"]:
        if p and os.path.isfile(p):
            return open(p, encoding="utf-8").read().strip()
    sys.exit("no ElevenLabs key")

def main():
    args = list(sys.argv[1:])
    instrumental = True
    if "--vocal" in args: instrumental = False; args.remove("--vocal")
    if "--instrumental" in args: instrumental = True; args.remove("--instrumental")
    if len(args) < 3:
        sys.exit('usage: elevenlabs-music.py "<prompt>" <length_sec|auto> <out.mp3> [--vocal|--instrumental] [seed]')
    prompt, length, out = args[0], args[1], args[2]
    body = {"prompt": prompt, "force_instrumental": instrumental}
    if length not in ("", "auto"):
        body["music_length_ms"] = max(3000, min(600000, int(float(length) * 1000)))
    if len(args) > 3 and args[3].lstrip("-").isdigit():
        body["seed"] = int(args[3])
    req = urllib.request.Request(
        "https://api.elevenlabs.io/v1/music/compose",
        data=json.dumps(body).encode(),
        headers={"xi-api-key": resolve_key(), "Content-Type": "application/json"},
        method="POST")
    try:
        data = urllib.request.urlopen(req, timeout=300).read()
    except urllib.error.HTTPError as e:
        sys.exit(f"HTTP {e.code}: {e.read().decode('utf-8','ignore')[:300]}")
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    open(out, "wb").write(data)
    print(f"OK {out} ({len(data)//1024}KB, {'instrumental' if instrumental else 'vocal'}) :: {prompt[:60]}")

if __name__ == "__main__":
    main()
