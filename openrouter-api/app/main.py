import os

from imagine.main import app



@app.get("/")
def root() -> dict[str, str]:
    return {"message": "Hello World"}


@app.get("/health")
def health() -> dict[str, str]:
    return {
        "status": "ok",
        "revision": os.environ.get("DEPLOYMENT_REVISION", "development"),
    }
