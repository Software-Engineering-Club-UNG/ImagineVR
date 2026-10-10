"""The offline director: builds a world from keywords, with no AI.

Used when there is no OpenRouter key, when the model is set to "offline", and as a safety
net when the AI provider fails before writing anything. It only knows the catalog, so its
output goes through the same validation as the model's.
"""

from __future__ import annotations

import difflib
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
    (("spooky", "creepy", "scary", "eerie", "haunted"), ("spooky", "moonlit")),
    (("fog", "foggy", "mist", "misty"), ("foggy_morning", "overcast")),
    (("kill the lights", "kill the light", "lights off", "turn off the lights", "turn the lights off", "lights out"), ("night_stars", "moonlit")),
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
GIVE = re.compile(r"\b(give me|hand me|make me|i want to hold|let me hold|i need|can i have|can i get|could i have|could i get|gib me|i want)\b")
MORE = re.compile(r"\b(more|add|another|some|lots of|a few|many|bunch of)\b")
REMOVE = re.compile(r"\b(remove|delete|get rid of|take away|no more|without)\b")
CLEAR = re.compile(r"\b(clear (it|everything|the world|all)|start over|start again|reset|back to (the )?dark|empty (it|the world)|(get rid of|remove|delete|erase|wipe) (everything|it all|all of it|the world))\b")
SEVERAL = re.compile(r"\b(more|some|lots of|a few|many|bunch of|several|loads of|a lot of)\b")
BIGGER = re.compile(r"\b(bigger|larger|huge|giant|enormous|taller|grow)\b")
SMALLER = re.compile(r"\b(smaller|tiny|little|shorter|shrink)\b")
FEWER = re.compile(r"\b(fewer|less|not so many|too many)\b")
ADJUST = re.compile(r"\b(make|turn|let) it\b|\b(change|make|turn|set) the sky\b|\blights? (off|out)\b|\bthe lights?\b")
ABOUT_LIMITS = re.compile(r"\b(save|load|undo|redo|a limit|how (much|many)|multiplayer|my friends? join)\b")
DECLINE = re.compile(r"\b(bomb|bombs|explosive|explosives|blow up|naked|nude|porn|sex|sexy|hitler|nazi|nazis|kkk|suicide|terrorist|rape|torture|behead|drugs|cocaine|meth)\b")
EVERYDAY = frozenset("""hello there where their about would could should think thing things going really around
    something please right great other these those which while world every never always maybe still again being
    doing having looks stuff place little night light today sound sounds first three seven eight front behind
    under above after before whole stairs outside middle video honestly maybe build create remove change bigger smaller fewer quiet scary happy""".split())
THANKS = re.compile(r"^(thanks|thank you|cheers|ta|nice one|good job|well done|perfect|love it)\b")
SPOKEN = ("snowing", "raining", "sunset", "sunrise", "night", "morning", "gravity", "bigger", "smaller", "remove", "zombie", "zombies")

# --- things people say that are not requests
GREETING = re.compile(r"^(hi|hello|hey|heya|hiya|yo|sup|howdy|greetings|good (morning|afternoon|evening|night)|thanks|thank you|cheers|bye|goodbye|see you|what s up|whats up|how are you|how s it going|hows it going|how is it going|nice to meet you)\b")
ABOUT = re.compile(r"\b(what can (you|i) (do|say|ask|make|build)|what do you do|who are you|what are you|what is this|how does this work|how do i|can you hear me|are you there|what should i (say|do))\b|^help( me)?$")
QUESTION = re.compile(r"^(what|why|how|who|when|where|which|is|are|am|do|does|did|was|were|will|would|should|have|has)\b")
INTENT = re.compile(r"\b(i want|i wanna|i d like|i would like|i need|give me|hand me|get me|make|show me|put|take me|bring|build|create|add|spawn|let s|lets|let me|can i|can you|can we|could you|could i|could we|will you|please|more|another|some|lots of|go to|teleport|remove|delete|get rid|drive|ride|fly|sail|imagine|picture|how about|what about|generate|surround|fill|i m in|im in|start over|reset|clear)\b")
CHATTER = re.compile(r"\b(my|he|she|they|we|you|your|his|her|their|our|yesterday|tomorrow|today|last (week|night|year|time)|did|was|were|said|says|told|think|thought|remember|went|guess|know|knew|heard|saw|feel|felt|should have|supposed|i m|im|i am|gonna|talking|listen|that s|dude|bro|haha|lol|ugh|dang|shh|no way|look|looked|looks|video)\b")
FILLER = frozenset("""um uh er erm hmm hm mm oh ah aha wow whoa woah cool nice awesome amazing sick dope lol haha ha hehe ok okay
    yeah yep yes yup no nope nah wait so well this that is it s its really very pretty great good neat sweet man dude
    bro wild crazy insane incredible beautiful gorgeous love i like alright right one sec second hold on hang test
    testing check mic never mind nevermind damn dang holy god gosh my the a an just and looks look at too much
    huh what hm fine sure whatever sorry oops nothing here there we go all done yay""".split())

