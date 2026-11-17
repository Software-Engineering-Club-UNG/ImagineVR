"""What the model is told: the sections, which of them a request gets, and the worked examples."""

import json
import re
from pathlib import Path

import pytest

from imagine import config, llm, structures
from imagine.catalog import Catalog
from imagine.language import Checker, WorldState, parse_line
from imagine.playbook import MOST_CHARACTERS, Playbook
from imagine.prompts import MODULES, Prompts
from imagine.router import modules_for

ROOT = Path(__file__).resolve().parents[2]
CATALOG = Catalog.load(ROOT / "shared" / "catalog.json")
PROMPTS = Prompts.load(config.PROMPT_PATH, config.PROMPTS_DIR, CATALOG)
PLAYBOOK = Playbook.load(ROOT / "shared" / "playbook")
SECTIONS = sorted(path.stem for path in config.PROMPTS_DIR.glob("*.md"))


def checker():
    return Checker(CATALOG, WorldState(), structures=True, wide=True, play=True, gen=True)


# ---------------------------------------------------------------------------- the sections

def test_every_turn_pays_for_its_prompt():
    sizes = PROMPTS.sizes()
    assert sizes["legacy"] < 24_000, "what an older headset is sent has not grown"
    assert sizes["core"] < 24_000, "the part every call of the third version pays for"
    for name in MODULES + ("director", "detail_pass", "play_pass", "event", "npc"):
        assert sizes[name] < 5_000, name
    assert sizes["make"] < 12_000, "the modeller's whole prompt"
    everything = PROMPTS.director(list(MODULES), structures.PROMPT, helped=True)
    assert len(everything) < 40_000, "the most a director call can be told"


def test_the_part_that_is_cached_is_the_same_for_every_call():
    core = PROMPTS.sections["core"]
    for prompt in (PROMPTS.director(["play"], structures.PROMPT, helped=True), PROMPTS.director(["ride", "story"]),
                   PROMPTS.for_pass("detail"), PROMPTS.for_pass("play", ["ride"]), PROMPTS.event()):
        stable, split, particular = prompt.partition(llm.SPLIT)
        assert stable == core and split and particular.strip()
    assert llm.SPLIT not in PROMPTS.director([]), "with no sections chosen there is nothing after the stable part"
    assert PROMPTS.director(["play"], "INTERIORS", play=False) == PROMPTS.legacy + "INTERIORS", "an older headset gets the prompt it always got"
    messages = llm.build_messages(PROMPTS.director(["play"]), "USER SAYS", "anthropic/claude-haiku-5.5")
    assert messages[0]["content"][0] == {"type": "text", "text": core, "cache_control": {"type": "ephemeral"}}
    assert "cache_control" not in messages[0]["content"][1] and "Making it a game" in messages[0]["content"][1]["text"]
    assert llm.SPLIT not in llm.build_messages(PROMPTS.director(["play"]), "x", "openai/gpt-5")[0]["content"]


def test_a_pass_is_told_what_it_is_for():
    assert "THE DETAIL PASS" in PROMPTS.for_pass("detail") and "Making it a game" not in PROMPTS.for_pass("detail")
    play = PROMPTS.for_pass("play", ["ride", "play"])
    assert play.index("Making it a game") < play.index("Driving, flying") < play.index("THE PLAY PASS")
    assert "You are the modeller" in PROMPTS.craft() and "CATALOG" not in PROMPTS.craft()
    assert "You speak for one character" in PROMPTS.npc()


@pytest.mark.parametrize("name", SECTIONS)
def test_nothing_in_a_section_is_left_unfilled_or_untrue(name):
    text = PROMPTS.sections[name]
    assert not re.findall(r"\{\{[A-Z_]+\}\}", text), "a list the code was meant to fill in"
    made = set(re.findall(r'"op":"make","name":"([a-z_]+)"', text))
    for asset in set(re.findall(r'"asset":"([a-z_]+)"', text)) - made - {"none"}:
        assert CATALOG.has("asset", asset), f"{name} names an asset that does not exist: {asset}"
    # every whole statement shown as an example is one the checker plays, losing nothing
    c = checker()
    c.check({"op": "structure", "id": "place", "rooms": [{"id": "front"}, {"id": "back"}]})
    for line in text.splitlines():
        line = line.strip().strip("`")
        if not line.startswith('{"op"') or not line.endswith("}") or "..." in line or "<" in line:
            continue     # a statement followed by words about it, or one with a gap left in it, is not a whole example
        raw = parse_line(line)
        assert raw is not None, line
        out = c.check(raw, strict=False)
        assert out is not None, line
        for key in ("do", "enter"):
            if isinstance(raw.get(key), list):
                assert [a.get("act") for a in raw[key]] == [a["act"] for a in out[key]], line


