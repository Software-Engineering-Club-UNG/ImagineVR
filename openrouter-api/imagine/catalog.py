"""The catalog: everything the build contains that a statement may name.

Loaded from shared/catalog.json, which is exported from the Unity project.
"""

from __future__ import annotations

import difflib
import json
import re
from pathlib import Path

# kind -> key in catalog.json
KINDS = {
    "sky": "sky_presets",
    "terrain": "terrain_types",
    "material": "materials",
    "asset": "assets",
    "ambience": "ambience",
    "weather": "weather",
}

DEFAULT_LIMITS = {
    "max_spawned": 60,
    "max_scatter_count": 300,
    "max_scatter_total": 1500,
    "max_physics": 40,
    "safe_radius_m": 3,
    "world_radius_m": 120,
}

_WORD = re.compile(r"[a-z0-9]+")


# words too common to say anything about what was asked for ("the woods" must not match every "the")
_STOP = frozenset("a an the of in on at to by for with and or it is me my i you some this that be do can "
                  "just should there stand stands them they so too".split())


def words(text: str) -> list[str]:
    return _WORD.findall(text.lower().replace("_", " "))


def _stem(word: str) -> str:
    """Singular and plural must land on the same string; it need not be a real word."""
    if len(word) > 4 and word.endswith("ie"):  # zombie = zombies (-> "zomby")
        return word[:-2] + "y"
    if len(word) > 5 and word.endswith("ies"):  # berries -> berry
        return word[:-3] + "y"
    if len(word) > 4 and word.endswith("ves"):  # wolves -> wolf
        return word[:-3] + "f"
    if len(word) > 4 and word.endswith(("ses", "xes", "zes", "ches", "shes")):  # boxes -> box
        return word[:-2]
    if len(word) > 3 and word.endswith("s") and not word.endswith("ss"):  # pirates -> pirate, trees -> tree
        return word[:-1]
    return word


