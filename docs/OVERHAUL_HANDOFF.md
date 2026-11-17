# ImagineVR overhaul: handoff

Branch `overhaul-v3`. Written by Claude Fable 5.2 for whoever continues (Opus 5.5). Read this first, then `README.md` ("Lessons from the first version" still bind) and `openrouter-api/imagine/v3.py`.

The approved plan is at `~/.claude/plans/make-a-plan-i-elegant-lobster.md`. This file supersedes it where they differ.

## Update (PR #17)

The client work in §6 has since been written, by Opus 5.5, following §7's priorities: seams, the game layer, rules, combat, articulation, drive models, roads, terrain size and regions, lights, synth, music, and the 56 library assets. **It has not been compiled.** `docs/OVERHAUL.md` is now the current status and QA checklist. These were not done: `LitPlus.shader`, the picture and sky texture shaders (so the client does not send `gen-v1`), climbing, turret aim, and `tests/test_gen.py`. Read the rest of this file as design notes.

## 1. Where things stand (when the server was finished)

**Done and tested (server, Phase 1):** 174 server tests pass. The one failure you will see locally, `test_local_stt.py::test_cached_model_uses_cpu_int8_and_no_network`, only means `faster-whisper` is not installed in the scratch venv; CI installs it.

```
cd openrouter-api
uv venv /tmp/iv && uv pip install --python /tmp/iv/bin/python fastapi==0.143.0 httpx==0.28.1 python-dotenv==1.2.4 numpy==2.5.3 pytest==9.1.1 PyYAML==6.0.3 pillow
/tmp/iv/bin/python -m pytest -q
python3 ../scripts/catalog.py check                 # catalog is written exactly as Unity writes it
/tmp/iv/bin/python scripts/preview.py <file with make lines> <out dir>   # draws any `make` to PNG; look at the PNG
```

| Piece | Files | State |
|---|---|---|
| Language v3: repair of every new op and field | `imagine/v3.py` (mixin), `imagine/language.py`, `imagine/fix.py` | done, tested (`tests/test_v3.py`) |
| Rule expressions | `imagine/expr.py`, `shared/expr-cases.json` | done, tested; the C# port must pass the same table |
| Prompt sections + router | `imagine/prompts/*.md`, `imagine/prompts.py`, `imagine/router.py` | done, tested (`tests/test_prompts.py`) |
| Worked examples ("playbook") | `shared/playbook/*.ndjson` (19), `imagine/playbook.py` | done; every line validated by test |
| Multi-call turn (director + crafts + detail pass + play pass), NPC talk turns, event turns | `imagine/turn.py`, `imagine/session.py`, `imagine/main.py` | done, tested (`tests/test_fanout.py`) |
| Images and speech through OpenRouter, optional local Kokoro | `imagine/gen.py`, `imagine/tts.py` | written, **not tested and never run against the real API** |
| Catalog: 56 new assets, 5 new ground materials, `since: 3` | `shared/catalog.json`, `scripts/catalog.py`, `scripts/catalog_additions.json` | done; **the client recipes do not exist yet** |
| Old headsets | capability gating (`play-v1`, `gen-v1`), `Catalog.before(3)`, `prompt.md` untouched | done, tested: an old build sees exactly today's behaviour |

**Not started: the whole Unity client (plan Phases 2 to 7).** Nothing under `client/` has been changed except `Resources/CatalogVersion.txt`. No client advertises `play-v1` yet, so none of the new server behaviour is reachable from a headset until the client work lands. That also makes the current branch safe to merge as it is.

