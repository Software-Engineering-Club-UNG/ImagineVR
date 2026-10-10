import asyncio
import io
import json
import wave
import subprocess
import sys
from pathlib import Path

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
        for suffix, has_done, succeeds in [('stop', True, True), ('length', True, False),
            ('error', True, False), ('content_filter', True, False), (None, False, False),
            (None, True, True), ('provider_specific', True, True)]:
            frames = [': keepalive', 'data: ' + json.dumps({'choices': [{'delta': {'content': '{"op":"say",'}}]}),
                      'data: ' + json.dumps({'choices': [{'delta': {'content': '"text":"hi"}\n'}, 'finish_reason': suffix}]})]
            if has_done:
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


def test_plan_line_stream_order_and_state(api, monkeypatch):
    async def model(*args):
        yield '{"op":"think","text":"A maze at sunset, then a flag marking the exit."}'
        yield '{"op":"kit","id":"maze1","kit":"maze"}'
        yield '{"op":"say","text":"Find your way out."}'
    monkeypatch.setattr(config, 'OFFLINE', False)
    monkeypatch.setattr(llm, 'stream_lines', model)
    result = statements(api.post('/v1/turn', headers=HEADERS, json={'text': 'make a maze'}))
    assert [s['op'] for s in result] == ['status', 'think', 'status', 'kit', 'say', 'done']
    assert result[2] == {'op': 'status', 'state': 'building'}
    assert result[-1]['outcome'] == 'world_changed'
    session = main.state.sessions.get('test-session')
    assert 'think' not in session.world.render()
    assert list(session.history) == [('make a maze', 'Find your way out.')]


def test_plan_only_is_not_a_world_change(api, monkeypatch):
    async def model(*args):
        yield '{"op":"think","text":"Sketching the route."}'
    monkeypatch.setattr(config, 'OFFLINE', False)
    monkeypatch.setattr(llm, 'stream_lines', model)
    result = statements(api.post('/v1/turn', headers=HEADERS, json={'text': 'a route'}))
    assert result[-1] == {'op': 'done', 'outcome': 'answered'}
    assert not any(s.get('state') == 'building' for s in result)
    assert main.state.sessions.get('test-session').world.is_empty()


def test_plan_validation():
    validator = Validator(Catalog.load(config.CATALOG_PATH))
    world = WorldState()
    assert validator.check({'op': 'think'}, world) == []
    assert validator.check({'op': 'think', 'text': 'x' * 500, 'id': 'discard'}, world) == [{'op': 'think', 'text': 'x' * 220}]
    assert world.is_empty()


def test_routes_and_solidity_round_trip():
    catalog = Catalog.load(config.CATALOG_PATH)
    validator, world = Validator(catalog), WorldState()
    path = [[0, 1, 0], [10, 6, 20], [20, 1, 30]]
    spawn = validator.check({'op': 'spawn', 'id': 'traffic', 'asset': 'car', 'path': path,
                             'speed': 12, 'loop': False, 'start': 0.5}, world)[-1]
    assert {field: spawn[field] for field in ('path', 'speed', 'loop', 'start')} == {
        'path': path, 'speed': 12, 'loop': False, 'start': 0.5}
    ride = validator.check({'op': 'ride', 'asset': 'car', 'path': path, 'loop': False}, world)[-1]
    assert ride['path'] == path and ride['loop'] is False
    wall = validator.check({'op': 'spawn', 'id': 'wall', 'asset': 'cube', 'scale': [12, 3, 0.3], 'solid': True}, world)[-1]
    assert wall['solid'] is True and wall['scale'] == [12, 3, 0.3]
    barriers = validator.check({'op': 'scatter', 'id': 'barriers', 'asset': 'crate', 'count': 4, 'solid': True}, world)[-1]
    assert barriers['solid'] is True
    rendered = [json.loads(line) for line in world.render().splitlines()]
    assert any(s.get('id') == 'traffic' and s['path'] == path for s in rendered)
    assert any(s['op'] == 'ride' and s['path'] == path for s in rendered)


@pytest.mark.parametrize('path,expected_length', [([], None), ([[1, 2, 3]], None),
    ([[0, 0, 0], 'invalid', [1, 1, 1]], 2), ([[i, 0, i] for i in range(30)], 24)])
