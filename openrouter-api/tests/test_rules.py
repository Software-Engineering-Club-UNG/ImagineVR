import copy
import pytest
from imagine.rules import validate_rule, RuleError, validate_snapshot, CAPABILITIES


def sheep_rule():
    return {'op': 'rule', 'id': 'sheep.hit', 'selector': {'asset': 'sheep'},
            'event': 'hit', 'filters': {'source_kind': 'projectile'}, 'mode': 'replace',
            'actions': [{'action': 'particles', 'preset': 'confetti', 'target': 'self'},
                        {'action': 'remove', 'target': 'self'}]}


def test_rule_and_atomic_replacement():
    rule = validate_rule(sheep_rule())
    assert rule['mode'] == 'replace'
    changed = copy.deepcopy(rule)
    changed['actions'] = [{'action': 'tint', 'color': '#0000FF', 'target': 'self'}]
    assert validate_rule(changed)['actions'][0]['action'] == 'tint'
    assert rule['actions'][0]['action'] == 'particles'


@pytest.mark.parametrize('mutation', [
    lambda r: r.update(event='execute_code'),
    lambda r: r.update(selector={'asset': 'sheep', 'group': 'flock'}),
    lambda r: r.update(actions=[{'action': 'remove', 'target': 'self', 'script': 'oops'}]),
    lambda r: r.update(actions=[{'action': 'tint', 'target': 'self'}]),
    lambda r: r.update(actions=[{'action': 'particles', 'preset': 'remote_url', 'target': 'self'}]),
    lambda r: r.update(actions=r['actions'] * 5),
    lambda r: r.update(cooldown=float('nan')),
    lambda r: r.update(conditions=[{'field': 'eval', 'operator': 'eq', 'value': 1}]),
])
def test_reject_invalid_rules(mutation):
    rule = sheep_rule()
    mutation(rule)
    with pytest.raises(RuleError):
        validate_rule(rule)


def test_snapshot_bounds_and_identity():
    snapshot = {'generation': 0, 'sequence': 1, 'entities': [], 'rules': [],
                'counters': {}, 'states': {}, 'player': [0, 1.6, 0], 'failures': []}
    assert validate_snapshot(snapshot)['sequence'] == 1
    snapshot['entities'] = [{'id': 'one', 'group': 'flock', 'asset': 'sheep',
                             'position': [0, 0, 2], 'tags': []}] * 2
    with pytest.raises(RuleError):
        validate_snapshot(snapshot)


def test_toolkit_is_shared_and_complete():
    assert {'hit', 'death', 'break', 'spawn', 'place', 'timer'} <= set(CAPABILITIES['events'])
    assert {'particles', 'remove', 'tint', 'teleport_player', 'counter_add'} <= set(CAPABILITIES['actions'])


def test_embedded_unity_contract_matches():
    import json
    from pathlib import Path
    path = Path(__file__).resolve().parents[2] / 'client/Assets/ImagineVR/Resources/RuntimeRules.json'
    assert json.loads(path.read_text()) == CAPABILITIES


def test_validator_updates_rules_atomically_and_gates_legacy():
    from imagine.main import load_files, state
    from imagine.script import Validator, WorldState
    load_files(force=True)
    world = WorldState()
    valid = sheep_rule()
    assert Validator(state.catalog).check(valid, world)[0]['op'] == 'rule'
    invalid = dict(valid, actions=[{'action': 'exec', 'target': 'self'}])
    assert Validator(state.catalog).check(invalid, world)[0]['op'] == 'note'
    assert world.rules['sheep.hit']['actions'][0]['preset'] == 'confetti'
    legacy = WorldState()
    assert Validator(state.catalog, allow_rules=False).check(valid, legacy)[0]['op'] == 'note'
    assert not legacy.rules


def test_snapshot_endpoint_auth_sequence_and_reset(monkeypatch):
    from fastapi.testclient import TestClient
    from imagine.main import app, state
    from imagine import config
    import uuid
    monkeypatch.setattr(config, 'APP_TOKEN', 'test-secret')
    client = TestClient(app)
    snapshot = {'generation': 0, 'sequence': 1, 'entities': [], 'rules': [],
                'counters': {'score': 5}, 'states': {}, 'player': [0, 1.6, 0], 'failures': []}
    headers = {'X-App-Token': 'test-secret', 'X-Session-Id': uuid.uuid4().hex}
    assert client.post('/v1/session/runtime', json=snapshot).status_code == 401
    assert client.post('/v1/session/runtime', headers=headers, json=snapshot).status_code == 200
    assert client.post('/v1/session/runtime', headers=headers, json=snapshot).status_code == 409
    assert state.sessions.get(headers['X-Session-Id']).world.runtime['counters']['score'] == 5
    response = client.post('/v1/session/reset', headers=headers)
    assert response.status_code == 204
    assert response.headers['x-session-generation'] == '1'
    assert client.post('/v1/session/runtime', headers=headers, json=snapshot).status_code == 409
    snapshot['generation'] = 1
    assert client.post('/v1/session/runtime', headers=headers, json=snapshot).status_code == 200


def test_all_actions_have_valid_examples():
    from imagine.rules import validate_action
    values = {'preset': 'confetti', 'color': '#0000FF', 'name': 'test', 'text': 'Hello',
              'value': 1, 'enabled': True, 'asset': 'sheep', 'position': [0, 0, 0],
              'rotation': [0, 0, 0], 'vector': [0, 1, 0], 'path': [[0, 0, 0], [0, 0, 5]],
              'behavior': 'wander', 'id': 'rule.one', 'duration': 1, 'intensity': 1,
              'density': .1, 'type': 'rain', 'loop': 'birds_breeze'}
    for action, schema in CAPABILITIES['actions'].items():
        example = {'action': action, **{key: values[key] for key in schema['required']}}
        if action == 'state_set': example['value'] = True
        validate_action(example)


def test_all_rule_examples_validate_without_substitution():
    import json
    from pathlib import Path
    from imagine.main import load_files, state
    from imagine.script import Validator, WorldState
    load_files(force=True)
    for path in (Path(__file__).resolve().parents[2] / 'shared/rule-examples').glob('*.ndjson'):
        world = WorldState()
        for line in path.read_text().splitlines():
            raw = json.loads(line)
            out = Validator(state.catalog).check(raw, world)
            assert any(stmt['op'] == raw['op'] for stmt in out), (path.name, raw, out)
            assert not any(stmt['op'] == 'note' for stmt in out), (path.name, out)


def test_clear_rules_and_bounded_runtime_prompt():
    from imagine.script import WorldState
    world = WorldState()
    world.apply(sheep_rule())
    assert 'sheep.hit' in world.render()
    world.apply({'op': 'clear', 'keep': ['rules']})
    assert 'sheep.hit' in world.rules
    world.apply({'op': 'clear'})
    assert not world.rules
    world.runtime = {'entities': [{'id': str(i), 'group': 'flock', 'asset': 'sheep'} for i in range(500)], 'rules': []}
    assert '"flock/sheep":500' in world.render()
    assert '"id":"499"' not in world.render()
