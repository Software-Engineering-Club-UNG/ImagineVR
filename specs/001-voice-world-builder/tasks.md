---
description: "Task list for Voice World Builder (Core Loop)"
---

# Tasks: Voice World Builder (Core Loop)

**Input**: Design documents from `/specs/001-voice-world-builder/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: No automated test tasks (constitution: tests optional). Verification tasks run the
scenarios in quickstart.md.

**Organization**: Tasks are grouped by user story and follow the Build order in plan.md so
`main` always has something demoable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: Which user story the task belongs to (US1–US4)
- **(manual)**: Needs a person: an account, a physical device, or a download behind a login

## Path Conventions

- Unity project: `client/` — scripts under `client/Assets/ImagineVR/Scripts/`
- Server: `server/` — package under `server/imagine/`
- Shared between both: `shared/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repository layout and two empty-but-running projects

- [ ] T001 Create top-level folders `client/`, `server/`, `shared/examples/` and a root `.gitignore` covering Unity (`client/Library/`, `client/Temp/`, `client/Obj/`, `client/Logs/`, `client/UserSettings/`, `client/Build*/`, `*.apk`) and the server (`server/.env`, `server/.venv/`, `__pycache__/`)
- [ ] T002 Create root `.gitattributes` with Git LFS patterns for `*.fbx *.glb *.obj *.png *.jpg *.tga *.psd *.exr *.wav *.mp3 *.ogg *.ttf *.otf`, plus Unity YAML merge settings for `*.unity *.prefab *.asset *.meta`; run `git lfs install`
- [ ] T003 Create the Unity project at `client/` with Unity 6000.3.26f1 using the URP 3D template; confirm `client/ProjectSettings/ProjectVersion.txt` reads `6000.3.26f1`
- [ ] T004 Add packages to `client/Packages/manifest.json`: OpenXR Plugin, XR Plugin Management, XR Interaction Toolkit (with Starter Assets and XR Interaction Simulator samples), Input System, TextMeshPro essentials
- [ ] T005 Configure Android/Quest player settings in `client/ProjectSettings/`: Android build target, IL2CPP, ARM64 only, Vulkan only, minimum API level 32, OpenXR enabled for Android with the Meta Quest feature set and Oculus Touch controller profile, single-pass instanced stereo, active input handling = Input System, internet access = Require, microphone usage declared (`RECORD_AUDIO`)
- [ ] T006 [P] Configure URP for Quest in `client/Assets/ImagineVR/Settings/`: one mobile URP asset with 4x MSAA, HDR off, no realtime shadows, no depth/opaque texture, SRP batcher on; target 72 Hz and enable fixed foveated rendering at startup in `client/Assets/ImagineVR/Scripts/World/QuestPerformance.cs`
- [ ] T007 [P] Create the server project in `server/`: `pyproject.toml` (Python 3.12; fastapi, uvicorn, httpx, python-dotenv, faster-whisper), `.env.example` listing every variable in contracts/server-api.md, package folder `server/imagine/__init__.py`
- [ ] T008 [P] Implement `GET /v1/health` in `server/imagine/main.py` returning `ok`, `language_version: 1`, `catalog_version`, `model`, `stt`, `tts` per contracts/server-api.md
- [ ] T009 Create `client/Assets/ImagineVR/Scenes/Main.unity` with an XR Origin (XRI rig, left/right controllers, ray and direct interactors) and add it as the only scene in the build
- [ ] T010 (manual) Enable developer mode on the Quest 3S, connect by USB-C, run Build And Run, and confirm an empty scene launches in the headset with tracked controllers

**Checkpoint**: An APK launches on the headset; the server answers `/v1/health`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The dark room, the terminal, the ImagineScript interpreter, and the catalog.
Everything every story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Dark room and terminal

