# Quickstart: Voice World Builder (Core Loop)

How to get the project running and prove the feature works. Paths and commands describe the
layout in [plan.md](plan.md); they exist once the tasks are implemented.

## Prerequisites

| Need | Windows PC | MacBook Air M1 |
|------|-----------|----------------|
| Unity Hub + Unity **6000.3.26f1** with Android Build Support (SDK/NDK, OpenJDK) | yes | yes (Apple-silicon editor) |
| Git and Git LFS | yes | yes |
| `uv` (for the server and scripts) | yes | yes |
| Meta Quest 3S in developer mode, USB-C cable | yes | yes |

Accounts and keys (server only, never in the repo): OpenRouter key; ElevenLabs key if spoken
replies or the fallback transcription are used; a Vultr instance with at least 4 vCPUs and
8 GB of memory. Transcription runs on the server itself and needs no key; the first start
downloads the Whisper model (a few hundred megabytes at most).

## 1. Clone

```text
git lfs install
git clone <repo-url>
```

## 2. Run the server locally

```text
cd server
cp .env.example .env      # fill in OPENROUTER_API_KEY and IMAGINE_APP_TOKEN
uv run uvicorn imagine.main:app --port 8080
```

Check: `GET http://localhost:8080/v1/health` returns `"ok": true` and a catalog version.

## 3. Prove the server without a headset

```text
uv run scripts/try.py "I want to see a golf course"
```

Expected: ImagineScript lines print one at a time, starting with `status`, including at
least `sky`, `terrain`, and one `kit` or `scatter`, and ending with
`{"op":"done","outcome":"world_changed"}`. See
[contracts/scene-language.md](contracts/scene-language.md).

Then, in the same session: `uv run scripts/try.py "make it sunset"`.
Expected: a `sky` statement and `done`, with no `clear` and no `terrain`.

## 4. Prove the client without a server

Open `client/` in Unity 6000.3.26f1, open the `Main` scene, press Play.

- Expected at start: black everywhere, one glowing terminal with a welcome line.
- Menu **ImagineVR → Replay Script…**, choose `shared/examples/golf-course.ndjson`.
- Expected: tool-call lines appear in the terminal one by one while the sky opens, the ground
  rises, and the course appears, each as an animation.

## 5. Whole loop in the editor

Set the server URL and app token in the ImagineVR settings asset, press Play, and type a
request into the debug prompt field (or hold Space and speak).

Expected: prompt line, `thinking`, `building`, world changes, back to `ready`.

## 6. On the headset

Connect the Quest by USB-C, accept the debugging prompt in the headset, then
**File → Build And Run** (Android).

Run these in order. Each maps to the spec's success criteria.

| # | Do | Expect |
|---|----|--------|
| 1 | Launch | Dark room, glowing terminal with instructions. |
| 2 | Hold A, say "I want to see a golf course", release | "Listening" while held; your words appear within about a second of release; the world starts changing within 5 s and is done within 20 s. |
| 3 | Look all the way around | A complete golf course; no black gaps; motion stays smooth. |
| 4 | "Make it sunset" | Only the sky and light change. |
| 5 | "Add more trees" | More trees; everything else stays. |
| 6 | "Make me a golf club" | A club appears within reach; grip to pick it up, swing, release, pick up again. |
| 7 | "Put me in a city" | The course transitions out and a city transitions in. |
| 8 | Ask for something not in the library, e.g. "a giraffe" | Something appears anyway (nearest match or a labelled shape) and the terminal says what was substituted. |
| 9 | Press and release A without speaking | Terminal returns to ready; world unchanged. |
| 10 | Turn headset Wi-Fi off, make a request, turn it back on, try again | Plain-language problem message; world intact; the retry works. |
| 11 | Speak a second request while the first is still building | Both complete, in order; world is not left half-built. |

Demo rehearsal: steps 2, 7, and 6 five times in a row each (SC-006), and the full run of
1 → 2 → 4 → 6 inside three minutes (SC-008).

## 7. Deploy the server to Vultr

```text
ssh root@<instance-ip>
# first time only
bash deploy/setup.sh            # installs uv, Caddy, the systemd service
# every update
git pull && systemctl restart imagine
```

Set the keys in `/etc/imagine.env`. Point the app's server URL at the instance's HTTPS
address, and re-run step 3 against it (`IMAGINE_URL=https://… uv run scripts/try.py …`).

Check from the headset's own browser on the venue Wi-Fi that `/v1/health` loads before
relying on it for the demo.

## After changing the library

Run **ImagineVR → Export Catalog** in Unity, commit `shared/catalog.json`, and restart the
server so the AI learns the new vocabulary.
