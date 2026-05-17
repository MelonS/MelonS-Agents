"""Operator-intervention trend chart.

Answers the operator's question: "is my involvement going down over
time?  it should be."  This is the autonomy signal — a multi-agent
system that needs constant human steering hasn't actually escaped
the same effort it was meant to replace.

For every commit, classify the initiator:
  - USER-INITIATED — operator surfaced the need, said go, picked an
    option, or explicitly approved.  Detected by:
      * `Requested-by: user` footer (the marker introduced in
        commit `7c6ff4f` on 2026-05-17 — strict signal going forward)
      * Body contains operator-quote phrases like "Operator surfaced",
        "Operator flagged", "Operator picked", "operator feedback",
        or Korean direct-quote markers (`"…"` or `「…」`)
      * Subject contains "user-requested" / "operator-asked" patterns
  - AGENT-AUTONOMOUS — agent picked the work from the roadmap, the
    audit caught drift, or the work was infra maintenance.  Default.

Output: docs/metrics/intervention.png + docs/metrics/intervention.json.

Historical caveat: the `Requested-by: user` marker convention only
started 2026-05-17.  Older commits classified by body-content
heuristic only — recall is lower for those days.  This is annotated
on the chart and in the JSON.
"""

import json
import pathlib
import re
import subprocess
import sys
from collections import Counter, defaultdict
from datetime import date, datetime

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT_PNG = ROOT / "docs" / "metrics" / "intervention.png"
OUT_JSON = ROOT / "docs" / "metrics" / "intervention.json"
OUT_PNG.parent.mkdir(parents=True, exist_ok=True)

MARKER_DATE = date(2026, 5, 17)

# Patterns that signal user initiation.  Conservative — we'd rather
# under-classify (call something agent-autonomous when it was actually
# user-initiated) than inflate the user-initiated count.
USER_PATTERNS = [
    re.compile(r"^Requested-by:\s*user\s*$", re.IGNORECASE | re.MULTILINE),
    re.compile(r"\boperator\s+(surfaced|flagged|picked|asked|requested|chose|surveys?|said|feedback|approved|directs?)\b",
               re.IGNORECASE),
    re.compile(r"\buser\s+(surfaced|flagged|picked|asked|requested|chose)\b", re.IGNORECASE),
    re.compile(r"\boperator-(asked|flagged|requested|driven|surfaced)\b", re.IGNORECASE),
    # Korean direct-quote markers in commit body indicate the operator
    # said something we quoted back.
    re.compile(r"[\"“]\s*[가-힣]"),  # opening quote followed by Hangul
]


def classify(subject: str, body: str) -> str:
    blob = f"{subject}\n{body}"
    for p in USER_PATTERNS:
        if p.search(blob):
            return "user"
    return "agent"


