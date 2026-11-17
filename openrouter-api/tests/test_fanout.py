"""A turn of the third version: several model calls at once, each replaced here by a script."""

import asyncio
import json
from pathlib import Path

import pytest

from imagine import config, gen, llm, tts, turn
from imagine.catalog import Catalog
from imagine.playbook import Playbook
from imagine.prompts import Prompts
from imagine.session import Session

ROOT = Path(__file__).resolve().parents[2]
CATALOG = Catalog.load(ROOT / "shared" / "catalog.json")
PROMPTS = Prompts.load(config.PROMPT_PATH, config.PROMPTS_DIR, CATALOG)
PLAYBOOK = Playbook.load(ROOT / "shared" / "playbook")
NEW = "structures-v1,structures-v2,play-v1,gen-v1"

ROUGH_CAR = {"op": "make", "name": "muscle_car", "kind": "vehicle", "parts": [{"shape": "cube", "scale": [1.8, 1.2, 4.4], "color": "#C02020"}],
             "refine": "a 1969 muscle car: long bonnet, fastback, chrome bumpers"}
FINE_CAR = {"op": "make", "name": "whatever_the_modeller_called_it", "kind": "thing", "parts": [{"shape": "cube", "scale": [1.8, 0.6, 4.4], "color": "#C02020"}] * 30}
DIRECTOR = [
    {"op": "think", "text": "A desert highway at dusk."},
    {"op": "sky", "preset": "dusk"},
    {"op": "terrain", "type": "dunes", "material": "sand", "size": "vast"},
    ROUGH_CAR,
    {"op": "spawn", "id": "car", "asset": "muscle_car", "at": [3, 0, 8]},
    {"op": "spawn", "id": "diner", "asset": "diner", "at": [-12, 0, 20]},
    {"op": "say", "text": "The director's words."},
]
DETAIL = [
    {"op": "think", "text": "never shown"},
    {"op": "scatter", "id": "d_cacti", "asset": "cactus", "count": 30, "area": "around"},
    {"op": "spawn", "id": "car", "asset": "barrel", "at": [1, 0, 2], "physics": True},      # the id the director used
    {"op": "sky", "preset": "night_stars"},                                                  # not a pass's to change
    {"op": "say", "text": "Not the detail pass's to say."},
    {"op": "when", "event": "hit", "target": "barrel", "do": [{"act": "remove"}]},           # nor to write the game
]
PLAY = [
    {"op": "var", "name": "score", "label": "Score"},
    {"op": "when", "id": "p_hit", "event": "hit", "target": "cactus", "do": [{"act": "add", "var": "score"}]},
    {"op": "clear"},
    {"op": "say", "text": "Shoot the cacti."},
]


def who_is_asked(content: str) -> str:
    if "MODEL THIS" in content:
        return "craft"
    if "follow THE DETAIL PASS" in content:
        return "detail"
    if "follow THE PLAY PASS" in content:
        return "play"
    if "THEY SAY TO YOU" in content:
        return "npc"
    return "event" if "THIS JUST HAPPENED" in content else "director"


def run(monkeypatch, replies, text="a desert highway with a muscle car", session=None, capabilities=NEW, prompts=PROMPTS, **kwargs):
    """Plays one turn. `replies` maps who is asked (director, craft, detail, play, npc, event) to its lines, an exception, or a function of the request."""
    calls = []

    async def fake(system_prompt, content, model=None):
        who = who_is_asked(content)
        calls.append({"who": who, "system": system_prompt, "content": content, "model": model, "options": dict(llm.options.get() or {})})
        reply = replies.get(who, [])
        reply = reply(content) if callable(reply) else reply
        if isinstance(reply, Exception):
            raise reply
        for line in reply:
            await asyncio.sleep(0)      # other calls get a turn, as they would on a network
            if line == "hang":
                await asyncio.sleep(3600)
            yield line if isinstance(line, str) else json.dumps(line)

    monkeypatch.setattr(config, "OFFLINE", False)
    monkeypatch.setattr(llm, "stream_lines", fake)
    session = session or Session("test")

    async def collect():
        return [s async for s in turn.run_turn(session, CATALOG, prompts, text=text, capabilities=capabilities, playbook=PLAYBOOK, **kwargs)]

    return asyncio.run(collect()), calls, session


def ops(statements):
    return [s["op"] for s in statements]


def asked(calls):
    return sorted(c["who"] for c in calls)


# ---------------------------------------------------------------------------- building a place

