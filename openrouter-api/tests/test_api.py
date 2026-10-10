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
