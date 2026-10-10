#!/usr/bin/env python3
"""Reads the session logs the app keeps on the headset.

    python3 scripts/headset_log.py pull            # copy every log off the plugged-in headset, show the newest
    python3 scripts/headset_log.py show [file]     # print one log as a timeline (newest pulled one by default)
    python3 scripts/headset_log.py show --all      # ...including the two-second position samples

Needs `adb` for `pull`. Logs land in headset-logs/, which Git ignores: they contain what people said.
"""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

DEVICE = "/sdcard/Android/data/com.hacklanta.imaginevr/files/logs"
LOCAL = Path(__file__).resolve().parent.parent / "headset-logs"


def pull() -> None:
    LOCAL.mkdir(exist_ok=True)
    done = subprocess.run(["adb", "pull", DEVICE + "/.", str(LOCAL)], text=True, capture_output=True)
    print((done.stdout or done.stderr).strip())
    if done.returncode != 0:
        sys.exit("adb could not copy the logs: is the headset plugged in, awake, and allowed to debug?")


def newest() -> Path:
    files = sorted(LOCAL.glob("session-*.ndjson"))
    if not files:
        sys.exit(f"no logs in {LOCAL}; run `pull` first")
    return files[-1]


def one_line(e: dict) -> str | None:
    """What one log entry means, in a few words. None for entries that are only noise."""
    k = e["kind"]
    if k == "start":
        return f"APP STARTED  {e.get('device')}, server {e.get('server')} ({e.get('settings_from')})"
    if k == "server":
        return f"server {'ONLINE' if e.get('online') else 'OFFLINE'}  {e.get('health') or ''}"
    if k == "press":
        return f"pressed {e.get('button')}" + (f" (holding {e['holding']})" if e.get("holding") else "")
    if k == "voice":
        extra = ", ".join(f"{n} {e[n]}" for n in ("seconds", "why", "loudest") if e.get(n) is not None)
        return f"voice: {e.get('what')}" + (f" ({extra})" if extra else "")
    if k == "send":
        return f"-> turn {e.get('turn')} sent, user at {e.get('pose')}  {e.get('text') or '(audio, %s bytes)' % e.get('bytes')}"
    if k == "recv":
        return f"<- {e.get('line')}" + ("" if e.get("understood") else "   [NOT UNDERSTOOD]")
    if k == "reply":
        return (f"turn {e.get('turn')} over: http {e.get('status')}, {e.get('lines')} lines in {e.get('seconds')}s"
                + ("" if e.get("finished") else f"   [NO `done`: {e.get('result')} {e.get('error') or ''}]"))
    if k == "play":
        return None  # the same statements as `recv`, at the moment they were applied
    if k == "placed":
        where = f"{e.get('metres_from_user')} m away, {e.get('degrees_from_gaze')} deg from where they looked"
        flags = "".join([
            "   [STAND-IN BOX: not in the library]" if e.get("stand_in") else "",
            "   [NOT IN VIEW]" if not e.get("in_view") and not e.get("problem") else "",
            f"   [{e['problem']}]" if e.get("problem") else "",
        ])
        swap = "" if e.get("asked") == e.get("used") or not e.get("used") else f" (asked for {e.get('asked')})"
        return f"   {e.get('op')} {e.get('used') or e.get('asked')}{swap}: {where}{flags}"
    if k == "grab":
        return f"{e.get('hand')} hand: {e.get('result')} {e.get('asset') or ''} {('by ' + e['how']) if e.get('how') else ''}".rstrip()
    if k == "release":
        return f"{e.get('hand')} hand let go of {e.get('asset')}" + (" (thrown)" if e.get("thrown") else "")
    if k == "recover":
        return f"returned {e.get('asset')} to reach: {e.get('why')} ({e.get('from')} -> {e.get('to')})"
    if k == "terminal":
        return "terminal tucked away" if e.get("collapsed") else "terminal opened"
    if k == "reset":
        return f"RESET {e.get('what')}" + (f" ({e['why']})" if e.get("why") else "")
    if k == "home":
        return f"user brought to the middle (moved {e.get('moved')} m, turned {e.get('turned')} deg)"
    if k == "hint":
        return f"hint shown: {e.get('what')}" + (f" ({e['asset']})" if e.get("asset") else "")
    if k == "unity":
        text = f"UNITY {e.get('level')}: {e.get('text')}"
        return text + ("\n" + str(e["stack"]).rstrip() if e.get("stack") else "") if e.get("level") != "info" else text
    if k == "state":
        return (f"at {e.get('head')} facing {e.get('yaw')}, {e.get('fps')} fps, {'dark room' if e.get('dark') else 'in a world'}"
                f"{', passthrough' if e.get('passthrough') else ''}, terminal {e.get('terminal')}"
                f"{' (tucked away)' if e.get('collapsed') else ''}, holding {e.get('left')}/{e.get('right')}")
    rest = {n: v for n, v in e.items() if n not in ("t", "clock", "kind")}
    return f"{k}: {json.dumps(rest, ensure_ascii=False)}"


def show(path: Path, everything: bool) -> None:
    print(f"# {path}")
    for raw in path.read_text(encoding="utf-8").splitlines():
        if not raw.strip():
            continue
        try:
            e = json.loads(raw)
        except json.JSONDecodeError:
            print(f"?? {raw}")
            continue
        if e.get("kind") == "state" and not everything:
            continue
        text = one_line(e)
        if text is not None:
            print(f"{e.get('clock', ''):12} {e.get('t', 0):8.2f}  {text}")


def main() -> None:
    args = sys.argv[1:]
    everything = "--all" in args
    args = [a for a in args if a != "--all"]
    command = args[0] if args else "show"
    if command == "pull":
        pull()
        show(newest(), everything)
    elif command == "show":
        show(Path(args[1]) if len(args) > 1 else newest(), everything)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
