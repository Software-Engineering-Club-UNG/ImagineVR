# Legacy server entry point

The canonical backend is now `openrouter-api/imagine/`. Edit its prompt, validator,
and application modules directly; there is no second implementation under `server/`.

`server/imagine/__init__.py` is only an import bridge for old launch commands.
Environment settings now come from `openrouter-api/.env` or process environment.
Prefer the Python 3.14 environment and instructions in `openrouter-api/README.md`.
The old `server/pyproject.toml` remains for developers using its optional local
Whisper environment; production uses hosted ElevenLabs transcription.
