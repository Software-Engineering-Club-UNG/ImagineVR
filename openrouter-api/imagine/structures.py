"""Bounded, deterministic room plans. Unity builds geometry, not model-authored walls."""
from __future__ import annotations

import math

CAPABILITY = "structures-v1"
# What the second version of the client can build: rooms of any real size, windows, vaulted
# ceilings, rooms that share a wall, doorways with no door, a floor high above the ground, and
# many of one thing arranged inside a room. An older client is sent only what it understands.
WIDE = "structures-v2"
SIDES = ("north", "east", "south", "west")
THEMES = ("plain", "dungeon", "house", "sci_fi")
PROMPT_V1 = """
# Explorable interiors (structures-v1)
To be INSIDE a building of any kind (a dungeon, a house, a spaceship, a castle, a school, a shop, a bunker, a cave of rooms), use `structure`: connected rooms with real floors, ceilings, solid walls and doors that open. Never build its walls from plain shapes, `make` or `scatter`; the built-places rules are for what stands in the open and for what goes INSIDE the rooms.
{"op":"structure","id":"keep","theme":"dungeon","wall":"#8A8478","floor":"#4A4640","light":"#FFB060","brightness":0.15,"layout":"chain","enter":true,"rooms":[{"id":"entry","size":[6,8]},{"id":"hall","size":[8,10]},{"id":"treasury","size":[6,6]}]}
`theme` is the starting look: `dungeon` (dark brick, torches), `house` (wood and plaster), `sci_fi` (metal panels, cool lamps). Make it THIS place with `wall`, `floor` and `light` colours and `brightness` (0 pitch dark .. 1 bright): a candy shop is `house` with pink walls, a castle is `dungeon` with pale stone and more light.
Layouts: `chain` (up to 8 rooms in a row), `hub` (a central room and up to 4 off it). A room is `size` [width,depth], 4..16 m; `height` 2.5..6, default 3. Optional `seed`, `at` [x,0,z], `rot_y`. One structure at a time; a new one replaces it. `enter:true` puts the user in the first room: use it when they ask to be inside.
Fill the rooms: `spawn` with `room:"entry"` and `at:[x,y,z]` measured from the middle of that room's floor. Use room ids from CURRENT WORLD. Keep the middle of each room and its doorways clear. Give every room three or four things that say what it is for, something loose to pick up, and put what is alive or dangerous in the rooms beyond the first.
Doors: the user points an EMPTY hand at a door within 2 m and pulls the trigger (desktop: look at it, press F). Never tell them to grab or push one. On foot and while riding, the left stick moves and strafes relative to the head and the right stick turns in 45 degree steps.
Door ids are in the structure's `doors` list in CURRENT WORLD: {"op":"door","id":"keep_door_1_hall","state":"open"}; `state` is open, close or toggle. What the headset reports about rooms and doors is the truth. `remove` with the structure's id takes it away.
A new interior is: `clear`, `sky`, `structure` with `enter:true`, things in its rooms, an `ambience`, `say`. No terrain, weather or heavy fog inside. Say what is there to do.
"""
PROMPT = """
# Interiors: being INSIDE anything
When they ask to be inside something (a room, a building, a vehicle's cabin or cockpit, a ship, a cave, a tent, a station: anything with walls round it), use `structure`: rooms with real floors, ceilings, solid walls, windows and doors. Never build walls from plain shapes, `make` or `scatter`. Being inside is not `ride`: use `ride` only when they ask to drive, fly or steer it.
{"op":"structure","id":"place","theme":"plain","wall":"#D8DCE2","floor":"#3A3F48","light":"#FFF4E0","brightness":0.7,"windows":true,"enter":true,"rooms":[{"id":"front","size":[5,6]},{"id":"back","size":[4,10],"height":2.6}]}
- Shape it like the real thing. A room is `size` [width,depth] in metres, 2..30, at its true size: a cabin is narrow, a hall is long, a hangar is huge, a cupboard is tiny. `height` 2.2..12 (default 3), for the structure or for one room. One room is often right: do not add rooms the thing does not have.
- `layout`: `chain` (rooms in a line running ahead, up to 8) or `hub` (a middle room with up to 4 off it: ahead, right, behind, left). `halls:false` joins the rooms wall to wall; otherwise a short corridor runs between them. A room with `"door":false` has an open doorway. `exit:false` leaves no door to the outside.
- `windows`: `true` (every outside wall) or a list of sides, on the structure or on one room. Sides are `north` (ahead as they enter), `east` (their right), `south`, `west`. Through a window they see the sky and whatever you build outside, so build an outside worth seeing: a `sky`, things in the sky or on the ground around.
- `altitude` (0..80 m) lifts the floor into the air, for anything that flies, floats or stands high: then the windows look out over the clouds or down on the land.
- `ceiling`: `flat` or `vault` (an arched roof: a fuselage, a tunnel, a cellar, a tent, a chapel).
- Look: `theme` is only wall texture: `plain` (smooth), `dungeon` (rough brick, torches), `house` (wood and plaster), `sci_fi` (metal panels). The place itself is `wall`, `floor` and `light` colours and `brightness` (0 pitch dark .. 1 daylight). Choose them for THIS place every time.
- `enter:true` puts them in the first room looking ahead; `"enter":"<room id>"` in that room. Optional `at` [x,0,z], `rot_y`, `seed`. One structure at a time; a new one replaces it.

Furnishing a room. Positions are measured from the middle of that room's floor: x to the east, z to the north, y up.
- One thing: `spawn` with `room` and `at`. Against the north wall of a room 8 deep is z 3.7. Plain shapes make counters, panels, boards and screens.
- Many of one thing: one `scatter` with `room`, `count` and `arrange`:
  `rows`: a tidy grid across the floor with an aisle, all facing `face` (a side, default `north`): seats, desks, beds, pews, shelves, crates in a store.
  `walls`: lined up against the walls, facing in: lockers, cabinets, machines, bunks, torches, paintings.
  `loose` (default): spread over the floor, never overlapping.
  `at` shifts the whole arrangement, so two scatters with the same `count` pair up: desks, then chairs with `"at":[0,0,-0.7]`. `gap` is the space between neighbours in metres.
- What is alive goes in a room the same way (`scatter` with `room` and `behavior`). Never fill a room with a scatter that has no `room`.
- Give each room what says what it is for, something loose to pick up, and leave doorways clear.

Doors: the user points an EMPTY hand at a door within 2 m and pulls the trigger. Never tell them to grab or push one. Door ids are in the structure's `doors` list in CURRENT WORLD: {"op":"door","id":"place_door_1_back","state":"open"}; `state` is open, close or toggle. `remove` with the structure's id takes it away.
A new interior is: `clear`, `sky`, `structure`, what is in its rooms, what is seen outside its windows, an `ambience`, `say`. Terrain only if they will look out at it or walk out onto it.
"""


