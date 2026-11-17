"""The third version of the language: repaired like the rest, and never shown to a headset that cannot play it."""

from pathlib import Path

from imagine import v3
from imagine.catalog import Catalog
from imagine.language import Checker, WorldState, reframe

CATALOG = Catalog.load(Path(__file__).resolve().parents[2] / "shared" / "catalog.json")


def play(frame=None, world=None, **more):
    return Checker(CATALOG, world or WorldState(), frame, structures=True, wide=True, play=True, gen=True, **more)


def old(world=None):
    return Checker(CATALOG.before(3), world or WorldState())


# ---------------------------------------------------------------------------- an older headset

def test_an_older_headset_is_sent_nothing_it_does_not_know():
    c = old()
    for op in v3.OPS:
        assert c.check({"op": op, "id": "x", "name": "x", "text": "hello", "kind": "fire", "layers": [{}], "objective": "go"}) is None, op
    out = c.check({"op": "spawn", "asset": "zombie", "behavior": "attack", "health": 5, "team": "red", "light": "#FFFFFF", "label": "Bob"})
    assert set(out) == {"op", "id", "asset", "place"}, "attack is not a behavior it has, and the rest are not fields it has"
    assert c.check({"op": "when", "event": "enter", "zone": "cup", "do": [{"act": "remove"}]}) is None
    assert c.check({"op": "when", "event": "hit", "do": [{"act": "set", "var": "a", "to": 1}, {"act": "remove"}]})["do"] == [{"act": "remove"}]
    assert c.check({"op": "player", "scale": 2, "health": 100, "jump": True}) == {"op": "player", "scale": 2.0}
    assert "drive" not in c.check({"op": "ride", "asset": "car", "drive": "car", "grip": 0.5})
    assert c.check({"op": "door", "id": "d", "state": "lock"}) is None      # and no interior to lock a door of


def test_an_older_headset_has_the_catalog_it_was_built_with():
    assert CATALOG.has("asset", "sports_car") and not CATALOG.before(3).has("asset", "sports_car")
    assert "sports_car" not in CATALOG.before(3).render() and "sports_car" in CATALOG.render()
    assert CATALOG.before(3).has("asset", "car") and CATALOG.before(3).has("material", "grass")
    assert not CATALOG.before(3).has("material", "green")
    c = old()
    assert c.check({"op": "spawn", "asset": "helicopter"}) is None and c.missing == ["helicopter"], "so it is made, as it always was"
    assert len(old().check({"op": "make", "name": "big", "parts": [{"shape": "cube"}] * 100})["parts"]) == 48


def test_only_statements_can_be_called_by_name():
    """`_part`, `_action` and friends are helpers of the checker. A model that wrote {"op":"part"} used to reach them."""
    for checker in (play(), old()):
        for op in ("part", "common", "action", "room", "shoots", "action_v3", "common_v3", "light_of", "where", "placed", "check", "_spawn", ""):
            assert checker.check({"op": op, "asset": "car", "shape": "cube"}) is None, op


# ---------------------------------------------------------------------------- things

def test_what_a_thing_may_be_given():
    out = play().check({"op": "spawn", "id": "guard", "asset": "knight", "behavior": "guard", "health": 99999999, "team": "The Red Team!",
                        "damage": -5, "sight": 30, "speed": 99, "mass": 0, "bounce": 2, "surface": "ICE", "label": "x" * 80,
                        "name": "Sir  Bors", "persona": "Gruff.", "says": ["Halt!", 7, ""], "voice": "gruff", "light": "#f80",
                        "breaks": "glass", "finish": "chrome", "pattern": "stripes", "pattern_color": "#000000", "image": "a crest"})
    assert out["behavior"] == "guard" and out["health"] == 100000 and out["team"] == "the_red_team" and out["damage"] == 0
    assert out["speed"] == 6 and out["mass"] == 0.01 and out["bounce"] == 1 and out["surface"] == "ice"
    assert len(out["label"]) == 40 and out["name"] == "Sir Bors" and out["says"] == ["Halt!", "7"]
    assert out["light"] == {"color": "#FF8800", "range": 8.0, "intensity": 1.0} and out["breaks"] == "glass"
    assert out["finish"] == "chrome" and out["pattern"] == "stripes" and out["image"] == "a crest"
    assert play().check({"op": "spawn", "asset": "lantern", "light": {"range": 500, "intensity": 9}})["light"] == {"color": "#FFD9A0", "range": 60.0, "intensity": 4.0}
    assert "breaks" not in play().check({"op": "spawn", "asset": "crate", "breaks": False})
    # a travelling thing's `speed` is metres a second, as it was; a living thing's is how fast it is for what it is
    assert play().check({"op": "spawn", "asset": "car", "path": [[0, 0, 0], [0, 0, 9]], "speed": 30})["speed"] == 30


