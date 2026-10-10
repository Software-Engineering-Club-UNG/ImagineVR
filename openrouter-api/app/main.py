import os

from fastapi import FastAPI

app = FastAPI(title="ImagineVR OpenRouter API", version="0.1.0")


@app.get("/")
def root() -> dict[str, str]:
    return {"service": "openrouter-api", "docs": "/docs"}


@app.get("/health")
def health() -> dict[str, str]:
    return {
        "status": "ok",
        "revision": os.environ.get("DEPLOYMENT_REVISION", "development"),
    }
