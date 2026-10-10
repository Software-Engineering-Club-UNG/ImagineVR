"""ImagineVR server: the only thing the headset talks to.

Run locally:  uv run uvicorn imagine.main:app --port 8080
"""

from __future__ import annotations

import asyncio
import json
import logging
import os
from contextlib import asynccontextmanager

from fastapi import FastAPI, Header, HTTPException, Request, Response
from fastapi.responses import JSONResponse, StreamingResponse

from . import LANGUAGE_VERSION, config, stt, tts
from .catalog import Catalog
from .session import SessionStore
from .turn import run_turn

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
    if not config.CATALOG_PATH.exists():
        raise RuntimeError(
            f"Catalog not found at {config.CATALOG_PATH}. Export it from Unity (ImagineVR > Export Catalog) "
            "or set IMAGINE_CATALOG."
        )
    load_files(force=True)
    if config.OFFLINE:
        log.warning("no OPENROUTER_API_KEY: running with the offline director (keywords only, no AI)")
    else:
        log.info("model: %s", config.MODEL)
    asyncio.get_running_loop().run_in_executor(None, stt.warm_up)
    yield


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
    }


@app.get("/health")
async def deploy_health():
    """The shape the Vultr deploy script checks after switching releases (see infra/deploy/deploy.sh)."""
    return {"status": "ok", "revision": os.environ.get("DEPLOYMENT_REVISION", "development"),
            "model": "offline" if config.OFFLINE else config.MODEL}


@app.post("/v1/turn")
async def turn(
    request: Request,
    x_app_token: str | None = Header(default=None),
    x_session_id: str | None = Header(default=None),
    x_catalog_version: str | None = Header(default=None),
):
    check_token(x_app_token)
    if not x_session_id:
        raise HTTPException(status_code=400, detail="missing X-Session-Id")
    load_files()
    if x_catalog_version and x_catalog_version != state.catalog.version:
        log.warning("app catalog %s differs from server catalog %s", x_catalog_version, state.catalog.version)

    content_type = (request.headers.get("content-type") or "").split(";")[0].strip().lower()
    body = await request.body()
    text: str | None = None
    audio: bytes | None = None
    if content_type in ("audio/wav", "audio/x-wav", "audio/wave"):
        seconds = stt.wav_seconds(body)
        if seconds <= 0:
            raise HTTPException(status_code=400, detail="unreadable audio")
        if seconds > config.MAX_AUDIO_SECONDS:
            raise HTTPException(status_code=413, detail="audio longer than 30 seconds")
        audio = body
    else:
        try:
            payload = json.loads(body or b"{}")
        except json.JSONDecodeError as exc:
            raise HTTPException(status_code=400, detail="body is not JSON") from exc
        text = str(payload.get("text", "")).strip() if isinstance(payload, dict) else ""
        if not text:
            raise HTTPException(status_code=400, detail="empty request")

    session = state.sessions.get(x_session_id)
    if not session.allow_turn():
        raise HTTPException(status_code=429, detail="too many requests, slow down")

    base_url = config.PUBLIC_URL or str(request.base_url).rstrip("/")

    async def stream():
        async for stmt in run_turn(
            session, state.catalog, state.system_prompt, text=text, audio=audio, base_url=base_url
        ):
            yield json.dumps(stmt, separators=(",", ":"), ensure_ascii=False) + "\n"

    return StreamingResponse(
        stream(),
        media_type="application/x-ndjson",
        headers={"Cache-Control": "no-cache, no-transform", "X-Accel-Buffering": "no"},
    )


@app.post("/v1/session/reset", status_code=204)
async def reset(x_app_token: str | None = Header(default=None), x_session_id: str | None = Header(default=None)):
    check_token(x_app_token)
    if x_session_id:
        state.sessions.reset(x_session_id)
    return Response(status_code=204)


@app.get("/v1/audio/{clip_id}")
async def audio(clip_id: str):
    data = await tts.fetch(clip_id)
    if data is None:
        raise HTTPException(status_code=404, detail="no such clip")
    return Response(content=data, media_type="audio/mpeg")