def test_a_picture_is_only_asked_for_where_it_can_be_fetched():
    no_pictures = Checker(CATALOG, WorldState(), play=True, gen=False)
    assert "image" not in no_pictures.check({"op": "spawn", "asset": "cube", "image": "a poster"})
    assert "image" not in no_pictures.check({"op": "sky", "preset": "dusk", "image": "peaks"})
    assert play().check({"op": "sky", "preset": "dusk", "image": "peaks", "cycle": 99999}) == {"op": "sky", "preset": "dusk", "cycle": 3600.0, "image": "peaks"}


def test_the_world_grows_with_its_ground():
    world = WorldState()
    c = play(world=world)
    assert c.check({"op": "scatter", "asset": "pine_tree", "radius": [0, 5000]})["radius"] == [0, 115]
    c.check({"op": "terrain", "type": "flat", "size": "vast", "regions": [
        {"material": "putting green", "at": [0, 90], "radius": 9, "level": True}, {"material": "sand", "at": [-9, 0, 80], "size": [8, 6], "height": -99},
        {"at": "nowhere"}, "junk"]})
    regions = world.globals["terrain"]["regions"]
    assert [r["material"] for r in regions] == ["green", "sand"] and regions[0]["at"] == [0, 0.0, 90] and regions[1]["height"] == -12
    assert c.check({"op": "scatter", "asset": "pine_tree", "radius": [0, 5000]})["radius"] == [0, 1190]
    assert c.check({"op": "spawn", "asset": "tower", "at": [900, 0, 5000]})["at"] == [900, 0, 1290]
    c.check({"op": "terrain", "type": "hills"})
    assert world.size == "vast", "a different kind of ground keeps its size unless told otherwise"
    assert old().check({"op": "spawn", "asset": "tower", "at": [900, 0, 5000]})["at"] == [200, 0, 200]


# ---------------------------------------------------------------------------- making

def test_parts_that_move_and_have_a_finish():
    out = play().check({"op": "make", "name": "kart", "kind": "vehicle", "drive": "rocket", "seat": [0, 1, 0], "refine": "  a red   kart ",
                        "fires": {"sound": "laser", "damage": 5000, "ammo": 30, "hitscan": True, "blast": 99},
                        "strike": {"power": 99, "loft": 12}, "parts": [
        {"shape": "cylinder", "role": "tyre", "finish": "RUBBER", "mirror": "x"},
        {"shape": "torus", "role": "steering wheel", "pivot": [0, 1, 0]},
        {"shape": "cube", "anim": {"spin": [0, 0, 99999], "swing": [0, 400, 0], "every": 0, "when": "sometimes"}, "pattern": "stripes", "pattern_scale": 500},
        {"shape": "building", "scale": [10, 30, 8], "floors": 500, "style": "brick", "roof": "pagoda", "shop": "true"},
        {"shape": "stairs", "steps": 100}, {"shape": "asset", "asset": "zzzqqq"}, {"shape": "asset", "asset": "barrel"}]})
    assert out["kind"] == "vehicle", "something that drives may shoot and still be what it is"
    assert "drive" not in out and out["seat"] == [0, 1, 0] and out["refine"] == "a red kart"
    assert out["fires"]["sound"] == "laser" and out["fires"]["damage"] == 1000 and out["fires"]["ammo"] == 30 and out["fires"]["blast"] == 15
    assert out["strike"] == {"power": 5.0, "loft": 12.0}
    wheel, steer, spun, tower, stairs, missing, barrel = out["parts"]
    assert wheel["role"] == "wheel" and wheel["finish"] == "rubber" and steer["role"] == "steer" and steer["pivot"] == [0, 1, 0]
    assert spun["anim"] == {"spin": [0, 0, 3600], "swing": [0, 180, 0], "every": 0.05} and spun["pattern_scale"] == 20
    assert tower["shape"] == "building" and tower["floors"] == 90 and tower["style"] == "brick" and "roof" not in tower and tower["shop"] is True
    assert stairs == {"shape": "stairs", "steps": 40} and missing["shape"] == "cube" and barrel == {"shape": "asset", "asset": "barrel"}
    assert len(play().check({"op": "make", "name": "big", "parts": [{"shape": "cube"}] * 500})["parts"]) == v3.MAX_PARTS


# ---------------------------------------------------------------------------- the new statements