def test_route_validation_and_point_limit(path, expected_length):
    validator, world = Validator(Catalog.load(config.CATALOG_PATH)), WorldState()
    out = validator.check({'op': 'ride', 'asset': 'car', 'path': path, 'speed': 100}, world)[-1]
    assert out['speed'] == 3
    if expected_length is None:
        assert 'path' not in out
    else:
        assert len(out['path']) == expected_length


def test_goals_player_size_and_followup_state():
    validator, world = Validator(Catalog.load(config.CATALOG_PATH)), WorldState()
    assert validator.check({'op': 'goal', 'text': 'Missing position'}, world) == []
    goal = validator.check({'op': 'goal', 'at': [0, 0, 40], 'radius': 999,
                            'for': 'user', 'text': 'You made it'}, world)[-1]
    assert goal['radius'] == 20 and goal['id'] in world.things
    assert validator.check({'op': 'player'}, world) == []
    assert validator.check({'op': 'player', 'scale': 999}, world) == [{'op': 'player', 'scale': 20}]
    assert '"op":"player"' in world.render()
    assert '"op":"goal"' in world.render()
    validator.check({'op': 'remove', 'id': goal['id']}, world)
    assert goal['id'] not in world.things
    validator.check({'op': 'player', 'scale': 1}, world)
    assert 'player' not in world.globals


def test_group_scale_and_extended_sky_transition():
    validator, world = Validator(Catalog.load(config.CATALOG_PATH)), WorldState()
    validator.check({'op': 'spawn', 'id': 'tree', 'asset': 'pine_tree'}, world)
    out = validator.check({'op': 'modify', 'id': 'tree', 'scale': [2, 4, 6]}, world)[-1]
    assert out['scale'] == 4 and world.things['tree']['scale'] == 4
    assert validator.check({'op': 'sky', 'preset': 'golden_hour', 't': 90}, world)[-1]['t'] == 90
    assert validator.check({'op': 'sky', 'preset': 'golden_hour', 't': 999}, world)[-1]['t'] == 120


@pytest.mark.parametrize('example', ['31-maze.ndjson', '32-coaster.ndjson'])
def test_new_unity_examples_are_validated_without_losing_commands(example):
    catalog = Catalog.load(config.CATALOG_PATH)
    validator, world = Validator(catalog), WorldState()
    source = config.CATALOG_PATH.parent / 'examples' / example
    for line in source.read_text(encoding='utf-8').splitlines():
        if not line.strip() or line.startswith('#'):
            continue
        raw = json.loads(line)
        out = validator.check(raw, world)
        assert out and out[-1]['op'] == raw['op'], raw
        for field in ('path', 'loop', 'speed'):
            if field in raw:
                assert out[-1][field] == raw[field]


def test_backend_and_unity_catalog_versions_match():
    unity_version = Path('../client/Assets/ImagineVR/Resources/CatalogVersion.txt').read_text().strip()
    assert Catalog.load(config.CATALOG_PATH).version == unity_version


def test_provider_diagnostics_redact_credentials(monkeypatch):
    monkeypatch.setattr(config, 'OPENROUTER_API_KEY', 'secret-test-key')
    detail = llm.error_detail(json.dumps({'error': {'message': 'No credit\nsecret-test-key Bearer token-123 ' + 'x' * 300}}))
    assert 'No credit' in detail
    assert 'secret-test-key' not in detail and 'token-123' not in detail
    assert '\n' not in detail and len(detail) <= 200


def test_non_200_includes_actionable_diagnostics(monkeypatch):
    async def check():
        async def handler(request):
            return httpx.Response(402, json={'error': {'message': 'Insufficient credits'}})
        async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as client:
            monkeypatch.setattr(llm, '_client', client)
            with pytest.raises(llm.LLMError, match='HTTP 402: Insufficient credits'):
                _ = [line async for line in llm.stream_lines('system', 'user')]
    asyncio.run(check())


