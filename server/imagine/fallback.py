"""The offline director: builds a world from keywords, with no AI.

Used when there is no OpenRouter key, when the model is set to "offline", and as a safety
net when the AI provider fails before writing anything. It only knows the catalog, so its
output goes through the same validation as the model's.
"""

from __future__ import annotations

import random
import re

from .catalog import Catalog, words
from .script import NAMED_COLORS, WorldState

# spoken word -> sky preset id (first existing one wins)
SKY_WORDS = [
    (("sunset", "sundown", "golden hour", "evening"), ("golden_hour", "sunset_pink")),
    (("sunrise", "dawn", "early morning"), ("dawn", "morning")),
    (("morning",), ("morning", "dawn")),
    (("midnight", "night", "nighttime", "dark", "stars", "starry"), ("night_stars", "moonlit")),
    (("moon", "moonlight", "moonlit"), ("moonlit", "night_stars")),
    (("noon", "midday", "daytime", "sunny", "bright", "clear sky"), ("clear_noon",)),
    (("dusk", "twilight"), ("dusk", "golden_hour")),
    (("overcast", "cloudy", "grey sky", "gray sky"), ("overcast",)),
    (("storm", "stormy", "thunder", "lightning"), ("stormy", "overcast")),
    (("aurora", "northern lights"), ("aurora_night", "night_stars")),
    (("alien", "another planet"), ("alien_dusk",)),
]

WEATHER_WORDS = [
    (("rain", "raining", "rainy", "storm", "stormy"), "rain"),
    (("snow", "snowing", "snowy", "blizzard"), "snow"),
    (("fireflies", "firefly"), "fireflies"),
    (("petals", "cherry blossom", "blossoms"), "petals"),
    (("falling leaves", "autumn", "fall leaves"), "leaves"),
    (("embers", "sparks"), "embers"),
    (("bubbles",), "bubbles"),
    (("dust", "sandstorm", "dusty"), "dust"),
]

GRAVITY_WORDS = [
    (("zero gravity", "no gravity", "weightless", "zero g"), 0.0),
    (("low gravity", "moon gravity", "less gravity", "lighter"), 0.16),
    (("normal gravity", "earth gravity", "regular gravity"), 1.0),
    (("high gravity", "heavy gravity", "more gravity"), 1.8),
]

NEW_PLACE = re.compile(r"\b(put me|take me|i want to (see|be|go)|i'd like to (see|be|go)|show me|bring me|go to|now a|let's go|teleport|transport|i wanna (see|be|go))\b")
GIVE = re.compile(r"\b(give me|hand me|make me|i want to hold|let me hold|i need|can i have|can i get|i want)\b")
MORE = re.compile(r"\b(more|add|another|some|lots of|a few|many|bunch of)\b")
REMOVE = re.compile(r"\b(remove|delete|get rid of|take away|no more|without)\b")
CLEAR = re.compile(r"\b(clear (it|everything|the world)|start over|reset|back to (the )?dark|empty (it|the world))\b")

NUMBER_WORDS = {
    "one": 1, "two": 2, "three": 3, "four": 4, "five": 5, "six": 6, "seven": 7, "eight": 8, "nine": 9,
    "ten": 10, "twelve": 12, "dozen": 12, "twenty": 20, "thirty": 30, "fifty": 50, "hundred": 100,
}


def _has(text: str, phrases) -> bool:
    return any(re.search(rf"\b{re.escape(p)}\b", text) for p in phrases)


def _first_existing(catalog: Catalog, kind: str, ids) -> str | None:
    for candidate in ids:
        if catalog.has(kind, candidate):
            return candidate
    return None


def _count_in(text: str) -> int | None:
    digits = re.search(r"\b(\d{1,3})\b", text)
    if digits:
        return int(digits.group(1))
    for word, value in NUMBER_WORDS.items():
        if re.search(rf"\b{word}\b", text):
            return value
    return None


def _color_in(text: str) -> str | None:
    for name, value in NAMED_COLORS.items():
        if re.search(rf"\b{name}\b", text):
            return value
    return None


