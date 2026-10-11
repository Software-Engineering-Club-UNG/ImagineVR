"""Server settings, read once from the environment (and openrouter-api/.env)."""

from __future__ import annotations

import os
from pathlib import Path

from dotenv import load_dotenv

SERVER_DIR = Path(__file__).resolve().parent.parent
REPO_DIR = SERVER_DIR.parent

load_dotenv(SERVER_DIR / ".env")


def _env(name: str, default: str = "") -> str:
    return os.environ.get(name, default).strip()


OPENROUTER_API_KEY = _env("OPENROUTER_API_KEY")
OPENROUTER_URL = _env("OPENROUTER_URL") or "https://openrouter.ai/api/v1/chat/completions"
MODEL = _env("IMAGINE_MODEL", "anthropic/claude-haiku-5.5")
# Whether the model thinks to itself before it writes. "off" by default: left to itself it spent
# 18 seconds in silence before the first line of a Batman, and over 30 on a helicopter, which the
# server took for a dead connection. The user is standing in a headset watching a terminal, and
# what they are waiting for is the first line. "low", "medium", "high" ask for thought; "auto"
# leaves it to the provider.
REASONING = _env("IMAGINE_REASONING", "off").lower()

STT = _env("IMAGINE_STT", "local").lower()
STT_MODEL = _env("IMAGINE_STT_MODEL", "base.en")
# How many readings of the audio Whisper keeps in play. 5 hears a noisy room about as well as
# the next model up does at 1, for a fraction of the time that model costs; 1 is the fastest.
STT_BEAM = max(1, min(8, int(_env("IMAGINE_STT_BEAM", "5") or 5)))

TTS = _env("IMAGINE_TTS", "off").lower() == "on"
ELEVENLABS_API_KEY = _env("ELEVENLABS_API_KEY")
TTS_VOICE = _env("IMAGINE_TTS_VOICE", "21m00Tcm4TlvDq8ikWAM")

APP_TOKEN = _env("IMAGINE_APP_TOKEN", "dev-token")
PUBLIC_URL = _env("IMAGINE_PUBLIC_URL").rstrip("/")

def catalog_path(server_dir: Path = SERVER_DIR) -> Path:
    """Resolve an explicit override, bundled release catalog, or repository catalog."""
    override = _env("IMAGINE_CATALOG")
    if override:
        return Path(override)
    bundled = server_dir / "shared" / "catalog.json"
    return bundled if bundled.is_file() else server_dir.parent / "shared" / "catalog.json"


CATALOG_PATH = catalog_path()
PROMPT_PATH = Path(__file__).resolve().parent / "prompt.md"

MAX_AUDIO_SECONDS = 30
# How long the model may stay silent before the request is given up on and tried again:
# before its first word, and between words once it has started.
FIRST_TOKEN_SECONDS = float(_env("IMAGINE_FIRST_TOKEN_SECONDS", "30"))
STALL_SECONDS = float(_env("IMAGINE_STALL_SECONDS", "15"))
SESSION_IDLE_SECONDS = 30 * 60
TURNS_PER_MINUTE = 20

# True when there is no key: turns answer that no model is connected. There is no offline director.
OFFLINE = not OPENROUTER_API_KEY or MODEL.lower() in ("offline", "fallback", "none")
PRODUCTION = _env("IMAGINE_ENV") == "production"
STT_CACHE = _env("IMAGINE_STT_CACHE") or None


def stt_model_path(cache: str | None = STT_CACHE, model: str = STT_MODEL) -> str | None:
    """Weights already on disk for the chosen model, so that changing IMAGINE_STT_MODEL is the whole switch."""
    explicit = _env("IMAGINE_STT_MODEL_PATH")
    if explicit:
        return explicit
    prepared = Path(cache) / f"whisper-{model}" if cache else None
    return str(prepared) if prepared and (prepared / "model.bin").is_file() else None


STT_MODEL_PATH = stt_model_path()