- [ ] T011 Implement the zero-state world in `client/Assets/ImagineVR/Scripts/World/WorldRoot.cs`: black skybox, ambient light zero, black fog, no directional light contribution, no terrain, no objects; this is what `Main.unity` shows at launch (FR-001)
- [ ] T012 [P] Add a monospace OFL-licensed font as a TextMeshPro font asset in `client/Assets/ImagineVR/Library/Fonts/` (with its licence file) and a self-lit terminal material that stays readable with zero scene light
- [ ] T013 Build the terminal prefab `client/Assets/ImagineVR/Library/Terminal.prefab` and `Scripts/Terminal/TerminalView.cs`: world-space panel about 1.6 m wide placed 1.5 m in front of the user at eye height; scrolling line history (FR-004); line styles for prompt (`> text`), tool call (`● op(args)`), assistant prose, dim note, and problem
- [ ] T014 Implement character-by-character streaming text and a blinking cursor in `client/Assets/ImagineVR/Scripts/Terminal/TerminalLine.cs` (FR-002)
- [ ] T015 Implement the state indicator in `client/Assets/ImagineVR/Scripts/Terminal/TerminalState.cs` with exactly these states: `ready`, `listening`, `thinking`, `building`, `problem` (FR-003), each with distinct colour and animation (spinner for thinking/building, pulse for listening)
- [ ] T016 Implement lazy follow in `client/Assets/ImagineVR/Scripts/Terminal/TerminalFollow.cs`: the terminal stays put until it is more than about 50° off the user's gaze for 1 s, then glides back in front (FR-005)
- [ ] T017 Show the welcome text at launch in `client/Assets/ImagineVR/Scripts/Terminal/TerminalBoot.cs`: product name, version line, and "Hold A and say what you imagine" (US1 acceptance 1)

### ImagineScript interpreter

- [ ] T018 Define statement types in `client/Assets/ImagineVR/Scripts/Script/Statements.cs` for every v1 operation in contracts/scene-language.md (world: `say clear sky light fog terrain kit spawn scatter give modify remove ambience`; stream: `transcript status note error done`)
- [ ] T019 Implement the line parser in `client/Assets/ImagineVR/Scripts/Script/ScriptParser.cs`: one JSON object per line; unknown `op` and unknown fields are skipped without error; malformed lines are dropped and reported as a dim note; `t` default 1.5 clamped to 0.2–6; numeric ranges clamped as listed in the contract
- [ ] T020 Implement the ordered build queue in `client/Assets/ImagineVR/Scripts/Script/BuildQueue.cs`: applies statements one at a time in arrival order with a short stagger, writes each world statement to the terminal as a tool-call line when applied (FR-011), and exposes events for stream statements
- [ ] T021 [P] Write the procedural sky shader `client/Assets/ImagineVR/Shaders/ImagineSky.shader`: three-stop gradient (top, horizon, bottom), sun disc with elevation and azimuth, star amount 0–1, cloud amount 0–1; all parameters animatable; all-zero parameters render pure black
- [ ] T022 Implement `client/Assets/ImagineVR/Scripts/World/SkySystem.cs`: applies `sky`, `light`, and `fog` statements by lerping from current to target values over `t`; a preset sets sky, directional light, ambient, and fog together; individual fields override the preset
- [ ] T023 [P] Create sky preset data assets in `client/Assets/ImagineVR/Library/Sky/`: `void`, `clear_noon`, `golden_hour`, `night_stars`, `night_city`, `overcast`, `dawn`, `alien_dusk`, `stormy`, `deep_space`
- [ ] T024 Implement `client/Assets/ImagineVR/Scripts/World/TerrainSystem.cs`: runtime-generated ground mesh for types `flat`, `rolling`, `hills`, `dunes`, `paved`, `water` with `material`, `tint`, `height` 0–1, `seed`; keeps a flat disc of 3 m radius under the user; rises from below as its transition; extends to `world_radius_m` (120)
- [ ] T025 [P] Write the shared palette lit shader `client/Assets/ImagineVR/Shaders/PaletteLit.shader` (palette texture, per-instance tint, GPU instancing, dissolve amount) and create terrain material variants `grass`, `sand`, `snow`, `asphalt`, `rock`, `water`, `dirt` in `client/Assets/ImagineVR/Library/Materials/`
- [ ] T026 Implement transitions in `client/Assets/ImagineVR/Scripts/Transitions/`: `GrowIn.cs`, `DissolveOut.cs`, `RiseIn.cs`, `CrossFade.cs`; no statement may make anything appear or vanish in a single frame (FR-010)
- [ ] T027 Implement `client/Assets/ImagineVR/Scripts/World/ObjectSystem.cs`: `spawn` (absolute `at` or `place` front/left/right/behind/center at `distance`, default 4 m), `scatter` (areas around/front/ring/edges/everywhere, rested on terrain, never inside the safe disc, GPU-instanced), `remove`, `modify`, `clear` with `keep`; tracks things by `id`, reusing an `id` replaces the old thing; baked blob shadow under spawned objects
- [ ] T028 Enforce limits in `client/Assets/ImagineVR/Scripts/World/WorldLimits.cs` read from the catalog: `max_spawned` 60, `max_scatter_count` 300, `max_scatter_total` 1500, `max_kits` 3; excess is trimmed and a dim note is written (FR-015)

