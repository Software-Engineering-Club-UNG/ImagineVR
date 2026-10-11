from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def test_health_identifies_the_running_deployment(monkeypatch):
    monkeypatch.setenv("DEPLOYMENT_REVISION", "test-revision")
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok", "revision": "test-revision"}


def test_api_documentation_is_available():
    response = client.get("/openapi.json")
    assert response.status_code == 200
    assert "/health" in response.json()["paths"]


def test_missing_route_returns_404():
    assert client.get("/does-not-exist").status_code == 404


def test_an_app_from_before_the_rewrite_is_not_turned_away(monkeypatch):
    from imagine import config
    monkeypatch.setattr(config, "APP_TOKEN", "t")
    assert client.post("/v1/session/runtime", headers={"X-App-Token": "t"}, json={"generation": 3, "sequence": 9}).status_code == 200
    assert client.post("/v1/session/runtime", headers={"X-App-Token": "wrong"}, json={}).status_code == 401
