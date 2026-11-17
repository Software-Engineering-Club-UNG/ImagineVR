"""The third version's library assets, written as parts: python3 scripts/library_v3.py [--preview DIR]

Writes client/Assets/ImagineVR/Resources/LibraryV3.json, which the headset loads into its library at
start (Library/Assets.ThirdVersion.cs) and builds with the same code as a `make`. Writing them here,
as parts, means every one can be drawn and looked at without Unity:

    python3 scripts/library_v3.py --preview /tmp/v3      # one PNG per asset (needs numpy and Pillow)

Order and names come from scripts/catalog_additions.json, so the catalog the server reads and the
library the headset builds cannot disagree. +Z is forward, +Y up, wheels and feet on y = 0, metres.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "client" / "Assets" / "ImagineVR" / "Resources" / "LibraryV3.json"

RUBBER, RIM, CHROME, GLASS, DARK, SEAT, LAMP, TAIL = "#17171A", "#B8BEC6", "#D4D9DF", "#1C2A36", "#202024", "#2A2A30", "#FFF4D0", "#FF2A20"


def P(shape, pos, scale, color, **more):
    part = {"shape": shape, "position": [round(v, 3) for v in pos], "scale": [round(v, 3) for v in scale] if isinstance(scale, (list, tuple)) else scale, "color": color}
    part.update(more)
    return part


# ---------------------------------------------------------------------------- pieces

def wheel(x, z, r, w, rim=RIM, tyre=RUBBER, mirror=True):
    m = {"mirror": "x"} if mirror else {}
    return [P("cylinder", [x, r, z], [2 * r, w, 2 * r], tyre, rotation=[0, 0, 90], sides=16, finish="rubber", role="wheel", **m),
            P("cylinder", [x + (0.01 if x >= 0 else -0.01), r, z], [1.25 * r, w + 0.02, 1.25 * r], rim, rotation=[0, 0, 90], sides=10, finish="chrome", role="wheel", **m)]


def lamps(x, y, z, w=0.34, h=0.12, color=LAMP):
    return [P("cube", [x, y, z], [w, h, 0.08], color, bevel=0.7, emission=1.6, role="lamp", mirror="x")]


def tails(x, y, z, w=0.4, h=0.1):
    return [P("cube", [x, y, z], [w, h, 0.06], TAIL, emission=1.2, mirror="x")]


def interior(seat_x, seat_z, floor_y, wheel_y, wheel_z, width):
    return [P("cube", [seat_x, floor_y + 0.22, seat_z], [0.48, 0.16, 0.5], SEAT, bevel=0.5, finish="cloth", mirror="x"),
            P("cube", [seat_x, floor_y + 0.55, seat_z - 0.27], [0.48, 0.62, 0.12], SEAT, rotation=[-12, 0, 0], bevel=0.5, finish="cloth", mirror="x"),
            P("cube", [0, wheel_y - 0.05, wheel_z + 0.25], [width - 0.2, 0.2, 0.32], "#1C1C20", bevel=0.4),
            P("cube", [seat_x, wheel_y + 0.02, wheel_z + 0.12], [0.3, 0.1, 0.05], "#0A0A0C", emission=0.5),
            P("torus", [seat_x, wheel_y, wheel_z], [0.36, 0.36, 0.36], "#101012", rotation=[72, 0, 0], radius=0.07, finish="rubber", role="steer"),
            P("cylinder", [seat_x, wheel_y - 0.04, wheel_z + 0.08], [0.05, 0.2, 0.05], "#2A2A2E", rotation=[72, 0, 0])]


def car(L=4.4, W=1.8, belt=0.82, roof=1.42, clear=0.24, r=0.33, ww=0.24, color="#2E5FA8", glass=GLASS, hood=1.25, trunk=0.85,
        roof_front=0.35, roof_back=-0.75, nose_drop=0.22, open_top=False, extras=()):
    """A car seen from the side as one board, a glass cabin on it, and what makes it look made."""
    f, b = L / 2, -L / 2
    body = P("slab", [0, 0, 0], [1, 1, 1], color, outline=[[b, clear], [b, belt - 0.08], [b + 0.12, belt], [f - hood, belt + 0.02], [f - 0.18, belt - nose_drop + 0.08],
                                                    [f, belt - nose_drop], [f + 0.02, clear + 0.12], [f - 0.12, clear]], thickness=W, finish="paint")
    parts = [body,
             P("cube", [0, clear + 0.02, 0], [W - 0.12, 0.1, L - 0.3], DARK),
             P("cube", [0, clear + 0.18, f - 0.02], [W - 0.1, 0.16, 0.12], "#222226", bevel=0.4),
             P("cube", [0, clear + 0.18, b + 0.02], [W - 0.1, 0.16, 0.12], "#222226", bevel=0.4),
             P("cube", [0, belt - nose_drop * 0.6, f + 0.01], [W * 0.45, 0.14, 0.05], "#101012", pattern="grid", pattern_scale=0.05)]
    parts += wheel(W / 2 - ww / 2 + 0.02, f - hood * 0.55, r, ww) + wheel(W / 2 - ww / 2 + 0.02, b + trunk * 0.7, r, ww)
    parts += lamps(W / 2 - 0.28, belt - nose_drop * 0.65, f - 0.02) + tails(W / 2 - 0.25, belt - 0.12, b + 0.01)
    parts += [P("cube", [W / 2 + 0.005, belt - 0.12, 0], [0.012, 0.035, L - 0.8], "#7A7A80", finish="chrome", mirror="x"),
              P("cube", [W / 2 + 0.012, belt - 0.05, roof_front - 0.25], [0.02, 0.03, 0.14], CHROME, finish="chrome", mirror="x"),
              P("cube", [W / 2 + 0.012, belt - 0.05, roof_back + 0.3], [0.02, 0.03, 0.14], CHROME, finish="chrome", mirror="x")]
    if not open_top:
        parts += [P("slab", [0, 0, 0], [1, 1, 1], glass, outline=[[roof_back - 0.55, belt], [roof_back, roof], [roof_front, roof], [roof_front + 0.75, belt]], thickness=W - 0.14, finish="glass"),
                  P("cube", [0, roof + 0.015, (roof_front + roof_back) / 2], [W - 0.18, 0.05, roof_front - roof_back + 0.1], color, bevel=0.6, finish="paint"),
                  P("tube", [0, 0, 0], [1, 1, 1], color, points=[[W / 2 - 0.08, belt + 0.01, roof_front + 0.74], [W / 2 - 0.09, roof, roof_front], [W / 2 - 0.09, roof, roof_back], [W / 2 - 0.08, belt + 0.01, roof_back - 0.54]],
                    radius=0.035, finish="paint", mirror="x"),
                  P("cube", [W / 2 + 0.06, belt + 0.08, roof_front + 0.6], [0.1, 0.08, 0.14], color, bevel=0.6, finish="paint", mirror="x")]
    parts += interior(-W * 0.22, roof_front - 0.65, clear, belt + 0.08, roof_front + 0.12, W)
    parts += list(extras)
    return parts


def car_seat(W=1.8, belt=0.82, roof_front=0.35):
    return [round(-W * 0.22, 2), round(belt + 0.36, 2), round(roof_front - 0.55, 2)]


def fuselage(length, radius, color, nose=0.18, tail=0.12, z0=None):
    """A body of revolution lying along Z, its middle at z0."""
    z0 = -length / 2 if z0 is None else z0
    prof = [[0, 0], [radius * 0.55, 0.06], [radius * 0.9, 0.18], [radius, 0.32], [radius, 0.62], [radius * 0.7, 0.86], [radius * tail * 2, 1.0]]
    return P("lathe", [0, radius + 0.0, z0 + length], [1, length, 1], color, rotation=[-90, 0, 0], profile=[[r, h] for r, h in prof], sides=16, finish="paint")


def wing(z, y, span, root, tip, sweep, thick, color, dihedral=0, mirror=True):
    """A wing seen from above: root chord at the body, tip chord at the end, swept back by `sweep`."""
    outline = [[root / 2, 0], [-root / 2, 0], [-sweep - tip / 2, span], [-sweep + tip / 2, span]]
    part = P("slab", [0, y, z], [1, 1, 1], color, rotation=[0, 0, 90 - dihedral], outline=outline, thickness=thick, finish="paint")
    if mirror:
        part["mirror"] = "x"
    return part


def spinner(pos, facing, blades, length, chord, color, hub="#303034"):
    """
    A propeller or rotor. Every piece of it turns about one axis, its own up as the headset sees it,
    so they all share one rotation (`facing`: up for a rotor, forward for a propeller, side for a tail
    rotor) and the blades differ only by which way they are long.
    """
    turn = {"up": [0, 0, 0], "forward": [90, 0, 0], "side": [0, 0, 90]}[facing]
    thin = chord * 0.18
    parts = [P("cylinder", pos, [chord * 2.2, chord * 1.4, chord * 2.2], hub, rotation=turn, role="spinner")]
    parts.append(P("cube", pos, [length, thin, chord], color, rotation=turn, role="spinner"))
    if blades >= 3:
        parts.append(P("cube", pos, [chord, thin, length], color, rotation=turn, role="spinner"))
    return parts


# ---------------------------------------------------------------------------- vehicles

def vehicles():
    v = {}
    v["sports_car"] = dict(kind="vehicle", drive="car", seat=car_seat(1.9, 0.72, 0.15), parts=car(L=4.45, W=1.9, belt=0.72, roof=1.2, clear=0.18, r=0.34, ww=0.28, color="#C81E1E", hood=1.55, trunk=0.8, roof_front=0.15, roof_back=-0.85, nose_drop=0.26,
        extras=[P("cube", [0, 1.0, -2.05], [1.6, 0.04, 0.32], DARK), P("cube", [0.65, 0.88, -2.05], [0.05, 0.22, 0.12], DARK, mirror="x"),
                P("cube", [0, 0.74, 1.1], [0.55, 0.05, 0.75], "#A01616", bevel=0.5, rotation=[-6, 0, 0]),
                P("cylinder", [0.42, 0.26, -2.25], [0.1, 0.16, 0.1], CHROME, rotation=[90, 0, 0], role="thruster", mirror="x")]))
    v["pickup_truck"] = dict(kind="vehicle", drive="car", seat=car_seat(2.0, 1.05, 0.7), parts=car(L=5.3, W=2.0, belt=1.05, roof=1.85, clear=0.38, r=0.42, ww=0.3, color="#5B6E3A", hood=1.4, trunk=2.0, roof_front=0.7, roof_back=-0.25, nose_drop=0.12,
        extras=[P("cube", [0, 1.12, -1.65], [1.9, 0.08, 1.9], DARK), P("cube", [0.96, 1.3, -1.65], [0.08, 0.36, 1.9], "#5B6E3A", finish="paint", mirror="x"),
                P("cube", [0, 1.3, -2.6], [2.0, 0.36, 0.08], "#5B6E3A", finish="paint"), P("tube", [0, 0, 0], [1, 1, 1], CHROME, points=[[0.9, 1.5, -0.7], [0.9, 2.0, -0.75], [-0.9, 2.0, -0.75], [-0.9, 1.5, -0.7]], radius=0.04, finish="chrome")]))
    police_bar = [P("cube", [0, 1.5, -0.2], [1.2, 0.1, 0.25], "#1A1A1E"), P("cube", [-0.32, 1.57, -0.2], [0.5, 0.08, 0.2], "#FF2030", emission=2.2), P("cube", [0.32, 1.57, -0.2], [0.5, 0.08, 0.2], "#2050FF", emission=2.2),
                  P("cube", [0.905, 0.62, 0], [0.01, 0.3, 2.2], "#1A1A1E", mirror="x")]
    v["police_car"] = dict(kind="vehicle", drive="car", seat=car_seat(), parts=car(color="#F2F2F2", extras=police_bar))
    v["taxi"] = dict(kind="vehicle", drive="car", seat=car_seat(), parts=car(color="#F5C21B", extras=[P("cube", [0, 1.52, -0.2], [0.6, 0.18, 0.25], "#FFF6D8", emission=1.2, bevel=0.4),
        P("cube", [0.905, 0.62, 0], [0.01, 0.08, 3.2], "#1A1A1A", pattern="checker", pattern_color="#FFFFFF", pattern_scale=0.08, mirror="x")]))
    race = [P("slab", [0, 0, 0], [1, 1, 1], "#D8261C", outline=[[-2.2, 0.15], [-2.2, 0.6], [-1.0, 0.75], [0.2, 0.62], [2.4, 0.3], [2.4, 0.15]], thickness=0.9, finish="paint"),
            P("cube", [0, 0.55, -0.6], [0.7, 0.3, 1.5], "#D8261C", bevel=0.6, finish="paint"), P("cube", [0, 0.1, 0], [1.4, 0.06, 4.4], DARK),
            P("cube", [0, 0.25, 2.55], [1.9, 0.04, 0.4], "#202020"), P("cube", [0.9, 0.3, 2.55], [0.04, 0.2, 0.5], "#D8261C", mirror="x"),
            P("cube", [0, 0.95, -2.25], [1.4, 0.05, 0.45], "#202020"), P("cube", [0.55, 0.75, -2.25], [0.05, 0.4, 0.45], "#D8261C", mirror="x"),
            P("cube", [0, 0.72, -0.25], [0.5, 0.22, 0.6], SEAT, bevel=0.5), P("tube", [0, 0, 0], [1, 1, 1], "#303030", points=[[0.3, 0.6, -0.4], [0, 1.0, -0.6], [-0.3, 0.6, -0.4]], radius=0.04),
            P("torus", [0, 0.72, 0.25], [0.3, 0.3, 0.3], "#101012", rotation=[72, 0, 0], radius=0.08, role="steer"),
            P("cube", [0.2, 0.62, 2.35], [0.2, 0.06, 0.06], "#FFFFFF", emission=0.5, mirror="x")]
    race += wheel(0.85, 1.55, 0.34, 0.36) + wheel(0.85, -1.45, 0.36, 0.42)
    v["race_car"] = dict(kind="vehicle", drive="car", seat=[0, 0.98, -0.35], parts=race)
    v["monster_truck"] = dict(kind="vehicle", drive="car", seat=[-0.45, 2.75, 0.1], parts=[p if p["shape"] != "cylinder" or p.get("role") != "wheel" else p for p in
        [dict(q, position=[q["position"][0], q["position"][1] + 1.25, q["position"][2]]) for q in car(L=4.6, W=2.3, belt=1.0, roof=1.6, clear=0.3, r=0.0001, ww=0.01, color="#3A6BD8", hood=1.3, trunk=1.0, roof_front=0.4, roof_back=-0.6, nose_drop=0.15)
         if q.get("role") != "wheel"]] + wheel(1.15, 1.45, 0.82, 0.75) + wheel(1.15, -1.35, 0.82, 0.75) + [
        P("cube", [0, 1.1, 0], [1.4, 0.3, 3.4], "#303034"), P("cylinder", [0.7, 1.0, 1.45], [0.12, 0.9, 0.12], "#C0C0C8", mirror="x"),
        P("cylinder", [0.7, 1.0, -1.35], [0.12, 0.9, 0.12], "#C0C0C8", mirror="x")])
    jeep = car(L=3.9, W=1.85, belt=1.0, roof=1.9, clear=0.42, r=0.4, ww=0.3, color="#4F5D2F", hood=1.15, trunk=0.7, roof_front=0.2, roof_back=-0.9, nose_drop=0.05, open_top=True,
               extras=[P("tube", [0, 0, 0], [1, 1, 1], "#2A2A2A", points=[[0.85, 1.0, -0.6], [0.85, 1.75, -0.65], [-0.85, 1.75, -0.65], [-0.85, 1.0, -0.6]], radius=0.05),
                       P("cube", [0, 1.35, 0.6], [1.7, 0.5, 0.05], GLASS, finish="glass"), P("cylinder", [0, 0.95, -2.0], [0.7, 0.25, 0.7], RUBBER, rotation=[90, 0, 0])])
    v["jeep"] = dict(kind="vehicle", drive="car", seat=car_seat(1.85, 1.0, 0.2), parts=jeep)
    v["van"] = dict(kind="vehicle", drive="car", seat=car_seat(2.0, 1.0, 1.0), parts=car(L=5.0, W=2.0, belt=1.0, roof=2.1, clear=0.3, r=0.36, ww=0.26, color="#E6E6E2", hood=0.8, trunk=0.3, roof_front=1.0, roof_back=-2.3, nose_drop=0.12,
        extras=[P("cube", [0, 1.55, -0.9], [2.0, 1.1, 3.0], "#E6E6E2", finish="paint"), P("cube", [1.005, 1.4, -0.9], [0.01, 0.35, 2.6], "#C8302A", mirror="x")]))
    fire = car(L=7.5, W=2.4, belt=1.25, roof=2.6, clear=0.45, r=0.5, ww=0.36, color="#C8201C", hood=0.6, trunk=4.7, roof_front=3.1, roof_back=1.9, nose_drop=0.1,
               extras=[P("cube", [0, 1.6, -1.2], [2.4, 1.3, 4.8], "#C8201C", finish="paint"), P("cube", [1.21, 1.3, -1.2], [0.02, 0.6, 4.6], "#B8BEC6", pattern="panels", pattern_scale=0.6, mirror="x"),
                       P("cube", [0, 2.35, -1.0], [0.7, 0.18, 6.0], "#C8C8CC", finish="metal", pattern="grid", pattern_scale=0.3), P("cube", [0, 2.75, 2.5], [1.4, 0.1, 0.3], "#FF2030", emission=2),
                       P("cylinder", [1.0, 1.7, 1.0], [0.6, 0.35, 0.6], "#E8E0C8", rotation=[0, 0, 90], mirror="x")] + wheel(1.04, -2.4, 0.5, 0.36))
    v["fire_truck"] = dict(kind="vehicle", drive="car", seat=[-0.55, 1.95, 2.4], parts=fire)
    bike = wheel(0, 0.72, 0.34, 0.14, mirror=False) + wheel(0, -0.72, 0.34, 0.16, mirror=False) + [
        P("tube", [0, 0, 0], [1, 1, 1], "#B02020", points=[[0, 0.34, -0.72], [0, 0.65, -0.25], [0, 0.9, 0.45], [0, 0.34, 0.72]], radius=0.05, finish="paint"),
        P("cube", [0, 0.72, -0.1], [0.32, 0.3, 0.7], "#B02020", bevel=0.6, finish="paint"), P("cube", [0, 0.85, -0.45], [0.3, 0.1, 0.55], SEAT, bevel=0.5),
        P("cylinder", [0, 0.5, 0.0], [0.3, 0.3, 0.3], "#606068", rotation=[0, 0, 90], finish="metal"), P("tube", [0, 0, 0], [1, 1, 1], CHROME, points=[[0.05, 0.42, -0.1], [0.12, 0.35, -0.7], [0.12, 0.4, -0.95]], radius=0.04, role="thruster"),
        P("cylinder", [0, 1.02, 0.5], [0.06, 0.66, 0.06], "#202024", rotation=[0, 0, 90], role="steer"), P("cube", [0, 0.95, 0.62], [0.2, 0.15, 0.08], LAMP, emission=1.6, role="lamp"),
        P("cube", [0, 0.68, -0.85], [0.1, 0.05, 0.04], TAIL, emission=1.2)]
    v["motorcycle"] = dict(kind="vehicle", drive="bike", seat=[0, 1.6, -0.35], parts=bike)
    cycle = [P("torus", [0, 0.34, 0.55], [0.68, 0.68, 0.68], RUBBER, rotation=[0, 0, 90], radius=0.04, role="wheel"), P("torus", [0, 0.34, -0.5], [0.68, 0.68, 0.68], RUBBER, rotation=[0, 0, 90], radius=0.04, role="wheel"),
             P("tube", [0, 0, 0], [1, 1, 1], "#2A7AD8", points=[[0, 0.34, -0.5], [0, 0.8, -0.2], [0, 0.82, 0.38], [0, 0.34, 0.55]], radius=0.022, finish="paint"),
             P("tube", [0, 0, 0], [1, 1, 1], "#2A7AD8", points=[[0, 0.8, -0.2], [0, 0.4, 0.0], [0, 0.82, 0.38]], radius=0.022, finish="paint"),
             P("cube", [0, 0.9, -0.24], [0.14, 0.05, 0.24], SEAT, bevel=0.6), P("cylinder", [0, 0.98, 0.4], [0.04, 0.5, 0.04], "#202024", rotation=[0, 0, 90], role="steer"),
             P("cylinder", [0, 0.4, 0.0], [0.15, 0.03, 0.15], CHROME, rotation=[0, 0, 90])]
    v["bicycle"] = dict(kind="vehicle", drive="bike", seat=[0, 1.65, -0.25], parts=cycle)
    kart = wheel(0.55, 0.5, 0.15, 0.16) + wheel(0.58, -0.45, 0.17, 0.22) + [
        P("cube", [0, 0.12, 0], [1.0, 0.06, 1.5], "#E8B020", finish="paint"), P("cube", [0, 0.28, -0.15], [0.42, 0.32, 0.4], SEAT, bevel=0.6),
        P("cube", [0, 0.22, 0.72], [0.9, 0.18, 0.18], "#E8B020", bevel=0.5, finish="paint"), P("torus", [0, 0.45, 0.3], [0.26, 0.26, 0.26], "#101012", rotation=[60, 0, 0], radius=0.09, role="steer"),
        P("cube", [0, 0.3, -0.62], [0.35, 0.25, 0.25], "#404044", role="thruster")]
    v["go_kart"] = dict(kind="vehicle", drive="car", seat=[0, 0.85, -0.2], parts=kart)
    tractor = [P("cube", [0, 1.0, 0.6], [0.9, 0.7, 1.9], "#2F8F3A", bevel=0.3, finish="paint"), P("cube", [0, 1.85, -0.55], [1.2, 1.3, 1.1], GLASS, finish="glass"),
               P("cube", [0, 2.55, -0.55], [1.35, 0.08, 1.25], "#2F8F3A", finish="paint"), P("cylinder", [0.25, 1.6, 1.0], [0.09, 0.9, 0.09], "#303030", role="thruster"),
               P("cube", [0, 1.3, -0.55], [0.5, 0.2, 0.5], SEAT, bevel=0.5), P("torus", [0, 1.65, -0.15], [0.34, 0.34, 0.34], "#101012", rotation=[50, 0, 0], radius=0.07, role="steer"),
               P("cube", [0, 1.15, 1.6], [0.6, 0.2, 0.1], LAMP, emission=1.4, role="lamp")]
    tractor += wheel(0.75, -0.6, 0.78, 0.45, rim="#E8C020") + wheel(0.6, 1.2, 0.42, 0.25, rim="#E8C020")
    v["tractor"] = dict(kind="vehicle", drive="car", seat=[0, 2.1, -0.55], parts=tractor)
    tank = [P("cube", [0, 0.85, 0], [2.9, 0.75, 6.2], "#5A6640", bevel=0.2, finish="metal", taper=[0.92, 0.85]), P("cube", [1.25, 0.45, 0], [0.6, 0.75, 6.4], "#2E2E2A", pattern="grid", pattern_scale=0.25, mirror="x"),
            P("cube", [0, 1.55, -0.3], [2.1, 0.6, 2.6], "#5A6640", bevel=0.4, finish="metal", taper=[0.85, 0.8]), P("cylinder", [0, 1.6, 2.4], [0.2, 3.2, 0.2], "#4A5434", rotation=[90, 0, 0], finish="metal", role="gun"),
            P("cylinder", [0, 1.6, 4.0], [0.28, 0.3, 0.28], "#3A4228", rotation=[90, 0, 0]), P("cylinder", [0.4, 2.0, -0.6], [0.6, 0.2, 0.6], "#4A5434"),
            P("cube", [0, 1.1, 3.05], [0.5, 0.12, 0.1], LAMP, emission=1.2, role="lamp")]
    for z in (-2.2, -1.1, 0.0, 1.1, 2.2):
        tank += [P("cylinder", [1.25, 0.36, z], [0.62, 0.66, 0.62], "#3A3A34", rotation=[0, 0, 90], role="wheel", mirror="x")]
    v["tank"] = dict(kind="vehicle", drive="tank", seat=[0, 2.6, -0.3], fires={"sound": "boom", "every": 1.6, "speed": 60, "size": 2.2, "damage": 15, "blast": 5, "color": "#FFB040", "muzzle": [0, 1.6, 4.2]}, parts=tank)
    train = [P("cylinder", [0, 2.0, 0.8], [1.6, 4.6, 1.6], "#202024", rotation=[90, 0, 0], sides=18, finish="metal"), P("cube", [0, 1.95, -2.2], [2.4, 2.3, 2.0], "#7A1E1A", finish="paint"),
             P("cube", [0, 3.18, -2.2], [2.6, 0.14, 2.2], "#202024"), P("cube", [1.21, 2.4, -2.2], [0.02, 0.7, 1.2], GLASS, finish="glass", mirror="x"),
             P("cylinder", [0, 3.15, 2.4], [0.5, 1.0, 0.5], "#202024", taper=1.5), P("dome", [0, 2.8, 0.6], [0.6, 0.4, 0.6], "#C8A040", finish="metal"),
             P("cube", [0, 0.75, 0], [1.8, 0.4, 6.6], "#303034"), P("wedge", [0, 0.25, 3.4], [2.0, 0.7, 0.8], "#B02020"), P("cylinder", [0, 2.0, 3.12], [0.4, 0.1, 0.4], LAMP, rotation=[90, 0, 0], emission=1.8, role="lamp"),
             P("cube", [0, 1.4, -2.6], [0.6, 0.5, 0.6], SEAT, bevel=0.4)]
    for z in (1.6, 0.0, -1.6):
        train += [P("cylinder", [0.95, 0.7, z], [1.3, 0.16, 1.3], "#C0201C", rotation=[0, 0, 90], role="wheel", sides=14, mirror="x")]
    train += [P("cylinder", [0.95, 0.42, 2.7], [0.8, 0.14, 0.8], "#C0201C", rotation=[0, 0, 90], role="wheel", mirror="x"), P("cube", [1.05, 0.7, 0], [0.05, 0.1, 3.4], CHROME, mirror="x")]
    v["train_engine"] = dict(kind="vehicle", drive="rail", seat=[0.6, 3.0, -2.4], parts=train)
    boat = [P("slab", [0, 0, 0], [1, 1, 1], "#F2F2F2", outline=[[-2.4, 0.2], [-2.4, 0.95], [1.6, 1.0], [2.6, 0.85], [2.0, 0.25], [0.8, 0.0], [-2.2, 0.05]], thickness=1.9, finish="gloss"),
            P("cube", [0.0, 0.9, -0.7], [1.6, 0.06, 2.4], "#C8A878", pattern="planks", pattern_scale=0.15), P("slab", [0, 0, 0], [1, 1, 1], GLASS, outline=[[0.4, 1.0], [0.6, 1.45], [0.85, 1.45], [1.1, 1.0]], thickness=1.6, finish="glass"),
            P("cube", [0.4, 1.15, -0.4], [0.5, 0.3, 0.5], "#2050A0", bevel=0.5, mirror="x"), P("torus", [-0.4, 1.35, 0.25], [0.3, 0.3, 0.3], "#101012", rotation=[70, 0, 0], radius=0.07, role="steer"),
            P("cube", [0, 0.85, -2.55], [0.45, 0.8, 0.45], "#202024", bevel=0.4, role="thruster"), P("cube", [0.96, 0.75, 0], [0.02, 0.1, 4.2], "#2050A0", mirror="x"),
            P("cube", [0, 1.05, 2.35], [0.3, 0.1, 0.1], LAMP, emission=1.4, role="lamp")]
    v["speedboat"] = dict(kind="vehicle", drive="boat", seat=[-0.4, 1.9, -0.25], flags=["floats"], parts=boat)

    # ---- aircraft
    plane = [fuselage(7.6, 0.6, "#F2F2F2", z0=-4.4), P("slab", [0, 0, 0], [1, 1, 1], GLASS, outline=[[0.4, 1.15], [0.1, 1.6], [-0.9, 1.65], [-1.3, 1.2]], thickness=1.0, finish="glass"),
             wing(0.0, 1.5, 5.2, 1.6, 1.0, 0.25, 0.14, "#F2F2F2", dihedral=4), P("cube", [5.3, 1.5, -0.15], [0.12, 0.05, 0.3], "#FF2030", emission=1.5),
             wing(-4.1, 0.95, 1.8, 0.9, 0.5, 0.3, 0.08, "#F2F2F2"), P("slab", [0, 0, 0], [1, 1, 1], "#2050A0", outline=[[-3.6, 0.9], [-4.5, 0.95], [-4.6, 2.3], [-4.1, 2.3]], thickness=0.1, finish="paint"),
             P("cube", [0.61, 0.95, -0.6], [0.02, 0.12, 5.4], "#2050A0", mirror="x"), P("cube", [0, 0.95, -0.5], [0.5, 0.3, 0.5], SEAT, bevel=0.5),
             P("cube", [0, 1.05, 0.4], [0.9, 0.3, 0.2], "#1C1C20"), P("cylinder", [0, 0.9, 0.2], [0.05, 0.4, 0.05], "#202020", rotation=[-60, 0, 0], role="steer")]
    plane += spinner([0, 0.65, 3.35], "forward", 2, 1.9, 0.14, "#303034")
    plane += [P("cylinder", [1.0, 0.25, 0.2], [0.5, 0.18, 0.5], RUBBER, rotation=[0, 0, 90], role="wheel", mirror="x"), P("cylinder", [0, 0.2, 2.6], [0.4, 0.14, 0.4], RUBBER, rotation=[0, 0, 90], role="wheel"),
              P("tube", [0, 0, 0], [1, 1, 1], "#808088", points=[[0.3, 0.7, 0.2], [1.0, 0.25, 0.2]], radius=0.04, mirror="x")]
    v["prop_plane"] = dict(kind="vehicle", drive="plane", seat=[0, 1.6, -0.5], flags=["flies"], parts=plane)
    biplane = [fuselage(6.2, 0.55, "#C8302A", z0=-3.6), wing(0.3, 0.75, 4.3, 1.4, 1.4, 0.0, 0.1, "#E8D8B0"), wing(0.6, 2.35, 4.6, 1.4, 1.4, 0.0, 0.1, "#E8D8B0"),
               P("cylinder", [2.6, 1.55, 0.45], [0.06, 1.6, 0.06], "#5A3A20", mirror="x"), P("cylinder", [0.6, 1.55, 0.45], [0.06, 1.6, 0.06], "#5A3A20", mirror="x"),
               wing(-3.4, 0.9, 1.5, 0.8, 0.5, 0.2, 0.06, "#E8D8B0"), P("slab", [0, 0, 0], [1, 1, 1], "#C8302A", outline=[[-3.0, 0.9], [-3.7, 0.95], [-3.8, 2.0], [-3.4, 2.0]], thickness=0.08),
               P("cube", [0, 1.0, -1.0], [0.5, 0.3, 0.5], "#4A3020", bevel=0.5), P("cylinder", [0, 1.15, -0.2], [0.05, 0.4, 0.05], "#202020", rotation=[-60, 0, 0], role="steer"),
               P("torus", [0, 1.15, -1.0], [0.75, 0.2, 0.9], "#4A3020", radius=0.08)]
    biplane += spinner([0, 0.6, 2.7], "forward", 2, 2.0, 0.15, "#5A3A20")
    biplane += [P("cylinder", [0.8, 0.3, 0.6], [0.6, 0.14, 0.6], RUBBER, rotation=[0, 0, 90], role="wheel", mirror="x")]
    v["biplane"] = dict(kind="vehicle", drive="plane", seat=[0, 1.75, -1.0], flags=["flies"], parts=biplane)
    jumbo = [fuselage(36, 2.6, "#F4F4F4", z0=-18), wing(1.0, 1.8, 18, 7.5, 2.2, 6.5, 0.5, "#E0E2E6", dihedral=5), wing(-15.5, 3.2, 6.5, 4.0, 1.5, 2.8, 0.25, "#E0E2E6"),
             P("slab", [0, 0, 0], [1, 1, 1], "#1E4A9A", outline=[[-12.5, 4.5], [-17.5, 4.6], [-18.2, 11.0], [-15.6, 11.0]], thickness=0.4, finish="paint"),
             P("cube", [2.62, 3.3, 0], [0.02, 0.45, 30], "#1C2A36", pattern="windows", pattern_scale=0.9, mirror="x"),
             P("cube", [2.62, 2.0, 0], [0.02, 0.3, 32], "#1E4A9A", mirror="x"),
             P("slab", [0, 0, 0], [1, 1, 1], GLASS, outline=[[16.8, 3.0], [16.2, 3.8], [15.0, 4.0], [15.0, 3.0]], thickness=2.2, finish="glass")]
    for x in (6.5, 11.0):
        jumbo += [P("cylinder", [x, 0.4, 3.0], [1.3, 3.4, 1.3], "#C8CCD2", rotation=[90, 0, 0], mirror="x", finish="metal"), P("cylinder", [x, 0.4, 4.75], [1.0, 0.1, 1.0], "#202024", rotation=[90, 0, 0], role="spinner", mirror="x")]
    jumbo += [P("cylinder", [2.2, 0.55, -1.5], [1.1, 0.5, 1.1], RUBBER, rotation=[0, 0, 90], role="wheel", mirror="x"), P("cylinder", [0, 0.5, 13.5], [0.9, 0.4, 0.9], RUBBER, rotation=[0, 0, 90], role="wheel")]
    v["airliner"] = dict(kind="vehicle", drive="plane", seat=[-0.6, 4.6, 15.5], flags=["flies"], parts=jumbo)
    jet = [fuselage(12, 0.75, "#7A8490", z0=-6.5), P("slab", [0, 0, 0], [1, 1, 1], GLASS, outline=[[3.9, 1.4], [3.0, 2.1], [1.2, 2.15], [0.4, 1.5]], thickness=0.9, finish="glass"),
           wing(-1.2, 0.85, 4.6, 5.2, 0.8, 3.6, 0.15, "#6A7480"), wing(-5.6, 1.0, 2.2, 2.0, 0.7, 1.2, 0.1, "#6A7480"),
           P("slab", [0, 0, 0], [1, 1, 1], "#6A7480", outline=[[-3.6, 1.4], [-5.9, 1.5], [-6.5, 3.6], [-5.3, 3.6]], thickness=0.12, rotation=[0, 0, 10], position=[0.9, 0, 0], mirror="x"),
           P("cylinder", [0, 0.75, -6.3], [1.0, 0.6, 1.0], "#303034", rotation=[90, 0, 0], role="thruster"), P("cylinder", [0, 0.75, -6.6], [0.8, 0.1, 0.8], "#FF8030", rotation=[90, 0, 0], emission=1.5),
           P("cube", [0, 1.2, 1.8], [0.5, 0.3, 0.5], SEAT, bevel=0.5), P("cylinder", [0.85, 0.95, 2.5], [0.06, 1.2, 0.06], "#202024", rotation=[90, 0, 0], role="gun", mirror="x"),
           P("cylinder", [0.8, 0.25, -0.5], [0.45, 0.16, 0.45], RUBBER, rotation=[0, 0, 90], role="wheel", mirror="x"), P("cylinder", [0, 0.22, 4.3], [0.36, 0.12, 0.36], RUBBER, rotation=[0, 0, 90], role="wheel")]
    v["fighter_jet"] = dict(kind="vehicle", drive="plane", seat=[0, 2.0, 1.8], flags=["flies"], fires={"sound": "gunshot", "every": 0.08, "auto": True, "speed": 80, "size": 0.6, "damage": 2, "color": "#FFD060", "muzzle": [0, 0.95, 3.2]}, parts=jet)
    heli = [P("lathe", [0, 1.6, 1.8], [1, 3.8, 1], "#2E5A3A", rotation=[-90, 0, 0], profile=[[0, 0], [0.9, 0.15], [1.1, 0.45], [1.0, 0.8], [0.5, 1.0]], sides=16, finish="paint"),
            P("slab", [0, 0, 0], [1, 1, 1], GLASS, outline=[[1.9, 0.9], [2.2, 1.6], [1.4, 2.35], [0.6, 2.4], [0.6, 0.9]], thickness=1.7, finish="glass"),
            P("tube", [0, 0, 0], [1, 1, 1], "#2E5A3A", points=[[0, 1.8, -1.9], [0, 2.0, -4.5], [0, 2.2, -6.5]], radius=0.22, finish="paint"),
            P("slab", [0, 0, 0], [1, 1, 1], "#2E5A3A", outline=[[-6.0, 2.1], [-6.8, 2.1], [-7.0, 3.2], [-6.5, 3.2]], thickness=0.1),
            P("cube", [0.75, 0.15, 0.4], [0.08, 0.08, 3.4], "#303034", mirror="x"), P("cylinder", [0.75, 0.5, 1.2], [0.06, 0.7, 0.06], "#303034", mirror="x"),
            P("cylinder", [0.75, 0.5, -0.4], [0.06, 0.7, 0.06], "#303034", mirror="x"), P("cylinder", [0, 3.1, 0], [0.4, 0.5, 0.4], "#303034"),
            P("cube", [0, 1.0, 0.8], [0.5, 0.3, 0.5], SEAT, bevel=0.5, mirror="x", position=[0.38, 1.0, 0.8]), P("cylinder", [0.38, 1.25, 1.4], [0.05, 0.6, 0.05], "#202020", rotation=[-30, 0, 0], role="steer", mirror="x"),
            P("cube", [0, 1.2, 2.5], [0.4, 0.1, 0.1], LAMP, emission=1.4, role="lamp")]
    heli += spinner([0, 3.35, 0], "up", 4, 9.0, 0.32, "#202024")
    heli += spinner([0.2, 2.6, -6.6], "side", 2, 1.4, 0.15, "#202024")
    v["helicopter"] = dict(kind="vehicle", drive="heli", seat=[-0.38, 1.85, 0.75], flags=["flies"], parts=heli)
    hover = [P("slab", [0, 0, 0], [1, 1, 1], "#E8EAF0", outline=[[-1.2, 0.5], [-1.0, 0.85], [0.6, 0.9], [1.3, 0.65], [1.0, 0.45], [-1.0, 0.4]], thickness=0.6, finish="gloss"),
             P("cube", [0, 0.9, -0.35], [0.4, 0.12, 0.7], SEAT, bevel=0.6), P("cylinder", [0.5, 0.45, -0.8], [0.35, 0.5, 0.35], "#30343A", rotation=[90, 0, 0], role="thruster", mirror="x"),
             P("cylinder", [0.5, 0.45, -1.06], [0.28, 0.04, 0.28], "#40C8FF", rotation=[90, 0, 0], emission=2.5, mirror="x"), P("cube", [0, 0.3, 0], [0.9, 0.08, 1.6], "#40C8FF", emission=1.2),
             P("cylinder", [0, 1.12, 0.65], [0.05, 0.75, 0.05], "#202024", rotation=[0, 0, 90], role="steer"), P("cube", [0, 0.85, 1.2], [0.25, 0.08, 0.08], LAMP, emission=1.6, role="lamp")]
    v["hover_bike"] = dict(kind="vehicle", drive="hover", seat=[0, 1.7, -0.35], flags=["flies"], parts=hover)
    star = [fuselage(7.5, 0.6, "#D8DCE2", z0=-4.0), P("slab", [0, 0, 0], [1, 1, 1], GLASS, outline=[[1.6, 1.1], [1.0, 1.55], [-0.3, 1.6], [-0.7, 1.15]], thickness=0.8, finish="glass"),
            wing(-2.2, 0.85, 4.0, 2.4, 0.9, 0.7, 0.1, "#C8CCD2", dihedral=14), wing(-2.2, 0.55, 4.0, 2.4, 0.9, 0.7, 0.1, "#C8CCD2", dihedral=-14),
            P("cylinder", [4.0, 1.85, -1.4], [0.12, 2.6, 0.12], "#404448", rotation=[90, 0, 0], role="gun", mirror="x"), P("cylinder", [4.0, -0.15, -1.4], [0.12, 2.6, 0.12], "#404448", rotation=[90, 0, 0], role="gun", mirror="x"),
            P("cylinder", [0.9, 1.1, -3.4], [0.6, 1.4, 0.6], "#505458", rotation=[90, 0, 0], mirror="x", role="thruster"), P("cylinder", [0.9, 1.1, -4.15], [0.45, 0.06, 0.45], "#FF5030", rotation=[90, 0, 0], emission=2.5, mirror="x"),
            P("cube", [0, 1.0, -0.2], [0.45, 0.3, 0.45], SEAT, bevel=0.5), P("cube", [0.61, 0.8, 0], [0.02, 0.15, 5.6], "#C8302A", mirror="x")]
    v["starfighter"] = dict(kind="vehicle", drive="space", seat=[0, 1.6, -0.2], flags=["flies"], fires={"sound": "laser", "every": 0.18, "auto": True, "speed": 90, "size": 0.7, "damage": 3, "color": "#FF4030", "muzzle": [0, 1, 4]}, parts=star)
    return v


# ---------------------------------------------------------------------------- buildings

def buildings():
    b = {}
    b["townhouse"] = dict(parts=[P("building", [0, 0, 0], [6, 9, 9], "#B85A3C", style="brick", roof="gable", floors=3, trim="#E8E0D0"),
                                 P("stairs", [0, 0.4, 4.9], [1.6, 0.8, 1.0], "#A8A8A0", steps=3)])
    b["brownstone"] = dict(parts=[P("building", [0, 0, 0], [8, 12.5, 12], "#7A4A32", style="brick", roof="flat", floors=4, trim="#C8B898"),
                                  P("stairs", [0, 0.9, 6.8], [2.2, 1.8, 1.6], "#8A6A52", steps=6), P("cube", [1.2, 1.5, 7.0], [0.05, 1.0, 1.6], "#1A1A1A", mirror="x")])
    b["diner"] = dict(parts=[P("cube", [0, 1.9, 0], [14, 3.2, 6], "#D8DCE2", bevel=0.4, finish="chrome", pattern="stripes", pattern_color="#C8302A", pattern_scale=0.6),
                             P("cube", [0, 1.7, 3.02], [12, 1.4, 0.06], "#FFE8B0", emission=0.4, pattern="windows", pattern_scale=1.2),
                             P("cube", [0, 3.6, 0], [14.4, 0.3, 6.4], "#C8302A"), P("cube", [0, 0.15, 0], [14.4, 0.3, 6.4], "#303034"),
                             P("cube", [0, 4.6, 2.8], [5, 1.0, 0.2], "#FF4060", emission=2.2), P("cube", [-5.0, 1.2, 3.02], [1.2, 2.2, 0.06], "#4A3020")])
    b["glass_tower"] = dict(parts=[P("building", [0, 0, 0], [22, 62, 22], "#35556A", style="glass", roof="flat", floors=18), P("cube", [0, 63, 0], [6, 2.5, 6], "#404850")])
    b["art_deco_tower"] = dict(parts=[P("building", [0, 0, 0], [28, 46, 28], "#C8C0A8", style="stone", roof="flat", floors=13),
                                      P("building", [0, 46, 0], [20, 18, 20], "#C8C0A8", style="stone", roof="flat", floors=5),
                                      P("building", [0, 64, 0], [12, 10, 12], "#C8C0A8", style="stone", roof="stepped", floors=3),
                                      P("cone", [0, 78, 0], [2.5, 12, 2.5], "#D8DCE2", finish="chrome")])
    b["warehouse"] = dict(parts=[P("building", [0, 0, 0], [24, 8, 18], "#8A9096", style="metal", roof="shed", floors=2),
                                 P("cube", [-6, 2.2, 9.03], [4.5, 4.4, 0.08], "#5A6066", pattern="panels", pattern_scale=0.5), P("cube", [6, 2.2, 9.03], [4.5, 4.4, 0.08], "#5A6066", pattern="panels", pattern_scale=0.5),
                                 P("cube", [0, 0.6, 10.2], [22, 1.2, 2.4], "#A8A8A0")])
    b["factory"] = dict(parts=[P("building", [0, 0, 0], [26, 11, 18], "#9A4A34", style="brick", roof="shed", floors=3),
                               P("cylinder", [7, 14, -4], [1.8, 16, 1.8], "#8A3E2C", taper=0.8), P("cylinder", [10.5, 12, -4], [1.5, 12, 1.5], "#8A3E2C", taper=0.8),
                               P("cube", [-6, 12, 0], [8, 2.5, 10], "#7A7E84", pattern="panels", pattern_scale=1)])
    b["church"] = dict(parts=[P("building", [0, 0, -2], [10, 9, 16], "#B8B0A0", style="stone", roof="gable", floors=1),
                              P("cube", [0, 7, 6.5], [4.5, 14, 4.5], "#B8B0A0", finish="stone"), P("pyramid", [0, 17.5, 6.5], [4.6, 7, 4.6], "#5A6470"),
                              P("cube", [0, 11, 8.78], [1.2, 2.0, 0.06], "#202024"), P("cube", [0, 2.0, 8.78], [1.8, 3.6, 0.08], "#5A3A20"), P("cylinder", [0, 21.5, 6.5], [0.1, 1.6, 0.1], "#D8C060")])
    b["hangar"] = dict(parts=[P("cylinder", [0, 0, 0], [30, 26, 20], "#9AA0A8", rotation=[90, 0, 0], sides=20, finish="metal", pattern="panels", pattern_scale=1.5),
                              P("cube", [0, 4, 13.05], [18, 8, 0.1], "#2A2E34", pattern="panels", pattern_scale=2), P("cube", [0, 0.05, 0], [32, 0.1, 28], "#7A7E84")])
    b["gas_station"] = dict(parts=[P("cube", [0, 5.0, 4], [12, 0.6, 9], "#E8E8E8", bevel=0.2), P("cube", [0, 4.6, 4], [12.2, 0.3, 9.2], "#C8302A", emission=0.3),
                                   P("cylinder", [4.5, 2.4, 4], [0.4, 4.8, 0.4], "#D8DCE2", mirror="x"), P("cube", [1.6, 0.8, 4], [0.7, 1.6, 0.5], "#C8302A", mirror="x"),
                                   P("cube", [1.6, 1.4, 4.27], [0.4, 0.3, 0.02], "#40E0FF", emission=1.2, mirror="x"),
                                   P("building", [0, 0, -4], [8, 4, 5], "#E8E0D0", style="plaster", roof="flat", floors=1, shop=True)])
    stand = [P("stairs", [0, 2.5, 0], [24, 5, 7], "#A8ACB2", steps=8), P("cube", [0, 8.2, -1.0], [25, 0.3, 9], "#C8302A"), P("cylinder", [11.5, 4.2, -3.2], [0.3, 8.4, 0.3], "#606670", mirror="x")]
    for i, color in enumerate(("#E84030", "#3060D8", "#F0C020", "#30A050", "#E8E8E8")):
        stand.append(P("capsule", [-10 + i * 1.3, 1.4 + (i % 3) * 1.25, 2.8 - (i % 3) * 0.85], [0.4, 0.8, 0.35], color, repeat=12, step=[1.75, 0, 0]))
    b["grandstand"] = dict(parts=stand)
    b["control_tower"] = dict(parts=[P("cylinder", [0, 9, 0], [3, 18, 3], "#D8D8D8", taper=0.8, sides=12), P("cylinder", [0, 19.5, 0], [6.5, 3, 6.5], GLASS, sides=12, finish="glass"),
                                     P("cylinder", [0, 21.3, 0], [7.2, 0.6, 7.2], "#404448", sides=12), P("cylinder", [0, 23, 0], [0.2, 3, 0.2], "#C0C0C0"), P("sphere", [0, 24.6, 0], [0.4, 0.4, 0.4], "#FF2020", emission=2.5)])
    return b


# ---------------------------------------------------------------------------- props

def props():
    p = {}
    p["ring_gate"] = dict(parts=[P("torus", [0, 2.6, 0], [5, 5, 5], "#40E0FF", rotation=[90, 0, 0], radius=0.07, emission=2.0, sides=32), P("torus", [0, 2.6, 0], [5.3, 5.3, 5.3], "#FFFFFF", rotation=[90, 0, 0], radius=0.02, emission=1.2)])
    p["explosive_barrel"] = dict(flags=["solid"], mass=30, collider="capsule", parts=[P("cylinder", [0, 0.55, 0], [0.6, 1.1, 0.6], "#C82018", sides=14, finish="paint"), P("cylinder", [0, 0.55, 0], [0.62, 0.18, 0.62], "#F0C020", sides=14),
                                                                         P("torus", [0, 0.25, 0], [0.62, 0.62, 0.62], "#901810", radius=0.04), P("torus", [0, 0.85, 0], [0.62, 0.62, 0.62], "#901810", radius=0.04), P("cylinder", [0.15, 1.12, 0], [0.1, 0.05, 0.1], "#606060")])
    p["sandbags"] = dict(parts=[P("blob", [-1.2 + 0.6 * i, 0.15 + 0.3 * (i // 5), 0], [0.62, 0.3, 0.42], "#B8A070", rough=0.1, seed=i) for i in range(5)] +
                               [P("blob", [-0.9 + 0.6 * i, 0.45, 0], [0.62, 0.3, 0.42], "#A89060", rough=0.1, seed=10 + i) for i in range(4)] +
                               [P("blob", [-0.6 + 0.6 * i, 0.75, 0], [0.62, 0.3, 0.42], "#B8A070", rough=0.1, seed=20 + i) for i in range(3)])
    p["coin"] = dict(flags=["hold"], parts=[P("cylinder", [0, 0.15, 0], [0.3, 0.04, 0.3], "#F2C230", rotation=[90, 0, 0], sides=20, finish="metal", emission=0.2), P("cylinder", [0, 0.15, 0], [0.22, 0.05, 0.22], "#E0A820", rotation=[90, 0, 0], sides=20)])
    p["gem"] = dict(flags=["hold"], parts=[P("pyramid", [0, 0.15, 0], [0.2, 0.1, 0.2], "#40E080", emission=0.6, finish="glass"), P("pyramid", [0, 0.05, 0], [0.2, 0.1, 0.2], "#30C070", rotation=[180, 0, 0], emission=0.4, finish="glass")])
    p["lever"] = dict(parts=[P("cube", [0, 0.15, 0], [0.5, 0.3, 0.3], "#505458", finish="metal"), P("cylinder", [0, 0.55, 0.12], [0.06, 0.8, 0.06], "#808890", rotation=[25, 0, 0], finish="metal"), P("sphere", [0, 0.92, 0.3], [0.14, 0.14, 0.14], "#C82018")])
    p["push_button"] = dict(parts=[P("cylinder", [0, 0.45, 0], [0.3, 0.9, 0.3], "#404448", finish="metal"), P("cylinder", [0, 0.92, 0], [0.4, 0.06, 0.4], "#F0C020"), P("dome", [0, 0.95, 0], [0.26, 0.14, 0.26], "#E82018", emission=0.6)])
    p["barrier"] = dict(parts=[P("cube", [0, 0.55, 0], [2.4, 0.9, 0.5], "#E8E8E8", bevel=0.3, taper=[0.8, 0.6], pattern="stripes", pattern_color="#D82018", pattern_scale=0.5)])
    p["tire_stack"] = dict(flags=["solid"], mass=25, parts=[P("torus", [0, 0.12 + 0.24 * i, 0], [0.8, 0.8, 0.8], RUBBER, radius=0.16, finish="rubber") for i in range(4)])
    p["hockey_stick"] = dict(kind="tool", flags=["tool"], strike={"power": 1.6, "loft": 3}, parts=[P("cube", [0, 0, 0.55], [0.03, 0.025, 1.4], "#C8A060", finish="wood"), P("cube", [0.08, 0, 1.28], [0.2, 0.06, 0.02], "#202024", rotation=[0, 20, 0]),
                                                                                                   P("cube", [0, 0, -0.08], [0.035, 0.03, 0.16], "#202024")])
    p["hockey_puck"] = dict(flags=["hold", "solid"], mass=0.17, bounce=0.1, parts=[P("cylinder", [0, 0.0125, 0], [0.076, 0.025, 0.076], "#101012", sides=16)])
    p["pool_cue"] = dict(kind="tool", flags=["tool"], strike={"power": 1.2, "loft": 0}, parts=[P("cylinder", [0, 0, 0.55], [0.03, 1.45, 0.03], "#C8A060", rotation=[90, 0, 0], taper=0.45, finish="wood"),
                                                                                            P("cylinder", [0, 0, -0.05], [0.032, 0.35, 0.032], "#202024", rotation=[90, 0, 0]), P("cylinder", [0, 0, 1.27], [0.014, 0.02, 0.014], "#3070D0", rotation=[90, 0, 0])])
    p["billiard_ball"] = dict(flags=["ball"], mass=0.17, bounce=0.9, parts=[P("sphere", [0, 0.0285, 0], [0.057, 0.057, 0.057], "#F2F2EA", finish="gloss")])
    p["dartboard"] = dict(parts=[P("cylinder", [0, 0.85, 0], [0.08, 1.7, 0.08], "#5A3A20"), P("cylinder", [0, 1.73, 0.06], [0.46, 0.04, 0.46], "#202020", rotation=[90, 0, 0], sides=20),
                                 P("cylinder", [0, 1.73, 0.08], [0.36, 0.02, 0.36], "#E8D8A8", rotation=[90, 0, 0], sides=20, pattern="checker", pattern_color="#202020", pattern_scale=0.06),
                                 P("cylinder", [0, 1.73, 0.095], [0.06, 0.02, 0.06], "#D82018", rotation=[90, 0, 0])])
    p["dart"] = dict(kind="tool", flags=["tool"], parts=[P("cylinder", [0, 0, 0.04], [0.012, 0.12, 0.012], "#C0C4C8", rotation=[90, 0, 0], finish="metal"), P("cone", [0, 0, 0.12], [0.006, 0.04, 0.006], "#808080", rotation=[90, 0, 0]),
                                                      P("cube", [0, 0, -0.04], [0.05, 0.002, 0.05], "#D82018", mirror="x", rotation=[0, 0, 0]), P("cube", [0, 0, -0.04], [0.002, 0.05, 0.05], "#D82018")])
    p["punching_bag"] = dict(parts=[P("cylinder", [0, 1.1, 0], [0.4, 1.1, 0.4], "#8A1A1A", sides=14, finish="skin"), P("cylinder", [0, 1.95, 0], [0.02, 0.6, 0.02], "#808080"), P("torus", [0, 1.66, 0], [0.42, 0.42, 0.42], "#5A1010", radius=0.05)])
    p["phone_booth"] = dict(parts=[P("cube", [0, 1.2, 0], [1.0, 2.4, 1.0], "#C81E1E", bevel=0.15, finish="paint"), P("cube", [0, 1.2, 0.51], [0.7, 1.7, 0.02], GLASS, finish="glass", pattern="grid", pattern_color="#C81E1E", pattern_scale=0.25),
                                   P("dome", [0, 2.4, 0], [1.0, 0.18, 1.0], "#C81E1E"), P("cube", [0, 2.25, 0.51], [0.6, 0.12, 0.02], "#FFFFFF", emission=1.2)])
    p["food_cart"] = dict(parts=[P("cube", [0, 0.75, 0], [1.6, 0.8, 0.9], "#E8E0C8", bevel=0.2, pattern="stripes", pattern_color="#C82018", pattern_scale=0.3), P("cylinder", [0, 1.7, 0], [0.04, 1.8, 0.04], "#808080"),
                                 P("cone", [0, 2.5, 0], [2.2, 0.5, 2.2], "#F0C020", sides=10), P("cylinder", [0.7, 0.25, -0.2], [0.5, 0.06, 0.5], RUBBER, rotation=[0, 0, 90], mirror="x"), P("cube", [0, 1.2, 0], [1.3, 0.1, 0.6], "#C0C0C0", finish="metal")])
    p["dumpster"] = dict(parts=[P("cube", [0, 0.7, 0], [2.0, 1.2, 1.2], "#2E6A3A", bevel=0.1, taper=[1.0, 0.85], finish="metal"), P("cube", [0, 1.35, -0.1], [2.05, 0.08, 1.3], "#1E4A2A", rotation=[-8, 0, 0])])
    p["parking_meter"] = dict(parts=[P("cylinder", [0, 0.55, 0], [0.07, 1.1, 0.07], "#505458"), P("cube", [0, 1.2, 0], [0.18, 0.28, 0.14], "#808890", bevel=0.4, finish="metal"), P("cube", [0, 1.22, 0.075], [0.1, 0.06, 0.01], "#40E080", emission=0.6)])
    p["windsock"] = dict(parts=[P("cylinder", [0, 3, 0], [0.1, 6, 0.1], "#C0C0C0"), P("cone", [0, 5.8, 0.9], [0.5, 1.8, 0.5], "#FF7A20", rotation=[90, 0, 0], pattern="stripes", pattern_color="#FFFFFF", pattern_scale=0.36,
                                                                                                            anim={"swing": [0, 12, 0], "every": 3})])
    return p


def build() -> list[dict]:
    spec = json.loads((ROOT / "scripts" / "catalog_additions.json").read_text(encoding="utf-8"))
    made = {**vehicles(), **buildings(), **props()}
    out = []
    for entry in spec["assets"]:
        body = made.get(entry["id"])
        if body is None:
            raise SystemExit(f"no parts written for {entry['id']}")
        row = {"id": entry["id"], "category": entry["category"], "size": entry["size_m"], "desc": entry["desc"], "tags": entry["tags"], "aliases": entry["aliases"]}
        row.update({k: v for k, v in body.items()})
        row.setdefault("kind", "vehicle" if entry["category"] == "vehicles" else "thing")
        out.append(row)
    return out


def main(argv: list[str]) -> int:
    rows = build()
    OUT.write_text(json.dumps(rows, separators=(",", ":")) + "\n", encoding="utf-8")
    print(f"wrote {len(rows)} assets to {OUT.relative_to(ROOT)} ({OUT.stat().st_size // 1024} KB)")
    if "--preview" in argv:
        sys.path.insert(0, str(ROOT / "openrouter-api" / "scripts"))
        from preview import render  # noqa: E402
        folder = Path(argv[argv.index("--preview") + 1])
        folder.mkdir(parents=True, exist_ok=True)
        for row in rows:
            render({"name": row["id"], "parts": row["parts"]}, roles="--roles" in argv).save(folder / (row["id"] + ".png"))
        print("pictures in", folder)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
