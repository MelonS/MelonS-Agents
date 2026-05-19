# `job-hunt` skill — walkthrough

Companion to [`skills/job-hunt/SKILL.md`](../../skills/job-hunt/SKILL.md).
SKILL.md is the agentskills.io-spec contract (what the skill is + how to
invoke).  This file is the contributor / operator guide (how to add a
source, how to flip a plugin to live mode, how to debug, how to schedule
recurring runs).

---

## Anatomy

```
skills/job-hunt/
├── SKILL.md                       # agentskills.io frontmatter + contract
├── scripts/
│   ├── run.sh                     # orchestrator: filters → sources → filter → dedupe → diff → render
│   ├── digest.sh                  # markdown digest renderer
│   └── apply-assist.sh            # per-source apply-URL rewrite helpers
├── config/
│   └── filters.example.yaml       # documented starting filter; operator copies to filters.yaml
├── sources/
│   ├── README.md                  # plugin contract (this is what you read first if adding a source)
│   ├── _mock.sh                   # deterministic fixture for testing
│   ├── kr-wanted.sh               # 원티드 — mock-fallback default, live behind JH_WANTED_LIVE=1
│   ├── kr-programmers.sh          # 프로그래머스 — mock-fallback default, live behind JH_PROGRAMMERS_LIVE=1
│   ├── kr-jobkorea.sh             # 잡코리아 — mock-fallback default, live behind JH_JOBKOREA_LIVE=1
│   └── kr-saramin.sh              # 사람인 — mock-fallback default, live behind JH_SARAMIN_LIVE=1 + SARAMIN_KEY
└── tests/
    └── smoke.sh                   # structural + end-to-end mock test (32 checks)
```

