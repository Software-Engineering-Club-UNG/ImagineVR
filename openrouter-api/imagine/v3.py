"""The third version of ImagineScript: what a headset that says `play-v1` can do beyond the first two.

Things that move as their parts say (wheels, rotors, doors), rides that drive and fly, a world that
keeps score (variables, zones, stages, health), people who speak, roads, sound and music written
on the spot. None of it names a game: bowling is a slippery lane, heavy pins and a rule about what
is still standing.

Same rule as the rest of the language: repair, never refuse. Everything here clamps, defaults or
skips a part; a statement is only dropped when nothing playable is left of it.
"""

from __future__ import annotations

import re

from . import expr
from .fix import color, flag, ground, name_of, num, pick, points, text_of, vec

CAPABILITY = "play-v1"
GENERATED = "gen-v1"        # the headset can fetch a generated picture, panorama or voice by URL

OPS = ("road", "zone", "var", "hud", "stage", "fx", "sfx", "music", "speak", "narrate", "story")
OP_ALIASES = {"variable": "var", "counter": "var", "timer": "var", "trigger": "zone", "area": "zone", "emitter": "fx",
              "particles": "fx", "objective": "hud", "banner": "hud", "scoreboard": "hud", "quest": "stage", "chapter": "stage",
              "synth": "sfx", "npc": "spawn", "say_as": "speak", "narration": "narrate", "street": "road", "track": "road"}

MAX_PARTS = 120
ROLES = ("wheel", "steer", "spinner", "door", "lamp", "seat", "thruster", "gun", "flap")
FINISHES = ("matte", "metal", "chrome", "glass", "paint", "gloss", "wood", "rubber", "cloth", "stone", "skin", "glow")
PATTERNS = ("stripes", "checker", "bricks", "planks", "tiles", "panels", "windows", "dots", "noise", "scales", "camo", "grid")
STYLES = ("plain", "brick", "glass", "concrete", "stone", "wood", "plaster", "metal", "neon", "adobe")
ROOFS = ("flat", "gable", "hip", "spire", "dome", "stepped", "shed")
DRIVES = ("auto", "car", "bike", "tank", "mount", "boat", "plane", "glider", "heli", "hover", "space", "rail", "simple")
SURFACES = ("ice", "wood", "felt", "rubber", "metal", "grass", "sand", "mud", "stone", "carpet", "glass")
VOICES = ("narrator", "warm", "bright", "deep", "gruff", "old", "young", "child", "robot", "monster", "whisper", "villain", "hero", "cheerful")
BEHAVIORS = ("wander", "chase", "flee", "still", "attack", "shoot", "guard", "follow", "patrol")
ROAD_KINDS = ("street", "highway", "lane", "dirt", "track", "runway", "rail", "path", "bridge")
ROAD_SHAPES = ("straight", "loop", "oval", "grid", "cross", "figure8", "ring")
FX = ("fire", "smoke", "steam", "sparks", "fountain", "fireflies", "leaves", "embers", "bubbles", "waterfall", "portal", "snowfall",
      "dust", "magic", "drips")
MOODS = ("calm", "happy", "tense", "epic", "eerie", "sad", "playful", "heroic", "mysterious", "action", "lofi", "retro", "spooky",
         "dreamy", "western", "none")
INSTRUMENTS = ("pad", "bass", "arp", "lead", "drums", "bells", "strings", "pluck")
WAVES = ("sine", "square", "saw", "triangle", "noise")
ZONE_SHOWS = ("none", "ring", "beam", "flag", "glow", "box")
SIZES = ("normal", "wide", "vast")
REACH = {"normal": 115.0, "wide": 390.0, "vast": 1190.0}

# Sounds the headset can synthesise by name. The first twelve are the ones the first two versions knew.
SOUNDS = ("gunshot", "zap", "boom", "pop", "whoosh", "sparkle", "bell", "drum", "chime", "thud", "confetti", "none",
          "horn", "screech", "crash", "glass", "crack", "splash", "clang", "twang", "thunk", "bounce", "swing", "roll", "strike",
          "cheer", "applause", "whistle", "bark", "meow", "moo", "roar", "growl", "chirp", "neigh", "fanfare", "lose", "ticktock",
          "beep", "coin", "powerup", "heal", "hurt", "reload", "click", "door", "lock", "unlock", "jet", "laser", "alarm", "siren",
          "heartbeat", "thunder", "gust", "magic", "score", "step", "engine")

EVENTS = ("hit", "near", "grab", "use", "every", "start", "enter", "exit", "press", "tip", "rest", "die", "collide", "drop",
          "becomes", "stage", "hurt", "strike", "lap")
