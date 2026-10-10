"""ImagineScript v1: validation, clamping, and catalog substitution.

Every line the model writes passes through `Validator.check` before it is sent to the
headset. A bad field is dropped, a bad statement is dropped, an unknown catalog ID is
replaced by the nearest entry. Nothing here raises on model output.
"""

from __future__ import annotations

import json
import math
import re

from .catalog import Catalog

WORLD_OPS = (
    "think", "say", "clear", "sky", "light", "fog", "terrain", "weather", "physics",
    "kit", "spawn", "scatter", "give", "ride", "goal", "player", "modify", "remove", "ambience",
)
STREAM_OPS = ("transcript", "status", "note", "error", "done")

PLACES = ("front", "left", "right", "behind", "center", "above")
AREAS = ("around", "front", "behind", "left", "right", "ring", "edges", "everywhere", "sky")
KEEP = ("sky", "terrain", "ambience", "weather")
BEHAVIORS = ("chase", "wander", "flee", "still")
NO_RIDE = ("none", "off", "stop", "exit", "out", "walk", "nothing", "foot", "on_foot")

NAMED_COLORS = {
    "white": "#FFFFFF", "black": "#000000", "red": "#E5484D", "green": "#46A758", "blue": "#3E7BFA",
    "yellow": "#F5D90A", "orange": "#F76B15", "purple": "#8E4EC6", "pink": "#E93D82", "brown": "#8B5A2B",
    "gray": "#8B8D98", "grey": "#8B8D98", "cyan": "#00C2D7", "teal": "#12A594", "magenta": "#D831B0",
    "gold": "#FFC53D", "silver": "#C1C8CD", "navy": "#1F2D5C", "lime": "#BDEE63", "maroon": "#6E2B2B",
    "turquoise": "#30D5C8", "violet": "#7C66DC", "indigo": "#3E63DD", "beige": "#E8DCC4", "tan": "#C8A97E",
    "crimson": "#E5484D", "sky": "#7CE2FE", "mint": "#86EAD4", "peach": "#FFB98A", "lavender": "#C4B8F3",
    "coral": "#FF7F6A", "olive": "#7A7A2A", "cream": "#FFF6DD", "charcoal": "#2B2D31", "ivory": "#FFFFF0",
}
_HEX6 = re.compile(r"^#[0-9a-fA-F]{6}$")
_HEX3 = re.compile(r"^#[0-9a-fA-F]{3}$")
_ID = re.compile(r"[^a-z0-9_.]+")


def to_color(value) -> str | None:
    if not isinstance(value, str):
        return None
    v = value.strip()
    if _HEX6.match(v):
        return v.upper()
    if _HEX3.match(v):
        return ("#" + "".join(c * 2 for c in v[1:])).upper()
    if _HEX6.match("#" + v):
        return ("#" + v).upper()
    return NAMED_COLORS.get(v.lower().replace(" ", ""))


def to_num(value, lo: float, hi: float) -> float | None:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        if isinstance(value, str):
            try:
                value = float(value)
            except ValueError:
                return None
        else:
            return None
    if value != value:  # NaN
        return None
    return round(max(lo, min(hi, float(value))), 4)


def to_id(value) -> str | None:
    if not isinstance(value, (str, int)):
        return None
    cleaned = _ID.sub("_", str(value).strip().lower()).strip("_.")
    return cleaned[:40] or None


def _num(lo, hi):
    return lambda v, _c: to_num(v, lo, hi)


def _int(lo, hi):
    def f(v, _c):
        n = to_num(v, lo, hi)
        return None if n is None else int(round(n))
    return f


def _color(v, _c):
    return to_color(v)


def _text(limit):
    return lambda v, _c: (str(v).strip()[:limit] or None) if isinstance(v, (str, int, float)) else None


def _bool(v, _c):
    if isinstance(v, bool):
        return v
    if isinstance(v, str) and v.lower() in ("true", "false"):
        return v.lower() == "true"
    return None


def _enum(options):
    return lambda v, _c: v.lower() if isinstance(v, str) and v.lower() in options else None


