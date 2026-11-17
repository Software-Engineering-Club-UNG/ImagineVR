"""What each model call is told.

A headset from before the third version of the language gets `prompt.md`, whole, as it always did.
One that plays the third version gets `prompts/core.md` (the same for every call, so the provider
can cache it) followed by only the sections this request needs: every turn pays for its prompt, and
a request to make it night should not pay for how to write a car chase.

    core.md         the language everyone needs, and the catalog
    play.md         variables, zones, stages, health, rules: making a place a game
    ride.md         driving and flying, roads, traffic
    story.md        people who speak, stages of a story, the director being called back
    make.md         modelling one asset in detail: the whole prompt of a craft call
    detail_pass.md  the second call that dresses a place
    play_pass.md    the second call that gives it something to do
    npc.md          a character answering the user
    event.md        something happened in a story and the director is asked what comes next
"""

from __future__ import annotations

import re
from pathlib import Path

from . import v3
from .catalog import Catalog
from .llm import SPLIT

MODULES = ("play", "ride", "story")
PASSES = ("detail", "play")
_SLOT = re.compile(r"\{\{([A-Z_]+)\}\}")

# lists the prompts quote, taken from the code that checks them so the two cannot drift apart
_LISTS = {
    "SOUNDS": [s for s in v3.SOUNDS if s != "none"], "FX": v3.FX, "MOODS": [m for m in v3.MOODS if m != "none"], "FINISHES": v3.FINISHES,
    "PATTERNS": v3.PATTERNS, "VOICES": v3.VOICES, "SURFACES": v3.SURFACES, "DRIVES": [d for d in v3.DRIVES if d != "auto"],
    "ROLES": v3.ROLES, "STYLES": v3.STYLES, "ROOFS": v3.ROOFS, "ROAD_KINDS": v3.ROAD_KINDS, "ROAD_SHAPES": v3.ROAD_SHAPES,
}


def fill(text: str, catalog_text: str) -> str:
    def slot(match: re.Match) -> str:
        name = match.group(1)
        if name == "CATALOG":
            return catalog_text
        return ", ".join(_LISTS[name]) if name in _LISTS else match.group(0)
    return _SLOT.sub(slot, text)


class Prompts:
    def __init__(self, legacy: str, sections: dict[str, str] | None = None):
        self.legacy = legacy
        self.sections = sections or {}

    @classmethod
    def load(cls, legacy_path: Path, folder: Path, catalog: Catalog) -> "Prompts":
        catalog_text = catalog.render()
        # an older headset is told only of what it has recipes for
        legacy = legacy_path.read_text(encoding="utf-8").replace("{{CATALOG}}", catalog.before(3).render())
        sections = {}
        if folder.is_dir():
            for path in sorted(folder.glob("*.md")):
                sections[path.stem] = fill(path.read_text(encoding="utf-8"), catalog_text).strip() + "\n"
        return cls(legacy, sections)

    @classmethod
    def plain(cls, text: str) -> "Prompts":
        """One prompt for every call: what the tests, and a deployment with no `prompts` folder, run on."""
        return cls(text, {})

    @property
    def modular(self) -> bool:
        return "core" in self.sections

    def _with(self, names, extra: str = "") -> str:
        if not self.modular:
            return self.legacy + extra
        chosen = "\n".join(self.sections[n] for n in names if n in self.sections) + extra
        return self.sections["core"] + (SPLIT + chosen if chosen.strip() else "")

    def director(self, modules: list[str], interiors: str = "", play: bool = True, helped: bool = False) -> str:
        """
        The call that answers the user. `interiors` is what `structures.py` adds for this headset;
        `helped` says other calls will dress the place and write its game, so this one need not.
        """
        if not play:
            return self.legacy + interiors
        return self._with(list(modules) + (["director"] if helped else []), interiors)

    def for_pass(self, name: str, modules: list[str] | None = None, interiors: str = "") -> str:
        """A second call. The one that writes the game is given the same sections the director had, and how to play first."""
        if name == "play":
            return self._with(["play"] + [m for m in (modules or []) if m != "play"] + ["play_pass"], interiors)
        return self._with([name + "_pass"], interiors)

    def event(self, interiors: str = "") -> str:
        return self._with(["story", "play", "event"], interiors)

    def craft(self) -> str:
        return self.sections.get("make") or self.legacy

    def npc(self) -> str:
        return self.sections.get("npc") or self.legacy

    def sizes(self) -> dict[str, int]:
        return {name: len(text) for name, text in self.sections.items()} | {"legacy": len(self.legacy)}