EVENT_ALIASES = {"shot": "hit", "struck": "hit", "touch": "near", "reach": "near", "timer": "every", "trigger": "use", "fire": "use",
                 "enters": "enter", "inside": "enter", "entered": "enter", "leave": "exit", "leaves": "exit", "left": "exit",
                 "interact": "press", "click": "press", "point": "press", "pressed": "press", "open": "press",
                 "fall": "tip", "falls": "tip", "knocked": "tip", "tipped": "tip", "toppled": "tip",
                 "stop": "rest", "stops": "rest", "settle": "rest", "rests": "rest", "stopped": "rest",
                 "death": "die", "dies": "die", "killed": "die", "dead": "die", "destroyed": "die",
                 "when": "becomes", "if": "becomes", "test": "becomes", "reaches": "becomes", "condition": "becomes",
                 "touches": "collide", "collides": "collide", "collision": "collide", "pickup": "grab", "dropped": "drop",
                 "release": "drop", "thrown": "drop", "damage": "hurt", "damaged": "hurt", "enter_stage": "stage",
                 "swing": "strike", "stroke": "strike", "putt": "strike", "batted": "strike", "laps": "lap", "finish": "lap"}
LEGACY_ACTIONS = ("burst", "sound", "remove", "spawn", "tint", "scale", "say", "score", "explode", "launch", "respawn")
ACTIONS = LEGACY_ACTIONS + ("set", "add", "banner", "objective", "reset", "teleport", "give", "take", "stage", "damage", "heal",
                            "enable", "disable", "speak", "narrate", "music", "fade", "slowmo", "shake", "door", "modify",
                            "win", "lose", "checkpoint", "direct", "fx", "ride")
ACTION_ALIASES = {"increment": "add", "inc": "add", "increase": "add", "message": "banner", "announce": "banner", "show": "banner",
                  "goto": "stage", "next_stage": "stage", "advance": "stage", "kill": "damage", "tp": "teleport",
                  "move": "teleport", "play": "sound", "talk": "speak", "story": "direct", "continue": "direct", "particles": "fx",
                  "heal_player": "heal", "restart": "reset", "rerack": "reset", "unlock": "door", "open": "door"}

_CHORD = re.compile(r"^[A-G][#b]?(m|maj|min|dim|aug|sus2|sus4)?(6|7|9|maj7|m7)?$")


def reach_of(size: str | None) -> float:
    """How far from the middle of the world things may stand, for a terrain of this size."""
    return REACH.get(size or "normal", REACH["normal"])


def _names(checker, value, most: int) -> list[str] | None:
    """Assets named in a list (or one alone): only those that exist. Naming one that does not costs nothing."""
    if isinstance(value, str):
        value = [value]
    if not isinstance(value, (list, tuple)):
        return None
    out: list[str] = []
    for item in value[:most * 2]:
        found = checker.asset(item, strict=False)
        if found and checker.knows(found) and found not in out:
            out.append(found)
    return out[:most] or None