def main() -> int:
    fmt = "%H%x1f%aI%x1f%s%x1f%b%x1e"  # delim 0x1f, record sep 0x1e
    raw = subprocess.check_output(
        ["git", "log", "--format=" + fmt, "--no-merges"],
        cwd=ROOT, text=True,
    )
    records = [r for r in raw.split("\x1e") if r.strip()]

    per_day_user = Counter()
    per_day_agent = Counter()
    details = []
    earliest = None
    latest = None
    for rec in records:
        parts = rec.split("\x1f")
        if len(parts) < 4:
            continue
        sha, iso_when, subject, body = parts[0].strip(), parts[1].strip(), parts[2], parts[3]
        when = datetime.fromisoformat(iso_when).date()
        # ignore old pre-project commits if any (this repo started 2026-05-13)
        if when < date(2026, 5, 13):
            continue
        klass = classify(subject, body)
        if klass == "user":
            per_day_user[when] += 1
        else:
            per_day_agent[when] += 1
        details.append({
            "sha": sha[:7], "date": when.isoformat(),
            "class": klass, "subject": subject,
        })
        if earliest is None or when < earliest:
            earliest = when
        if latest is None or when > latest:
            latest = when

    if not details:
        print("no commits in window", file=sys.stderr)
        return 0

    # Build a fully-populated date axis (no gaps).
    days = []
    d = earliest
    while d <= latest:
        days.append(d)
        d = date.fromordinal(d.toordinal() + 1)

    user_counts = [per_day_user.get(d, 0) for d in days]
    agent_counts = [per_day_agent.get(d, 0) for d in days]
    totals = [u + a for u, a in zip(user_counts, agent_counts)]
    user_ratio = [
        (u / t * 100) if t else 0 for u, t in zip(user_counts, totals)
    ]

    # Write JSON source.
    OUT_JSON.write_text(json.dumps({
        "generated_at": datetime.now().isoformat(timespec="seconds"),
        "marker_convention_started": MARKER_DATE.isoformat(),
        "rationale": "Per-day count of commits classified as user-initiated vs agent-autonomous. See scripts/generate-intervention-chart.py for the classification heuristics. Aim is for user-initiated to trend down over time as agent autonomy grows.",
        "days": [
            {
                "date": d.isoformat(),
                "user_initiated": u,
                "agent_autonomous": a,
                "user_ratio_pct": round(r, 1),
            }
            for d, u, a, r in zip(days, user_counts, agent_counts, user_ratio)
        ],
        "commits": details,
    }, ensure_ascii=False, indent=2))

    # Plot.
    fig, ax = plt.subplots(figsize=(11, 5))
    ax2 = ax.twinx()

    x = list(range(len(days)))
    width = 0.7
    bars_agent = ax.bar(
        x, agent_counts, width=width,
        label="Agent-autonomous", color="#3a90d6", edgecolor="white", linewidth=0.5,
    )
    bars_user = ax.bar(
        x, user_counts, width=width, bottom=agent_counts,
        label="User-initiated", color="#d94a4a", edgecolor="white", linewidth=0.5,
    )

    # Trend line — user-initiated ratio on secondary axis.
    ax2.plot(
        x, user_ratio,
        color="#333", linewidth=1.5, marker="o", markersize=6,
        label="User-initiated %",
    )

    # Total label above each bar.
    for xi, t in zip(x, totals):
        if t > 0:
            ax.text(xi, t + 0.4, str(t), ha="center", va="bottom", fontsize=9, color="#444")

    # Ratio labels at line points.
    for xi, r, t in zip(x, user_ratio, totals):
        if t > 0:
            ax2.text(xi, r + 3, f"{r:.0f}%", ha="center", va="bottom", fontsize=8,
                     color="#333", fontweight="bold")

    ax.set_xticks(x)
    ax.set_xticklabels([d.isoformat()[-5:] for d in days], rotation=0, fontsize=9)
    ax.set_ylabel("commits / day")
    ax2.set_ylabel("user-initiated %", color="#333")
    ax2.set_ylim(0, 110)
    ax.set_xlabel("date (2026-)")
    ax.set_axisbelow(True)
    ax.grid(axis="y", linestyle=":", color="#bbb", alpha=0.6)
    ax.spines["top"].set_visible(False)
    ax2.spines["top"].set_visible(False)

    title = "Operator-intervention trend — daily commit count, split by initiator"
    ax.set_title(title, fontsize=12, pad=14)

    # Legends — combine into one block.
    h1, l1 = ax.get_legend_handles_labels()
    h2, l2 = ax2.get_legend_handles_labels()
    ax.legend(h1 + h2, l1 + l2, loc="upper right", frameon=False, fontsize=9)

    # Annotation: marker convention boundary.
    if MARKER_DATE in days:
        i = days.index(MARKER_DATE)
        ax.axvline(i - 0.5, color="#999", linestyle="--", linewidth=1, alpha=0.7)
        ax.text(i - 0.45, ax.get_ylim()[1] * 0.92,
                "Requested-by marker\nconvention starts",
                fontsize=7, color="#666", va="top", ha="left")

    plt.tight_layout()
    plt.savefig(OUT_PNG, dpi=140, bbox_inches="tight", facecolor="white")
    rel = OUT_PNG.relative_to(ROOT)
    print(f"wrote {rel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
