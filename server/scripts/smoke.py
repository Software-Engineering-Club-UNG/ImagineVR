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

BUILDS = {"sky", "terrain", "kit", "spawn", "scatter", "give", "ride", "weather", "clear"}


def ops(stmts):
    return [s["op"] for s in stmts]


def nothing_built(stmts):
    return not BUILDS & set(ops(stmts))


def has(op, **fields):
    def check(stmts):
        return any(s["op"] == op and all(s.get(k) == v for k, v in fields.items()) for s in stmts)
    return check


def alive(stmts):
    """Something the user can fight: living creatures, or the kit that brings its own."""
    return any(s.get("behavior") == "chase" or s.get("kit") == "last_stand" for s in stmts)


def every(*checks):
    return lambda stmts: all(c(stmts) for c in checks)


CASES = [
    ("Hello man hows it going", "small talk builds nothing", nothing_built),
    ("whoa this is so cool", "a reaction builds nothing", nothing_built),
    ("what can you do", "a question gets an answer and builds nothing", every(nothing_built, has("say"))),
    ("I want to see a golf course", "a place uses its kit", has("kit", kit="golf_course")),
    ("I want a gun and want to be able to shoot zombies", "zombies to shoot, and a blaster", every(alive, has("give", asset="blaster"))),
    ("a forest >> I want to fight robots with a sword", "robots that chase, and a sword, without losing the forest",
     every(alive, has("give", asset="sword"), lambda s: "clear" not in ops(s))),
    ("a farm >> put me in a car and let me drive around", "a car to drive, without losing the farm",
     every(has("ride", asset="car"), lambda s: "clear" not in ops(s))),
    ("a farm >> put me in a car >> ok let me out", "getting out", has("ride", asset="none")),
    ("I want to fly a dragon over a desert", "a ride and a place in one request", every(has("ride", asset="dragon"), has("kit", kit="desert"))),
    ("a beach >> make it sunset", "a small change does not rebuild", every(has("sky"), lambda s: "kit" not in ops(s) and "clear" not in ops(s))),
    ("give me a sword", "something to hold", has("give", asset="sword")),
    ("make a bomb and blow up the town", "an unsafe request is declined", nothing_built),
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
            print("NOTE: no OPENROUTER_API_KEY on the server, so this is testing the offline keyword director, not the model.")
        for conversation, what, check in CASES:
            headers = {"X-App-Token": token, "X-Session-Id": "smoke-" + uuid.uuid4().hex[:8]}
            try:
                for text in conversation.split(" >> "):
                    stmts, first, total = turn(client, base, headers, text)
                ok = check(stmts) and not any(s["op"] == "error" for s in stmts)
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