# --- getting about, and picking a fight
RIDE = re.compile(r"\b(take me for a (spin|ride|drive|flight)|drive|driving|ride|riding|get in|get into|hop in|hop on|jump in|get on|climb (in|on|into|onto)|pilot|steer|race|racing|cruise|(i|me|us|we) (want to |wanna |can |could |get to |d like to |would like to )?fly|let (me|us|s) fly|fly (a|an|the|my|me|around|over|through|away|up)|sail|sailing|behind the wheel|take a (drive|ride|spin|flight)|go for a (drive|ride|spin|flight))\b")
STOP_RIDE = re.compile(r"\b(let me (out|off)|i want (out|off)|get me out|get out|get off|hop out|hop off|climb (out|off)|stop (driving|riding|flying|sailing|the car)|park (it|the car|here)|on foot|i want to walk|let me walk|dismount)\b")
FIGHT = re.compile(r"\b(shoot|shooting|fight|fighting|battle|kill|slay|defend|blast|attack|attacked|attacking|survive|hunt|hunting|destroy|defeat|take on)\b")
CREATURES = ("people", "animals")
RIDEABLE = ("vehicles", "animals", "sea")

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


def _small_talk(t: str) -> list[dict] | None:
    """A reply for things that are not requests, so that chatter never builds anything."""
    tokens = t.split()
    if not tokens or all(w in FILLER for w in tokens):
        return [{"op": "outcome", "value": "ignored"}]
    if DECLINE.search(t):
        return [{"op": "say", "text": "That's not something I'll build. How about a place to explore instead?"},
                {"op": "outcome", "value": "declined"}]
    if ABOUT_LIMITS.search(t) and (QUESTION.search(t) or re.search(r"^can (i|you|we)\b", t)):
        return [{"op": "say", "text": "Not yet. For now I build one world at a time, and it lasts as long as you're in it."}]
    if ABOUT.search(t):
        return [{"op": "say", "text": "Tell me a place and I'll build it around you, then change anything you like. "
                                      "Ask for something to hold, creatures to face, or a car to drive."}]
    if INTENT.search(t):
        return None
    if THANKS.search(t):
        return [{"op": "say", "text": "Anytime."}]
    if GREETING.search(t):
        return [{"op": "say", "text": "Hey. Tell me a place and I'll build it around you."}]
    if QUESTION.search(t):
        return [{"op": "say", "text": "I can't answer that one right now. Name a place and I'll build it."}]
    if CHATTER.search(t):
        return [{"op": "outcome", "value": "ignored"}]
    return None


_VOCAB: dict[int, set[str]] = {}


def _heard(t: str, catalog: Catalog) -> str:
    """Nudges misheard or misspelt words onto words the catalog knows ("gitar" -> "guitar")."""
    vocab = _VOCAB.get(id(catalog))
    if vocab is None:
        spoken = {w for phrases, _ in SKY_WORDS + WEATHER_WORDS for phrase in phrases for w in phrase.split()}
        vocab = _VOCAB[id(catalog)] = catalog.vocabulary() | set(SPOKEN) | spoken
    fixed = []
    for w in t.split():
        if len(w) >= 5 and w not in EVERYDAY and w not in vocab and w.rstrip("s") not in vocab and not INTENT.fullmatch(w) and w not in FILLER:
            close = difflib.get_close_matches(w, [v for v in vocab if v[0] == w[0]], n=1, cutoff=0.84)
            w = close[0] if close else w
        fixed.append(w)
    return " ".join(fixed)


