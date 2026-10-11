# Explorable interiors

The `structures-v1`/`structures-v2` client capabilities enable enclosed, single-floor structures: the inside of anything with walls round it (a house, a cockpit, a classroom, a hangar). The backend lays out rooms deterministically; Unity creates solid floors, segmented brick/panel walls, ceilings, door openings, and independently interactive doors. This is not a scatter of building props.

## Controls

| Action | VR, on foot | Desktop |
|---|---|---|
| Walk / strafe | Left joystick, relative to head direction | WASD / arrows |
| Turn | Right joystick left/right: 45° once, then release | Existing mouse look |
| Open / close door | Point an empty hand at a door within 2 m and press trigger | Look at door and press F |
| Ride / steer | Existing right-stick controls, unchanged | Existing riding controls |

Closed doors and walls block joystick movement. The walking capsule ignores the user's own hands and every loose prop, so nothing held or thrown can block or shove them. A door pauses while a player or physics object obstructs its swing. Room-scale tracking is never forcibly moved: a safety fade hides views through a wall when the real headset crosses it.

## Examples

Ask: **“Put me in a dark brick dungeon with connected rooms and doors I can open.”**

```json
{"op":"structure","id":"dungeon","theme":"dungeon","layout":"chain","enter":true,"rooms":[{"id":"entry","size":[6,8]},{"id":"hall","size":[8,10]},{"id":"treasury","size":[6,6]}]}
{"op":"spawn","asset":"table","room":"treasury","at":[2,0,1]}
{"op":"door","id":"dungeon_door_1_hall","state":"open"}
```

The model writes the compact room description. The backend sends a normalized `zones` plan, with room centres, dimensions, openings, and stable door IDs. Room-local furniture positions are relative to the room centre and floor, not the user's current position. Door commands accept `open`, `close`, and `toggle`; use the IDs in the current structure context. The client reports actual room and door states on subsequent turns.

## Defaults and limits

A client that sends `structures-v2` gets the general form below; one that sends only `structures-v1` is sent the first version (4–16 m rooms, corridors, no windows).

| Setting | Now |
|---|---|
| Themes | Wall texture only: `plain` (smooth, the default), `dungeon` (brick, torches), `house`, `sci_fi` |
| Look | `wall`, `floor` and `light` colours and `brightness` 0..1 make it the place it is |
| Layouts | `chain`: up to 8 rooms in a line; `hub`: centre plus up to 4 branches |
| Rooms | The size of the real thing: 2–30 m, 2.2–12 m high, per room; default 6 × 8 × 3 |
| Joining | A 4 m corridor, or `halls:false` for rooms wall to wall; `"door":false` on a room is an open doorway; `exit:false` leaves no door outside |
| Windows | `windows:true` or a list of sides, on the structure or a room: openings with a sill, head and frame. The collider is the whole wall |
| Ceiling | `flat` or `vault` (an arch along the line of rooms) |
| Altitude | 0–80 m above the ground, for anything that flies or stands high; no way out unless `exit:true` |
| Entering | `enter:true` (first room) or `"enter":"<room id>"` |
| Active structures | One; a new structure replaces it |
| Lighting | Per-room ambient (more with windows) and two bounded lights whose reach grows with the room |

### Furnishing

`spawn` with `room` and `at` places one thing, measured from the middle of that room's floor (x east, z north). `scatter` with `room` places many: `arrange` is `rows` (a grid with an aisle, facing `face`), `walls` (backs to the walls, clear of doorways) or `loose` (spread out, never overlapping). `at` shifts a whole arrangement, so two scatters of the same count pair up (desks and their chairs). A scatter with a short `radius` and no room, asked for by someone standing indoors, lands in the room they are in; what grows on the ground does not grow through an interior's floor, and nothing scattered outdoors lands inside one.

Static geometry uses GameObjects/MeshRenderers and box colliders; there is no enclosing building-sized collider. Indoor volumes suppress outdoor sunlight for the shared solid-object shader. Nothing is refused: sizes out of range are clamped, and a room or door named wrongly is the nearest one. Removing/clearing the floor relocates the player to terrain.

Multi-floor layouts, stairs, destruction, locks/keys, grabbed hinges, and physically simulated door pushing are not included.

## Verification and rollout

Run backend tests from the `openrouter-api` directory with `.venv/Scripts/python.exe -m pytest`. Run Unity PlayMode tests, including `StructureTests`, and built-player snapshots. `ImagineVR.EditorTools.Builder.BuildWindowsCheck` creates a Windows player for the existing snapshot runner. Then verify on the actual headset: four-direction walking, diagonal speed, one 45° turn per stick gesture, doors from both sides, blocked closing, ceilings, indoor darkness, safe entry/removal, and outdoors/riding regressions. Measure performance at the headset's configured refresh rate; headless tests cannot verify XR appearance or frame rate.

The normalized regression scenes are in `shared/interior-examples`: `dungeon.ndjson`, `house.ndjson`, and `sci_fi.ndjson`. Pass these to the snapshot runner's `-snapScripts` option to verify a real built player without an AI call.

Deploy the capability-aware backend first, then install the updated client. Older clients do not receive new structure operations. Local implementation does not update the live Vultr backend or installed APK until separately deployed/built.
