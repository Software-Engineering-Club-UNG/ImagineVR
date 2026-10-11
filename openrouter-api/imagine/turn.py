"""One turn: speech or text in, a stream of ImagineScript statements out."""

from __future__ import annotations

import asyncio
import logging
import time
from typing import AsyncIterator

from . import config, llm, stt, tts
from .catalog import Catalog
from .language import Checker, parse_line
from .session import Session

log = logging.getLogger("imagine.turn")

NO_MODEL = "There's no AI model connected to this server, so I can't build anything yet."

MAKE_MISSING = """You used these assets, but they are not in the CATALOG and you have not made them, so nothing appeared:
{names}

Write one `make` line for each of them now, with the same name, and nothing else: no `think`, no `say`, no `spawn`.
Hold each to "Making things": real proportions, 12 to 30 parts, colour doing half the work."""


def heard_here(session: Session) -> str:
    """The names of what is in the user's world, as a sentence for the speech recogniser to lean on."""
    names: list[str] = []
    for stmt in list(session.world.things.values()) + [session.world.globals.get("ride", {})]:
        name = str(stmt.get("asset") or "").replace("_", " ")
        if name and name not in names:
            names.append(name)
    return "Here now: " + ", ".join(names[:24]) + "." if names else ""


def build_user_content(session: Session, text: str, frame: list | None, user_state: str) -> str:
    parts = ["CURRENT WORLD:", session.world.render(frame, user_state), ""]
    if session.history:
        parts.append("RECENT CONVERSATION:")
        for said, replied in session.history:
            parts.append(f"user: {said}")
            if replied:
                parts.append(f"you: {replied}")
        parts.append("")
    parts.append(f'USER SAYS: "{text}"')
    return "\n".join(parts)


async def _model_lines(system_prompt: str, content: str) -> AsyncIterator[dict]:
    """Statements from the model. Asked once more if it fails before writing anything."""
    for attempt in (1, 2):
        wrote = False
        try:
            async for line in llm.stream_lines(system_prompt, content):
                stmt = parse_line(line)
                if stmt is None:
                    log.info("model line dropped (not a statement): %.200s", line.strip())
                    continue
                wrote = True
                yield stmt
            return
        except llm.LLMError as exc:
            log.warning("model failed (attempt %d, %s): %s", attempt, "partway" if wrote else "before writing", exc)
            if wrote or attempt == 2:
                raise


async def _run_turn(session: Session, catalog: Catalog, system_prompt: str, *, text: str | None = None,
                    audio: bytes | None = None, base_url: str = "", frame: list | None = None,
                    user_state: str = "", capabilities: str = "") -> AsyncIterator[dict]:
    started = time.perf_counter()

    if audio is not None:
        try:
            async with _stt_slots:
                text = await stt.transcribe(audio, heard_here(session))
        except Exception:  # noqa: BLE001
            log.exception("transcription failed")
            yield {"op": "transcript", "text": ""}
            yield {"op": "error", "message": "I couldn't process the audio. Try again.", "retryable": True}
            yield {"op": "done", "outcome": "failed"}
            return
        yield {"op": "transcript", "text": text}
        if not text:
            yield {"op": "say", "text": "I didn't catch that. Hold the button and say it again."}
            yield {"op": "done", "outcome": "not_understood"}
            return

    text = (text or "").strip()[:600]
    log.info("turn %.8s heard %r, user at %s", session.id, text, frame or "the middle")
    yield {"op": "status", "state": "thinking"}

    if config.OFFLINE:
        yield {"op": "say", "text": NO_MODEL}
        yield {"op": "done", "outcome": "failed"}
        return

    from .structures import CAPABILITY, PROMPT, PROMPT_V1, WIDE
    offered = [c.strip() for c in capabilities.split(",")]
    wide = WIDE in offered
    structures = wide or CAPABILITY in offered
    system_prompt += PROMPT if wide else PROMPT_V1 if structures else "\nThis client cannot build explorable interiors or interactive doors. Do not emit structure/door or room-local placement. Do not claim a decorative building is an explorable interior."
    checker = Checker(catalog, session.world, frame, structures=structures, wide=wide)
    content = build_user_content(session, text, frame, user_state)
    held: list[dict] = []        # statements waiting on an asset that has to be made first
    said: list[str] = []
    changed = False
    building = False
    ignored = False

    def play(raw: dict, strict: bool) -> list[dict]:
        nonlocal changed, building
        before = len(checker.missing)
        stmt = checker.check(raw, strict)
        if stmt is None:
            if checker.problem:
                problem, checker.problem = checker.problem, None
                return [{"op": "note", "text": problem}]
            if len(checker.missing) > before or (strict and str(raw.get("asset", "")) and not checker.knows(str(raw.get("asset")))):
                held.append(raw)
            else:
                log.info("statement dropped: %.200s", raw)
            return []
        if stmt["op"] == "say":
            if held and strict:
                held.append(raw)     # it talks about things that are not there yet
                return []
            said.append(stmt["text"])
            if tts.enabled():
                stmt["audio"] = f"{base_url}/v1/audio/{tts.start(stmt['text'])}"
            return [stmt]
        if stmt["op"] == "think":
            return [stmt]
        out = []
        if not building:
            building = True
            out.append({"op": "status", "state": "building"})
        changed = True
        return out + [stmt]

    try:
        async for raw in _model_lines(system_prompt, content):
            if str(raw.get("op", "")).lower() == "ignore":
                ignored = True     # not addressed to us: silence is the right answer
                continue
            for stmt in play(raw, strict=True):
                yield stmt

        if checker.missing:
            # The model named things nobody has. It is asked to make them; whatever it still does
            # not make is played anyway, and the headset stands a labelled block in for it.
            names = list(checker.missing)
            log.info("turn %.8s not in the library: %s", session.id, ", ".join(names))
            yield {"op": "note", "text": "no " + ", ".join(n.replace("_", " ") for n in names) + " in the library, making it"}
            try:
                async for raw in _model_lines(system_prompt, content + "\n\n" + MAKE_MISSING.format(names="\n".join("- " + n for n in names))):
                    if str(raw.get("op", "")).lower() in ("make", "blueprint"):
                        for stmt in play(raw, strict=False):
                            yield stmt
            except llm.LLMError as exc:
                log.warning("making the missing assets failed: %s", exc)
        waiting, held = held, []
        for raw in waiting:
            for stmt in play(raw, strict=False):
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

    if ignored and not changed and not said:
        yield {"op": "done", "outcome": "ignored"}
        return
    if not changed and not said:
        yield {"op": "say", "text": "I couldn't work out how to build that. Try saying it another way."}
        said.append("")
    session.history.append((text, " ".join(said)[:300]))
    session.touch()
    log.info("turn done: changed=%s, %.2fs", changed, time.perf_counter() - started)
    yield {"op": "done", "outcome": "world_changed" if changed else "answered"}


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