def compose(text: str, catalog: Catalog, world: WorldState) -> list[dict]:
    t = " ".join(words(text))
    rng = random.Random(hash(t) & 0xFFFF)
    out: list[dict] = []

    if CLEAR.search(t):
        return [{"op": "clear"}, {"op": "sky", "preset": _first_existing(catalog, "sky", ("void",)) or "void"},
                {"op": "say", "text": "Back to a blank page."}]

    # --- sky / time of day
    sky_preset = None
    for phrases, presets in SKY_WORDS:
        if _has(t, phrases):
            sky_preset = _first_existing(catalog, "sky", presets)
            if sky_preset:
                break

    # --- weather and gravity
    weather = next((w for phrases, w in WEATHER_WORDS if _has(t, phrases) and catalog.has("weather", w)), None)
    stop_weather = bool(re.search(r"\b(stop|no more|clear up)\b.*\b(rain|snow|weather)\b", t))
    gravity = next((g for phrases, g in GRAVITY_WORDS if _has(t, phrases)), None)

    # --- remove something already in the world
    if REMOVE.search(t) and world.things:
        asset_hits = catalog.search("asset", t, limit=3)
        hit_ids = {a for a, _ in asset_hits}
        removed = [tid for tid, stmt in world.things.items() if stmt.get("asset") in hit_ids or stmt.get("kit") and stmt["kit"] in t]
        if removed:
            out += [{"op": "remove", "id": tid} for tid in removed]
            out.append({"op": "say", "text": "Gone."})
            return out

    kit_hits = catalog.search("kit", t, limit=1)
    asset_hits = catalog.search("asset", t, limit=3)
    best_kit = kit_hits[0] if kit_hits else None
    best_asset = asset_hits[0] if asset_hits else None
    wants_give = bool(GIVE.search(t)) and not NEW_PLACE.search(t)
    color = _color_in(t)

    # --- something to hold
    if wants_give and best_asset and (not best_kit or best_asset[1] >= best_kit[1]):
        entry = catalog.get("asset", best_asset[0]) or {}
        if entry.get("holdable") or re.search(r"\b(hold|hand me|give me)\b", t):
            stmt = {"op": "give", "id": world.next_id("give", best_asset[0]), "asset": best_asset[0]}
            if color:
                stmt["tint"] = color
            out.append(stmt)
            out.append({"op": "say", "text": f"One {best_asset[0].replace('_', ' ')}, right in front of you."})
            return out

    # --- a whole place
    if best_kit and (best_kit[1] >= 3.0) and not (MORE.search(t) and best_asset and best_asset[1] > best_kit[1]):
        kit_id = best_kit[0]
        entry = catalog.get("kit", kit_id) or {}
        defaults = entry.get("defaults") or {}
        if not world.is_empty():
            out.append({"op": "clear"})
        out.append({"op": "sky", "preset": sky_preset or defaults.get("sky") or _first_existing(catalog, "sky", ("clear_noon",)) or catalog.ids("sky")[0], "t": 3})
        terrain = defaults.get("terrain")
        if isinstance(terrain, dict):
            out.append({"op": "terrain", **terrain, "seed": rng.randint(1, 9999), "t": 3})
        if gravity is not None or "gravity" in defaults:
            out.append({"op": "physics", "gravity": gravity if gravity is not None else defaults["gravity"]})
        out.append({"op": "kit", "id": kit_id, "kit": kit_id, "density": 0.6, "size": 0.6, "seed": rng.randint(1, 9999)})
        kit_weather = weather or defaults.get("weather")
        if kit_weather and catalog.has("weather", kit_weather):
            out.append({"op": "weather", "type": kit_weather, "intensity": 0.5})
        if defaults.get("ambience") and catalog.has("ambience", defaults["ambience"]):
            out.append({"op": "ambience", "loop": defaults["ambience"]})
        out.append({"op": "say", "text": entry.get("greeting") or f"Here is your {kit_id.replace('_', ' ')}."})
        return out

    # --- adjustments to the current world
    if sky_preset:
        out.append({"op": "sky", "preset": sky_preset, "t": 4})
    if stop_weather:
        out.append({"op": "weather", "type": "none"})
    elif weather:
        out.append({"op": "weather", "type": weather, "intensity": 0.6})
    if gravity is not None:
        out.append({"op": "physics", "gravity": gravity})

    # --- things by name
    if best_asset and best_asset[1] >= 3.0:
        asset_id = best_asset[0]
        entry = catalog.get("asset", asset_id) or {}
        count = _count_in(t)
        plural = bool(MORE.search(t)) or (count or 0) > 1 or re.search(rf"\b{re.escape(asset_id.split('_')[-1])}(s|es)\b", t)
        if "terrain" not in world.globals and not out:
            out.append({"op": "sky", "preset": _first_existing(catalog, "sky", ("morning", "clear_noon")) or catalog.ids("sky")[0], "t": 3})
            out.append({"op": "terrain", "type": "rolling" if catalog.has("terrain", "rolling") else catalog.ids("terrain")[0], "t": 3})
        if plural:
            stmt = {"op": "scatter", "id": world.next_id("scatter", asset_id), "asset": asset_id,
                    "count": count or (40 if entry.get("size_m", 1) < 8 else 12), "area": "around"}
        else:
            stmt = {"op": "spawn", "id": world.next_id("spawn", asset_id), "asset": asset_id,
                    "place": "front", "distance": max(4.0, float(entry.get("size_m", 1)) * 1.5)}
        if entry.get("prop") and re.search(r"\b(throw|kick|hit|push|play|bounce|knock|stack)\b", t):
            stmt["physics"] = True
        if color:
            stmt["tint"] = color
        out.append(stmt)

    if not out:
        return [{"op": "say", "text": "I'm running without my AI model right now, so I only understand simple "
                                      "requests. Try naming a place, like a city, a forest, or a golf course."},
                {"op": "outcome", "value": "not_understood"}]
    out.append({"op": "say", "text": "Done."})
    return out
