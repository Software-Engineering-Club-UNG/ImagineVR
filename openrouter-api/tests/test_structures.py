import math
from pathlib import Path

from imagine.catalog import Catalog
from imagine.language import Checker, WorldState, reframe
from imagine.structures import normalize

CATALOG = Catalog.load(Path(__file__).resolve().parents[2] / "shared" / "catalog.json")


def checker(frame=None):
    return Checker(CATALOG, WorldState(), frame, structures=True)


def test_default_dungeon_is_connected_and_deterministic():
    plan = normalize({"id": "dungeon", "enter": True})
    assert plan == normalize({"id": "dungeon", "enter": True})
    assert len(plan["rooms"]) == 3 and len(plan["zones"]) == 5 and len(plan["doors"]) == 3
    assert plan["enter"] and plan["entry"] == "entry"
    assert all(len(z["openings"]) > 0 for z in plan["zones"])


def test_dimensions_limits_and_unique_room_names():
    plan = normalize({"height": 99, "rooms": [{"id": "a", "size": [999, -5]}] * 20})
    assert len(plan["rooms"]) == 8 and len(plan["zones"]) == 15
    assert len({r["id"] for r in plan["rooms"]}) == 8
    assert all(r["size"] == [16, 6, 4] for r in plan["rooms"])
    assert normalize({"rooms": [{"id": "room_2_1"}, {"id": "room_2_1"}, {"id": "room_2_1"}]})


def test_hub_rooms_do_not_overlap():
    plan = normalize({"layout": "hub", "rooms": [{"size": [16, 16]}] * 8})
    assert len(plan["rooms"]) == 5 and len(plan["zones"]) == 9
    for i, a in enumerate(plan["zones"]):
        for b in plan["zones"][i + 1:]:
            assert (abs(a["center"][0] - b["center"][0]) >= (a["size"][0] + b["size"][0]) / 2 or
                    abs(a["center"][2] - b["center"][2]) >= (a["size"][2] + b["size"][2]) / 2)


def test_world_bounds_respect_user_frame():
    frame = [110, 100, 90]
    plan = normalize({"at": [90, 0, 90], "rooms": [{"size": [16, 16]}] * 8}, frame)
    origin = [frame[0] + plan["at"][2], frame[1] - plan["at"][0]]
    for z in plan["zones"]:
        for x in (-1, 1):
            for zz in (-1, 1):
                p = [z["center"][0] + x * z["size"][0] / 2, z["center"][2] + zz * z["size"][2] / 2]
                assert math.hypot(origin[0] + p[1], origin[1] - p[0]) <= 110


def test_old_clients_cannot_receive_structure_operations():
    c = Checker(CATALOG, WorldState())
    assert c.check({"op": "structure"}) is None and "update" in c.problem
    assert c.world.structure is None


def test_room_placement_and_door_state():
    c = checker([10, 20, 90])
    structure = c.check({"op": "structure", "id": "dungeon"})
    assert structure["frame"] == [10, 20, 90]
    prop = c.check({"op": "spawn", "asset": "chair", "room": "entry", "at": [99, 0, 99]})
    assert prop["room"] == "entry" and "frame" not in prop and prop["at"] == [2, 0, 3]
    assert reframe(prop, [30, 40, 180])["at"] == prop["at"]
    door = structure["doors"][0]["id"]
    assert c.check({"op": "door", "id": door, "state": "open"})["state"] == "open"
    assert c.world.structure["doors"][0]["state"] == "open"
    c.check({"op": "door", "id": door, "state": "toggle"})
    assert c.world.structure["doors"][0]["state"] == "closed"
    # nothing is refused: a door or a room named wrongly is the nearest there is
    assert c.check({"op": "door", "id": door[:-2], "state": "open"})["id"] == door
    assert c.check({"op": "door", "room": structure["doors"][1]["room"]})["id"] == structure["doors"][1]["id"]
    assert c.check({"op": "spawn", "asset": "chair", "room": "the entry"})["room"] == "entry"
    assert c.check({"op": "spawn", "asset": "chair", "room": "ballroom"})["room"] == "entry"
    assert c.problem is None
    assert "structure" in c.world.render()
    c.check({"op": "remove", "id": "dungeon"})
    assert c.world.structure is None


def test_replacement_clear_and_structure_reframing():
    c = checker([10, 20, 90])
    original = c.check({"op": "structure", "id": "first", "rot_y": 30})
    assert reframe(original, [10, 20, 0])["rot_y"] == 120
    c.check({"op": "structure", "id": "second"})
    assert c.world.structure["id"] == "second"
    c.check({"op": "spawn", "asset": "chair", "room": "entry"})
    c.check({"op": "clear"})
    assert c.world.structure is None
    assert not c.world.things


def test_long_identifiers_remain_targetable():
    c = checker()
    s = c.check({"op": "structure", "id": "a" * 40, "rooms": [{"id": "b" * 40}] * 3})
    assert len({d["id"] for d in s["doors"]}) == len(s["doors"])
    for d in s["doors"]:
        assert len(d["id"]) <= 40
        assert c.check({"op": "door", "id": d["id"], "state": "open"})


def test_normalized_world_can_be_reused_without_losing_dimensions():
    plan = normalize({"rooms": [{"id": "entry", "size": [12, 16]}, {"id": "hall", "size": [8, 10]}], "height": 5})
    assert normalize(plan) == plan


def test_an_interior_may_look_like_anything():
    plan = normalize({"theme": "castle", "wall": "#EEDDCC", "floor": "pink", "light": "#FFAA00", "brightness": 7})
    assert plan["theme"] == "dungeon", "an unknown theme is the default one"
    assert plan["wall"] == "#EEDDCC" and plan["light"] == "#FFAA00" and plan["brightness"] == 1
    assert "floor" not in plan, "a colour that is not one is left to the theme"
    assert normalize(plan) == plan
    assert "wall" not in normalize({"theme": "house"})


def test_a_door_with_no_structure_is_nothing_and_no_complaint():
    c = checker()
    assert c.check({"op": "door", "id": "front"}) is None and c.problem is None
    assert c.check({"op": "spawn", "asset": "chair", "room": "kitchen"})["asset"] == "chair"
