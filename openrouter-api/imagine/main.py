"""ImagineVR server: the only thing the headset talks to.

Run locally:  uv run uvicorn imagine.main:app --port 8080
"""

from __future__ import annotations

import asyncio
import json
import logging
import re
from contextlib import asynccontextmanager

from fastapi import FastAPI, Header, HTTPException, Request, Response
from fastapi.responses import JSONResponse, StreamingResponse

from . import LANGUAGE_VERSION, config, stt, tts, llm
from .catalog import Catalog
from .script import to_frame
from .session import SessionStore
from .turn import run_turn, reset_stt_slots
from .rules import CAPABILITY, CAPABILITIES, USER_STATE, RuleError, validate_snapshot
from .blueprints import CAPABILITY as BLUEPRINT_CAPABILITY, CONTRACT as BLUEPRINT_CONTRACT

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(name)s %(levelname)s %(message)s")
log = logging.getLogger("imagine")


class State:
    catalog: Catalog
    catalog_mtime: float = 0.0
    prompt_template: str = ""
    prompt_mtime: float = 0.0
    system_prompt: str = ""
    sessions = SessionStore()


state = State()


def load_files(force: bool = False) -> None:
    """(Re)load the catalog and prompt when their files change, so neither needs a restart."""
    catalog_mtime = config.CATALOG_PATH.stat().st_mtime
    prompt_mtime = config.PROMPT_PATH.stat().st_mtime
    if not force and catalog_mtime == state.catalog_mtime and prompt_mtime == state.prompt_mtime:
        return
    state.catalog = Catalog.load(config.CATALOG_PATH)
    state.prompt_template = config.PROMPT_PATH.read_text(encoding="utf-8")
    state.system_prompt = state.prompt_template.replace("{{CATALOG}}", state.catalog.render())
    state.catalog_mtime, state.prompt_mtime = catalog_mtime, prompt_mtime
    log.info(
        "catalog %s loaded: %d kits, %d assets, prompt is %d characters",
        state.catalog.version, len(state.catalog.ids("kit")), len(state.catalog.ids("asset")), len(state.system_prompt),
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
        log.warning("no OPENROUTER_API_KEY: running with the offline director (keywords only, no AI)")
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


# An app that understands the richer blueprint parts says so; one built before them is sent plainer recipes.
RICH_BLUEPRINTS = 'runtime-blueprints-v2'
_ONLY = re.compile(r'<!--(blueprints|shapes-only)-->\n?(.*?)<!--/\1-->\n?', re.S)


def for_client(prompt: str, blueprints: bool) -> str:
    """
    The instructions as this app should hear them. Passages marked `blueprints` are for an app
    that can build things from recipes, and `shapes-only` for one that cannot: each app is told
    one way of making what the library lacks, never two that contradict each other.
    """
    keep = 'blueprints' if blueprints else 'shapes-only'
    return _ONLY.sub(lambda m: m.group(2) if m.group(1) == keep else '', prompt)


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
        "capabilities": [CAPABILITY, BLUEPRINT_CAPABILITY, RICH_BLUEPRINTS, USER_STATE],
    }


@app.post("/v1/turn")
async def turn(
    request: Request,
    x_app_token: str | None = Header(default=None),
    x_session_id: str | None = Header(default=None),
    x_catalog_version: str | None = Header(default=None),
    x_user_pose: str | None = Header(default=None),
    x_client_capabilities: str | None = Header(default=None),
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
        if not text:
            raise HTTPException(status_code=400, detail="empty request")
    else:
        raise HTTPException(status_code=415, detail="expected audio/wav or application/json")

    session = state.sessions.get(x_session_id)
    if not session.allow_turn():
        raise HTTPException(status_code=429, detail="too many requests, slow down")

    base_url = config.PUBLIC_URL or str(request.base_url).rstrip("/")

    async def stream():
        allow_rules = CAPABILITY in (x_client_capabilities or '').split(',')
        allow_blueprints = allow_rules and BLUEPRINT_CAPABILITY in (x_client_capabilities or '').split(',')
        rich_blueprints = allow_blueprints and RICH_BLUEPRINTS in (x_client_capabilities or '').split(',')
        prompt = for_client(state.system_prompt, allow_blueprints)
        if allow_rules:
            prompt += '\n\n' + (config.PROMPT_PATH.parent / 'rules-prompt.md').read_text(encoding='utf-8')
            prompt += '\nRULE CAPABILITIES:\n' + json.dumps(CAPABILITIES, separators=(',', ':'))
        if allow_blueprints:
            prompt += '\n\n' + (config.PROMPT_PATH.parent / 'blueprints-prompt.md').read_text(encoding='utf-8')
            prompt += '\nBLUEPRINT CAPABILITIES:\n' + json.dumps(BLUEPRINT_CONTRACT,separators=(',',':'))
        statements = run_turn(
            session, state.catalog, prompt, text=text, audio=audio, base_url=base_url,
            frame=to_frame(x_user_pose), allow_rules=allow_rules, allow_blueprints=allow_blueprints, rich_blueprints=rich_blueprints,
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
        state.sessions.get(x_session_id)
        await state.sessions.reset(x_session_id)
    return Response(status_code=204, headers={'X-Session-Generation': str(state.sessions.get(x_session_id).generation)} if x_session_id else {})


@app.post('/v1/session/runtime')
async def runtime(request: Request, x_app_token: str | None = Header(default=None), x_session_id: str | None = Header(default=None)):
    check_token(x_app_token)
    if not x_session_id:
        raise HTTPException(400, 'missing X-Session-Id')
    body = bytearray()
    async for chunk in request.stream():
        body.extend(chunk)
        if len(body) > 1_000_000: raise HTTPException(413, 'runtime snapshot too large')
    try:
        snapshot = validate_snapshot(json.loads(body))
    except (ValueError, UnicodeDecodeError) as exc:
        raise HTTPException(400, 'invalid runtime snapshot: ' + str(exc)) from exc
    session = state.sessions.get(x_session_id)
    async with session.lock:
        if snapshot['generation'] != session.generation or snapshot['sequence'] <= session.runtime_sequence:
            raise HTTPException(409, 'stale runtime snapshot; reset session')
        session.world.runtime = snapshot
        session.world.rules = {rule['id']: rule for rule in snapshot['rules']}
        if 'blueprints' in snapshot:
            session.world.blueprints = {b['id']: b for b in snapshot['blueprints']}
        session.runtime_sequence = snapshot['sequence']
    return {'ok': True, 'generation': session.generation, 'sequence': snapshot['sequence']}


@app.get("/v1/audio/{clip_id}")
async def audio(clip_id: str):
    data = await tts.fetch(clip_id)
    if data is None:
        raise HTTPException(status_code=404, detail="no such clip")
    return Response(content=data, media_type="audio/mpeg")
