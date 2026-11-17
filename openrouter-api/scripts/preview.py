"""See a `make` without a headset: draws its parts from four sides into one PNG.

    python scripts/preview.py scene.ndjson out_dir          # every `make` line in a file of ImagineScript
    python scripts/preview.py imagine/prompts/make.md out   # ...or in a prompt, a playbook example, a session log

The shapes follow client/Assets/ImagineVR/Scripts/World/Maker.cs closely enough to judge proportion,
placement and colour, which is what goes wrong in a model. It is not the headset's renderer: no
bevels, no lighting model, lumpy shapes drawn smooth. Needs numpy and Pillow.
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import numpy as np

SIZE = 420          # pixels per view
ROLE_TINT = {"wheel": (40, 220, 255), "steer": (255, 80, 220), "spinner": (255, 200, 40), "lamp": (255, 255, 120),
             "seat": (120, 255, 120), "gun": (255, 90, 60), "thruster": (255, 140, 40), "door": (150, 150, 255), "flap": (200, 120, 255)}


def rot(euler) -> np.ndarray:
    """Unity's Quaternion.Euler: about Z, then X, then Y."""
    x, y, z = (math.radians(v) for v in euler)
    cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
    rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return ry @ rx @ rz


def trs(position=(0, 0, 0), euler=(0, 0, 0), scale=(1, 1, 1)) -> np.ndarray:
    m = np.eye(4)
    m[:3, :3] = rot(euler) @ np.diag(scale)
    m[:3, 3] = position
    return m


def ring(radius_x: float, radius_z: float, y: float, sides: int) -> list:
    return [(math.cos(2 * math.pi * i / sides) * radius_x, y, math.sin(2 * math.pi * i / sides) * radius_z) for i in range(sides)]


def loft(rings: list[list]) -> list:
    """Triangles joining a stack of rings, with the ends closed."""
    tris = []
    for lower, upper in zip(rings, rings[1:]):
        n = len(lower)
        for i in range(n):
            a, b, c, d = lower[i], lower[(i + 1) % n], upper[(i + 1) % n], upper[i]
            tris += [(a, b, c), (a, c, d)]
    for end in (rings[0], rings[-1]):
        centre = tuple(np.mean(np.array(end), axis=0))
        tris += [(centre, end[i], end[(i + 1) % len(end)]) for i in range(len(end))]
    return tris


def box(size, taper=(1.0, 1.0)) -> list:
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    bottom = [(-hx, -hy, -hz), (hx, -hy, -hz), (hx, -hy, hz), (-hx, -hy, hz)]
    top = [(-hx * taper[0], hy, -hz * taper[1]), (hx * taper[0], hy, -hz * taper[1]), (hx * taper[0], hy, hz * taper[1]), (-hx * taper[0], hy, hz * taper[1])]
    return loft([bottom, top])


def ellipsoid(radii, sides=14, rings_=8, half=False) -> list:
    stack = []
    for j in range(rings_ + 1):
        t = j / rings_
        angle = (t * 0.5 if half else t - 0.5) * math.pi
        stack.append(ring(max(math.cos(angle), 1e-4) * radii[0], max(math.cos(angle), 1e-4) * radii[2], math.sin(angle) * radii[1], sides))
    return loft(stack)