**Server loose ends (small):**
- `requirements.txt`: add `pillow==13.0.0` (used by `gen.finish`; imported lazily, falls back to raw bytes without it).
- `tests/test_gen.py` not written (fake `httpx.AsyncClient`; `gen.join_ends`; `tts.mode()` table).
- `tests/test_catalog_v3.py` not written: assert every id in `scripts/catalog_additions.json` has an `Add("id", "category", ...)` of the same category in the C# library, in the same order (model: `tests/test_held_catalog.py`).
- `.env.example`, `openrouter-api/README.md`, root `README.md` not updated for the new env vars (`IMAGINE_DEPTH`, `IMAGINE_CRAFTS`, `IMAGINE_EXAMPLES`, `IMAGINE_CRAFT_MODEL`, `IMAGINE_PASS_MODEL`, `IMAGINE_NPC_MODEL`, `IMAGINE_TURN_SECONDS`, `IMAGINE_IMAGES`, `IMAGINE_IMAGE_MODEL`, `IMAGINE_GEN_CACHE`, `IMAGINE_TTS=off|on|openrouter|elevenlabs|local`, `IMAGINE_TTS_MODEL`, `IMAGINE_TTS_VOICES`, `IMAGINE_TTS_LOCAL_MODEL`, `IMAGINE_TTS_LOCAL_VOICES`, `IMAGINE_PLAYBOOK`).
- `infra/deploy/openrouter-api.service`: add `Environment=IMAGINE_GEN_CACHE=/var/cache/openrouter-api/gen` (the only writable path). The provision workflow writes a fixed env file; new keys are optional and default sensibly.
- OpenRouter request shapes in `gen.py`/`tts.py` come from their docs (`POST /api/v1/images` with `model, prompt, aspect_ratio, resolution` returning `data[0].b64_json`; `POST /api/v1/audio/speech` with `model, input, voice, response_format, instructions` returning raw audio). Voice names in `tts.OPENROUTER_VOICES` are the OpenAI ones and are a guess for the default model; check `GET /api/v1/models?output_modalities=speech`.

## 2. What the user decided

- General purpose, never tailored: no kits, no game modes in the client. Bowling, golf, racing come from general pieces.
- All AI generation through the one OpenRouter key, or local/on-device on consumer hardware. No fal.ai, no new ElevenLabs dependence. OpenRouter has no 3D or sound-effect models, so: meshes come from a dedicated LLM modelling call; sound effects and music are synthesised on the headset from specs the LLM writes.
- "Balanced" budget: Haiku 5.5 for every call (`IMAGINE_DEPTH=2` default).
- One PR against `main`; no Unity, no headset; a later agent does QA. Merging `main` auto-deploys the server, so only open the PR.
- "Without using too much space": no Unity install, no binary assets, scratch tooling outside the repo.
- Most recently: spend effort on the parts that need a strong model; tests are secondary but still wanted.

## 3. How a turn works now (server)

```
request -> Caps(X-Client-Capabilities) -> kind: user | talk (X-Talk-To names a thing in the world) | event (JSON {"event": "..."})
user turn, play-v1 client:
  director call   system = core.md <<<THIS REQUEST>>> routed sections [+ director.md when DEPTH>=2] + interiors
                  user   = CURRENT WORLD (+ USER RIGHT NOW, PLAY RIGHT NOW) + up to 2 playbook examples + USER SAYS
  each `make` with `refine`  -> a craft call (system = make.md) starts at once; its `make` replaces the rough one by name
  when the director ends, if it laid out a place (terrain|structure|road|clear) and placed >= 3:
      detail pass and play pass start together (system = core + pass section), reading the world as the director left it
  all lines go through one queue into Checker.check -> NDJSON. Deadline IMAGINE_TURN_SECONDS (70).
  the director's `say` is held; the play pass's `say` is used if there is one. `refine` never reaches the headset.
```

`core.md` is byte-identical across director and passes so the provider caches it (`llm.SPLIT` marks the end of the cached part). Per-call options (max_tokens, images) travel in the `llm.options` ContextVar, so test fakes keep the 3-argument `stream_lines` signature.

## 4. The contract the client must implement

Source of truth for fields and clamps: `imagine/v3.py`. Source of truth for what the model has been **promised** the headset does: `imagine/prompts/*.md`. If you change behaviour, change the prompt in the same commit. The promises that are easy to miss:

