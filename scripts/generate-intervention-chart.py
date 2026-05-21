"""Operator-intervention trend chart + signal aggregator.

Answers the operator's question: "is my involvement going down over
time?  it should be."  This is the autonomy signal — a multi-agent
system that needs constant human steering hasn't actually escaped
the same effort it was meant to replace.

Two data sources, two panels.

Panel A — commit attribution (since 2026-05-13):
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

  Also computed per-day:
    - Leverage ratio = agent_commits / max(1, user_commits) — how many
      autonomous commits each operator nudge produced.  Higher = better.
    - Longest autonomous streak hours = longest gap between two
      consecutive user-initiated commits within the day.

Panel B — Claude Code session activity (~/.claude/projects/-Users-melons-ai/):
  For every session JSONL on the local machine:
    - Operator prompts = count of user messages whose content is a
      real text string (excludes tool_result auto-replies).
    - Active session minutes = (last_ts - first_ts) summed across the
      day's sessions, capped at 60min per session to prevent idle
      sessions from inflating the signal.

  Both panels share the same date axis.  Goal: panels move in the same
  direction (commits ratio down + prompts down = real reduction).

Output: docs/metrics/intervention.png + docs/metrics/intervention.json.

Historical caveats:
- The `Requested-by: user` marker convention only started 2026-05-17.
  Older commits classified by body-content heuristic only — recall is
  lower for those days.  Annotated on the chart.
- Session JSONLs only exist on the operator's local machine; the
  session panel is empty on a fresh clone.  The chart still renders
  with only the commit panel in that case.
"""

import json
import pathlib
import re
import statistics
import subprocess
import sys
from collections import Counter, defaultdict
from datetime import date, datetime, timedelta, timezone

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT_PNG = ROOT / "docs" / "metrics" / "intervention.png"
OUT_JSON = ROOT / "docs" / "metrics" / "intervention.json"
OUT_PNG.parent.mkdir(parents=True, exist_ok=True)

# Claude Code session JSONLs.  Path encodes the cwd:
# "-Users-melons-ai" for cwd /Users/melons/ai.
CC_PROJECT_DIR = pathlib.Path.home() / ".claude" / "projects" / "-Users-melons-ai"

MARKER_DATE = date(2026, 5, 17)

# Per-session cap.  An idle session left open for hours shouldn't
# inflate "active minutes"; cap each session contribution at 60min.
SESSION_CAP_MIN = 60

USER_PATTERNS = [
    re.compile(r"^Requested-by:\s*user\s*$", re.IGNORECASE | re.MULTILINE),
    re.compile(r"\boperator\s+(surfaced|flagged|picked|asked|requested|chose|surveys?|said|feedback|approved|directs?)\b",
               re.IGNORECASE),
    re.compile(r"\buser\s+(surfaced|flagged|picked|asked|requested|chose)\b", re.IGNORECASE),
    re.compile(r"\boperator-(asked|flagged|requested|driven|surfaced)\b", re.IGNORECASE),
    re.compile(r"[\"“]\s*[가-힣]"),  # opening quote followed by Hangul
]


def classify(subject: str, body: str) -> str:
    blob = f"{subject}\n{body}"
    for p in USER_PATTERNS:
        if p.search(blob):
            return "user"
    return "agent"


def collect_commits():
    fmt = "%H%x1f%aI%x1f%s%x1f%b%x1e"
    raw = subprocess.check_output(
        ["git", "log", "--format=" + fmt, "--no-merges"],
        cwd=ROOT, text=True,
    )
    records = [r for r in raw.split("\x1e") if r.strip()]
    out = []
    for rec in records:
        parts = rec.split("\x1f")
        if len(parts) < 4:
            continue
        sha, iso_when, subject, body = parts[0].strip(), parts[1].strip(), parts[2], parts[3]
        when_dt = datetime.fromisoformat(iso_when)
        when = when_dt.date()
        if when < date(2026, 5, 13):
            continue
        klass = classify(subject, body)
        out.append({
            "sha": sha[:7], "datetime": when_dt, "date": when,
            "class": klass, "subject": subject,
        })
    out.sort(key=lambda r: r["datetime"])
    return out


