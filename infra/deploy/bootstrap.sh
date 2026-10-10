#!/usr/bin/env bash
# Run once as an operator with sudo. Configuration remains reviewable in Git.
set -euo pipefail

if [[ $(id -u) -ne 0 || $# -ne 1 ]]; then
  echo "Usage: sudo bash bootstrap.sh /path/to/deploy-key.pub" >&2
  exit 1
fi

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
public_key=$(cat -- "$1")
[[ "$public_key" == ssh-ed25519\ * ]] || { echo 'Expected an Ed25519 public key' >&2; exit 1; }

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y python3-venv nginx curl

id deploy &>/dev/null || useradd --create-home --shell /bin/bash deploy
id openrouter-api &>/dev/null || useradd --system --home /nonexistent --shell /usr/sbin/nologin openrouter-api
install -d -m 755 -o deploy -g deploy /srv/openrouter-api /srv/openrouter-api/releases /srv/openrouter-api/incoming
install -d -m 700 -o deploy -g deploy /home/deploy/.ssh
touch /home/deploy/.ssh/authorized_keys
if ! grep -Fq -- "$public_key" /home/deploy/.ssh/authorized_keys; then
  printf 'restrict %s\n' "$public_key" >> /home/deploy/.ssh/authorized_keys
fi
chown deploy:deploy /home/deploy/.ssh/authorized_keys
chmod 600 /home/deploy/.ssh/authorized_keys

install -d -m 750 -o root -g openrouter-api /etc/openrouter-api
if [[ ! -e /etc/openrouter-api/environment ]]; then
  install -m 600 -o root -g root /dev/null /etc/openrouter-api/environment
fi
install -m 644 "$script_dir/openrouter-api.service" /etc/systemd/system/openrouter-api.service
printf 'deploy ALL=(root) NOPASSWD: /usr/bin/systemctl restart openrouter-api.service\n' > /etc/sudoers.d/openrouter-api-deploy
chmod 440 /etc/sudoers.d/openrouter-api-deploy
visudo -cf /etc/sudoers.d/openrouter-api-deploy

install -m 644 "$script_dir/nginx.conf" /etc/nginx/sites-available/default
nginx -t
systemctl daemon-reload
systemctl enable openrouter-api.service
systemctl enable --now nginx
systemctl reload nginx
if command -v ufw >/dev/null && ufw status | grep -q '^Status: active'; then
  ufw allow 80/tcp comment 'openrouter-api HTTP via nginx'
fi
echo 'Bootstrap complete. Deploy a release to start FastAPI.'
