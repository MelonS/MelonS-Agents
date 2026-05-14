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
    mtype = ("highlight" if m.name.startswith("highlight-") else
             "summarize" if m.name.startswith("summarize-") else "other")
    rows.append({"mission": f"{m.parent.name}/{m.name}", "type": mtype, **data})

summary = {"count": len(rows), "missions": rows}
if rows:
    pass_count = sum(1 for r in rows if r.get("verdict") == "PASS")
    summary["pass_rate"] = pass_count / len(rows)

out = pathlib.Path("records/metrics/summary.json")
out.write_text(json.dumps(summary, indent=2))

md = [f"# Metrics dashboard\n",
      f"_{len(rows)} mission(s) tracked._\n"]
if rows:
    md.append(f"- Pass rate: {summary['pass_rate']*100:.0f}%")
    md.append("")

    hl = [r for r in rows if r["type"] == "highlight"]
    if hl:
        totals = [r.get("total_s", 0) for r in hl if r.get("total_s")]
        if totals:
            md.append(f"## Highlight missions ({len(hl)})\n")
            md.append(f"- Total wall time (s): min={min(totals):.1f} · "
                      f"median={statistics.median(totals):.1f} · "
                      f"max={max(totals):.1f}\n")
        md.append("| Mission | Verdict | Total (s) | Render (s) | Output dur (s) | Size (MB) |")
        md.append("|---------|---------|-----------|------------|----------------|-----------|")
        for r in hl:
            stages = r.get("stages_s", {})
            md.append(
                f"| {r['mission']} | {r.get('verdict','?')} | "
                f"{r.get('total_s',0):.1f} | {stages.get('render',0):.1f} | "
                f"{r.get('output_duration_s',0):.1f} | {r.get('size_mb',0):.2f} |"
            )
        md.append("")

    sm = [r for r in rows if r["type"] == "summarize"]
    if sm:
        md.append(f"## Summarize missions ({len(sm)})\n")
        md.append("| Mission | Verdict | Transcript words | Summary bytes |")
        md.append("|---------|---------|------------------|---------------|")
        for r in sm:
            md.append(
                f"| {r['mission']} | {r.get('verdict','?')} | "
                f"{r.get('transcript_words','?')} | {r.get('summary_bytes','?')} |"
            )
        md.append("")

docs = pathlib.Path("docs/metrics-dashboard.md")
docs.write_text("\n".join(md) + "\n")
print(f"  wrote {out} and {docs} ({len(rows)} missions)")
