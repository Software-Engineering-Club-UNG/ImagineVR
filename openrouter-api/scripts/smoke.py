"""Check a running server understands the things people actually say.

    uv run scripts/smoke.py                              # the server on this machine
    IMAGINE_URL=http://64.177.52.162 uv run scripts/smoke.py

Run it after setting OPENROUTER_API_KEY, after changing prompt.md, and after every deploy.
Each line is a fresh session; " >> " separates turns of one conversation. A check looks only
at the last turn. Exit code 0 means every check passed.
"""

from __future__ import annotations

import json
import os
import sys
import time
import uuid

import httpx

BUILDS = {"sky", "terrain", "make", "spawn", "scatter", "give", "ride", "weather", "clear", "when"}


def ops(stmts):
    return [s["op"] for s in stmts]


def nothing_built(stmts):
    return not BUILDS & set(ops(stmts))


def has(op, **fields):
    def check(stmts):
        return any(s["op"] == op and all(s.get(k) == v for k, v in fields.items()) for s in stmts)
    return check


def alive(stmts):
    """Something the user can fight or chase: living creatures."""
    return any(s.get("behavior") in ("chase", "wander", "flee") for s in stmts)


def count(op, at_least):
    return lambda stmts: sum(s["op"] == op for s in stmts) >= at_least


def made_and_used(op):
    """A thing the catalog lacks was made from parts, with some care, and then put to use."""
    def check(stmts):
        made = {s["name"]: len(s["parts"]) for s in stmts if s["op"] == "make"}
        return any(s["op"] == op and s.get("asset") in made and made[s["asset"]] >= 8 for s in stmts)
    return check


def every(*checks):
    return lambda stmts: all(c(stmts) for c in checks)


def deep(stmts):
    """A first build has a sky, ground with character, several kinds of scenery, life, and something to do."""
    return (has("sky")(stmts) and has("terrain")(stmts) and count("scatter", 4)(stmts) and alive(stmts)
            and any(s.get("physics") or s["op"] in ("give", "ride", "when") for s in stmts))


CASES = [
    ("Hello man hows it going", "small talk builds nothing", nothing_built),
    ("whoa this is so cool", "a reaction builds nothing", nothing_built),
    ("what can you do", "a question gets an answer and builds nothing", every(nothing_built, has("say"))),
    ("put me in a misty pine forest by a lake", "a place has depth", deep),
    ("take me to a street in tokyo at night and let me drive", "a city to drive through", every(has("ride"), count("scatter", 3), lambda s: any(x.get("solid") for x in s))),
    ("give me an AK-47", "a gun the catalog lacks is made, in detail, and fires",
     every(made_and_used("give"), lambda s: any(x["op"] == "make" and "fires" in x for x in s))),
    ("let me ride a giant rubber duck with wheels", "a ride nobody has seen before", made_and_used("ride")),
    ("a field with sheep >> when I shoot a sheep make it explode into confetti", "the world reacts",
     every(has("when", event="hit"), lambda s: "clear" not in ops(s))),
    ("set up ten pin bowling", "an activity from loose physics, with no special mode",
     lambda s: any(x.get("physics") for x in s) and any("pin" in str(x.get("asset")) for x in s)),
    ("a farm >> put me in a car >> ok let me out", "getting out", has("ride", asset="none")),
    ("a beach >> make it sunset", "a small change does not rebuild", every(has("sky"), lambda s: "clear" not in ops(s) and "terrain" not in ops(s))),
    ("a beach >> it's kind of empty and boring", "unhappy means more depth, not a question", every(count("scatter", 2), lambda s: "clear" not in ops(s))),
    ("give me a sword", "something to hold", has("give", asset="sword")),
]


def turn(client, base, headers, text):
    stmts, first = [], None
    started = time.perf_counter()
    with client.stream("POST", f"{base}/v1/turn", headers=headers, json={"text": text}) as response:
        if response.status_code != 200:
            raise RuntimeError(f"HTTP {response.status_code}: {response.read().decode('utf-8', 'replace')[:200]}")
        for line in response.iter_lines():
            if line.strip():
                stmt = json.loads(line)
                if first is None and stmt["op"] not in ("status", "transcript"):
                    first = time.perf_counter() - started
                stmts.append(stmt)
    return stmts, first or 0.0, time.perf_counter() - started


def main() -> int:
    base = os.environ.get("IMAGINE_URL", "http://127.0.0.1:8080").rstrip("/")
    token = os.environ.get("IMAGINE_APP_TOKEN", "dev-token")
    failed = 0
    with httpx.Client(timeout=90.0) as client:
        health = client.get(f"{base}/v1/health").json()
        print(f"{base}  model: {health.get('model')}  catalog: {health.get('catalog_version')}  stt: {health.get('stt')}")
        if health.get("model") == "offline":
            print("The server has no OPENROUTER_API_KEY: there is no model to check.")
            return 1
        for conversation, what, check in CASES:
            headers = {"X-App-Token": token, "X-Session-Id": "smoke-" + uuid.uuid4().hex[:8]}
            try:
                for text in conversation.split(" >> "):
                    stmts, first, total = turn(client, base, headers, text)
                ok = check(stmts) and not any(s["op"] == "error" for s in stmts)
                ok = ok and sum(s['op'] == 'done' for s in stmts) == 1 and stmts[-1]['op'] == 'done'
            except Exception as exc:  # noqa: BLE001
                stmts, first, total, ok = [{"op": "error", "message": str(exc)}], 0.0, 0.0, False
            failed += not ok
            print(f"{'ok  ' if ok else 'FAIL'} {what}  [{conversation}]  first {first:.1f}s, total {total:.1f}s")
            if not ok:
                for s in stmts:
                    if s["op"] not in ("status",):
                        print("       ", json.dumps(s)[:160])
    print(f"{len(CASES) - failed} of {len(CASES)} passed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
