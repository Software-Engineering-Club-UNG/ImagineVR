# Before we can test on the headset

Parker: this is the short list from Israel's side, after reviewing this branch at `7c60ab1`
on 2026-10-10. Your branch is in good shape: it matches `online-ready`, your 42 tests pass,
and the model path works through your code against a stand-in for OpenRouter. These are the
things between here and a headset talking to the VM.

## To get to a first headset test (in order)

1. **Merge `online-ready` once more.** It has one new commit, `9e524b7`: the headset now always
   sends 16 kHz mono audio. Your server only accepts exactly that, and the headset used to
   encode at whatever rate the microphone returned, so a 48 kHz mic would have had every
   spoken request refused.

2. **Add the repository secrets** (Settings → Secrets and variables → Actions). Only
   `VULTR_DEPLOY_SSH_KEY` exists today. Still needed by `provision-openrouter-api.yml`:

   | Secret | Who has it |
   | --- | --- |
   | `OPENROUTER_API_KEY` | Israel |
   | `ELEVENLABS_API_KEY` | Israel |
   | `IMAGINE_APP_TOKEN` | make one: `python -c "import secrets; print(secrets.token_urlsafe(32))"` |
   | `VULTR_PROVISION_SSH_KEY` | Parker (`hacklanta_vultr`) |

   From a terminal: `gh secret set OPENROUTER_API_KEY` and paste when asked. Nothing is echoed.

3. **Send Israel the `IMAGINE_APP_TOKEN` value privately** (not in Git, not in the PR). The
   headset has to send the same value.

4. **Get this branch onto `main`**, apply the Terraform HTTPS firewall rule, run **Provision
   ImagineVR API host**, then **Test and deploy openrouter-api**.

5. **Run the smoke test against the live server and paste the output in the PR:**

   ```sh
   cd openrouter-api
   IMAGINE_URL=https://64.177.52.162.sslip.io IMAGINE_APP_TOKEN=<token> IMAGINE_REQUIRE_ONLINE=1 python scripts/smoke.py
   ```

   This is the first time `prompt.md` meets a real model. Some of the twelve may fail on the
   first run; that is a prompt fix on Israel's side, not a deployment problem.

6. **Israel** then sets `ServerUrl` to `https://64.177.52.162.sslip.io` and `AppToken` to the
   token in `client/Assets/ImagineVR/Resources/ImagineSettings.asset`, **locally, without
   committing it**, builds the APK and tests on the Quest.

## Do not commit the real token

This repository is public. The token in `ImagineSettings.asset` must stay `dev-token` in Git.
Anyone with the real one can call the server and spend the OpenRouter and ElevenLabs credit;
the per-session rate limit does not stop that, because the client chooses its own session id.
Please also set a hard monthly spend limit on both provider accounts before the demo.

## Worth fixing, none of it blocks the first test

1. **`llm.py` only accepts `finish_reason == "stop"`.** Any other ending fails the turn after
   the world is already built: the user sees "I lost my connection", and the turn is dropped
   from conversation memory. Suggest: after `[DONE]`, treat a missing or unrecognised reason as
   success, and keep failing on `length`, `error` and `content_filter`. Watch for this on the
   first real call.

2. **Provisioning a brand new VM fails on its first run.** The workflow writes
   `/etc/openrouter-api/environment` with group `openrouter-api` before `bootstrap.sh` has
   created that group. It works today only because this VM was bootstrapped earlier. Running
   `bootstrap.sh` first, then writing the secrets, fixes it.

3. **The certificate depends on `sslip.io`.** Let's Encrypt limits certificates per registered
   domain and that domain is shared with everyone who uses it, so issuance can be refused
   through no fault of ours, and `bootstrap.sh` then stops (`set -e`). Fallback: the headset is
   allowed to use plain `http://64.177.52.162`, which is fine for a demo on a trusted network.

4. **Transcription runs one at a time for the whole server** (`_stt_slots = Semaphore(1)`).
   Fine for one headset. With ElevenLabs doing the work remotely it could be 4.

5. **OpenRouter errors log only the status code.** "No credit", "bad key" and "unknown model"
   are hard to tell apart in `journalctl`. Logging the first 200 characters of the body on a
   non-200 (it contains no secrets) would help on demo day.

6. **There are two copies of the server**: `server/imagine/` and `openrouter-api/imagine/`.
   They are identical today and will drift the next time either of us edits one. Proposal:
   yours is the one that deploys and has the tests, so keep `openrouter-api/imagine/` and
   delete `server/imagine/`; Israel then edits `openrouter-api/imagine/prompt.md` and
   `script.py` directly. Say yes and Israel's side will make that change.

## What Israel's side has and has not checked

- Checked: Unity compiles; shooting, creatures, driving, walking, solid walls, the maze, routes,
  rides on rails, goals and player size all pass an automated play-mode run on the desktop.
- Not checked: anything on the Quest itself, a real microphone through the new 16 kHz
  conversion, and any real model call (there is no OpenRouter key on Israel's machine).
