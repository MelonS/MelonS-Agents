"""Mission-outcome trend chart — daily PASS / retry / FAIL composition.

Companion to `generate-intervention-chart.py`.  Where intervention chart
answers "is operator involvement going down?", this one answers "is the
pipeline producing more reliable output over time?".  Both panels read
the same `records/missions/<date>/<mission-id>/` tree.

External-Claude critique 2026-05-22: "'self-evolving' narrative needs a
metric — QA pass rate, retry rate, intervention freq in time series."
Intervention is already charted.  This script covers the other two.

Panel A — Outcome composition per day:
  Stacked bar by outcome class:
    - PASS-1     — succeeded on attempt 1 (the gold standard)
    - PASS-retry — succeeded on attempt 2+ (QA feedback loop worked)
    - FAIL       — all retries exhausted, blocker written
    - NO-QA      — mission has metrics.json but no qa-report.md (the
                   music-video class; no per-mission retry harness, so
                   every metrics.json on disk = the mission's run.sh
                   reached its tail without exiting non-zero).  These
                   render as a lighter tint of PASS-1 — they are not
                   audited the same way but they are deliverables on
                   disk.
  Plus a PASS-1 rate % line overlaid (right axis) — the
  reliability-on-first-try signal.

Panel B — Mission-type composition per day:
  Stacked bar by mission type (music-video / faceless-short /
  highlight / summarize / shorts-batch / unknown).  Shows the
  pipeline's evolution from the v1 highlight era through faceless
  pivot to the current music-video focus.

Output: docs/metrics/quality-trend-{en,ko}.png + quality-trend.json
        + docs/metrics/quality-trend.png (alias for legacy refs).
"""

import argparse
import json
import pathlib
import re
import sys
from collections import Counter, defaultdict
from datetime import date, datetime

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from matplotlib import font_manager as _fm  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
MISSIONS_DIR = ROOT / "records" / "missions"
OUT_DIR = ROOT / "docs" / "metrics"
OUT_JSON = OUT_DIR / "quality-trend.json"
OUT_DIR.mkdir(parents=True, exist_ok=True)
SITE_ASSETS = ROOT / "site" / "assets"

LABELS = {
    "en": {
        "title":         "Mission-outcome trend",
        "panel_a":       "Outcome composition (per day)",
        "panel_b":       "Mission-type composition (per day)",
        "y_count":       "missions / day",
        "y_rate":        "PASS-on-first-try %",
        "x_axis":        "date (2026-)",
        "lg_pass1":      "PASS attempt 1",
        "lg_retry":      "PASS after retry",
        "lg_fail":       "FAIL (blocker)",
        "lg_noqa":       "metrics.json only (no QA retry harness)",
        "lg_rate":       "PASS-1 rate %",
        "lg_type":       lambda t: t,
        "subtitle":      "data: records/missions/<date>/<id>/qa-report.md + metrics.json",
    },
    "ko": {
        "title":         "미션 결과 추세",
        "panel_a":       "결과 구성 (일별)",
        "panel_b":       "미션 타입 구성 (일별)",
        "y_count":       "미션 / 일",
        "y_rate":        "1회 시도 PASS %",
        "x_axis":        "날짜 (2026-)",
        "lg_pass1":      "1회 시도 PASS",
        "lg_retry":      "재시도 후 PASS",
        "lg_fail":       "FAIL (blocker)",
        "lg_noqa":       "metrics.json 만 있음 (QA retry 없음)",
        "lg_rate":       "1회 시도 PASS %",
        "lg_type":       lambda t: t,
        "subtitle":      "데이터: records/missions/<date>/<id>/qa-report.md + metrics.json",
    },
}


def select_font(lang: str):
    if lang != "ko":
        return
    candidates = ["Apple SD Gothic Neo", "AppleGothic", "Nanum Gothic", "Noto Sans CJK KR"]
    available = {f.name for f in _fm.fontManager.ttflist}
    for name in candidates:
        if name in available:
            plt.rcParams["font.family"] = name
            plt.rcParams["axes.unicode_minus"] = False
            return
    print(f"[warn] no CJK font found for lang=ko; KO labels may tofu", file=sys.stderr)


ATTEMPT_RE = re.compile(r"attempt\s+(\d+)\s+of\s+\d+", re.IGNORECASE)
VERDICT_RE = re.compile(r"\*\*Verdict\*\*:\s*([A-Z]+)", re.IGNORECASE)