def _vec3(v, _c):
    if isinstance(v, (list, tuple)) and len(v) == 3:
        parts = [to_num(v[0], -400, 400), to_num(v[1], -50, 300), to_num(v[2], -400, 400)]
        if None not in parts:
            return parts
    return None


def _scale(v, _c):
    if isinstance(v, (list, tuple)) and len(v) == 3:
        parts = [to_num(p, 0.02, 200) for p in v]
        return parts if None not in parts else None
    return to_num(v, 0.05, 60)


def _rot(v, _c):
    if isinstance(v, (list, tuple)) and len(v) == 3:
        parts = [to_num(p, -720, 720) for p in v]
        return parts if None not in parts else None
    return None


def _path(v, _c):
    """A route: 2 to 24 points, each [x, height above ground, z]."""
    if not isinstance(v, (list, tuple)):
        return None
    points = [p for p in (_vec3(item, _c) for item in v[:24]) if p is not None]
    return points if len(points) >= 2 else None


def _scale1(v, _c):
    """One number; a [x, y, z] is averaged, since a whole group grows by one factor."""
    if isinstance(v, (list, tuple)) and len(v) == 3:
        parts = [to_num(p, 0.05, 60) for p in v]
        return round(sum(parts) / 3, 4) if None not in parts else None
    return to_num(v, 0.05, 60)


def _range2(lo, hi):
    def f(v, _c):
        if isinstance(v, (int, float)) and not isinstance(v, bool):
            n = to_num(v, lo, hi)
            return [n, n]
        if isinstance(v, (list, tuple)) and len(v) == 2:
            a, b = to_num(v[0], lo, hi), to_num(v[1], lo, hi)
            if a is not None and b is not None:
                return [min(a, b), max(a, b)]
        return None
    return f


def _keep(v, _c):
    if isinstance(v, str):
        v = [v]
    if isinstance(v, list):
        kept = [k.lower() for k in v if isinstance(k, str) and k.lower() in KEEP]
        return kept or None
    return None


def _obj(v, _c):
    if isinstance(v, dict):
        clean = {}
        for k, val in list(v.items())[:8]:
            if isinstance(val, (int, float, str, bool)):
                clean[str(k)[:24]] = val if not isinstance(val, str) else val[:40]
        return clean or None
    return None


def _motion(v, catalog: Catalog):
    if isinstance(v, str) and (not catalog.motions or v.lower() in catalog.motions):
        return v.lower()
    return None


T = _num(0.2, 6)

# op -> {field: converter}. Catalog references are handled separately in check().
FIELDS: dict[str, dict] = {
    "think": {"text": _text(220)},
    "say": {"text": _text(400)},
    "clear": {"keep": _keep, "everything": _bool, "t": T},
    "sky": {
        "top": _color, "horizon": _color, "bottom": _color,
        "sun_elevation": _num(-90, 90), "sun_azimuth": _num(0, 360), "sun_color": _color,
        "sun_size": _num(0, 1), "stars": _num(0, 1), "clouds": _num(0, 1), "cloud_color": _color,
        "moon": _num(0, 1), "aurora": _num(0, 1), "nebula": _num(0, 1), "t": _num(0.2, 120),
    },
    "light": {"intensity": _num(0, 2), "color": _color, "ambient": _color, "t": T},
    "fog": {"color": _color, "density": _num(0, 1), "t": T},
    "terrain": {
        "tint": _color, "height": _num(0, 1), "seed": _int(0, 99999),
        "grid": _num(0, 1), "grid_color": _color, "water_color": _color, "t": T,
    },
    "weather": {"intensity": _num(0, 1), "color": _color, "t": T},
    "physics": {"gravity": _num(-0.5, 2)},
    "kit": {
        "density": _num(0, 1), "size": _num(0, 1), "seed": _int(0, 99999),
        "tint": _color, "params": _obj, "t": T,
    },
    "spawn": {
        "at": _vec3, "place": _enum(PLACES), "distance": _num(0, 400), "height": _num(-20, 200),
        "scale": _scale, "rot_y": _num(-720, 720), "rot": _rot, "tint": _color,
        "motion": _motion, "physics": _bool, "solid": _bool, "behavior": _enum(BEHAVIORS), "t": T,
        "path": _path, "speed": _num(0.1, 40), "loop": _bool, "start": _num(0, 1),
    },
    "scatter": {
        "count": _int(1, 100000), "area": _enum(AREAS), "radius": _range2(0, 400),
        "height": _range2(-20, 200), "scale": _range2(0.05, 60), "tint": _color, "seed": _int(0, 99999),
        "motion": _motion, "physics": _bool, "solid": _bool, "behavior": _enum(BEHAVIORS), "t": T,
    },
    "give": {"tint": _color},
    "ride": {"tint": _color, "speed": _num(0.2, 3), "path": _path, "loop": _bool},
    "goal": {"at": _vec3, "radius": _num(0.3, 20), "for": _text(30), "text": _text(80)},
    "player": {"scale": _num(0.05, 20)},
    "modify": {
        "scale": _scale1, "tint": _color, "count": _int(0, 100000),
        "density": _num(0, 1), "motion": _motion, "t": T,
    },
    "remove": {"t": T},
    "ambience": {"volume": _num(0, 1), "t": T},
}

