"""ImagineVR server: the only thing the headset talks to.

Run locally:  uv run uvicorn imagine.main:app --port 8080
"""

from __future__ import annotations

import asyncio
import json
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Header, HTTPException, Request, Response
from fastapi.responses import JSONResponse, StreamingResponse

from . import LANGUAGE_VERSION, config, gen, stt, tts, llm, v3
from .catalog import Catalog
from .fix import name_of, pick, text_of
from .language import to_frame
from .playbook import Playbook
from .prompts import Prompts
from .session import SessionStore
from .turn import run_turn, reset_stt_slots

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(name)s %(levelname)s %(message)s")
log = logging.getLogger("imagine")


class State:
    catalog: Catalog
    catalog_mtime: float = 0.0
    prompt_template: str = ""
    prompt_mtime: float = 0.0
    system_prompt: str = ""
    prompts: Prompts
    playbook: Playbook
    sessions = SessionStore()


state = State()


def _newest(*folders) -> float:
    """When anything the prompts are made of last changed."""
    stamps = [config.PROMPT_PATH.stat().st_mtime]
    for folder in folders:
        if folder.is_dir():
            stamps += [path.stat().st_mtime for path in folder.iterdir() if path.suffix in (".md", ".ndjson")]
    return max(stamps)


def load_files(force: bool = False) -> None:
    """(Re)load the catalog, the prompts and the worked examples when their files change, so none needs a restart."""
    catalog_mtime = config.CATALOG_PATH.stat().st_mtime
    prompt_mtime = _newest(config.PROMPTS_DIR, config.playbook_path())
    if not force and catalog_mtime == state.catalog_mtime and prompt_mtime == state.prompt_mtime:
        return
    state.catalog = Catalog.load(config.CATALOG_PATH)
    state.prompt_template = config.PROMPT_PATH.read_text(encoding="utf-8")
    state.system_prompt = state.prompt_template.replace("{{CATALOG}}", state.catalog.render())
    state.prompts = Prompts.load(config.PROMPT_PATH, config.PROMPTS_DIR, state.catalog)
    state.playbook = Playbook.load(config.playbook_path())
    state.catalog_mtime, state.prompt_mtime = catalog_mtime, prompt_mtime
    log.info(
        "catalog %s loaded: %d assets, prompt is %d characters; sections %s; %d worked examples",
        state.catalog.version, len(state.catalog.ids("asset")), len(state.system_prompt),
        state.prompts.sizes(), len(state.playbook.examples),
    )


@asynccontextmanager
async def lifespan(_app: FastAPI):
    if config.PRODUCTION and (config.OFFLINE or not config.APP_TOKEN or config.APP_TOKEN == "dev-token"):
        raise RuntimeError("Production requires OPENROUTER_API_KEY and a non-development IMAGINE_APP_TOKEN")
    if config.STT != "local":
        raise RuntimeError("Speech-to-text runs locally only; set IMAGINE_STT=local")
    if not config.CATALOG_PATH.exists():
        raise RuntimeError(
            f"Catalog not found at {config.CATALOG_PATH}. Export it from Unity (ImagineVR > Export Catalog) "
            "or set IMAGINE_CATALOG."
        )
    load_files(force=True)
    reset_stt_slots()
    if config.OFFLINE:
        log.warning("no OPENROUTER_API_KEY: every turn will answer that no model is connected")
    else:
        log.info("model: %s", config.MODEL)
    await asyncio.to_thread(stt.warm_up)
    try:
        yield
    finally:
        if llm._client is not None:
            await llm._client.aclose()
            llm._client = None


app = FastAPI(title="ImagineVR", lifespan=lifespan)


def check_token(token: str | None) -> None:
    if config.APP_TOKEN and token != config.APP_TOKEN:
        raise HTTPException(status_code=401, detail="bad app token")


@app.exception_handler(HTTPException)
async def http_error(_request: Request, exc: HTTPException):
    return JSONResponse(status_code=exc.status_code, content={"message": str(exc.detail)})


@app.get("/v1/health")
async def health():
    return {
        "ok": True,
        "language_version": LANGUAGE_VERSION,
        "catalog_version": state.catalog.version,
        "model": "offline" if config.OFFLINE else config.MODEL,
        "stt": f"local:{config.STT_MODEL}" if config.STT == "local" else config.STT,
        "tts": tts.enabled(),
        "voice": tts.mode(),
        "images": gen.enabled(),
        "depth": config.DEPTH,
        "capabilities": [v3.CAPABILITY, v3.GENERATED],
    }


