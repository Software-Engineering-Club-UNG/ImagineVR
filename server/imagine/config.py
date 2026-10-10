"""Server settings, read once from the environment (and server/.env if present)."""

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
MODEL = _env("IMAGINE_MODEL", "anthropic/claude-haiku-5.5")
REASONING = _env("IMAGINE_REASONING").lower()

STT = _env("IMAGINE_STT", "local").lower()
STT_MODEL = _env("IMAGINE_STT_MODEL", "base.en")

TTS = _env("IMAGINE_TTS", "off").lower() == "on"
ELEVENLABS_API_KEY = _env("ELEVENLABS_API_KEY")
TTS_VOICE = _env("IMAGINE_TTS_VOICE", "21m00Tcm4TlvDq8ikWAM")

APP_TOKEN = _env("IMAGINE_APP_TOKEN", "dev-token")
PUBLIC_URL = _env("IMAGINE_PUBLIC_URL").rstrip("/")

CATALOG_PATH = Path(_env("IMAGINE_CATALOG") or (REPO_DIR / "shared" / "catalog.json"))
PROMPT_PATH = Path(__file__).resolve().parent / "prompt.md"

MAX_AUDIO_SECONDS = 30
SESSION_IDLE_SECONDS = 30 * 60
TURNS_PER_MINUTE = 20

# True when there is no key: the built-in offline director answers every turn.
OFFLINE = not OPENROUTER_API_KEY or MODEL.lower() in ("offline", "fallback", "none")
