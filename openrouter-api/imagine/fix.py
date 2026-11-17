"""Small repairs shared by everything that reads what the model wrote.

Each takes a value of any type and returns something playable, or None when nothing usable is
left. None of them raises: a wrong value is never a reason to turn a statement away.
"""

from __future__ import annotations

import math
import re

_ID = re.compile(r"[^a-z0-9_]+")
_COLOR = re.compile(r"^#?([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$")


def name_of(value, fallback: str = "") -> str:
    text = _ID.sub("_", str(value or "").strip().lower()).strip("_")
    return text[:40] or fallback


def num(value, lo: float, hi: float, default: float | None = None) -> float | None:
    if isinstance(value, bool):
        return default
    if isinstance(value, str):
        try:
            value = float(value)
        except ValueError:
            return default
    if not isinstance(value, (int, float)) or math.isnan(value) or math.isinf(value):
        return default
    return round(min(max(float(value), lo), hi), 4)


def color(value) -> str | None:
    if isinstance(value, str) and _COLOR.match(value.strip()):
        hexed = value.strip().lstrip("#").upper()
        if len(hexed) == 3:
            hexed = "".join(c * 2 for c in hexed)
        return "#" + hexed
    return None


def vec(value, n: int, lo: float, hi: float) -> list[float] | None:
    if isinstance(value, (list, tuple)) and len(value) == n:
        out = [num(v, lo, hi) for v in value]
        if None not in out:
            return out
    return None


def size3(value, lo: float, hi: float) -> list[float] | float | None:
    """A size may be one number or three."""
    three = vec(value, 3, lo, hi)
    return three if three is not None else num(value, lo, hi)


def points(value, n: int, most: int, lo: float = -200, hi: float = 200) -> list[list[float]] | None:
    if not isinstance(value, (list, tuple)):
        return None
    out = [p for p in (vec(v, n, lo, hi) for v in value[:most]) if p is not None]
    return out if len(out) >= 2 else None


def pick(value, allowed, default=None):
    text = str(value or "").strip().lower().replace(" ", "_").replace("-", "_")
    return text if text in allowed else default


def flag(value) -> bool:
    return value is True or (isinstance(value, str) and value.strip().lower() in ("true", "yes", "on"))


def text_of(value, most: int) -> str | None:
    """One line of plain words, trimmed to length; None when there are none."""
    if not isinstance(value, (str, int, float)) or isinstance(value, bool):
        return None
    line = " ".join(str(value).split())[:most]
    return line or None


def ground(value, lo: float, hi: float) -> list[float] | None:
    """A place on the ground, written as [x, z] or [x, y, z]: always returned as [x, y, z]."""
    three = vec(value, 3, lo, hi)
    if three is not None:
        return three
    two = vec(value, 2, lo, hi)
    return [two[0], 0.0, two[1]] if two is not None else None
