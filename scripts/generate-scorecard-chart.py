"""Render docs/pilots/scorecard.json into docs/metrics/scorecard.png.

The scorecard answers the operator's "I can't tell what's improving"
question.  Thumbnails alone don't carry the evolution signal; the
score-per-version-per-dimension does.

Output is a stacked horizontal bar chart, grouped by topic.  Each bar
is one (topic, version) pair; each segment is one of the five
dimensions.  Total at the right edge of each bar makes the
version-over-version delta scannable in one glance.

Regenerate after editing the JSON:
    .venv/bin/python scripts/generate-scorecard-chart.py
"""

import json
import pathlib

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
DATA = ROOT / "docs" / "pilots" / "scorecard.json"
OUT = ROOT / "docs" / "metrics" / "scorecard.png"
OUT.parent.mkdir(parents=True, exist_ok=True)

with DATA.open() as f:
    payload = json.load(f)

dims = payload["score_dimensions"]
dim_labels = {
    "hook_strength":         "Hook",
    "visual_caption_sync":   "Visual sync",
    "caption_readability":   "Readability",
    "factual_coherence":     "Factual",
    "production_polish":     "Polish",
}
dim_colors = {
    "hook_strength":         "#d94a4a",
    "visual_caption_sync":   "#d99540",
    "caption_readability":   "#d9c540",
    "factual_coherence":     "#5fb44b",
    "production_polish":     "#3a90d6",
}

rows = payload["scores"]
labels = [f"{r['topic']} · {r['version']}" for r in rows]
y_pos = list(range(len(rows)))

fig, ax = plt.subplots(figsize=(11, max(3, 0.7 * len(rows) + 1.5)))

left = [0] * len(rows)
for d in dims:
    vals = [r[d] for r in rows]
    ax.barh(
        y_pos,
        vals,
        left=left,
        height=0.65,
        color=dim_colors[d],
        edgecolor="white",
        linewidth=0.7,
        label=dim_labels[d],
    )
    left = [l + v for l, v in zip(left, vals)]

# Total label at the end of each bar.
for i, r in enumerate(rows):
    ax.text(
        r["total"] + 0.7,
        i,
        f"{r['total']}/50",
        va="center",
        fontsize=10,
        fontweight="bold",
        color="#333",
    )

ax.set_yticks(y_pos)
ax.set_yticklabels(labels, fontsize=10)
ax.invert_yaxis()  # v4 on top, v6 on bottom of its group
ax.set_xlim(0, 55)
ax.set_xlabel("Score (each dimension 0–10, total 0–50)")
ax.set_title("Pilot scorecard — Claude self-evaluation across pipeline versions", fontsize=12, pad=14)
ax.set_axisbelow(True)
ax.grid(axis="x", linestyle=":", color="#bbb", alpha=0.6)
ax.spines["top"].set_visible(False)
ax.spines["right"].set_visible(False)

ax.legend(
    loc="lower right",
    bbox_to_anchor=(1.0, -0.20),
    ncol=5,
    frameon=False,
    fontsize=9,
)

plt.tight_layout()
plt.savefig(OUT, dpi=140, bbox_inches="tight", facecolor="white")
print(f"wrote {OUT.relative_to(ROOT)}")
