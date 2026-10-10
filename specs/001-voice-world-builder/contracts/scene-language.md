# Contract: ImagineScript v1 (the scene language)

ImagineScript is how the server tells the headset what to do. The AI writes it, the server
validates it, and the headset applies it. It is the only thing that changes the world.

## Format

- Newline-delimited JSON (NDJSON), UTF-8. One statement per line. No line spans statements.
- Every statement is a JSON object with a string field `op`.
- Statements are applied in order, each as soon as its line is complete.
- A parser MUST skip, without failing, any statement whose `op` it does not know and any
  field it does not know. This is what lets later versions add operations.

## Conventions

- Units are metres. Model-authored positions put the user at the origin, facing +Z;
  +Y is up and +X is to their right. The server supplies the optional placement `frame`
  described below. Ground-relative height is y = 0 at the terrain surface.
- Colours are `"#RRGGBB"` strings.
- `id` is a short name chosen by the author of the statement (for example `"trees1"`) so
  later statements can refer to what this one created. IDs are unique within a world.
- `t` is an optional transition time in seconds. Default 1.5. Clamped to 0.2–6.
- Every catalog reference (`preset`, `type`, `kit`, `asset`, `material`, `loop`) is an ID from
  `shared/catalog.json`. Name matching may select a catalog entry, but multiword asset
  names must match their last word (for example, spider_man must not become spider_web).
  Unknown spawn/scatter assets trigger a second model call to compose a replacement from
  catalog shapes. If no replacement is produced, a labelled shape is emitted with
  `stand_in: true`; world memory retains that marker so it does not claim a real asset exists.
- Numbers outside a stated range are clamped. Counts above catalog limits are reduced.

## World statements

| `op` | Fields | Effect |
|------|--------|--------|
| `say` | `text`, `audio?` | Assistant message shown in the terminal. If `audio` (a URL) is present, it is played. |
| `clear` | `keep?` (array of `"sky"`, `"terrain"`, `"ambience"`, `"weather"`), `everything?` (bool), `t?` | Clears scenery and kits; ordinary clear preserves given items. `everything: true` also removes given/held items and paint, resets the environment, ride and player size, and returns to the dark/passthrough room, ignoring `keep`. |
| `sky` | `preset?`, `top?`, `horizon?`, `bottom?`, `sun_elevation?` (-90–90), `sun_azimuth?` (0–360), `sun_color?`, `stars?` (0–1), `clouds?` (0–1), `t?` | Sets the sky. A preset also sets matching light, ambient, and fog. Individual fields override the preset. |
| `light` | `intensity?` (0–2), `color?`, `ambient?` (colour), `t?` | Overrides light after `sky`. |
| `fog` | `color?`, `density?` (0–1), `t?` | Sets atmosphere. 0 is none, 1 is thick. |
| `terrain` | `type`, `material?`, `tint?`, `height?` (0–1), `seed?`, `t?` | Replaces the ground. The 3 m disc under the user stays flat. |
| `kit` | `id`, `kit`, `density?` (0–1), `size?` (0–1), `seed?`, `params?` (object, kit-specific), `t?` | Builds a whole themed set. |
| `spawn` | `id`, `asset`, `at?` (`[x,y,z]`) or `place?` (`"front"`, `"left"`, `"right"`, `"behind"`, `"center"`), `distance?`, `scale?` (0.1–10), `rot_y?`, `tint?`, `t?` | Places one object. With `place`, it is positioned relative to the user at `distance` metres (default 4). |
| `scatter` | `id`, `asset`, `count`, `area?` (`"around"`, `"front"`, `"ring"`, `"edges"`, `"everywhere"`), `radius?` (`[min,max]`), `scale?` (`[min,max]`), `tint?`, `seed?`, `t?` | Places many copies, rested on the terrain, never inside the user's safe disc. |
| `give` | `id`, `asset`, `tint?` | Makes a holdable object appear within reach in front of the user. If the asset is not holdable, the nearest holdable one is used. |
| `modify` | `id`, any of `scale`, `tint`, `count`, `density`, `t?` | Changes something made earlier by `spawn`, `scatter`, `kit`, or `give`. |
| `remove` | `id`, `t?` | Transitions out something made earlier. Unknown `id` is ignored. |
| `ambience` | `loop`, `volume?` (0–1), `t?` | Cross-fades to a baked ambient sound loop. |

## Placement frames and fallback markers

The server adds `frame: [x, z, yaw]` to `spawn`, `scatter`, `goal`, and `ride` with a
`path` when the request supplies a nonzero `X-User-Pose`. These are the user's horizontal
world coordinates in metres and heading in degrees. The client rotates local positions
and paths about +Y by yaw, then translates by `[x, 0, z]`. Spawn rotations receive the
same yaw; heights remain relative to the terrain. Missing `frame` is the identity
transform, preserving existing demo scripts. `give` uses the current head directly;
terrain and kits retain their world layout.

For example, a user at x=30 facing +X receives an object four metres ahead at x=34:

```json
{"op":"spawn","id":"car","asset":"car","at":[0,0,4],"frame":[30,0,90]}
```

`frame` is server-owned: model-provided frame values are discarded. Remembered `at`
positions and paths are re-expressed relative to the user's pose on the next turn. A
`clear` homes the client, so subsequent statements use the identity frame.

`stand_in: true` is also server-owned and marks an unavailable asset rendered as a
labelled fallback shape. It is retained in world state. A sculpted replacement instead
uses existing primitive assets and dotted part IDs such as `batman.body` and
`batman.cape`; these share one root object for spawn limits and group removal. This
composes baked shapes; runtime generation of new meshes remains deferred.

