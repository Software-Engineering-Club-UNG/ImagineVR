"""Speech to text using local CPU faster-whisper only. Audio never goes to a provider."""

from __future__ import annotations

import asyncio
import logging
import struct
import threading
import time
import io
import wave

from . import config

log = logging.getLogger("imagine.stt")

_model = None
_model_lock = threading.Lock()

# Whisper tends to invent these when given silence or noise.
_HALLUCINATIONS = {
    "", "you", "thank you", "thanks", "thank you.", "thanks for watching", "thanks for watching!",
    "bye", "bye.", ".", "uh", "um", "so", "okay", "ok",
}

# Bias the decoder towards the words people actually say here.
_PROMPT = ("ImagineVR voice commands: put me in a city at night. Give me a new gun. Put a ray gun in front of my face. "
           "Make the sheep massive. Put seventeen pigs in front of me. Let me ride a dragon. Fill it with zombies.")


def wav_seconds(data: bytes) -> float:
    """Duration of a PCM WAV from its header. Returns 0 for anything unreadable."""
    try:
        with wave.open(io.BytesIO(data)) as wav:
            if (wav.getnchannels(), wav.getframerate(), wav.getsampwidth()) != (1, 16000, 2):
                return 0.0
            frames = wav.getnframes()
            if len(wav.readframes(frames)) != frames * 2:
                return 0.0
            return frames / 16000
    except (wave.Error, EOFError, ValueError):
        return 0.0


def wav_to_samples(data: bytes):
    """Decode a PCM WAV to 16 kHz mono float32, the format Whisper expects.

    Done by hand so transcription does not depend on an audio-decoding library.
    """
    import numpy as np

    if data[:4] != b"RIFF" or data[8:12] != b"WAVE":
        raise ValueError("not a WAV file")
    pos, fmt, pcm = 12, None, None
    while pos + 8 <= len(data):
        chunk_id, size = data[pos:pos + 4], struct.unpack("<I", data[pos + 4:pos + 8])[0]
        body = data[pos + 8:pos + 8 + size]
        if chunk_id == b"fmt ":
            fmt = struct.unpack("<HHIIHH", body[:16])
        elif chunk_id == b"data":
            pcm = body
            break
        pos += 8 + size + (size & 1)
    if fmt is None or pcm is None:
        raise ValueError("WAV is missing its format or data")
    audio_format, channels, rate, _byte_rate, _align, bits = fmt
    if audio_format == 1 and bits == 16:
        samples = np.frombuffer(pcm[: len(pcm) // 2 * 2], dtype="<i2").astype(np.float32) / 32768.0
    elif audio_format == 1 and bits == 8:
        samples = (np.frombuffer(pcm, dtype=np.uint8).astype(np.float32) - 128.0) / 128.0
    elif audio_format == 3 and bits == 32:
        samples = np.frombuffer(pcm[: len(pcm) // 4 * 4], dtype="<f4").astype(np.float32)
    else:
        raise ValueError(f"unsupported WAV encoding (format {audio_format}, {bits} bits)")
    if channels > 1:
        samples = samples[: len(samples) // channels * channels].reshape(-1, channels).mean(axis=1)
    if rate != 16000 and len(samples) > 1:
        target = int(round(len(samples) * 16000 / rate))
        samples = np.interp(
            np.linspace(0.0, len(samples) - 1, target), np.arange(len(samples)), samples
        ).astype(np.float32)
    return samples


def _load_model():
    global _model
    with _model_lock:
        if _model is None:
            from faster_whisper import WhisperModel

            started = time.perf_counter()
            _model = WhisperModel(
                config.STT_MODEL_PATH or config.STT_MODEL,
                device="cpu", compute_type="int8", cpu_threads=4, num_workers=1,
                download_root=config.STT_CACHE, local_files_only=bool(config.STT_MODEL_PATH),
            )
            log.info("whisper %s loaded in %.1fs", config.STT_MODEL, time.perf_counter() - started)
    return _model


def warm_up() -> None:
    """Load the model at startup so the first request is not slow."""
    if config.STT == "local":
        _load_model()


def _transcribe_local(wav: bytes) -> str:
    model = _load_model()
    segments, _info = model.transcribe(
        wav_to_samples(wav),
        language="en",
        beam_size=1,
        vad_filter=True,
        vad_parameters={"min_silence_duration_ms": 400},
        initial_prompt=_PROMPT,
        condition_on_previous_text=False,
        without_timestamps=True,
    )
    parts = [s.text.strip() for s in segments if s.no_speech_prob < 0.7]
    return " ".join(p for p in parts if p).strip()


async def transcribe(wav: bytes) -> str:
    """Return what was said, or an empty string when nothing intelligible was heard."""
    started = time.perf_counter()
    if config.STT != "local":
        raise RuntimeError("Only local Whisper transcription is supported")
    text = await asyncio.to_thread(_transcribe_local, wav)
    log.info("transcribed %.1fs of audio in %.2fs", wav_seconds(wav), time.perf_counter() - started)
    if text.lower().strip(" .!?") in _HALLUCINATIONS:
        return ""
    return text
