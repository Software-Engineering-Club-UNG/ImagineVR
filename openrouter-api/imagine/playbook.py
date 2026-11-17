"""Worked examples: whole requests answered well, shown to the model when it is asked for something like them.

The headset has no bowling mode and no golf mode, and must not grow one. What makes a bowling
alley come out right is knowing the method (a slippery lane, heavy pins, a rule about what is
still standing), and that is knowledge the model can be shown at the moment it is needed. So it
is kept as data: one file per example in shared/playbook, each a header line and then the
statements. Only the two nearest to a request are ever shown, which is why an example of bowling
does not teach the model to reach for pins in a forest. (A kit in the first version did exactly that.)

    {"playbook":"ten-pin bowling","ask":"take me bowling","tags":["bowl","pins","lane","strike","alley"]}
    {"op":"think","text":"..."}
    ...
"""

from __future__ import annotations

import json
import logging
from pathlib import Path

from .catalog import _STOP, _stem, words

log = logging.getLogger("imagine.playbook")

MOST_CHARACTERS = 3400       # of one example, as shown; longer ones are cut at a line
HEADING = "WORKED EXAMPLES: requests like this one, answered well. Borrow the method, never the scene: build THEIR place."


class Example:
    def __init__(self, title: str, ask: str, tags: list[str], lines: list[str]):
        self.title = title
        self.ask = ask
        self.tags = tags
        self.lines = lines
        self.words = {_stem(w) for t in tags for w in words(t)}
        self.ask_words = {_stem(w) for w in words(ask) + words(title)} - _STOP

    def render(self) -> str:
        shown, size = [], 0
        for line in self.lines:
            if size + len(line) > MOST_CHARACTERS:
                break
            shown.append(line)
            size += len(line) + 1
        return f'User says: "{self.ask}"\n' + "\n".join(shown)


class Playbook:
    def __init__(self, examples: list[Example] | None = None):
        self.examples = examples or []

    @classmethod
    def load(cls, folder: Path) -> "Playbook":
        examples = []
        if folder.is_dir():
            for path in sorted(folder.glob("*.ndjson")):
                lines = [line.strip() for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
                try:
                    head = json.loads(lines[0])
                    title, ask, tags = str(head["playbook"]), str(head["ask"]), [str(t) for t in head["tags"]]
                except (IndexError, KeyError, TypeError, json.JSONDecodeError):
                    log.warning("playbook example %s has no header line, skipped", path.name)
                    continue
                examples.append(Example(title, ask, tags, lines[1:]))
        return cls(examples)

    def nearest(self, text: str, most: int = 2) -> list[Example]:
        """The examples most like a request. None at all when nothing is like it: no example is better than a wrong one."""
        said = {_stem(w) for w in words(text or "")} - _STOP
        if not said or most <= 0:
            return []
        scored = []
        for example in self.examples:
            score = 2.0 * len(said & example.words) + 1.0 * len(said & (example.ask_words - example.words))
            if score >= 2.0:
                scored.append((score, example))
        scored.sort(key=lambda pair: -pair[0])
        return [example for _score, example in scored[:most]]

    def render(self, text: str, most: int = 2) -> str:
        found = self.nearest(text, most)
        return HEADING + "\n\n" + "\n\n".join(e.render() for e in found) if found else ""