def test_a_new_place_is_built_by_several_calls_at_once(monkeypatch):
    out, calls, session = run(monkeypatch, {"director": DIRECTOR, "craft": [FINE_CAR], "detail": DETAIL, "play": PLAY})
    assert asked(calls) == ["craft", "detail", "director", "play"]
    assert out[-1] == {"op": "done", "outcome": "world_changed"}

    makes = [s for s in out if s["op"] == "make"]
    assert [len(m["parts"]) for m in makes] == [1, 30], "the rough one first, then the modeller's in its place"
    assert all(m["name"] == "muscle_car" and m["kind"] == "vehicle" for m in makes), "it is that asset and no other, whatever the modeller called it"
    assert all("refine" not in s for s in out), "the brief is the server's business"
    assert session.world.made["muscle_car"]["parts"] == 30

    says = [s for s in out if s["op"] == "say"]
    assert [s["text"] for s in says] == ["Shoot the cacti."] and out[-2] == says[0], "one closing line, from whoever wrote the game, last"
    assert ops(out).count("think") == 1 and ops(out).count("sky") == 1 and "clear" not in ops(out), "a pass does not rebuild the place"
    assert [s["id"] for s in out if s["op"] == "spawn"] == ["car", "diner", "car_d"], "two calls choosing one id made two things"
    assert [s["id"] for s in out if s["op"] == "when"] == ["p_hit"], "rules are the play pass's"
    assert session.history[-1] == ("a desert highway with a muscle car", "Shoot the cacti.")
    assert set(session.world.things) == {"car", "diner", "car_d", "d_cacti"} and "var:score" in session.world.extras


def test_each_call_is_told_what_it_needs(monkeypatch):
    _, calls, _ = run(monkeypatch, {"director": DIRECTOR, "craft": [FINE_CAR], "detail": DETAIL, "play": PLAY}, text="take me bowling in the desert in a muscle car")
    by = {c["who"]: c for c in calls}
    core = PROMPTS.sections["core"]
    assert all(by[w]["system"].startswith(core) for w in ("director", "detail", "play")), "so the provider caches it once for all three"
    assert "You are not alone on this one" in by["director"]["system"] and "Making it a game" in by["director"]["system"]
    assert "THE DETAIL PASS" in by["detail"]["system"] and "THE PLAY PASS" in by["play"]["system"] and "Driving, flying" in by["play"]["system"]
    assert by["craft"]["system"] == PROMPTS.craft() and '"muscle_car" (kind: vehicle)' in by["craft"]["content"]
    assert "1969 muscle car" in by["craft"]["content"] and '"scale":[1.8,1.2,4.4]' in by["craft"]["content"]
    assert "WORKED EXAMPLES" in by["director"]["content"] and "ten-pin bowling" not in by["director"]["content"] and "LUCKY STRIKE" in by["director"]["content"]
    assert "WORKED EXAMPLES" in by["play"]["content"] and "WORKED EXAMPLES" not in by["detail"]["content"]
    # the passes see the place as the director left it
    assert '"asset":"diner"' in by["detail"]["content"] and '"asset":"diner"' in by["play"]["content"] and '"asset":"diner"' not in by["director"]["content"]
    assert by["craft"]["options"]["max_tokens"] == 9000 and by["director"]["options"] == {}


def test_a_follow_up_is_one_call(monkeypatch):
    session = Session("t")
    run(monkeypatch, {"director": DIRECTOR}, session=session)
    out, calls, _ = run(monkeypatch, {"director": [{"op": "sky", "preset": "night_stars"}, {"op": "say", "text": "Night falls."}]}, text="make it night", session=session)
    assert asked(calls) == ["director"] and ops(out) == ["status", "status", "sky", "say", "done"]
    assert llm.SPLIT in calls[0]["system"], "what is there decides the sections: a ride, so how to drive"
    assert "WORKED EXAMPLES" not in calls[0]["content"]


def test_an_older_headset_is_one_call_with_the_prompt_it_always_had(monkeypatch):
    out, calls, _ = run(monkeypatch, {"director": DIRECTOR, "detail": DETAIL, "play": PLAY}, capabilities="structures-v1,structures-v2")
    assert asked(calls) == ["director", "director"], "the second only to make what its library lacks, as before; nothing dresses or scores"
    assert calls[0]["system"].startswith(PROMPTS.legacy) and "sports_car" not in calls[0]["system"] and "WORKED EXAMPLES" not in calls[0]["content"]
    assert "size" not in next(s for s in out if s["op"] == "terrain") and all("refine" not in s for s in out)
    assert [s["text"] for s in out if s["op"] == "say"] == ["The director's words."]
    assert next(s for s in out if s["op"] == "note")["text"].startswith("no diner"), "a diner is not in the library of a headset that old: it is asked for, as it always was"