### Library and catalog

- [ ] T029 Define library entries in `client/Assets/ImagineVR/Scripts/Library/LibraryEntry.cs` (ScriptableObject: `id` lowercase snake_case, `desc`, `tags`, `aliases`, `size_m`, `holdable`, prefab reference) and `LibraryIndex.cs` to look entries up by id for assets, presets, terrain types, materials, kits, ambience, and the five primitives `cube sphere cylinder cone plane`
- [ ] T030 Implement nearest-match fallback in `client/Assets/ImagineVR/Scripts/Library/NearestMatch.cs` per contracts/catalog.md: exact alias, then most shared words across id/tags/aliases, then for assets a tinted primitive with the requested name as a floating label (FR-014)
- [ ] T031 (manual) Download CC0 low-poly packs (Kenney, Quaternius) covering nature, city buildings, street props, and sports items into `client/Assets/ThirdParty/<pack>/`, each with its licence file; confirm the licence says CC0 before committing
- [ ] T032 Create the first 25 library entries and prefabs in `client/Assets/ImagineVR/Library/Assets/` using the palette material: trees (pine, oak, palm), bushes, rocks, grass tufts, flowers, buildings (3 heights), street lamp, bench, car, road segment, flag pole, golf flag, sand bunker, fence, cloud, bird, crate, barrel
- [ ] T033 Implement the kit base in `client/Assets/ImagineVR/Scripts/Kits/KitBase.cs` (inputs `density` 0–1, `size` 0–1, `seed`, `params`; builds with staggered transitions; respects the safe disc and limits) and `KitSystem.cs` to apply `kit` statements
- [ ] T034 [P] Implement `client/Assets/ImagineVR/Scripts/Kits/GolfCourseKit.cs`: fairway strip, raised green with flag and hole, 2–4 bunkers, tee box near the user, tree line along the edges
- [ ] T035 [P] Implement `client/Assets/ImagineVR/Scripts/Kits/CityKit.cs`: street grid around the user, building modules of varied height with lit windows, street lamps, a few parked cars; the user stands at an intersection
- [ ] T036 Implement the catalog exporter `client/Assets/ImagineVR/Editor/ExportCatalog.cs` (menu **ImagineVR → Export Catalog**) writing `shared/catalog.json` in the exact shape of contracts/catalog.md with a new `catalog_version` each export; also copies it into `client/Assets/StreamingAssets/catalog.json`; run it and commit the result

### Replay tool and examples

- [ ] T037 [P] Write example scripts in `shared/examples/`: `golf-course.ndjson`, `city.ndjson`, `dark-to-dawn.ndjson`, `sunset-followup.ndjson`, using only catalog IDs
- [ ] T038 Implement the replay window `client/Assets/ImagineVR/Editor/ReplayScript.cs` (menu **ImagineVR → Replay Script…**): feeds a chosen `.ndjson` file into the build queue line by line with a configurable delay, in Play mode
- [ ] T039 Verify quickstart.md section 4 in the editor: dark room and terminal at start; replaying `golf-course.ndjson` shows tool-call lines while the sky opens, the ground rises, and the course appears, all animated

**Checkpoint**: The reveal works from a file. Client and server work can now proceed in
parallel against the contracts.

---

## Phase 3: User Story 1 - Speak a world into existence (Priority: P1) 🎯 MVP

**Goal**: Hold a button, say a place, see your words in the terminal, and watch the dark room
become that place.

**Independent Test**: Launch on the headset, hold A, say "I want to see a golf course", and
confirm the prompt appears and the surroundings change within the timing targets.

### Server: a turn from text

