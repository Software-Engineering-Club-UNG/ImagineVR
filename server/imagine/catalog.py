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
    "kit": "kits",
    "asset": "assets",
    "ambience": "ambience",
    "weather": "weather",
}

DEFAULT_LIMITS = {
    "max_spawned": 60,
    "max_scatter_count": 300,
    "max_scatter_total": 1500,
    "max_kits": 3,
    "max_physics": 40,
    "safe_radius_m": 3,
    "world_radius_m": 120,
}

_WORD = re.compile(r"[a-z0-9]+")


# words too common to say anything about what was asked for ("the woods" must not match every "the")
_STOP = frozenset("a an the of in on at to by for with and or it is me my i you some this that be do can".split())


def words(text: str) -> list[str]:
    return _WORD.findall(text.lower().replace("_", " "))


def _stem(word: str) -> str:
    if len(word) > 4 and word.endswith("ie"):  # "zombie" must meet "zombies" (-> "zomby")
        return word[:-2] + "y"
    for suffix in ("ies", "es", "s"):
        if len(word) > len(suffix) + 2 and word.endswith(suffix):
            return word[: -len(suffix)] + ("y" if suffix == "ies" else "")
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
        match = self.nearest(kind, raw)
        return match, match is not None

    def nearest(self, kind: str, name: str, holdable_only: bool = False) -> str | None:
        raw = name.strip().lower().replace(" ", "_").replace("-", "_")
        query = {_stem(w) for w in words(raw)}
        if not query:
            return None
        best_id, best_score = None, 0.0
        for eid, id_words, aliases, other_words in self._index[kind]:
            if holdable_only and not self._entries[kind][eid].get("holdable"):
                continue
            if raw in aliases:
                score = 100.0
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

    def search(self, kind: str, text: str, limit: int = 3) -> list[tuple[str, float]]:
        """Score every entry of a kind against free text. Used by the offline director."""
        query = {_stem(w) for w in words(text)} - _STOP
        spaced = " " + " ".join(words(text)) + " "  # whole words only: "hell" is not in "hello"
        scored = []
        for eid, id_words, aliases, other_words in self._index[kind]:
            alias_hit = any(f" {p} " in spaced or f" {p}s " in spaced for p in (a.replace("_", " ") for a in aliases))
            score = 3.0 * len(query & id_words) + 1.5 * len(query & other_words) + (4.0 if alias_hit else 0.0)
            if id_words and id_words <= query:
                score += 3.0
            if score > 0:
                scored.append((eid, score))
        scored.sort(key=lambda pair: -pair[1])
        return scored[:limit]

    def vocabulary(self) -> set[str]:
        """Every word the catalog knows, for correcting what speech recognition misheard."""
        vocab: set[str] = set()
        for kind in ("kit", "asset", "weather"):
            for eid, entry in self._entries[kind].items():
                vocab.update(words(eid))
                for name in entry.get("aliases", []) + entry.get("tags", []):
                    vocab.update(words(name))
        return {w for w in vocab if len(w) >= 4}

    # ------------------------------------------------------------------ prompt

    def render(self) -> str:
        """The catalog as compact text for the model's instructions."""
        out: list[str] = []

        def section(title: str, kind: str, extra=None):
            entries = self._entries[kind]
            if not entries:
                return
            out.append(f"## {title}")
            for eid, e in entries.items():
                if e.get("primitive"):
                    continue
                line = f"- {eid}: {e.get('desc', '')}"
                if extra:
                    more = extra(e)
                    if more:
                        line += f" {more}"
                out.append(line)
            out.append("")

        def kit_extra(e):
            bits = []
            if e.get("params"):
                bits.append("params: " + "; ".join(f"{k} = {v}" for k, v in e["params"].items()))
            d = e.get("defaults") or {}
            if d:
                bits.append(
                    "defaults: "
                    + ", ".join(f"{k} {json.dumps(v) if isinstance(v, dict) else v}" for k, v in d.items())
                )
            return f"({' | '.join(bits)})" if bits else ""

        section("Sky presets (sky.preset)", "sky")
        section("Terrain types (terrain.type)", "terrain")
        section("Ground materials (terrain.material)", "material")
        section("Kits (kit.kit) - whole themed places", "kit", kit_extra)

        # Assets grouped by category
        assets = [e for e in self._entries["asset"].values() if not e.get("primitive")]
        categories: dict[str, list[dict]] = {}
        for e in assets:
            categories.setdefault(e.get("category", "other"), []).append(e)
        out.append("## Assets (spawn.asset, scatter.asset, give.asset)")
        out.append("Marks: [hold] can be given to hold; [prop] can use physics:true; size is height in metres at scale 1.")
        for cat, entries in categories.items():
            out.append(f"### {cat}")
            for e in entries:
                marks = ""
                if e.get("holdable"):
                    marks += " [hold]"
                if e.get("prop"):
                    marks += " [prop]"
                size = e.get("size_m")
                size_txt = f" ({size:g}m)" if isinstance(size, (int, float)) else ""
                out.append(f"- {e['id']}: {e.get('desc', '')}{size_txt}{marks}")
        if self.primitives:
            out.append("### shapes")
            out.append("- " + ", ".join(self.primitives) + ": plain shapes, 1m, tint them. Use when nothing else fits.")
        out.append("")

        section("Weather (weather.type)", "weather")
        section("Ambience loops (ambience.loop)", "ambience")
        if self.motions:
            out.append("## Motions (motion)")
            out.append("- " + ", ".join(self.motions))
            out.append("")
        lim = self.limits
        out.append("## Limits")
        out.append(
            f"- at most {lim['max_spawned']} spawned objects, {lim['max_scatter_count']} per scatter, "
            f"{lim['max_scatter_total']} scattered in total, {lim['max_kits']} kits, "
            f"{lim['max_physics']} physics objects"
        )
        out.append(f"- the world is {lim['world_radius_m']} m in radius; nothing is placed within {lim['safe_radius_m']} m of the user")
        return "\n".join(out)