def test_a_call_that_fails_costs_only_its_own_part(monkeypatch):
    out, calls, _ = run(monkeypatch, {"director": DIRECTOR, "craft": llm.LLMError("down"), "detail": llm.LLMError("down"), "play": [{"op": "var", "name": "score"}, "garbage"]})
    assert out[-1]["outcome"] == "world_changed" and "error" not in ops(out)
    assert len([s for s in out if s["op"] == "make"]) == 1, "the rough car stays"
    assert [s["text"] for s in out if s["op"] == "say"] == ["The director's words."], "with no closing line from the play pass, the director's is used"
    lost, _, _ = run(monkeypatch, {"director": [DIRECTOR[1], llm.LLMError("cut off")]})
    assert ops(lost)[-2:] == ["error", "done"] and lost[-1]["outcome"] == "failed", "the director failing is the turn failing"


def test_a_turn_ends_on_time(monkeypatch):
    monkeypatch.setattr(config, "TURN_SECONDS", 0.3)
    out, _, _ = run(monkeypatch, {"director": DIRECTOR, "craft": ["hang"], "detail": [DETAIL[1], "hang"], "play": PLAY})
    assert out[-1]["outcome"] == "world_changed" and "d_cacti" in [s.get("id") for s in out]
    assert [s["text"] for s in out if s["op"] == "say"] == ["Shoot the cacti."], "what was written in time is kept; what was not is left out"


def test_how_much_a_turn_is_worth_can_be_set(monkeypatch):
    monkeypatch.setattr(config, "DEPTH", 0)
    out, calls, _ = run(monkeypatch, {"director": DIRECTOR, "craft": [FINE_CAR], "detail": DETAIL, "play": PLAY})
    assert asked(calls) == ["director"] and "You are the modeller" in calls[0]["system"] and "You are not alone" not in calls[0]["system"]
    assert all("refine" not in s for s in out) and [s["text"] for s in out if s["op"] == "say"] == ["The director's words."]
    monkeypatch.setattr(config, "DEPTH", 1)
    assert asked(run(monkeypatch, {"director": DIRECTOR, "craft": [FINE_CAR]})[1]) == ["craft", "director"]
    monkeypatch.setattr(config, "DEPTH", 2)
    monkeypatch.setattr(config, "CRAFTS", 1)
    two = DIRECTOR[:4] + [{**ROUGH_CAR, "name": "second_car"}] + DIRECTOR[4:]
    assert asked(run(monkeypatch, {"director": two, "craft": [FINE_CAR]})[1]).count("craft") == 1, "no more modellers than allowed"


def test_one_prompt_for_everything_still_works(monkeypatch):
    """A deployment with no `prompts` folder, and the older tests, hand over one string."""
    out, calls, _ = run(monkeypatch, {"director": DIRECTOR}, prompts="ONE PROMPT")
    assert asked(calls) == ["director"] and calls[0]["system"].startswith("ONE PROMPT")
    assert [s["text"] for s in out if s["op"] == "say"] == ["The director's words."]


# ---------------------------------------------------------------------------- pictures and voices

def test_pictures_and_voices_are_given_an_address(monkeypatch):
    monkeypatch.setattr(gen, "enabled", lambda: True)
    monkeypatch.setattr(gen, "start", lambda kind, prompt: f"{kind}key")
    monkeypatch.setattr(tts, "enabled", lambda: True)
    spoken = []
    monkeypatch.setattr(tts, "start", lambda text, voice=None: spoken.append((text, voice)) or "clip.mp3")
    director = [{"op": "sky", "preset": "dusk", "image": "far peaks"}, {"op": "spawn", "id": "poster", "asset": "cube", "image": "a circus poster"},
                {"op": "spawn", "id": "cap", "asset": "pirate", "persona": "A captain.", "voice": "gruff"},
                {"op": "speak", "id": "cap", "text": "Welcome aboard."}, {"op": "narrate", "text": "The tide turned."}, {"op": "say", "text": "Done."}]
    out, _, _ = run(monkeypatch, {"director": director}, base_url="https://x")
    by = {s["op"] + ":" + str(s.get("id", "")): s for s in out}
    assert by["sky:"]["image_url"] == "https://x/v1/gen/skykey" and by["spawn:poster"]["image_url"] == "https://x/v1/gen/picturekey"
    assert by["speak:cap"]["audio"] == by["narrate:"]["audio"] == by["say:"]["audio"] == "https://x/v1/audio/clip.mp3"
    assert spoken == [("Welcome aboard.", "gruff"), ("The tide turned.", "narrator"), ("Done.", "narrator")], "each in the voice of who says it"
    no_fetch, _, _ = run(monkeypatch, {"director": director}, base_url="https://x", capabilities="play-v1")
    assert not any("image_url" in s for s in no_fetch), "a picture only for a headset that can show one"
    assert all("audio" in s for s in no_fetch if s["op"] in ("speak", "narrate")), "a voice for any that plays the third version"


# ---------------------------------------------------------------------------- people, and stories

