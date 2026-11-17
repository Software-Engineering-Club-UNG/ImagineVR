"""Spoken replies, narration, and the voices of characters.

A statement that is spoken gets an audio URL straight away; the speech is synthesised in the
background and the headset's request for that URL waits for it. World statements are never
delayed by speech.

Three ways to speak, chosen by IMAGINE_TTS (see config.py):
  openrouter   OpenRouter's speech API, with the key everything else uses. Each of the language's
               voices ("gruff", "child", "narrator"...) is a provider voice plus a line of direction.
  elevenlabs   ElevenLabs directly, as before the third version. Needs ELEVENLABS_API_KEY.
  local        Kokoro on this machine's own processor, when `kokoro-onnx` and its model files are
               installed. Nothing leaves the machine and nothing is paid per line.
"""

from __future__ import annotations

import asyncio
import io
import json
import logging
import time
import uuid
import wave

import httpx

from . import config, llm

log = logging.getLogger("imagine.tts")

_KEEP_SECONDS = 5 * 60
_clips: dict[str, tuple[float, asyncio.Task]] = {}

# the language's voices as (voice of an OpenAI-style speech model, how to deliver the line)
OPENROUTER_VOICES = {
    "narrator": ("sage", "A calm storyteller reading aloud, unhurried."),
    "warm": ("coral", "Warm and friendly."),
    "bright": ("nova", "Bright and upbeat."),
    "deep": ("onyx", "Deep and steady."),
    "gruff": ("ash", "Gruff and rough-edged, a little impatient."),
    "old": ("fable", "An old person: slow, creaky and kindly."),
    "young": ("alloy", "Young and quick."),
    "child": ("shimmer", "A small child, high-pitched and excited."),
    "robot": ("echo", "A machine: flat, clipped, every word evenly spaced."),
    "monster": ("onyx", "A huge monster: very low, growling, slow."),
    "whisper": ("sage", "Whispering, close and secretive."),
    "villain": ("ash", "Cold, slow and menacing."),
    "hero": ("verse", "Bold and confident."),
    "cheerful": ("ballad", "Cheerful, almost singing."),
}
LOCAL_VOICES = {
    "narrator": "bm_george", "warm": "af_heart", "bright": "af_bella", "deep": "am_onyx", "gruff": "am_fenrir", "old": "bm_lewis",
    "young": "am_puck", "child": "af_nicole", "robot": "am_echo", "monster": "am_onyx", "whisper": "af_sky", "villain": "bm_daniel",
    "hero": "am_michael", "cheerful": "af_nova",
}
_kokoro = None
_kokoro_failed = False


def _local_ready() -> bool:
    if _kokoro is not None:
        return True
    if _kokoro_failed or not (config.TTS_LOCAL_MODEL and config.TTS_LOCAL_VOICES):
        return False
    try:
        import kokoro_onnx  # noqa: F401
    except ImportError:
        return False
    return True


def mode() -> str:
    wanted = config.TTS_MODE
    if wanted == "elevenlabs":
        return "elevenlabs" if config.ELEVENLABS_API_KEY else "off"
    if wanted == "openrouter":
        return "openrouter" if config.OPENROUTER_API_KEY else "off"
    if wanted == "local":
        return "local" if _local_ready() else "off"
    if wanted == "on" or config.TTS:
        # whichever there is: the key that was asked for by name first, as before
        return "elevenlabs" if config.ELEVENLABS_API_KEY else "local" if _local_ready() else "openrouter" if config.OPENROUTER_API_KEY else "off"
    return "off"


def enabled() -> bool:
    return mode() != "off"


def _voices() -> dict:
    """The table of voices, with whatever IMAGINE_TTS_VOICES says laid over it."""
    table = {name: {"voice": voice, "instructions": how} for name, (voice, how) in OPENROUTER_VOICES.items()}
    if config.TTS_VOICES:
        try:
            for name, entry in json.loads(config.TTS_VOICES).items():
                if isinstance(entry, dict) and entry.get("voice"):
                    table[name] = {"voice": str(entry["voice"]), "instructions": str(entry.get("instructions", ""))}
        except (ValueError, AttributeError):
            log.warning("IMAGINE_TTS_VOICES is not a JSON object of voices; the built-in table is used")
    return table


async def _openrouter(text: str, voice: str | None) -> bytes:
    chosen = _voices().get(voice or "narrator") or _voices()["narrator"]
    body = {"model": config.TTS_MODEL, "input": text, "voice": chosen["voice"], "response_format": "mp3"}
    if chosen.get("instructions"):
        body["instructions"] = chosen["instructions"]
    headers = {"Authorization": f"Bearer {config.OPENROUTER_API_KEY}", "Content-Type": "application/json", "X-Title": "ImagineVR"}
    async with httpx.AsyncClient(timeout=25.0) as client:
        response = await client.post(config.OPENROUTER_BASE + "/audio/speech", headers=headers, json=body)
        if response.status_code == 400 and "instructions" in body:
            # a speech model that takes no direction still has the voice
            body.pop("instructions")
            response = await client.post(config.OPENROUTER_BASE + "/audio/speech", headers=headers, json=body)
        if response.status_code != 200:
            raise RuntimeError(f"speech API HTTP {response.status_code}: {llm.error_detail(response.text)}")
        return response.content


async def _elevenlabs(text: str, voice: str | None) -> bytes:
    custom = _voices().get(voice or "") if config.TTS_VOICES else None
    voice_id = custom["voice"] if custom and len(custom["voice"]) > 12 else config.TTS_VOICE
    url = f"https://api.elevenlabs.io/v1/text-to-speech/{voice_id}?output_format=mp3_44100_64"
    async with httpx.AsyncClient(timeout=20.0) as client:
        response = await client.post(
            url,
            headers={"xi-api-key": config.ELEVENLABS_API_KEY, "Content-Type": "application/json"},
            json={"text": text, "model_id": "eleven_flash_v2_5"},
        )
        response.raise_for_status()
        return response.content


def _speak_locally(text: str, voice: str | None) -> bytes:
    global _kokoro, _kokoro_failed
    try:
        if _kokoro is None:
            from kokoro_onnx import Kokoro
            _kokoro = Kokoro(config.TTS_LOCAL_MODEL, config.TTS_LOCAL_VOICES)
        samples, rate = _kokoro.create(text, voice=LOCAL_VOICES.get(voice or "narrator", "bm_george"), speed=1.0, lang="en-us")
    except Exception:
        _kokoro_failed = True
        raise
    import numpy as np
    pcm = (np.clip(samples, -1.0, 1.0) * 32767).astype("<i2").tobytes()
    out = io.BytesIO()
    with wave.open(out, "wb") as clip:
        clip.setnchannels(1)
        clip.setsampwidth(2)
        clip.setframerate(int(rate))
        clip.writeframes(pcm)
    return out.getvalue()


async def _synthesise(text: str, voice: str | None = None) -> bytes:
    how = mode()
    if how == "openrouter":
        return await _openrouter(text, voice)
    if how == "local":
        return await asyncio.to_thread(_speak_locally, text, voice)
    return await _elevenlabs(text, voice)


def start(text: str, voice: str | None = None) -> str:
    """Begin synthesising and return the clip id. Its ending says what kind of sound file it will be."""
    _sweep()
    clip_id = uuid.uuid4().hex + (".wav" if mode() == "local" else ".mp3")
    _clips[clip_id] = (time.monotonic(), asyncio.create_task(_synthesise(text, voice)))
    return clip_id


def media_type(clip_id: str) -> str:
    return "audio/wav" if clip_id.endswith(".wav") else "audio/mpeg"


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
