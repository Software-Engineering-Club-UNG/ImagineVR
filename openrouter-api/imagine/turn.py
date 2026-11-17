"""One turn: speech or text in, a stream of ImagineScript statements out.

For a headset that plays the third version of the language, a request to build a place is
answered by several model calls at once, and their lines reach the headset in the order they are
written:

    the director      answers the user: the sky, the ground, where things stand, rough models
    crafts            one per rough model that asked to be `refine`d: the same thing in detail,
                      which replaces the rough one where it stands
    the detail pass   dresses the place once the director has laid it out
    the play pass     makes it something to do, and says what that is

A follow-up ("make it night") is still one call. So is every turn of an older headset.
"""

from __future__ import annotations

import asyncio
import json
import logging
import time
from typing import AsyncIterator

from . import config, gen, llm, stt, tts, v3
from .catalog import Catalog
from .language import Checker, parse_line
from .playbook import Playbook
from .prompts import Prompts
from .router import modules_for
from .session import Session

log = logging.getLogger("imagine.turn")

NO_MODEL = "There's no AI model connected to this server, so I can't build anything yet."

MAKE_MISSING = """You used these assets, but they are not in the CATALOG and you have not made them, so nothing appeared:
{names}

Write one `make` line for each of them now, with the same name, and nothing else: no `think`, no `say`, no `spawn`.
Hold each to "Making things": real proportions, 12 to 30 parts, colour doing half the work."""

CRAFT = """MODEL THIS: "{name}" (kind: {kind})
WHAT IT IS: {brief}

THE ROUGH VERSION IT REPLACES. Keep its overall size, where its feet or wheels stand, and which way it faces:
{blockout}

Write one `make` line named "{name}" and nothing else."""

PASS = {
    "detail": "The place above has just been laid out for this request. Dress it: follow THE DETAIL PASS. Write only your additions.",
    "play": "The place above has just been laid out for this request. Give them something to do in it: follow THE PLAY PASS. Write only your additions, and end with the `say`.",
}
# what a second call may not do to a place the director has just built
_NOT_IN_A_PASS = frozenset(("clear", "sky", "terrain", "structure", "remove", "think", "weather", "ambience", "physics", "door"))
# what a character may do when spoken to
TALK_OPS = ("speak", "say", "give", "door", "modify", "hud", "stage", "var", "spawn", "remove", "narrate", "music", "when")
# statements that lay out a new place: seeing one of these is what makes a turn worth the extra calls
_LAYS_OUT = frozenset(("terrain", "structure", "road", "clear"))


class Caps:
    """What the headset said it can play."""

    def __init__(self, header: str = ""):
        from .structures import CAPABILITY, WIDE
        offered = {c.strip() for c in (header or "").split(",")}
        self.wide = WIDE in offered
        self.structures = self.wide or CAPABILITY in offered
        self.play = v3.CAPABILITY in offered
        self.gen = self.play and v3.GENERATED in offered

    def interiors(self) -> str:
        from .structures import PROMPT, PROMPT_V1
        if self.wide:
            return PROMPT
        if self.structures:
            return PROMPT_V1
        return ("\nThis client cannot build explorable interiors or interactive doors. Do not emit structure/door or room-local "
                "placement. Do not claim a decorative building is an explorable interior.")


def heard_here(session: Session) -> str:
    """The names of what is in the user's world, as a sentence for the speech recogniser to lean on."""
    names: list[str] = []
    for stmt in list(session.world.things.values()) + [session.world.globals.get("ride", {})]:
        for name in (str(stmt.get("asset") or "").replace("_", " "), str(stmt.get("name") or "")):
            if name and name not in names:
                names.append(name)
    return "Here now: " + ", ".join(names[:24]) + "." if names else ""


def build_user_content(session: Session, text: str, frame: list | None, user_state: str, play_state: str = "",
                       examples: str = "", heading: str = "USER SAYS") -> str:
    parts = ["CURRENT WORLD:", session.world.render(frame, user_state, play_state), ""]
    if session.history:
        parts.append("RECENT CONVERSATION:")
        for said, replied in session.history:
            parts.append(f"user: {said}")
            if replied:
                parts.append(f"you: {replied}")
        parts.append("")
    if examples:
        parts += [examples, ""]
    parts.append(f'{heading}: "{text}"')
    return "\n".join(parts)


