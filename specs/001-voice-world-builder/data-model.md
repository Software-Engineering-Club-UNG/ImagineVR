# Data Model: Voice World Builder (Core Loop)

Nothing is stored permanently. All of this lives in memory, on the headset or the server,
for the length of one session.

## Session

One run of the experience.

| Field | Where | Notes |
|-------|-------|-------|
| `session_id` | both | Random, made by the app at launch. |
| `turns` | both | Ordered list of Turns. The terminal history is a rendering of it. |
| `world` | both | The current World. The headset holds the real one; the server holds a compact description. |
| `last_active` | server | Dropped after 30 minutes idle. |

## Turn (the spec's "Request")

One spoken or typed instruction and everything that came back.

| Field | Notes |
|-------|-------|
| `input_kind` | `audio` or `text`. |
| `transcript` | What the user said. |
| `statements` | The ImagineScript statements returned, in order. Each world statement is also a Build Step. |
| `outcome` | `world_changed`, `object_given`, `answered`, `not_understood`, `declined`, `failed`. |

**Turn states on the headset** (this is the terminal's state indicator, FR-003):

```text
ready ──hold talk──▶ listening ──release──▶ thinking ──first world statement──▶ building
  ▲                                            │                                   │
  │                                            ├── done (answered/not understood/declined) ──┐
  │                                            │                                   │         │
  └──────────────── done ◀─────────────────────┴───────────────────────────────────┘         │
  ▲                                                                                          │
  └──────────── problem (error, timeout) ── shown until next talk ◀──────────────────────────┘
```

- Releasing after less than 0.4 s of audio returns straight to `ready`.
- Holding talk during `thinking` or `building` records a new Turn; it is sent immediately and
  the server runs it after the current one.

## World

The user's surroundings. Exactly one exists. The dark room is the World with every part at
its zero value.

| Part | Holds | Zero value |
|------|-------|-----------|
| Sky | preset plus overrides (gradient, sun, stars, clouds) | all black |
| Light | intensity, colour, ambient | none |
| Fog | colour, density | black, none |
| Terrain | type, material, tint, height, seed | absent |
| Kits | up to `max_kits` kit instances, by `id` | none |
| Objects | spawned objects and scatter groups, by `id` | none |
| Held items | holdable objects made by `give`, by `id` | none |
| Ambience | loop and volume | silence |

Rules:

- IDs are unique across kits, objects, and held items. Reusing an ID replaces the old thing.
- Totals never exceed the catalog `limits`; excess is trimmed when a statement is applied.
- Nothing is placed inside the safe radius around the user except held items.
- Every change is a transition from the current value to the new one; there is no
  intermediate invalid state, so stopping after any statement leaves a valid World.

## Statement / Build Step

One line of ImagineScript. See [contracts/scene-language.md](contracts/scene-language.md).
World statements change the World and appear in the terminal as tool-call lines. Stream
statements change only the Turn's state.

## Catalog entry (the spec's "Library Item")

Something the build contains. See [contracts/catalog.md](contracts/catalog.md).

| Kind | Named by | In Unity |
|------|----------|----------|
| Sky preset | `sky.preset` | data asset of shader and light values |
| Terrain type | `terrain.type` | generator settings |
| Material | `terrain.material` | palette material variant |
| Kit | `kit.kit` | generator component plus the parts it uses |
| Asset | `spawn.asset`, `scatter.asset`, `give.asset` | prefab with a library entry (description, tags, aliases, size, holdable) |
| Primitive | same as asset | built-in shape |
| Ambience loop | `ambience.loop` | audio clip |

## Server-side world description

What the server keeps so the AI can handle follow-ups. It is a compaction of applied
statements, not a copy of the scene:

- the most recent `sky`, `light`, `fog`, `terrain`, and `ambience` statements;
- for each live `id`: the statement that created it, with any later `modify` folded in;
- `clear` and `remove` delete entries.

It is rendered into the AI's context as a short ImagineScript listing titled "current world".
