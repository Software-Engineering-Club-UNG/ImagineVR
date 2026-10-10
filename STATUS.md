# Status and handoff

Written by Claude on the Windows PC, night of 2026-10-09 into 10-10. Branch: `initial-work`.
Everything is committed locally. The code branch has **not** been pushed; only the docs-only
`specs-preview` branch is on GitHub.

ImagineVR is a voice-driven VR experience for Meta Quest 3S (Hacklanta, Creative track). You
start in a dark room facing a terminal that behaves like an agentic coding CLI, say what you
imagine, and the world is built around you in real time.

---

## For the next agent: start here

A new session has none of the conversation that produced this. Read, in this order:

1. this file
2. `.specify/memory/constitution.md` (the rules: bake first, thin headset, demo first)
3. `specs/001-voice-world-builder/plan.md`, then `contracts/` and `tasks.md`

### Exactly where the work stopped

- **Last thing finished**: 28 new kits (`client/Assets/ImagineVR/Scripts/Kits/Kits.More.cs`),
  about 200 new assets (`Scripts/Library/Assets.*.cs`), and one demo script per kit in
  `shared/examples/`.
- **Last successful compile**: straight after `Kits.More.cs` was written. Only non-code files
  changed after that, so it should still compile, but that is not re-verified.
- **About to do when interrupted**: render every demo to a picture and look at each kit,
  because only `golf_course` and `city` have ever been looked at. The command is under
  "Looking at worlds without a headset" below.
