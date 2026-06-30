# config/subjects/ — real-subject profiles for the `idol` content-short

The `idol` profile (`/idol-short`) produces shorts **about a real artist/idol**.
The concrete artist is defined in a per-subject YAML here and selected at run
time with `--subject=<id>`.

## Why these files stay local (gitignored)

`*.yaml` in this directory is **gitignored**. A subject file names a real,
trademarked group + its members and configures a tool to make content about
them — that combination should not ship in a public repo (IP / trademark /
publicity-rights sensitivity, same "abstract to genre in public" rule the repo
applies elsewhere). The `idol-short` skill itself is genre-abstract and does not
need any subject file committed to function.

Keep your subject files here locally; they are read by:
- `agents/missions/content-short/run.sh` (narrator voice, overlay params)
- `scripts/subject-overlay.sh` (channel branding + disclaimers)
- the `research-team` / `legal-team` subagents

## Schema (see the legal posture — it is the point of this profile)

```yaml
subject:
  id: <slug>
  display_name: <name>
  kind: real-idol-group | real-person | brand
  agency: <rights holder>
  official:                 # the ONLY first-party sources (for verification)
    youtube: <url>
    instagram: <url>
  legal:
    real_people: true       # portrait / publicity rights apply
    has_minors: true|false  # if true → heightened care (legal-team enforces)
    media_owner: <who owns official photos/MV/performance>
    default_safe_path: "narration + sourced facts + generic license-clean B-roll + text; no member imagery, no group audio"
  brand:                    # TEXT-based identity (no member likeness by default)
    lower_third_text: <channel name>
    brand_mark_text: <mark>
    accent_color: "0x......"
    member_image: ""        # MUST stay empty unless you hold rights to that image
  disclosures:
    fan_content: "비공식 팬 제작 / Unofficial fan-made, not affiliated"
    ai_narration: "AI 합성 나레이션 / Synthetic AI narration"
  content_base: news | info
  voice: <narrator TTS voice — never a member's voice>
```

The **default-safe render path** uses none of the artist's copyrighted media —
synthetic narration + sourced facts + license-clean generic B-roll + on-screen
text. The 법률팀 (legal-team) enforces it (portrait-publicity-rights,
media-rights-reuse, fan-content-disclaimer, defamation, minors).