**Capabilities and transport**
- Send `X-Client-Capabilities: structures-v1,structures-v2,play-v1,gen-v1` only once the client really plays v3.
- Send `X-Play-State`: one line, e.g. `score 3; frame 2; stage wave_2; health 80; lap 1` (every var, current stage, player health).
- Talk turn: when push-to-talk is released while a character with a `persona` is addressed (within 5 m and within about 25 degrees of gaze, or pointed at), add `X-Talk-To: <thing id>`.
- Event turn: the `direct` action POSTs JSON `{"event": "<note with {vars} filled in>"}` to `/v1/turn`. Server rate-limits (429); ignore a 429 silently. Do not send while a turn is in flight; queue one.
- `sky.image_url`, `spawn.image_url`: GET, 202 means not ready (retry every 2 s, give up after about 60 s), 200 is a JPEG. `say/speak/narrate.audio`: GET audio; `.wav` or `.mp3` by URL ending. Rule actions `speak`/`narrate` have no URL: `POST /v1/speak {"text","voice"}` returns `{"audio": url|null}`.
- New ops must be added to `Statement.WorldOps` or `Conductor` skips them: `road zone var hud stage fx sfx music speak narrate story`.
- `frame` now also arrives on `road`, `zone`, `fx`, `terrain` (with regions), `player` (with `at`), `hud` (with board), and on `when`/`stage` whose actions hold positions: apply `s.Frame()` to `at`, `points`, `regions[].at`, `board.at`, and action `to`/`at`.

**Controls the prompts tell users about**
- car, bike, tank, mount, boat: right trigger throttle, left trigger brake then reverse, left stick steers; gripping a `steer` part with either hand and turning it also steers. B fires a ground vehicle's guns.
- plane, glider: left stick Y is throttle (stays where put), right stick pitches (back = up) and rolls. Right trigger fires.
- heli, hover, space, living winged things: left stick translates, right stick X turns, right stick Y climbs/descends.
- rail follows `path`; `simple` is today's behaviour. Right stick click gets out (unchanged).
- On foot: right stick click jumps (when `player.jump`); `fly`: point right hand and squeeze trigger; `climb`: grip a solid collider and pull. Empty hand + trigger within 3 m is `press`.
- Talk to a character: look at them, hold the talk button.

