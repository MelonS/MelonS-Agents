"""Generate per-mission metric charts as PNG into docs/metrics/.

Reads gitignored `records/missions/*/metrics.json` and writes committed
evidence PNGs to `docs/metrics/`. The README embeds them directly so a
read-only reviewer sees pipeline behaviour without cloning the repo.

First-time setup:
    scripts/setup-venv.sh                    # one-off: creates .venv/, installs matplotlib

Regenerate the charts (after a new mission lands):
    .venv/bin/python scripts/generate-charts.py
"""

import json
import pathlib

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

ROOT = pathlib.Path("records/missions")
OUT_DIR = pathlib.Path("docs/metrics")
OUT_DIR.mkdir(parents=True, exist_ok=True)


def collect_rows():
    rows = []
    for mission_dir in sorted(ROOT.glob("*/*-*")):
        metrics_path = mission_dir / "metrics.json"
        if not metrics_path.exists():
            continue
        try:
            data = json.loads(metrics_path.read_text())
        except Exception:
            continue
        name = mission_dir.name
        if name.startswith("highlight-"):
            mtype = "highlight"
        elif name.startswith("summarize-"):
            mtype = "summarize"
        elif name.startswith("shorts-batch-"):
            mtype = "shorts-batch"
        else:
            mtype = "other"
        rows.append(
            {
                "mission": f"{mission_dir.parent.name}/{mission_dir.name}",
                "type": mtype,
                **data,
            }
        )
    return rows


def chart_stage_breakdown(highlight_rows):
    """Per-highlight stacked horizontal bar — where the seconds go."""
    rows = sorted(highlight_rows, key=lambda r: r["mission"])
    if not rows:
        return None

    labels = [r["mission"].split("/", 1)[1] for r in rows]
    transcribe = [r.get("stages_s", {}).get("transcribe", 0) for r in rows]
    select = [r.get("stages_s", {}).get("select", 0) for r in rows]
    render = [r.get("stages_s", {}).get("render", 0) for r in rows]
    totals = [r.get("total_s", 0) for r in rows]
    # Difference between total_s and the three tracked stages is shell
    # overhead, retry attempts, or QA — bucket it as "other" so bars
    # equal the total and the label position is honest.
    other = [
        max(0.0, t - (a + b + c))
        for t, a, b, c in zip(totals, transcribe, select, render)
    ]

    fig, ax = plt.subplots(figsize=(9, 4.6), dpi=140)
    y = list(range(len(labels)))
    ax.barh(y, transcribe, color="#5B8DEF", label="transcribe · whisper.cpp")
    ax.barh(
        y,
        select,
        left=transcribe,
        color="#9B7BEA",
        label="select · ollama",
    )
    ax.barh(
        y,
        render,
        left=[a + b for a, b in zip(transcribe, select)],
        color="#E68A4C",
        label="render · ffmpeg",
    )
    ax.barh(
        y,
        other,
        left=[a + b + c for a, b, c in zip(transcribe, select, render)],
        color="#C9CBD3",
        label="other · QA + retry + shell",
    )
    for i, total in enumerate(totals):
        ax.text(
            total + 1.0,
            i,
            f"{total:.1f}s",
            va="center",
            fontsize=8,
            color="#444",
        )

    ax.set_yticks(y)
    ax.set_yticklabels(labels, fontsize=8)
    ax.invert_yaxis()
    ax.set_xlim(0, max(totals) * 1.12)
    ax.set_xlabel("seconds (Tier 2 — local CPU / GPU)")
    ax.set_title(
        "Per-mission time breakdown — highlight missions\n"
        "All bars run on local tools.  Zero Anthropic tokens spent "
        "during these stages.",
        fontsize=11,
    )
    ax.legend(loc="lower right", fontsize=8, framealpha=0.95)
    ax.grid(axis="x", linestyle="--", alpha=0.3)
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)
    fig.tight_layout()

    out = OUT_DIR / "per-mission-time.png"
    fig.savefig(out, bbox_inches="tight")
    plt.close(fig)
    return out


def chart_realtime_ratio(highlight_rows):
    """How many seconds of output produced per second of compute."""
    rows = sorted(highlight_rows, key=lambda r: r["mission"])
    rows = [
        r
        for r in rows
        if r.get("total_s") and r.get("output_duration_s")
    ]
    if not rows:
        return None

    labels = [r["mission"].split("/", 1)[1] for r in rows]
    ratios = [r["output_duration_s"] / r["total_s"] for r in rows]
    colors = [
        "#3FAE6A" if r >= 1.0 else "#E07A7A" for r in ratios
    ]

    fig, ax = plt.subplots(figsize=(9, 3.8), dpi=140)
    bars = ax.bar(labels, ratios, color=colors, width=0.65)
    ax.axhline(1.0, color="#888", linestyle="--", linewidth=1)
    ax.text(
        -0.45,
        1.0,
        "1×  real-time",
        ha="left",
        va="center",
        fontsize=8,
        color="#666",
        backgroundcolor="white",
    )
    for bar, ratio in zip(bars, ratios):
        ax.text(
            bar.get_x() + bar.get_width() / 2,
            bar.get_height() + 0.03,
            f"{ratio:.2f}×",
            ha="center",
            va="bottom",
            fontsize=8,
            color="#444",
        )

    ax.set_ylabel("output seconds  ÷  total compute seconds")
    ax.set_title(
        "Throughput — output duration produced per second of compute\n"
        "(above 1.0× = faster than real-time on this machine)",
        fontsize=11,
    )
    ax.set_ylim(0, max(ratios) * 1.30)
    ax.set_xlim(-0.6, len(labels) - 0.4)
    ax.tick_params(axis="x", labelrotation=20, labelsize=8)
    ax.grid(axis="y", linestyle="--", alpha=0.3)
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)
    fig.tight_layout()

    out = OUT_DIR / "throughput-realtime.png"
    fig.savefig(out, bbox_inches="tight")
    plt.close(fig)
    return out


def main():
    rows = collect_rows()
    highlights = [r for r in rows if r["type"] == "highlight"]
    print(f"collected {len(rows)} missions ({len(highlights)} highlight)")

    a = chart_stage_breakdown(highlights)
    b = chart_realtime_ratio(highlights)
    for p in (a, b):
        if p:
            print(f"wrote {p}")


if __name__ == "__main__":
    main()