def a_world_with_a_captain(monkeypatch):
    session = Session("t")
    run(monkeypatch, {"director": [{"op": "spawn", "id": "captain", "asset": "pirate", "name": "Captain Mora", "persona": "Gruff. Guards the map.", "voice": "gruff"},
                                   {"op": "say", "text": "A captain."}]}, session=session, text="a pirate captain")
    return session


def test_talking_to_a_character(monkeypatch):
    session = a_world_with_a_captain(monkeypatch)
    reply = [{"op": "think", "text": "no"}, {"op": "say", "text": "Arr. Take the key, then."}, {"op": "give", "asset": "key"},
             {"op": "terrain", "type": "ocean"}, {"op": "clear"}]
    out, calls, _ = run(monkeypatch, {"npc": reply}, text="where is the map?", session=session, talk_to="captain")
    assert asked(calls) == ["npc"] and calls[0]["system"] == PROMPTS.npc() and calls[0]["options"]["max_tokens"] == 700
    assert 'YOU ARE: Captain Mora (id "captain"' in calls[0]["content"] and "Gruff. Guards the map." in calls[0]["content"] and 'THEY SAY TO YOU: "where is the map?"' in calls[0]["content"]
    assert ops(out) == ["status", "speak", "status", "give", "done"], "a character speaks and may hand something over; they do not remake the world"
    assert out[1] == {"op": "speak", "id": "captain", "text": "Arr. Take the key, then."}
    assert list(session.talk["captain"]) == [("where is the map?", "Arr. Take the key, then.")]
    assert session.history[-1][0] == "a pirate captain", "what is said to a character is between the two of them"
    _, calls, _ = run(monkeypatch, {"npc": [{"op": "speak", "text": "I told you."}]}, text="and the treasure?", session=session, talk_to="captain")
    assert "them: where is the map?\nyou: Arr. Take the key, then." in calls[0]["content"]


def test_a_character_always_answers_and_nobody_else_is_a_character(monkeypatch):
    session = a_world_with_a_captain(monkeypatch)
    silent, _, _ = run(monkeypatch, {"npc": ["..."]}, text="hello?", session=session, talk_to="captain")
    assert [s for s in silent if s["op"] == "speak"] == [{"op": "speak", "id": "captain", "text": "Hm."}]
    out, calls, _ = run(monkeypatch, {"director": [{"op": "say", "text": "Nobody there."}]}, text="hello?", session=session, talk_to="nobody")
    assert asked(calls) == ["director"], "talking to someone who is not there is talking to the builder"
    old, calls, _ = run(monkeypatch, {"director": [{"op": "say", "text": "Hello."}]}, text="hello?", session=session, talk_to="captain", capabilities="")
    assert asked(calls) == ["director"]


def test_the_story_calls_the_director_back(monkeypatch):
    session = a_world_with_a_captain(monkeypatch)
    nxt = [{"op": "narrate", "text": "The vault stood empty."}, {"op": "stage", "id": "chase", "goal": "Catch the thief"}, ROUGH_CAR]
    out, calls, _ = run(monkeypatch, {"event": nxt, "craft": [FINE_CAR], "detail": DETAIL, "play": PLAY}, text=None, event="they opened the vault and found it empty", session=session)
    assert asked(calls) == ["event"], "what happens next is one call: nothing is dressed or modelled over"
    assert "THE STORY GOES ON" in calls[0]["system"] and 'THIS JUST HAPPENED: "they opened the vault and found it empty"' in calls[0]["content"]
    assert ops(out) == ["status", "narrate", "status", "stage", "make", "done"] and "say" not in ops(out)
    assert session.history[-1] == ("(it happened) they opened the vault and found it empty", "The vault stood empty.")
    nothing, _, _ = run(monkeypatch, {"event": []}, text=None, event="a leaf fell", session=session)
    assert ops(nothing) == ["status", "done"] and nothing[-1]["outcome"] == "ignored", "nothing to add is not a failure to be apologised for"


def test_a_world_cannot_call_the_director_every_second():
    session = Session("t")
    assert session.allow_event() and not session.allow_event(), "never two close together"
    session._event_times.clear()
    session._event_times.extend([-1000.0] * (config.EVENTS_PER_MINUTE - 1))
    assert session.allow_event()


def test_what_the_headset_says_of_the_game_reaches_the_model(monkeypatch):
    _, calls, _ = run(monkeypatch, {"director": [{"op": "say", "text": "Three."}]}, text="what's my score?", play_state="score 3; stage wave_2; health 80")
    assert "PLAY RIGHT NOW: score 3; stage wave_2; health 80" in calls[0]["content"]
    _, calls, _ = run(monkeypatch, {"director": [{"op": "say", "text": "?"}]}, text="what's my score?", play_state="score 3", capabilities="")
    assert "PLAY RIGHT NOW" not in calls[0]["content"]