def mission_type_from_id(mission_id: str) -> str:
    """Mission type prefix.  Newer music-video uses repeated prefixes
    like `music-video-skill-music-video-...` — collapse to `music-video`.
    Faceless follow-up renders use `faceless-<topic>` — collapse to
    `faceless-short` (the original mission name).
    """
    if mission_id.startswith("music-video"):
        return "music-video"
    if mission_id.startswith("faceless"):
        return "faceless-short"
    if mission_id.startswith("highlight"):
        return "highlight"
    if mission_id.startswith("summarize"):
        return "summarize"
    if mission_id.startswith("shorts-batch"):
        return "shorts-batch"
    return "unknown"


def classify_outcome(mission_dir: pathlib.Path) -> str:
    qa_path = mission_dir / "qa-report.md"
    metrics_path = mission_dir / "metrics.json"
    if qa_path.exists():
        try:
            body = qa_path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            return "no-qa" if metrics_path.exists() else "incomplete"
        attempt_m = ATTEMPT_RE.search(body)
        verdict_m = VERDICT_RE.search(body)
        attempt = int(attempt_m.group(1)) if attempt_m else 1
        verdict = verdict_m.group(1).upper() if verdict_m else ""
        if verdict == "PASS" and attempt == 1:
            return "pass-1"
        if verdict == "PASS":
            return "pass-retry"
        if verdict == "FAIL":
            return "fail"
        # Some early qa-reports omit attempt line entirely → treat as PASS-1 by default
        return "pass-1" if verdict == "PASS" else "fail"
    if metrics_path.exists():
        return "no-qa"
    return "incomplete"


def collect():
    """Walk records/missions/<date>/<mission-id>/ and classify each.

    Skips:
      - Anything outside `records/missions/`.
      - Mission dirs without metrics.json AND without qa-report.md
        (likely abandoned mid-run).
    """
    rows = []
    if not MISSIONS_DIR.exists():
        return rows
    for date_dir in sorted(MISSIONS_DIR.iterdir()):
        if not date_dir.is_dir():
            continue
        try:
            d = date.fromisoformat(date_dir.name)
        except ValueError:
            continue
        for mission_dir in sorted(date_dir.iterdir()):
            if not mission_dir.is_dir():
                continue
            outcome = classify_outcome(mission_dir)
            if outcome == "incomplete":
                continue  # abandoned mid-run, don't pollute the chart
            rows.append({
                "date": d,
                "mission_id": mission_dir.name,
                "type": mission_type_from_id(mission_dir.name),
                "outcome": outcome,
            })
    return rows


def aggregate(rows):
    """Per-day counts of outcome + type."""
    days = sorted({r["date"] for r in rows})
    outcome_classes = ["pass-1", "pass-retry", "fail", "no-qa"]
    type_classes = ["music-video", "faceless-short", "highlight", "summarize",
                    "shorts-batch", "unknown"]
    per_day = {}
    for d in days:
        oc = Counter(r["outcome"] for r in rows if r["date"] == d)
        tc = Counter(r["type"] for r in rows if r["date"] == d)
        total = sum(oc.values())
        # PASS-1 rate among qa'd-or-implicit-pass missions; treat no-qa as PASS-1
        pass1_eq = oc["pass-1"] + oc["no-qa"]
        rate = (pass1_eq / total * 100.0) if total else 0.0
        per_day[d] = {
            "outcomes": {k: oc[k] for k in outcome_classes},
            "types":    {k: tc[k] for k in type_classes},
            "total":    total,
            "pass1_rate": rate,
        }
    return per_day, outcome_classes, type_classes


# Per-class colors — picked to be colorblind-distinguishable
OUTCOME_COLORS = {
    "pass-1":     "#2e9555",  # green
    "pass-retry": "#cfa320",  # amber
    "fail":       "#c84a4a",  # red
    "no-qa":      "#9bc3a1",  # pale green (PASS-1 sibling)
}
TYPE_COLORS = {
    "music-video":    "#3a90d6",
    "faceless-short": "#8b6cd6",
    "highlight":      "#4aa0a0",
    "summarize":      "#a0a04a",
    "shorts-batch":   "#d68b3a",
    "unknown":        "#9a9a9a",
}


