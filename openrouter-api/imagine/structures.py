"""Bounded, deterministic room plans. Unity builds geometry, not model-authored walls."""
from __future__ import annotations

import math

CAPABILITY = "structures-v1"
PROMPT = """
# Explorable interiors (structures-v1)
For being INSIDE a dungeon, house or spaceship, use `structure`, never a scatter of themed props.
{"op":"structure","id":"dungeon","theme":"dungeon","layout":"chain","enter":true,"rooms":[{"id":"entry","size":[6,8]},{"id":"hall","size":[8,10]},{"id":"treasury","size":[6,6]}]}
Themes: dungeon (dark brick and torches), house (wood/plaster), sci_fi (metal and lamps).
Layouts: chain (up to 8 rooms), hub (central room then up to 4 branches). Rooms have size [width,depth], 4..16m; height 2.5..6m defaults to 3. Optional seed, at [x,0,z], rot_y. One active structure; a new structure replaces it. enter:true safely puts the user in the first room, only use for requests to go inside.
The client provides real floors, ceilings, solid walls and interactive doors automatically. Do not build wall pieces with make/scatter. Add furniture with spawn using room:"entry" and at:[x,y,z] LOCAL to that room; y is above its floor. Use room ids from CURRENT WORLD. Keep furniture away from the room centre/doorways.
Controls: point an EMPTY hand at a door within 2 metres and press trigger (desktop: look at it and press F). Do not tell them to grab or physically push the door. On foot, left joystick walks/strafes, right joystick snaps 45 degrees; riding still uses existing right-stick steering.
Door ids are in the structure's normalized doors list. {"op":"door","id":"dungeon_door_1_hall","state":"open"}; states open/close/toggle. The user's live door/room state overrides remembered commands. Use remove with the structure id to remove it.
For a new dungeon: clear, sky, structure with enter:true, room-local furnishings, cave_drips ambience, say. Do not add terrain inside, heavy fog, weather, or bright global lighting. Say what actually exists, not that unspecified functionality works.
"""


def normalize(raw: dict, frame=None) -> dict:
    # Import here to avoid a language <-> structures module cycle.
    from .language import name_of, num, pick, vec, flag

    identity = name_of(raw.get("id"), "interior")
    theme = pick(raw.get("theme"), ("dungeon", "house", "sci_fi"), "dungeon")
    layout = pick(raw.get("layout"), ("chain", "hub"), "chain")
    source = raw.get("rooms")
    source = source if isinstance(source, list) and source else [{"id": "entry"}, {"id": "hall"}, {"id": "chamber"}]
    height = num(raw.get("height"), 2.5, 6, 3)
    rooms, zones, doors = [], [], []
    used = set()
    for i, value in enumerate(source[:5 if layout == "hub" else 8]):
        value = value if isinstance(value, dict) else {}
        name = name_of(value.get("id"), f"room_{i + 1}")
        suffix = 0
        while name in used:
            suffix += 1
            name = f"room_{i + 1}_{suffix}"
        used.add(name)
        raw_size = value.get("size")
        if isinstance(raw_size, (list, tuple)) and len(raw_size) == 3:
            raw_size = [raw_size[0], raw_size[2]]  # normalized CURRENT WORLD uses width,height,depth
        size = vec(raw_size, 2, 4, 16) or [6, 8]
        room = {"id": name, "center": [0, 0, 0], "size": [size[0], height, size[1]], "openings": []}
        rooms.append(room)

    vectors = {"north": (0, 1), "east": (1, 0), "south": (0, -1), "west": (-1, 0)}
    opposite = {"north": "south", "south": "north", "east": "west", "west": "east"}
    for i in range(1, len(rooms)):
        a, b = rooms[0 if layout == "hub" else i - 1], rooms[i]
        side = ("north", "east", "south", "west")[i - 1] if layout == "hub" else "north"
        dx, dz = vectors[side]
        axis = 0 if dx else 2
        gap = 4
        distance = (a["size"][axis] + b["size"][axis]) / 2 + gap
        b["center"] = [a["center"][0] + dx * distance, 0, a["center"][2] + dz * distance]
        door_id = f"{identity[:16]}_door_{i}_{b['id'][:12]}"
        a["openings"].append({"side": side})
        b["openings"].append({"side": opposite[side], "door": door_id})
        doors.append({"id": door_id, "room": b["id"], "side": opposite[side], "state": "closed"})
        centre = [a["center"][0] + dx * (a["size"][axis] / 2 + gap / 2), 0,
                  a["center"][2] + dz * (a["size"][axis] / 2 + gap / 2)]
        zones.append({"id": f"corridor_{i}", "center": centre, "size": [gap if dx else 2, height, 2 if dx else gap],
                      "openings": [{"side": side}, {"side": opposite[side]}]})
    # An exterior door at the end of a branch; never conflict with a connected opening.
    exterior_room = rooms[1] if layout == "hub" and len(rooms) > 1 else rooms[0]
    exterior_side = "north" if layout == "hub" and len(rooms) > 1 else "south"
    exit_id = identity[:35] + "_exit"
    exterior_room["openings"].append({"side": exterior_side, "door": exit_id})
    doors.append({"id": exit_id, "room": exterior_room["id"], "side": exterior_side, "state": "closed"})
    zones = rooms + zones
    at = vec(raw.get("at"), 3, -100, 100) or [0, 0, 0]
    at[1] = 0
    yaw = num(raw.get("rot_y"), -360, 360, 0)
    # Keep every corner within the walking world. Move the centre back if necessary.
    x0 = min(z["center"][0] - z["size"][0] / 2 for z in zones)
    x1 = max(z["center"][0] + z["size"][0] / 2 for z in zones)
    z0 = min(z["center"][2] - z["size"][2] / 2 for z in zones)
    z1 = max(z["center"][2] + z["size"][2] / 2 for z in zones)
    # Recenter long chains while keeping the entry's local coordinate explicit.
    offset = [(x0 + x1) / 2, (z0 + z1) / 2]
    for z in zones:
        z["center"][0] -= offset[0]
        z["center"][2] -= offset[1]
    radius = math.hypot((x1 - x0) / 2, (z1 - z0) / 2) + 1
    wx, wz = at[0], at[2]
    if frame:
        fc, fs = math.cos(math.radians(frame[2])), math.sin(math.radians(frame[2]))
        wx, wz = frame[0] + wx * fc + wz * fs, frame[1] - wx * fs + wz * fc
    distance = math.hypot(wx, wz)
    allowed = max(0, 110 - radius)
    if distance > allowed:
        wx, wz = wx * allowed / distance, wz * allowed / distance
        if frame:
            dx, dz = wx - frame[0], wz - frame[1]
            at = [dx * fc - dz * fs, 0, dx * fs + dz * fc]
        else:
            at = [wx, 0, wz]
    return {"id": identity, "theme": theme, "layout": layout, "height": height,
            "seed": int(num(raw.get("seed"), 0, 99999, 0)), "at": at, "rot_y": yaw,
            "enter": flag(raw.get("enter")), "entry": rooms[0]["id"], "rooms": rooms,
            "zones": zones, "doors": doors}
