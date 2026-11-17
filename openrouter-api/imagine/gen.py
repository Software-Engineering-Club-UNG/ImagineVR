"""Pictures made on the spot: a panorama for the sky, a painting on a wall, a sketch to model from.

They go through OpenRouter's image API with the same key as everything else. A statement that
wants one is sent straight away with the address the picture will be at; the picture is made in
the background and the headset's request for that address waits for it (as spoken replies have
always worked, see tts.py). The world is never held up by a picture, and a picture that fails is
just the procedural sky or the plain wall that was already there.

The same words always make the same address, so asking twice costs once.
"""

from __future__ import annotations

import asyncio
import base64
import hashlib
import io
import logging
import time
from pathlib import Path

import httpx

from . import config, llm

log = logging.getLogger("imagine.gen")

_KEEP_SECONDS = 30 * 60
_jobs: dict[str, tuple[float, asyncio.Task]] = {}

# kind -> (aspect ratio, resolution tier, how it is asked for)
KINDS = {
    "sky": ("2:1", "2K", "Equirectangular 360 degree panorama for a game skybox: {prompt}. The horizon runs exactly along the "
            "vertical centre; the top half is sky; distant scenery sits on the horizon; the left and right edges join seamlessly. "
            "Painterly and stylised, soft detail. No people, no text, no borders, no frame."),
    "picture": ("4:3", "1K", "{prompt}. A single flat picture filling the whole frame, no border, no frame, no mock-up."),
    "concept": ("1:1", "1K", "A clean three-quarter view of {prompt}, as a simple low-poly toy model with flat colours on a plain "
                "light grey background. The whole object in frame, nothing else in the picture, no text."),
}
PENDING = object()


def enabled() -> bool:
    return config.IMAGES and not config.OFFLINE


def key_of(kind: str, prompt: str) -> str:
    return hashlib.sha1(f"{config.IMAGE_MODEL}|{kind}|{' '.join(prompt.lower().split())}".encode()).hexdigest()[:24]


def start(kind: str, prompt: str) -> str:
    """Begin making a picture (or find the one already made) and return its key."""
    _sweep()
    key = key_of(kind, prompt)
    if key not in _jobs:
        _jobs[key] = (time.monotonic(), asyncio.create_task(_make(kind, prompt, key)))
    return key


async def fetch(key: str, wait: float = 25.0):
    """The picture's bytes; PENDING when it is still being made after `wait` seconds; None when there is none."""
    entry = _jobs.get(key)
    if entry is None:
        return await asyncio.to_thread(_read, key)
    try:
        return await asyncio.wait_for(asyncio.shield(entry[1]), wait)
    except asyncio.TimeoutError:
        return PENDING
    except Exception:  # noqa: BLE001 - a picture that failed is a sky that stays as it was
        log.exception("making a picture failed")
        _jobs.pop(key, None)
        return None


async def concept(prompt: str, wait: float = 20.0) -> str | None:
    """A sketch of something about to be modelled, as a data URL a model that sees can be shown."""
    if not enabled():
        return None
    data = await fetch(start("concept", prompt), wait)
    return "data:image/jpeg;base64," + base64.b64encode(data).decode() if isinstance(data, (bytes, bytearray)) else None


async def _make(kind: str, prompt: str, key: str) -> bytes | None:
    cached = await asyncio.to_thread(_read, key)
    if cached:
        return cached
    aspect, resolution, wording = KINDS[kind]
    started = time.perf_counter()
    async with httpx.AsyncClient(timeout=httpx.Timeout(90.0, connect=8.0)) as client:
        response = await client.post(
            config.OPENROUTER_BASE + "/images",
            headers={"Authorization": f"Bearer {config.OPENROUTER_API_KEY}", "Content-Type": "application/json", "X-Title": "ImagineVR"},
            json={"model": config.IMAGE_MODEL, "prompt": wording.format(prompt=prompt), "aspect_ratio": aspect, "resolution": resolution},
        )
    if response.status_code != 200:
        raise RuntimeError(f"image API HTTP {response.status_code}: {llm.error_detail(response.text)}")
    items = response.json().get("data") or []
    if not items or not items[0].get("b64_json"):
        raise RuntimeError("image API returned no picture")
    data = await asyncio.to_thread(finish, kind, base64.b64decode(items[0]["b64_json"]))
    await asyncio.to_thread(_write, key, data)
    log.info("made a %s picture in %.1fs, %d KB", kind, time.perf_counter() - started, len(data) // 1024)
    return data


def finish(kind: str, raw: bytes) -> bytes:
    """Sized for a headset and saved as JPEG; a panorama also has its two ends made to meet."""
    try:
        from PIL import Image
    except ImportError:      # without Pillow the picture is used as it came
        return raw
    image = Image.open(io.BytesIO(raw)).convert("RGB")
    if kind == "sky":
        image = image.resize((2048, 1024), Image.LANCZOS)
        image = join_ends(image)
    else:
        image.thumbnail((1024, 1024) if kind == "picture" else (512, 512), Image.LANCZOS)
    out = io.BytesIO()
    image.save(out, "JPEG", quality=88, optimize=True)
    return out.getvalue()


def join_ends(image, band: int = 96):
    """
    A picture drawn as a panorama rarely wraps: where its right edge meets its left there is a
    line down the sky. Over the last `band` columns it is faded into a mirror image of the first
    ones, so the last column is the first and the seam is a soft reflection in place of a cut.
    """
    from PIL import Image
    width, height = image.size
    band = min(band, width // 4)
    mirrored = image.crop((0, 0, band, height)).transpose(Image.FLIP_LEFT_RIGHT)
    mask = Image.linear_gradient("L").rotate(90, expand=True).resize((band, height))   # black at the left, white at the right
    edge = image.crop((width - band, 0, width, height))
    image = image.copy()
    image.paste(Image.composite(mirrored, edge, mask), (width - band, 0))
    return image


def _folder() -> Path | None:
    return Path(config.GEN_CACHE) if config.GEN_CACHE else None


def _read(key: str) -> bytes | None:
    folder = _folder()
    if folder is None or not key.isalnum():
        return None
    path = folder / (key + ".jpg")
    try:
        return path.read_bytes() if path.is_file() else None
    except OSError:
        return None


def _write(key: str, data: bytes) -> None:
    folder = _folder()
    if folder is None:
        return
    try:
        folder.mkdir(parents=True, exist_ok=True)
        (folder / (key + ".jpg")).write_bytes(data)
    except OSError:
        log.warning("could not keep a generated picture in %s", folder)


def _sweep() -> None:
    cutoff = time.monotonic() - _KEEP_SECONDS
    for key in [k for k, (made, task) in _jobs.items() if made < cutoff and task.done()]:
        _jobs.pop(key, None)