def render(per_day, outcome_classes, type_classes, lang: str, out_path: pathlib.Path):
    L = LABELS[lang]
    select_font(lang)
    days = sorted(per_day.keys())
    if not days:
        print("[warn] no mission data", file=sys.stderr)
        return
    x = list(range(len(days)))
    fig, (axA, axB) = plt.subplots(2, 1, figsize=(11, 8), sharex=True)

    # Panel A — outcome stacked bar
    outcome_label_map = {
        "pass-1": "lg_pass1", "pass-retry": "lg_retry",
        "fail": "lg_fail", "no-qa": "lg_noqa",
    }
    bottom = [0] * len(days)
    for cls in outcome_classes:
        heights = [per_day[d]["outcomes"][cls] for d in days]
        axA.bar(x, heights, bottom=bottom,
                label=L[outcome_label_map[cls]],
                color=OUTCOME_COLORS[cls], edgecolor="white", linewidth=0.5)
        bottom = [b + h for b, h in zip(bottom, heights)]
    axA.set_ylabel(L["y_count"])
    axA.set_title(L["panel_a"], loc="left", fontsize=11)
    axA.grid(axis="y", linestyle=":", color="#cccccc", alpha=0.7)
    axA.set_axisbelow(True)

    # PASS-1 rate as overlay on right axis
    axA2 = axA.twinx()
    rates = [per_day[d]["pass1_rate"] for d in days]
    axA2.plot(x, rates, color="#1f6fb8", marker="o", markersize=4,
              linewidth=1.5, label=L["lg_rate"])
    axA2.set_ylabel(L["y_rate"], color="#1f6fb8")
    axA2.tick_params(axis="y", labelcolor="#1f6fb8")
    axA2.set_ylim(0, 105)
    # Combine legends
    handlesA, labelsA = axA.get_legend_handles_labels()
    handlesA2, labelsA2 = axA2.get_legend_handles_labels()
    axA.legend(handlesA + handlesA2, labelsA + labelsA2,
               loc="upper left", fontsize=8, framealpha=0.9)

    # Panel B — type stacked bar
    bottom = [0] * len(days)
    for cls in type_classes:
        heights = [per_day[d]["types"][cls] for d in days]
        if sum(heights) == 0:
            continue
        axB.bar(x, heights, bottom=bottom, label=cls,
                color=TYPE_COLORS[cls], edgecolor="white", linewidth=0.5)
        bottom = [b + h for b, h in zip(bottom, heights)]
    axB.set_ylabel(L["y_count"])
    axB.set_title(L["panel_b"], loc="left", fontsize=11)
    axB.legend(loc="upper left", fontsize=8, framealpha=0.9)
    axB.grid(axis="y", linestyle=":", color="#cccccc", alpha=0.7)
    axB.set_axisbelow(True)

    # Shared x-axis ticks — show MM-DD
    axB.set_xticks(x)
    axB.set_xticklabels([d.strftime("%m-%d") for d in days], rotation=45, ha="right")
    axB.set_xlabel(L["x_axis"])

    fig.suptitle(L["title"], fontsize=13, fontweight="bold", x=0.05, ha="left")
    fig.text(0.05, 0.92, L["subtitle"], fontsize=8, color="#5a5a5a")
    fig.tight_layout(rect=[0, 0, 1, 0.93])
    fig.savefig(out_path, dpi=130)
    plt.close(fig)
    print(f"  ✓ wrote {out_path}")


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--lang", choices=["en", "ko", "both"], default="both")
    args = ap.parse_args()

    rows = collect()
    per_day, outcome_classes, type_classes = aggregate(rows)
    langs = ["en", "ko"] if args.lang == "both" else [args.lang]
    for lang in langs:
        out = OUT_DIR / f"quality-trend-{lang}.png"
        render(per_day, outcome_classes, type_classes, lang, out)
        # Mirror to site/assets so Pages picks it up
        if SITE_ASSETS.exists():
            mirror = SITE_ASSETS / f"quality-trend-{lang}.png"
            mirror.write_bytes(out.read_bytes())
            print(f"  ✓ mirrored to {mirror}")

    # Default unscoped alias (legacy + Markdown convenience): EN copy
    if "en" in langs:
        alias = OUT_DIR / "quality-trend.png"
        alias.write_bytes((OUT_DIR / "quality-trend-en.png").read_bytes())
        if SITE_ASSETS.exists():
            (SITE_ASSETS / "quality-trend.png").write_bytes(alias.read_bytes())

    # JSON dump
    raw = {
        "generated_at": datetime.now().astimezone().isoformat(),
        "total_missions": len(rows),
        "per_day": {
            d.isoformat(): {
                "outcomes": v["outcomes"],
                "types": v["types"],
                "total": v["total"],
                "pass1_rate_pct": round(v["pass1_rate"], 1),
            } for d, v in per_day.items()
        },
    }
    OUT_JSON.write_text(json.dumps(raw, indent=2))
    print(f"  ✓ wrote {OUT_JSON}")


if __name__ == "__main__":
    main()
