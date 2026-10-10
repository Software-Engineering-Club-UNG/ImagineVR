#!/usr/bin/env bash
# Provision a system-accessible Python and public Whisper weights; no provider key.
set -euo pipefail
[[ $(id -u) -eq 0 ]] || { echo 'Run as root after creating the service account.' >&2; exit 1; }

python3 -m venv /opt/openrouter-uv
/opt/openrouter-uv/bin/pip install --disable-pip-version-check uv==0.13.0
export UV_PYTHON_INSTALL_DIR=/opt/openrouter-python-managed
/opt/openrouter-uv/bin/uv python install 3.13.16
python_path=$(/opt/openrouter-uv/bin/uv python find 3.13.16)
install -d -m 755 /opt/openrouter-python/bin
ln -sfn "$python_path" /opt/openrouter-python/bin/python3.13
chmod -R a+rX /opt/openrouter-python-managed

/opt/openrouter-python/bin/python3.13 -m venv /opt/openrouter-whisper
/opt/openrouter-whisper/bin/pip install --disable-pip-version-check faster-whisper==1.2.1 numpy==2.5.3
install -d -m 750 -o openrouter-api -g openrouter-api /var/cache/openrouter-api
sudo -u openrouter-api env HF_HOME=/var/cache/openrouter-api/huggingface HF_HUB_DISABLE_TELEMETRY=1 \
  /opt/openrouter-whisper/bin/python - <<'PY'
from pathlib import Path
from faster_whisper import WhisperModel
from faster_whisper.utils import download_model

# base.en is what runs. small.en is fetched too, so that trying it is one line in the service
# unit (IMAGINE_STT_MODEL=small.en) and a restart, with no download at that moment.
for name in ('base.en', 'small.en'):
    path = Path('/var/cache/openrouter-api') / f'whisper-{name}'
    if not (path / 'model.bin').is_file():
        download_model(name, output_dir=str(path))
    model = WhisperModel(str(path), device='cpu', compute_type='int8', cpu_threads=4,
                         num_workers=1, local_files_only=True)
    del model
    print(f'Local Whisper {name} validated; normal startup requires no network or speech key.')
PY