# op -> (field, catalog kind)
REFS = {
    "sky": ("preset", "sky"),
    "terrain": ("type", "terrain"),
    "weather": ("type", "weather"),
    "kit": ("kit", "kit"),
    "spawn": ("asset", "asset"),
    "scatter": ("asset", "asset"),
    "give": ("asset", "asset"),
    "ambience": ("loop", "ambience"),
}
NEEDS_ID = ("kit", "spawn", "scatter", "give")
# Statements that put something at a position. The model writes those positions relative to the
# user (who it is told stands at the origin facing +Z); `frame` says where the user really was.
PLACED = ("spawn", "scatter", "goal", "ride")


def to_frame(value) -> list[float] | None:
    """[x, z, yaw in degrees]: where the user stands in the world and which way they face."""
    if isinstance(value, str):
        value = value.split(",")
    if not isinstance(value, (list, tuple)) or len(value) != 3:
        return None
    try:
        x, z, yaw = (float(v) for v in value)
    except (TypeError, ValueError):
        return None
    if not all(math.isfinite(v) for v in (x, z, yaw)) or abs(x) > 400 or abs(z) > 400:
        return None
    yaw = -((180.0 - yaw) % 360.0 - 180.0)  # -180 < yaw <= 180
    if abs(x) < 0.05 and abs(z) < 0.05 and abs(yaw) < 0.5:
        return None  # at the origin facing +Z: the world's own frame
    return [round(x, 2), round(z, 2), round(yaw, 1)]


def _to_world(p: list, frame: list | None) -> list:
    if not frame:
        return list(p)
    a = math.radians(frame[2])
    return [p[0] * math.cos(a) + p[2] * math.sin(a) + frame[0], p[1], -p[0] * math.sin(a) + p[2] * math.cos(a) + frame[1]]


def _from_world(p: list, frame: list | None) -> list:
    if not frame:
        return list(p)
    a = math.radians(frame[2])
    dx, dz = p[0] - frame[0], p[2] - frame[1]
    return [dx * math.cos(a) - dz * math.sin(a), p[1], dx * math.sin(a) + dz * math.cos(a)]


def _seen_from(stmt: dict, now: list | None) -> dict:
    """A remembered statement as the user would describe it from where they stand now."""
    was = stmt.get("frame")
    out = {k: v for k, v in stmt.items() if k not in ("t", "frame")}
    if was == now:
        return out

    def move(p):
        return [round(v, 1) for v in _from_world(_to_world(p, was), now)]

    if "at" in out:
        out["at"] = move(out["at"])
    elif out["op"] == "spawn" and "path" not in out:
        # "4 m in front" was true where they stood then; say where that is from here
        d, h = out.pop("distance", 4), out.pop("height", 0)
        place = out.pop("place", "front")
        local = {"left": [-d, h, 0], "right": [d, h, 0], "behind": [0, h, -d],
                 "center": [0, h, 0], "above": [0, max(d, 3) + h, 0]}.get(place, [0, h, d])
        out["at"] = move(local)
    if "path" in out:
        out["path"] = [move(p) for p in out["path"]]
    return out