class Catalog:
    def __init__(self, data: dict):
        self.data = data
        self.language_version: int = int(data.get("language_version", 1))
        self.version: str = str(data.get("catalog_version", "unknown"))
        self.limits: dict = {**DEFAULT_LIMITS, **data.get("limits", {})}
        self.primitives: list[str] = list(data.get("primitives", []))
        self.motions: list[str] = list(data.get("motions", []))
        self._entries: dict[str, dict[str, dict]] = {}
        for kind, key in KINDS.items():
            self._entries[kind] = {e["id"]: e for e in data.get(key, []) if "id" in e}
        for prim in self.primitives:
            self._entries["asset"].setdefault(
                prim, {"id": prim, "desc": f"Plain {prim}", "tags": ["shape", "primitive"], "primitive": True}
            )
        # Search index: kind -> list of (id, id words, alias set, tag words)
        self._index: dict[str, list[tuple[str, set[str], set[str], set[str]]]] = {}
        for kind, entries in self._entries.items():
            rows = []
            for eid, entry in entries.items():
                aliases = {a.lower().replace(" ", "_") for a in entry.get("aliases", [])}
                tag_words = {_stem(w) for t in entry.get("tags", []) for w in words(t)}
                alias_words = {_stem(w) for a in aliases for w in words(a)}
                rows.append((eid, {_stem(w) for w in words(eid)}, aliases, tag_words | alias_words))
            self._index[kind] = rows

    @classmethod
    def load(cls, path: Path) -> "Catalog":
        with open(path, "r", encoding="utf-8") as f:
            return cls(json.load(f))

    # ------------------------------------------------------------------ lookup

    def ids(self, kind: str) -> list[str]:
        return list(self._entries[kind].keys())

    def get(self, kind: str, entry_id: str) -> dict | None:
        return self._entries[kind].get(entry_id)

    def has(self, kind: str, entry_id: str) -> bool:
        return entry_id in self._entries[kind]

    def resolve(self, kind: str, name: str) -> tuple[str | None, bool]:
        """Return (catalog id, was_substituted). id is None when nothing is close enough."""
        raw = str(name).strip().lower().replace(" ", "_").replace("-", "_")
        if raw in self._entries[kind]:
            return raw, False
        # An asset name of several words is a thing with a describer in front ("red car",
        # "spider man"): the match has to be the thing, never just the describer.
        match = self.nearest(kind, raw, head_only=kind == "asset")
        return match, match is not None

    def nearest(self, kind: str, name: str, holdable_only: bool = False, head_only: bool = False) -> str | None:
        raw = name.strip().lower().replace(" ", "_").replace("-", "_")
        named = [_stem(w) for w in words(raw)]
        query = set(named)
        if not query:
            return None
        head = named[-1] if head_only and len(named) > 1 else None
        best_id, best_score = None, 0.0
        for eid, id_words, aliases, other_words in self._index[kind]:
            if holdable_only and not self._entries[kind][eid].get("holdable"):
                continue
            if raw in aliases:
                score = 100.0
            elif head is not None and head not in id_words and head not in other_words:
                continue  # "spider man" is not a spider web, "fire truck" is not a fire hydrant
            else:
                score = 3.0 * len(query & id_words) + 1.5 * len(query & other_words)
                if score == 0:
                    # typo tolerance: close spelling of the id or an alias
                    ratio = max(
                        [difflib.SequenceMatcher(None, raw, eid).ratio()]
                        + [difflib.SequenceMatcher(None, raw, a).ratio() for a in aliases]
                    )
                    score = ratio if ratio >= 0.82 else 0.0
                else:
                    # prefer entries that are not much more specific than the request
                    score -= 0.1 * len(id_words - query)
            if score > best_score:
                best_id, best_score = eid, score
        return best_id

    def alias(self, kind: str, name: str) -> str | None:
        """The entry that lists exactly this name as an alias, if one does."""
        raw = name.strip().lower().replace(" ", "_").replace("-", "_")
        for eid, _id_words, aliases, _other in self._index[kind]:
            if raw in aliases:
                return eid
        return None

    def vocabulary(self) -> set[str]:
        """Every word the catalog knows, for the speech recogniser to lean on."""
        vocab: set[str] = set()
        for kind in ("asset", "weather"):
            for eid, entry in self._entries[kind].items():
                vocab.update(words(eid))
                for name in entry.get("aliases", []) + entry.get("tags", []):
                    vocab.update(words(name))
        return {w for w in vocab if len(w) >= 4}

    # ------------------------------------------------------------------ prompt

    def render(self) -> str:
        """The catalog as compact text for the model's instructions: ids, not descriptions.

        Every turn pays for this, so it is kept to what the model needs to choose an id. An id
        says what the thing is; the size is there so scenes come out in proportion.
        """
        out: list[str] = []
        for title, kind in (("Sky presets", "sky"), ("Terrain types", "terrain"), ("Ground materials", "material"),
                            ("Weather", "weather"), ("Ambience loops", "ambience")):
            out.append(f"{title}: " + ",".join(self._entries[kind]))
        categories: dict[str, list[str]] = {}
        for e in self._entries["asset"].values():
            if e.get("primitive") or e.get("category") == "shapes":
                continue
            size = e.get("size_m")
            mark = "*" if e.get("holdable") else ""
            label = f"{e['id']}{mark}" + (f":{size:g}" if isinstance(size, (int, float)) and not e.get("holdable") else "")
            category = e.get("category", "other")
            if e.get("held_action"):
                category = "Held [" + e["held_action"] + "]"
            categories.setdefault(category, []).append(label)
        out.append("Assets (:height in m at scale 1; * held/life-size; [action] is native):")
        for cat, labels in categories.items():
            out.append(f"- {cat}: " + ",".join(labels))
        if self.primitives:
            out.append("- plain shapes (1 m, white, take a tint): " + ", ".join(self.primitives))
        return "\n".join(out)