- **Blocked on**: Unity command-line runs fail with exit code 198 ("No valid Unity Editor
  license found") when Unity Hub is not running. Start Unity Hub, signed in, first.
- **Left undone because of that**:
  - `shared/catalog.json` is stale: 54 assets and 2 kits, while the code has 251 and 30.
    Run **ImagineVR > Export Catalog** (or the command below) and commit the result.
  - The 28 files in `client/Assets/ImagineVR/Resources/Examples/*.txt` were copied by hand
    and have no `.meta` files yet. Unity creates them on next open; commit them.

### Commands

Windows, from the repo root (Unity Hub must be running):

```powershell
./scripts/unity.ps1                                                     # compile only
./scripts/unity.ps1 -Method ImagineVR.EditorTools.Catalog.Export        # catalog + embed demos
./scripts/unity.ps1 -Method ImagineVR.EditorTools.Catalog.CheckAssets   # build every mesh, list triangle counts
./scripts/unity.ps1 -Method ImagineVR.EditorTools.Builder.BuildAndroid -Target Android -NoQuit -TimeoutSec 3600
```

There is no macOS/Linux wrapper yet. The equivalent direct call is:

```
/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath client -logFile - -executeMethod <Method>
```

Do not pass a broad `-Show` pattern to `unity.ps1`; it prints matching log lines and a loose
pattern returns thousands of them.

Server:

```
cd server
uv run uvicorn imagine.main:app --port 8080
uv run scripts/try.py --new "I want to see a golf course"
```

### Looking at worlds without a headset

The snapshot tool plays scripts in Play mode in batch and writes PNGs, which an agent can
open and look at. It needs a GPU, so no `-nographics`, and it must not be given `-quit`.

```powershell
# worlds: one picture per script, per time, per direction
./scripts/unity.ps1 -Method ImagineVR.EditorTools.Snapshot.Run -Graphics -NoQuit -TimeoutSec 600 `
  -Extra '-snapScripts','shared/examples/03-forest-dawn.ndjson;shared/examples/04-beach.ndjson', `
         '-snapOut','client/Snapshots','-snapTimes','9','-snapYaws','0,120,240'

# contact sheet of library assets by category (21 per page): "category:page"
./scripts/unity.ps1 -Method ImagineVR.EditorTools.Snapshot.Run -Graphics -NoQuit `
  -Extra '-snapSheet','furniture:0;party:0;landmarks:0','-snapOut','client/Snapshots'
```

Asset categories: shapes, nature, sky, buildings, city, vehicles, props, sports, art, toys,
magic, fantasy, animals, people, furniture, camp, farm, party, sea, space, spooky, winter,
landmarks, japan, desert, music, tools, food.

### Code map

```
client/Assets/ImagineVR/
  Scripts/
    App/        ImagineApp (builds everything at startup; the scene holds only this),
                Rig (head, hands, input, desktop mode, Grabber), Conductor (plays statements
                into terminal + world at a readable pace), SnapshotRunner (editor only)
    Core/       MiniJson, Statement (one line of ImagineScript, plus Ease)
    Net/        TurnClient (POST + streamed NDJSON reply), ImagineSettings
    Voice/      Voice (push-to-talk, WAV encoding)
    Terminal/   Terminal (lines, states, follow) and TerminalToolbar
    World/      World (dispatches statements), SkySystem (+ sky presets), TerrainSystem
                (+ terrain types, ground materials, height maths), ObjectSystem (instanced
                groups: spawn, scatter, modify, remove), PropSystem (physics props, give,
                paint strokes, ScoreZone), WeatherSystem (+ Fx bursts)
    Audio/      Sfx (synthesised effects), AmbienceSystem (+ LoopSynth)
    Library/    MeshBuilder (the modelling toolkit), Library (registry, lookup, Pal colours),
                Assets.*.cs (recipes)
    Kits/       Kit (KitDef, KitContext, KitSystem), Kits.Places (golf, city), Kits.More
  Editor/       ImagineEditor (Setup, Catalog, Snapshot, Builder, ReplayWindow)
  Resources/    Shaders/, Fonts/, Examples/, ImagineSettings.asset, CatalogVersion.txt
server/imagine/ main (routes), turn (one turn), script (validation, WorldState), catalog,
                llm (OpenRouter), fallback (offline director), stt (Whisper), tts, session,
                prompt.md (the model's instructions; {{CATALOG}} is filled from catalog.json)
shared/         catalog.json (generated by Unity), examples/*.ndjson (demo scripts)
```

### Conventions that are easy to break

- **Asset recipes**: metres, +Y up, origin at the centre of the base, front faces +Z. Tools
  stand upright with the grip at the origin; in the hand their +Y becomes the controller's
  forward. Things meant to be tinted are modelled white.
- **MeshBuilder fixes triangle winding itself** from the `outside` hint each face is given.
  Recipes never choose a winding order. Negative-height `Cyl`/`Cone` hang downward.
- **Emission**: `Emit(always, night)`; `Window()` is glass by day and warm light at night;
  `Glow()` adds a halo quad in submesh 1 (drawn by the Glow shader).
- **Use `AssetDef.Bounds`, not `Mesh.bounds`**. Mesh bounds are padded for halos.
- **Lighting never uses URP lights.** `SkySystem` sets `_Im*` shader globals every frame and
  every ImagineVR shader lights and fogs itself from them (`ImagineCommon.hlsl`).
- **Shaders live in `Resources/Shaders`** so builds include them; materials are made in code.
- **A MonoBehaviour or ScriptableObject that an asset or scene refers to needs its own file
  named after the class.** (`ImagineSettings` broke until it was moved out of TurnClient.cs.)
  Classes only ever added with `AddComponent` can share files.
- **The terminal font asset is dynamic and starts empty**: test glyphs with
  `HasCharacter(c, false, true)`. JetBrains Mono has no braille, ★ or ⎿; the terminal falls
  back to other characters on its own.
- **Kits** fill in a missing sky and terrain themselves, are rebuilt when the ground changes,
  and own any loose props they create (`KitContext.Loose`), removed with the kit.
- **Batch mode**: `WaitForEndOfFrame` never fires; instanced draws must be queued again
  (`Objects.Render()`) before a manual `Camera.Render()`.
- **Server on Windows**: use `127.0.0.1`, not `localhost` (two-second IPv6 delay).
  `pyproject.toml` caps Python below 3.14 because `uv` otherwise picks 3.15 and packages fail
  to build. WAV is decoded by hand in `stt.py` because faster-whisper's decoder breaks with
  the current PyAV.
- **Spec Kit CLI**: `uvx --python 3.13 --from git+https://github.com/github/spec-kit.git@v1.1.3 specify ...`

### Places I guessed and could not check

- How tools sit in the hand, and the hand model's position on the controller (`Rig.cs`).
- Terminal size and distance: chosen from one desktop render, never seen in a headset.
- Frame rate on the Quest. A scatter may hold 300 instances, a world 1,500, and kits with many
  tints make many draw calls. Limits are constants in `ObjectSystem` and `PropSystem`.
- Foveated rendering, floor-level tracking origin, haptics, the microphone permission prompt.
- Score zones (golf hole, hoop, goal): sizes and positions are estimates.
- Every sound. They are synthesised and nobody has heard them.
- `prompt.md` has never been run against a real model.

---

## Try it in two minutes (no headset, no server, no keys)

1. Open `client/` in Unity 6000.3.26f1, open `Assets/ImagineVR/Scenes/Main`, press Play.
2. You start in the dark room with the terminal. Press **F1** to **F8** for built-in demos,
   or **B** to cycle through all 28.
3. Hold the **right mouse button** to look around. **F12** returns to the dark room.

Other desktop keys: **Enter** to type a request (needs the server), **Space** to speak, **E**
grab, **left click** use the held thing, **R** recentre the terminal.

With no `OPENROUTER_API_KEY` the server uses an offline keyword director, so "I want to see a
golf course" or "make it sunset" already work end to end. Copy `server/.env.example` to
`server/.env` and add the key to use the real model (default `anthropic/claude-haiku-5.5`).

## Setting up another machine

- Unity Hub, signed in, and **Unity 6000.3.26f1** with **Android Build Support** (SDK/NDK and
  OpenJDK). Several gigabytes.
- `git`, `git lfs`, and `uv`.
- First server start downloads Python packages and the Whisper model.

`client/Library/`, `client/Temp/`, `client/Logs/`, `client/Snapshots/` and `server/.venv/` are
caches. They need not be copied; Unity and `uv` rebuild them.

## What exists

- **Server**: turn endpoint with streamed replies, validation and catalog substitution, session
  memory, OpenRouter streaming, offline fallback, Whisper transcription on the machine,
  optional ElevenLabs speech.
- **Unity client**, all built from code at startup: procedural sky (26 presets), terrain
  (14 types, 18 ground materials), water, GPU-instanced objects, 251 assets, 30 kits, physics
  props with grabbing and throwing, tools (paintbrush, blaster, wand, bubbles), weather
  (10 kinds), synthesised effects and 16 ambience loops, the terminal, push-to-talk, and a
  desktop mode.
- **Language**: ImagineScript v1. Beyond the plan it gained `weather`, `physics` (gravity),
  `physics: true` on spawn and scatter, vector `scale` and `rot`, and dotted ids for sculpting.

## Verified, and not verified

| Thing | State |
|---|---|
| C# compiles in Unity 6000.3.26f1 | Verified at the last code change |
| Golf course and night city render correctly (looked at rendered pictures) | Verified |
| All 251 assets build; animals, people and sea assets looked at | Verified |
| Server text turns, auth, offline director, Whisper on a spoken clip | Verified |
| The other 28 kits | Compile, **never looked at** |
| Real AI model through OpenRouter | **Not tested** (no key) |
| Anything on the headset | **Not tested** |
| Grabbing, throwing, tools, score zones | **Not tested**, even on the desktop |
| Android build | **Not attempted** |
| Microphone capture in the app | **Not tested** |
| ElevenLabs speech | **Not tested** (no key) |

## What is next

1. Start Unity Hub, export the catalog, commit it and the new `.meta` files.
2. Render or play each demo and fix kits that look wrong.
3. Try physics on the desktop: grab (E), throw, the paintbrush, the bowling lane.
4. Build the Quest APK and get it onto the headset (tasks T010, T063).
5. Add an OpenRouter key, run `scripts/try.py` with real requests, tune `prompt.md`.
6. Not written yet: `server/deploy/` (Vultr setup script, Caddyfile, systemd unit; task T061)
   and a macOS/Linux version of `scripts/unity.ps1`.
7. Docs that lag the code: `contracts/scene-language.md` and `contracts/catalog.md` do not
   describe the added operations and fields; `tasks.md` checkboxes are not ticked; `README.md`
   is still two lines; the constitution still has its scratch "Sync Impact Report" comment.

## Different from the plan, on purpose

- No XR Interaction Toolkit: hands and grabbing are a small custom system on the Input System.
- Library entries are code, not ScriptableObjects. A prefab override at
  `Resources/Library/<id>` is not wired up.
- Sounds are synthesised. A clip at `Resources/Sfx/<name>` or `Resources/Ambience/<id>`
  replaces the synthesised one.
