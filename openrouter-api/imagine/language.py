"""ImagineScript: the statements the model writes and the headset plays.

One rule runs through this file: repair, never refuse. A statement the model got slightly wrong
is clamped, trimmed or defaulted into something the headset can play. The only statements dropped
are ones with nothing usable left in them. (The previous version rejected a `blueprint` for any
fault in it, and the user watched the terminal say "rejected" while nothing appeared.)
"""

from __future__ import annotations

import math
import re

from .catalog import Catalog

STREAM_OPS = ("transcript", "status", "note", "think", "say", "error", "done")

SHAPES = ("cube", "sphere", "blob", "cylinder", "cone", "capsule", "pyramid", "wedge", "dome",
          "torus", "disc", "lathe", "slab", "tube")
KINDS = ("thing", "tool", "animal", "person", "vehicle", "flyer")
AREAS = ("around", "front", "behind", "left", "right", "ring", "edges", "everywhere", "sky")
PLACES = ("front", "left", "right", "behind", "center", "above")
BEHAVIORS = ("wander", "chase", "flee", "still")
MOTIONS = ("none", "bob", "spin", "hover", "sway", "orbit", "drift")
FEATURES = ("river", "pond", "path", "terraces", "outcrops")
WHERES = ("shore", "high", "low", "steep", "flat")
EVENTS = ("hit", "near", "grab", "use", "every", "start")
ACTIONS = ("burst", "sound", "remove", "spawn", "tint", "scale", "say", "score", "explode", "launch", "respawn")
BURSTS = ("confetti", "sparks", "smoke", "sparkles", "dust", "splash", "fire")
SOUNDS = ("gunshot", "zap", "boom", "pop", "whoosh", "sparkle", "bell", "drum", "chime", "thud", "confetti", "none")

MAX_PARTS = 48
_ID = re.compile(r"[^a-z0-9_]+")
_COLOR = re.compile(r"^#?([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$")


# ---------------------------------------------------------------------------- small repairs

def name_of(value, fallback: str = "") -> str:
    text = _ID.sub("_", str(value or "").strip().lower()).strip("_")
    return text[:40] or fallback


def num(value, lo: float, hi: float, default: float | None = None) -> float | None:
    if isinstance(value, bool):
        return default
    if isinstance(value, str):
        try:
            value = float(value)
        except ValueError:
            return default
    if not isinstance(value, (int, float)) or math.isnan(value) or math.isinf(value):
        return default
    return round(min(max(float(value), lo), hi), 4)


def color(value) -> str | None:
    if isinstance(value, str) and _COLOR.match(value.strip()):
        hexed = value.strip().lstrip("#").upper()
        if len(hexed) == 3:
            hexed = "".join(c * 2 for c in hexed)
        return "#" + hexed
    return None


def vec(value, n: int, lo: float, hi: float) -> list[float] | None:
    if isinstance(value, (list, tuple)) and len(value) == n:
        out = [num(v, lo, hi) for v in value]
        if None not in out:
            return out
    return None


def size3(value, lo: float, hi: float) -> list[float] | float | None:
    """A size may be one number or three."""
    three = vec(value, 3, lo, hi)
    return three if three is not None else num(value, lo, hi)


def points(value, n: int, most: int, lo: float = -200, hi: float = 200) -> list[list[float]] | None:
    if not isinstance(value, (list, tuple)):
        return None
    out = [p for p in (vec(v, n, lo, hi) for v in value[:most]) if p is not None]
    return out if len(out) >= 2 else None


def pick(value, allowed, default=None):
    text = str(value or "").strip().lower().replace(" ", "_")
    return text if text in allowed else default


def flag(value) -> bool:
    return value is True or (isinstance(value, str) and value.strip().lower() == "true")


def to_frame(header: str | None) -> list[float] | None:
    """`X-User-Pose` is `x,z,yaw` in world metres and degrees, or `home` for the middle of the world."""
    if not header or header.strip().lower() == "home":
        return None
    try:
        x, z, yaw = (float(p) for p in header.split(","))
    except ValueError:
        return None
    if not all(math.isfinite(v) for v in (x, z, yaw)):
        return None
    return [round(x, 2), round(z, 2), round(yaw, 1)]


def _to_world(frame: list[float] | None, p: list[float]) -> list[float]:
    if not frame:
        return p
    yaw = math.radians(frame[2])
    c, s = math.cos(yaw), math.sin(yaw)
    return [frame[0] + p[0] * c + p[2] * s, p[1], frame[1] - p[0] * s + p[2] * c]


