# Status and handoff

Written by Claude on the Windows PC, night of 2026-10-09 into 10-10. Branch: `initial-work`.
Everything is committed locally. **Nothing has been pushed.**

## Picking this up on another machine

A new Claude Code session will not remember the conversation that produced this. Open Claude
Code in this folder and say:

> Read STATUS.md and specs/001-voice-world-builder/, then continue from "What is next".

That, plus the git history, is everything it needs.

The MacBook needs, before any of this runs:

- Unity Hub, signed in, and **Unity 6000.3.26f1** with **Android Build Support** (SDK/NDK and
  OpenJDK). Several gigabytes: needs a real internet connection.
- `git` and `git lfs`, and `uv` for the server.
- First server start downloads Python packages and the Whisper model (also needs internet).

`client/Library/`, `client/Temp/`, `client/Logs/`, `client/Snapshots/` and `server/.venv/` are
caches. They do not need to be copied; Unity and `uv` rebuild them.

## Try it in two minutes (no headset, no server, no keys)

1. Open `client/` in Unity 6000.3.26f1, open `Assets/ImagineVR/Scenes/Main`, press Play.
2. You start in the dark room with the terminal. Press **F1** to **F8** for built-in demos,
   or **B** to cycle through all 28.
3. Hold the **right mouse button** to look around. **F12** returns to the dark room.

Other desktop keys: **Enter** to type a request (needs the server), **Space** to speak, **E**
grab, **left click** use the held thing, **R** recentre the terminal.

## Run the server

```
cd server
uv run uvicorn imagine.main:app --port 8080
```

With no `OPENROUTER_API_KEY` it uses an offline keyword director, so "I want to see a golf
course" or "make it sunset" already work end to end. Copy `.env.example` to `.env` and add the
key to use the real model (default `anthropic/claude-haiku-5.5`).

## What exists

- **Server** (`server/`): turn endpoint with streamed replies, validation and catalog
  substitution, session memory, OpenRouter streaming, offline fallback, Whisper transcription
  on the machine, optional ElevenLabs speech.
- **Unity client** (`client/`), all built from code at startup:
  - procedural sky (26 presets), terrain (14 types, 18 ground materials), water
  - GPU-instanced objects, 251 library assets modelled in code, 30 kits
  - physics props, grabbing and throwing, tools (paintbrush, blaster, wand, bubbles)
  - weather particles (10 kinds), synthesised sound effects and 16 ambience loops
  - the terminal (streaming lines, tool calls, status line, tool strip), push-to-talk
  - desktop mode for working without a headset
- **Editor tools** (menu `ImagineVR`): Setup Project, Export Catalog, Replay Script, Build
  Quest APK. `scripts/unity.ps1` runs them from the command line (Windows only for now).
- **Language**: ImagineScript v1. Beyond the plan it gained `weather`, `physics` (gravity),
  `physics: true` on spawn/scatter, vector `scale` and `rot`, and dotted ids for sculpting.

## Verified, and not verified

| Thing | State |
|---|---|
| C# compiles in Unity 6000.3.26f1 | Verified |
| Golf course and night city render correctly (looked at rendered pictures) | Verified |
| All 251 assets build; animals, people and sea assets looked at | Verified |
| Server text turns, auth, offline director, Whisper on a spoken clip | Verified |
| The other 28 kits | Compile, **not yet looked at** |
| Real AI model through OpenRouter | **Not tested** (no key) |
| Anything on the headset: tracking, controllers, grabbing, frame rate | **Not tested** |
| Grabbing, throwing, tools, score zones | **Not tested**, even on the desktop |
| Android build | **Not attempted** |
| Microphone capture in the app | **Not tested** |
| Every sound (all synthesised; nobody has heard them) | **Not tested** |
| ElevenLabs speech | **Not tested** (no key) |

## What is next

1. Play each built-in demo in the editor and fix kits that look wrong.
2. Try physics on the desktop: grab (E), throw, the paintbrush, the bowling lane.
3. Run **ImagineVR > Build Quest APK** and get it onto the headset (tasks T010, T063).
4. Put an OpenRouter key in `server/.env`, run `scripts/try.py` with real requests, and tune
   `server/imagine/prompt.md`.
5. Not written yet: `server/deploy/` (Vultr setup script, Caddyfile, systemd unit, task T061);
   a macOS/Linux version of `scripts/unity.ps1`.
6. Docs that lag the code: `specs/001-voice-world-builder/contracts/scene-language.md` and
   `catalog.md` do not yet describe the added operations and fields; `tasks.md` checkboxes
   are not ticked; `README.md` is still the two-line original.
7. The constitution still carries its scratch "Sync Impact Report" comment at the top.

## Things that differ from the plan, on purpose

- No XR Interaction Toolkit: hands and grabbing are a small custom system on the Input System.
- Library entries are code, not ScriptableObjects. A prefab at `Resources/Library/<id>` is
  not wired up yet as an override.
- Sounds are synthesised. A clip at `Resources/Sfx/<name>` or `Resources/Ambience/<id>`
  replaces the synthesised one.
