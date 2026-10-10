"""Legacy import bridge; the only server implementation is openrouter-api/imagine.

Keeps `cd server; uv run uvicorn imagine.main:app` usable during the transition.
All submodules, the prompt, and the language version load from the canonical folder.
"""
from pathlib import Path

__path__ = [str(Path(__file__).resolve().parents[2] / "openrouter-api" / "imagine")]

from .version import LANGUAGE_VERSION
