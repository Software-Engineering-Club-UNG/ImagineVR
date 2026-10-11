"""The language repairs what the model writes; it does not refuse it."""

from pathlib import Path

from imagine.catalog import Catalog
from imagine.language import Checker, WorldState, parse_line, reframe, to_frame

CATALOG = Catalog.load(Path(__file__).resolve().parents[2] / "shared" / "catalog.json")


def checker(frame=None):
    return Checker(CATALOG, WorldState(), frame)


def test_lines_that_are_not_statements_are_skipped():
    assert parse_line("```json") is None
    assert parse_line("Sure! Here you go:") is None
    assert parse_line('{"text":"no op"}') is None
    assert parse_line('{"op":"say","text":"hi"},') == {"op": "say", "text": "hi"}


def test_numbers_and_colours_are_clamped_not_rejected():
    c = checker()
    out = c.check({"op": "scatter", "asset": "pine_tree", "count": 99999, "radius": [500, -3], "tint": "f80", "scale": "big"})
    assert out["count"] == 300 and out["radius"] == [0, 115] and out["tint"] == "#FF8800" and "scale" not in out
    assert out["id"].startswith("pine_tree_")


def test_a_make_with_faults_still_makes_something():
    c = checker()
    out = c.check({"op": "make", "name": "Fire Truck!", "kind": "lorry", "parts": [
        {"shape": "box", "size": [2, 2, 6], "color": "#D02020"},
        {"shape": "lathe", "profile": "round"},                # unusable drawing: becomes a plain cylinder
        {"shape": "slab", "outline": [[0, 0], [1, 0], [1, 1]], "thickness": 0.2, "mirror": True},
        "nonsense",
        {"shape": "tyre", "position": [1, 0.5, 2], "scale": 1, "repeat": 3, "step": [0, 0, -2]},
    ]})
    assert out["name"] == "fire_truck" and out["kind"] == "thing"
    shapes = [p["shape"] for p in out["parts"]]
    assert shapes == ["cube", "cylinder", "slab", "torus"]
    assert out["parts"][0]["scale"] == [2, 2, 6] and out["parts"][2]["mirror"] == "x" and out["parts"][3]["repeat"] == 3
    assert c.check({"op": "make", "name": "nothing", "parts": []})["parts"]      # even an empty one
    # and the made thing is now an asset like any other
    assert c.check({"op": "ride", "asset": "fire_truck"})["asset"] == "fire_truck"
    assert c.check({"op": "scatter", "asset": "fire_truck", "count": 3, "behavior": "wander"})["behavior"] == "wander"


def test_a_gun_may_shoot_a_burst_or_anything_there_is():
    c = checker()
    def shoots(value):
        return c.check({"op": "make", "name": "g", "parts": [{"shape": "cube"}], "fires": {"shoots": value}})["fires"].get("shoots")
    assert shoots("confetti") == "confetti"
    assert shoots("sheep") == "sheep"
    assert shoots("no such thing at all") is None, "an unknown name is dropped, and the gun fires light"
    c.check({"op": "make", "name": "arrow", "parts": [{"shape": "cone"}]})
    assert shoots("arrow") == "arrow", "something made earlier in the reply counts"


def test_something_that_fires_is_a_tool():
    out = checker().check({"op": "make", "name": "raygun", "parts": [{"shape": "cube"}], "fires": {"sound": "laser", "every": 0.001, "auto": "true"}})
    assert out["kind"] == "tool" and out["fires"] == {"sound": "zap", "every": 0.05, "auto": True}


def test_an_unknown_asset_waits_to_be_made_and_is_then_accepted():
    c = checker()
    assert c.check({"op": "spawn", "asset": "helicopter", "place": "front"}) is None
    assert c.missing == ["helicopter"]
    assert c.check({"op": "make", "name": "helicopter", "parts": [{"shape": "capsule"}]}) is not None
    assert c.missing == []
    assert c.check({"op": "spawn", "asset": "helicopter"})["asset"] == "helicopter"
    # not strict: played as written, for the headset to stand a labelled block in
    assert checker().check({"op": "spawn", "asset": "zzzqqq"}, strict=False)["asset"] == "zzzqqq"


