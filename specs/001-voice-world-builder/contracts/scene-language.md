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

- Units are metres. The user stands at the origin, facing +Z; +Y is up; +X is to their right.
  Ground level under the user is y = 0.
- Colours are `"#RRGGBB"` strings.
- `id` is a short name chosen by the author of the statement (for example `"trees1"`) so
  later statements can refer to what this one created. IDs are unique within a world.
- `t` is an optional transition time in seconds. Default 1.5. Clamped to 0.2–6.
- Every catalog reference (`preset`, `type`, `kit`, `asset`, `material`, `loop`) is an ID from
  `shared/catalog.json`. An unknown ID is replaced by the nearest catalog entry; failing
  that, by a simple labelled shape. It is never an error.
- Numbers outside a stated range are clamped. Counts above catalog limits are reduced.

## World statements

| `op` | Fields | Effect |
|------|--------|--------|
| `say` | `text`, `audio?` | Assistant message shown in the terminal. If `audio` (a URL) is present, it is played. |
| `clear` | `keep?` (array of `"sky"`, `"terrain"`, `"ambience"`), `t?` | Transitions out all objects and kits, and everything else not listed in `keep`. Used when the user asks for a different place. |
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

- The language version is an integer, currently `1`, reported by the server's health check
  and embedded in the catalog.
- Adding an operation or an optional field does not change the version. Changing the meaning
  of an existing field or removing one does.
