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

# Spoken replies, narration and the voices of characters. "off" (default); "openrouter" speaks through the
# same key as everything else; "elevenlabs" needs ELEVENLABS_API_KEY; "local" runs Kokoro on this machine's CPU
# when kokoro-onnx and its model files are installed; "on" is the one of those that is available.
TTS_MODE = _env("IMAGINE_TTS", "off").lower()
TTS = TTS_MODE == "on"      # kept for callers from before there was a choice of voice
ELEVENLABS_API_KEY = _env("ELEVENLABS_API_KEY")
TTS_VOICE = _env("IMAGINE_TTS_VOICE", "21m00Tcm4TlvDq8ikWAM")
TTS_MODEL = _env("IMAGINE_TTS_MODEL", "openai/gpt-4o-mini-tts")
TTS_VOICES = _env("IMAGINE_TTS_VOICES")          # JSON: {"gruff": {"voice": "onyx", "instructions": "..."}, ...} over the built-in table
TTS_LOCAL_MODEL = _env("IMAGINE_TTS_LOCAL_MODEL")    # kokoro .onnx file
TTS_LOCAL_VOICES = _env("IMAGINE_TTS_LOCAL_VOICES")  # kokoro voices .bin file

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
PROMPTS_DIR = Path(__file__).resolve().parent / "prompts"


def playbook_path() -> Path:
    """Worked examples live beside the catalog, so they are deployed and overridden with it."""
    override = _env("IMAGINE_PLAYBOOK")
    return Path(override) if override else catalog_path().parent / "playbook"


def _int(name: str, default: int, lo: int, hi: int) -> int:
    try:
        return max(lo, min(hi, int(_env(name, str(default)) or default)))
    except ValueError:
        return default


# How much one request to build a place is worth. 0: one model call, as before the third version.
# 1: custom assets are modelled in detail by calls of their own while the place is built.
# 2 (default): and two more calls run beside them, one dressing the place and one making it something to play.
# 3: and each custom asset is modelled from a picture drawn of it first (needs IMAGINE_IMAGES=on).
DEPTH = _int("IMAGINE_DEPTH", 2, 0, 3)
CRAFTS = _int("IMAGINE_CRAFTS", 4, 0, 8)                 # custom assets modelled in detail per turn, at most
EXAMPLES = _int("IMAGINE_EXAMPLES", 2, 0, 3)             # worked examples shown to the model for a request like them
CRAFT_MODEL = _env("IMAGINE_CRAFT_MODEL") or MODEL       # who models assets
PASS_MODEL = _env("IMAGINE_PASS_MODEL") or MODEL         # who dresses places and writes their games
NPC_MODEL = _env("IMAGINE_NPC_MODEL") or MODEL           # who speaks for characters
TURN_SECONDS = float(_env("IMAGINE_TURN_SECONDS", "70")) # all of that must be done by then; what is not is left out

# Pictures made on the spot (a panorama for the sky, a painting, a poster), through OpenRouter's image API.
IMAGES = _env("IMAGINE_IMAGES", "off").lower() == "on"
IMAGE_MODEL = _env("IMAGINE_IMAGE_MODEL", "google/gemini-2.5-flash-image")
OPENROUTER_BASE = OPENROUTER_URL.split("/chat/completions")[0].rstrip("/")
GEN_CACHE = _env("IMAGINE_GEN_CACHE")                    # a folder to keep what was generated; memory only when unset

MAX_AUDIO_SECONDS = 30
# How long the model may stay silent before the request is given up on and tried again:
# before its first word, and between words once it has started.
FIRST_TOKEN_SECONDS = float(_env("IMAGINE_FIRST_TOKEN_SECONDS", "30"))
STALL_SECONDS = float(_env("IMAGINE_STALL_SECONDS", "15"))
SESSION_IDLE_SECONDS = 30 * 60
TURNS_PER_MINUTE = 20
EVENTS_PER_MINUTE = 6       # times a minute a world may ask the director what happens next
EVENT_GAP_SECONDS = 4.0

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
