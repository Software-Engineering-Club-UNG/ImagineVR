# Contract: Catalog (`shared/catalog.json`)

The catalog is the vocabulary of ImagineScript: everything the build contains that a
statement may name. It is exported from the Unity project by an editor command, committed,
embedded in the app, and loaded by the server to build the AI's instructions.

It is generated. Do not edit it by hand; change the library in Unity and re-export.

## Shape

```json
{
  "language_version": 1,
  "catalog_version": "2026-10-10.1",
  "limits": {
    "max_spawned": 60,
    "max_scatter_count": 300,
    "max_scatter_total": 1500,
    "max_kits": 3,
    "safe_radius_m": 3,
    "world_radius_m": 120
  },
  "sky_presets": [
    { "id": "golden_hour", "desc": "Low warm sun, orange horizon, long light", "tags": ["sunset", "dusk", "warm"] }
  ],
  "terrain_types": [
    { "id": "rolling", "desc": "Gentle grassy hills", "tags": ["hills", "field"] }
  ],
  "materials": [
    { "id": "grass", "desc": "Short green grass", "tags": ["lawn", "turf"] }
  ],
  "kits": [
    {
      "id": "golf_course",
      "desc": "Fairway, green with flag, bunkers, tree line",
      "tags": ["golf", "sport"],
      "params": { "holes": "1-3, number of greens" }
    }
  ],
  "assets": [
    {
      "id": "pine_tree",
      "desc": "Tall low-poly pine",
      "tags": ["tree", "forest", "evergreen"],
      "aliases": ["fir", "conifer", "redwood"],
      "size_m": 6,
      "holdable": false
    },
    {
      "id": "golf_club",
      "desc": "Iron golf club",
      "tags": ["sport", "golf"],
      "aliases": ["club", "iron", "putter", "driver"],
      "size_m": 1,
      "holdable": true
    }
  ],
  "primitives": ["cube", "sphere", "cylinder", "cone", "plane"],
  "ambience": [
    { "id": "birds_breeze", "desc": "Daytime birdsong and light wind", "tags": ["outdoor", "day", "calm"] }
  ]
}
```

## Rules

- Every `id` is lowercase snake_case and unique within its list.
- `desc` is one short line; it is the only thing the AI reads to decide what an entry is.
- `aliases` are other words a user might say; they drive nearest-match fallback.
- `catalog_version` changes on every export. The app sends it with each turn and the server
  logs a warning when it differs from the file it loaded.
- `primitives` are always available as `asset` values and are the last-resort stand-ins.
- `limits` are enforced by both the server and the headset.

## Nearest-match fallback

When a statement names an ID that is not in the relevant list:

1. Exact match on an `aliases` entry.
2. Most shared words between the unknown ID and each entry's `id`, `tags`, and `aliases`.
3. For assets only: a primitive, tinted, with the requested name shown as a floating label.

The server emits a `note` statement whenever it substitutes.