def build_talk_content(session: Session, who: str, text: str, frame: list | None, user_state: str, play_state: str) -> str:
    thing = session.world.things.get(who, {})
    name = thing.get("name") or str(thing.get("asset", who)).replace("_", " ")
    parts = [f'YOU ARE: {name} (id "{who}", a {str(thing.get("asset", "person")).replace("_", " ")})',
             "WHO YOU ARE: " + (thing.get("persona") or "Someone who lives here. Friendly, brief, and knows this place."),
             "", "THE WORLD AROUND YOU:", session.world.render(frame, user_state, play_state), ""]
    spoken = session.spoken_with(who)
    if spoken:
        parts.append("WHAT HAS BEEN SAID BETWEEN YOU:")
        for said, replied in spoken:
            parts += [f"them: {said}", f"you: {replied}"]
        parts.append("")
    parts.append(f'THEY SAY TO YOU: "{text}"')
    return "\n".join(parts)


async def _model_lines(system_prompt: str, content: str, model: str | None = None, attempts: int = 2) -> AsyncIterator[dict]:
    """Statements from the model. Asked once more if it fails before writing anything."""
    for attempt in range(1, attempts + 1):
        wrote = False
        try:
            async for line in llm.stream_lines(system_prompt, content) if model is None else llm.stream_lines(system_prompt, content, model):
                stmt = parse_line(line)
                if stmt is None:
                    log.info("model line dropped (not a statement): %.200s", line.strip())
                    continue
                wrote = True
                yield stmt
            return
        except llm.LLMError as exc:
            log.warning("model failed (attempt %d, %s): %s", attempt, "partway" if wrote else "before writing", exc)
            if wrote or attempt == attempts:
                raise


class _Lines:
    """Lines from several model calls, handed over in the order they arrive."""

    def __init__(self):
        self.queue: asyncio.Queue = asyncio.Queue()
        self.tasks: list[asyncio.Task] = []
        self.open = 0

    def start(self, source: tuple, lines: AsyncIterator[dict], options: dict | None = None) -> None:
        self.open += 1
        self.tasks.append(asyncio.create_task(self._pump(source, lines, options)))

    async def _pump(self, source: tuple, lines: AsyncIterator[dict], options: dict | None) -> None:
        if options:
            llm.options.set(options)     # this task's own: calls running beside it keep theirs
        failure = None
        try:
            async for raw in lines:
                await self.queue.put((source, raw, None))
        except asyncio.CancelledError:
            raise
        except Exception as exc:  # noqa: BLE001 - handed to whoever is reading, who knows which calls matter
            failure = exc
        await self.queue.put((source, None, failure))

    async def stop(self) -> None:
        for task in self.tasks:
            if not task.done():
                task.cancel()
        await asyncio.gather(*self.tasks, return_exceptions=True)


async def _craft_lines(prompts: Prompts, name: str, kind: str, brief: str, blockout: list) -> AsyncIterator[dict]:
    """One asset modelled in detail. With pictures on and depth 3, it is drawn first and modelled from the drawing."""
    if config.DEPTH >= 3 and gen.enabled():
        sketch = await gen.concept(brief)
        if sketch:
            llm.options.set({**(llm.options.get() or {}), "images": [sketch]})
    content = CRAFT.format(name=name, kind=kind, brief=brief, blockout=json.dumps(blockout, separators=(",", ":")))
    async for raw in _model_lines(prompts.craft(), content, config.CRAFT_MODEL, attempts=1):
        yield raw