An ordinary `clear` preserves given items. If it leaves no scenery or ground, and no
replacement sky is supplied, the client fades to the dark/passthrough room at turn end.
`clear` with `everything: true` clears the whole world, including given items, even when
`keep` is supplied. It is a scene operation; the controller's full reset additionally
cancels pending work and starts a fresh conversation and terminal.

## Stream statements

These carry the state of the turn, not the world. They are written by the server, never by
the AI.

| `op` | Fields | Meaning |
|------|--------|---------|
| `transcript` | `text` | What the user was heard to say. Shown as the prompt line. Always first when the request was audio. |
| `status` | `state` (`"thinking"`, `"building"`) | Drives the terminal's state indicator. |
| `note` | `text` | A dim terminal line from the server, for example "used pine tree in place of redwood". |
| `error` | `message`, `retryable` (bool) | The turn failed. `message` is plain language for the terminal. The world keeps what was already applied. |
| `done` | `outcome` (`"world_changed"`, `"object_given"`, `"answered"`, `"not_understood"`, `"declined"`, `"failed"`) | Always the last line of a turn. |

## Reserved for later (not implemented in v1)

A v1 parser skips these like any unknown operation. They are named here so adding them later
is not a breaking change.

| `op` | Intended fields | Purpose |
|------|-----------------|---------|
| `skybox_image` | `url`, `t?` | AI-generated 360° panorama layered behind the procedural sky. |
| `mesh` | `id`, `url`, placement fields as `spawn` | A generated 3D model fetched at runtime. |
| `sketch` | `id`, `image` | Turn a user drawing into an object. |

## How the terminal shows statements

Each world statement appears as one tool-call line when it is applied, for example:

```text
> I want to see a golf course
● sky(preset: clear_noon)
● terrain(type: rolling, material: grass)
● kit(golf_course, density: 0.6)
● scatter(pine_tree × 40, area: edges)
  Welcome to the first tee.
```

`say` text is shown as assistant prose. `note` is shown dimmed. `error` is shown in the
problem style.

## Example turn

```json
{"op":"transcript","text":"I want to see a golf course"}
{"op":"status","state":"thinking"}
{"op":"status","state":"building"}
{"op":"sky","preset":"clear_noon","t":3}
{"op":"terrain","type":"rolling","material":"grass","height":0.3,"t":3}
{"op":"kit","id":"course","kit":"golf_course","density":0.6}
{"op":"scatter","id":"trees1","asset":"pine_tree","count":40,"area":"edges"}
{"op":"ambience","loop":"birds_breeze"}
{"op":"say","text":"Welcome to the first tee."}
{"op":"done","outcome":"world_changed"}
```

Follow-up "make it sunset" in the same session:

```json
{"op":"transcript","text":"make it sunset"}
{"op":"status","state":"building"}
{"op":"sky","preset":"golden_hour","t":4}
{"op":"say","text":"Golden hour it is."}
{"op":"done","outcome":"world_changed"}
```

## Versioning

### Current Unity additions (2026-10-10)

These additive fields and operations remain ImagineScript v1. The deployed
backend lives in `openrouter-api/`; it follows Israel-Jauregui's `online-ready`
implementation through `e5f8c5a`, including the exported catalog and prompt.

- `think`: `{"op":"think","text":"A maze at sunset, then a flag at the exit."}`
  is a brief display-only plan line, limited to 220 characters. Unity shows it
  dimly in the terminal; it does not enter world state or conversational replies,
  and does not trigger `building` or a `world_changed` outcome by itself.
- `spawn` and `scatter` accept `solid: true` for scenery that blocks movement.
  This differs from `physics: true`, which creates loose, movable props.
- `spawn` accepts `path: [[x,height,z], ...]`, `speed` (0.1–40 m/s), `loop`, and
  `start` (0–1). `ride` accepts `path` and `loop` alongside its existing speed
  multiplier (0.2–3). Routes retain 2–24 valid points; heights are above ground.
  Too-short/invalid paths are dropped without dropping the remaining statement.
- `goal`: `{"op":"goal","id":"finish","at":[0,0,40],"radius":3,"for":"user","text":"You made it"}`.
  Position is required; radius is bounded to 0.3–20 m, message to 80 characters.
  `for` defaults to `user`; Unity also accepts asset-name matching or `any`.
  Goals receive IDs, enter world state, and can be removed or cleared.
- `player`: `{"op":"player","scale":0.1}` changes player size (0.05–20).
  It enters global world state for follow-ups; setting scale to 1 resets it.
- `modify.scale` remains uniform: a three-component vector is converted to its
  average. `sky.t` now permits 0.2–120 seconds for gradual sunsets.
- The generated catalog now includes the `maze` kit. Client and backend must use
  the same exported catalog version; the maze/coaster examples are regression
  fixtures, not hand-edited catalog entries.

- The language version is an integer, currently `1`, reported by the server's health check
  and embedded in the catalog.
- Adding an operation or an optional field does not change the version. Changing the meaning
  of an existing field or removing one does.

## Runtime rules (capability `runtime-rules-v1`)

`rule` installs or replaces an ID with a selector (instance/group/asset/tag), event,
optional filters/conditions, and up to eight actions. `rule_remove` removes its ID.
The shared contract is `shared/runtime-rules.json`; the full reference and examples
are in `RUNTIME_EFFECTS_AND_TRIGGERS.md` and `shared/rule-examples/`.

Rules execute bounded data-only actions; no generated code is evaluated. Position/path
coordinates are authored relative to the current user and transformed by the server
once on installation; offsets and vectors rotate with the user's heading. The stored
and transmitted rules use world coordinates. Rule heights are absolute world heights.

Ordinary clear drops rules unless `keep` includes `rules`; full clear ignores that
exception. Full reset also cancels delayed actions/timers/effects, clears scores/state,
removes held items and registry entries, and disables automatic player movement.