def test_a_road():
    c = play()
    out = c.check({"op": "street", "id": "Main St", "shape": "grid", "blocks": [99, 0], "block": [60, 45], "sidewalks": True, "lamps": "yes",
                   "traffic": {"assets": ["car", "hovercraft", "taxi"], "count": 500, "speed": 12}, "walkers": 6,
                   "buildings": {"assets": ["shop", "glass_tower", "nonsense tower thing"], "density": 5}, "checkpoints": 99})
    assert out["op"] == "road" and out["id"] == "main_st" and out["kind"] == "street" and out["blocks"] == [8, 1]
    assert out["traffic"] == {"assets": ["car", "taxi"], "count": 24, "speed": 12} and out["walkers"] == {"count": 6}
    assert out["buildings"]["density"] == 1 and "shop" in out["buildings"]["assets"] and out["checkpoints"] == 12
    drawn = c.check({"op": "road", "kind": "motorway", "points": [[0, 0], [10, 2, 40], "x", [50, 90]], "loop": True, "width": 0})
    assert drawn["kind"] == "street" and drawn["points"] == [[0, 0.0, 0], [10, 2, 40], [50, 0.0, 90]] and drawn["loop"] is True and drawn["width"] == 1.5
    assert c.check({"op": "road"})["shape"] == "straight"


def test_zones_counters_stages_and_the_rest():
    world = WorldState()
    c = play(world=world)
    assert c.check({"op": "trigger", "id": "cup", "at": [0, 0.1, 60], "radius": 0.3, "show": "glow", "effect": {"damage": 5, "push": [0, 99, 0], "nonsense": 1}}) == {
        "op": "zone", "id": "cup", "shape": "sphere", "at": [0, 0.1, 60], "radius": 0.3, "show": "glow", "effect": {"damage": 5, "push": [0, 40, 0]}}
    assert c.check({"op": "zone", "on": "ball", "size": [1, 2, 3]})["on"] == "ball"
    assert c.check({"op": "var", "name": "Strokes Taken", "value": "3", "label": "Strokes", "max": 9}) == {"op": "var", "name": "strokes_taken", "value": 3.0, "max": 9, "label": "Strokes"}
    assert c.check({"op": "var", "name": "lives", "show": True})["label"] == "Lives"
    assert c.check({"op": "timer", "name": "clock", "seconds": 60, "label": "Time"}) == {"op": "var", "name": "clock", "value": 60, "min": 0.0, "rate": -1.0, "as": "time", "label": "Time"}
    assert c.check({"op": "var"}) is None
    hud = c.check({"op": "hud", "objective": "Knock them down", "board": {"at": [0, 3, 19], "title": "LANE 7", "show": ["frame", "score"]}})
    assert hud["objective"] == "Knock them down" and hud["board"]["show"] == ["frame", "score"] and hud["board"]["size"] == 1.5
    assert c.check({"op": "banner", "text": "Go!"}) == {"op": "hud", "banner": "Go!"}
    assert c.check({"op": "hud"}) is None
    stage = c.check({"op": "stage", "id": "wave 1", "goal": "Hold on", "first": True, "next": "wave 2", "enter": [
        {"act": "spawn", "asset": "zombie", "count": 99, "behavior": "attack", "near": "player", "radius": 22}, {"act": "bogus"}], "timeout": 99999})
    assert stage["id"] == "wave_1" and stage["next"] == "wave_2" and stage["timeout"] == 3600 and len(stage["enter"]) == 1
    assert stage["enter"][0]["count"] == 12 and stage["enter"][0]["near"] == "player"
    assert c.check({"op": "stage", "go": "wave 2"}) == {"op": "stage", "go": "wave_2"}
    assert c.check({"op": "fx", "kind": "smoke", "on": "chimney"})["on"] == "chimney" and c.check({"op": "fx", "kind": "lasers"}) is None
    sfx = c.check({"op": "sfx", "name": "Dragon Roar", "layers": [{"wave": "sawtooth", "freq": 5, "to": 60, "dur": 99}, "x", {"wave": "noise", "lowpass": 400}]})
    assert sfx["name"] == "dragon_roar" and [layer["wave"] for layer in sfx["layers"]] == ["saw", "noise"] and sfx["layers"][0]["freq"] == 20 and sfx["layers"][0]["dur"] == 6
    assert c.check({"op": "when", "event": "hit", "do": [{"act": "sound", "name": "dragon_roar"}]})["do"][0]["name"] == "dragon_roar", "a sound it defined is a sound it may play"
    assert c.check({"op": "music", "mood": "TENSE", "tempo": 999, "chords": ["Am", "F", "nope", "C7"], "key": "dm"}) == {"op": "music", "mood": "tense", "tempo": 200, "key": "Dm", "chords": ["Am", "F", "C7"]}
    assert c.check({"op": "music", "stop": True}) == {"op": "music", "stop": True} and "music" not in world.globals
    assert c.check({"op": "speak", "who": "captain", "text": "You're late."}) == {"op": "speak", "id": "captain", "text": "You're late."}
    assert c.check({"op": "narrate"}) is None and c.check({"op": "story", "title": "The Ember Tower"}) == {"op": "story", "title": "The Ember Tower"}
    assert c.check({"op": "player", "health": 100, "jump": "true", "fly": 0, "at": [0, 0, 5000]}) == {"op": "player", "health": 100, "at": [0, 0, 215.0], "jump": True, "fly": False}