async def _run_turn(session: Session, catalog: Catalog, system_prompt, *, text: str | None = None,
                    audio: bytes | None = None, base_url: str = "", frame: list | None = None,
                    user_state: str = "", capabilities: str = "", play_state: str = "", talk_to: str = "",
                    event: str = "", playbook: Playbook | None = None) -> AsyncIterator[dict]:
    started = time.perf_counter()
    deadline = time.monotonic() + config.TURN_SECONDS

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

    text = (event or text or "").strip()[:600]
    log.info("turn %.8s heard %r, user at %s", session.id, text, frame or "the middle")
    yield {"op": "status", "state": "thinking"}

    if config.OFFLINE:
        yield {"op": "say", "text": NO_MODEL}
        yield {"op": "done", "outcome": "failed"}
        return

    caps = Caps(capabilities)
    if not caps.play:
        catalog = catalog.before(3)     # what an older headset has recipes for, and nothing it has not
    prompts = system_prompt if isinstance(system_prompt, Prompts) else Prompts.plain(system_prompt)
    kind = "user"
    if caps.play and event:
        kind = "event"
    elif caps.play and talk_to and talk_to in session.world.things:
        kind = "talk"
    interiors = caps.interiors()
    checker = Checker(catalog, session.world, frame, structures=caps.structures, wide=caps.wide, play=caps.play, gen=caps.gen,
                      only=TALK_OPS if kind == "talk" else None)

    director_options = None
    modules: list[str] = []
    examples = ""
    if kind == "talk":
        system = prompts.npc()
        content = build_talk_content(session, talk_to, text, frame, user_state, play_state)
        director_model, director_options = config.NPC_MODEL, {"max_tokens": 700, "temperature": 0.9}
    elif kind == "event":
        system = prompts.event(interiors)
        content = build_user_content(session, text, frame, user_state, play_state, heading="THIS JUST HAPPENED")
        director_model = None
    else:
        modules = modules_for(text, session.world) if caps.play else []
        if caps.play and config.DEPTH == 0:
            modules = modules + ["make"]     # nobody else will model what it names, so it is told how
        system = prompts.director(modules, interiors, play=caps.play, helped=caps.play and config.DEPTH >= 2)
        examples = playbook.render(text, config.EXAMPLES) if caps.play and playbook is not None else ""
        content = build_user_content(session, text, frame, user_state, play_state if caps.play else "", examples)
        director_model = None

    held: list[tuple[tuple, dict]] = []     # statements waiting on an asset that has to be made first
    said: list[str] = []
    changed = False
    building = False
    ignored = False
    laid_out = False                        # the director is building a new place: worth the extra calls
    placed = 0
    crafts: dict[str, dict] = {}            # name -> the rough version, for each asset being modelled in detail
    owned: dict[str, tuple] = {}            # id -> the call that put it there, this turn
    last_say: dict | None = None            # what is said when everything is built
    fallback_say: dict | None = None
    lines = _Lines()

    def enriching() -> bool:
        return caps.play and kind == "user" and config.DEPTH >= 2 and laid_out and prompts.modular

    def speak(stmt: dict) -> dict:
        """A statement someone hears: given the address its voice will be at."""
        if tts.enabled() and (stmt["op"] == "say" or caps.play):
            voice = stmt.get("voice") or (session.world.things.get(stmt.get("id") or "", {}).get("voice") if stmt["op"] == "speak" else None)
            stmt["audio"] = f"{base_url}/v1/audio/{tts.start(stmt['text'], voice or ('narrator' if stmt['op'] != 'speak' else None))}"
        return stmt

    def play(raw: dict, strict: bool, source: tuple = ("director",)) -> list[dict]:
        nonlocal changed, building, laid_out, placed, last_say, fallback_say
        op = str(raw.get("op", "")).strip().lower()
        who = source[0]
        if who == "craft":
            if op not in ("make", "blueprint"):
                return []
            # it is this asset and no other, whatever the modeller called it
            raw = {**raw, "name": source[1], "kind": crafts[source[1]]["kind"], "refine": None}
        elif who in PASS:
            if op in _NOT_IN_A_PASS or (who == "detail" and op in ("say", "hud", "stage", "var", "zone", "when")):
                return []
            # two calls writing at once may pick the same id: the later one is a second thing, not a replacement
            identity = str(raw.get("id") or "")
            if op in ("spawn", "scatter", "road", "zone", "fx", "when") and identity and owned.get(identity, source) != source:
                raw = {**raw, "id": identity + "_" + who[0]}
        elif who == "talk" and op == "say":
            raw = {**raw, "op": "speak"}     # a character does not talk through the terminal
        if who == "talk" and op in ("speak", "say") and not raw.get("id"):
            raw = {**raw, "id": talk_to}

        before = len(checker.missing)
        stmt = checker.check(raw, strict)
        if stmt is None:
            if checker.problem:
                problem, checker.problem = checker.problem, None
                return [{"op": "note", "text": problem}]
            if len(checker.missing) > before or (strict and str(raw.get("asset", "")) and not checker.knows(str(raw.get("asset")))):
                held.append((source, raw))
            else:
                log.info("statement dropped: %.200s", raw)
            return []
        op = stmt["op"]
        if op in ("spawn", "scatter", "road", "zone", "fx", "when") and stmt.get("id"):
            owned.setdefault(stmt["id"], source)

        if op == "say":
            if held and strict:
                held.append((source, raw))     # it talks about things that are not there yet
                return []
            if who == "director" and enriching():
                fallback_say = stmt            # the play pass will know better what there is to do
                return []
            if who == "play":
                last_say = stmt
                return []
            said.append(stmt["text"])
            return [speak(stmt)]
        if op == "think":
            return [stmt]
        if op in ("speak", "narrate"):
            said.append(stmt["text"])     # something said is not something built: no "building", and the turn may still be only an answer
            return [speak(stmt)]
        if op in _LAYS_OUT and who == "director":
            laid_out = True
        if op == "make" and "refine" in stmt:
            brief = stmt.pop("refine")
            if caps.play and config.DEPTH >= 1 and kind == "user" and who != "craft" and prompts.modular \
                    and stmt["name"] not in crafts and len(crafts) < config.CRAFTS:
                crafts[stmt["name"]] = stmt
                lines.start(("craft", stmt["name"]), _craft_lines(prompts, stmt["name"], stmt["kind"], brief, stmt["parts"]),
                            {"max_tokens": 9000, "temperature": 0.6})
        if caps.gen and gen.enabled():
            wanted = "sky" if op == "sky" else "picture" if op in ("spawn", "scatter") else None
            if wanted and stmt.get("image"):
                stmt["image_url"] = f"{base_url}/v1/gen/{gen.start(wanted, stmt['image'])}"
        out = []
        if not building:
            building = True
            out.append({"op": "status", "state": "building"})
        changed = True
        placed += 1
        return out + [stmt]

    try:
        lines.start(("director",), _model_lines(system, content, director_model), director_options)
        timed_out = False
        while lines.open:
            try:
                source, raw, failure = await asyncio.wait_for(lines.queue.get(), max(deadline - time.monotonic(), 0.01))
            except asyncio.TimeoutError:
                timed_out = True
                log.warning("turn %.8s ran out of time with %d calls still writing", session.id, lines.open)
                break
            if raw is None:
                lines.open -= 1
                if source[0] == "director":
                    if failure is not None:
                        raise failure
                    if enriching() and placed >= 3:
                        for name in PASS:
                            # whoever writes the game is shown the same worked examples the director was
                            world_now = build_user_content(session, text, frame, user_state, play_state, examples if name == "play" else "")
                            lines.start((name,), _model_lines(prompts.for_pass(name, modules, interiors), world_now + "\n\n" + PASS[name],
                                                               config.PASS_MODEL, attempts=1), {"max_tokens": 4500})
                elif failure is not None:
                    log.warning("the %s call failed and its part is left out: %s", "/".join(source), failure)
                continue
            if source[0] == "director" and str(raw.get("op", "")).lower() == "ignore":
                ignored = True     # not addressed to us: silence is the right answer
                continue
            # a character handing over "a key" means the nearest key there is: nothing waits to be made in a conversation
            for stmt in play(raw, strict=kind != "talk", source=("talk",) if kind == "talk" else source):
                yield stmt

        if checker.missing and not timed_out and kind != "talk":
            # The model named things nobody has. It is asked to make them; whatever it still does
            # not make is played anyway, and the headset stands a labelled block in for it.
            names = list(checker.missing)
            log.info("turn %.8s not in the library: %s", session.id, ", ".join(names))
            yield {"op": "note", "text": "no " + ", ".join(n.replace("_", " ") for n in names) + " in the library, making it"}
            try:
                async for raw in _model_lines(system, content + "\n\n" + MAKE_MISSING.format(names="\n".join("- " + n for n in names))):
                    if str(raw.get("op", "")).lower() in ("make", "blueprint"):
                        for stmt in play({**raw, "refine": None}, strict=False):
                            yield stmt
            except llm.LLMError as exc:
                log.warning("making the missing assets failed: %s", exc)
        waiting, held = held, []
        for source, raw in waiting:
            for stmt in play(raw, strict=False, source=source):
                yield stmt
        closing = last_say or fallback_say
        if closing is not None:
            said.append(closing["text"])
            yield speak(closing)
    except llm.LLMError:
        yield {"op": "error", "message": "I lost my connection to the AI partway through. What's built so far stays; try again.", "retryable": True}
        yield {"op": "done", "outcome": "failed"}
        return
    except Exception:  # noqa: BLE001 - never leave the stream without `done`
        log.exception("turn failed")
        yield {"op": "error", "message": "Something went wrong on my side. Try again.", "retryable": True}
        yield {"op": "done", "outcome": "failed"}
        return
    finally:
        await lines.stop()

    if ignored and not changed and not said:
        yield {"op": "done", "outcome": "ignored"}
        return
    if kind == "talk":
        if not said:
            reply = checker.check({"op": "speak", "id": talk_to, "text": "Hm."})
            if reply:
                said.append(reply["text"])
                yield speak(reply)
        session.spoken_with(talk_to).append((text, " ".join(said)[:300]))
    elif not changed and not said:
        if kind == "event":
            yield {"op": "done", "outcome": "ignored"}
            return
        yield {"op": "say", "text": "I couldn't work out how to build that. Try saying it another way."}
        said.append("")
    if kind != "talk":
        session.history.append((("(it happened) " if kind == "event" else "") + text, " ".join(said)[:300]))
    session.touch()
    log.info("turn done: kind=%s changed=%s statements=%d crafts=%d, %.2fs", kind, changed, placed, len(crafts), time.perf_counter() - started)
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