def shape_triangles(part: dict) -> list:
    shape = part.get("shape", "cube")
    drawn = shape in ("lathe", "slab", "tube")
    s = part.get("scale", part.get("size"))
    s = [s] * 3 if isinstance(s, (int, float)) else list(s) if s else [1, 1, 1] if drawn else [0.3, 0.3, 0.3]
    s = [max(abs(v), 0.001) for v in s]
    sides = int(min(max(part.get("sides", 12), 3), 24))
    taper = part.get("taper", 1)
    tx, tz = (taper, taper) if isinstance(taper, (int, float)) else taper
    if shape in ("sphere", "blob"):
        return ellipsoid([v / 2 for v in s])
    if shape in ("cylinder", "disc"):
        return loft([ring(s[0] / 2, s[2] / 2, -s[1] / 2, sides), ring(s[0] / 2 * tx, s[2] / 2 * tx, s[1] / 2, sides)])
    if shape == "cone":
        return loft([ring(s[0] / 2, s[2] / 2, -s[1] / 2, sides), ring(1e-4, 1e-4, s[1] / 2, sides)])
    if shape == "capsule":
        height, radius = max(s[1], s[0]), s[0] / 2
        body = max(height / 2 - radius, 0)
        stack = [ring(max(math.cos(a), 1e-4) * radius, max(math.cos(a), 1e-4) * radius * s[2] / s[0], math.sin(a) * radius + (body if a > 0 else -body), max(sides, 8))
                 for a in [(-0.5 + i / 8) * math.pi for i in range(9)]]
        return loft(stack[:5] + stack[4:])
    if shape == "pyramid":
        return box(s, (1e-4, 1e-4))
    if shape == "dome":
        return [tuple((x, y, z) for x, y, z in tri) for tri in ellipsoid([s[0] / 2, s[1], s[2] / 2], half=True)]
    if shape == "torus":
        tube = min(max(part.get("radius", 0.15), 0.02), 0.45)
        tris, segments, around = [], 16, 6
        def point(u, v):
            r = (0.5 - tube) + math.cos(v) * tube
            return (math.cos(u) * r * s[0], math.sin(v) * tube * s[1], math.sin(u) * r * s[2])
        for i in range(segments):
            for j in range(around):
                u0, u1, v0, v1 = (2 * math.pi * k for k in (i / segments, (i + 1) / segments, j / around, (j + 1) / around))
                a, b, c, d = point(u0, v0), point(u1, v0), point(u1, v1), point(u0, v1)
                tris += [(a, b, c), (a, c, d)]
        return tris
    if shape == "wedge":
        hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
        a, b, c, d = (-hx, -hy, -hz), (hx, -hy, -hz), (hx, -hy, hz), (-hx, -hy, hz)
        e, f = (hx, hy, hz), (-hx, hy, hz)
        return [(a, b, c), (a, c, d), (d, c, e), (d, e, f), (a, b, e), (a, e, f), (a, d, f), (b, c, e)]
    if shape == "lathe" and len(part.get("profile", [])) >= 2:
        return loft([ring(max(r, 1e-4) * s[0], max(r, 1e-4) * s[2], h * s[1], sides) for r, h in part["profile"]])
    if shape == "slab" and len(part.get("outline", [])) >= 3:
        half = max(part.get("thickness", 0.05), 0.002) / 2
        outline = part["outline"]
        left = [(-half * s[0], up * s[1], forward * s[2]) for forward, up in outline]
        right = [(half * s[0], up * s[1], forward * s[2]) for forward, up in outline]
        return loft([left, right])
    if shape == "tube" and len(part.get("points", [])) >= 2:
        radius, tris = max(part.get("radius", 0.05), 0.002), []
        for p, q in zip(part["points"], part["points"][1:]):
            p, q = np.array(p) * s, np.array(q) * s
            axis = q - p
            length = np.linalg.norm(axis)
            if length < 1e-6:
                continue
            axis /= length
            side = np.cross(axis, [0, 1, 0] if abs(axis[1]) < 0.9 else [1, 0, 0])
            side /= np.linalg.norm(side)
            other = np.cross(axis, side)
            lower = [tuple(p + (math.cos(2 * math.pi * i / 6) * side + math.sin(2 * math.pi * i / 6) * other) * radius) for i in range(6)]
            upper = [tuple(q + (math.cos(2 * math.pi * i / 6) * side + math.sin(2 * math.pi * i / 6) * other) * radius) for i in range(6)]
            tris += loft([lower, upper])
        return tris
    if shape == "building":
        return [tuple((x, y + s[1] / 2, z) for x, y, z in tri) for tri in box(s)]      # it stands on its position
    if shape == "stairs":
        steps, tris = int(part.get("steps", 6)), []
        for i in range(steps):
            tread = box([s[0], s[1] / steps * (i + 1), s[2] / steps])
            tris += [tuple((x, y + s[1] / steps * (i + 1) / 2 - s[1] / 2, z - s[2] / 2 + s[2] / steps * (i + 0.5)) for x, y, z in tri) for tri in tread]
        return tris
    return box(s, (tx, tz) if shape == "cube" else (1, 1))


