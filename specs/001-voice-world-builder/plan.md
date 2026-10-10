# Implementation Plan: Voice World Builder (Core Loop)

**Branch**: `001-voice-world-builder` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-voice-world-builder/spec.md`

## Summary

The user starts in a dark room facing a glowing agentic-CLI terminal, holds a button, speaks,
and the world is built around them in real time.

Technical approach: a Unity app on Quest 3S contains everything that can be drawn (sky
shader, terrain generators, themed kits, an asset library, transitions, ambience) and an
interpreter for **ImagineScript**, a small line-by-line JSON scene language. A server on
Vultr receives the user's audio, transcribes it on the instance with Whisper, asks an LLM
through OpenRouter to write
ImagineScript using a catalog exported from the Unity library, validates each line, and
streams it back. The headset applies each line the moment it arrives and shows it in the
terminal as a tool call. The AI creates nothing at runtime; it composes what was baked.

## Technical Context

**Language/Version**: C# on Unity 6000.3.26f1 (client); Python 3.12 (server)

**Primary Dependencies**: Client — URP, OpenXR Plugin with Meta Quest feature set, XR
Interaction Toolkit, Input System, TextMeshPro. Server — FastAPI, uvicorn, httpx,
faster-whisper (transcription runs on the instance), managed with `uv`; Caddy for TLS.
External services — OpenRouter (LLM), ElevenLabs (optional: spoken replies, fallback
transcription).

**Storage**: None. In-memory session state on the server; `shared/catalog.json` is a
generated, committed file.

**Testing**: No automated suite (constitution). Verification is [quickstart.md](quickstart.md)
on the headset, a text-to-stream script for the server, and an editor replay tool for the
client.

**Target Platform**: Meta Quest 3S (Android, ARM64, Vulkan, IL2CPP); Ubuntu LTS on Vultr
(at least 4 vCPUs and 8 GB memory, no GPU).
Development on Windows and Apple-silicon macOS.

**Project Type**: VR client application + streaming web service

**Performance Goals**: 72 fps sustained; acknowledgement within 1 s of release; first world
change within 5 s; typical place complete within 20 s.

**Constraints**: About 150k visible triangles and 100 draw calls; no realtime shadows; no
runtime code compilation; headset talks only to our server; provider keys only on the server;
must clone and build on Windows and macOS with no manual fix-up; no dependence on Quest Link.

**Scale/Scope**: One user, one headset, one server instance. Library target for the demo:
about 6 kits, 10 sky presets, 6 terrain types, 60–100 assets (10+ holdable), 8 ambience loops.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|-----------|------|--------|
| I. Demo-First, Always Shippable | Work is ordered so the core loop runs end to end before anything else; Stories 2–4 layer on without rework | Pass |
| II. Speak → See | Local "listening" feedback is instant; `transcript` and `status` lines precede AI output; line-by-line streaming means the first change does not wait for the full reply | Pass |
| III. Bake First, Inject Live | All content is baked; runtime AI output is ImagineScript only; one catalog generated from the library feeds both app and server; unknown IDs resolve to nearest match, then a primitive | Pass |
| IV. Thin Headset, Smart Server | One HTTPS API; keys in server environment only; prompt, model, and validation on the server; language and catalog versioned in the repo | Pass |
| V. Clone-and-Run on Windows and macOS | Unity version pinned; all packages from Package Manager; Git LFS; editor simulator and replay tool in place of Quest Link | Pass |
| VI. Polish Over Breadth | Budgets enforced through catalog limits; every statement applies as a transition; terminal renders statements as tool calls; sponsor nod is a visual detail only | Pass |

**Post-design re-check**: Pass. No violations, so Complexity Tracking is empty.

One point to watch, not a violation: the app carries a shared app token. It is not a provider
key and can be rotated on the server, so it is consistent with Principle IV.

## Project Structure

### Documentation (this feature)

```text
specs/001-voice-world-builder/
├── plan.md              # This file
├── research.md          # Decisions and alternatives
├── data-model.md        # Session, Turn, World, catalog entries
├── quickstart.md        # Setup and validation scenarios
├── contracts/
│   ├── scene-language.md   # ImagineScript v1
│   ├── catalog.md          # shared/catalog.json
│   └── server-api.md       # HTTP API
└── tasks.md             # Created by /speckit-tasks
```

### Source Code (repository root)

```text
client/                          # Unity project (6000.3.26f1)
├── Assets/
│   ├── ImagineVR/
│   │   ├── Scenes/Main.unity
│   │   ├── Scripts/
│   │   │   ├── Terminal/        # world-space CLI: lines, streaming text, state indicator
│   │   │   ├── Voice/           # push-to-talk, microphone capture, WAV encode
│   │   │   ├── Net/             # turn client: upload, streaming NDJSON reader, timeouts
│   │   │   ├── Script/          # ImagineScript parser, statement types, build queue
│   │   │   ├── World/           # sky, light, fog, terrain, objects, held items, ambience
│   │   │   ├── Kits/            # themed generators (city, golf_course, forest, ...)
│   │   │   ├── Library/         # catalog entries, lookup, nearest-match fallback
│   │   │   └── Transitions/     # grow, dissolve, rise, cross-fade
│   │   ├── Editor/              # Export Catalog, Replay Script window
│   │   ├── Shaders/             # procedural sky, palette lit, dissolve
│   │   ├── Library/             # prefabs, palette texture, presets, audio loops
│   │   └── Settings/            # server URL and app token asset, URP assets
│   └── ThirdParty/              # CC0 packs, each with its licence file
├── Packages/manifest.json
└── ProjectSettings/