- [ ] T040 [P] [US1] Implement `server/imagine/catalog.py`: load `IMAGINE_CATALOG` (default `../shared/catalog.json`), nearest-match per contracts/catalog.md, and render the catalog as compact text for the prompt
- [ ] T041 [P] [US1] Write `server/imagine/prompt.md`: the assistant's role, the full ImagineScript v1 world-statement reference from contracts/scene-language.md, coordinate conventions, limits, the rules "output only NDJSON lines, one statement per line, no prose outside `say`", "use a kit first when one fits", "always end with one short `say`", how to handle non-world requests (`say` only), and declining inappropriate requests; placeholders for the catalog and the current world
- [ ] T042 [P] [US1] Implement `server/imagine/llm.py`: OpenRouter chat completions with streaming via httpx, model from `IMAGINE_MODEL`, stable system prompt first, yields complete lines as soon as each newline arrives
- [ ] T043 [US1] Implement `server/imagine/script.py`: validate each AI line against v1 (reject stream ops written by the AI, drop malformed lines), clamp ranges, enforce catalog limits, substitute unknown IDs with the nearest match and emit a `note` statement naming the substitution
- [ ] T044 [P] [US1] Implement `server/imagine/session.py`: in-memory sessions keyed by `X-Session-Id`, dropped after 30 minutes idle, one turn at a time per session (a second turn waits), and storage for the world description
- [ ] T045 [US1] Implement `server/imagine/turn.py` and `POST /v1/turn` in `server/imagine/main.py` for `application/json` `{ "text": … }`: emit `status: thinking`, stream validated statements with `status: building` before the first world statement, always finish with `done` and an outcome from `world_changed`, `object_given`, `answered`, `not_understood`, `declined`, `failed`; on mid-stream failure emit `error` then `done: failed`; response `application/x-ndjson` unbuffered
- [ ] T046 [US1] Add `X-App-Token` check (`401`), a per-session rate limit (`429`), empty-body handling (`400`), provider-unreachable handling (`503`), and `POST /v1/session/reset` (`204`) in `server/imagine/main.py`
- [ ] T047 [P] [US1] Write `server/scripts/try.py`: posts text to `IMAGINE_URL` (default `http://localhost:8080`) with a fixed session id and prints each line as it arrives with elapsed time
- [ ] T048 [US1] Verify quickstart.md section 3: `try.py "I want to see a golf course"` prints `status`, at least `sky`, `terrain`, and a `kit` or `scatter`, and `done: world_changed`; record time to first world statement for two candidate models and set the faster acceptable one as the default in `server/.env.example`

### Client: talk to the server

- [ ] T049 [P] [US1] Create the settings asset `client/Assets/ImagineVR/Settings/ImagineSettings.asset` and `Scripts/Net/ImagineSettings.cs`: server URL, app token, timeouts (8 s between lines, 45 s total)
- [ ] T050 [US1] Implement `client/Assets/ImagineVR/Scripts/Net/TurnClient.cs`: `POST /v1/turn` with `X-App-Token`, `X-Session-Id` (random at launch), `X-Catalog-Version`; streaming download handler that hands each complete line to the parser as it arrives; calls `/v1/session/reset` at launch
- [ ] T051 [US1] Wire stream statements to the terminal in `client/Assets/ImagineVR/Scripts/Terminal/TurnPresenter.cs`: `transcript` → prompt line, `status` → state, `say` → prose, `note` → dim line, `error` → problem state with the message, `done` → `ready`
- [ ] T052 [US1] Handle failures in `TurnClient.cs`: non-200 responses, no line for 8 s, stream ending without `done`, and no network all show a plain-language problem line, leave the world as it is, and return to a state where the user can try again (FR-020)
- [ ] T053 [P] [US1] Add an editor-only debug prompt in `client/Assets/ImagineVR/Scripts/Net/DebugPrompt.cs`: a text field in the Game view that sends typed requests as JSON turns
- [ ] T054 [US1] Verify quickstart.md section 5 with typed input in the editor: prompt line, `thinking`, `building`, the world builds, back to `ready`

### Voice