def commit_metrics_per_day(commits):
    by_day = defaultdict(lambda: {"user": [], "agent": []})
    for c in commits:
        by_day[c["date"]][c["class"]].append(c)
    days_with_data = sorted(by_day.keys())
    metrics = {}
    for d in days_with_data:
        u = by_day[d]["user"]
        a = by_day[d]["agent"]
        total = len(u) + len(a)
        ratio = (len(u) / total * 100) if total else 0
        # Longest autonomous streak (hours): longest gap between
        # consecutive user-initiated commits inside the day.
        if len(u) >= 2:
            ts = sorted([c["datetime"] for c in u])
            gaps = [(ts[i+1] - ts[i]).total_seconds() / 3600 for i in range(len(ts) - 1)]
            longest_gap_h = max(gaps)
        else:
            longest_gap_h = 0
        # Leverage ratio = agent / max(1, user).  Capped at 30 for plot
        # legibility (one-user-commit days can hit infinity).
        leverage = len(a) / max(1, len(u))
        metrics[d] = {
            "user": len(u),
            "agent": len(a),
            "total": total,
            "user_ratio_pct": round(ratio, 1),
            "longest_autonomous_gap_h": round(longest_gap_h, 1),
            "leverage_ratio": round(leverage, 1),
        }
    return metrics, days_with_data


def collect_sessions():
    """Mine local Claude Code session JSONLs for operator prompts +
    active session time.

    Returns dict keyed by date with: prompts, active_minutes.
    """
    if not CC_PROJECT_DIR.exists():
        return {}, []
    by_day = defaultdict(lambda: {"prompts": 0, "active_minutes": 0.0,
                                   "sessions": 0})
    seen_days = set()
    for jsonl_path in sorted(CC_PROJECT_DIR.glob("*.jsonl")):
        first_ts = None
        last_ts = None
        prompts = 0
        session_day = None
        try:
            with jsonl_path.open("r") as fh:
                for line in fh:
                    try:
                        obj = json.loads(line)
                    except Exception:
                        continue
                    ts_raw = obj.get("timestamp")
                    if ts_raw:
                        try:
                            ts = datetime.fromisoformat(ts_raw.replace("Z", "+00:00"))
                        except Exception:
                            ts = None
                        if ts:
                            if first_ts is None or ts < first_ts:
                                first_ts = ts
                            if last_ts is None or ts > last_ts:
                                last_ts = ts
                    if obj.get("type") == "user":
                        msg = obj.get("message", {})
                        content = msg.get("content", "")
                        # Real operator prompt: content is a non-empty
                        # string.  Tool results are list-of-dicts.
                        if isinstance(content, str) and len(content.strip()) > 5:
                            prompts += 1
                        elif isinstance(content, list):
                            # A list with at least one type=text item is
                            # also a real prompt (rare, but possible).
                            for c in content:
                                if isinstance(c, dict) and c.get("type") == "text":
                                    txt = c.get("text", "")
                                    if isinstance(txt, str) and len(txt.strip()) > 5:
                                        prompts += 1
                                        break
        except Exception:
            continue
        if first_ts is None:
            continue
        # Attribute session to start-of-session date (in KST — operator's tz).
        # KST = UTC+9.  Convert.
        kst = timezone(timedelta(hours=9))
        local_start = first_ts.astimezone(kst)
        session_day = local_start.date()
        if session_day < date(2026, 5, 13):
            continue
        seen_days.add(session_day)
        elapsed_min = (last_ts - first_ts).total_seconds() / 60
        # Cap per-session at SESSION_CAP_MIN to avoid idle sessions
        # bloating the signal.
        active = min(elapsed_min, SESSION_CAP_MIN)
        by_day[session_day]["prompts"] += prompts
        by_day[session_day]["active_minutes"] += active
        by_day[session_day]["sessions"] += 1
    return dict(by_day), sorted(seen_days)