def _sides(value, free: list[str]) -> list[str]:
    """Which of a room's walls have windows: only walls with no doorway in them can."""
    if value is True or (isinstance(value, str) and value.strip().lower() in ("true", "all", "yes")):
        return list(free)
    if isinstance(value, str):
        value = [value]
    if not isinstance(value, (list, tuple)):
        return []
    words = {"ahead": "north", "front": "north", "forward": "north", "right": "east", "behind": "south", "back": "south", "left": "west"}
    wanted = [words.get(str(v).strip().lower(), str(v).strip().lower()) for v in value]
    return [side for side in free if side in wanted]


def normalize(raw: dict, frame=None, wide: bool = True) -> dict:
    # Import here to avoid a language <-> structures module cycle.
    from .language import color, name_of, num, pick, vec, flag

    identity = name_of(raw.get("id"), "interior")
    theme = pick(raw.get("theme"), THEMES if wide else THEMES[1:], "plain" if wide else "dungeon")
    layout = pick(raw.get("layout"), ("chain", "hub"), "chain")
    source = raw.get("rooms")
    source = source if isinstance(source, list) and source else [{"id": "entry"}, {"id": "hall"}, {"id": "chamber"}]
    low, high = (2, 30) if wide else (4, 16)
    low_h, high_h = (2.2, 12) if wide else (2.5, 6)
    height = num(raw.get("height"), low_h, high_h, 3)
    halls = not wide or raw.get("halls") is not False and str(raw.get("halls")).strip().lower() != "false"
    rooms, zones, doors = [], [], []
    doorless = set()
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
        tall = height
        if isinstance(raw_size, (list, tuple)) and len(raw_size) == 3:
            # normalized CURRENT WORLD uses width,height,depth
            tall = num(raw_size[1], low_h, high_h, height) if wide else height
            raw_size = [raw_size[0], raw_size[2]]
        elif wide:
            tall = num(value.get("height"), low_h, high_h, height)
        size = vec(raw_size, 2, low, high) or [6, 8]
        room = {"id": name, "center": [0, 0, 0], "size": [size[0], tall, size[1]], "openings": []}
        if wide and "door" in value and (value["door"] is False or str(value["door"]).strip().lower() in ("false", "none", "open")):
            doorless.add(name)
        rooms.append(room)

    vectors = {"north": (0, 1), "east": (1, 0), "south": (0, -1), "west": (-1, 0)}
    opposite = {"north": "south", "south": "north", "east": "west", "west": "east"}
    for i in range(1, len(rooms)):
        a, b = rooms[0 if layout == "hub" else i - 1], rooms[i]
        side = ("north", "east", "south", "west")[i - 1] if layout == "hub" else "north"
        dx, dz = vectors[side]
        axis = 0 if dx else 2
        gap = 4 if halls else 0.4   # a corridor, or only the thickness of the wall the two rooms share
        distance = (a["size"][axis] + b["size"][axis]) / 2 + gap
        b["center"] = [a["center"][0] + dx * distance, 0, a["center"][2] + dz * distance]
        door_id = f"{identity[:16]}_door_{i}_{b['id'][:12]}"
        a["openings"].append({"side": side})
        if b["id"] in doorless:
            b["door"] = False
            b["openings"].append({"side": opposite[side]})
        else:
            b["openings"].append({"side": opposite[side], "door": door_id})
            doors.append({"id": door_id, "room": b["id"], "side": opposite[side], "state": "closed"})
        centre = [a["center"][0] + dx * (a["size"][axis] / 2 + gap / 2), 0,
                  a["center"][2] + dz * (a["size"][axis] / 2 + gap / 2)]
        through = min(height, a["size"][1], b["size"][1])
        zones.append({"id": f"corridor_{i}", "center": centre, "size": [gap if dx else 2, through, 2 if dx else gap],
                      "openings": [{"side": side}, {"side": opposite[side]}]})
    # An exterior door at the end of a branch; never conflict with a connected opening. Something
    # high in the air has none unless it is asked for: the way out of a cockpit is not a drop.
    altitude = num(raw.get("altitude"), 0, 80, 0) if wide else 0
    wants_exit = raw.get("exit")
    has_exit = not wide or (flag(wants_exit) if altitude > 0 else wants_exit is not False and str(wants_exit).strip().lower() != "false")
    if has_exit:
        exterior_room = rooms[1] if layout == "hub" and len(rooms) > 1 else rooms[0]
        exterior_side = "north" if layout == "hub" and len(rooms) > 1 else "south"
        exit_id = identity[:35] + "_exit"
        exterior_room["openings"].append({"side": exterior_side, "door": exit_id})
        doors.append({"id": exit_id, "room": exterior_room["id"], "side": exterior_side, "state": "closed"})
    if wide:
        for room, value in zip(rooms, source):
            value = value if isinstance(value, dict) else {}
            free = [side for side in SIDES if all(o["side"] != side for o in room["openings"])]
            asked = value["windows"] if "windows" in value else raw.get("windows")
            sides = _sides(asked, free)
            if sides:
                room["windows"] = sides
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
    # `enter` may name the room to stand in; said plainly, it is the first.
    entry = name_of(raw.get("enter")) if isinstance(raw.get("enter"), str) else ""
    entry = entry if wide and entry in used else str(raw.get("entry") or "")
    entry = entry if entry in used else rooms[0]["id"]
    enter = flag(raw.get("enter")) or (wide and isinstance(raw.get("enter"), str) and name_of(raw.get("enter")) in used)
    extra = {}
    if wide:
        extra = {"halls": None if halls else False, "exit": None if has_exit else False, "altitude": altitude or None,
                 "ceiling": pick(raw.get("ceiling"), ("flat", "vault"))}
        if extra["ceiling"] == "flat":
            extra["ceiling"] = None
    return {"id": identity, "theme": theme, "layout": layout, "height": height,
            "seed": int(num(raw.get("seed"), 0, 99999, 0)), "at": at, "rot_y": yaw,
            "enter": bool(enter), "entry": entry, "rooms": rooms,
            "zones": zones, "doors": doors, **{k: v for k, v in {**look, **extra}.items() if v is not None}}


def room_of(structure: dict | None, name: str, hint: str = "") -> dict | None:
    """The room a statement means. Named a little wrongly it is the nearest there is, or one the statement's own id mentions, or the way in."""
    import difflib

    if not structure:
        return None
    rooms = {r["id"]: r for r in structure["rooms"]}
    if name in rooms:
        return rooms[name]
    close = difflib.get_close_matches(name, list(rooms), n=1, cutoff=0.6) if name else []
    if close:
        return rooms[close[0]]
    for identity in sorted(rooms, key=len, reverse=True):
        if hint and identity in hint:
            return rooms[identity]
    return rooms[structure["entry"]] if name else None
