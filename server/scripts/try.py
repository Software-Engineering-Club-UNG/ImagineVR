"""Send one request to the server and print the stream as it arrives.

    uv run scripts/try.py "I want to see a golf course"
    uv run scripts/try.py --wav clip.wav
    IMAGINE_URL=https://my-server uv run scripts/try.py "make it sunset"

The session id is fixed, so follow-ups modify the world from the previous call.
Use --new to start from the dark room.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time

import httpx


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("text", nargs="*", help="what to say")
    parser.add_argument("--wav", help="send this WAV file instead of text")
    parser.add_argument("--session", default="try-script", help="session id")
    parser.add_argument("--new", action="store_true", help="reset the session first")
    parser.add_argument("--raw", action="store_true", help="print raw lines only")
    args = parser.parse_args()

    base = os.environ.get("IMAGINE_URL", "http://127.0.0.1:8080").rstrip("/")
    headers = {
        "X-App-Token": os.environ.get("IMAGINE_APP_TOKEN", "dev-token"),
        "X-Session-Id": args.session,
    }
    with httpx.Client(timeout=60.0) as client:
        if args.new:
            client.post(f"{base}/v1/session/reset", headers=headers)
        if args.wav:
            with open(args.wav, "rb") as f:
                content, content_type = f.read(), "audio/wav"
        else:
            text = " ".join(args.text).strip()
            if not text:
                parser.error("say something, or pass --wav")
            content, content_type = json.dumps({"text": text}).encode(), "application/json"

        started = time.perf_counter()
        first_world: float | None = None
        with client.stream(
            "POST", f"{base}/v1/turn", headers={**headers, "Content-Type": content_type}, content=content
        ) as response:
            if response.status_code != 200:
                print(f"HTTP {response.status_code}: {response.read().decode('utf-8', 'replace')}")
                return 1
            for line in response.iter_lines():
                if not line.strip():
                    continue
                elapsed = time.perf_counter() - started
                if args.raw:
                    print(line)
                    continue
                try:
                    op = json.loads(line).get("op")
                except json.JSONDecodeError:
                    op = "?"
                if first_world is None and op not in ("transcript", "status", "note", "say", "done", "error"):
                    first_world = elapsed
                print(f"{elapsed:6.2f}s  {line}")
        if not args.raw:
            total = time.perf_counter() - started
            first = f"{first_world:.2f}s" if first_world is not None else "none"
            print(f"\nfirst world statement: {first}   total: {total:.2f}s")
    return 0


if __name__ == "__main__":
    sys.exit(main())
