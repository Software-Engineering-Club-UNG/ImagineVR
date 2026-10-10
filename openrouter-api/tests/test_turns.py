import asyncio
import io
import json
import wave

import httpx
import pytest
from fastapi.testclient import TestClient

from app.main import app
from imagine import config, llm, main, stt, turn
from imagine.catalog import Catalog
from imagine.script import Validator, WorldState
from imagine.session import Session, SessionStore
from scripts.smoke import CASES


@pytest.fixture
def api(monkeypatch):
    monkeypatch.setattr(config, 'PRODUCTION', False)
    monkeypatch.setattr(config, 'OFFLINE', True)
    monkeypatch.setattr(config, 'APP_TOKEN', 'test-token')
    monkeypatch.setattr(stt, 'warm_up', lambda: None)
    main.state.sessions = SessionStore()
    monkeypatch.setattr(llm, '_client', None)
    with TestClient(app) as client:
        yield client


HEADERS = {'X-App-Token': 'test-token', 'X-Session-Id': 'test-session'}


def wav(seconds=1, rate=16000):
    output = io.BytesIO()
    with wave.open(output, 'wb') as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(rate)
        audio.writeframes(b'\0\0' * int(seconds * rate))
    return output.getvalue()


def statements(response):
    return [json.loads(line) for line in response.text.splitlines() if line.strip()]


def test_unity_health_and_text_turn(api):
    assert api.get('/v1/health').json()['language_version'] == 1
    result = statements(api.post('/v1/turn', headers=HEADERS, json={'text': 'build a forest'}))
    assert result[0] == {'op': 'status', 'state': 'thinking'}
    assert result[-1]['op'] == 'done'
    assert sum(s['op'] == 'done' for s in result) == 1
    assert any(s['op'] in ('kit', 'scatter', 'spawn') for s in result)


def test_auth_and_bad_requests(api):
    assert api.post('/v1/turn', json={'text': 'forest'}).status_code == 401
    assert api.post('/v1/turn', headers={'X-App-Token': 'test-token'}, json={'text': 'forest'}).status_code == 400
    assert api.post('/v1/turn', headers=HEADERS, json={'text': ''}).status_code == 400
    assert api.post('/v1/turn', headers=HEADERS, content=b'invalid', ).status_code == 415


@pytest.mark.parametrize('data,status', [(b'bad', 400), (wav(rate=8000), 400), (wav(30.1), 413), (b'x' * 1_100_001, 413)], ids=['invalid', 'wrong-rate', 'too-long', 'oversized'])
def test_audio_validation(api, data, status):
    assert api.post('/v1/turn', headers={**HEADERS, 'Content-Type': 'audio/wav'}, content=data).status_code == status


def test_audio_turn_and_transcription_error(api, monkeypatch):
    async def transcribe(_):
        return 'build a forest'
    monkeypatch.setattr(stt, 'transcribe', transcribe)
    response = api.post('/v1/turn', headers={**HEADERS, 'Content-Type': 'audio/wav'}, content=wav())
    assert statements(response)[0] == {'op': 'transcript', 'text': 'build a forest'}
    async def fail(_):
        raise RuntimeError('test')
    monkeypatch.setattr(stt, 'transcribe', fail)
    result = statements(api.post('/v1/turn', headers={**HEADERS, 'Content-Type': 'audio/wav'}, content=wav()))
    assert [s['op'] for s in result] == ['transcript', 'error', 'done']
    assert result[-1]['outcome'] == 'failed'


def test_sessions_reset_and_rate_limit(api):
    api.post('/v1/turn', headers=HEADERS, json={'text': 'forest'})
    assert main.state.sessions.get('test-session').history
    assert not main.state.sessions.get('different').history
    assert api.post('/v1/session/reset', headers=HEADERS).status_code == 204
    assert not main.state.sessions.get('test-session').history
    session = main.state.sessions.get('limited')
    for _ in range(config.TURNS_PER_MINUTE):
        assert session.allow_turn()
    assert api.post('/v1/turn', headers={**HEADERS, 'X-Session-Id': 'limited'}, json={'text': 'forest'}).status_code == 429