server/
├── pyproject.toml
├── .env.example
├── imagine/
│   ├── main.py                  # FastAPI app and routes
│   ├── turn.py                  # one turn: transcribe → prompt → stream → validate
│   ├── llm.py                   # OpenRouter streaming client
│   ├── stt.py                   # transcribe(): local Whisper by default, ElevenLabs fallback
│   ├── tts.py                   # optional spoken replies
│   ├── script.py                # statement validation, clamping, substitution
│   ├── catalog.py               # load catalog, nearest match, render for the prompt
│   ├── session.py               # in-memory sessions and world description
│   └── prompt.md                # the AI's instructions and language reference
├── scripts/try.py               # send text, print the stream
└── deploy/                      # setup.sh, Caddyfile, imagine.service

shared/
├── catalog.json                 # generated by Unity, read by the server
└── examples/                    # hand-written ImagineScript: golf-course, city, dark-to-dawn

.gitattributes                   # Git LFS patterns
.gitignore                       # Unity Library/, Temp/, builds; server .env
```

**Structure Decision**: Three top-level folders. `client/` is the Unity project, `server/` is
the Python service, and `shared/` holds the two things both sides must agree on: the catalog
and example scripts. The contract documents in this feature folder define the format of both.

## Build order

This is the order the tasks should follow so that `main` always has something demoable.

1. **Skeleton** — repository layout, LFS, Unity project with XR set up, an empty APK that
   launches on the headset; server with `/v1/health`.
2. **Dark room and terminal** — zero-state world, the terminal with its states and streaming
   text. Demoable: you stand in the dark with a working terminal.
3. **Interpreter with replay** — parser, build queue, sky/light/fog, terrain, spawn, scatter,
   transitions, and the editor replay tool, driven by example scripts. Demoable: the reveal,
   from a file.
4. **Catalog and library, first pass** — library entries, export command, the first kits
   (`golf_course`, `city`), first assets.
5. **Server turn with text** — prompt, OpenRouter streaming, validation, substitution,
   session memory; verified with `try.py`. Then wire the client's turn client to it.
   Demoable: type a request in the editor, the world builds. *Story 1 minus voice.*
6. **Voice** — push-to-talk, capture, upload, transcription. *Story 1 complete.*
7. **Deploy** — Vultr, TLS, headset pointed at it; run the quickstart table on device.
8. **Follow-ups** — world description in the prompt, `modify`, `remove`, `clear`. *Story 2.*
9. **Holdables** — `give`, grab interaction, holdable library entries. *Story 3.*
10. **Library growth and polish** — more kits, assets, presets; transition and terminal
    polish; the sponsor visual touch; ambience loops.
11. **Spoken replies** — optional. *Story 4.*

Steps 3 and 5 can proceed in parallel once the contracts are fixed, which they are.

## Risks

| Risk | Mitigation |
|------|-----------|
| Transcription is slow or inaccurate in a noisy hall | Measure local Whisper on the real instance on day one; step up the model size or the instance if needed; `transcribe()` is one swappable function with ElevenLabs behind it; push-to-talk limits noise; typed input exists as a fallback for the demo |
| First statement arrives too late for the 5 s target | Fast model by default; long stable prompt first for caching; `status` lines and terminal animation cover the wait; measure early |
| Frame rate drops on unusual requests | Limits in the catalog are enforced on both sides; one shared material; no realtime shadows |
| Clone does not open cleanly on the Mac | Clone and build on the Mac before leaving for the venue; packages only from Package Manager |
| Venue Wi-Fi blocks or throttles the server | Check `/v1/health` from the headset browser on arrival; a phone hotspot is the backup network |
| The AI writes malformed lines | Per-line validation; a bad line is dropped with a `note`, the stream continues |

## Complexity Tracking

No constitution violations to justify.
