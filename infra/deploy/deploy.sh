#!/usr/bin/env bash
# Executed as deploy by GitHub Actions; sudo permits restarting only this service.
set -euo pipefail
umask 022

revision=${1:?Usage: deploy.sh REVISION}
[[ "$revision" =~ ^([a-f0-9]{40}|[a-f0-9]{64})$ ]] || { echo 'Invalid revision' >&2; exit 1; }
base=/srv/openrouter-api
archive="$base/incoming/$revision.tar.gz"
[[ -f "$archive" ]] || { echo 'Deployment archive is missing' >&2; exit 1; }

exec 9>"$base/deploy.lock"
flock -w 300 9
release=$(mktemp -d "$base/releases/$revision.XXXXXX")
chmod 755 "$release"
tar -xzf "$archive" -C "$release"
python3 -m venv "$release/.venv"
"$release/.venv/bin/python" -m pip install --disable-pip-version-check -r "$release/requirements.txt"
printf 'DEPLOYMENT_REVISION=%s\n' "$revision" > "$release/release.env"

previous=$(readlink "$base/current" || true)
switched=false
rollback() {
  result=$?
  if [[ "$result" -ne 0 && "$switched" == true && -n "$previous" ]]; then
    echo 'Deployment failed; restoring the previous release.' >&2
    ln -s "$previous" "$base/.rollback-$$"
    mv -Tf "$base/.rollback-$$" "$base/current"
    sudo -n /usr/bin/systemctl restart openrouter-api.service || true
  fi
  exit "$result"
}
trap rollback EXIT

ln -s "$release" "$base/.current-$$"
mv -Tf "$base/.current-$$" "$base/current"
switched=true
sudo -n /usr/bin/systemctl restart openrouter-api.service

for attempt in {1..150}; do
  if curl --fail --silent --max-time 3 http://127.0.0.1:8000/health |
    "$release/.venv/bin/python" -c 'import json,sys; data=json.load(sys.stdin); sys.exit(0 if data.get("status") == "ok" and data.get("revision") == sys.argv[1] else 1)' "$revision" 2>/dev/null; then
    echo "Deployment healthy: $revision"
    exit 0
  fi
  sleep 2
done
echo 'Health check failed.' >&2
exit 1