- [ ] T055 [US1] Implement push-to-talk in `client/Assets/ImagineVR/Scripts/Voice/PushToTalk.cs`: right controller primary button (A) held = listening, Space in the editor; request microphone permission on first use; terminal shows `listening` immediately on press
- [ ] T056 [US1] Implement capture in `client/Assets/ImagineVR/Scripts/Voice/MicCapture.cs`: Unity `Microphone` at 16 kHz mono, 16-bit PCM WAV encoding, 30 s cap; a release after less than 0.4 s of audio returns to `ready` without sending; on release switch to `thinking` within 1 s (FR-008) and send as `audio/wav` through `TurnClient`
- [ ] T057 [US1] Implement `server/imagine/stt.py`: `transcribe(wav_bytes)` using faster-whisper with `IMAGINE_STT_MODEL` (default `base.en`), model loaded once at startup, CPU int8; `IMAGINE_STT=elevenlabs` switches to ElevenLabs speech-to-text behind the same function
- [ ] T058 [US1] Accept `audio/wav` in `POST /v1/turn` in `server/imagine/turn.py`: reject over 30 s with `413`; emit `transcript` as the first line; empty or unintelligible speech yields a `say` asking the user to repeat and `done: not_understood` with no world statements (FR-019)
- [ ] T059 [US1] Allow a new recording while a turn is in progress in `PushToTalk.cs` and `TurnClient.cs`: the new turn is sent immediately, the server queues it, and the build queue applies both in order (FR-021)

### Deploy and prove on device

- [ ] T060 (manual) [US1] Create an Ubuntu LTS instance on Vultr with at least 4 vCPUs and 8 GB memory; note its IP; obtain an OpenRouter API key with a spend limit
- [ ] T061 [US1] Write `server/deploy/setup.sh`, `server/deploy/Caddyfile`, and `server/deploy/imagine.service`: install uv and Caddy, run uvicorn as a systemd service reading `/etc/imagine.env`, reverse-proxy HTTPS with response buffering off (use an `sslip.io` hostname for the IP if there is no domain)
- [ ] T062 [US1] Deploy to the instance, set keys in `/etc/imagine.env`, verify `/v1/health` over HTTPS and `try.py` against the public URL; measure transcription time for a 5-second clip on the instance and raise `IMAGINE_STT_MODEL` or the instance size if it exceeds about 1.5 s
- [ ] T063 [US1] Point `ImagineSettings.asset` at the deployed URL, build to the headset, and run quickstart.md section 6 steps 1, 2, 3, 9, and 10; fix anything that misses the timing targets (acknowledge within 1 s, first change within 5 s, complete within 20 s)

**Checkpoint**: Story 1 works on the headset. This is the MVP and a complete demo.

---

## Phase 4: User Story 2 - Reshape the world with follow-ups (Priority: P2)

**Goal**: Small requests adjust the current world; a request for a different place replaces it.

**Independent Test**: After building one world, say "make it sunset" and confirm only the sky
and light change; then say "put me in a city" and confirm the world is replaced.

- [ ] T064 [US2] Maintain the world description in `server/imagine/session.py`: keep the latest `sky`, `light`, `fog`, `terrain`, `ambience`; keep each live `id` with the statement that made it and later `modify` folded in; `clear` and `remove` delete entries; update it from the validated statements of each turn
- [ ] T065 [US2] Render the world description and the last few prompts and `say` replies into the model context in `server/imagine/turn.py` as a short ImagineScript listing titled "current world"
- [ ] T066 [US2] Extend `server/imagine/prompt.md` with follow-up rules: change only what was asked; reuse existing `id`s with `modify` and `remove`; never repeat unchanged statements; use `clear` first only when the user asks for a different place; and worked examples for "make it sunset", "add more trees", "now a city"
- [ ] T067 [US2] Make `modify` cover every created thing in `client/Assets/ImagineVR/Scripts/World/ObjectSystem.cs` and `Scripts/Kits/KitSystem.cs`: `scale`, `tint`, `count` for scatter groups (adds or removes instances with transitions), `density` for kits
- [ ] T068 [US2] Polish the replace transition in `client/Assets/ImagineVR/Scripts/World/WorldRoot.cs`: `clear` dissolves objects and kits and lowers terrain while the next statements bring the new world in, with no frame where the user sees an abrupt cut
- [ ] T069 [US2] Verify quickstart.md section 6 steps 4, 5, 7, and 11 on the headset, and that the terminal history still shows every earlier prompt and step

**Checkpoint**: Stories 1 and 2 work together as a conversation.

---

## Phase 5: User Story 3 - Ask for something to hold (Priority: P3)

