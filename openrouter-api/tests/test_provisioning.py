from pathlib import Path
import os
import shutil
import subprocess

import pytest

import yaml

ROOT = Path(__file__).resolve().parents[2]


def workflow(name):
    return yaml.load((ROOT / '.github' / 'workflows' / name).read_text(), Loader=yaml.BaseLoader)


def test_first_boot_creates_accounts_before_installing_secrets():
    config = workflow('provision-openrouter-api.yml')
    step = next(step for step in config['jobs']['provision']['steps'] if step.get('name') == 'Configure host and runtime secrets')
    script = step['run']
    assert script.index('bootstrap.sh') < script.index('install-environment.sh')
    assert 'sudo tee' not in script
    assert 'VULTR_PROVISION_SSH_KEY' in str(config)


def test_http_downgrade_requires_explicit_opt_in():
    config = workflow('provision-openrouter-api.yml')
    option = config['on']['workflow_dispatch']['inputs']['allow_http_demo']
    assert option['type'] == 'boolean' and option['default'] == 'false'
    bootstrap = (ROOT / 'infra/deploy/bootstrap.sh').read_text()
    assert '${ALLOW_HTTP_DEMO:-0} == 1' in bootstrap
    assert 'public_url=http://64.177.52.162' in bootstrap
    assert 'exit 1' in bootstrap[bootstrap.index("echo 'TLS issuance failed."):]


def test_public_verification_uses_selected_url_and_never_deploys_review_branches():
    config = workflow('deploy-openrouter-api.yml')
    deploy = config['jobs']['deploy']
    assert "github.ref == 'refs/heads/main'" in deploy['if']
    assert 'vars.IMAGINE_API_URL' in deploy['env']['API_URL']
    script = next(step['run'] for step in deploy['steps'] if step.get('name') == 'Verify public API')
    assert '"$API_URL/health"' in script


def test_runtime_secret_install_is_atomic_and_root_only():
    script = (ROOT / 'infra/deploy/install-environment.sh').read_text()
    assert '$(id -u) -eq 0' in script
    assert 'mktemp /etc/openrouter-api/environment.' in script
    assert script.index('chmod 600') < script.index('cat > "$temporary"') < script.index('mv -f')
    assert 'tee' not in script


def test_local_whisper_provisioning_needs_no_speech_key():
    config = workflow('provision-openrouter-api.yml')
    assert 'ELEVENLABS_API_KEY' not in str(config)
    assert 'IMAGINE_STT=local' in str(config)
    bootstrap = (ROOT / 'infra/deploy/bootstrap.sh').read_text()
    assert bootstrap.index('useradd --system') < bootstrap.index('prepare-whisper.sh')
    unit = (ROOT / 'infra/deploy/openrouter-api.service').read_text()
    assert 'Environment=IMAGINE_STT=local' in unit
    assert 'Environment=HF_HUB_OFFLINE=1' in unit
    assert 'ReadWritePaths=/var/cache/openrouter-api' in unit


@pytest.mark.parametrize('name', ['provision-openrouter-api.yml', 'deploy-openrouter-api.yml'])
def test_workflow_shell_steps_parse(name):
    bash = str(Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Git/bin/bash.exe') if os.name == 'nt' else shutil.which('bash')
    if not bash or not Path(bash).exists():
        pytest.skip('bash unavailable')
    for job in workflow(name)['jobs'].values():
        for step in job['steps']:
            if 'run' in step:
                result = subprocess.run([bash, '-n'], input=step['run'], text=True, capture_output=True)
                assert result.returncode == 0, result.stderr