The skill is **standalone-shaped** (per `docs/architecture.md` "Skills
layer — two shapes"): no `agents/missions/job-hunt/` counterpart, no
5-agent orchestrator routing, the skill's own `scripts/run.sh` is the
canonical implementation.

---

## First-time setup

```bash
# 1. Copy the example filter and edit your 직군/지역/keywords/sources.
cp skills/job-hunt/config/filters.example.yaml \
   skills/job-hunt/config/filters.yaml

# 2. Sanity-check the pipeline runs end-to-end without touching any
#    network (all plugins are mock-fallback by default).
skills/job-hunt/scripts/run.sh --dry-run

# 3. Inspect the produced digest.  --dry-run writes under /tmp.
#    Without --dry-run, the digest lands at the path printed on stdout
#    (typically ./records/jobs/<YYYY-MM-DD>/digest.md per filters.yaml).
```

Expected output without any env vars set: a digest with 9 postings
across 4 sources (the same shape as the committed sample at
[`docs/samples/job-hunt-digest-mock.md`](../samples/job-hunt-digest-mock.md)).

---

## Flipping a plugin from mock to live

Each `kr-*` plugin ships with the live HTTP path **fully written but
commented out** and **gated on an env-var flag**.  The reason: live
endpoint shapes change without notice; an operator-validation step is
required before any live request is issued.

The general flip-on procedure for a plugin (using `kr-wanted` as the
example):

```bash
# (a) Operator-validation step.  Issue one curl manually and compare
#     the response shape against the assumed schema in
#     skills/job-hunt/sources/kr-wanted.sh comments.
curl -sS \
  -H "wanted-client-id: $WANTED_API_KEY" \
  'https://api.wanted.co.kr/v4/jobs?country=kr&limit=3' | jq '.data[0]'

# (b) If field names match the assumed shape, edit
#     sources/kr-wanted.sh: locate the commented-out `raw=$(curl ...)`
#     + `echo "$raw" | jq ...` block under the "Placeholder live call"
#     header and uncomment it.  Delete the `echo "[kr-wanted] live
#     path not yet operator-validated" >&2; return 1` early-return.
#
#     If field names DON'T match, adjust the jq transformation block
#     to map the actual response fields onto the normalized schema
#     before uncommenting.

# (c) Run the orchestrator with the live flag set:
JH_WANTED_LIVE=1 WANTED_API_KEY=<token> \
  skills/job-hunt/scripts/run.sh --sources=kr-wanted

# (d) Inspect the digest.  If results look reasonable, set the env
#     vars permanently in .env (gitignored).  Commit the source-plugin
#     change once stable.
```

Per-source-specific notes:

- **`kr-wanted`**: partner API key required (Wanted issues these to
  approved integrators).  No anti-bot — clean JSON API.
- **`kr-programmers`**: public REST listing; no auth required.
  Endpoint shape is the most volatile of the four — re-validate the
  curl step periodically.
- **`kr-jobkorea`**: HTML scrape — needs pup or a python+bs4 parser.
  Anti-bot is MEDIUM (UA + request-rate).  Stay under 500ms between
  requests; never parallelize against this source.
- **`kr-saramin`**: OpenAPI partner key required (register at
  https://oapi.saramin.co.kr/guide).  Rate-limit: 1000 calls/day per
  Saramin docs.

---

## Adding a new locale

Drop a plugin at `skills/job-hunt/sources/<locale>-<board>.sh` that
implements `fetch_postings()` per the contract in
[`sources/README.md`](../../skills/job-hunt/sources/README.md).
Example skeleton for a hypothetical US source:

```bash
#!/usr/bin/env bash
# sources/us-linkedin.sh — LinkedIn Jobs (US).

fetch_postings() {
  if [[ "${JH_LINKEDIN_LIVE:-0}" == "1" ]]; then
    # Live path goes here.  See SKILL.md "Adding a locale" for the
    # required JSON output shape.
    echo "[us-linkedin] not yet validated" >&2
    return 1
  fi

  local fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"
  cat <<EOF
{
  "source": "us-linkedin",
  "fetched_at": "${fetched_at}",
  "postings": [
    { "title": "...", "company": "...", "region": "...",
      "posted_at": "...", "url": "...", "summary": "...", "apply_url": "..." }
  ]
}
EOF
}
```

Then update `config/filters.yaml`:

```yaml
locale: kr      # current orchestrator only validates `kr`; an `us` value
                # would require run.sh to drop the `[[ "$locale" == "kr" ]]`
                # guard or extend it to a list.
sources:
  - us-linkedin
```

Note: the orchestrator's locale validation is currently a single-value
allowlist (`[[ "$locale" == "kr" ]] || die`).  Extending support to
additional locales requires a tiny `scripts/run.sh` change to accept a
configured list — that landed last because all four KR sources predate
any non-KR demand.

---

## Filter semantics — details

```yaml
job_categories:
  - 백엔드 개발자       # OR semantics: at least one must match the posting category
keywords:
  include: [Python, AI] # OR semantics: at least one must appear in title or summary
  exclude: [SI, 파견]   # AND-of-NOT: none of these may appear
```

Source plugins receive the filter context via these exported env vars:

- `JH_REGIONS` — newline-separated
- `JH_CATEGORIES` — newline-separated
- `JH_KEYWORDS_INCLUDE` — newline-separated
- `JH_KEYWORDS_EXCLUDE` — newline-separated

The mock source ignores these (data is fixed).  Live sources should
use them to scope the upstream query so the network burst stays
proportionate to the operator's actual scope.

Post-fetch, the orchestrator applies the include/exclude keyword check
once more against `title + summary` of each posting — this is the
final filter regardless of what the source returned.

---

## Dedupe + diff semantics

**Dedupe**: identical posting `url` values are collapsed; first
occurrence wins.  Sources are processed in `filters.yaml` order, so
preferring a source means putting it earlier.  The mock fixture
deliberately includes a duplicate URL across two postings to exercise
this path.

**Diff**: the orchestrator looks for the most-recent prior
`<records_root>/<other-date>/index.json` (excluding today's) and
treats any URL present today but absent in the prior index as "new
since".  These URLs are listed up front in the rendered digest and
populated in `index.json`'s `new_urls` array.

If there is no prior index, `postings_new` is `0` and the "new since
last digest" section is omitted from the markdown.

---

## Scheduling recurring runs

Per [`docs/architecture.md`](../architecture.md) and
[`operator-contract.md`](../operator-contract.md) §4, the project's
schedulers are launchd plists under `scripts/com.melons.agents.*.plist`
rendered per-machine.  To add a daily job-hunt digest:

```bash
# Create a daily plist (template; copy + adapt to add to install-scheduler.sh).
cat <<'PLIST' > scripts/com.melons.agents.job-hunt.plist.template
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>com.melons.agents.job-hunt</string>
  <key>WorkingDirectory</key><string>@@REPO_ROOT@@</string>
  <key>ProgramArguments</key>
  <array>
    <string>/bin/bash</string>
    <string>-lc</string>
    <string>skills/job-hunt/scripts/run.sh --quiet</string>
  </array>
  <key>StartCalendarInterval</key>
  <dict>
    <key>Hour</key><integer>9</integer>
    <key>Minute</key><integer>0</integer>
  </dict>
  <key>StandardOutPath</key><string>@@REPO_ROOT@@/records/jobs/launchd.out.log</string>
  <key>StandardErrorPath</key><string>@@REPO_ROOT@@/records/jobs/launchd.err.log</string>
</dict>
</plist>
PLIST
```

`@@REPO_ROOT@@` is the project's standard template placeholder rendered
by `scripts/install-claude-local.sh` (see operator-contract §8).  Wire
through `install-scheduler.sh` to make it portable per-machine.

(Not yet wired in this branch — operator decides if/when daily
scheduling is desired; manual `/job-hunt` invocations work today.)

---

## Debugging

### "all enabled sources failed"

Exit 3.  Common causes:

- A plugin's `fetch_postings()` is returning malformed JSON.
- A plugin's live mode was flipped on without operator-validation
  (the plugin's documented early-return fires).
- The `sources/` file is named differently from the `sources:` list
  entry in `filters.yaml`.

Quick triage: `bash skills/job-hunt/sources/<name>.sh; echo $?`
should source the file without error.  Then inside an interactive
shell:

```bash
. skills/job-hunt/sources/_mock.sh
fetch_postings | jq .
```

That should print the synthetic fixture.  Substitute `_mock` with the
plugin that's failing.

### "no YAML parser available"

Exit 2.  Install `yq` (`brew install yq` on macOS), or ensure ruby is
on PATH (macOS ships with ruby by default), or `pip install pyyaml`
in the project venv.

### Digest produced but empty

Filter is too restrictive.  Check `index.json` — it carries the raw
post-source / pre-filter postings under `.postings` and the filter
context in `.filter_summary`.  Loosen `include` keywords or remove
some `exclude` keywords as needed.

### Live mode hits a "field not found" error

The upstream API surface changed.  Re-run the operator-validation
curl step for that source and update the jq transformation block in
the plugin to map current field names.

---

## Privacy / data handling

- `config/filters.yaml` carrying personally-revealing context
  (specific company exclusion lists, name-based filters, etc.) is
  fine to keep local-only: add it to `.gitignore` or use
  `config/filters.local.yaml` and `--filters=<path>`.
- `config/filters.example.yaml` is committed in-repo as a documented
  starting point; it must not contain operator-specific information.
- Output digests under `records/jobs/<date>/` are gitignored (the
  repo's `records/` convention).
- No source credentials are stored in the skill.  Each plugin reads
  its key from environment, which the operator sets via `.env`
  (gitignored) or shell-export.

---

## Related references

- [`skills/job-hunt/SKILL.md`](../../skills/job-hunt/SKILL.md) — the
  agentskills.io-spec contract surface.
- [`skills/job-hunt/sources/README.md`](../../skills/job-hunt/sources/README.md)
  — plugin authoring contract.
- [`docs/architecture.md`](architecture.md) §"Skills layer — two
  shapes" — why this skill is standalone, not missions-routed.
- [`docs/operator-contract.md`](operator-contract.md) §8 portability
  principles — applies to source plugins as much as to bootstrap
  scripts.
- [`docs/samples/job-hunt-digest-mock.md`](../samples/job-hunt-digest-mock.md)
  — committed reference of what a real digest looks like.