def parse_line(line: str) -> dict | None:
    """Parse one line of model output. Returns None for anything that is not a JSON object."""
    text = line.strip().rstrip(",")
    if not text or text.startswith("```") or not text.startswith("{"):
        return None
    try:
        value = json.loads(text)
    except json.JSONDecodeError:
        # tolerate trailing junk after the closing brace
        end = text.rfind("}")
        if end <= 0:
            return None
        try:
            value = json.loads(text[: end + 1])
        except json.JSONDecodeError:
            return None
    return value if isinstance(value, dict) and isinstance(value.get("op"), str) else None


class WorldState:
    """Compact description of the session's world, kept so follow-ups can modify it."""

    def __init__(self):
        self.globals: dict[str, dict] = {}  # sky, light, fog, terrain, weather, physics, ambience
        self.things: dict[str, dict] = {}  # id -> the statement that made it
        self._auto = 0

    def is_empty(self) -> bool:
        return not self.globals and not self.things

    def next_id(self, op: str, hint: str | None = None) -> str:
        base = to_id(hint) or op
        while True:
            self._auto += 1
            candidate = f"{base}{self._auto}"
            if candidate not in self.things:
                return candidate

    def count(self, op: str) -> int:
        # the pieces of one sculpture ("robot.head", "robot.body") are one thing
        return len({tid.split(".")[0] for tid, s in self.things.items() if s["op"] == op})

    def scatter_total(self) -> int:
        return sum(int(s.get("count", 0)) for s in self.things.values() if s["op"] == "scatter")

    def physics_total(self) -> int:
        total = 0
        for s in self.things.values():
            if s.get("physics"):
                total += int(s.get("count", 1))
        return total

    def matching(self, target: str) -> list[str]:
        """Ids a modify/remove refers to: the exact id, or every `target.part` sculpture piece."""
        if target in self.things:
            return [target]
        return [tid for tid in self.things if tid.startswith(target + ".")]

    def apply(self, stmt: dict) -> None:
        op = stmt["op"]
        if op in ("sky", "terrain"):
            ref = "preset" if op == "sky" else "type"
            if ref in stmt or op not in self.globals:
                self.globals[op] = dict(stmt)
            else:
                self.globals[op].update(stmt)
            if op == "sky" and "preset" in stmt:
                # a preset also resets light and fog
                self.globals.pop("light", None)
                self.globals.pop("fog", None)
        elif op == "goal":
            self.things[stmt["id"]] = dict(stmt)
        elif op == "player":
            if stmt.get("scale") == 1:
                self.globals.pop("player", None)
            else:
                self.globals["player"] = dict(stmt)
        elif op == "ride":
            if stmt.get("asset") == "none":
                self.globals.pop("ride", None)
            else:
                self.globals["ride"] = dict(stmt)
        elif op in ("light", "fog", "weather", "physics", "ambience"):
            merged = self.globals.get(op, {})
            merged.update(stmt)
            self.globals[op] = merged
            if op == "weather" and stmt.get("type") == "none":
                self.globals.pop("weather", None)
        elif op in NEEDS_ID:
            self.things[stmt["id"]] = dict(stmt)
        elif op == "modify":
            for tid in self.matching(stmt.get("id", "")):
                for k, v in stmt.items():
                    if k not in ("op", "id", "t"):
                        self.things[tid][k] = v
        elif op == "remove":
            for tid in self.matching(stmt.get("id", "")):
                self.things.pop(tid, None)
        elif op == "clear":
            if stmt.get("everything"):
                # back to the dark room: nothing left, not even what they were holding
                self.globals.clear()
                self.things.clear()
                return
            keep = set(stmt.get("keep", []))
            # things the user was given stay with them; `remove` takes those away
            self.things = {k: v for k, v in self.things.items() if v["op"] == "give"}
            for g in ("sky", "terrain", "ambience", "weather"):
                if g not in keep:
                    self.globals.pop(g, None)
            if "sky" not in keep:
                self.globals.pop("light", None)
                self.globals.pop("fog", None)

    def render(self, frame: list | None = None) -> str:
        """The world as ImagineScript, with positions relative to a user standing at `frame`."""
        if self.is_empty():
            return "(empty: the dark room, nothing built yet)"
        lines = []
        for op in ("sky", "light", "fog", "terrain", "weather", "physics", "ambience", "ride", "player"):
            if op in self.globals:
                lines.append(_compact(self.globals[op], frame))
        for stmt in self.things.values():
            lines.append(_compact(stmt, frame))
        return "\n".join(lines)