def triangles_of(make: dict) -> list:
    """Every triangle of a `make`, in the asset's own space, with its colour: [(3x3 array, (r, g, b))]."""
    parts = [p for p in make.get("parts", []) if isinstance(p, dict)]
    by_id = {p["id"]: p for p in parts if "id" in p}
    out = []
    for part in parts:
        chain, at = [part], part
        while len(chain) < 8 and at.get("parent") in by_id and by_id[at["parent"]] not in chain:
            at = by_id[at["parent"]]
            chain.insert(0, at)
        mirror = any(link.get("mirror") for link in chain)
        base = shape_triangles(part)
        tint = tuple(int(str(part.get("color", "#C8C8D0")).lstrip("#")[i:i + 2], 16) for i in (0, 2, 4))
        top = part.get("color_top")
        top = tuple(int(top.lstrip("#")[i:i + 2], 16) for i in (0, 2, 4)) if top else None
        if part.get("role") in ROLE_TINT and make.get("_show_roles"):
            tint, top = ROLE_TINT[part["role"]], None
        ys = [v[1] for tri in base for v in tri] or [0]
        low, high = min(ys), max(ys)
        for side in range(2 if mirror else 1):
            matrix = np.eye(4)
            for link in chain:
                position = list(link.get("position", link.get("at", [0, 0, 0])))
                rotation = list(link.get("rotation", link.get("rot", [0, 0, 0])))
                if side:
                    position[0], rotation = -position[0], [rotation[0], -rotation[1], -rotation[2]]
                matrix = matrix @ trs(position, rotation)
            step = np.array(part.get("step", [0, 0, 0]), dtype=float)
            turn = list(part.get("rotation", part.get("rot", [0, 0, 0])))
            if side:
                step[0], turn = -step[0], [turn[0], -turn[1], -turn[2]]
            step = rot(turn).T @ step
            for k in range(int(min(max(part.get("repeat", 1), 1), 16))):
                local = matrix @ trs(step * k, (0, 0, 0), (-1 if side else 1, 1, 1))
                for tri in base:
                    points = np.array([(local @ np.array([*v, 1.0]))[:3] for v in tri])
                    shade = (np.mean([v[1] for v in tri]) - low) / max(high - low, 1e-6)
                    colour = tuple(int(tint[c] + (top[c] - tint[c]) * shade) for c in range(3)) if top else tint
                    out.append((points, colour))
    return out


VIEWS = {
    "side": np.array([[0, 0, 1], [0, 1, 0], [1, 0, 0]], dtype=float),            # seen from its right: forward is to the right
    "front": np.array([[-1, 0, 0], [0, 1, 0], [0, 0, 1]], dtype=float),          # seen from in front, looking back at it
    "top": np.array([[1, 0, 0], [0, 0, 1], [0, 1, 0]], dtype=float),             # from above: forward is up the picture
    "three_quarter": None,
}


def looking_from(where) -> np.ndarray:
    """
    The view from a point towards the origin: rows are screen-right, screen-up and towards-the-eye.
    The world is left-handed (x right, y up, z forward), so seen from its front a thing's right is on the left.
    """
    towards = np.array(where, dtype=float)
    towards /= np.linalg.norm(towards)
    right = np.array([-towards[2], 0.0, towards[0]])
    right /= np.linalg.norm(right)
    up = np.cross(towards, right)
    return np.array([right, up if up[1] >= 0 else -up, towards])