def _to_user(frame: list[float] | None, p: list[float]) -> list[float]:
    if not frame:
        return p
    yaw = math.radians(frame[2])
    c, s = math.cos(yaw), math.sin(yaw)
    dx, dz = p[0] - frame[0], p[2] - frame[1]
    return [dx * c - dz * s, p[1], dx * s + dz * c]


def reframe(stmt: dict, frame: list[float] | None) -> dict:
    """The same statement as seen from where the user stands now: the model only ever sees user-relative positions."""
    old = stmt.get("frame")
    if old == frame:
        return {k: v for k, v in stmt.items() if k != "frame"}
    out = {k: v for k, v in stmt.items() if k != "frame"}

    def move(p):
        return [round(v, 1) for v in _to_user(frame, _to_world(old, p))]

    if "at" in out:
        out["at"] = move(out["at"])
        out.pop("place", None)
    for key in ("along", "path"):
        if key in out:
            out[key] = [move(p) for p in out[key]]
    if out.get("op") == "scatter" and "along" not in out and (old or frame):
        centre = move([0.0, 0.0, 0.0])
        if abs(centre[0]) + abs(centre[2]) > 3:
            out["around"] = centre   # where its middle is now, since they have walked on
    return out


# ---------------------------------------------------------------------------- the world as the server knows it

class WorldState:
    """What has been built, kept as the statements that built it. This is all the model is shown."""

    def __init__(self):
        self.globals: dict[str, dict] = {}      # sky, terrain, weather, ambience, physics, player, ride
        self.things: dict[str, dict] = {}       # id -> spawn | scatter | give
        self.made: dict[str, dict] = {}         # name -> a one-line summary of a `make`
        self.rules: dict[str, dict] = {}        # id -> when
        self._auto = 0

    def next_id(self, base: str) -> str:
        while True:
            self._auto += 1
            candidate = f"{base}{self._auto}"
            if candidate not in self.things and candidate not in self.rules:
                return candidate

    def apply(self, stmt: dict) -> None:
        op = stmt["op"]
        if op == "clear":
            keep = set(stmt.get("keep", []))
            if stmt.get("everything"):
                self.__init__()
                return
            self.globals = {k: v for k, v in self.globals.items() if k in keep or k == "player"}
            self.things = {k: v for k, v in self.things.items() if v["op"] == "give"}
            self.rules = {}
        elif op in ("sky", "terrain"):
            merged = {**self.globals.get(op, {}), **stmt} if not stmt.get("preset" if op == "sky" else "type") else stmt
            self.globals[op] = {k: v for k, v in merged.items() if k != "t"}
        elif op in ("weather", "ambience", "physics", "player", "fog", "light"):
            self.globals[op] = stmt
            if op == "weather" and stmt.get("type") == "none":
                self.globals.pop(op)
        elif op == "ride":
            if stmt.get("asset") == "none":
                self.globals.pop("ride", None)
            else:
                self.globals["ride"] = stmt
        elif op == "make":
            self.made[stmt["name"]] = {"kind": stmt.get("kind", "thing"), "parts": len(stmt["parts"]), "fires": "fires" in stmt}
        elif op in ("spawn", "scatter", "give"):
            self.things[stmt["id"]] = stmt
        elif op == "modify":
            target = self.things.get(stmt["id"])
            if target is not None:
                changed = {k: v for k, v in stmt.items() if k not in ("op", "id", "t")}
                if target["op"] == "scatter" and changed.get("count") == 0:
                    self.things.pop(stmt["id"])
                else:
                    self.things[stmt["id"]] = {**target, **changed}
        elif op == "remove":
            self.things.pop(stmt["id"], None)
            self.rules.pop(stmt["id"], None)
        elif op == "when":
            self.rules[stmt["id"]] = stmt

    def render(self, frame: list[float] | None = None, user_state: str = "") -> str:
        import json

        def line(stmt: dict) -> str:
            return json.dumps(reframe(stmt, frame), separators=(",", ":"))

        lines = [line(s) for s in self.globals.values()]
        for name, info in self.made.items():
            lines.append(f'(you made "{name}": {info["kind"]}, {info["parts"]} parts{", fires" if info["fires"] else ""})')
        lines += [line(s) for s in self.things.values()]
        lines += [line(s) for s in self.rules.values()]
        if not lines:
            lines = ["(empty: the dark room)"]
        if user_state:
            lines.append("USER RIGHT NOW: " + user_state)
        return "\n".join(lines)


