# ImagineVR overhaul (language v3): what changed, and how to check it

Branch `overhaul-v3`. Written without Unity or a headset: **no C# here has been compiled by Unity, no shader change has been compiled, and the OpenRouter image and speech calls have never been made for real.** The server half is covered by 179 pytest tests. This page is the QA agent's starting point; `docs/OVERHAUL_HANDOFF.md` has the deeper design notes.

## 1. What a user gets

- **Several model calls per new place** (server): a director lays it out; a modeller details each custom asset it sketched (`make` with `refine`); a detail pass dresses it; a play pass makes it a game and writes the closing line. Follow-ups stay one call. `IMAGINE_DEPTH=0` turns this off.
- **Worked examples** (`shared/playbook/*.ndjson`, 19 of them: bowling, golf, mini golf, race, city taxi, flight, dogfight, heli rescue, zombie waves, shooting gallery, knight quest, escape room, hockey, hoops, tanks, rollercoaster, gentle beach, solar-system tour, "surprise me"). The two nearest a request are shown to the model; none for a request like nothing in them.
- **A game layer** in the headset: counters, objectives, stages, zones, health, win/lose, resets, scoreboards, a HUD.
- **Vehicles that drive and fly as what they are**: steering geometry and grip for cars, tracks for tanks, a flight model for planes (throttle, stall, takeoff, landing), hover for helicopters, hover bikes and dragons. Wheels roll, rotors spin, and the steering wheel turns and can be gripped.
- **Roads, cities and big ground**: street grids with buildings facing the streets, traffic that brakes, pedestrians, lamps, circuits with lap times; ground up to 1.2 km across with painted, levelled regions (greens, bunkers, runways).
- **People who talk**: anyone with a `persona` answers when the user looks at them and holds talk; a story can call the director back (`direct`).
- **Sound**: synthesised effects from recipes the model writes, and a generative score in a mood.
- **56 new library assets**: 23 articulated vehicles, 12 buildings, 21 game props.

Old headsets (no `play-v1` in `X-Client-Capabilities`) get exactly the old prompt, catalog and language: `tests/test_fanout.py::test_an_older_headset_is_one_call_with_the_prompt_it_always_had`.

## 2. Where it lives

| | Server (`openrouter-api/imagine/`) | Headset (`client/Assets/ImagineVR/Scripts/`) |
|---|---|---|
| language | `v3.py` (repair of every op/field), `expr.py`, `fix.py`, `language.py` | `Core/Statement.cs` (WorldOps), `World/World.cs` (dispatch, each op in try/catch) |
| game layer | | `Play/PlaySystem.cs`, `Play/Zones.cs`, `Play/Health.cs`, `Play/Hud.cs`, `Play/Expr.cs`, `World/Rules.cs` |
| vehicles | | `App/Drive.cs`, `App/Vehicle.cs`, `World/Articulation.cs`, `Library/MeshBuilder.cs` (part capture) |
| world | | `World/RoadSystem.cs`, `World/TerrainSystem.cs` (size, regions), `Library/Architecture.cs`, `Play/Things.cs` (surfaces, lights, effects), `Play/Lights.cs` |
| sound | `tts.py` (voices) | `Audio/Synth.cs`, `Audio/Music.cs` |
| model calls | `turn.py`, `prompts.py`, `prompts/*.md`, `router.py`, `playbook.py`, `gen.py` | `Net/TurnClient.cs`, `App/Conductor.cs`, `App/ImagineApp.cs` |
| library | `shared/catalog.json` (`since: 3` entries) | `Resources/LibraryV3.json` from `scripts/library_v3.py`, loaded by `Library/Assets.ThirdVersion.cs` |

## 3. QA checklist, in order

1. **Compile.** Open `client/` in Unity 6000.3.26f1. Expect some errors: fix them before anything else. These are the most likely places:
   - `Play/Expr.cs`: the nested `Node.Eval`, and the "Color Color" naming of `Kind`.
   - `World/Rules.cs`: switch sections that declare locals.
   - `App/Vehicle.cs`: the drive branch inside `Update`.
   - `World/RoadSystem.cs`: tuple and `List<object>` conversions.
   - `Play/Hud.cs`: deconstruction of named tuples.
   - `Library/MeshBuilder.cs`: `Slice`.
2. **Shaders.** `Resources/Shaders/ImagineCommon.hlsl` now has 12 light slots instead of 4 (the arrays and one `[unroll]` loop). It is the only existing shader file touched. If it fails to compile, set the arrays and loop back to 4 and set `Lights.Count` to 4.
3. **Play-mode tests.** Run `Tests/PlayTests.cs`, which covers:
   - expressions, held to the server's answers by `shared/expr-cases.json`;
   - counters, `becomes` firing on the rising edge, stages and zones;
   - part capture;
   - a car that accelerates, turns and stops, and a plane that takes off.

   The existing tests (`WorldTests`, `WalkTests`, `HeldItemTests`, `StructureTests`) must still pass.
