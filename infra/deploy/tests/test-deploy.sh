#!/usr/bin/env bash
# Exercise release switching and rollback without touching the running service.
set -euo pipefail
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
test_dir=$(mktemp -d)
trap 'rm -rf -- "$test_dir"' EXIT
export TEST_BASE="$test_dir/server"
mkdir -p "$TEST_BASE/releases/previous" "$TEST_BASE/incoming" "$test_dir/bin" "$test_dir/source"
ln -s "$TEST_BASE/releases/previous" "$TEST_BASE/current"
printf '# Test fixture: dependencies are mocked.\n' > "$test_dir/source/requirements.txt"
sed 's|^base=/srv/openrouter-api$|base=${TEST_BASE:?}|' "$script_dir/deploy.sh" > "$test_dir/deploy.sh"

cat > "$test_dir/bin/python3" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == -m && "$2" == venv ]]
mkdir -p "$3/bin"
cat > "$3/bin/python" <<'PYTHON'
#!/usr/bin/env bash
if [[ "$1" == -m && "$2" == pip ]]; then
  exit 0
fi
exec /usr/bin/python3 "$@"
PYTHON
chmod +x "$3/bin/python"
MOCK

cat > "$test_dir/bin/sudo" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
[[ "$*" == '-n /usr/bin/systemctl restart openrouter-api.service' ]]
printf 'restart\n' >> "$TEST_BASE/restarts"
MOCK

cat > "$test_dir/bin/curl" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
[[ "${FAIL_HEALTH:-0}" == 0 ]] || exit 22
revision=$(cut -d= -f2 "$TEST_BASE/current/release.env")
printf '{"status":"ok","revision":"%s"}\n' "$revision"
MOCK

printf '#!/usr/bin/env bash\nexit 0\n' > "$test_dir/bin/sleep"
chmod +x "$test_dir/bin/"*
export PATH="$test_dir/bin:$PATH"
export PYTHON_BIN="$test_dir/bin/python3"
good_revision=$(printf 'a%.0s' {1..40})
bad_revision=$(printf 'b%.0s' {1..40})
tar -czf "$TEST_BASE/incoming/$good_revision.tar.gz" -C "$test_dir/source" requirements.txt
cp "$TEST_BASE/incoming/$good_revision.tar.gz" "$TEST_BASE/incoming/$bad_revision.tar.gz"

bash "$test_dir/deploy.sh" "$good_revision"
good_release=$(readlink "$TEST_BASE/current")
[[ "$good_release" == "$TEST_BASE/releases/$good_revision."* ]]
[[ $(wc -l < "$TEST_BASE/restarts") == 1 ]]

if FAIL_HEALTH=1 bash "$test_dir/deploy.sh" "$bad_revision"; then
  echo 'Unhealthy release unexpectedly succeeded' >&2
  exit 1
fi
[[ $(readlink "$TEST_BASE/current") == "$good_release" ]]
[[ $(wc -l < "$TEST_BASE/restarts") == 3 ]]

if bash "$test_dir/deploy.sh" 'invalid-revision'; then
  echo 'Invalid revision unexpectedly succeeded' >&2
  exit 1
fi
[[ $(readlink "$TEST_BASE/current") == "$good_release" ]]
echo 'Deployment tests passed: success, rollback, and invalid revision.'
