# openrouter-api

FastAPI backend for ImagineVR, adapted from Israel-Jauregui's `online-ready`
commit `e5f8c5a`. It exposes Unity's `/v1/turn`, `/v1/session/reset`, and
`/v1/health`, plus the existing `/`, `/health`, `/docs`, and `/openapi.json`.

Push-to-talk WAV uploads are transcribed through ElevenLabs; complete text and
session world state go to OpenRouter. Validated ImagineScript commands stream
back to Unity as NDJSON, including `ride`, creature `behavior`, and `ignored`
outcomes for background chatter. It also supports `think` terminal plan lines,
solid scenery and the maze kit, moving spawn/ride paths, `goal` triggers, and
`player` scaling. Plan lines are displayed but never stored as scene state or
counted as world changes. The Unity-exported catalog is authoritative;
do not edit it by hand. TTS remains off.

This follows Israel's latest deployment handoff: keep the 1 CPU / 1 GB VM and
Python 3.14, with hosted transcription rather than local Whisper. Do not install
Whisper on production. The optional local adapter requires a separate Python
3.13 environment with faster-whisper and numpy if used for development.

## Activate the world-building backend

The new configuration is not live until provisioned. Add these **Actions
repository secrets**, in addition to the existing `VULTR_DEPLOY_SSH_KEY`:

- `OPENROUTER_API_KEY`: an OpenRouter key with available credit.
- `ELEVENLABS_API_KEY`: an ElevenLabs key with speech-to-text access.
- `IMAGINE_APP_TOKEN`: a random token of at least 32 characters; securely share
  this with Israel for Unity's `ImagineSettings` asset. Never use `dev-token`.
- `VULTR_PROVISION_SSH_KEY`: the privileged operator SSH key, separate from the
  restricted deployment key. The existing operator key is
  `C:\Users\parke\.ssh\hacklanta_vultr`.

Merge backend changes onto current `main` without overwriting Israel's work.
Apply the Terraform HTTPS firewall rule (no VM resize), then manually run
**Provision ImagineVR API host** on `main`. This installs server configuration,
runtime secrets, TLS for `64.177.52.162.sslip.io`, and certificate renewal. Then
run **Test and deploy openrouter-api**. An initial application deployment before
provisioning will fail safely and retain/restore the prior release.

Point Unity at `https://64.177.52.162.sslip.io` with the matching token and catalog.
An application token in a shipped client is a demo access control, not a
substitute for individual-user authentication. Configure provider spending limits.
Sessions are in memory and are lost on restart/deployment. Audio and transcripts
are not intentionally persisted or included in application logs.

From `openrouter-api`, run `python scripts/smoke.py` with `IMAGINE_URL` and
`IMAGINE_APP_TOKEN` set. This is Israel's twelve-scenario acceptance check;
live execution incurs model usage. Set `IMAGINE_REQUIRE_ONLINE=1` for real-model
acceptance so offline fallback cannot pass unnoticed. Test a real audio upload
and the Unity/headset path separately; the smoke script tests text turns.

For local development, copy `.env.example` to `.env` and set provider keys.
Without a key, development retains Israel's labelled keyword fallback.
Production fails startup if model, transcription, or client-token secrets are
missing. OpenRouter errors before any output retain the explicit fallback note;
errors after partial output preserve the world already built and finish failed.

## Local development

Use Python 3.14 to match production and CI:

```sh
cd openrouter-api
python -m venv .venv
# Linux/macOS: source .venv/bin/activate
# PowerShell: .venv\Scripts\Activate.ps1
python -m pip install -r requirements-dev.txt
python -m pytest -q
python -m uvicorn app.main:app --reload
```

Visit `http://127.0.0.1:8000/docs`.

## Deployment

The workflow in `.github/workflows/deploy-openrouter-api.yml` tests backend pull
requests. Pushes/merges to `main` deploy only when files in `openrouter-api/`,
`infra/deploy/`, or the workflow itself change. The workflow also supports manual
deployment of `main` from GitHub's Actions tab.

The existing VM serves the API at `http://64.177.52.162`; `/health` includes the
deployed revision. nginx forwards HTTP traffic to Uvicorn on localhost, and
systemd keeps it running through restarts. This initial endpoint uses HTTP;
configure a domain and HTTPS before sending sensitive user data or credentials.

### Activate GitHub Actions

A repository administrator must add this repository secret:

| Secret | Value |
| --- | --- |
| `VULTR_DEPLOY_SSH_KEY` | Entire contents of the dedicated deployment private key |

On the Windows machine used for setup, the private key is at
`C:\Users\parke\.ssh\imaginevr_github_deploy`. To copy it without printing it:

```powershell
Get-Content -Raw "$env:USERPROFILE\.ssh\imaginevr_github_deploy" | Set-Clipboard
```

Paste into **GitHub repo → Settings → Secrets and variables → Actions → New
repository secret**. Include the `BEGIN` and `END` lines. Never commit that key or
send it in chat. The workflow uses the VM address and the `deploy` user directly;
it does not need the Vultr API key. The public key is in
`infra/deploy/keys/github-actions.pub`.

Commit and push the backend, workflow, deployment scripts, and Terraform changes.
Automatic deployment becomes active once both the workflow is on `main` and the
secret exists. Check the workflow logs for the test, deployment, and health checks.

### VM setup and operations

The one-time VM setup is recorded in `infra/deploy/bootstrap.sh`. It creates a
dedicated `deploy` SSH user and an `openrouter-api` service user, installs nginx and
Python venv support, and installs the service configuration. The deployment SSH key
disables port forwarding and PTYs; the deploy user may restart only this service
via sudo. SSH host identity is pinned in `infra/deploy/known_hosts`.

To bootstrap a replacement Ubuntu VM, upload `infra/deploy/` and run:

```sh
sudo bash infra/deploy/bootstrap.sh infra/deploy/keys/github-actions.pub
```

Update the host address, host key, and Terraform resources when replacing the VM.
Changes to the service or nginx configuration require rerunning bootstrap; normal
deployments update application releases only.

`deploy.sh` creates a separate venv for each release under
`/srv/openrouter-api/releases`, switches the `current` symlink, restarts the service,
and verifies that `/health` reports the requested revision. If the service fails
after switching, it restores the preceding release when one exists. Deployments
are serialized using both GitHub concurrency and a server-side lock. Releases and
archives are retained for inspection/rollback; monitor disk usage and remove old
releases periodically, keeping the current and previous releases.

An operator can inspect the service with:

```sh
sudo systemctl status openrouter-api
sudo journalctl -u openrouter-api -n 100 --no-pager
```

Future application secrets (such as `OPENROUTER_API_KEY`) belong in the root-owned
`/etc/openrouter-api/environment`, never in Git. Restart the service after editing
that file. No OpenRouter credential is needed for the starter health endpoint.
