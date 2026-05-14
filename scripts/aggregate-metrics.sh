#!/usr/bin/env bash
# Aggregate per-mission metrics.json into records/metrics/summary.json
# and a docs/metrics-dashboard.md for human reading.
set -u
cd "$(dirname "${BASH_SOURCE[0]}")/.."

mkdir -p records/metrics docs

python3 <<'PYEOF'
import json, pathlib, statistics

root = pathlib.Path("records/missions")
rows = []
for m in sorted(root.glob("*/*-*")):
    f = m / "metrics.json"
    if not f.exists():
        continue
    try:
        data = json.loads(f.read_text())
    except Exception:
        continue
    rows.append({"mission": f"{m.parent.name}/{m.name}", **data})

summary = {
    "missions": rows,
    "count": len(rows),
}
if rows:
    totals = [r["total_s"] for r in rows]
    summary["total_s"] = {
        "min": min(totals), "max": max(totals),
        "mean": statistics.mean(totals),
        "median": statistics.median(totals),
    }
    pass_count = sum(1 for r in rows if r.get("verdict") == "PASS")
    summary["pass_rate"] = pass_count / len(rows)

out = pathlib.Path("records/metrics/summary.json")
out.write_text(json.dumps(summary, indent=2))

# Markdown dashboard
md = ["# Metrics dashboard\n",
      f"_{len(rows)} mission(s) tracked._\n"]
if rows:
    md.append(f"- Pass rate: {summary['pass_rate']*100:.0f}%")
    t = summary["total_s"]
    md.append(f"- Total wall time (s): min={t['min']:.1f} · median={t['median']:.1f} · max={t['max']:.1f}")
    md.append("")
    md.append("| Mission | Verdict | Total (s) | Render (s) | Output dur (s) | Size (MB) |")
    md.append("|---------|---------|-----------|------------|----------------|-----------|")
    for r in rows:
        stages = r.get("stages_s", {})
        md.append("| {mission} | {v} | {t:.1f} | {rs:.1f} | {od:.1f} | {sz:.2f} |".format(
            mission=r["mission"],
            v=r.get("verdict", "?"),
            t=r.get("total_s", 0),
            rs=stages.get("render", 0),
            od=r.get("output_duration_s", 0),
            sz=r.get("size_mb", 0),
        ))
docs = pathlib.Path("docs/metrics-dashboard.md")
docs.write_text("\n".join(md) + "\n")
print(f"  wrote {out} and {docs} ({len(rows)} missions)")
PYEOF
