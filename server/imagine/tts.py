"""Optional spoken replies through ElevenLabs.

A `say` statement gets an audio URL straight away; the speech is synthesised in the
background and the headset's request for that URL waits for it. World statements are never
delayed by speech.
"""

from __future__ import annotations

import asyncio
import logging
import time
import uuid

import httpx

from . import config

log = logging.getLogger("imagine.tts")

_KEEP_SECONDS = 5 * 60
_clips: dict[str, tuple[float, asyncio.Task]] = {}


def enabled() -> bool:
    return config.TTS and bool(config.ELEVENLABS_API_KEY)


async def _synthesise(text: str) -> bytes:
    url = f"https://api.elevenlabs.io/v1/text-to-speech/{config.TTS_VOICE}?output_format=mp3_44100_64"
    async with httpx.AsyncClient(timeout=20.0) as client:
        response = await client.post(
            url,
            headers={"xi-api-key": config.ELEVENLABS_API_KEY, "Content-Type": "application/json"},
            json={"text": text, "model_id": "eleven_flash_v2_5"},
        )
        response.raise_for_status()
        return response.content


def start(text: str) -> str:
    """Begin synthesising and return the clip id."""
    _sweep()
    clip_id = uuid.uuid4().hex
    _clips[clip_id] = (time.monotonic(), asyncio.create_task(_synthesise(text)))
    return clip_id


async def fetch(clip_id: str) -> bytes | None:
    entry = _clips.get(clip_id)
    if entry is None:
        return None
    try:
        return await entry[1]
    except Exception:  # noqa: BLE001 - a failed clip is just silence
        log.exception("speech synthesis failed")
        return None


def _sweep() -> None:
    cutoff = time.monotonic() - _KEEP_SECONDS
    for clip_id in [c for c, (made, _task) in _clips.items() if made < cutoff]:
        _clips.pop(clip_id, None)