def test_openrouter_fragmentation_and_failures(monkeypatch):
    async def check():
        for suffix, succeeds in [('stop', True), ('length', False), ('error', False), (None, False)]:
            frames = [': keepalive', 'data: ' + json.dumps({'choices': [{'delta': {'content': '{"op":"say",'}}]}),
                      'data: ' + json.dumps({'choices': [{'delta': {'content': '"text":"hi"}\n'}, 'finish_reason': suffix}]})]
            if suffix:
                frames += ['data: [DONE]']
            async def handler(request):
                assert json.loads(request.content)['stream'] is True
                return httpx.Response(200, text='\n\n'.join(frames))
            async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as client:
                monkeypatch.setattr(llm, '_client', client)
                if succeeds:
                    assert [line async for line in llm.stream_lines('system', 'user')] == ['{"op":"say","text":"hi"}']
                else:
                    with pytest.raises(llm.LLMError):
                        _ = [line async for line in llm.stream_lines('system', 'user')]
    asyncio.run(check())


def test_partial_provider_failure_keeps_world(monkeypatch):
    async def check():
        async def fail(*args):
            yield '{"op":"spawn","asset":"tree","id":"tree1"}'
            raise llm.LLMError('disconnected')
        monkeypatch.setattr(config, 'OFFLINE', False)
        monkeypatch.setattr(llm, 'stream_lines', fail)
        session = Session('partial')
        catalog = Catalog.load(config.CATALOG_PATH)
        result = [s async for s in turn.run_turn(session, catalog, 'system', text='tree')]
        assert result[-1] == {'op': 'done', 'outcome': 'failed'}
        assert 'tree1' in session.world.things
        assert sum(s['op'] == 'done' for s in result) == 1
    asyncio.run(check())


def test_catalog_limits_and_unknown_ops():
    catalog = Catalog.load(config.CATALOG_PATH)
    validator, world = Validator(catalog), WorldState()
    assert validator.check({'op': 'execute', 'code': 'danger'}, world) == []
    for i in range(8):
        validator.check({'op': 'scatter', 'asset': 'tree', 'id': f'trees{i}', 'count': 99999}, world)
    assert world.scatter_total() <= catalog.limits['max_scatter_total']


def test_production_rejects_missing_secrets(monkeypatch):
    monkeypatch.setattr(config, 'PRODUCTION', True)
    monkeypatch.setattr(config, 'OFFLINE', True)
    with pytest.raises(RuntimeError, match='Production requires'):
        with TestClient(app):
            pass


@pytest.mark.parametrize('conversation,description,check', CASES, ids=[case[1] for case in CASES])
def test_israels_smoke_scenarios_with_fallback(api, conversation, description, check):
    for text in conversation.split(' >> '):
        result = statements(api.post('/v1/turn', headers=HEADERS, json={'text': text}))
    assert check(result), description
    assert result[-1]['op'] == 'done'
    assert not any(s['op'] == 'error' for s in result)


def test_ignored_chatter_does_not_pollute_history(api):
    result = statements(api.post('/v1/turn', headers=HEADERS, json={'text': 'dude that car looked so real'}))
    assert result[-1] == {'op': 'done', 'outcome': 'ignored'}
    assert not main.state.sessions.get('test-session').history


def test_ride_and_creature_behavior_contract():
    catalog = Catalog.load(config.CATALOG_PATH)
    validator, world = Validator(catalog), WorldState()
    ride = validator.check({'op': 'ride', 'asset': 'car', 'speed': 999}, world)[-1]
    assert ride['asset'] == 'car' and ride['speed'] == 3
    assert world.globals['ride'] == ride
    creature = validator.check({'op': 'scatter', 'asset': 'zombie', 'id': 'enemies', 'behavior': 'chase', 'count': 4}, world)[-1]
    assert creature['behavior'] == 'chase'
    assert validator.check({'op': 'ride', 'asset': 'none'}, world)[-1]['asset'] == 'none'
    assert 'ride' not in world.globals