def main() -> int:
    commits = collect_commits()
    if not commits:
        print("no commits", file=sys.stderr)
        return 0

    commit_metrics, commit_days = commit_metrics_per_day(commits)
    session_metrics, session_days = collect_sessions()

    # Build a fully-populated date axis (no gaps) from earliest to latest
    # across both signal sources.
    earliest = commit_days[0]
    latest = commit_days[-1]
    if session_days:
        earliest = min(earliest, session_days[0])
        latest = max(latest, session_days[-1])
    days = []
    d = earliest
    while d <= latest:
        days.append(d)
        d = date.fromordinal(d.toordinal() + 1)

    # Write JSON source.
    out_days = []
    for d in days:
        cm = commit_metrics.get(d, {
            "user": 0, "agent": 0, "total": 0,
            "user_ratio_pct": 0,
            "longest_autonomous_gap_h": 0,
            "leverage_ratio": 0,
        })
        sm = session_metrics.get(d, {"prompts": 0, "active_minutes": 0.0, "sessions": 0})
        out_days.append({
            "date": d.isoformat(),
            "user_initiated": cm["user"],
            "agent_autonomous": cm["agent"],
            "user_ratio_pct": cm["user_ratio_pct"],
            "longest_autonomous_gap_h": cm["longest_autonomous_gap_h"],
            "leverage_ratio": cm["leverage_ratio"],
            "operator_prompts": sm["prompts"],
            "active_session_minutes": round(sm["active_minutes"], 1),
            "session_count": sm["sessions"],
        })

    # Trend annotations — quick at-a-glance signal of direction.
    # 7-day rolling: latest 7 vs prior 7 (window ending today).
    def avg(lst):
        return sum(lst) / len(lst) if lst else 0
    last7 = out_days[-7:] if len(out_days) >= 7 else out_days
    prev7 = out_days[-14:-7] if len(out_days) >= 14 else []
    def delta(field):
        a = avg([r[field] for r in last7])
        b = avg([r[field] for r in prev7]) if prev7 else None
        return {
            "last7_avg": round(a, 2),
            "prev7_avg": round(b, 2) if b is not None else None,
            "delta": round(a - b, 2) if b is not None else None,
        }
    trend_summary = {
        "user_ratio_pct":          delta("user_ratio_pct"),
        "leverage_ratio":          delta("leverage_ratio"),
        "operator_prompts":        delta("operator_prompts"),
        "active_session_minutes":  delta("active_session_minutes"),
    }
    # Direction hints — negative delta on user_ratio + prompts = good.
    direction = []
    ur = trend_summary["user_ratio_pct"]["delta"]
    lr = trend_summary["leverage_ratio"]["delta"]
    op = trend_summary["operator_prompts"]["delta"]
    if ur is not None:
        if ur < -1:
            direction.append("user-ratio↓")
        elif ur > 1:
            direction.append("user-ratio↑")
    if lr is not None:
        if lr > 0.5:
            direction.append("leverage↑")
        elif lr < -0.5:
            direction.append("leverage↓")
    if op is not None:
        if op < -5:
            direction.append("prompts↓")
        elif op > 5:
            direction.append("prompts↑")
    trend_summary["direction"] = direction
    OUT_JSON.write_text(json.dumps({
        "generated_at": datetime.now().isoformat(timespec="seconds"),
        "marker_convention_started": MARKER_DATE.isoformat(),
        "session_cap_minutes": SESSION_CAP_MIN,
        "rationale": (
            "Two-source intervention tracker. Panel A: per-day commit count "
            "classified by initiator (user vs agent). Panel B: per-day operator "
            "prompts and active session minutes mined from local Claude Code "
            "session JSONLs. Goal: both signals trend down as the agent system "
            "absorbs more decisions."
        ),
        "trend_7d": trend_summary,
        "days": out_days,
        "commits": [
            {"sha": c["sha"], "date": c["date"].isoformat(),
             "class": c["class"], "subject": c["subject"]}
            for c in reversed(commits)
        ],
    }, ensure_ascii=False, indent=2))

    # Plot — 2 stacked panels sharing X.
    fig, (ax_a, ax_b) = plt.subplots(
        2, 1, figsize=(12, 8), sharex=True,
        gridspec_kw={"height_ratios": [3, 2], "hspace": 0.15},
    )

    x = list(range(len(days)))
    user_counts = [commit_metrics.get(d, {}).get("user", 0) for d in days]
    agent_counts = [commit_metrics.get(d, {}).get("agent", 0) for d in days]
    totals = [u + a for u, a in zip(user_counts, agent_counts)]
    user_ratio = [
        (u / t * 100) if t else 0 for u, t in zip(user_counts, totals)
    ]

    # Panel A — commit stack + ratio line.
    ax_a2 = ax_a.twinx()
    width = 0.7
    ax_a.bar(x, agent_counts, width=width, label="Agent-autonomous",
             color="#3a90d6", edgecolor="white", linewidth=0.5)
    ax_a.bar(x, user_counts, width=width, bottom=agent_counts,
             label="User-initiated", color="#d94a4a",
             edgecolor="white", linewidth=0.5)
    ax_a2.plot(x, user_ratio, color="#333", linewidth=1.5, marker="o",
               markersize=6, label="User-initiated %")
    for xi, t in zip(x, totals):
        if t > 0:
            ax_a.text(xi, t + 0.4, str(t), ha="center", va="bottom",
                      fontsize=9, color="#444")
    for xi, r, t in zip(x, user_ratio, totals):
        if t > 0:
            ax_a2.text(xi, r + 3, f"{r:.0f}%", ha="center", va="bottom",
                       fontsize=8, color="#333", fontweight="bold")
    ax_a.set_ylabel("commits / day")
    ax_a2.set_ylabel("user-initiated %", color="#333")
    ax_a2.set_ylim(0, 110)
    ax_a.set_axisbelow(True)
    ax_a.grid(axis="y", linestyle=":", color="#bbb", alpha=0.6)
    ax_a.spines["top"].set_visible(False)
    ax_a2.spines["top"].set_visible(False)
    ax_a.set_title(
        "Panel A — commit attribution (user vs autonomous, by day)",
        fontsize=11, pad=10,
    )
    h1, l1 = ax_a.get_legend_handles_labels()
    h2, l2 = ax_a2.get_legend_handles_labels()
    ax_a.legend(h1 + h2, l1 + l2, loc="upper right", frameon=False, fontsize=9)
    if MARKER_DATE in days:
        i = days.index(MARKER_DATE)
        ax_a.axvline(i - 0.5, color="#999", linestyle="--",
                     linewidth=1, alpha=0.7)
        ax_a.text(i - 0.45, ax_a.get_ylim()[1] * 0.92,
                  "Requested-by marker\nconvention starts",
                  fontsize=7, color="#666", va="top", ha="left")

    # Panel B — operator prompts + active session minutes.
    prompts = [session_metrics.get(d, {}).get("prompts", 0) for d in days]
    minutes = [round(session_metrics.get(d, {}).get("active_minutes", 0), 1)
               for d in days]
    has_session_data = any(prompts) or any(minutes)
    ax_b2 = ax_b.twinx()
    ax_b.bar(x, prompts, width=width, label="Operator prompts",
             color="#e08e3e", edgecolor="white", linewidth=0.5)
    ax_b2.plot(x, minutes, color="#0f9d58", linewidth=1.5, marker="s",
               markersize=5, label="Active session min (capped 60/sess)")
    for xi, p in zip(x, prompts):
        if p > 0:
            ax_b.text(xi, p + 0.5, str(p), ha="center", va="bottom",
                      fontsize=8, color="#444")
    ax_b.set_ylabel("operator prompts / day")
    ax_b2.set_ylabel("active session minutes", color="#0f9d58")
    ax_b.set_axisbelow(True)
    ax_b.grid(axis="y", linestyle=":", color="#bbb", alpha=0.6)
    ax_b.spines["top"].set_visible(False)
    ax_b2.spines["top"].set_visible(False)
    panel_b_title = (
        "Panel B — operator engagement (Claude Code session JSONLs)"
        if has_session_data else
        "Panel B — operator engagement (no session data on this machine)"
    )
    ax_b.set_title(panel_b_title, fontsize=11, pad=10)
    h3, l3 = ax_b.get_legend_handles_labels()
    h4, l4 = ax_b2.get_legend_handles_labels()
    ax_b.legend(h3 + h4, l3 + l4, loc="upper right", frameon=False, fontsize=9)
    ax_b.set_xticks(x)
    ax_b.set_xticklabels([d.isoformat()[-5:] for d in days], rotation=0, fontsize=9)
    ax_b.set_xlabel("date (2026-)")

    fig.suptitle(
        "Operator-intervention trend — commits + Claude Code prompts",
        fontsize=13, y=0.995,
    )
    plt.tight_layout(rect=[0, 0, 1, 0.97])
    plt.savefig(OUT_PNG, dpi=140, bbox_inches="tight", facecolor="white")
    rel = OUT_PNG.relative_to(ROOT)
    print(f"wrote {rel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
