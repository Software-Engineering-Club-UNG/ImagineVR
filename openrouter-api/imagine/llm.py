"""OpenRouter streaming client. Yields the model's output one complete line at a time."""

from __future__ import annotations

import json
from typing import AsyncIterator

import httpx

from . import config

URL = config.OPENROUTER_URL


class LLMError(Exception):
    pass


_client: httpx.AsyncClient | None = None


def client() -> httpx.AsyncClient:
    global _client
    if _client is None:
        # one shared client keeps the TLS connection warm between turns
        _client = httpx.AsyncClient(timeout=httpx.Timeout(60.0, connect=8.0), http2=False)
    return _client


def build_messages(system_prompt: str, user_content: str, model: str) -> list[dict]:
    """The long, stable instructions go first so provider-side prompt caching can apply."""
    if model.startswith(("anthropic/", "google/")):
        system = {
            "role": "system",
            "content": [{"type": "text", "text": system_prompt, "cache_control": {"type": "ephemeral"}}],
        }
    else:
        system = {"role": "system", "content": system_prompt}
    return [system, {"role": "user", "content": user_content}]


async def stream_lines(system_prompt: str, user_content: str, model: str | None = None) -> AsyncIterator[str]:
    model = model or config.MODEL
    body: dict = {
        "model": model,
        "messages": build_messages(system_prompt, user_content, model),
        "stream": True,
        "temperature": 0.8,
        "max_tokens": 2500,
    }
    if config.REASONING in ("low", "medium", "high"):
        body["reasoning"] = {"effort": config.REASONING, "exclude": True}
    elif config.REASONING == "off":
        body["reasoning"] = {"enabled": False}
    headers = {
        "Authorization": f"Bearer {config.OPENROUTER_API_KEY}",
        "Content-Type": "application/json",
        "X-Title": "ImagineVR",
    }
    try:
        async with client().stream("POST", URL, headers=headers, json=body) as response:
            if response.status_code != 200:
                raise LLMError(f"OpenRouter HTTP {response.status_code}")
            buffer = ""
            finished = False
            reason = None
            async for raw in response.aiter_lines():
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
                delta = (choices[0].get("delta") or {}).get("content") or ""
                if not delta:
                    continue
                buffer += delta
                while "\n" in buffer:
                    line, buffer = buffer.split("\n", 1)
                    if line.strip():
                        yield line
            if not finished or reason != "stop":
                raise LLMError("OpenRouter stream ended without successful completion")
            if buffer.strip():
                yield buffer
    except httpx.HTTPError as exc:
        raise LLMError(f"network error talking to OpenRouter: {exc.__class__.__name__}") from exc