def _compact(stmt: dict, frame: list | None = None) -> str:
    return json.dumps(_seen_from(stmt, frame), separators=(",", ":"))


class Validator:
    def __init__(self, catalog: Catalog, frame: list | None = None, hold_unknown: bool = False):
        self.catalog = catalog
        # where the user stands; placed statements are stamped with it so the headset builds
        # them around the user and not around the middle of the world
        self.frame = frame
        # with hold_unknown, a spawn or scatter of something the catalog lacks is kept back in
        # `missing` (as the model wrote it) for the caller to have made from shapes
        self.hold_unknown = hold_unknown
        self.missing: list[dict] = []

    def check(self, raw: dict, world: WorldState) -> list[dict]:
        """Validate one model statement. Returns the statements to send (notes first)."""
        out = self._check(raw, world)
        if out and out[-1]["op"] == "clear":
            self.frame = None  # the headset brings the user back to the middle, facing +Z
        return out

    def _stamp(self, out: dict) -> dict:
        if self.frame and (out["op"] != "ride" or "path" in out):
            out["frame"] = list(self.frame)
        return out

    def _check(self, raw: dict, world: WorldState) -> list[dict]:
        op = str(raw.get("op", "")).lower()
        if op not in WORLD_OPS:
            return []  # includes stream ops, which the model may not write
        notes: list[dict] = []
        out: dict = {"op": op}
        lim = self.catalog.limits

        # --- what the user is riding or driving; "none" puts them back on their feet
        if op == "ride":
            wanted = to_id(raw.get("asset")) if isinstance(raw.get("asset"), str) else None
            if wanted is None or wanted in NO_RIDE:
                if "ride" not in world.globals:
                    return []
                out["asset"] = "none"
            else:
                resolved, _ = self.catalog.resolve("asset", wanted)
                out["asset"] = resolved or ("car" if self.catalog.has("asset", "car") else wanted)
                if out["asset"] != wanted:
                    notes.append(_note(f"used {_pretty(out['asset'])} in place of {_pretty(wanted)}"))
                for field, convert in FIELDS[op].items():
                    if field in raw:
                        value = convert(raw[field], self.catalog)
                        if value is not None:
                            out[field] = value
            world.apply(self._stamp(out))
            return notes + [out]

        # --- a spot that reacts, and the user's own size
        if op in ("goal", "player"):
            for field, convert in FIELDS[op].items():
                if field in raw:
                    value = convert(raw[field], self.catalog)
                    if value is not None:
                        out[field] = value
            if op == "goal":
                if "at" not in out:
                    return []
                out["for"] = to_id(out.get("for")) or "user"
                out["id"] = to_id(raw.get("id")) or world.next_id("goal")
                self._stamp(out)
            elif "scale" not in out:
                return []
            world.apply(out)
            return [out]

        # --- catalog reference
        if op in REFS:
            field, kind = REFS[op]
            name = raw.get(field)
            if isinstance(name, str) and name.strip():
                wanted = to_id(name) or ""
                # `give` accepts any asset: the headset shrinks non-holdables to hand size
                resolved, _ = self.catalog.resolve(kind, wanted)
                if resolved is None:
                    if kind == "asset":
                        if self.hold_unknown and op in ("spawn", "scatter"):
                            self.missing.append(dict(raw))
                            return []
                        # keep the name: the headset shows a labelled stand-in shape. It is
                        # marked, so the model is never told that the real thing is there.
                        out[field] = wanted
                        out["stand_in"] = True
                        notes.append(_note(f"no '{_pretty(wanted)}' in the library yet, using a stand-in shape"))
                    elif op in ("kit", "ambience", "weather"):
                        notes.append(_note(f"no {op} called '{_pretty(wanted)}', skipped"))
                        return notes
                    # sky/terrain without a valid ref fall through and may still carry overrides
                else:
                    out[field] = resolved
                    if resolved != wanted:
                        notes.append(_note(f"used {_pretty(resolved)} in place of {_pretty(wanted)}"))
            elif op in ("kit", "spawn", "scatter", "give", "ambience", "weather"):
                return []  # the reference is required
            if op == "terrain":
                mat = raw.get("material")
                if isinstance(mat, str) and mat.strip():
                    resolved, _ = self.catalog.resolve("material", to_id(mat) or "")
                    if resolved:
                        out["material"] = resolved

        # --- plain fields
        for field, convert in FIELDS[op].items():
            if field in raw:
                value = convert(raw[field], self.catalog)
                if value is not None:
                    out[field] = value

        # --- per-op rules
        if op in ("say", "think"):
            if "text" not in out:
                return []
            if op == "think":
                return [out]   # shown, never remembered: it is not part of the world
        elif op in ("sky", "light", "fog", "physics"):
            if len(out) == 1:
                return notes
        elif op == "terrain":
            if "type" not in out and "terrain" not in world.globals:
                out["type"] = "flat" if self.catalog.has("terrain", "flat") else self.catalog.ids("terrain")[0]
            if len(out) == 1:
                return notes
        elif op in ("modify", "remove"):
            target = to_id(raw.get("id"))
            if not target or not world.matching(target):
                return notes
            out["id"] = target
            if op == "modify":
                if len(out) == 2:
                    return notes
                if "count" in out:
                    out["count"] = min(out["count"], lim["max_scatter_count"])
        elif op in NEEDS_ID:
            wanted_id = to_id(raw.get("id"))
            out["id"] = wanted_id or world.next_id(op, out.get(REFS[op][0]))
            replacing = world.things.get(out["id"])
            if op == "kit":
                if not replacing and world.count("kit") >= lim["max_kits"]:
                    notes.append(_note("kit limit reached, skipped one"))
                    return notes
            elif op == "spawn":
                if "at" in out:
                    out.pop("place", None)
                    out.pop("distance", None)
                if not replacing and world.count("spawn") >= lim["max_spawned"]:
                    notes.append(_note("object limit reached, skipped one"))
                    return notes
                if out.get("physics") and world.physics_total() >= lim["max_physics"]:
                    out.pop("physics")
            elif op == "scatter":
                count = out.get("count", 12)
                cap = lim["max_scatter_count"]
                if out.get("physics"):
                    cap = max(0, lim["max_physics"] - world.physics_total())
                    if cap == 0:
                        out.pop("physics")
                        cap = lim["max_scatter_count"]
                already = world.scatter_total() - (int(replacing.get("count", 0)) if replacing else 0)
                room = max(0, lim["max_scatter_total"] - already)
                allowed = min(count, cap, room)
                if allowed <= 0:
                    notes.append(_note("the world is full, skipped a scatter"))
                    return notes
                if allowed < count:
                    notes.append(_note(f"capped {_pretty(out.get('asset', 'scatter'))} at {allowed} to keep things smooth"))
                out["count"] = allowed

        if op in PLACED:
            self._stamp(out)
        world.apply(out)
        return notes + [out]


def _note(text: str) -> dict:
    return {"op": "note", "text": text}


def _pretty(identifier: str) -> str:
    return identifier.replace("_", " ")