def _named(catalog: Catalog, t: str, categories, holdable: bool = False) -> str | None:
    """The best asset the text names outright (id or alias, whole words) from the given categories."""
    spaced = f" {t} "
    for asset_id, _ in catalog.search("asset", t, limit=12):
        entry = catalog.get("asset", asset_id) or {}
        if holdable != bool(entry.get("holdable")) or (categories and entry.get("category") not in categories):
            continue
        names = [asset_id.replace("_", " ")] + [a.replace("_", " ") for a in entry.get("aliases", [])]
        if any(f" {n} " in spaced or f" {n}s " in spaced or f" {n}es " in spaced for n in names):
            return asset_id
    return None


def _without(t: str, catalog: Catalog, asset_id: str | None, pattern: re.Pattern | None = None) -> str:
    """The text with one asset's names (and a pattern) taken out, to see what else was asked for."""
    if pattern is not None:
        t = pattern.sub(" ", t)
    if asset_id:
        entry = catalog.get("asset", asset_id) or {}
        for name in [asset_id.replace("_", " ")] + [a.replace("_", " ") for a in entry.get("aliases", [])]:
            t = re.sub(rf"\b{re.escape(name)}(s|es)?\b", " ", t)
    return " ".join(t.split())


def _place(catalog: Catalog, t: str, world: WorldState, rng: random.Random, sky_preset, weather, gravity,
           kit_id: str, params: dict | None = None) -> list[dict]:
    """Statements that build one kit as a whole place."""
    out: list[dict] = []
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
    kit = {"op": "kit", "id": kit_id, "kit": kit_id, "density": 0.6, "size": 0.6, "seed": rng.randint(1, 9999)}
    if params:
        kit["params"] = params
    out.append(kit)
    kit_weather = weather or defaults.get("weather")
    if kit_weather and catalog.has("weather", kit_weather):
        out.append({"op": "weather", "type": kit_weather, "intensity": 0.5})
    if defaults.get("ambience") and catalog.has("ambience", defaults["ambience"]):
        out.append({"op": "ambience", "loop": defaults["ambience"]})
    return out


def _ground(catalog: Catalog, world: WorldState) -> list[dict]:
    """Somewhere to stand, when nothing has been built yet."""
    if "terrain" in world.globals:
        return []
    return [{"op": "sky", "preset": _first_existing(catalog, "sky", ("morning", "clear_noon")) or catalog.ids("sky")[0], "t": 3},
            {"op": "terrain", "type": "rolling" if catalog.has("terrain", "rolling") else catalog.ids("terrain")[0], "t": 3}]


