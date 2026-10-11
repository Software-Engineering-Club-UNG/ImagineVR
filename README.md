# ImagineVR

![Speak a world into being: ImagineVR's glowing terminal surrounded by a forest at dawn.](docs/images/speak-a-world.png)

Hacklanta 2 submission (Creative track).

A voice-driven VR sandbox for Meta Quest 3S: Garry's Mod with a voice, behind a terminal that
behaves like an agentic coding CLI. You stand in a dark room, say what you imagine, and it is
built around you as you watch. Then you poke at it: hold it, throw it, ride it, shoot it.

`old_main` is the first version, kept for reference. `main` is the rewrite described here.

## A look inside

<table>
  <tr>
    <td width="50%"><img src="docs/images/built-for-vr.png" alt="Built for VR: Meta Quest headsets and controllers." width="100%"></td>
    <td width="50%"><img src="docs/images/watch-it-build.png" alt="Watch it build: the terminal bringing a lunar landscape to life." width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="docs/images/many-places.png" alt="One idea, many places: a forest, the moon, a neon landscape, and a medieval village." width="100%"></td>
    <td width="50%"><img src="docs/images/make-it-playable.png" alt="Make it playable: a dinosaur world with a tool held in front of the terminal." width="100%"></td>
  </tr>
</table>

## How it fits together

The headset is thin. It records push-to-talk audio, sends it to the server, and plays what
comes back.

1. The server transcribes the audio (faster-whisper, on the VM).
2. It sends the text, the current world and what the user is holding to a model through OpenRouter.
3. It repairs the reply and streams it back as **ImagineScript**: one JSON statement per line.
4. The headset plays each statement into the terminal and the world as it arrives.

```
client/           Unity 6000.3.26f1 project (Quest 3S, plus a desktop mode for working without one)
openrouter-api/   FastAPI server: speech to text, the model call, the language, streaming
shared/           catalog.json: exported from Unity, read by the server. Do not edit by hand.
infra/, .github/  The VM and the workflow that deploys the server on every merge to main
scripts/          headset_log.py (pull and read session logs), unity.ps1 (Windows wrapper)
```

## The language

`openrouter-api/imagine/prompt.md` is the reference: it is what the model is told. In short:

| | |
|---|---|
| the place | `sky`, `terrain` (types, materials, `features`, `patches`), `weather`, `ambience`, `physics`, `player`, `clear` |
| things | `spawn` (one, or a `repeat` run or ring, or travelling a `path`), `scatter` (many: `clusters`, `along`, `where`), `give`, `ride`, `modify`, `remove` |
| on any thing | `behavior` (alive: wander, chase, flee), `physics` (loose), `solid` (a body), `motion`, `tint`, `scale` |
| anything the library lacks | `make`: a new asset from parts (cube, blob, lathe, slab, tube, ... with `mirror` and `repeat`), optionally with `fires` |
| reactions | `when`: an event (`hit`, `near`, `grab`, `use`, `every`, `start`), a target, and a list of actions |
| talk | `think`, `say` |

There are no scenario features: no kits, no game modes, no scoring system. Bowling is loose
pins and a ball; a city to drive through is rows of solid buildings, traffic on paths and a
ride; a shooting gallery is things that are alive and a `when hit` rule.

## Lessons from the first version (read before changing how things work)

1. **Check a build, not the editor.** Scenery was drawn with GPU instancing from materials
   made at runtime. The editor drew it. The headset build stripped those shader variants and
   drew nothing but creatures and held things, for the whole first day, while every log said
   "placed". Scenery is now combined into ordinary meshes on ordinary renderers
   (`ObjectSystem.Rebuild`). Before trusting any change to how things are drawn, run the
   snapshot tool against a built player (below).
2. **Log what is drawn, not what was asked.** The session log's `state` line now carries
   `renderers`, `drawn` and `triangles`, and each `placed` line says how many items exist.
   "The user says nothing is there" plus `drawn: 0` is a bug in the app, not in the prompt.
3. **Do not explain a bug with a story.** The model told the user things were "too far away"
   and moved them closer four times. Prompt changes cannot fix a client that is not drawing.
4. **Repair, never refuse.** The old `blueprint` was rejected whole for one bad field, and
   the user watched "rejected" scroll past. `language.py` and `Maker.cs` clamp, default and
   skip a bad part; nothing the model writes is turned away.
5. **One representation.** A made thing is an `AssetDef`, the same as a library one, so
   every feature works on it for free. The old version had five parallel object systems and
   each new feature worked on some of them.
6. **The headset owns its own state.** It used to mirror rule state to the server before each
   turn; a redeploy made every turn fail with 409 until the user reset. Now the server only
   remembers the statements it sent, and the headset sends one line about the user's hands.
7. **Every turn pays for the prompt.** It went from about 80 KB to about 20 KB. There is a
   test that fails if it grows past 24 KB.
8. **General beats special.** Each kit and demo made one request look good and taught the
   model to reach for it everywhere.

## Server

```
cd openrouter-api
cp .env.example .env            # add OPENROUTER_API_KEY; without one, turns answer "no model connected"
uv run --python 3.13 --with-requirements requirements-dev.txt python -m pytest -q
uv run --python 3.13 --with-requirements requirements.txt uvicorn app.main:app --host 127.0.0.1 --port 8080
uv run --with httpx python scripts/smoke.py          # against a running server with a key
```

Routes: `POST /v1/turn` (audio or text in, streamed NDJSON out), `POST /v1/session/reset`,
`GET /v1/health`, `GET /health` (deployed revision). Headers: `X-App-Token`, `X-Session-Id`,
`X-User-Pose` (`x,z,yaw` or `home`), `X-User-State` (what they hold and ride).

The hosted server is `https://64.177.52.162.sslip.io`. It is deployed by the workflow
`Test and deploy openrouter-api` on every push to `main`; see `openrouter-api/README.md`. Sessions live in memory, so a deploy makes the server forget the world it was
told about (the headset keeps it; the model just starts from "empty" for that session).

## Client

Open `client/` in Unity 6000.3.26f1, open `Assets/ImagineVR/Scenes/Main`, press Play.
Desktop: **Enter** types, **Space** speaks, right mouse looks, **WASD** moves, **E** grabs,
click uses, **C** folds the terminal, **F12** starts over. Headset: hold **A** to speak,
**Y** folds the terminal, **X** recentres it, hold **X** two seconds to start over, grip
grabs, trigger uses, left stick walks/strafes, and right stick turns 45 degrees on foot or drives while riding. Point an empty hand at a nearby door and press trigger to open it (desktop: look and press F). See [explorable interiors](INTERIORS.md).

The headset reads its server URL and token from `imagine.json` in the app's files folder
(falling back to `Resources/ImagineSettings.asset`).

### From the command line (macOS; the editor must be closed)

```
UNITY=/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity

# compile and run the play mode tests
$UNITY -batchmode -projectPath client -runTests -testPlatform PlayMode -testResults out.xml -logFile log

# play scripts and save pictures (no -quit, no -nographics)
$UNITY -batchmode -projectPath client -executeMethod ImagineVR.EditorTools.Snapshot.Run \
  -snapScripts "$PWD/a.ndjson;$PWD/b.ndjson" -snapOut "$PWD/out" -snapTimes 8 -snapYaws 0,90 -snapBare 1

# CHECK A BUILD: the same thing inside a real player, which strips shaders as the headset build does
$UNITY -batchmode -quit -projectPath client -buildTarget StandaloneOSX -executeMethod ImagineVR.EditorTools.Builder.BuildMacCheck
client/Builds/ImagineVRCheck.app/Contents/MacOS/ImagineVR -batchmode \
  -snapOut "$PWD/out" -snapScripts "$PWD/a.ndjson" -snapBare 1 -snapProbe 1
cat out/census.ndjson      # renderers, items and triangles really drawn; and the instancing probe

# the headset build, and the catalog (commit shared/catalog.json after changing the library)
$UNITY -batchmode -projectPath client -buildTarget Android -executeMethod ImagineVR.EditorTools.Builder.BuildAndroid
$UNITY -batchmode -quit -projectPath client -executeMethod ImagineVR.EditorTools.Catalog.Export
```

A script is a file of ImagineScript lines, such as the examples in `prompt.md`. Each run
dirties `Resources/Fonts/ImagineMono.asset`; restore it with `git checkout` before committing.

### Session logs

Every run writes an NDJSON log (the newest 20 are kept): on Quest in
`/sdcard/Android/data/com.hacklanta.imaginevr/files/logs`. `python3 scripts/headset_log.py pull`
copies them into the ignored `headset-logs/` and prints the newest as a timeline. They contain
what people said.

### Code map

```
client/Assets/ImagineVR/Scripts/
  App/       ImagineApp (builds everything at startup), Rig (head, hands, input, Grabber),
             Vehicle (ride), Conductor (plays statements at a readable pace), Hints,
             SessionLog, SnapshotRunner (pictures, in the editor or a built player)
  Core/      MiniJson, Statement
  Net/       TurnClient (POST, streamed NDJSON reply), ImagineSettings
  Voice/     push-to-talk
  Terminal/  the terminal
  World/     World (dispatches statements), SkySystem, TerrainSystem, WeatherSystem,
             ObjectSystem (spawn, scatter, and how scenery is drawn), Maker (`make`),
             Rules (`when`), PropSystem (loose and held things), Creature (alive), Mover (paths)
  Library/   MeshBuilder and the asset recipes: every model is code, there are no art files
  Audio/     Sfx and AmbienceSystem: every sound is synthesised
openrouter-api/imagine/
  main.py (routes), turn.py (one turn), language.py (repairing statements, the world as the
  server knows it), catalog.py, llm.py (OpenRouter streaming), stt.py, prompt.md
```
