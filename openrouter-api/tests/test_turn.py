"""A turn end to end, with the model replaced by a script."""

import asyncio
import json
from pathlib import Path

import pytest

from imagine import config, llm, turn
from imagine.catalog import Catalog
from imagine.session import Session

CATALOG = Catalog.load(Path(__file__).resolve().parents[2] / "shared" / "catalog.json")


def run(monkeypatch, replies, text="build it", session=None, **kwargs):
    """Plays one turn. `replies` is one list of lines (or an exception) per model call."""
    calls = []

    async def fake(system_prompt, content, model=None):
        calls.append(content)
        reply = replies[min(len(calls), len(replies)) - 1]
        if isinstance(reply, Exception):
            raise reply
        for line in reply:
            yield line if isinstance(line, str) else json.dumps(line)

    monkeypatch.setattr(config, "OFFLINE", False)
    monkeypatch.setattr(llm, "stream_lines", fake)
    session = session or Session("test")

    async def collect():
        return [s async for s in turn.run_turn(session, CATALOG, "PROMPT", text=text, **kwargs)]

    return asyncio.run(collect()), calls, session


def ops(statements):
    return [s["op"] for s in statements]


def test_a_build_streams_through_in_order(monkeypatch):
    out, calls, session = run(monkeypatch, [[
        {"op": "think", "text": "A meadow."},
        "Here is some prose the model should not have written",
        {"op": "sky", "preset": "morning"},
        {"op": "scatter", "asset": "sheep", "count": 6, "behavior": "wander"},
        {"op": "say", "text": "There you go."},
    ]])
    assert ops(out) == ["status", "think", "status", "sky", "scatter", "say", "done"]
    assert out[-1]["outcome"] == "world_changed" and len(calls) == 1
    assert "USER SAYS" in calls[0] and "(empty: the dark room)" in calls[0]
    assert session.history[-1] == ("build it", "There you go.")


def test_a_thing_the_model_forgot_to_make_is_made_and_then_placed(monkeypatch):
    out, calls, _ = run(monkeypatch, [
        [{"op": "spawn", "id": "heli", "asset": "helicopter", "place": "front"}, {"op": "say", "text": "A helicopter."}],
        [{"op": "think", "text": "ignored here"}, {"op": "make", "name": "helicopter", "kind": "flyer", "parts": [{"shape": "capsule", "scale": [2, 2, 6]}]}],
    ])
    assert ops(out) == ["status", "note", "status", "make", "spawn", "say", "done"]
    assert "helicopter" in calls[1] and len(calls) == 2
    assert "stand_in" not in out[4]


def test_when_making_fails_the_thing_is_still_placed(monkeypatch):
    out, _, _ = run(monkeypatch, [[{"op": "spawn", "asset": "zzzqqq"}], llm.LLMError("down")])
    assert ops(out) == ["status", "note", "status", "spawn", "done"]


def test_noise_is_met_with_silence(monkeypatch):
    out, _, session = run(monkeypatch, [[{"op": "ignore"}]], text="whoa")
    assert ops(out) == ["status", "done"] and out[-1]["outcome"] == "ignored"
    assert not session.history


def test_a_model_that_fails_before_writing_is_asked_again(monkeypatch):
    out, calls, _ = run(monkeypatch, [llm.LLMError("timeout"), [{"op": "say", "text": "Hello."}]])
    assert ops(out) == ["status", "say", "done"] and len(calls) == 2 and out[-1]["outcome"] == "answered"


def test_a_model_that_dies_partway_keeps_what_was_built(monkeypatch):
    async def fake(system_prompt, content, model=None):
        yield json.dumps({"op": "sky", "preset": "dusk"})
        raise llm.LLMError("cut off")

    monkeypatch.setattr(config, "OFFLINE", False)
    monkeypatch.setattr(llm, "stream_lines", fake)
    session = Session("t")

    async def collect():
        return [s async for s in turn.run_turn(session, CATALOG, "P", text="a city")]

    out = asyncio.run(collect())
    assert ops(out) == ["status", "status", "sky", "error", "done"] and out[-1]["outcome"] == "failed"
    assert "sky" in session.world.globals


def test_nothing_at_all_still_gets_an_answer(monkeypatch):
    out, _, _ = run(monkeypatch, [["garbage"], ["garbage"]])
    assert ops(out) == ["status", "say", "done"]


def test_with_no_key_it_says_so(monkeypatch):
    monkeypatch.setattr(config, "OFFLINE", True)

    async def collect():
        return [s async for s in turn.run_turn(Session("t"), CATALOG, "P", text="hi")]

    out = asyncio.run(collect())
    assert ops(out) == ["status", "say", "done"] and out[-1]["outcome"] == "failed"


def test_the_user_state_and_position_reach_the_model(monkeypatch):
    session = Session("t")
    run(monkeypatch, [[{"op": "spawn", "id": "well", "asset": "well", "at": [0, 0, 5]}]], session=session)
    _, calls, _ = run(monkeypatch, [[{"op": "say", "text": "ok"}]], session=session, frame=[0, 5, 0], user_state="riding car")
    assert '"at":[0.0,0.0,0.0]' in calls[0]            # they walked to the well
    assert "USER RIGHT NOW: riding car" in calls[0] and "RECENT CONVERSATION" in calls[0]
