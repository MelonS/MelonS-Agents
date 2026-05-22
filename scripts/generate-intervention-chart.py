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

import argparse
import json
import pathlib
import re
import shutil
import statistics
import subprocess
import sys
from collections import Counter, defaultdict
from datetime import date, datetime, timedelta, timezone

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from matplotlib import font_manager as _fm  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "docs" / "metrics"
OUT_JSON = OUT_DIR / "intervention.json"
OUT_DIR.mkdir(parents=True, exist_ok=True)

# Bilingual label dictionary.  Plot rendering picks one variant; both
# variants are emitted as separate PNG files so README.md uses the EN
# variant and README.ko.md uses the KO variant.
LABELS = {
    "en": {
        "title":        "Operator-intervention trend",
        "panel_a":      "Daily commit attribution",
        "panel_b":      "Operator engagement (Claude Code sessions)",
        "panel_b_none": "Operator engagement (no session data here)",
        "y_commits":    "commits / day",
        "y_ratio":      "user-initiated %",
        "y_prompts":    "operator prompts / day",
        "y_minutes":    "active session minutes",
        "legend_agent": "Agent-autonomous",
        "legend_user":  "User-initiated",
        "legend_pct":   "User-initiated %",
        "legend_prompts": "Operator prompts",
        "legend_minutes": "Active session min (60-cap/session)",
        "x_axis":       "date (2026-)",
    },
    "ko": {
        "title":        "운영자 개입 추세",
        "panel_a":      "일별 커밋 분류",
        "panel_b":      "운영자 참여도 (Claude Code 세션)",
        "panel_b_none": "운영자 참여도 (이 머신엔 세션 데이터 없음)",
        "y_commits":    "커밋 / 일",
        "y_ratio":      "운영자 주도 %",
        "y_prompts":    "운영자 프롬프트 / 일",
        "y_minutes":    "활성 세션 분",
        "legend_agent": "에이전트 자율",
        "legend_user":  "운영자 주도",
        "legend_pct":   "운영자 주도 %",
        "legend_prompts": "운영자 프롬프트",
        "legend_minutes": "활성 세션 분 (세션당 60분 cap)",
        "x_axis":       "날짜 (2026-)",
    },
}


def select_font(lang: str):
    """Pick a font that can render the target language and configure
    matplotlib to use it.  Korean needs a CJK font; English falls back
    to matplotlib's default sans-serif."""
    if lang != "ko":
        return  # default sans-serif handles Latin + numerics fine
    candidates = [
        "Apple SD Gothic Neo",
        "AppleGothic",
        "Nanum Gothic",
        "Noto Sans CJK KR",
    ]
    available = {f.name for f in _fm.fontManager.ttflist}
    for name in candidates:
        if name in available:
            plt.rcParams["font.family"] = name
            plt.rcParams["axes.unicode_minus"] = False
            return
    # No CJK font — KO labels will render as tofu boxes.  Warn but proceed.
    print(f"[warn] no CJK font found for lang=ko; trying default", file=sys.stderr)

# Claude Code session JSONLs.  Claude Code encodes the working
# directory into the JSONL parent dir name by replacing `/` with `-`,
# so a repo at /Users/melons/ai becomes "-Users-melons-ai".  Derive
# from ROOT instead of hardcoding so a fresh clone in any path picks
# up the right folder on its own machine.
_cc_key = str(ROOT).replace("/", "-")
CC_PROJECT_DIR = pathlib.Path.home() / ".claude" / "projects" / _cc_key

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