@pytest.mark.parametrize('provider,expected_parallel', [('local', 1)])
def test_transcription_concurrency(monkeypatch, provider, expected_parallel):
    async def check():
        monkeypatch.setattr(config, 'STT', provider)
        monkeypatch.setattr(config, 'OFFLINE', True)
        turn.reset_stt_slots()
        release, ready = asyncio.Event(), asyncio.Event()
        active = maximum = 0
        async def transcribe(_):
            nonlocal active, maximum
            active += 1
            maximum = max(maximum, active)
            if active == expected_parallel:
                ready.set()
            await release.wait()
            active -= 1
            return 'forest'
        monkeypatch.setattr(stt, 'transcribe', transcribe)
        catalog = Catalog.load(config.CATALOG_PATH)
        async def run(i):
            return [s async for s in turn.run_turn(Session(str(i)), catalog, 'system', audio=wav())]
        tasks = [asyncio.create_task(run(i)) for i in range(5)]
        try:
            await asyncio.wait_for(ready.wait(), timeout=2)
            assert active == expected_parallel
        finally:
            release.set()
            results = await asyncio.gather(*tasks)
        assert maximum == expected_parallel
        assert all(result[-1]['op'] == 'done' for result in results)
    asyncio.run(check())


def test_legacy_entrypoint_loads_the_canonical_backend():
    result = subprocess.run([sys.executable, '-c',
        'import imagine.main, imagine.script; print(imagine.main.__file__); print(imagine.script.__file__)'],
        cwd=Path(__file__).resolve().parents[2] / 'server', capture_output=True, text=True, check=True)
    for line in result.stdout.splitlines():
        assert Path(line).parent == Path(__file__).resolve().parents[1] / 'imagine'


# ---------------------------------------------------------------- things that did not appear

def _catalog():
    return Catalog.load(config.CATALOG_PATH)


@pytest.mark.parametrize('wanted,expected', [
    ('spider_man', 'person'), ('bat_man', 'person'), ('cat_woman', 'woman'),   # the thing, not the describer
    ('fire_truck', None), ('monster_truck', None), ('batman', None),          # nothing like it: sculpt
    ('red_car', 'car'), ('big_oak_tree', 'oak_tree'), ('zombies', 'zombie'), ('pistol', 'blaster'),
])
def test_an_asset_is_never_swapped_for_something_that_only_shares_a_word(wanted, expected):
    assert _catalog().resolve('asset', wanted)[0] == expected


def test_things_are_placed_around_the_user_wherever_they_stand():
    frame = [30.0, -12.0, 90.0]   # 30 m right of the middle, 12 m back, facing +X
    validator, world = Validator(_catalog(), frame), WorldState()
    spawn = validator.check({'op': 'spawn', 'id': 'oak', 'asset': 'oak_tree', 'at': [0, 0, 4]}, world)[-1]
    assert spawn['frame'] == frame and spawn['at'] == [0, 0, 4]
    assert validator.check({'op': 'scatter', 'id': 'herd', 'asset': 'sheep', 'count': 4}, world)[-1]['frame'] == frame
    assert validator.check({'op': 'goal', 'at': [0, 0, 9]}, world)[-1]['frame'] == frame
    assert 'frame' not in validator.check({'op': 'sky', 'preset': 'morning'}, world)[-1]
    assert 'frame' not in validator.check({'op': 'kit', 'id': 'town', 'kit': 'city'}, world)[-1]
    assert 'frame' not in validator.check({'op': 'ride', 'asset': 'car'}, world)[-1]

    seen = {s.get('id'): s for s in map(json.loads, world.render(frame).splitlines())}
    assert seen['oak']['at'] == [0, 0, 4] and 'frame' not in seen['oak']
    # the user turns round and walks: the same oak is now described from the new spot
    moved = {s.get('id'): s for s in map(json.loads, world.render([30.0, -12.0, -90.0]).splitlines())}
    assert moved['oak']['at'] == [0, 0, -4]
    home = {s.get('id'): s for s in map(json.loads, world.render(None).splitlines())}
    assert home['oak']['at'] == [34, 0, -12]

    # `clear` brings the user back to the middle, so what follows is built there
    validator.check({'op': 'clear'}, world)
    assert 'frame' not in validator.check({'op': 'spawn', 'asset': 'oak_tree'}, world)[-1]


def test_a_spawn_by_place_is_remembered_where_it_really_went():
    validator, world = Validator(_catalog(), [10.0, 0.0, 0.0]), WorldState()
    validator.check({'op': 'spawn', 'id': 'car', 'asset': 'car', 'place': 'left', 'distance': 6}, world)
    seen = json.loads(world.render(None))
    assert seen['at'] == [4, 0, 0] and 'place' not in seen and 'distance' not in seen