# ---------------------------------------------------------------------------- statements

def parse_line(line: str) -> dict | None:
    """One line of model output as a statement, or None. Tolerates code fences and trailing commas."""
    import json

    text = line.strip().rstrip(",")
    if text.startswith("```") or not text.startswith("{"):
        return None
    try:
        stmt = json.loads(text)
    except json.JSONDecodeError:
        return None
    return stmt if isinstance(stmt, dict) and isinstance(stmt.get("op"), str) else None


class Checker:
    """Turns what the model wrote into what the headset plays. One per turn."""

    def __init__(self, catalog: Catalog, world: WorldState, frame: list[float] | None = None):
        self.catalog = catalog
        self.world = world
        self.frame = frame
        self.missing: list[str] = []      # asset names used that neither the catalog nor a `make` provides
        self.made_now: set[str] = set()

    # ---- assets

    def knows(self, asset: str) -> bool:
        return asset in self.world.made or asset in self.made_now or self.catalog.has("asset", asset)

    def asset(self, value, strict: bool) -> str | None:
        """The asset a statement names. With `strict`, an unknown one returns None and is remembered as missing."""
        name = name_of(value)
        if not name:
            return None
        if self.knows(name):
            return name
        exact = self.catalog.alias("asset", name)
        if exact:
            return exact
        if strict:
            if name not in self.missing:
                self.missing.append(name)
            return None
        return self.catalog.nearest("asset", name) or name   # the headset shows an unknown name as a labelled block

    # ---- one statement

    def check(self, raw: dict, strict: bool = True) -> dict | None:
        """The repaired statement, or None when it cannot be played (or must wait for a missing asset)."""
        op = str(raw.get("op", "")).strip().lower()
        op = {"blueprint": "make", "rule": "when", "sound": "ambience", "gravity": "physics", "delete": "remove", "change": "modify"}.get(op, op)
        handler = getattr(self, "_" + op, None)
        if handler is None or op.startswith("_"):
            return None
        stmt = handler(raw, strict) if op in ("spawn", "scatter", "give", "ride") else handler(raw)
        if stmt is None:
            return None
        stmt = {"op": op, **{k: v for k, v in stmt.items() if v is not None}}
        t = num(raw.get("t"), 0.2, 120 if op == "sky" else 6)
        if t is not None and op in ("sky", "terrain", "clear", "spawn", "scatter", "remove", "modify"):
            stmt["t"] = t
        if self.frame and op in ("spawn", "scatter", "give", "ride"):
            stmt["frame"] = self.frame
        self.world.apply(stmt)
        return stmt

    # ---- talk

    def _say(self, raw):
        text = " ".join(str(raw.get("text", "")).split())[:320]
        return {"text": text} if text else None

    _think = _say

    # ---- the place

    def _sky(self, raw):
        out = {}
        if raw.get("preset"):
            out["preset"] = self.catalog.resolve("sky", raw["preset"])[0]
        for key in ("top", "horizon", "bottom", "sun_color", "cloud_color", "fog_color"):
            out[key] = color(raw.get(key))
        for key, lo, hi in (("sun_elevation", -90, 90), ("sun_azimuth", 0, 360), ("sun_size", 0, 1), ("stars", 0, 1), ("clouds", 0, 1),
                            ("moon", 0, 1), ("aurora", 0, 1), ("nebula", 0, 1), ("fog", 0, 1)):
            out[key] = num(raw.get(key), lo, hi)
        return out if any(v is not None for v in out.values()) else {"preset": "morning"}

    def _fog(self, raw):
        return {"density": num(raw.get("density"), 0, 1, 0.3), "color": color(raw.get("color"))}

    def _light(self, raw):
        out = {"intensity": num(raw.get("intensity"), 0, 3), "color": color(raw.get("color")), "ambient": color(raw.get("ambient"))}
        return out if any(v is not None for v in out.values()) else None

    def _terrain(self, raw):
        out = {}
        if raw.get("type"):
            out["type"] = self.catalog.resolve("terrain", raw["type"])[0] or "rolling"
        if raw.get("material"):
            out["material"] = self.catalog.resolve("material", raw["material"])[0]
        if raw.get("patches"):
            out["patches"] = self.catalog.resolve("material", raw["patches"])[0]
        if isinstance(raw.get("features"), list):
            out["features"] = [f for f in (pick(v, FEATURES) for v in raw["features"]) if f]
        for key in ("height", "roughness", "grid"):
            out[key] = num(raw.get(key), 0, 1)
        for key in ("tint", "grid_color", "water_color"):
            out[key] = color(raw.get(key))
        out["seed"] = num(raw.get("seed"), 0, 99999)
        return out if any(v is not None for v in out.values()) else {"type": "rolling"}

    def _weather(self, raw):
        kind = self.catalog.resolve("weather", raw.get("type") or "none")[0] or "none"
        return {"type": kind, "intensity": num(raw.get("intensity"), 0, 1), "color": color(raw.get("color"))}

    def _ambience(self, raw):
        loop = self.catalog.resolve("ambience", raw.get("loop") or raw.get("type") or "")[0]
        return {"loop": loop, "volume": num(raw.get("volume"), 0, 1)} if loop else None

    def _physics(self, raw):
        return {"gravity": num(raw.get("gravity"), -0.5, 2, 1)}

    def _player(self, raw):
        return {"scale": num(raw.get("scale"), 0.05, 20, 1)}

    def _clear(self, raw):
        if flag(raw.get("everything")):
            return {"everything": True}
        keep = raw.get("keep") if isinstance(raw.get("keep"), list) else []
        keep = [k for k in (pick(v, ("sky", "terrain", "ambience", "weather")) for v in keep) if k]
        return {"keep": keep or None}

    # ---- making things

    def _part(self, raw) -> dict | None:
        if not isinstance(raw, dict):
            return None
        shape = pick(raw.get("shape"), SHAPES) or {"box": "cube", "block": "cube", "plank": "cube", "ball": "sphere", "ring": "torus",
                                                   "tyre": "torus", "plate": "disc", "pipe": "tube", "ramp": "wedge"}.get(str(raw.get("shape", "")).lower(), "cube")
        part = {"shape": shape, "id": name_of(raw.get("id")) or None, "parent": name_of(raw.get("parent")) or None}
        part["position"] = vec(raw.get("position", raw.get("at")), 3, -60, 60)
        part["rotation"] = vec(raw.get("rotation", raw.get("rot")), 3, -360, 360)
        scale = size3(raw.get("scale", raw.get("size")), 0.002, 80)
        part["scale"] = [scale] * 3 if isinstance(scale, float) else scale
        part["color"] = color(raw.get("color", raw.get("tint")))
        part["color_top"] = color(raw.get("color_top"))
        part["emission"] = num(raw.get("emission", raw.get("glow")), 0, 3)
        part["bevel"] = num(raw.get("bevel"), 0, 1)
        taper = raw.get("taper")
        part["taper"] = vec(taper, 2, 0, 3) or num(taper, 0, 3)
        part["rough"] = num(raw.get("rough"), 0, 0.6)
        part["seed"] = num(raw.get("seed"), 0, 9999)
        part["sides"] = num(raw.get("sides"), 3, 24)
        part["radius"] = num(raw.get("radius"), 0.002, 5)
        part["thickness"] = num(raw.get("thickness"), 0.002, 20)
        part["mirror"] = "x" if raw.get("mirror") in ("x", True, "X") else None
        part["anchor"] = pick(raw.get("anchor"), ("top", "bottom"))
        repeat = num(raw.get("repeat"), 2, 16)
        step = vec(raw.get("step"), 3, -40, 40)
        if repeat and step:
            part["repeat"], part["step"] = int(repeat), step
        # a shape that needs a drawing and did not get a usable one becomes the nearest plain shape, not nothing
        if shape == "lathe":
            part["profile"] = points(raw.get("profile"), 2, 16, 0, 60)
            if part["profile"] is None:
                part["shape"] = "cylinder"
        elif shape == "slab":
            outline = points(raw.get("outline"), 2, 24, -60, 60)
            part["outline"] = outline if outline and len(outline) >= 3 else None
            if part["outline"] is None:
                part["shape"] = "cube"
        elif shape == "tube":
            part["points"] = points(raw.get("points"), 3, 16, -60, 60)
            if part["points"] is None:
                part["shape"] = "capsule"
        return {k: v for k, v in part.items() if v is not None}

    def _make(self, raw):
        name = name_of(raw.get("name") or raw.get("id") or raw.get("asset"), "thing")
        parts = [p for p in (self._part(r) for r in (raw.get("parts") if isinstance(raw.get("parts"), list) else [])[:MAX_PARTS]) if p]
        if not parts:
            parts = [{"shape": "cube", "scale": [0.5, 0.5, 0.5], "color": "#C8C8D0"}]
        out = {"name": name, "kind": pick(raw.get("kind"), KINDS, "thing"), "parts": parts}
        fires = raw.get("fires")
        if fires is not None and fires is not False:
            fires = fires if isinstance(fires, dict) else {}
            out["kind"] = "tool"
            out["fires"] = {k: v for k, v in {
                "sound": pick(fires.get("sound"), SOUNDS, "zap"),
                "every": num(fires.get("every", fires.get("cooldown")), 0.05, 5, 0.25),
                "auto": flag(fires.get("auto")) or None,
                "speed": num(fires.get("speed"), 2, 80),
                "size": num(fires.get("size"), 0.2, 6),
                "pellets": num(fires.get("pellets"), 1, 12),
                "spread": num(fires.get("spread"), 0, 30),
                "color": color(fires.get("color")),
                "muzzle": vec(fires.get("muzzle"), 3, -5, 5),
                "shoots": self._shoots(fires.get("shoots") or fires.get("projectile")),
            }.items() if v is not None}
        if out["kind"] == "tool":
            out["grip"] = vec(raw.get("grip"), 3, -5, 5)
        self.made_now.add(name)
        if name in self.missing:
            self.missing.remove(name)
        return out

    def _shoots(self, value) -> str | None:
        """What a gun throws in place of a ball of light: a kind of burst, or any asset there is."""
        name = name_of(value)
        if not name or name in BURSTS:
            return name or None
        found = self.asset(name, strict=False)
        return found if found and self.knows(found) else None

    # ---- putting things in the world

    def _common(self, raw, out):
        out["tint"] = color(raw.get("tint", raw.get("color")))
        out["behavior"] = pick(raw.get("behavior"), BEHAVIORS)
        out["motion"] = pick(raw.get("motion"), MOTIONS)
        out["solid"] = True if flag(raw.get("solid")) else None
        out["physics"] = True if flag(raw.get("physics")) else None

    def _spawn(self, raw, strict):
        asset = self.asset(raw.get("asset") or "cube", strict)
        if asset is None:
            return None
        out = {"id": name_of(raw.get("id")) or self.world.next_id(asset + "_"), "asset": asset}
        out["at"] = vec(raw.get("at"), 3, -200, 200)
        if out["at"] is None:
            out["place"] = pick(raw.get("place"), PLACES, "front")
            out["distance"] = num(raw.get("distance"), 0, 110)
            out["height"] = num(raw.get("height"), -5, 80)
        out["scale"] = size3(raw.get("scale"), 0.02, 60)
        out["rot_y"] = num(raw.get("rot_y"), -360, 360)
        out["rot"] = vec(raw.get("rot"), 3, -360, 360)
        self._common(raw, out)
        repeat = num(raw.get("repeat"), 2, 40)
        if repeat:
            out["repeat"] = int(repeat)
            out["step"] = vec(raw.get("step"), 3, -40, 40)
            out["ring"] = num(raw.get("ring"), 0.5, 100)
            if out["step"] is None and out["ring"] is None:
                out.pop("repeat")
        out["path"] = points(raw.get("path"), 3, 24)
        if out["path"]:
            out["speed"] = num(raw.get("speed"), 0.1, 40)
            out["loop"] = False if raw.get("loop") is False else None
        return out

    def _scatter(self, raw, strict):
        asset = self.asset(raw.get("asset") or "cube", strict)
        if asset is None:
            return None
        out = {"id": name_of(raw.get("id")) or self.world.next_id(asset + "_"), "asset": asset}
        out["count"] = int(num(raw.get("count"), 1, 300, 12))
        out["area"] = pick(raw.get("area"), AREAS)
        for key, lo, hi in (("radius", 0, 115), ("height", 0, 80), ("scale", 0.02, 30)):
            pair = vec(raw.get(key), 2, lo, hi)
            if pair:
                out[key] = sorted(pair)
            elif key == "scale" and num(raw.get(key), lo, hi):
                out[key] = [num(raw.get(key), lo, hi)] * 2
        out["clusters"] = num(raw.get("clusters"), 1, 24)
        out["spread"] = num(raw.get("spread"), 0, 40)
        out["along"] = points(raw.get("along"), 3, 24)
        out["where"] = pick(raw.get("where"), WHERES)
        out["seed"] = num(raw.get("seed"), 0, 99999)
        self._common(raw, out)
        return out

    def _give(self, raw, strict):
        asset = self.asset(raw.get("asset") or "cube", strict)
        if asset is None:
            return None
        return {"id": name_of(raw.get("id")) or asset, "asset": asset, "tint": color(raw.get("tint", raw.get("color")))}

    def _ride(self, raw, strict):
        if str(raw.get("asset", "")).strip().lower() in ("none", "", "off"):
            return {"asset": "none"}
        asset = self.asset(raw.get("asset"), strict)
        if asset is None:
            return None
        return {"asset": asset, "tint": color(raw.get("tint", raw.get("color"))), "speed": num(raw.get("speed"), 0.2, 3),
                "path": points(raw.get("path"), 3, 24), "loop": False if raw.get("loop") is False else None}

    def _modify(self, raw):
        target = name_of(raw.get("id"))
        if not target:
            return None
        out = {"id": target, "tint": color(raw.get("tint", raw.get("color"))), "scale": num(raw.get("scale"), 0.02, 60),
               "motion": pick(raw.get("motion"), MOTIONS), "behavior": pick(raw.get("behavior"), BEHAVIORS)}
        if raw.get("count") is not None:
            out["count"] = int(num(raw.get("count"), 0, 300, 12))
        return out

    def _remove(self, raw):
        target = name_of(raw.get("id"))
        return {"id": target} if target else None

    # ---- rules

    def _action(self, raw) -> dict | None:
        if isinstance(raw, str):
            raw = {"act": raw}
        if not isinstance(raw, dict):
            return None
        act = pick(raw.get("act") or raw.get("action"), ACTIONS)
        if act is None:
            return None
        out = {"act": act, "delay": num(raw.get("delay"), 0, 30)}
        if act == "burst":
            out["kind"] = pick(raw.get("kind") or raw.get("preset"), BURSTS, "confetti")
            out["color"] = color(raw.get("color"))
        elif act == "sound":
            out["name"] = pick(raw.get("name") or raw.get("sound"), SOUNDS, "pop")
        elif act == "spawn":
            out["asset"] = self.asset(raw.get("asset") or "cube", False)
            out["count"] = int(num(raw.get("count"), 1, 8, 1))
            out["scale"] = num(raw.get("scale"), 0.05, 20)
            out["behavior"] = pick(raw.get("behavior"), BEHAVIORS)
            out["physics"] = True if flag(raw.get("physics")) else None
            out["tint"] = color(raw.get("tint", raw.get("color")))
        elif act == "tint":
            out["color"] = color(raw.get("color")) or "#FF4040"
        elif act == "scale":
            out["by"] = num(raw.get("by", raw.get("scale")), 0.1, 10, 1.5)
        elif act == "say":
            out["text"] = " ".join(str(raw.get("text", "")).split())[:120] or None
        elif act == "score":
            out["add"] = num(raw.get("add", raw.get("value")), -100, 100, 1)
        elif act in ("explode", "launch"):
            out["force"] = num(raw.get("force"), 0, 40)
            out["color"] = color(raw.get("color"))
        return {k: v for k, v in out.items() if v is not None}

    def _when(self, raw):
        event = pick(raw.get("event") or raw.get("on"), EVENTS) or {"shot": "hit", "struck": "hit", "touch": "near", "reach": "near",
                                                                 "timer": "every", "trigger": "use", "fire": "use"}.get(str(raw.get("event", "")).lower())
        actions = raw.get("do") or raw.get("actions") or []
        actions = [a for a in (self._action(r) for r in (actions if isinstance(actions, list) else [actions])[:8]) if a]
        if event is None or not actions:
            return None
        target = name_of(raw.get("target") or raw.get("who") or raw.get("asset"), "any")
        return {"id": name_of(raw.get("id")) or self.world.next_id("rule_"), "event": event, "target": target,
                "seconds": num(raw.get("seconds", raw.get("every")), 0.2, 600) if event == "every" else None,
                "radius": num(raw.get("radius"), 0.3, 60) if event == "near" else None,
                "once": True if flag(raw.get("once")) else None, "do": actions}