class V3:
    """Mixed into the Checker. `self.play` and `self.gen` say what this headset understands."""

    # ---- on any spawn or scatter

    def _common_v3(self, raw: dict, out: dict) -> None:
        out["behavior"] = pick(raw.get("behavior"), BEHAVIORS)
        out["health"] = num(raw.get("health", raw.get("hp")), 1, 100000)
        out["team"] = name_of(raw.get("team"))[:16] or None
        out["damage"] = num(raw.get("damage"), 0, 1000)
        out["sight"] = num(raw.get("sight"), 1, 200)
        if out.get("behavior") and not out.get("path"):
            out["speed"] = num(raw.get("speed"), 0.1, 6)
        out["bounce"] = num(raw.get("bounce"), 0, 1)
        out["friction"] = num(raw.get("friction"), 0, 2)
        out["mass"] = num(raw.get("mass"), 0.01, 5000)
        out["surface"] = pick(raw.get("surface"), SURFACES)
        out["label"] = text_of(raw.get("label"), 40)
        out["sign"] = text_of(raw.get("sign"), 40)
        out["name"] = text_of(raw.get("name"), 30)
        out["persona"] = text_of(raw.get("persona"), 240)
        out["voice"] = pick(raw.get("voice"), VOICES)
        if isinstance(raw.get("says"), (list, tuple, str)):
            lines = [raw["says"]] if isinstance(raw["says"], str) else raw["says"]
            out["says"] = [t for t in (text_of(v, 120) for v in lines[:6]) if t] or None
        out["light"] = self._light_of(raw.get("light"))
        # a plain shape may be given a look as a part is: a glossy lane, a brick wall, a glass pane
        out["finish"] = pick(raw.get("finish", raw.get("material")), FINISHES)
        out["pattern"] = pick(raw.get("pattern"), PATTERNS)
        if out["pattern"]:
            out["pattern_color"] = color(raw.get("pattern_color"))
            out["pattern_scale"] = num(raw.get("pattern_scale"), 0.02, 20)
        breaks = raw.get("breaks", raw.get("breakable"))
        out["breaks"] = pick(breaks, ("glass", "wood", "stone", "metal", "crate")) or (True if flag(breaks) else None)
        if self.gen:
            out["image"] = text_of(raw.get("image") or raw.get("picture"), 200)

    @staticmethod
    def _light_of(value) -> dict | None:
        if value is None or value is False:
            return None
        if isinstance(value, dict):
            return {k: v for k, v in {"color": color(value.get("color")) or "#FFD9A0", "range": num(value.get("range"), 1, 60, 8),
                                      "intensity": num(value.get("intensity"), 0, 4, 1)}.items() if v is not None}
        tone = color(value)
        if tone or flag(value):
            return {"color": tone or "#FFD9A0", "range": 8.0, "intensity": 1.0}
        return None

    # ---- make

    def _part_v3(self, raw: dict, part: dict) -> None:
        shape = str(raw.get("shape", "")).strip().lower()
        if shape in ("building", "stairs", "arch", "asset"):
            part["shape"] = shape
        part["role"] = pick(raw.get("role"), ROLES) or {"wheels": "wheel", "tyre": "wheel", "tire": "wheel", "propeller": "spinner",
                                                        "rotor": "spinner", "fan": "spinner", "steering_wheel": "steer",
                                                        "handlebar": "steer", "yoke": "steer", "headlight": "lamp", "light": "lamp",
                                                        "engine": "thruster", "exhaust": "thruster", "cannon": "gun", "turret": "gun",
                                                        "wing": "flap"}.get(name_of(raw.get("role")))
        part["finish"] = pick(raw.get("finish", raw.get("material")), FINISHES)
        part["pattern"] = pick(raw.get("pattern"), PATTERNS)
        if part["pattern"]:
            part["pattern_color"] = color(raw.get("pattern_color"))
            part["pattern_scale"] = num(raw.get("pattern_scale"), 0.02, 20)
        part["pivot"] = vec(raw.get("pivot"), 3, -60, 60)
        anim = raw.get("anim")
        if isinstance(anim, dict):
            part["anim"] = {k: v for k, v in {
                "spin": vec(anim.get("spin"), 3, -3600, 3600), "swing": vec(anim.get("swing"), 3, -180, 180),
                "every": num(anim.get("every"), 0.05, 60), "bob": num(anim.get("bob"), 0, 3),
                "when": pick(anim.get("when"), ("always", "moving", "alive", "throttle")),
            }.items() if v is not None} or None
        if shape == "building":
            part["floors"] = num(raw.get("floors"), 1, 90)
            part["style"] = pick(raw.get("style"), STYLES)
            part["roof"] = pick(raw.get("roof"), ROOFS)
            part["trim"] = color(raw.get("trim"))
            part["shop"] = True if flag(raw.get("shop")) else None
            part["lit"] = num(raw.get("lit"), 0, 1)
        elif shape == "stairs":
            part["steps"] = num(raw.get("steps"), 2, 40)
        elif shape == "asset":
            found = self.asset(raw.get("asset") or raw.get("name"), strict=False)
            if found and self.knows(found):
                part["asset"] = found
            else:
                part["shape"] = "cube"      # a part that names nothing there is: a block where it would have stood

    def _make_v3(self, raw: dict, out: dict) -> None:
        out["drive"] = pick(raw.get("drive"), DRIVES)
        out["seat"] = vec(raw.get("seat"), 3, -40, 40)
        out["health"] = num(raw.get("health"), 1, 100000)
        strike = raw.get("strike")
        if isinstance(strike, dict) or flag(strike):
            strike = strike if isinstance(strike, dict) else {}
            out["strike"] = {"power": num(strike.get("power"), 0.2, 5, 1.2), "loft": num(strike.get("loft"), 0, 60, 0)}
        out["refine"] = text_of(raw.get("refine") or raw.get("detail"), 400)
        fires = raw.get("fires")
        if isinstance(fires, dict) and "fires" in out:
            extra = {"damage": num(fires.get("damage"), 0, 1000), "ammo": num(fires.get("ammo", fires.get("magazine")), 1, 999),
                     "reload": num(fires.get("reload"), 0.2, 10), "recoil": num(fires.get("recoil"), 0, 1),
                     "hitscan": True if flag(fires.get("hitscan")) else None, "blast": num(fires.get("blast"), 0, 15)}
            out["fires"].update({k: v for k, v in extra.items() if v is not None})
            sound = name_of(fires.get("sound"))
            if sound in SOUNDS or sound in self.world.sounds:
                out["fires"]["sound"] = sound

    # ---- ride, player, sky, terrain, modify

    def _ride_v3(self, raw: dict, out: dict) -> None:
        out["drive"] = pick(raw.get("drive"), DRIVES)
        out["speed"] = num(raw.get("speed"), 0.2, 4)
        out["grip"] = num(raw.get("grip", raw.get("handling")), 0, 1)
        out["accel"] = num(raw.get("accel"), 0.2, 3)
        out["seat"] = vec(raw.get("seat"), 3, -40, 40)
        out["altitude"] = num(raw.get("altitude"), 0, 800)

    def _player_v3(self, raw: dict) -> dict | None:
        out = {"scale": num(raw.get("scale"), 0.05, 20), "health": num(raw.get("health"), 0, 100000),
               "speed": num(raw.get("speed"), 0.3, 4), "team": name_of(raw.get("team"))[:16] or None,
               "at": vec(raw.get("at"), 3, -self.limit, self.limit), "face": num(raw.get("face", raw.get("rot_y")), -360, 360)}
        for key in ("jump", "fly", "climb"):
            if key in raw:
                out[key] = bool(flag(raw[key]) or (isinstance(raw[key], (int, float)) and not isinstance(raw[key], bool) and raw[key] > 0))
        out = {k: v for k, v in out.items() if v is not None}
        return out or {"scale": 1.0}

    def _sky_v3(self, raw: dict, out: dict) -> None:
        if "cycle" in raw:
            out["cycle"] = num(raw.get("cycle"), 0, 3600)
        if self.gen:
            out["image"] = text_of(raw.get("image") or raw.get("panorama"), 240)

    def _terrain_v3(self, raw: dict, out: dict) -> None:
        out["size"] = pick(raw.get("size"), SIZES)
        if isinstance(raw.get("regions"), list):
            limit = reach_of(out["size"] or self.world.size) + 10
            regions = []
            for item in raw["regions"][:16]:
                if not isinstance(item, dict):
                    continue
                at = ground(item.get("at"), -limit, limit)
                if at is None:
                    continue
                region = {"at": [at[0], 0.0, at[2]], "material": self.catalog.resolve("material", item["material"])[0] if item.get("material") else None,
                          "radius": num(item.get("radius"), 0.5, 400), "size": vec(item.get("size"), 2, 0.5, 800),
                          "rot_y": num(item.get("rot_y"), -360, 360), "height": num(item.get("height", item.get("raise")), -12, 40),
                          "level": True if flag(item.get("level", item.get("flat"))) else None, "blend": num(item.get("blend"), 0, 30),
                          "id": name_of(item.get("id")) or None}
                if region["radius"] is None and region["size"] is None:
                    region["radius"] = 6.0
                regions.append({k: v for k, v in region.items() if v is not None})
            out["regions"] = regions or None

    def _modify_v3(self, raw: dict, out: dict) -> None:
        out["behavior"] = pick(raw.get("behavior"), BEHAVIORS)
        for key, lo, hi in (("health", 1, 100000), ("speed", 0.1, 6), ("damage", 0, 1000), ("sight", 1, 200), ("bounce", 0, 1),
                            ("friction", 0, 2), ("mass", 0.01, 5000), ("grip", 0, 1), ("accel", 0.2, 3)):
            out[key] = num(raw.get(key), lo, hi)
        out["team"] = name_of(raw.get("team"))[:16] or None
        out["label"] = text_of(raw.get("label"), 40)
        out["name"] = text_of(raw.get("name"), 30)
        out["persona"] = text_of(raw.get("persona"), 240)
        out["voice"] = pick(raw.get("voice"), VOICES)
        out["drive"] = pick(raw.get("drive"), DRIVES)
        out["surface"] = pick(raw.get("surface"), SURFACES)
        out["light"] = self._light_of(raw.get("light"))

    # ---- new statements

    def _road(self, raw: dict) -> dict | None:
        limit = self.limit
        kind = pick(raw.get("kind", raw.get("type")), ROAD_KINDS, "street")
        out = {"id": name_of(raw.get("id")) or self.world.next_id("road_"), "kind": kind}
        line = None
        if isinstance(raw.get("points"), (list, tuple)):
            line = [p for p in (ground(v, -limit, limit) for v in raw["points"][:32]) if p is not None]
        if line and len(line) >= 2:
            out["points"] = line
            out["loop"] = True if flag(raw.get("loop")) else None
        else:
            out["shape"] = pick(raw.get("shape"), ROAD_SHAPES, "straight")
            out["at"] = ground(raw.get("at"), -limit, limit)
            out["rot_y"] = num(raw.get("rot_y"), -360, 360)
            out["size"] = vec(raw.get("size"), 2, 8, 2 * limit)
            out["radius"] = num(raw.get("radius"), 6, limit)
            out["length"] = num(raw.get("length"), 8, 2 * limit)
            if out["shape"] == "grid":
                blocks = vec(raw.get("blocks"), 2, 1, 8)
                out["blocks"] = [int(blocks[0]), int(blocks[1])] if blocks else [3, 3]
                out["block"] = vec(raw.get("block"), 2, 16, 200)
        out["width"] = num(raw.get("width"), 1.5, 40)
        out["lanes"] = int(num(raw.get("lanes"), 1, 6)) if raw.get("lanes") is not None and num(raw.get("lanes"), 1, 6) else None
        for key in ("sidewalks", "lamps", "barriers", "banked"):
            if key in raw:
                out[key] = bool(flag(raw[key]))
        if raw.get("markings") is False or str(raw.get("markings")).strip().lower() == "false":
            out["markings"] = False
        out["tint"] = color(raw.get("tint", raw.get("color")))
        out["height"] = num(raw.get("height"), 0, 60)
        out["checkpoints"] = int(num(raw.get("checkpoints"), 0, 12, 0)) or None
        traffic = raw.get("traffic")
        if isinstance(traffic, dict):
            assets = _names(self, traffic.get("assets") or traffic.get("asset"), 6)
            count = int(num(traffic.get("count"), 0, 24, 8))
            if count:
                out["traffic"] = {k: v for k, v in {"assets": assets, "count": count, "speed": num(traffic.get("speed"), 1, 40)}.items() if v is not None}
        elif flag(traffic) or (isinstance(traffic, (int, float)) and not isinstance(traffic, bool) and traffic > 0):
            out["traffic"] = {"count": int(num(traffic, 1, 24, 8)) if not isinstance(traffic, bool) and isinstance(traffic, (int, float)) else 8}
        walkers = raw.get("walkers", raw.get("pedestrians"))
        if isinstance(walkers, dict):
            count = int(num(walkers.get("count"), 0, 16, 8))
            if count:
                out["walkers"] = {k: v for k, v in {"assets": _names(self, walkers.get("assets") or walkers.get("asset"), 6), "count": count}.items() if v is not None}
        elif flag(walkers) or (isinstance(walkers, (int, float)) and not isinstance(walkers, bool) and walkers > 0):
            out["walkers"] = {"count": int(num(walkers, 1, 16, 8)) if not isinstance(walkers, bool) and isinstance(walkers, (int, float)) else 8}
        buildings = raw.get("buildings")
        if isinstance(buildings, dict):
            out["buildings"] = {k: v for k, v in {
                "assets": _names(self, buildings.get("assets") or buildings.get("asset"), 8),
                "density": num(buildings.get("density"), 0, 1), "setback": num(buildings.get("setback"), 0, 40),
                "scale": vec(buildings.get("scale"), 2, 0.2, 6), "solid": False if buildings.get("solid") is False else None,
            }.items() if v is not None}
        elif flag(buildings):
            out["buildings"] = {}
        return out

    def _zone(self, raw: dict) -> dict | None:
        limit = self.limit
        out = {"id": name_of(raw.get("id")) or self.world.next_id("zone_"),
               "shape": pick(raw.get("shape"), ("box", "sphere"), "sphere" if raw.get("radius") is not None and raw.get("size") is None else "box")}
        on = name_of(raw.get("on") or raw.get("attach"))
        if on:
            out["on"] = on
            out["offset"] = vec(raw.get("offset", raw.get("at")), 3, -60, 60)
        else:
            out["at"] = vec(raw.get("at"), 3, -limit, limit) or [0.0, 0.0, 4.0]
        size = vec(raw.get("size", raw.get("scale")), 3, 0.05, 800)
        out["size"] = size
        out["radius"] = num(raw.get("radius"), 0.05, 400)
        if size is None and out["radius"] is None:
            out["radius"] = 1.5
        out["rot_y"] = num(raw.get("rot_y"), -360, 360)
        out["show"] = pick(raw.get("show"), ZONE_SHOWS)
        out["color"] = color(raw.get("color", raw.get("tint")))
        effect = raw.get("effect")
        if isinstance(effect, dict):
            out["effect"] = {k: v for k, v in {
                "damage": num(effect.get("damage"), 0, 1000), "heal": num(effect.get("heal"), 0, 1000),
                "gravity": num(effect.get("gravity"), -1, 3), "push": vec(effect.get("push", effect.get("wind")), 3, -40, 40),
                "slow": num(effect.get("slow"), 0.05, 1), "boost": num(effect.get("boost"), 1, 5),
                "kill": True if flag(effect.get("kill")) else None, "water": True if flag(effect.get("water")) else None,
            }.items() if v is not None} or None
        return out

    def _var(self, raw: dict) -> dict | None:
        name = name_of(raw.get("name") or raw.get("var") or raw.get("id"))[:24]
        if not name:
            return None
        out = {"name": name, "value": num(raw.get("value", raw.get("start")), -1e9, 1e9), "text": text_of(raw.get("text"), 60),
               "min": num(raw.get("min"), -1e9, 1e9), "max": num(raw.get("max"), -1e9, 1e9), "rate": num(raw.get("rate"), -1000, 1000),
               "as": pick(raw.get("as", raw.get("style")), ("number", "bar", "time"))}
        # a `timer` is a variable that runs: seconds counting down unless it says otherwise
        if str(raw.get("op", "")).strip().lower() == "timer":
            seconds = num(raw.get("seconds", raw.get("value")), 0, 86400, 60)
            up = pick(raw.get("count"), ("up", "down"), "down") == "up"
            out.update({"value": 0.0 if up else seconds, "rate": 1.0 if up else -1.0, "min": 0.0, "max": seconds if up else out["max"], "as": "time"})
        if out["value"] is None and out["text"] is None:
            out["value"] = 0.0
        show = raw.get("show", raw.get("label"))
        if isinstance(show, str) and show.strip().lower() not in ("true", "false", "yes", "no"):
            out["label"] = text_of(show, 20)
        elif flag(show):
            out["label"] = name.replace("_", " ").title()[:20]
        return out

    def _hud(self, raw: dict) -> dict | None:
        op = str(raw.get("op", "")).strip().lower()
        out = {"objective": text_of(raw.get("objective", raw.get("text") if op == "objective" else None), 120),
               "banner": text_of(raw.get("banner", raw.get("text") if op in ("banner", "hud") else None), 90),
               "seconds": num(raw.get("seconds"), 0.5, 15), "color": color(raw.get("color"))}
        show = raw.get("show")
        if isinstance(show, (list, tuple, str)):
            listed = [show] if isinstance(show, str) else show
            out["show"] = [n for n in (name_of(v)[:24] for v in listed[:8]) if n] or None
        if flag(raw.get("hide")) or raw.get("objective") == "":
            out["hide"] = True
        board = raw.get("board") if isinstance(raw.get("board"), dict) else raw if op == "scoreboard" else None
        if isinstance(board, dict):
            listed = board.get("show") if isinstance(board.get("show"), (list, tuple)) else [board.get("show")] if board.get("show") else []
            out["board"] = {k: v for k, v in {
                "id": name_of(board.get("id")) or "board", "at": vec(board.get("at"), 3, -self.limit, self.limit) or [0.0, 2.5, 8.0],
                "rot_y": num(board.get("rot_y"), -360, 360), "size": num(board.get("size"), 0.3, 12, 1.5),
                "title": text_of(board.get("title"), 30), "show": [n for n in (name_of(v)[:24] for v in listed[:8]) if n] or None,
            }.items() if v is not None}
        out = {k: v for k, v in out.items() if v is not None}
        return out if any(k in out for k in ("objective", "banner", "show", "hide", "board")) else None

    def _stage(self, raw: dict) -> dict | None:
        go = name_of(raw.get("go"))
        identity = name_of(raw.get("id") or raw.get("name"))
        if go and not identity:
            return {"go": go}
        if not identity:
            return None
        enter = raw.get("enter", raw.get("do")) or []
        enter = [a for a in (self._action_v3(r) for r in (enter if isinstance(enter, list) else [enter])[:10]) if a]
        return {"id": identity, "goal": text_of(raw.get("goal") or raw.get("objective"), 120), "enter": enter or None,
                "next": name_of(raw.get("next")) or None, "timeout": num(raw.get("timeout", raw.get("seconds")), 1, 3600),
                "fail": name_of(raw.get("fail")) or None, "first": True if flag(raw.get("first", raw.get("start"))) else None}

    def _fx(self, raw: dict) -> dict | None:
        kind = pick(raw.get("kind", raw.get("type")), FX)
        if kind is None:
            return None
        out = {"id": name_of(raw.get("id")) or self.world.next_id(kind + "_"), "kind": kind, "size": num(raw.get("size", raw.get("scale")), 0.1, 40),
               "color": color(raw.get("color", raw.get("tint"))), "rate": num(raw.get("rate", raw.get("intensity")), 0, 1)}
        on = name_of(raw.get("on") or raw.get("attach"))
        if on:
            out["on"] = on
            out["offset"] = vec(raw.get("offset", raw.get("at")), 3, -60, 60)
        else:
            out["at"] = vec(raw.get("at"), 3, -self.limit, self.limit) or [0.0, 0.0, 4.0]
        return out

    def _sfx(self, raw: dict) -> dict | None:
        name = name_of(raw.get("name") or raw.get("id"))[:24]
        layers = raw.get("layers", raw.get("synth"))
        if not name or not isinstance(layers, list):
            return None
        out = []
        for layer in layers[:6]:
            if not isinstance(layer, dict):
                continue
            wave = pick(layer.get("wave", layer.get("shape")), WAVES) or {"sawtooth": "saw", "tri": "triangle", "white": "noise", "pink": "noise"}.get(str(layer.get("wave", "")).lower(), "sine")
            out.append({k: v for k, v in {
                "wave": wave, "freq": num(layer.get("freq", layer.get("from")), 20, 12000, 220), "to": num(layer.get("to", layer.get("freq_to")), 20, 12000),
                "dur": num(layer.get("dur", layer.get("seconds")), 0.02, 6, 0.4), "attack": num(layer.get("attack"), 0, 2),
                "decay": num(layer.get("decay", layer.get("release")), 0, 4), "vol": num(layer.get("vol", layer.get("volume")), 0, 1, 0.6),
                "vibrato": num(layer.get("vibrato"), 0, 40), "depth": num(layer.get("depth"), 0, 1),
                "lowpass": num(layer.get("lowpass"), 50, 12000), "highpass": num(layer.get("highpass"), 20, 8000),
                "drive": num(layer.get("drive", layer.get("distort")), 0, 1), "delay": num(layer.get("delay", layer.get("at")), 0, 4),
                "echo": num(layer.get("echo"), 0, 0.8),
            }.items() if v is not None})
        if not out:
            return None
        return {"name": name, "layers": out, "loop": True if flag(raw.get("loop")) else None}

    def _music(self, raw: dict) -> dict | None:
        if flag(raw.get("stop")) or str(raw.get("mood", "")).strip().lower() in ("none", "off", "stop"):
            return {"stop": True}
        out = {"mood": pick(raw.get("mood", raw.get("style")), MOODS), "tempo": num(raw.get("tempo", raw.get("bpm")), 40, 200),
               "energy": num(raw.get("energy", raw.get("intensity")), 0, 1), "volume": num(raw.get("volume"), 0, 1)}
        key = str(raw.get("key") or "").strip()
        if re.match(r"^[A-Ga-g][#b]?m?$", key):
            out["key"] = key[0].upper() + key[1:]
        if isinstance(raw.get("chords"), (list, tuple)):
            chords = [c.strip() for c in raw["chords"][:8] if isinstance(c, str) and _CHORD.match(c.strip())]
            out["chords"] = chords or None
        if isinstance(raw.get("instruments"), (list, tuple)):
            out["instruments"] = [i for i in (pick(v, INSTRUMENTS) for v in raw["instruments"][:6]) if i] or None
        out = {k: v for k, v in out.items() if v is not None}
        return out or {"mood": "calm"}

    def _speak(self, raw: dict) -> dict | None:
        text = text_of(raw.get("text") or raw.get("line"), 240)
        if not text:
            return None
        return {"id": name_of(raw.get("id") or raw.get("who") or raw.get("npc")) or None, "text": text, "voice": pick(raw.get("voice"), VOICES)}

    def _narrate(self, raw: dict) -> dict | None:
        text = text_of(raw.get("text"), 300)
        return {"text": text, "voice": pick(raw.get("voice"), VOICES)} if text else None

    def _story(self, raw: dict) -> dict | None:
        out = {"title": text_of(raw.get("title"), 60), "premise": text_of(raw.get("premise") or raw.get("text"), 300),
               "goal": text_of(raw.get("goal"), 160)}
        return out if any(out.values()) else None

    # ---- rules

    def _sound_name(self, value, default: str | None = "pop") -> str | None:
        name = name_of(value)
        return name if name in SOUNDS or name in self.world.sounds else default

    def _where(self, value):
        """A place an action names: a position, or the id of something that is somewhere."""
        at = vec(value, 3, -self.limit, self.limit)
        if at is not None:
            return at
        return name_of(value) or None if isinstance(value, str) else None

    def _action_v3(self, raw) -> dict | None:
        if isinstance(raw, str):
            raw = {"act": raw}
        if not isinstance(raw, dict):
            return None
        said = str(raw.get("act") or raw.get("action") or "").strip().lower().replace(" ", "_")
        act = said if said in ACTIONS else ACTION_ALIASES.get(said)
        if act is None:
            return None
        if act in LEGACY_ACTIONS:
            out = self._action({**raw, "act": act})
            if out is None:
                return None
            if act == "sound":
                out["name"] = self._sound_name(raw.get("name") or raw.get("sound"))
            elif act == "spawn":
                out["count"] = int(num(raw.get("count"), 1, 12, 1))
                out.update({k: v for k, v in {
                    "behavior": pick(raw.get("behavior"), BEHAVIORS), "at": self._where(raw.get("at")),
                    "near": "player" if str(raw.get("near", "")).strip().lower() in ("player", "user", "me") else None,
                    "health": num(raw.get("health"), 1, 100000), "team": name_of(raw.get("team"))[:16] or None,
                    "speed": num(raw.get("speed"), 0.1, 6), "damage": num(raw.get("damage"), 0, 1000),
                    "max": num(raw.get("max"), 1, 40), "id": name_of(raw.get("id")) or None,
                    "radius": num(raw.get("radius"), 0, 60),
                }.items() if v is not None})
            elif act == "explode":
                out.update({k: v for k, v in {"damage": num(raw.get("damage"), 0, 100000), "radius": num(raw.get("radius"), 0.5, 40)}.items() if v is not None})
            if act not in ("spawn", "score", "say") and raw.get("target"):
                out["target"] = name_of(raw.get("target"))     # done to that thing, in place of the one it happened to
        else:
            out = {"act": act, "delay": num(raw.get("delay"), 0, 120)}
            if act in ("set", "add"):
                name = name_of(raw.get("var") or raw.get("name"))[:24]
                if not name:
                    return None
                out["var"] = name
                value = raw.get("to", raw.get("value")) if act == "set" else raw.get("by", raw.get("value", raw.get("add", 1)))
                if isinstance(value, str):
                    written = expr.tidy(value)
                    if written is not None:
                        out["to" if act == "set" else "by"] = written
                    elif act == "set":
                        out["text"] = text_of(value, 60)
                    else:
                        out["by"] = 1.0
                else:
                    out["to" if act == "set" else "by"] = num(value, -1e9, 1e9, 0.0 if act == "set" else 1.0)
            elif act in ("banner", "win", "lose"):
                out["text"] = text_of(raw.get("text") or raw.get("banner"), 90)
                if act == "banner" and not out["text"]:
                    return None
                out["seconds"] = num(raw.get("seconds"), 0.5, 15)
                out["color"] = color(raw.get("color"))
                out["sound"] = self._sound_name(raw.get("sound"), None)
            elif act == "objective":
                out["text"] = text_of(raw.get("text"), 120) or ""
            elif act == "reset":
                out["target"] = name_of(raw.get("target") or raw.get("id") or raw.get("group")) or None
            elif act == "teleport":
                out["who"] = name_of(raw.get("who") or raw.get("target"))[:40] or None
                out["to"] = self._where(raw.get("to", raw.get("at")))
                out["face"] = num(raw.get("face", raw.get("rot_y")), -360, 360)
                if out["to"] is None:
                    return None
            elif act == "give":
                found = self.asset(raw.get("asset"), strict=False)
                if not found or not self.knows(found):
                    return None
                out["asset"] = found
                out["tint"] = color(raw.get("tint", raw.get("color")))
            elif act == "take":
                out["asset"] = name_of(raw.get("asset")) or "all"
            elif act == "stage":
                out["go"] = name_of(raw.get("go") or raw.get("to") or raw.get("stage") or raw.get("id")) or "next"
            elif act in ("damage", "heal"):
                heavy = said == "kill"
                out["amount"] = num(raw.get("amount", raw.get("by")), 0, 100000, 100000.0 if heavy else 1.0 if act == "damage" else 25.0)
                out["who"] = name_of(raw.get("who") or raw.get("target")) or None
                out["radius"] = num(raw.get("radius"), 0, 40)
            elif act in ("enable", "disable"):
                out["rule"] = name_of(raw.get("rule") or raw.get("id") or raw.get("target"))
                if not out["rule"]:
                    return None
            elif act in ("speak", "narrate"):
                out["text"] = text_of(raw.get("text") or raw.get("line"), 240 if act == "speak" else 300)
                if not out["text"]:
                    return None
                out["voice"] = pick(raw.get("voice"), VOICES)
                if act == "speak":
                    out["id"] = name_of(raw.get("id") or raw.get("who")) or None
            elif act == "music":
                out.update(self._music(raw) or {})
            elif act == "fade":
                out["seconds"] = num(raw.get("seconds"), 0.2, 6)
                out["color"] = color(raw.get("color"))
            elif act == "slowmo":
                out["scale"] = num(raw.get("scale", raw.get("speed")), 0.1, 1, 0.3)
                out["seconds"] = num(raw.get("seconds"), 0.2, 10, 2.0)
            elif act == "shake":
                out["amount"] = num(raw.get("amount"), 0, 1, 0.5)
            elif act == "door":
                out["id"] = name_of(raw.get("id") or raw.get("door"))
                unlocking = said in ("unlock", "open")
                out["state"] = pick(raw.get("state"), ("open", "close", "toggle", "lock", "unlock"), "open" if unlocking else "toggle")
                if not out["id"]:
                    return None
            elif act == "modify":
                out["target"] = name_of(raw.get("target") or raw.get("id")) or None
                changed = {"tint": color(raw.get("tint", raw.get("color"))), "scale": num(raw.get("scale"), 0.02, 60),
                           "motion": pick(raw.get("motion"), ("none", "bob", "spin", "hover", "sway", "orbit", "drift"))}
                self._modify_v3(raw, changed)
                changed = {k: v for k, v in changed.items() if v is not None}
                if not changed:
                    return None
                out.update(changed)
            elif act == "checkpoint":
                out["at"] = vec(raw.get("at"), 3, -self.limit, self.limit)
            elif act == "direct":
                out["note"] = text_of(raw.get("note") or raw.get("text"), 240)
                if not out["note"]:
                    return None
            elif act == "fx":
                out["kind"] = pick(raw.get("kind"), FX, "sparks")
                out["seconds"] = num(raw.get("seconds"), 0.5, 120, 6.0)
                out["size"] = num(raw.get("size"), 0.1, 40)
                out["color"] = color(raw.get("color"))
            elif act == "ride":
                wanted = str(raw.get("asset", "")).strip().lower()
                if wanted in ("none", "", "off"):
                    out["asset"] = "none"
                else:
                    found = self.asset(raw.get("asset"), strict=False)
                    if not found or not self.knows(found):
                        return None
                    out["asset"] = found
                    out["drive"] = pick(raw.get("drive"), DRIVES)
        if raw.get("if") is not None:
            out["if"] = expr.tidy(raw.get("if"))
        return {k: v for k, v in out.items() if v is not None}

    def _when_v3(self, raw: dict) -> dict | None:
        said = str(raw.get("event") or raw.get("on") or "").strip().lower().replace(" ", "_")
        event = said if said in EVENTS else EVENT_ALIASES.get(said)
        test = expr.tidy(raw.get("test", raw.get("when", raw.get("expr"))))
        if event is None and test is not None:
            event = "becomes"
        actions = raw.get("do") or raw.get("actions") or raw.get("then") or []
        actions = [a for a in (self._action_v3(r) for r in (actions if isinstance(actions, list) else [actions])[:12]) if a]
        if event is None or not actions:
            return None
        condition = expr.tidy(raw.get("if")) if raw.get("if") is not None else None
        if event == "becomes":
            test = test or condition
            condition = None if test == condition else condition
            if test is None:
                return None
        target = name_of(raw.get("target") or raw.get("who") or raw.get("asset"))
        zone = name_of(raw.get("zone") or raw.get("in"))
        if event in ("enter", "exit"):
            # written either way round: `target` the zone, or `zone` the zone and `target` who goes into it
            if not zone and target in self.world.zones():
                zone, target = target, ""
            if not zone:
                return None
            target = target or "player"
        out = {"id": name_of(raw.get("id")) or self.world.next_id("rule_"), "event": event, "target": target or "any",
               "zone": zone or None, "with": name_of(raw.get("with")) or None if event == "collide" else None,
               "test": test if event == "becomes" else None, "if": condition, "stage": name_of(raw.get("stage")) or None,
               "seconds": num(raw.get("seconds", raw.get("every")), 0.2, 3600) if event == "every" else None,
               "radius": num(raw.get("radius"), 0.3, 200) if event == "near" else None,
               "once": True if flag(raw.get("once")) else None, "cooldown": num(raw.get("cooldown"), 0, 600),
               "times": num(raw.get("times"), 1, 9999), "do": actions}
        return out
