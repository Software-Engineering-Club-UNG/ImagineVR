"""Keep shared/catalog.json in step with the library when Unity is not to hand.

The catalog is exported from the Unity project (ImagineVR > Export Catalog) and that export stays
the source of truth. This script writes the same file the same way, so that new library entries
listed in scripts/catalog_additions.json can reach the server, its prompt and its tests before the
editor is next opened. After a real export, `check` should say the two agree.

    python3 scripts/catalog.py check     # the file is written the way Unity writes it, and its version is right
    python3 scripts/catalog.py apply     # add what catalog_additions.json lists, and stamp the new version
"""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "shared" / "catalog.json"
VERSION_FILE = ROOT / "client" / "Assets" / "ImagineVR" / "Resources" / "CatalogVersion.txt"
ADDITIONS = Path(__file__).resolve().parent / "catalog_additions.json"


def _number(value) -> str:
    """A number as MiniJson.Serialize writes it: whole numbers bare, others rounded to four places."""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int):
        return str(value)
    rounded = round(float(value), 4)
    return str(int(rounded)) if rounded == math.floor(rounded) and abs(rounded) < 1e15 else repr(rounded)


def _string(text: str) -> str:
    out = ['"']
    for ch in text:
        if ch == '"':
            out.append('\\"')
        elif ch == "\\":
            out.append("\\\\")
        elif ch == "\n":
            out.append("\\n")
        elif ch == "\r":
            out.append("\\r")
        elif ch == "\t":
            out.append("\\t")
        elif ord(ch) < 0x20:
            out.append("\\u%04x" % ord(ch))
        else:
            out.append(ch)
    out.append('"')
    return "".join(out)


def write(value, pretty: bool, depth: int = 0) -> str:
    """MiniJson.Serialize, in Python: objects on their own lines, lists of plain values on one."""
    def indent(level: int) -> str:
        return "\n" + "  " * level if pretty else ""

    if value is None:
        return "null"
    if isinstance(value, str):
        return _string(value)
    if isinstance(value, (bool, int, float)):
        return _number(value)
    if isinstance(value, dict):
        if not value:
            return "{}"
        body = ",".join(indent(depth + 1) + _string(str(k)) + (": " if pretty else ":") + write(v, pretty, depth + 1) for k, v in value.items())
        return "{" + body + indent(depth) + "}"
    simple = not any(isinstance(item, dict) for item in value)
    if simple:
        return "[" + (", " if pretty else ",").join(write(item, pretty, depth + 1) for item in value) + "]"
    return "[" + ",".join(indent(depth + 1) + write(item, pretty, depth + 1) for item in value) + indent(depth) + "]"


def version_of(catalog: dict) -> str:
    blank = {**catalog, "catalog_version": ""}
    digest = hashlib.sha1(write(blank, False).encode("utf-8")).hexdigest()[:8]
    return f"1.{len(catalog['assets'])}.{digest}"


def check() -> bool:
    text = CATALOG.read_text(encoding="utf-8")
    catalog = json.loads(text)
    same_text = write(catalog, True) + "\n" == text
    same_version = version_of(catalog) == catalog["catalog_version"] == VERSION_FILE.read_text(encoding="utf-8").strip()
    print(f"written as Unity writes it: {same_text}; version {catalog['catalog_version']} is right: {same_version}; {len(catalog['assets'])} assets")
    return same_text and same_version


def apply() -> None:
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    additions = json.loads(ADDITIONS.read_text(encoding="utf-8"))
    catalog["language_version"] = additions["language_version"]
    catalog["limits"] = additions["limits"]
    for key in ("materials", "assets"):
        have = {entry["id"]: i for i, entry in enumerate(catalog[key])}
        for entry in additions[key]:
            entry = {**entry, "since": additions["language_version"]}     # a headset from before then has no such thing
            if entry["id"] in have:
                catalog[key][have[entry["id"]]] = entry
            else:
                catalog[key].append(entry)
    catalog["catalog_version"] = version_of(catalog)
    CATALOG.write_text(write(catalog, True) + "\n", encoding="utf-8")
    VERSION_FILE.write_text(catalog["catalog_version"] + "\n", encoding="utf-8")
    print(f"catalog is now {catalog['catalog_version']}")


if __name__ == "__main__":
    command = sys.argv[1] if len(sys.argv) > 1 else "check"
    if command == "apply":
        apply()
    sys.exit(0 if check() else 1)
