#!/bin/bash
# Run on tang-server (ssh -t tang-server bash apps/sparc-meetingcells-agent/set_agent_key.sh): stores the provider key and restarts the unit.
set -euo pipefail
DIR="$HOME/.config/sparc-meetingcells-agent"
read -rsp 'Provider API key (input hidden): ' KEY; echo
[ -n "$KEY" ] || { echo 'No key entered; nothing changed.'; exit 1; }
mkdir -p "$DIR"; chmod 700 "$DIR"
(umask 077; printf 'SPARC_AGENT_API_KEY=%s\n' "$KEY" > "$DIR/key.env")
systemctl --user restart sparc-meetingcells-agent; sleep 1
curl -s http://127.0.0.1:28093/healthz; echo
