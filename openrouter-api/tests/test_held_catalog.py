"""Keep the AI's held-item vocabulary in lockstep with Unity's real recipes."""
import re
from pathlib import Path

from imagine.catalog import Catalog
from imagine.language import Checker, WorldState

ROOT = Path(__file__).resolve().parents[2]
CATALOG = Catalog.load(ROOT / "shared" / "catalog.json")
SOURCE = (ROOT / "client/Assets/ImagineVR/Scripts/Library/Assets.HeldExpansion.cs").read_text(encoding="utf-8")
ITEMS = re.findall(r'NewHeld\("([^"]+)", "([^"]+)", "([^"]+)", "([^"]+)"', SOURCE)


def test_one_hundred_new_recipes_are_exported_as_real_held_items():
    assert len(ITEMS) == len({row[0] for row in ITEMS}) == 100
    for identity, category, _, action in ITEMS:
        entry = CATALOG.get("asset", identity)
        assert entry is not None, identity
        assert entry["holdable"] and entry["prop"]
        assert entry["category"] == category
        assert entry["held_action"] == action
        assert entry["use"]


def test_every_new_item_resolves_exactly_in_give_and_spawn():
    checker = Checker(CATALOG, WorldState())
    for identity, _, _, _ in ITEMS:
        assert checker.check({"op": "give", "asset": identity})["asset"] == identity
        assert checker.check({"op": "spawn", "asset": identity})["asset"] == identity
    assert not checker.missing


def test_prompt_catalog_explains_native_behaviors_without_overloading_light_effects():
    text = CATALOG.render()
    assert "frag_grenade*" in text and "[explosive]" in text
    assert CATALOG.get("asset", "frag_grenade")["held_effect"] == "explosion"
    assert "[fishing]" in text and "[returning]" in text and "[disc]" in text
    assert CATALOG.get("asset", "tactical_flashlight")["held_action"] == "light"
    assert "held_effect" not in CATALOG.get("asset", "tactical_flashlight")


def test_existing_fishing_and_throwing_recipes_keep_ids_and_get_behaviors():
    for identity, action in [("fishing_rod", "fishing"), ("boomerang", "returning"), ("frisbee", "disc"), ("flashlight", "light"), ("lantern", "light")]:
        assert CATALOG.get("asset", identity)["held_action"] == action