**Goal**: Ask for an object, see it appear within reach, grab it, use it, drop it.

**Independent Test**: In any world say "make me a golf club" and confirm it appears within
reach and can be grabbed, moved, released, and picked up again.

- [ ] T070 [P] [US3] Create at least 10 holdable library entries and prefabs in `client/Assets/ImagineVR/Library/Holdables/` with `holdable: true`, colliders, rigidbodies, and grab attach points: golf club, golf ball, paintbrush, umbrella, flashlight, sword, baseball bat, balloon, mug, flower
- [ ] T071 [US3] Implement `give` in `client/Assets/ImagineVR/Scripts/World/HeldItemSystem.cs`: the object grows in about 0.5 m in front of the user at chest height, floating until first grabbed; if the asset is not holdable use the nearest holdable entry; held items are the only things allowed inside the safe disc
- [ ] T072 [US3] Add XRI grab interaction to holdables in `client/Assets/ImagineVR/Scripts/World/Holdable.cs`: grip to grab with direct and ray interactors, follows the hand, physics on release, rests on terrain, can be picked up again; anything that falls below the ground returns to in front of the user
- [ ] T073 [US3] Add `give` guidance to `server/imagine/prompt.md` (requests like "make me a…", "give me a…", "I want to hold…" use `give`, outcome `object_given`) and include held items in the world description in `server/imagine/session.py`
- [ ] T074 [US3] Re-export the catalog (`shared/catalog.json`), restart the server, and verify quickstart.md section 6 steps 6 and 8 on the headset

**Checkpoint**: Stories 1–3 work; the full three-minute demo is possible.

---

## Phase 6: User Story 4 - The assistant talks back (Priority: P4)

**Goal**: A short spoken reply and ambient sound that suits the world. First to drop if time
runs short.

**Independent Test**: Make any request and confirm a spoken reply is heard and the ambient
sound changes with the world.

- [ ] T075 [P] [US4] Add 8 ambience loops to `client/Assets/ImagineVR/Library/Ambience/` as library entries (for example `birds_breeze`, `city_night`, `ocean_waves`, `forest_deep`, `desert_wind`, `rain`, `space_hum`, `crowd_distant`), seamless, compressed for Quest; sources must be CC0 or generated for this project
- [ ] T076 [US4] Implement `ambience` in `client/Assets/ImagineVR/Scripts/World/AmbienceSystem.cs`: cross-fade between loops over `t`, `volume` 0–1; silence in the dark room; add an ambience rule to `server/imagine/prompt.md`
- [ ] T077 [P] [US4] Implement `server/imagine/tts.py` and `GET /v1/audio/{id}` in `server/imagine/main.py`: when `IMAGINE_TTS=on`, synthesise each `say` text with ElevenLabs, keep the audio for at least 5 minutes, and add the `audio` URL to the `say` statement without delaying world statements
- [ ] T078 [US4] Play `say.audio` in `client/Assets/ImagineVR/Scripts/Terminal/Voiceover.cs`: fetch and play the clip from near the terminal; a failed fetch is silent and never blocks the turn
- [ ] T079 [US4] Re-export the catalog, restart the server with `IMAGINE_TTS=on`, and verify on the headset that replies are spoken and ambience follows the world; confirm `IMAGINE_TTS=off` restores the silent behaviour

**Checkpoint**: All four stories work.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Library breadth, feel, and demo readiness