def draw(tris: list, view: np.ndarray, size: int = SIZE):
    from PIL import Image
    image = np.full((size, size, 3), 236, dtype=np.uint8)
    depth = np.full((size, size), -1e9)
    if not tris:
        return Image.fromarray(image)
    points = np.array([t[0] for t in tris]) @ view.T
    low, high = points[..., :2].reshape(-1, 2).min(axis=0), points[..., :2].reshape(-1, 2).max(axis=0)
    span = max((high - low).max(), 1e-6)
    scale = (size - 40) / span
    offset = (size - (high - low) * scale) / 2
    light = np.array([0.35, 0.8, 0.45])
    light /= np.linalg.norm(light)
    for (world, colour), p in zip(tris, points):
        normal = np.cross(world[1] - world[0], world[2] - world[0])
        length = np.linalg.norm(normal)
        if length < 1e-12:
            continue
        lit = 0.55 + 0.45 * abs(float(normal @ light) / length)
        px = (p[:, 0] - low[0]) * scale + offset[0]
        py = size - ((p[:, 1] - low[1]) * scale + offset[1])
        x0, x1 = int(max(math.floor(px.min()), 0)), int(min(math.ceil(px.max()), size - 1))
        y0, y1 = int(max(math.floor(py.min()), 0)), int(min(math.ceil(py.max()), size - 1))
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        area = (px[1] - px[0]) * (py[2] - py[0]) - (px[2] - px[0]) * (py[1] - py[0])
        if abs(area) < 1e-9:
            continue
        w0 = ((px[1] - gx) * (py[2] - gy) - (px[2] - gx) * (py[1] - gy)) / area
        w1 = ((px[2] - gx) * (py[0] - gy) - (px[0] - gx) * (py[2] - gy)) / area
        w2 = 1 - w0 - w1
        z = w0 * p[0, 2] + w1 * p[1, 2] + w2 * p[2, 2]
        inside = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6) & (z > depth[y0:y1 + 1, x0:x1 + 1])
        depth[y0:y1 + 1, x0:x1 + 1][inside] = z[inside]
        image[y0:y1 + 1, x0:x1 + 1][inside] = [min(int(c * lit), 255) for c in colour]
    return Image.fromarray(image)


def render(make: dict, roles: bool = False):
    """Four views of one `make`, side by side, with its name and size written under them."""
    from PIL import Image, ImageDraw
    tris = triangles_of({**make, "_show_roles": roles})
    quarter = looking_from((1.0, 0.62, 1.25))
    sheet = Image.new("RGB", (SIZE * 4, SIZE + 28), (250, 250, 250))
    pen = ImageDraw.Draw(sheet)
    for i, (name, view) in enumerate(VIEWS.items()):
        sheet.paste(draw(tris, quarter if view is None else view), (i * SIZE, 0))
        pen.text((i * SIZE + 8, 6), name, fill=(60, 60, 60))
    if tris:
        allp = np.array([t[0] for t in tris]).reshape(-1, 3)
        size = allp.max(axis=0) - allp.min(axis=0)
        pen.text((8, SIZE + 8), f'{make.get("name", "?")}: {len(make.get("parts", []))} parts, {size[0]:.2f} wide x {size[1]:.2f} high x {size[2]:.2f} long (m), '
                 f'lowest point y = {allp[:, 1].min():.2f}', fill=(30, 30, 30))
    return sheet


def makes_in(text: str) -> list[dict]:
    found = []
    for line in text.splitlines():
        line = line.strip().rstrip(",")
        if '"op":"make"' not in line.replace(" ", "") or not line.startswith("{"):
            continue
        try:
            found.append(json.loads(line))
        except json.JSONDecodeError:
            pass
    return found


def main(argv: list[str]) -> int:
    if len(argv) < 3:
        print(__doc__)
        return 2
    out = Path(argv[2])
    out.mkdir(parents=True, exist_ok=True)
    makes = makes_in(Path(argv[1]).read_text(encoding="utf-8"))
    for make in makes:
        path = out / (str(make.get("name", "thing")) + ".png")
        render(make, roles="--roles" in argv).save(path)
        print(path)
    return 0 if makes else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