@app.post("/v1/turn")
async def turn(
    request: Request,
    x_app_token: str | None = Header(default=None),
    x_session_id: str | None = Header(default=None),
    x_catalog_version: str | None = Header(default=None),
    x_user_pose: str | None = Header(default=None),
    x_user_state: str | None = Header(default=None),
    x_client_capabilities: str | None = Header(default=None),
    x_play_state: str | None = Header(default=None),
    x_talk_to: str | None = Header(default=None),
):
    check_token(x_app_token)
    if not x_session_id:
        raise HTTPException(status_code=400, detail="missing X-Session-Id")
    load_files()
    if x_catalog_version and x_catalog_version != state.catalog.version:
        log.warning("app catalog %s differs from server catalog %s", x_catalog_version, state.catalog.version)

    content_type = (request.headers.get("content-type") or "").split(";")[0].strip().lower()
    body = bytearray()
    async for chunk in request.stream():
        body.extend(chunk)
        if len(body) > 1_100_000:
            raise HTTPException(status_code=413, detail="request too large")
    body = bytes(body)
    text: str | None = None
    audio: bytes | None = None
    event = ""
    if content_type in ("audio/wav", "audio/x-wav", "audio/wave"):
        seconds = stt.wav_seconds(body)
        if seconds <= 0:
            raise HTTPException(status_code=400, detail="unreadable audio")
        if seconds > config.MAX_AUDIO_SECONDS:
            raise HTTPException(status_code=413, detail="audio longer than 30 seconds")
        audio = body
    elif content_type == "application/json":
        try:
            payload = json.loads(body or b"{}")
        except (json.JSONDecodeError, UnicodeDecodeError) as exc:
            raise HTTPException(status_code=400, detail="body is not JSON") from exc
        text = str(payload.get("text", "")).strip() if isinstance(payload, dict) else ""
        # a world calling the director back: something happened in its story, and it asks what comes next
        event = (text_of(payload.get("event"), 300) or "") if isinstance(payload, dict) else ""
        if not text and not event:
            raise HTTPException(status_code=400, detail="empty request")
    else:
        raise HTTPException(status_code=415, detail="expected audio/wav or application/json")

    session = state.sessions.get(x_session_id)
    if event:
        if not session.allow_event():
            raise HTTPException(status_code=429, detail="the story is being told as fast as it can be")
    elif not session.allow_turn():
        raise HTTPException(status_code=429, detail="too many requests, slow down")

    base_url = config.PUBLIC_URL or str(request.base_url).rstrip("/")

    async def stream():
        statements = run_turn(
            session, state.catalog, state.prompts, text=text, audio=audio, base_url=base_url,
            frame=to_frame(x_user_pose), user_state=" ".join((x_user_state or "").split())[:1800],
            capabilities=(x_client_capabilities or "")[:200],
            play_state=" ".join((x_play_state or "").split())[:900], talk_to=name_of(x_talk_to),
            event=event, playbook=state.playbook,
        )
        pending = None
        try:
            while True:
                pending = asyncio.create_task(anext(statements))
                while not pending.done():
                    await asyncio.wait({pending}, timeout=3)
                    if not pending.done():
                        yield "\n"  # Unity ignores empty lines; keeps its eight-second watchdog alive.
                try:
                    stmt = pending.result()
                except StopAsyncIteration:
                    break
                yield json.dumps(stmt, separators=(",", ":"), ensure_ascii=False) + "\n"
        finally:
            if pending is not None and not pending.done():
                pending.cancel()
                await asyncio.gather(pending, return_exceptions=True)
            await statements.aclose()

    return StreamingResponse(
        stream(),
        media_type="application/x-ndjson",
        headers={"Cache-Control": "no-cache, no-transform", "X-Accel-Buffering": "no"},
    )


@app.post("/v1/session/reset", status_code=204)
async def reset(x_app_token: str | None = Header(default=None), x_session_id: str | None = Header(default=None)):
    check_token(x_app_token)
    if x_session_id:
        await state.sessions.reset(x_session_id)
    return Response(status_code=204)


@app.post("/v1/session/runtime")
async def runtime_from_an_old_app(x_app_token: str | None = Header(default=None)):
    """
    The first version of the app posts its rule state here before every turn and refuses to send
    the turn if this fails. Nothing is done with it now; answering keeps a headset that has not
    been updated yet talking, though it will not understand `make` or `when`. Delete this once
    no old build is in use.
    """
    check_token(x_app_token)
    return {"ok": True}


@app.get("/v1/audio/{clip_id}")
async def audio(clip_id: str):
    data = await tts.fetch(clip_id)
    if data is None:
        raise HTTPException(status_code=404, detail="no such clip")
    return Response(content=data, media_type=tts.media_type(clip_id))


@app.post("/v1/speak")
async def speak(request: Request, x_app_token: str | None = Header(default=None), x_session_id: str | None = Header(default=None)):
    """
    A line spoken by a rule in the headset (a character's bark, a narrator's cue) long after the
    turn that wrote it: the headset asks for its voice when it is needed. Answers with where the
    clip will be, or with no address when nothing here can speak.
    """
    check_token(x_app_token)
    try:
        payload = json.loads(await request.body() or b"{}")
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        raise HTTPException(status_code=400, detail="body is not JSON") from exc
    line = text_of(payload.get("text") if isinstance(payload, dict) else None, 300)
    if not line:
        raise HTTPException(status_code=400, detail="nothing to say")
    if not tts.enabled():
        return {"audio": None}
    session = state.sessions.get(x_session_id or "speech")
    if not session.allow_turn():
        raise HTTPException(status_code=429, detail="too many requests, slow down")
    base_url = config.PUBLIC_URL or str(request.base_url).rstrip("/")
    return {"audio": f"{base_url}/v1/audio/{tts.start(line, pick(payload.get('voice'), v3.VOICES))}"}


@app.get("/v1/gen/{key}")
async def generated(key: str):
    """A picture made for a statement. 202 while it is still being made: ask again."""
    if not key.isalnum() or len(key) > 40:
        raise HTTPException(status_code=404, detail="no such picture")
    data = await gen.fetch(key)
    if data is gen.PENDING:
        return Response(status_code=202, headers={"Retry-After": "2"})
    if data is None:
        raise HTTPException(status_code=404, detail="no such picture")
    return Response(content=data, media_type="image/jpeg", headers={"Cache-Control": "public, max-age=86400"})