def render_chart(days, commit_metrics, session_metrics, lang: str, out_png: pathlib.Path):
    """Render the 2-panel chart in the given language to out_png."""
    L = LABELS[lang]
    select_font(lang)

    x = list(range(len(days)))
    user_counts = [commit_metrics.get(d, {}).get("user", 0) for d in days]
    agent_counts = [commit_metrics.get(d, {}).get("agent", 0) for d in days]
    totals = [u + a for u, a in zip(user_counts, agent_counts)]
    user_ratio = [(u / t * 100) if t else 0 for u, t in zip(user_counts, totals)]

    prompts = [session_metrics.get(d, {}).get("prompts", 0) for d in days]
    minutes = [round(session_metrics.get(d, {}).get("active_minutes", 0), 1)
               for d in days]
    has_session_data = any(prompts) or any(minutes)

    # Larger figure + extra hspace to fit legends below each panel
    # without overlapping bars.  height_ratios slightly favors panel A
    # since it's the headline signal.
    fig, (ax_a, ax_b) = plt.subplots(
        2, 1, figsize=(14, 9), sharex=False,
        gridspec_kw={"height_ratios": [3, 2], "hspace": 0.45},
    )

    width = 0.7
    # ───── Panel A — commit stack + ratio line ────────────────────────
    ax_a2 = ax_a.twinx()
    ax_a.bar(x, agent_counts, width=width, label=L["legend_agent"],
             color="#3a90d6", edgecolor="white", linewidth=0.5)
    ax_a.bar(x, user_counts, width=width, bottom=agent_counts,
             label=L["legend_user"], color="#d94a4a",
             edgecolor="white", linewidth=0.5)
    ax_a2.plot(x, user_ratio, color="#222", linewidth=2.0, marker="o",
               markersize=7, label=L["legend_pct"], zorder=10)
    # Ratio % labels only — one signal per bar, not two.  Position below
    # the line marker when ratio is high (>50) so it doesn't escape the
    # plot area; above otherwise.
    for xi, r, t in zip(x, user_ratio, totals):
        if t > 0:
            offset = -6 if r > 80 else 4
            va = "top" if r > 80 else "bottom"
            ax_a2.text(xi, r + offset, f"{r:.0f}%", ha="center", va=va,
                       fontsize=10, color="#222", fontweight="bold")

    ax_a.set_ylabel(L["y_commits"], fontsize=11)
    ax_a2.set_ylabel(L["y_ratio"], color="#222", fontsize=11)
    ax_a2.set_ylim(0, 110)
    # Headroom on Panel A so the % labels above peaks don't clip the title.
    ax_a.set_ylim(0, max(totals + [10]) * 1.18)
    ax_a.set_axisbelow(True)
    ax_a.grid(axis="y", linestyle=":", color="#bbb", alpha=0.5)
    ax_a.spines["top"].set_visible(False)
    ax_a2.spines["top"].set_visible(False)
    ax_a.set_title(L["panel_a"], fontsize=13, pad=8, loc="left", fontweight="bold")
    ax_a.set_xticks(x)
    ax_a.set_xticklabels([d.isoformat()[-5:] for d in days], fontsize=10)

    # Single combined legend below Panel A, centered, frameless.
    h1, l1 = ax_a.get_legend_handles_labels()
    h2, l2 = ax_a2.get_legend_handles_labels()
    ax_a.legend(h1 + h2, l1 + l2, loc="upper center",
                bbox_to_anchor=(0.5, -0.13), ncol=3, frameon=False, fontsize=10)

    # ───── Panel B — operator prompts + active session minutes ────────
    ax_b2 = ax_b.twinx()
    ax_b.bar(x, prompts, width=width, label=L["legend_prompts"],
             color="#e08e3e", edgecolor="white", linewidth=0.5)
    ax_b2.plot(x, minutes, color="#0f9d58", linewidth=2.0, marker="s",
               markersize=6, label=L["legend_minutes"], zorder=10)

    # Prompt count labels inside or above bars depending on height.
    max_p = max(prompts + [1])
    for xi, p in zip(x, prompts):
        if p > 0:
            if p > max_p * 0.15:  # tall bar — label inside
                ax_b.text(xi, p - max_p * 0.05, str(p), ha="center",
                          va="top", fontsize=9, color="white", fontweight="bold")
            else:  # short bar — label above
                ax_b.text(xi, p + max_p * 0.02, str(p), ha="center",
                          va="bottom", fontsize=9, color="#444")

    ax_b.set_ylabel(L["y_prompts"], fontsize=11)
    ax_b2.set_ylabel(L["y_minutes"], color="#0f9d58", fontsize=11)
    ax_b.set_ylim(0, max_p * 1.15)
    ax_b.set_axisbelow(True)
    ax_b.grid(axis="y", linestyle=":", color="#bbb", alpha=0.5)
    ax_b.spines["top"].set_visible(False)
    ax_b2.spines["top"].set_visible(False)
    panel_b_title = L["panel_b"] if has_session_data else L["panel_b_none"]
    ax_b.set_title(panel_b_title, fontsize=13, pad=8, loc="left", fontweight="bold")
    ax_b.set_xticks(x)
    ax_b.set_xticklabels([d.isoformat()[-5:] for d in days], fontsize=10)
    ax_b.set_xlabel(L["x_axis"], fontsize=11)

    h3, l3 = ax_b.get_legend_handles_labels()
    h4, l4 = ax_b2.get_legend_handles_labels()
    ax_b.legend(h3 + h4, l3 + l4, loc="upper center",
                bbox_to_anchor=(0.5, -0.18), ncol=2, frameon=False, fontsize=10)

    fig.suptitle(L["title"], fontsize=16, fontweight="bold", y=0.995)
    plt.tight_layout(rect=[0, 0, 1, 0.96])
    plt.savefig(out_png, dpi=140, bbox_inches="tight", facecolor="white")
    plt.close(fig)
    print(f"wrote {out_png.relative_to(ROOT)}")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--lang", choices=["en", "ko", "both"], default="both",
                    help="render chart in this language; default 'both' emits "
                         "intervention-en.png + intervention-ko.png + intervention.png alias")
    args = ap.parse_args()

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

    # Render chart per requested language.  Default `both` emits two
    # PNGs (intervention-en.png, intervention-ko.png) plus a copy at
    # intervention.png (= EN) for backward compat with prior references.
    langs = ["en", "ko"] if args.lang == "both" else [args.lang]
    for lang in langs:
        out_png = OUT_DIR / f"intervention-{lang}.png"
        render_chart(days, commit_metrics, session_metrics, lang, out_png)
    # Backward-compat alias.
    en_path = OUT_DIR / "intervention-en.png"
    if en_path.exists():
        shutil.copy(en_path, OUT_DIR / "intervention.png")
        print(f"wrote docs/metrics/intervention.png (alias of en)")
    return 0


# ─────────────────────────────────────────────────────────────────────
# DEAD CODE BELOW — kept as a sentinel to ensure no other path falls
# through into the legacy plotting routine.  Stripped on next refactor.
# ─────────────────────────────────────────────────────────────────────
def _legacy_unused():
    x = []  # noqa: F841

if __name__ == "__main__":
    raise SystemExit(main())
