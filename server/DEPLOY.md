# Putting the world-building server on the Vultr VM

For whoever owns `infra/` and `openrouter-api/`. Written 2026-10-10.

## Where things stand

There are two backends in the repo:

| Folder | What it is | Deployed? |
| --- | --- | --- |
| `openrouter-api/` | Starter FastAPI app (`/`, `/health`). No model calls. | Yes, by `deploy-openrouter-api.yml`, to `http://64.177.52.162` |
| `server/` | The real one. The headset posts speech or text to `/v1/turn` and gets a stream of world-building statements back. It already calls OpenRouter. | No |

The headset needs `server/`. Nothing has to be rewritten: the deploy pipeline you built is the
right shape, it just has to ship `server/` in place of the starter. `server/` now answers
`GET /health` with `{"status": "ok", "revision": ...}`, so `infra/deploy/deploy.sh` can
health-check it unchanged.

## What the pipeline needs to change

1. **What gets packed.** The server needs its code and the catalog. In the workflow's upload step:

   ```sh
   tar -czf "$RUNNER_TEMP/$REVISION.tar.gz" \
     -C server imagine requirements.txt \
     -C ../shared catalog.json
   ```

   That gives a release folder containing `imagine/`, `requirements.txt` and `catalog.json`.

2. **What gets started.** In `infra/deploy/openrouter-api.service`:

   ```ini
   ExecStart=/srv/openrouter-api/current/.venv/bin/python -m uvicorn imagine.main:app --host 127.0.0.1 --port 8000 --workers 1 --proxy-headers --forwarded-allow-ips=127.0.0.1
   Environment=IMAGINE_CATALOG=/srv/openrouter-api/current/catalog.json
   ```

   Keep `--workers 1`. Sessions (each headset's world and conversation) live in memory, so a
   second worker would lose them half the time.

3. **When it runs.** Add `server/**` and `shared/catalog.json` to both `paths:` lists in the
   workflow, or server changes will never deploy.

4. **The test job.** It currently runs `pytest` in `openrouter-api/`. `server/` has no pytest
   suite yet; the check that matters is `server/scripts/smoke.py` against the deployed URL
   (see below). Run it as the last deploy step in place of, or after, the `/health` curl.

5. **Python version.** The VM runs Python 3.14. `server/pyproject.toml` caps Python below 3.14,
   but only because of `faster-whisper` (local speech-to-text). `server/requirements.txt` leaves
   that out, and the server imports and starts on 3.14 with it (checked on 3.14.4). So deploy
   with `requirements.txt`, not `uv`, and do not use local speech-to-text on the VM (next section).

## Secrets and settings on the VM

Put these in `/etc/openrouter-api/environment` (root-owned, already read by the service), then
`sudo systemctl restart openrouter-api`. Never in Git.

```sh
OPENROUTER_API_KEY=sk-or-...          # without this the server silently runs the offline keyword director
IMAGINE_MODEL=anthropic/claude-haiku-5.5
IMAGINE_APP_TOKEN=<a long random string>   # not "dev-token"; the headset must send the same value
IMAGINE_STT=elevenlabs                # speech-to-text through the ElevenLabs API
ELEVENLABS_API_KEY=...
IMAGINE_PUBLIC_URL=http://64.177.52.162    # only matters if IMAGINE_TTS=on
```

Why `IMAGINE_STT=elevenlabs`: the default, `local`, runs Whisper on the machine. The VM is
`vc2-1c-1gb` (1 vCPU, 1 GB RAM), which is too small for that, and it needs the package that
does not install on 3.14. If `IMAGINE_STT=elevenlabs` is set without `ELEVENLABS_API_KEY`, the
server falls back to local Whisper and every spoken request fails; typed requests still work.

`GET /health` and `GET /v1/health` both report `"model"`. If it says `"offline"`, the key is
missing or misnamed.

## nginx

`infra/deploy/nginx.conf` works as it is:

- The reply to `/v1/turn` is a stream, one JSON line at a time, and the headset shows each line
  as it lands. The server sends `X-Accel-Buffering: no`, which nginx honours, so **do not** add
  `proxy_ignore_headers X-Accel-Buffering` or turn on gzip for `application/x-ndjson`. If the
  world ever appears all at once at the end of a turn, buffering is the cause.
- `proxy_read_timeout 60s` is enough: a turn is 5 to 20 seconds.
- `client_max_body_size 10m` is enough: a 30 second voice clip is about 1 MB.

## Pointing the headset at it

`client/Assets/ImagineVR/Resources/ImagineSettings.asset` holds `ServerUrl` and `AppToken`.
Today they are `http://127.0.0.1:8080` and `dev-token`. Set them to the VM address and the
token above, then rebuild the APK. Plain HTTP is allowed in the Unity player settings, so the
bare IP works, but the app token and the user's voice then cross the network unencrypted. For
anything beyond the demo, put a domain and HTTPS in front (the firewall in
`infra/vultr/main.tf` only opens 22 and 80, so 443 has to be added too).

The headset gives up on a turn if no line arrives for 8 seconds (`LineTimeout`) or the whole
turn passes 45 (`TotalTimeout`). The server sends a "thinking" line straight away, so the 8
seconds is the wait for the model's first statement.

## Checking it works

From any machine:

```sh
cd server
IMAGINE_URL=http://64.177.52.162 IMAGINE_APP_TOKEN=<token> uv run scripts/smoke.py
```

It sends twelve things people really say (small talk, a place, "I want a gun and want to be
able to shoot zombies", driving a car, an unsafe request) and checks what comes back. It
prints the model name first; if that says `offline` you are testing keywords, not the model.

One request by hand:

```sh
IMAGINE_URL=http://64.177.52.162 IMAGINE_APP_TOKEN=<token> uv run scripts/try.py --new "I want to see a golf course"
```

## What has and has not been tested

- The model path was run end to end against a stand-in for OpenRouter: request format,
  streaming in awkward chunks, stripping prose and code fences, validation of the new `ride`
  and `behavior` statements, the `ignored` outcome, fallback to the keyword director on a
  rejected key, and a failure partway through a reply. All behaved.
- `anthropic/claude-haiku-5.5` is a valid model id on OpenRouter (checked against their public
  model list).
- **No real model call has been made.** There was no OpenRouter key on the development
  machine. The first run of `smoke.py` with a key is the first time `prompt.md` meets a model.
  Expect to adjust `prompt.md` after reading its failures; the server reloads that file without
  a restart.
- ElevenLabs speech-to-text has not been tested.

## If something is wrong

| Symptom | Likely cause |
| --- | --- |
| `/health` says `"model": "offline"` | `OPENROUTER_API_KEY` missing from `/etc/openrouter-api/environment`, or the service was not restarted |
| Terminal shows "the AI model is unreachable, using the built-in director" | OpenRouter rejected the call: bad key, no credit, or a wrong `IMAGINE_MODEL`. `journalctl -u openrouter-api` has the status and message |
| Service will not start: "Catalog not found" | `catalog.json` not in the archive, or `IMAGINE_CATALOG` not set |
| 401 "bad app token" | `AppToken` in the Unity asset differs from `IMAGINE_APP_TOKEN` |
| The world appears all at once | Something is buffering the stream (see nginx) |
| Typed requests work, spoken ones fail | Speech-to-text: see `IMAGINE_STT` above |
| Headset log warns the catalog differs | The APK was built from a different `shared/catalog.json` than the server has. Re-export from Unity, commit, rebuild and redeploy together |