- [ ] T080 [P] Add kits in `client/Assets/ImagineVR/Scripts/Kits/`: `ForestKit.cs`, `BeachKit.cs`, `DesertKit.cs`, `SpaceKit.cs`, each with the parts it needs as library entries
- [ ] T081 [P] Grow the asset library toward 60–100 entries in `client/Assets/ImagineVR/Library/Assets/`, prioritising things people ask for first: animals, vehicles, furniture, landmarks, weather props; give every entry generous `aliases`
- [ ] T082 [P] Add the sponsor nod in `client/Assets/ImagineVR/Library/Terminal.prefab`: an original paintbrush icon and tool-palette styling on the terminal frame; no Adobe logos or trademarks (FR-023)
- [ ] T083 Polish the first-request reveal in `client/Assets/ImagineVR/Scripts/World/WorldRoot.cs`: when leaving the zero state, light spreads outward from the terminal and the first sky transition runs longer (about 4 s) (FR-001a)
- [ ] T084 [P] Add controller haptics and short interface sounds for talk press, talk release, and each applied statement in `client/Assets/ImagineVR/Scripts/Terminal/TerminalFeedback.cs`
- [ ] T085 Profile on the headset with the heaviest scenes (dense city, large scatter); adjust catalog limits in `ExportCatalog.cs` and kit densities until 72 fps holds throughout (SC-007)
- [ ] T086 Tune `server/imagine/prompt.md` against 10 varied requests through `try.py` until 10 of 10 produce a fitting visible world with no errors (SC-005)
- [ ] T087 [P] Rewrite the root `README.md`: what ImagineVR is, the architecture in a paragraph, and setup pointing at specs/001-voice-world-builder/quickstart.md
- [ ] T088 (manual) Clone the repository fresh on the MacBook Air M1, install Unity 6000.3.26f1 with Android Build Support, open `client/`, and Build And Run to the headset; fix anything that needed manual intervention (Constitution V)
- [ ] T089 (manual) Demo rehearsal on the headset: golf course, city, and golf club five times in a row each (SC-006), then the full run inside three minutes (SC-008); at the venue, load `/v1/health` in the headset browser on the venue Wi-Fi

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: needs Setup; blocks every story
- **US1 (Phase 3)**: needs Foundational
- **US2 (Phase 4)**: needs US1 (it is a follow-up to a built world)
- **US3 (Phase 5)**: needs US1; independent of US2
- **US4 (Phase 6)**: needs US1; independent of US2 and US3
- **Polish (Phase 7)**: T080–T082, T084, and T087 can start any time after Foundational; the rest need the stories they tune

### Within Phase 2

- T011 → T013 → T014, T015, T016, T017
- T018 → T019 → T020 → T022, T024, T027
- T021 → T022; T025 → T024 and T032; T026 → T024, T027
- T029 → T030, T032, T033 → T034, T035 → T036 → T037 → T038 → T039

### Within US1

- Server chain: T040, T041, T042, T044 → T043 → T045 → T046 → T048
- Client chain: T049 → T050 → T051 → T052 → T054 (T054 also needs T045)
- Voice: T055 → T056; T057 → T058; both → T059
- Deploy: T060 → T061 → T062 → T063 (T063 needs everything above)

### Parallel Opportunities

- After Phase 1, one person can take the Unity side of Phase 2 while another starts the
  server tasks T040–T047, which depend only on the contracts and a hand-written
  `shared/catalog.json` until T036 produces the real one.
- Within Phase 2: T012, T021, T023, T025 alongside the terminal work; T034 and T035 together.
- US3 and US4 can run at the same time once US1 is done.
- Library growth (T080, T081) can run alongside everything from Phase 3 onward.

---

## Parallel Example: User Story 1

```text
# Server pieces with no dependency on each other:
T040 catalog.py   T041 prompt.md   T042 llm.py   T044 session.py   T047 try.py

# Client pieces that can start at the same time as the server chain:
T049 ImagineSettings   T053 DebugPrompt
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: Setup — an APK on the headset and a server that answers
2. Phase 2: Foundational — the reveal works from a file (demoable fallback from here on)
3. Phase 3: US1 — typed input first (T054), then voice (T059), then on device (T063)
4. **Stop and validate**: Story 1 alone is a complete demo

### Incremental Delivery

1. US1 → the core demo
2. US2 → it becomes a conversation
3. US3 → you can hold what you asked for
4. Library growth and polish (Phase 7) → arbitrary requests look good
5. US4 → sound, only if everything above is solid

Merge to `main` at every checkpoint, and only when it builds and runs on the headset.

### If time runs short

Drop in this order: US4, extra kits (T080), haptics (T084), US3. Do not drop T085, T088, or
T089.

---

## Notes

- Tasks marked **(manual)** need a person; everything else can be done by the coding agent,
  though Unity scene and prefab work must be checked in the editor.
- After any change to the library, re-run **ImagineVR → Export Catalog**, commit
  `shared/catalog.json`, and restart the server.
- The contracts in `specs/001-voice-world-builder/contracts/` are the source of truth for the
  language, the catalog, and the API. If a task needs to deviate, change the contract first.