def compose(text: str, catalog: Catalog, world: WorldState) -> list[dict]:
    t = " ".join(words(text))
    chat = _small_talk(t)   # judged on what was actually said, before any spelling repair
    if chat is not None:
        return chat
    t = _heard(t, catalog)
    rng = random.Random(hash(t) & 0xFFFF)
    out: list[dict] = []

    if CLEAR.search(t) and not GIVE.search(t):
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
    stop_weather = bool(re.search(r"\b(stop|no more|clear up|remove|get rid of|take away)\b.*\b(rain|raining|snow|snowing|weather)\b", t))
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
    if REMOVE.search(t) and not stop_weather and not (weather and re.search(r"\bno more\b", t)):
        return [{"op": "say", "text": "I can't pick those out on their own right now. Say \"start over\" to clear everything."
                 if world.things else "There's nothing here to take away yet."}]

    # --- "make the trees red": a change to something that is already there
    if re.search(r"^(now |please |can you |could you )?(make|turn|paint|colou?r) (the|those|these|all the)\b", t) and not world.is_empty():
        hit_ids = {a for a, _ in catalog.search("asset", t, limit=3)}
        targets = [tid for tid, stmt in world.things.items() if stmt.get("asset") in hit_ids]
        shade = _color_in(t)
        if targets and shade:
            return [{"op": "modify", "id": tid, "tint": shade} for tid in targets] + [{"op": "say", "text": "Done."}]
        if not targets and not (BIGGER.search(t) or SMALLER.search(t)):
            return [{"op": "say", "text": "I can only change things I placed one by one. Try asking for the whole place again."}]

    # --- resizing and thinning out what was placed
    resize = 1.6 if BIGGER.search(t) else 0.6 if SMALLER.search(t) else None
    if (resize or FEWER.search(t)) and not GIVE.search(t):
        hit_ids = {a for a, _ in catalog.search("asset", t, limit=3)}
        targets = [(tid, stmt) for tid, stmt in world.things.items() if stmt.get("asset") in hit_ids]
        for tid, stmt in targets:
            if resize:
                current = stmt.get("scale", 1.0)
                current = sum(current) / len(current) if isinstance(current, list) else current
                out.append({"op": "modify", "id": tid, "scale": round(current * resize, 2)})
            elif stmt["op"] == "scatter":
                out.append({"op": "modify", "id": tid, "count": max(1, int(stmt.get("count", 12)) // 2)})
        if out:
            return out + [{"op": "say", "text": "Done."}]
        if world.things or not world.is_empty():
            return [{"op": "say", "text": "I can only change things I placed one by one. Try asking for the whole place again."}]

    # --- getting out of whatever the user is riding
    if STOP_RIDE.search(t) and "ride" in world.globals:
        return [{"op": "ride", "asset": "none"}, {"op": "say", "text": "Back on your feet."}]

    riding = world.globals.get("ride")
    if riding:
        pace = 1.6 if re.search(r"\b(faster|speed up|floor it|hurry)\b", t) else 0.6 if re.search(r"\b(slower|slow down)\b", t) else None
        if pace:
            speed = max(0.2, min(3.0, round(float(riding.get("speed", 1.0)) * pace, 2)))
            return [{**{k: v for k, v in riding.items() if k != "t"}, "speed": speed}, {"op": "say", "text": "Faster it is." if pace > 1 else "Easing off."}]
        swap = _named(catalog, t, RIDEABLE) if re.search(r"\b(change|switch|swap|trade)\b", t) else None
        if swap:
            return [{"op": "ride", "asset": swap}, {"op": "say", "text": f"Swapped for the {swap.replace('_', ' ')}."}]

    # --- driving, riding, flying: optionally somewhere new ("drive a car through the desert")
    # "put me in a car" is a ride; "put me in a city" is a place
    if RIDE.search(t) or (re.search(r"\bput me (in|on)\b", t) and _named(catalog, t, RIDEABLE)):
        vehicle = _named(catalog, t, RIDEABLE)
        if vehicle is None:   # a few things from other shelves that are plainly for riding
            vehicle = next((v for v in ("airplane", "hot_air_balloon", "rocket", "ufo", "skateboard")
                            if re.search(rf"\b{v.replace('_', ' ')}s?\b", t) and catalog.has("asset", v)), None)
        if vehicle is None:
            flying = re.search(r"\b(fly|flying|flight|pilot)\b", t)
            vehicle = _first_existing(catalog, "asset", ("airplane",) if flying else ("car",)) or "car"
        rest = _without(t, catalog, vehicle, RIDE)
        place = catalog.search("kit", rest, limit=1)
        if place and place[0][1] >= 3.0:
            out += _place(catalog, rest, world, rng, sky_preset, weather, gravity, place[0][0])
        else:
            out += _ground(catalog, world)
            if sky_preset:
                out.append({"op": "sky", "preset": sky_preset, "t": 4})
        out.append({"op": "ride", "asset": vehicle})
        out.append({"op": "say", "text": f"The {vehicle.replace('_', ' ')} is yours. Push the stick to drive, or use W A S D."})
        return out

    if re.search(r"\b(make|tell) (them|it|him|her) stop\b|\bcall (them|it) off\b", t):
        horde = [tid for tid, stmt in world.things.items() if stmt.get("behavior") or stmt.get("kit") == "last_stand"]
        if horde:
            return [{"op": "remove", "id": tid} for tid in horde] + [{"op": "say", "text": "They're gone."}]

    # --- something to fight: creatures that come at the user, and something to fight them with
    enemy = _named(catalog, t, CREATURES) if FIGHT.search(t) else None
    if enemy:
        weapon = _named(catalog, _without(t, catalog, enemy), None, holdable=True) or _first_existing(catalog, "asset", ("blaster",))
        rest = _without(t, catalog, enemy, FIGHT)
        rest = _without(rest, catalog, weapon)
        place = catalog.search("kit", rest, limit=1)
        arena = "last_stand" if catalog.has("kit", "last_stand") else None
        if place and place[0][1] >= 3.0 and place[0][0] != arena:
            out += _place(catalog, rest, world, rng, sky_preset, weather, gravity, place[0][0])
            arena = None
        elif arena and (world.is_empty() or NEW_PLACE.search(t)):
            out += _place(catalog, rest, world, rng, sky_preset, weather, gravity, arena, {"enemy": enemy})
        else:
            out += _ground(catalog, world)
            arena = None
        if arena is None:
            out.append({"op": "scatter", "id": world.next_id("scatter", enemy), "asset": enemy, "count": _count_in(t) or 8,
                        "area": "front", "radius": [8, 20], "behavior": "chase"})
        armed = any(s["op"] == "give" and s.get("asset") == weapon for s in world.things.values())
        if weapon and not armed:
            out.append({"op": "give", "id": world.next_id("give", weapon), "asset": weapon})
        plural = enemy.replace("_", " ") + "s"
        tool = (weapon or "").replace("_", " ")
        out.append({"op": "say", "text": f"The {plural} are coming. Grab the {tool} and take them down." if weapon and not armed
                    else f"More {plural}, coming your way."})
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
    tweak = bool(sky_preset or weather or gravity is not None)
    adjusting = stop_weather or (bool(ADJUST.search(t)) and tweak and (not world.is_empty() or "light" in t))
    if best_kit and (best_kit[1] >= 3.0) and not adjusting and not (MORE.search(t) and best_asset and best_asset[1] > best_kit[1]):
        kit_id = best_kit[0]
        entry = catalog.get("kit", kit_id) or {}
        out += _place(catalog, t, world, rng, sky_preset, weather, gravity, kit_id)
        blaster = _first_existing(catalog, "asset", ("blaster",))
        if kit_id == "last_stand" and blaster and not any(s["op"] == "give" for s in world.things.values()):
            out.append({"op": "give", "id": world.next_id("give", blaster), "asset": blaster})
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
    if best_asset and best_asset[1] >= 3.0 and not adjusting:
        asset_id = best_asset[0]
        entry = catalog.get("asset", asset_id) or {}
        count = _count_in(t)
        plural = bool(SEVERAL.search(t)) or (count or 0) > 1 or re.search(rf"\b{re.escape(asset_id.split('_')[-1])}(s|es)\b", t)
        if not out:
            out += _ground(catalog, world)
        if plural:
            stmt = {"op": "scatter", "id": world.next_id("scatter", asset_id), "asset": asset_id,
                    "count": count or (40 if entry.get("size_m", 1) < 8 else 12), "area": "around"}
        else:
            stmt = {"op": "spawn", "id": world.next_id("spawn", asset_id), "asset": asset_id,
                    "place": "front", "distance": max(4.0, float(entry.get("size_m", 1)) * 1.5)}
        if entry.get("prop") and re.search(r"\b(throw|kick|hit|push|play|bounce|knock|stack)\b", t):
            stmt["physics"] = True
        if entry.get("category") in CREATURES and re.search(r"\b(away|scared|shy|flee\w*|wander\w*|roam\w*|walk\w*|run|runs|running|moving|move|alive|graz\w*|follow\w*|chas\w*|come|comes|coming)\b", t):
            stmt["behavior"] = ("flee" if re.search(r"\b(run(s|ning)? away|stay(s)? away|keep(s)? away|scared|shy|flee\w*|bolt\w*)\b", t)
                                else "chase" if re.search(r"\b(follow\w*|chas\w*|come|comes|coming)\b", t) else "wander")
        if color:
            stmt["tint"] = color
        out.append(stmt)

    if not out and len(t.split()) > 4 and not re.search(r"\b(i want|i wanna|i d like|i would like|i need|give me|hand me|get me|make|show me|put|take me|build|create|add|spawn|can you|could you)\b", t):
        return [{"op": "outcome", "value": "ignored"}]   # a long remark with no request in it: nothing is the answer
    if not out:
        return [{"op": "say", "text": "I'm running without my AI model right now, so I only understand simple "
                                      "requests. Try naming a place, like a city, a forest, or a golf course."},
                {"op": "outcome", "value": "not_understood"}]
    out.append({"op": "say", "text": "Done."})
    return out