4. **Catalog parity.** Run `ImagineVR > Export Catalog`, then `python3 scripts/catalog.py check` (expect `True`/`True`). `git diff shared/catalog.json` should be empty or only reorder nothing. If it differs, Unity is right: commit its export.
5. **Snapshot each playbook scene in a built player** (README, "Check a build, not the editor"). Run `-snapScripts` over `shared/playbook/*.ndjson`, with `-snapProbe 1`. Look for:
   - roads: grids, ovals and runways drape over the ground;
   - buildings face their street;
   - zones show their markers;
   - the HUD strip and scoreboards render;
   - lamps light the ground at night.
6. **Desktop drive test.** Use `Autopilot`, or WASD while riding:
   - `{"op":"ride","asset":"sports_car"}`: W throttle, S brake/reverse, A/D steer, Shift handbrake. Wheels roll and the front ones steer. The gauge shows km/h and gear.
   - `prop_plane`: arrows set the throttle, W/S pitch, A/D roll. It needs speed to take off.
   - `helicopter`: WASD moves it, the arrows turn and climb.
7. **Headset passes.**
   - Driving: right trigger, left trigger, left stick, and gripping the wheel with either hand.
   - Flight: left stick Y is the throttle, the right stick flies. The horizon must stay level, with the craft banking around the user.
   - A rules game: bowling (`shared/playbook/bowling.ndjson`) and golf. Check that pins tip, the frame advances and the putt rolls.
   - Combat: zombie waves, where enemies attack and the user's health and death work.
   - Talking: stand in front of a character with a persona, hold A, ask something. The voice needs `IMAGINE_TTS` set on the server.

## 4. Known gaps (deliberately not done, or not verifiable here)

- **Generated pictures are not shown.**
  - The client does not send `gen-v1`, so the server never sends `image_url`.
  - Showing one needs a shader that samples a texture, plus loading through `UnityWebRequestTexture`. Proposed: copies named `SkyPano.shader` and `Picture.shader`.
  - The server side (`gen.py`, `GET /v1/gen/{key}`) is written and unit-tested only through `turn.py`.
- **Not implemented:**
  - climbing;
  - the zone effects slow, boost and gravity;
  - a turret that turns;
  - enemies of `team:"player"` fighting other teams;
  - a terrain collider beyond ±50 m (balls far out are rescued by `PropSystem.Tick`).

  The prompts do not promise any of these.
- **Talking to a character** works only for live creatures. A persona makes a still person into a `guard` creature (`ObjectSystem.Spawn`).
- **`finish` and `pattern` are carried but not drawn.** They reach the client and are stored on parts, but `Lit.shader` ignores them. A `LitPlus.shader` (handoff §6.5) would draw specular, glass and procedural patterns from vertex-colour alpha.
- **Vehicle guns.** Each `gun` part is marked, but tank and jet shots all leave from those points along the vehicle's nose. There is no turret aim.
- **The model's control descriptions in `prompts/ride.md` must match `Vehicle.ReadDrive`.** Change both together.

## 5. Server settings (all optional)

`IMAGINE_DEPTH` (0 one call, 1 + modellers, 2 + passes [default], 3 + concept pictures), `IMAGINE_CRAFTS` (4), `IMAGINE_EXAMPLES` (2), `IMAGINE_CRAFT_MODEL` / `IMAGINE_PASS_MODEL` / `IMAGINE_NPC_MODEL` (default `IMAGINE_MODEL`), `IMAGINE_TURN_SECONDS` (70), `IMAGINE_TTS` (`off`, `openrouter`, `elevenlabs`, `local`, `on`), `IMAGINE_TTS_MODEL`, `IMAGINE_TTS_VOICES` (JSON), `IMAGINE_TTS_LOCAL_MODEL` / `IMAGINE_TTS_LOCAL_VOICES` (Kokoro files), `IMAGINE_IMAGES` (`on` to make pictures), `IMAGINE_IMAGE_MODEL`, `IMAGINE_GEN_CACHE`, `IMAGINE_PLAYBOOK`.

New routes: `POST /v1/speak` (a voice for a line a rule speaks), `GET /v1/gen/{key}` (a generated picture; 202 while it is being made). New request headers: `X-Play-State`, `X-Talk-To`; JSON `{"event": "..."}` is a story event turn.
