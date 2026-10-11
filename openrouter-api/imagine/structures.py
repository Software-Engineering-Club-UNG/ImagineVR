"""Bounded, deterministic room plans. Unity builds geometry, not model-authored walls."""
from __future__ import annotations

import math

CAPABILITY = "structures-v1"
PROMPT = """
# Explorable interiors (structures-v1)
To be INSIDE a building of any kind (a dungeon, a house, a spaceship, a castle, a school, a shop, a bunker, a cave of rooms), use `structure`: connected rooms with real floors, ceilings, solid walls and doors that open. Never build its walls from plain shapes, `make` or `scatter`; the built-places rules are for what stands in the open and for what goes INSIDE the rooms.
{"op":"structure","id":"keep","theme":"dungeon","wall":"#8A8478","floor":"#4A4640","light":"#FFB060","brightness":0.15,"layout":"chain","enter":true,"rooms":[{"id":"entry","size":[6,8]},{"id":"hall","size":[8,10]},{"id":"treasury","size":[6,6]}]}
`theme` is the starting look: `dungeon` (dark brick, torches), `house` (wood and plaster), `sci_fi` (metal panels, cool lamps). Make it THIS place with `wall`, `floor` and `light` colours and `brightness` (0 pitch dark .. 1 bright): a candy shop is `house` with pink walls, a castle is `dungeon` with pale stone and more light.
Layouts: `chain` (up to 8 rooms in a row), `hub` (a central room and up to 4 off it). A room is `size` [width,depth], 4..16 m; `height` 2.5..6, default 3. Optional `seed`, `at` [x,0,z], `rot_y`. One structure at a time; a new one replaces it. `enter:true` puts the user in the first room: use it when they ask to be inside.
Fill the rooms: `spawn` with `room:"entry"` and `at:[x,y,z]` measured from the middle of that room's floor. Use room ids from CURRENT WORLD. Keep the middle of each room and its doorways clear. Give every room three or four things that say what it is for, something loose to pick up, and put what is alive or dangerous in the rooms beyond the first.
Doors: the user points an EMPTY hand at a door within 2 m and pulls the trigger (desktop: look at it, press F). Never tell them to grab or push one. On foot the left stick walks and strafes, the right stick turns in 45 degree steps; riding steers with the right stick as before.
Door ids are in the structure's `doors` list in CURRENT WORLD: {"op":"door","id":"keep_door_1_hall","state":"open"}; `state` is open, close or toggle. What the headset reports about rooms and doors is the truth. `remove` with the structure's id takes it away.
A new interior is: `clear`, `sky`, `structure` with `enter:true`, things in its rooms, an `ambience`, `say`. No terrain, weather or heavy fog inside. Say what is there to do.
"""


def normalize(raw: dict, frame=None) -> dict:
    # Import here to avoid a language <-> structures module cycle.
    from .language import color, name_of, num, pick, vec, flag

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
    # A theme is a starting look. Any of these, given, replaces its part of it: the interior of
    # anything at all is a theme plus its own colours and light.
    look = {key: color(raw.get(key)) for key in ("wall", "floor", "light")}
    look["brightness"] = num(raw.get("brightness"), 0, 1)
    return {"id": identity, "theme": theme, "layout": layout, "height": height,
            "seed": int(num(raw.get("seed"), 0, 99999, 0)), "at": at, "rot_y": yaw,
            "enter": flag(raw.get("enter")), "entry": rooms[0]["id"], "rooms": rooms,
            "zones": zones, "doors": doors, **{k: v for k, v in look.items() if v is not None}}
