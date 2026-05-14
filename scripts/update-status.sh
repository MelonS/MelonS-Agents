#!/usr/bin/env bash
# Update the auto-managed Status checklist in README.md.
# Usage:
#   scripts/update-status.sh check "<item text>"     # flip [ ] -> [x]
#   scripts/update-status.sh add   "<item text>"     # append as [x]
#   scripts/update-status.sh todo  "<item text>"     # append as [ ]
set -u
cd "$(dirname "${BASH_SOURCE[0]}")/.."

OP="${1:-}"; shift || true
ITEM="${1:-}"
[[ -z "$OP" || -z "$ITEM" ]] && { echo "usage: $0 {check|add|todo} <item>"; exit 64; }

python3 - "$OP" "$ITEM" <<'PYEOF'
import sys, re, pathlib
op, item = sys.argv[1], sys.argv[2]
p = pathlib.Path("README.md")
src = p.read_text()

# Status block delimited by <!-- status:start --> ... <!-- status:end -->
m = re.search(r"(<!-- status:start -->)(.*?)(<!-- status:end -->)", src, re.DOTALL)
if not m:
    print("FAIL: status block markers missing in README.md")
    sys.exit(1)
block = m.group(2)

if op == "check":
    pattern = re.compile(rf"- \[ \] {re.escape(item)}", re.MULTILINE)
    if not pattern.search(block):
        print(f"  '{item}' not in block as unchecked — appending as checked")
        block = block.rstrip() + f"\n- [x] {item}\n"
    else:
        block = pattern.sub(f"- [x] {item}", block, count=1)
elif op == "add":
    block = block.rstrip() + f"\n- [x] {item}\n"
elif op == "todo":
    block = block.rstrip() + f"\n- [ ] {item}\n"
else:
    print(f"unknown op: {op}")
    sys.exit(64)

src = src[:m.start(2)] + block + src[m.end(2):]
p.write_text(src)
print(f"  status {op}: {item}")
PYEOF
