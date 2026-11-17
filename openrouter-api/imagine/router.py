"""Which sections of the prompt a request needs.

Decided from the words of the request and from what the world already holds, with no model call:
it has to cost nothing and take no time. Being wrong is cheap in one direction only, so it errs
towards including a section: a section too many is a few hundred tokens, a section too few is a
car that cannot be steered.
"""

from __future__ import annotations

from .catalog import _stem, words
from .language import WorldState

_WORDS = {
    "ride": """drive driving drove car cars truck van bus taxi race racing racer rally drift lap circuit road roads highway
        street streets city town downtown traffic fly flying flight plane airplane aeroplane jet pilot cockpit helicopter chopper
        airport runway boat ship sail sailing speedboat jetski submarine train railway locomotive tram tank bike bicycle
        motorcycle motorbike scooter kart gokart ride riding vehicle rocket spaceship starship ufo balloon glider wingsuit dragon
        horse mech tractor sled sleigh snowmobile skateboard rollercoaster coaster chase garage parking derby land takeoff""",
    "play": """game games play playing score scores points win winning lose losing beat bowl bowling pins strike golf putt putting
        minigolf hole tee shoot shooting shooter target targets gallery range basketball hoop hoops archery bow darts soccer
        football goal penalty tennis baseball bat pitch cricket hockey pool billiards snooker fishing fish boxing fight fighting
        battle war combat enemy enemies zombie zombies monster monsters alien aliens robot robots wave waves boss health damage
        die survive survival defend defense attack sport sports ball throw catch challenge level levels timer countdown puzzle
        maze escape lock key code hunt collect coins race lap tournament match duel arena dodge tag laser turret gun guns weapon
        weapons sword swords knockdown knock stack tower obstacle course parkour climb jump trampoline skiing slalom curling
        carnival fair arcade pinball whack darts quiz""",
    "story": """story stories tale adventure quest mission mystery murder detective clue clues investigate rescue save heist
        robbery treasure talk talking speak conversation ask tell shopkeeper barkeep bartender customer customers interview
        courtroom lawyer judge host guide teacher wizard knight princess king queen villain hero captain crew pirate pirates
        narrator narrate chapter act scene plot haunted ghost survive explore expedition journey legend prophecy dungeon
        tavern inn merchant trader villager villagers companion character characters npc people
        roleplay pretend scary horror spooky creepy secret spy agent""",
}
_INDEX = {name: frozenset(_stem(w) for w in text.split()) for name, text in _WORDS.items()}
# said alone, these ask for somewhere to be and something to do there, whatever it turns out to be
_OPEN = frozenset(_stem(w) for w in "surprise bored boring fun cool awesome something anything whatever random idea exciting".split())


def modules_for(text: str, world: WorldState | None = None, most: int = 3) -> list[str]:
    said = {_stem(w) for w in words(text or "")}
    scores = {name: len(said & index) for name, index in _INDEX.items()}
    if world is not None:
        # what the place already is decides what a follow-up is about: "faster" in a car is about the car
        extras = world.extras
        if "ride" in world.globals or any(k.startswith("road:") for k in extras):
            scores["ride"] += 2
        if world.rules or any(k.startswith(("var:", "zone:")) for k in extras):
            scores["play"] += 2
        if world.story or world.cast() or any(k.startswith("stage:") for k in extras):
            scores["story"] += 2
    chosen = [name for name, score in sorted(scores.items(), key=lambda item: -item[1]) if score > 0][:most]
    empty = world is None or (not world.things and not world.globals.get("terrain") and not world.structure)
    if "play" not in chosen and (said & _OPEN or (empty and not chosen)):
        chosen.append("play")     # a new place with nothing asked of it still gets something to do
    return [name for name in ("play", "ride", "story") if name in chosen]