**Matching and semantics**
- A rule target or expression name matches: exact id, asset id, id prefix + `.` (existing), **and id prefix + `_`** (new: `pins` covers `pins_1`..`pins_4`). Server ids cannot contain dots.
- Any action may carry `if` (expression), `delay`, and `target` (act on that thing instead of the event's subject). Text in `banner/win/lose/objective/direct` replaces `{var}` with the value.
- `becomes` fires on the rising edge of `test`; re-arms when it goes false. Evaluate when a var changes and a few times a second for world functions.
- `time` = seconds in the current stage (or since the first v3 statement). `stage` is a string var. Undefined vars are 0.
- A road with `checkpoints` owns vars `lap`, `lap_time`, `best_lap` and raises event `lap`.
- `timer` arrives as a `var` with `rate` (per second) and `as: "time"`.
- `health` on a thing: hits subtract damage (default 1), zero raises `die`; without `health` keep today's knock-down-and-get-up. Player health is opt-in; at zero raise `die` for target `player`, and if no rule handles it, fade, restore, return to the last `checkpoint`.
- `reset target`: put a physics group back at its spawn poses, recreating destroyed members (keep each group's source statement).
- `tip`: a prop's up axis passes about 55 degrees from vertical, once. `rest`: speed under 0.05 m/s for 0.5 s after having moved. Track only groups a rule or expression names.
- Plain spawned scenery cannot receive `hit` today (no Prop or Creature). Give solid or rule-targeted placements a small `Hittable`; give `Mover`s a kinematic body. This also fixes the old prompt's own chest example.

## 5. Facts about the client you will otherwise rediscover

- `Main.unity` has one GameObject with `ImagineApp`. Everything is created in `ImagineApp.Awake`. New systems are plain classes owned by `World` (constructed in `World.Init`, ticked in `World.Update`) or components added in code.
- C# 9 level, no LINQ in runtime, no string interpolation in the existing style, `sealed` classes, `_camelCase` fields. IL2CPP: no reflection. MiniJson: objects are `Dictionary<string, object>`, arrays `List<object>`, **every number is `double`**. Read fields only through `Statement` accessors (`Str/Num/Int/Flag/TryVec3/Vec/ObjList/Obj/StrList/TryColor`, `Statement.Wrap(dict)` for nested objects).
- Every `.cs`, shader and folder has a committed `.meta` (two lines: `fileFormatVersion: 2` and `guid: <32 hex>`, no trailing newline). Create one per new file with a fresh GUID. New scripts under `Scripts/` join `ImagineVR.Runtime` automatically.
- No URP lights exist or work (additional lights are off in the pipeline). All lighting is custom globals in `Resources/Shaders/ImagineCommon.hlsl`, set by `SkySystem.Push`. Held lights: 4 slots (`_ImHeldLightPositions/Directions/Colors`), published by `HeldLights` in `HeldItemRuntime.cs`.
- Shaders: only `Resources/Shaders`; runtime keywords must be `multi_compile_local`; never rely on GPU instancing; stereo is single-pass instanced (keep `UNITY_VERTEX_INPUT_INSTANCE_ID` etc.). Shaders cannot be compiled outside Unity: **add new shader files beside the old ones and fall back at runtime** (`Shader.Find` null or `!isSupported`), never edit `Lit.shader` in place.
- Mesh layout: vertex colour = albedo (alpha always 1 today), UV0 = (emission always, emission night; 2 = warm window), UV1.x = sway. Scenery is combined per group (`ObjectSystem.Rebuild`) with per-vertex pivots written to UV2, drawn with the `IM_BAKED` keyword. `ObjectSystem.Single()` copies UV0/UV1 as `Vector2`: do not change UV dimensions.
- `AssetDef` is one lazily built mesh (`Recipe(MeshBuilder)`); there are no parts, sockets or seats. `Placement` stores the `Mesh` at placement time, so redefining a made asset needs: update `Placement.Mesh` in every group whose `Asset` is that id, set `Dirty`, and rebuild live visuals.
- Renderer setup is duplicated in `Vehicle.Mount`, `Mover.Create`, `Creature.Create`, `PropSystem.Create`. Replace all four with one helper when adding articulation.
- `Vehicle.cs`: the rig is moved kinematically; the ride mesh is a child of the rig. `Blocked()` (solid scenery sweep), `Bump()` (props and creatures), `Crash()`, `Liven()` (lean, audio, dust, comfort vignette) are reusable. Walking uses a `CharacterController`.
- `World.Radius` is a `const 120f` used in `Vehicle.cs` (2 places), `Creature.cs` (3), `HeldItemRuntime.cs`, `StructureSystem.cs`, `World.cs` (`Sky.SetWorldRadius`), `Editor/ImagineEditor.cs`. Make it a static property.
- Terrain: `TerrainMath.N = 129`, `Extent = 125` constants; non-uniform grid (`Coord(i)`: fine near the origin); `TerrainData.HeightAt` is bilinear on the grid (exact to the mesh); shapes in `TerrainMath.Raw` are tuned in absolute metres round the origin; generation runs on a worker `Task`. The `MeshCollider` is 41x41 over +/-50 m only.
- Limits to raise for the denser builds: `ObjectSystem.MaxSpawned` 60 -> 140, `MaxScatterTotal` 1500 -> 2500, `PropSystem.MaxPhysics` 40 -> 64 (the catalog already says so), `Maker.MaxParts` 48 -> 120.
- `Rules.Fire(string ev, in Subject)` is the single entry point for events; `Rules.Subject` carries Creature/Prop/Asset/Group/Position. Do not touch `Grabber.ThrowVelocity`/`Release` maths (the owner tuned throwing by hand).
- `Conductor.Speak(url)` already downloads and plays TTS with `UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG)`. `Callout` (in `Hints.cs`) + `Panel.shader` is the reusable world-space text panel for all HUD surfaces. `AmbienceSystem` renders loops on a worker Task: copy that pattern for music.
- Editor export (`ImagineEditor.cs` about lines 276-367) hard-codes `language_version = 2` and has no `since`. Change to 3; add `AssetDef.Since` and `GroundMaterial.Since` (default 0), written last in the entry as `"since": 3` only when > 0. Register new assets in new partial methods called **after** `RegisterThemes()` in `Library.EnsureLoaded` (order = catalog order = `scripts/catalog_additions.json` order: vehicles, buildings, play props).

## 6. Client design, in the order to build it

Wrap every new handler in `World.Apply` in try/catch that writes `SessionLog.Write("fault", ...)`: a bug in a new system must never kill a turn. Add a static `Features` class (bools read from `imagine.json`) so QA can switch any subsystem off: `Play, Drive, Articulation, Roads, LitPlus, Lights, Music, Gen`.

### 6.1 Seams (small, do first)
`TurnClient` (capabilities, `X-Play-State`, `X-Talk-To`, event turns), `Statement.WorldOps`, `World.Apply` cases, `World.Radius` -> property, raised limits, `Conductor` routing `speak/narrate/story/hud` (they are world ops but should not pace like builds).

### 6.2 Play layer: `Scripts/Play/`
- **`Expr.cs`**: port `imagine/expr.py` exactly (tokenizer, precedence, functions). Parse once to a tree, cache by string. `IExprHost` supplies vars and world functions (`count alive standing tipped moving dist inside holding riding speed height health`). World-function arguments are names, not values. Write `Tests/ExprTests.cs` that loads `shared/expr-cases.json`.
- **`PlayState.cs`**: vars (double or string), `rate`, `min/max`, labels, stages (`goal`, `enter`, `next`, `timeout`/`fail`, `first`), objective, stage clock. Serialises `X-Play-State`. Cleared by `clear`.
- **`Hud.cs`**: (1) a strip that lazily follows the gaze low in view (copy `Terminal.Follow`): objective line + chips for labelled vars + health bar + ammo; (2) centre banner that fades; (3) world boards (`hud.board`); (4) vehicle gauges (speed, gear; airspeed, altitude, throttle).
- **`Zones.cs`**: box/sphere, world-placed or `on` a thing; test player head, vehicle, tracked props/creatures each few frames; raise `enter`/`exit`; effects (`damage heal gravity push slow boost kill`); markers (`ring beam flag glow`) from MeshBuilder with the Glow/Unlit materials.
- **`Health.cs`**: per-thing hp/team, `World.Damage(subject, amount, source)`, floating numbers, `die`; player health, fade (reuse `Comfort`), checkpoint.
- **`Rules.cs`**: add events, `if`, `stage` scope, `cooldown`, `times`, the new actions, action-level `if`/`target`, `_`-prefix matching, `press` via the existing empty-hand ray (`World.TryBoard` / `Structures.TryInteract` site in `Grabber.Update`).

### 6.3 Articulation
- `MeshBuilder.BeginPart(role, pivot)` / `EndPart()`: geometry in between is also recorded into a separate vertex list in pivot-local space. `AssetDef.Mesh` stays the full rest pose (scenery combining unchanged); add lazy `BodyMesh` (everything not in a part) and `Parts` (mesh, pivot, role, anim).
- `AssetDef` additions: `Parts`, `Seat`/`HasSeat`, `Drive`, `Damage`, `MagSize`, `Reload`, `Recoil`, `Hitscan`, `Blast`, `StrikePower`, `StrikeLoft`, `Since`.
- New `World/Articulation.cs`: `Articulated.Attach(GameObject host, AssetDef def, Material lit, Material glow, MaterialPropertyBlock block)` builds body + one child renderer per part (falls back to the single mesh when `Parts` is empty) and exposes `Drive(wheelDegrees, steerDegrees, throttle, lamps)` and `Tick(dt, moving)`.
- Roles: `wheel` spins about the vehicle's X axis through its pivot, and wheels with pivot z > bounds centre also yaw with steering; `spinner` spins about the part's own local Y (rate scales with throttle when ridden); `steer` turns about its own Y with steering input; `flap` swings about Z (mirrored copies opposite); `lamp` registers a world light when on; `thruster` emits `Fx` with throttle; `gun` is a muzzle; `seat` = sit on top of it. `anim`: `spin` deg/s and `swing` degrees with `every` seconds, about `pivot` or the part's position, `when: always|moving|alive|throttle`.
- `Maker.cs`: read `role anim pivot finish pattern pattern_color pattern_scale`, shapes `building stairs arch asset`, top-level `drive seat strike health`, `fires` extras; `fires` no longer forces `tool` for vehicle/flyer/animal/person. Re-`make` of an existing name refreshes instances (see section 5).

### 6.4 Drive models: `Scripts/App/Drive/`
`Vehicle.cs` keeps mounting, `Home/PlaceAt`, walking, `Blocked/Bump/Crash/Liven`, and the legacy path as `simple`. Add `Drive` (abstract: `Step(dt, in DriveInput, ref Vector3 position, ref float yaw)`, visual pitch/roll/offset out, gauges) chosen from `ride.drive`, else `AssetDef.Drive`, else: `vehicles` category with wheel parts -> car; `Airborne` vehicles -> plane (rotor/`spinner` on top -> heli); `OnWater` -> boat; animals -> mount; `Winged` animals -> hover; else simple. **The rig only yaws and translates; pitch and roll go to the visual** (existing comfort approach, keep it).

- **GroundDrive** (car/bike/tank/mount/boat by parameters). State: `vLong, vLat, yaw, steer, y, vy`. Per step:
  `steerTarget = input.steer * lerp(35, 8, saturate(|vLong| / (0.6 * top)))`, slew 180 deg/s.
  throttle: `a = accel * throttle * (1 - (vLong/top)^2)`; brake: decelerate at `brake`, then reverse to `-0.3 * top`; rolling and air drag.
  `yawRate = vLong / wheelbase * tan(steer)`; needed lateral accel `= vLong * yawRate`; limit `= mu * 9.81 * grip` where `mu` comes from the surface under it (asphalt 1, dirt .7, grass .6, sand .5, snow .35, ice .15; wet weather x .8). Over the limit: scale `yawRate` down and feed the excess into `vLat`; `vLat` decays with `exp(-mu * k * dt)`. Handbrake (desktop Shift) drops rear grip.
  Move by `forward * vLong + right * vLat` through `Vehicle.Blocked`; a blocked step with speed over 4 m/s is `Crash`.
  Ground: sample `World.SurfaceHeight` at four wheel points (from wheel part pivots, else bounds); body height follows a damped spring; if the ground falls away faster than gravity the car is airborne (`vy -= g dt`). Visual pitch/roll = terrain slope + accel squat + lateral lean.
  Top speed: 18 m/s on normal ground, 30 on wide, 45 on vast, times `ride.speed`. Tank: differential steer, turns in place. Boat: only where water depth > 0.3, bobbing, wide turns. Mount: quick steering, gait via the existing `_Gait`.
  Wheels: spin `vLong / radius`; engine pitch from a fake gearbox (`rpm = frac(|vLong| / gearSpan)`); screech and skid puffs when `|vLat|` is high.
- **AirDrive** (plane/glider). State: position, `yaw, pitch, roll, v, throttle, grounded`. Thrust minus drag minus `g sin(pitch)`; roll from stick, auto-levels when centred; `yawRate = g tan(roll) / max(v, vMin)` plus rudder; climb `= v sin(pitch)` above stall speed, nose drops below it. On the ground it steers like a car and lifts off above takeoff speed with back stick. Terrain contact with a high sink rate is `Crash` and a bounce. Visual roll clamp +/-35, pitch +/-20. `ride.altitude` starts airborne at cruise speed.
- **HoverDrive** (heli/hover/space/winged): velocity follows stick with inertia in the craft's frame, right stick X yaws smoothly, right stick Y changes height, visual tilts into motion, `space` has no gravity or ground clamp.
- **`Cockpit.cs`**: seat = `ride.seat` / `AssetDef.Seat` / `seat` part / heuristic (car: left of centre, forward of middle, 1.05 m up; plane: on the centreline). Steering-wheel grab: when grip is held within 0.2 m of a `steer` part, steer = signed angle of the hand about the wheel's axis relative to the grab, / 120 degrees. `Grabber.Update` must skip grabbing while a control is held.
- Vehicle guns: an asset with `fires` and a ride fires from its `gun` parts (or the nose) through `PropSystem.Fire`.

### 6.5 World
- **Terrain size**: move `N`/`Extent` into `TerrainRecipe` (`normal` 125 m N=129, `wide` 400 m N=161, `vast` 1200 m N=193). Evaluate shapes at scaled coordinates so every type still works: `Raw(shape, amp, seed, x/k, z/k) * pow(k, 0.6)` with `k = extent/125`. `World.Radius` follows it; call `Sky.SetWorldRadius`.
- **Regions and road beds**: `TerrainRecipe.Mods` (disc/box/capsule; flatten to its centre height, or offset; material override; blend width), applied after the base height, coloured from the region's `GroundMaterial`. New materials `green fairway gravel mud cobble` with `Since = 3`.
- **Collider follows the player**: re-centre the 41x41 collider on a 25 m lattice. **Surface lookup**: `World.SurfaceAt(x, z)` -> grip, roll drag, name (region material, else terrain material, else a `surface` set on a solid shape under the point). Rolling drag: set a sphere prop's `linearDamping` from it each fixed step (green .35, fairway .8, grass 2.5, sand 6, ice .02, wood .05).
- **`World/RoadSystem.cs`**: centreline from `points` (Catmull-Rom) or `shape` (`straight loop oval grid cross figure8 ring`, with `at rot_y size radius length blocks block`); ribbon meshes via MeshBuilder (asphalt, lane markings 2 cm above, kerbs, sidewalks, crosswalks on grids); terrain flattened under it with a Mod; `lamps` along it with world lights; `barriers`; `traffic` as `Mover`s on lane polylines that brake for what is ahead and have bodies; `walkers` as creatures on sidewalk paths; `buildings` on lots along both sides, facing the road, solid near the user; `checkpoints` -> zones `<id>_cp1..n`, lap vars and the `lap` event; `ride` after a road starts the user on it facing along it. Default widths by kind: street 8, highway 14, lane 5, dirt 5, track 12, runway 22, rail 2.2, path 2.5, bridge 8.
- **`Library/Architecture.cs`**: one facade generator (floors, window grid with frames and `Window()` emission, door and awning when `shop`, cornice, roofs `flat gable hip spire dome stepped shed`, styles as colour + pattern). Used by the `building` part shape and by the 12 new catalog buildings.
- **New library assets** (ids, categories, sizes and order are in `scripts/catalog_additions.json`; each needs `Add(...)` with `Since = 3`): 23 vehicles with wheel/steer/lamp/seat/spinner/gun parts (`tank`, `fighter_jet`, `starfighter` have `fires`), 12 buildings, and props `ring_gate explosive_barrel sandbags coin gem lever push_button barrier tire_stack hockey_stick hockey_puck pool_cue billiard_ball dartboard dart punching_bag phone_booth food_cart dumpster parking_meter windsock`. Re-author `car bus golf_cart airplane boat rocket ufo` with parts. Give `golf_club baseball_bat tennis_racket hockey_stick pool_cue` a strike spec. Use `scripts/preview.py` on equivalent `make` JSON to check proportions if in doubt.
- **`LitPlus.shader`** (new file; `Lit.shader` untouched). Finish and pattern ride in **vertex colour alpha** as one integer float: `alpha = 1 + finish + 16*pattern + 256*scaleStep + 2048*paletteIndex` (legacy alpha 1 decodes to "none"; `Lit` ignores alpha so the same meshes draw either way; `CombineMeshes` carries colours unchanged). Pass world position, world normal and the packed value to the fragment stage; per pixel: sun specular and a fresnel tint from `_ImAmbSky` for `paint gloss chrome glass metal`, procedural patterns by dominant normal axis (`stripes checker bricks planks tiles panels windows dots noise scales camo grid`), `windows` lit with `_ImNight`. 16-colour palette for `pattern_color` (nearest). Choose `LitPlus` only if found, supported and `Features.LitPlus`.
- **World lights**: grow the held-light arrays in `ImagineCommon.hlsl` from 4 to 12 (same loop, larger bound) and publish held lights + the nearest lit things (`light` on spawns, lamps, fires, headlights) from one place. This is the one edit to an existing shader file; it copies a loop that already compiles.
- `fx` emitters (`Fx.Stream`), `breaks` (replace with tinted debris props + dust), `label`/`sign` (TMP child), `SkyPano.shader` and `Picture.shader` (copies with one texture sample, used only when an image arrives).

### 6.6 Characters, combat, story
- `Creature.cs`: hp, team, `attack` (close, 0.5 s wind-up, strike, 1.2 s cooldown), `shoot` (hold 8-14 m, telegraph 0.4 s, hostile projectile), `guard`, `follow`, `patrol`; two-whisker avoidance against solids; with `health` it stays down when it dies. Raise `MaxLive` carefully (24 today).
- Weapons in `Grabber` "blaster" + `PropSystem.Fire`: damage, magazine/reload (empty click, reload on next trigger pull after `Reload` s), recoil kick, hitscan tracer, blast radius.
- `World/Npc.cs`: name tag, `says` barks, face the user when addressed, speech bubble (`Callout`), audio at the speaker; without audio play a pitched "babble" from `Sfx`.
- Striker: for a held tool with a strike spec, sweep the top third of the tool between fixed steps against loose props; on contact set velocity = head velocity x power, rotated up by loft; raise `strike` and `hit`.

### 6.7 Sound
- `Audio/Synth.cs`: `sfx` layers -> `AudioClip` (24 kHz mono): `wave freq to dur attack decay vol vibrato depth lowpass highpass drive delay echo`; register by name so `Sfx.Play(name)` finds it. Add synthesised versions of the new names in `v3.SOUNDS` (anything unknown already falls back).
- `Audio/Music.cs`: `music {mood,tempo,key,chords,energy,instruments}` -> a loop of 4 to 8 bars (pad, bass, arp, drums) rendered on a worker Task, cross-faded under ambience.

## 7. If time is short, cut from the bottom

1. Seams + play layer + Rules v2 + Hittable + reset/tip/rest (bowling, golf, shooters, stories all need these).
2. Articulation + GroundDrive + Cockpit + the car-like library vehicles ("proper car support" was the first thing asked for).
3. Terrain size/regions/surfaces + RoadSystem + buildings (cities, tracks).
4. AirDrive, HoverDrive, aircraft.
5. Creature AI, weapons, NPC talk, striker.
6. LitPlus, lights, music, synth, generated images.

Whatever is not built: remove it from the prompts (and the playbook) or leave the client off `play-v1`, so the model never promises what the headset cannot do.

## 8. Verification and the PR

- Server: the pytest command above; the PR's CI runs it.
- C#: Unity is not installed here. `dotnet` 9 and 10 are. Best effort: compile all runtime `.cs` against Unity reference assemblies (try the `UnityEngine.Modules` NuGet package for 6000.x on the BepInEx feed) plus thin stubs for InputSystem, TMP and OpenXR; if that is not quick, `dotnet build` with no references and filter for syntax error codes only. Keep it in a scratch folder.
- Shaders cannot be checked at all: say so in the PR.
- Also write `docs/OVERHAUL.md` for the QA agent: feature flags, every seam, what was never run (all C#, all shaders, the OpenRouter image and speech calls), and the Unity steps (play-mode tests, re-export the catalog and confirm `scripts/catalog.py check`, Snapshot tool over `shared/playbook/*.ndjson` in a built player).
- Then: commits per phase on `overhaul-v3`, `git push -u origin overhaul-v3`, `gh pr create --base main`. Commit trailer `Co-Authored-By:` per the session's instructions; PR body ends with the Claude Code line. Do not merge.
