"""OpenRouter streaming client. Yields the model's output one complete line at a time."""

from __future__ import annotations

import asyncio
import contextvars
import json
import re
import time
from typing import AsyncIterator

import httpx

from . import config

URL = config.OPENROUTER_URL


class LLMError(Exception):
    pass


def error_detail(body: str) -> str:
    """Useful provider diagnostics without logging credentials or control characters."""
    try:
        payload = json.loads(body)
        error = payload.get("error", {})
        detail = str(error.get("message", "")) if isinstance(error, dict) else str(error)
    except (json.JSONDecodeError, AttributeError):
        detail = "unreadable provider error"
    for secret in (config.OPENROUTER_API_KEY, config.ELEVENLABS_API_KEY, config.APP_TOKEN):
        if secret:
            detail = detail.replace(secret, "[redacted]")
    detail = re.sub(r"(?i)bearer\s+[^\s]+|sk-or-[A-Za-z0-9_-]+", "[redacted]", detail)
    return " ".join(detail.split())[:200]


_client: httpx.AsyncClient | None = None


def client() -> httpx.AsyncClient:
    global _client
    if _client is None:
        # one shared client keeps the TLS connection warm between turns
        # a provider that says nothing at all is given up on as soon as one that is slow to start (see stream_lines)
        quiet = max(config.FIRST_TOKEN_SECONDS, config.STALL_SECONDS)
        _client = httpx.AsyncClient(timeout=httpx.Timeout(60.0, connect=8.0, read=quiet), http2=False)
    return _client


# What follows this line in a system prompt is particular to one call (the sections chosen for this request);
# what comes before is the same for every call and is what the provider is asked to cache.
SPLIT = "\n<<<THIS REQUEST>>>\n"

# How this call differs from the ordinary one: {"max_tokens": 1200, "temperature": 0.5, "images": [data URLs]}.
# Set by the caller in its own task, so that calls running side by side each keep their own.
options: contextvars.ContextVar[dict | None] = contextvars.ContextVar("imagine_llm_options", default=None)


def build_messages(system_prompt: str, user_content: str, model: str, images: list[str] | None = None) -> list[dict]:
    """The long, stable instructions go first so provider-side prompt caching can apply."""
    stable, _, particular = system_prompt.partition(SPLIT)
    if model.startswith(("anthropic/", "google/")):
        content = [{"type": "text", "text": stable, "cache_control": {"type": "ephemeral"}}]
        if particular:
            content.append({"type": "text", "text": particular})
        system = {"role": "system", "content": content}
    else:
        system = {"role": "system", "content": stable + ("\n" + particular if particular else "")}
    if images:
        user = {"role": "user", "content": [{"type": "text", "text": user_content}] + [{"type": "image_url", "image_url": {"url": url}} for url in images]}
    else:
        user = {"role": "user", "content": user_content}
    return [system, user]


async def stream_lines(system_prompt: str, user_content: str, model: str | None = None) -> AsyncIterator[str]:
    model = model or config.MODEL
    wanted = options.get() or {}
    body: dict = {
        "model": model,
        "messages": build_messages(system_prompt, user_content, model, wanted.get("images")),
        "stream": True,
        "temperature": wanted.get("temperature", 0.8),
        "max_tokens": wanted.get("max_tokens", 7000),
    }
    if config.REASONING in ("low", "medium", "high"):
        # not excluded from the stream: its arrival is how a model that is thinking is told from one that has hung
        body["reasoning"] = {"effort": config.REASONING}
    elif config.REASONING == "off":
        body["reasoning"] = {"enabled": False}
    headers = {
        "Authorization": f"Bearer {config.OPENROUTER_API_KEY}",
        "Content-Type": "application/json",
        "X-Title": "ImagineVR",
    }
    # A provider that hangs is worse than one that fails: the user stands waiting. So the model
    # gets a configurable start allowance and content-stall allowance (keep-alives do not count),
    # and past that the caller is told, while there is still time to ask again.
    heard = time.monotonic()
    started = False
    try:
        async with client().stream("POST", URL, headers=headers, json=body) as response:
            if response.status_code != 200:
                body_text = (await response.aread()).decode("utf-8", "replace")
                raise LLMError(f"OpenRouter HTTP {response.status_code}: {error_detail(body_text)}")
            buffer = ""
            finished = False
            reason = None
            frames = response.aiter_lines().__aiter__()
            while True:
                left = (config.STALL_SECONDS if started else config.FIRST_TOKEN_SECONDS) - (time.monotonic() - heard)
                try:
                    raw = await asyncio.wait_for(anext(frames), max(left, 0.01))
                except StopAsyncIteration:
                    break
                except asyncio.TimeoutError:
                    raise LLMError("OpenRouter went quiet " + ("partway through" if started else "before answering")) from None
                if not raw.startswith("data:"):
                    continue
                data = raw[5:].strip()
                if data == "[DONE]":
                    finished = True
                    break
                try:
                    chunk = json.loads(data)
                except json.JSONDecodeError:
                    continue
                if "error" in chunk:
                    raise LLMError("OpenRouter stream failed")
                choices = chunk.get("choices") or []
                if not choices:
                    continue
                reason = choices[0].get("finish_reason") or reason
                if reason in ("length", "error", "content_filter"):
                    raise LLMError(f"OpenRouter incomplete response: {reason}")
                message = choices[0].get("delta") or {}
                delta = message.get("content") or ""
                if not delta:
                    # thinking is not writing, but it is not silence either: the wait for the first line starts over
                    if message.get("reasoning") or message.get("reasoning_details"):
                        heard = time.monotonic()
                    continue
                started = True
                heard = time.monotonic()
                buffer += delta
                while "\n" in buffer:
                    line, buffer = buffer.split("\n", 1)
                    if line.strip():
                        yield line
            if not finished:
                raise LLMError("OpenRouter stream ended without successful completion")
            if buffer.strip():
                yield buffer
    except httpx.HTTPError as exc:
        raise LLMError(f"network error talking to OpenRouter: {exc.__class__.__name__}") from exc