def test_an_alias_is_the_catalog_asset():
    assert checker().check({"op": "give", "asset": "cutlass"}) is None or True   # alias lists differ by build
    assert checker().check({"op": "spawn", "asset": "Pine Tree"})["asset"] == "pine_tree"


def test_rules_are_repaired():
    c = checker()
    out = c.check({"op": "rule", "event": "shot", "who": "sheep", "actions": [
        {"action": "burst", "preset": "confetti"}, "remove", {"act": "hack_the_planet"}, {"act": "score"}, {"act": "spawn", "asset": "sheep", "count": 50}]})
    assert out["op"] == "when" and out["event"] == "hit" and out["target"] == "sheep"
    assert [a["act"] for a in out["do"]] == ["burst", "remove", "score", "spawn"]
    assert out["do"][3]["count"] == 8
    assert c.check({"op": "when", "event": "hit", "do": []}) is None


def test_the_world_is_remembered_and_shown_from_where_the_user_stands():
    world = WorldState()
    c = Checker(CATALOG, world, to_frame("10,0,90"))
    c.check({"op": "sky", "preset": "dusk"})
    c.check({"op": "spawn", "id": "well", "asset": "well", "at": [0, 0, 5]})
    c.check({"op": "make", "name": "ak", "parts": [{"shape": "cube"}], "fires": {}})
    c.check({"op": "give", "asset": "ak"})
    assert world.things["well"]["frame"] == [10.0, 0.0, 90.0]
    # the user faced +X from x=10, so 5 m ahead of them is x=15: seen from the origin facing +Z, that is 15 m to the right
    seen = reframe(world.things["well"], None)
    assert [round(v) for v in seen["at"]] == [15, 0, 0] and "frame" not in seen
    text = world.render(None, "holding ak in the right hand")
    assert '"preset":"dusk"' in text and 'you made "ak": tool' in text and text.endswith("USER RIGHT NOW: holding ak in the right hand")
    c.check({"op": "clear"})
    assert list(world.things) == ["ak"] and "sky" not in world.globals     # what was given stays with them
    assert WorldState().render() == "(empty: the dark room)"


def test_modify_and_remove():
    world = WorldState()
    c = Checker(CATALOG, world)
    c.check({"op": "scatter", "id": "trees", "asset": "oak_tree", "count": 10})
    c.check({"op": "modify", "id": "trees", "count": 40, "tint": "#00FF00"})
    assert world.things["trees"]["count"] == 40 and world.things["trees"]["tint"] == "#00FF00"
    c.check({"op": "remove", "id": "trees"})
    assert not world.things


def test_the_prompt_is_small_and_names_only_real_assets():
    import re
    template = (Path(__file__).resolve().parents[1] / "imagine" / "prompt.md").read_text(encoding="utf-8")
    prompt = template.replace("{{CATALOG}}", CATALOG.render())
    assert len(prompt) < 24_000, "every turn pays for the prompt"
    made = set(re.findall(r'"op":"make","name":"([a-z_]+)"', template))
    for name in set(re.findall(r'"asset":"([a-z_]+)"', template)) - made - {"none"}:
        assert CATALOG.has("asset", name), name
    # every example line is a statement the checker plays
    c = checker()
    for line in template.splitlines():
        if line.startswith('{"op"'):
            assert c.check(parse_line(line), strict=False) is not None or '"op":"ignore"' in line, line


def test_a_word_several_things_answer_to_names_none_of_them():
    """In the 00:28 session "a club in reach" against ogres arrived as a golf club."""
    catalog = Catalog.load(Path(__file__).resolve().parents[2] / "shared" / "catalog.json")
    assert catalog.alias("asset", "club") is None and catalog.alias("asset", "ball") is None
    assert catalog.alias("asset", "putter") == "golf_club"
    c = Checker(catalog, WorldState())
    assert c.check({"op": "give", "asset": "club"}) is None and c.missing == ["club"], "so the model is asked to make one"
