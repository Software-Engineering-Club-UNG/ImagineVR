"""One turn: speech or text in, a stream of ImagineScript statements out."""

from __future__ import annotations

import asyncio
import logging
import time
from typing import AsyncIterator

from . import config, fallback, llm, stt, tts
from .catalog import Catalog
from .script import Validator, parse_line
from .session import Session

log = logging.getLogger("imagine.turn")

OUTCOMES = ("world_changed", "object_given", "answered", "ignored", "not_understood", "declined", "failed")


def build_user_content(session: Session, text: str) -> str:
    parts = ["CURRENT WORLD (ImagineScript):", session.world.render(), ""]
    if session.history:
        parts.append("RECENT CONVERSATION:")
        for said, replied in session.history:
            parts.append(f"user: {said}")
            if replied:
                parts.append(f"you: {replied}")
        parts.append("")
    parts.append(f'USER SAYS: "{text}"')
    return "\n".join(parts)


async def _raw_statements(session: Session, catalog: Catalog, system_prompt: str, text: str) -> AsyncIterator[dict]:
    """Raw statements from the model, or from the offline director when the model is unavailable."""
    if config.OFFLINE:
        for stmt in fallback.compose(text, catalog, session.world):
            yield stmt
        return
    wrote_anything = False
    try:
        async for line in llm.stream_lines(system_prompt, build_user_content(session, text)):
            stmt = parse_line(line)
            if stmt is not None:
                wrote_anything = True
                yield stmt
    except llm.LLMError as exc:
        log.warning("model failed: %s", exc)
        if wrote_anything:
            raise
        yield {"op": "note", "text": "the AI model is unreachable, using the built-in director"}
        for stmt in fallback.compose(text, catalog, session.world):
            yield stmt


async def _run_turn(
    session: Session,
    catalog: Catalog,
    system_prompt: str,
    *,
    text: str | None = None,
    audio: bytes | None = None,
    base_url: str = "",
) -> AsyncIterator[dict]:
    started = time.perf_counter()

    if audio is not None:
        try:
            async with _stt_slots:
                text = await stt.transcribe(audio)
        except Exception:  # noqa: BLE001
            log.exception("transcription failed")
            yield {"op": "transcript", "text": ""}
            yield {"op": "error", "message": "I couldn't process the audio. Try again.", "retryable": True}
            yield {"op": "done", "outcome": "failed"}
            return
        yield {"op": "transcript", "text": text}
        if not text:
            yield {"op": "status", "state": "thinking"}
            yield {"op": "say", "text": "I didn't catch that. Hold the button and say it again."}
            yield {"op": "done", "outcome": "not_understood"}
            return

    text = (text or "").strip()[:600]
    yield {"op": "status", "state": "thinking"}

    validator = Validator(catalog)
    building = False
    changed = False
    gave = False
    hinted: str | None = None
    said: list[str] = []
    emitted = 0
    first_at: float | None = None
    try:
        async for raw in _raw_statements(session, catalog, system_prompt, text):
            op = str(raw.get("op", "")).lower()
            if op == "outcome":
                value = str(raw.get("value", "")).lower()
                hinted = value if value in OUTCOMES else hinted
                continue
            if op == "note" and isinstance(raw.get("text"), str):
                yield {"op": "note", "text": raw["text"][:200]}
                continue
            for stmt in validator.check(raw, session.world):
                kind = stmt["op"]
                if kind == "say":
                    said.append(stmt["text"])
                    if tts.enabled():
                        stmt["audio"] = f"{base_url}/v1/audio/{tts.start(stmt['text'])}"
                elif kind not in ("note", "think"):
                    if not building:
                        building = True
                        first_at = time.perf_counter() - started
                        yield {"op": "status", "state": "building"}
                    if kind == "give":
                        gave = True
                    else:
                        changed = True
                emitted += 1
                yield stmt
    except llm.LLMError:
        yield {"op": "error", "message": "I lost my connection to the AI partway through. What's built so far stays; try again.", "retryable": True}
        yield {"op": "done", "outcome": "failed"}
        return
    except Exception:  # noqa: BLE001 - never leave the stream without `done`
        log.exception("turn failed")
        yield {"op": "error", "message": "Something went wrong on my side. Try again.", "retryable": True}
        yield {"op": "done", "outcome": "failed"}
        return

    if emitted == 0 and hinted != "ignored":
        yield {"op": "say", "text": "I couldn't work out how to build that. Try saying it another way."}
        hinted = hinted or "not_understood"

    outcome = hinted or ("world_changed" if changed else "object_given" if gave else "answered")
    if outcome != "ignored":
        session.history.append((text, " ".join(said)[:300]))
    session.touch()

    log.info(
        "turn -> %s, %d statements, first change %s, total %.2fs",
        outcome, emitted,
        f"{first_at:.2f}s" if first_at is not None else "n/a",
        time.perf_counter() - started,
    )
    yield {"op": "done", "outcome": outcome}

_stt_slots = asyncio.Semaphore(1)


def reset_stt_slots():
    """Create the limiter on application startup for this event-loop lifetime."""
    global _stt_slots
    _stt_slots = asyncio.Semaphore(1)


async def run_turn(session, catalog, system_prompt, **kwargs):
    async with session.lock:
        session.touch()
        async for stmt in _run_turn(session, catalog, system_prompt, **kwargs):
            yield stmt

