# Explorable interiors

The `structures-v1` client capability enables enclosed, single-floor structures. The backend lays out rooms deterministically; Unity creates solid floors, segmented brick/panel walls, ceilings, door openings, and independently interactive doors. This is not a scatter of building props.

## Controls

| Action | VR, on foot | Desktop |
|---|---|---|
| Walk / strafe | Left joystick, relative to head direction | WASD / arrows |
| Turn | Right joystick left/right: 45° once, then release | Existing mouse look |
| Open / close door | Point an empty hand at a door within 2 m and press trigger | Look at door and press F |
| Ride / steer | Existing right-stick controls, unchanged | Existing riding controls |

Closed doors and walls block joystick movement. A door pauses while a player or physics object obstructs its swing. Room-scale tracking is never forcibly moved: a safety fade hides views through a wall when the real headset crosses it.

## Examples

Ask: **“Put me in a dark brick dungeon with connected rooms and doors I can open.”**

```json
{"op":"structure","id":"dungeon","theme":"dungeon","layout":"chain","enter":true,"rooms":[{"id":"entry","size":[6,8]},{"id":"hall","size":[8,10]},{"id":"treasury","size":[6,6]}]}
{"op":"spawn","asset":"table","room":"treasury","at":[2,0,1]}
{"op":"door","id":"dungeon_door_1_hall","state":"open"}
```

The model writes the compact room description. The backend sends a normalized `zones` plan, with room centres, dimensions, openings, and stable door IDs. Room-local furniture positions are relative to the room centre and floor, not the user's current position. Door commands accept `open`, `close`, and `toggle`; use the IDs in the current structure context. The client reports actual room and door states on subsequent turns.

## Defaults and limits

| Setting | First version |
|---|---|
| Themes | `dungeon` (brick/torches), `house` (wood/plaster), `sci_fi` (metal/lamps) |
| Layouts | `chain`: up to 8 rooms; `hub`: centre plus up to 4 branches |
| Rooms | Default 6 × 8 m; dimensions 4–16 m |
| Ceilings | Default 3 m; 2.5–6 m |
| Corridors | 2 m wide, 4 m long |
| Door opening | 1.2 × 2.2 m |
| Active structures | One; a new structure replaces it |
| Lighting | Per-room ambient and two bounded local lights; no real-time shadow maps |
| Terrain | Common floor above sampled terrain; foundation skirts, no landscape flattening |

Static geometry uses GameObjects/MeshRenderers and box colliders; there is no enclosing building-sized collider. Indoor volumes suppress outdoor sunlight for the shared solid-object shader. Lights are scoped to their room, so outdoor appearance is unchanged. Invalid builds leave the previous structure intact and report a note. Removing/clearing the floor relocates the player to terrain.

Multi-floor layouts, stairs, destruction, locks/keys, grabbed hinges, and physically simulated door pushing are not included. Lighting is stylized rather than a physically accurate simulation of illumination through doors. Optional `seed`, `at`, and `rot_y` control appearance/placement. `enter:true` relocates the player; otherwise placement leaves them outside/currently positioned.

## Verification and rollout

Run backend tests from the `openrouter-api` directory with `.venv/Scripts/python.exe -m pytest`. Run Unity PlayMode tests, including `StructureTests`, and built-player snapshots. `ImagineVR.EditorTools.Builder.BuildWindowsCheck` creates a Windows player for the existing snapshot runner. Then verify on the actual headset: four-direction walking, diagonal speed, one 45° turn per stick gesture, doors from both sides, blocked closing, ceilings, indoor darkness, safe entry/removal, and outdoors/riding regressions. Measure performance at the headset's configured refresh rate; headless tests cannot verify XR appearance or frame rate.

The normalized regression scenes are in `shared/interior-examples`: `dungeon.ndjson`, `house.ndjson`, and `sci_fi.ndjson`. Pass these to the snapshot runner's `-snapScripts` option to verify a real built player without an AI call.

Deploy the capability-aware backend first, then install the updated client. Older clients do not receive new structure operations. Local implementation does not update the live Vultr backend or installed APK until separately deployed/built.
