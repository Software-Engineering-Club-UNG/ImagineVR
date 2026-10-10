#!/usr/bin/env bash
# Root-only, atomic installation; secret contents arrive on stdin, never in arguments.
set -euo pipefail
[[ $(id -u) -eq 0 ]] || { echo 'Run as root after bootstrap.' >&2; exit 1; }
install -d -m 750 -o root -g openrouter-api /etc/openrouter-api
temporary=$(mktemp /etc/openrouter-api/environment.XXXXXX)
trap 'rm -f -- "$temporary"' EXIT
chmod 600 "$temporary"
cat > "$temporary"
# The caller validates provider keys and the client token before uploading anything.
mv -f -- "$temporary" /etc/openrouter-api/environment
