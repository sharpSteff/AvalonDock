#!/usr/bin/env bash
# Linux runtime smoke test for the LibreWPF portable leg.
#
# Launches MVVMTestApp (which embeds the LeXtudio DevFlow HTTP agent) under Xvfb
# and drives AvalonDock through the agent: seed documents, float an anchorable
# (creates a real floating window), and assert the layout model reflects it.
#
# Prerequisites:
#   - eng/use-public-librewpf.sh applied (or a local LibreWPF SDK)
#   - MVVMTestApp built for $LIBREWPF_TFM
#   - Xvfb, mesa-vulkan-drivers (lavapipe), fonts incl. eng/linux-test-fonts.sh
set -uo pipefail

TFM="${LIBREWPF_TFM:-net10.0-windows}"
PORT="${DEVFLOW_AGENT_PORT:-9223}"
BASE="http://127.0.0.1:${PORT}"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP_DLL="$REPO_ROOT/source/MVVMTestApp/bin/Release/$TFM/MVVMTestApp.dll"
LOG_DIR="${SMOKE_LOG_DIR:-$REPO_ROOT/smoke-logs}"
mkdir -p "$LOG_DIR"

fail() { echo "SMOKE_RESULT: FAIL - $1"; [ -f "$LOG_DIR/app.log" ] && tail -40 "$LOG_DIR/app.log"; exit 1; }

[ -f "$APP_DLL" ] || fail "MVVMTestApp not built at $APP_DLL"

export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/tmp/xdg-smoke}"
mkdir -p "$XDG_RUNTIME_DIR" && chmod 700 "$XDG_RUNTIME_DIR"

XVFB_PID=""
if [ -z "${DISPLAY:-}" ]; then
	Xvfb :99 -screen 0 1600x1000x24 > "$LOG_DIR/xvfb.log" 2>&1 &
	XVFB_PID=$!
	export DISPLAY=:99
	sleep 1
fi

dotnet "$APP_DLL" > "$LOG_DIR/app.log" 2>&1 &
APP_PID=$!
cleanup() {
	kill "$APP_PID" 2>/dev/null
	[ -n "$XVFB_PID" ] && kill "$XVFB_PID" 2>/dev/null
}
trap cleanup EXIT

echo "waiting for DevFlow agent on $BASE ..."
for _ in $(seq 1 60); do
	curl -sf --max-time 2 -o /dev/null "$BASE/api/v1/agent/status" && break
	kill -0 "$APP_PID" 2>/dev/null || fail "app exited during startup"
	sleep 1
done
curl -sf --max-time 2 -o /dev/null "$BASE/api/v1/agent/status" || fail "agent did not come up in 60s"
echo "agent is up"

invoke() {
	local action=$1 args=${2:-[]}
	curl -sf --max-time 30 -X POST "$BASE/api/v1/invoke/actions/$action" \
		-H 'Content-Type: application/json' -d "{\"args\":$args}"
}

invoke dock-seed-documents '["2"]' > "$LOG_DIR/seed.json" || fail "dock-seed-documents failed"
invoke dock-float-anchorable '["FileStatsTool"]' > "$LOG_DIR/float.json" || fail "dock-float-anchorable failed"
invoke dock-query-layout > "$LOG_DIR/layout.json" || fail "dock-query-layout failed"

python3 - "$LOG_DIR/layout.json" <<'EOF' || fail "layout assertion failed"
import json, sys
resp = json.load(open(sys.argv[1]))
assert resp.get("success"), f"action reported failure: {resp}"
layout = json.loads(resp["returnValue"])
docs = [t for p in layout["documentPanes"] for t in p["tabs"]]
assert "doc1" in docs and "doc2" in docs, f"seeded documents missing: {layout}"
floating = [c for w in layout["floatingWindows"] for c in w["contents"]]
assert "FileStatsTool" in floating, f"floating window missing: {layout}"
print("layout OK:", layout)
EOF

if command -v import > /dev/null; then
	import -window root "$LOG_DIR/screenshot.png" 2>/dev/null || true
fi

echo "SMOKE_RESULT: PASS - AvalonDock floated an anchorable on the LibreWPF portable backend"