# ---------------------------------------------------------------------------- which sections

def test_a_request_gets_the_sections_it_needs():
    assert modules_for("take me bowling") == ["play"]
    assert modules_for("put me in a lamborghini on a mountain road") == ["ride"]
    assert "story" in modules_for("I'm a knight rescuing a princess from a dragon")
    assert modules_for("a murder mystery in a mansion, I'm the detective") == ["story"]
    assert set(modules_for("race my friends round a city in go karts")) == {"play", "ride"}
    assert modules_for("a quiet forest") == ["play"], "a new place with nothing asked of it still gets something to do"
    assert modules_for("surprise me") == ["play"]


def test_a_follow_up_is_about_what_is_there():
    world = WorldState()
    c = Checker(CATALOG, world, play=True)
    c.check({"op": "terrain", "type": "hills"})
    c.check({"op": "scatter", "asset": "pine_tree", "count": 20})
    assert modules_for("make it night", world) == [], "and pays for no more than the core"
    c.check({"op": "ride", "asset": "car"})
    assert modules_for("faster", world) == ["ride"]
    c.check({"op": "var", "name": "score"})
    c.check({"op": "spawn", "id": "bob", "asset": "person", "persona": "A baker."})
    assert modules_for("make it night", world) == ["play", "ride", "story"]


# ---------------------------------------------------------------------------- the worked examples

EXAMPLES = sorted((ROOT / "shared" / "playbook").glob("*.ndjson"))


def test_there_are_examples_and_each_can_be_found():
    assert len(PLAYBOOK.examples) == len(EXAMPLES) >= 15
    for example in PLAYBOOK.examples:
        assert PLAYBOOK.nearest(example.ask, 1) == [example], f'"{example.ask}" does not find its own example'


def test_an_example_is_shown_only_for_a_request_like_it():
    assert [e.title for e in PLAYBOOK.nearest("let's go bowling tonight")] [0] == "ten-pin bowling"
    assert PLAYBOOK.nearest("a forest with a river") == [], "no example is better than a wrong one"
    assert PLAYBOOK.nearest("put something on the table") == [], "nor is one shown for a word anyone might say"
    assert PLAYBOOK.nearest("make it night") == [] and PLAYBOOK.render("make the trees taller") == ""
    assert len(PLAYBOOK.nearest("a zombie shooting game in a city with cars", 2)) == 2
    assert PLAYBOOK.nearest("take me bowling", 0) == []
    shown = PLAYBOOK.render("take me bowling", 2)
    assert shown.startswith("WORKED EXAMPLES") and 'User says: "take me bowling"' in shown and '"playbook"' not in shown


@pytest.mark.parametrize("path", EXAMPLES, ids=[p.stem for p in EXAMPLES])
def test_every_line_of_an_example_is_played_as_written(path):
    """An example teaches by being right: if the checker would change or drop a line, the example is teaching something that does not work."""
    lines = [line for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    head = json.loads(lines[0])
    assert head["playbook"] and head["ask"] and len(head["tags"]) >= 4
    assert sum(len(line) + 1 for line in lines[1:]) < MOST_CHARACTERS + 500, "longer and its ending is never shown"
    c = checker()
    ops = []
    for line in lines[1:]:
        raw = parse_line(line)
        assert raw is not None, line
        out = c.check(raw, strict=True)
        assert out is not None, f"dropped: {line}"
        ops.append(out["op"])
        for key in ("do", "enter"):
            if isinstance(raw.get(key), list):
                assert len(raw[key]) == len(out[key]), f"an action was dropped: {line}"
                for wrote, kept in zip(raw[key], out[key]):
                    assert set(wrote) <= set(kept), f"{set(wrote) - set(kept)} lost from {wrote}"
        if raw["op"] not in ("timer", "structure"):     # a timer becomes a counter that runs; an interior is laid out
            assert {k for k, v in raw.items() if v not in (None, False)} <= set(out), f"{set(raw) - set(out)} lost from {line}"
    assert not c.missing, f"assets that do not exist: {c.missing}"
    assert ops[0] == "think" and ops[-1] == "say", "an example is a whole answer"
    assert "when" in ops, "and every one of them gives the user something to do"