def test_what_the_world_remembers_of_them():
    world = WorldState()
    c = play(world=world)
    for stmt in ({"op": "road", "id": "main"}, {"op": "zone", "id": "cup"}, {"op": "var", "name": "score"}, {"op": "stage", "id": "one", "goal": "Go"},
                 {"op": "fx", "id": "smoke", "kind": "smoke"}, {"op": "hud", "objective": "Win", "banner": "Go!"}, {"op": "story", "title": "A Tale"},
                 {"op": "sfx", "name": "zing", "layers": [{}]}, {"op": "spawn", "id": "bob", "asset": "person", "persona": "A baker."},
                 {"op": "ride", "asset": "car"}, {"op": "modify", "id": "ride", "speed": 2, "grip": 0.3}, {"op": "player", "health": 100, "at": [0, 0, 9]}):
        assert c.check(stmt) is not None, stmt
    text = world.render(None, "on foot", "score 3; stage one; health 80")
    for piece in ('"op":"road"', '"op":"zone"', '"name":"score"', '"op":"stage"', '"op":"fx"', '"objective":"Win"', "the story: title: A Tale",
                  'defined the sound "zing"', '"grip":0.3', "USER RIGHT NOW: on foot\nPLAY RIGHT NOW: score 3; stage one; health 80"):
        assert piece in text, piece
    assert "Go!" not in text, "a banner is a moment, not a fact about the world"
    assert '"at"' not in str(world.globals["player"]), "nor is where they were put"
    assert list(world.cast()) == ["bob"] and world.zones() == {"cup"}
    c.check({"op": "remove", "id": "cup"})
    assert world.zones() == set()
    c.check({"op": "clear"})
    assert not world.extras and world.story is None and world.sounds == {"zing": 1}


# ---------------------------------------------------------------------------- rules

def test_rules_of_the_third_version():
    world = WorldState()
    c = play(world=world)
    c.check({"op": "zone", "id": "pit"})
    by_zone = c.check({"op": "when", "event": "enters", "target": "pit", "do": ["remove"]})
    assert by_zone["event"] == "enter" and by_zone["zone"] == "pit" and by_zone["target"] == "player", "the zone written as the target is still the zone"
    assert c.check({"op": "when", "event": "enter", "zone": "pit", "target": "bowling_ball", "do": ["remove"]})["target"] == "bowling_ball"
    assert c.check({"op": "when", "event": "enter", "do": ["remove"]}) is None, "arriving nowhere is not an event"
    rule = c.check({"op": "when", "id": "frame_over", "test": "throws >= 2 || standing(pins) = 0", "do": [
        {"act": "banner", "text": "STRIKE!", "if": "throws == 1"}, {"act": "add", "var": "score", "by": "tipped(pins)"},
        {"act": "add", "var": "score", "by": "ten!!"}, {"act": "set", "var": "throws", "to": 0}, {"act": "set", "var": "status", "to": "in play now"},
        {"act": "reset", "target": "pins", "delay": 1.5}, {"act": "increment", "var": "frame"}, {"act": "set"}, {"act": "banner"}]})
    assert rule["event"] == "becomes" and rule["test"] == "throws >= 2 or standing(pins) == 0"
    assert rule["do"] == [{"act": "banner", "text": "STRIKE!", "if": "throws == 1"}, {"act": "add", "var": "score", "by": "tipped(pins)"},
                          {"act": "add", "var": "score", "by": 1.0}, {"act": "set", "var": "throws", "to": 0}, {"act": "set", "var": "status", "text": "in play now"},
                          {"act": "reset", "delay": 1.5, "target": "pins"}, {"act": "add", "var": "frame", "by": 1}]
    assert c.check({"op": "when", "event": "becomes", "test": "2 +", "do": ["remove"]}) is None
    kept = c.check({"op": "when", "event": "hit", "target": "zombie", "if": "((", "stage": "Wave 1", "cooldown": 9999, "do": ["remove"]})
    assert "if" not in kept and kept["stage"] == "wave_1" and kept["cooldown"] == 600, "a condition that cannot be read is dropped; the rule stands"


