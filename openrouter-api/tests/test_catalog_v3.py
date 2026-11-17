"""What the server offers the model of the third version's library is what the headset builds."""

import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CATALOG = json.loads((ROOT / "shared" / "catalog.json").read_text(encoding="utf-8"))
ADDITIONS = json.loads((ROOT / "scripts" / "catalog_additions.json").read_text(encoding="utf-8"))
LIBRARY = json.loads((ROOT / "client" / "Assets" / "ImagineVR" / "Resources" / "LibraryV3.json").read_text(encoding="utf-8"))


def test_every_new_asset_has_parts_in_the_same_order_and_category():
    assert [a["id"] for a in ADDITIONS["assets"]] == [row["id"] for row in LIBRARY], "Catalog.Export writes in registration order"
    for wanted, row in zip(ADDITIONS["assets"], LIBRARY):
        assert row["category"] == wanted["category"] and row["parts"], row["id"]
        if wanted.get("holdable"):
            assert {"tool", "hold", "ball"} & set(row.get("flags", [])), f"{row['id']} is offered as something to hold"


def test_the_catalog_marks_them_as_new_and_is_written_as_unity_writes_it():
    by_id = {a["id"]: a for a in CATALOG["assets"]}
    for wanted in ADDITIONS["assets"]:
        assert by_id[wanted["id"]].get("since") == 3, wanted["id"]
    for wanted in ADDITIONS["materials"]:
        assert any(m["id"] == wanted["id"] and m.get("since") == 3 for m in CATALOG["materials"]), wanted["id"]
    assert CATALOG["language_version"] == 3
    done = subprocess.run([sys.executable, str(ROOT / "scripts" / "catalog.py"), "check"], capture_output=True, text=True)
    assert done.returncode == 0, done.stdout + done.stderr


def test_the_new_ground_materials_are_registered_by_the_headset():
    source = (ROOT / "client" / "Assets" / "ImagineVR" / "Scripts" / "World" / "TerrainSystem.cs").read_text(encoding="utf-8")
    for wanted in ADDITIONS["materials"]:
        assert f'Ground("{wanted["id"]}"' in source, wanted["id"]


def test_parts_use_only_what_make_knows():
    shapes = {"cube", "sphere", "blob", "cylinder", "cone", "capsule", "pyramid", "wedge", "dome", "torus", "disc", "lathe", "slab", "tube", "building", "stairs", "arch", "asset"}
    roles = {"wheel", "steer", "spinner", "door", "lamp", "seat", "thruster", "gun", "flap"}
    for row in LIBRARY:
        for part in row["parts"]:
            assert part["shape"] in shapes, (row["id"], part["shape"])
            assert part.get("role") in roles | {None}, (row["id"], part.get("role"))