@pytest.mark.parametrize('pose,frame', [('30,-12,90', [30, -12, 90]), ('0,0,0', None), ('home', None),
                                        ('1,2', None), ('nan,0,0', None), ('9999,0,0', None), ('0,5,-270', [0, 5, 90])])
def test_user_pose_header(pose, frame):
    from imagine.script import to_frame
    assert to_frame(pose) == frame


def test_sculpture_pieces_count_as_one_object():
    catalog = _catalog()
    validator, world = Validator(catalog), WorldState()
    for i in range(catalog.limits['max_spawned'] - 1):
        validator.check({'op': 'spawn', 'id': f'robot.part{i}', 'asset': 'cube'}, world)
    assert world.count('spawn') == 1
    assert validator.check({'op': 'spawn', 'id': 'tree', 'asset': 'oak_tree'}, world)[-1]['op'] == 'spawn'


def test_an_unknown_asset_is_marked_as_a_stand_in():
    validator, world = Validator(_catalog()), WorldState()
    note, spawn = validator.check({'op': 'spawn', 'id': 'hero', 'asset': 'batman'}, world)
    assert note['op'] == 'note' and spawn['asset'] == 'batman' and spawn['stand_in'] is True
    assert '"stand_in":true' in world.render()


def test_something_not_in_the_library_is_sculpted_instead_of_claimed(api, monkeypatch):
    calls = []

    async def model(system, content, *args):
        calls.append(content)
        if len(calls) == 1:
            yield '{"op":"spawn","id":"batman","asset":"batman","place":"front"}'
            yield '{"op":"say","text":"Batman is here."}'
        else:
            assert 'NOT in the CATALOG' in content and '"asset":"batman"' in content
            yield '{"op":"spawn","id":"batman.body","asset":"cube","at":[0,0,4],"tint":"#111111"}'
            yield '{"op":"spawn","id":"batman.head","asset":"sphere","at":[0,1.5,4],"scale":0.3}'
            yield '{"op":"say","text":"not wanted"}'
    monkeypatch.setattr(config, 'OFFLINE', False)
    monkeypatch.setattr(llm, 'stream_lines', model)
    result = statements(api.post('/v1/turn', headers={**HEADERS, 'X-User-Pose': '20,5,180'}, json={'text': 'make batman appear'}))
    assert [s['op'] for s in result] == ['status', 'note', 'status', 'spawn', 'spawn', 'say', 'done']
    assert [s['asset'] for s in result if s['op'] == 'spawn'] == ['cube', 'sphere']
    assert all(s['frame'] == [20, 5, 180] for s in result if s['op'] == 'spawn')
    assert result[-2]['text'] == 'Batman is here.' and result[-1]['outcome'] == 'world_changed'
    world = main.state.sessions.get('test-session').world
    assert set(world.things) == {'batman.body', 'batman.head'} and world.count('spawn') == 1


def test_a_failed_sculpt_still_leaves_a_marked_stand_in(api, monkeypatch):
    calls = []

    async def model(*args):
        calls.append(1)
        if len(calls) > 1:
            raise llm.LLMError('provider went away')
        yield '{"op":"spawn","id":"hero","asset":"batman"}'
        yield '{"op":"say","text":"Here he is."}'
    monkeypatch.setattr(config, 'OFFLINE', False)
    monkeypatch.setattr(llm, 'stream_lines', model)
    result = statements(api.post('/v1/turn', headers=HEADERS, json={'text': 'make batman appear'}))
    spawn = next(s for s in result if s['op'] == 'spawn')
    assert spawn['asset'] == 'batman' and spawn['stand_in'] is True
    assert [s['op'] for s in result][-2:] == ['say', 'done'] and result[-1]['outcome'] == 'world_changed'


def test_clear_everything_forgets_the_whole_world_including_what_was_given():
    validator, world = Validator(_catalog()), WorldState()
    for raw in ({'op': 'sky', 'preset': 'morning'}, {'op': 'give', 'id': 'club', 'asset': 'golf_club'},
                {'op': 'ride', 'asset': 'car'}, {'op': 'player', 'scale': 3}):
        validator.check(raw, world)
    validator.check({'op': 'clear'}, world)
    assert 'club' in world.things and 'ride' in world.globals
    assert validator.check({'op': 'clear', 'everything': True}, world) == [{'op': 'clear', 'everything': True}]
    assert world.is_empty()