def test_what_a_rule_may_do():
    c = play()
    c.check({"op": "make", "name": "medal", "parts": [{"shape": "disc"}]})
    def actions(*written):
        return c.check({"op": "when", "event": "press", "target": "lever", "do": list(written)})["do"]

    done = actions(
        {"act": "teleport", "who": "player", "to": [0, 0, 9000]}, {"act": "teleport", "to": "checkpoint_2"}, {"act": "teleport"},
        {"act": "give", "asset": "medal"}, {"act": "give", "asset": "thing that is not"}, {"act": "take"},
        {"act": "goto", "go": "wave 2"}, {"act": "kill", "who": "boss"}, {"act": "heal", "who": "player"},
        {"act": "disable", "rule": "flak"}, {"act": "enable"}, {"act": "unlock", "id": "vault_door"})
    done += actions(
        {"act": "modify", "target": "zombies", "speed": 99, "behavior": "flee"}, {"act": "modify", "target": "zombies"},
        {"act": "direct", "note": "they pulled the lever"}, {"act": "direct"}, {"act": "win", "text": "Done"}, {"act": "remove", "target": "gate"},
        {"act": "speak", "id": "guard", "text": "Who goes there?"}, {"act": "slowmo"}, {"act": "ride", "asset": "none"}, {"act": "fx", "kind": "nope"})
    assert [a["act"] for a in done] == ["teleport", "teleport", "give", "take", "stage", "damage", "heal", "disable", "door", "modify", "direct",
                                        "win", "remove", "speak", "slowmo", "ride", "fx"]
    assert done[0]["to"] == [0, 0, 215.0] and done[1]["to"] == "checkpoint_2" and done[3] == {"act": "take", "asset": "all"}
    assert done[4]["go"] == "wave_2" and done[5] == {"act": "damage", "amount": 100000.0, "who": "boss"} and done[6]["amount"] == 25
    assert done[8] == {"act": "door", "id": "vault_door", "state": "open"} and done[9] == {"act": "modify", "target": "zombies", "behavior": "flee", "speed": 6}
    assert done[12] == {"act": "remove", "target": "gate"} and done[14] == {"act": "slowmo", "scale": 0.3, "seconds": 2.0} and done[16]["kind"] == "sparks"
    assert len(c.check({"op": "when", "event": "hit", "do": [{"act": "remove"}] * 30})["do"]) == 12


# ---------------------------------------------------------------------------- where things are

def test_places_in_the_new_statements_follow_the_user():
    world = WorldState()
    c = Checker(CATALOG, world, [10.0, 0.0, 90.0], play=True)      # the user stands at x=10, facing +X
    c.check({"op": "zone", "id": "cup", "at": [0, 0, 5]})
    c.check({"op": "road", "id": "lane", "points": [[0, 0], [0, 20]]})
    c.check({"op": "when", "id": "tp", "event": "press", "target": "lever", "do": [{"act": "teleport", "who": "player", "to": [0, 0, 5]}]})
    c.check({"op": "terrain", "type": "flat", "regions": [{"material": "sand", "at": [0, 5], "radius": 3}]})
    c.check({"op": "var", "name": "score"})
    assert world.extras["zone:cup"]["frame"] == [10.0, 0.0, 90.0] and "frame" in world.rules["tp"] and "frame" in world.globals["terrain"]
    assert "frame" not in world.extras["var:score"], "a counter is nowhere"
    # seen from the middle of the world facing +Z, five metres in front of that user is fifteen metres to the right
    assert [round(v) for v in reframe(world.extras["zone:cup"], None)["at"]] == [15, 0, 0]
    assert [[round(v) for v in p] for p in reframe(world.extras["road:lane"], None)["points"]] == [[10, 0, 0], [30, 0, 0]]
    assert [round(v) for v in reframe(world.rules["tp"], None)["do"][0]["to"]] == [15, 0, 0]
    assert [round(v) for v in reframe(world.globals["terrain"], None)["regions"][0]["at"]] == [15, 0, 0]
    on_thing = c.check({"op": "zone", "id": "hoop", "on": "ring"})
    assert "frame" not in on_thing, "what is fixed to a thing is wherever that thing is"
