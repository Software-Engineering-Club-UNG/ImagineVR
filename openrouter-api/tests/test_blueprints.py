import copy
import json
from pathlib import Path

import pytest
from imagine.blueprints import validate_blueprint
from imagine.rules import RuleError
from imagine.catalog import Catalog
from imagine.script import Validator, WorldState

ROOT = Path(__file__).resolve().parents[2]


def recipe():
    return {'op':'blueprint','id':'new_tool','parts':[
        {'id':'handle','shape':'cylinder','scale':[.1,.3,.1],'collider':'box'},
        {'id':'blade','parent':'handle','shape':'capsule','position':[0,1,0],'color':'#0088ff'}],
        'capabilities':{'grabbable':True}}


def test_contract_copy():
    assert json.loads((ROOT/'shared/runtime-blueprints.json').read_text()) == json.loads((ROOT/'client/Assets/ImagineVR/Resources/RuntimeBlueprints.json').read_text())


@pytest.mark.parametrize('mutate',[
    lambda r:r.update(code='evil'),
    lambda r:r['parts'][0].update(parent='blade'),
    lambda r:r['parts'][1].update(parent='missing'),
    lambda r:r['parts'][1].update(id='handle'),
    lambda r:r['parts'][0].update(shape='execute_code'),
    lambda r:r['parts'][0].update(scale=[float('nan'),1,1]),
    lambda r:r.update(parts=r['parts']*40),
    lambda r:r.update(animations=[{'name':'bad','part':'missing','mode':'spin','property':'rotation','amplitude':[0,30,0]}]),
    lambda r:r.update(capabilities={'movement':{'mode':'fly_script'}}),
    lambda r:r.update(capabilities={'emitters':[{'name':'shot','socket':'missing','asset':'sphere'}]}),
    lambda r:r.update(sockets=[{'name':'bad','part':{}}]),
    lambda r:r.update(initial_state={}),
])
def test_reject_invalid(mutate):
    r=recipe(); mutate(r)
    with pytest.raises(RuleError): validate_blueprint(r)


def test_registry_atomic_and_legacy():
    cat=Catalog.load(ROOT/'shared/catalog.json'); world=WorldState()
    validator=Validator(cat,allow_blueprints=True)
    assert validator.check(recipe(),world)[-1]['op']=='blueprint'
    previous=copy.deepcopy(world.blueprints)
    bad=recipe(); bad['parts'][0]['shape']='eval'
    assert validator.check(bad,world)[-1]['op']=='note'
    assert world.blueprints==previous
    assert validator.check({'op':'give','asset':'new_tool','id':'tool'},world)[-1]['asset']=='new_tool'
    assert Validator(cat,allow_blueprints=False).check(recipe(),world)[-1]['op']=='note'


def test_removal_and_reset():
    cat=Catalog.load(ROOT/'shared/catalog.json'); world=WorldState(); v=Validator(cat,allow_blueprints=True)
    v.check(recipe(),world)
    assert 'new_tool' in world.render()
    v.check({'op':'blueprint_remove','id':'new_tool'},world)
    assert not world.blueprints
    v.check(recipe(),world); v.check({'op':'clear','everything':True},world)
    assert not world.blueprints


def test_all_examples_validate_in_dependency_order():
    cat=Catalog.load(ROOT/'shared/catalog.json'); world=WorldState(); v=Validator(cat,allow_blueprints=True)
    files=['lightsaber','dragon','dragon-hostile','dragon-friendly','turret','door','six-legged-creature']
    for name in files:
        for line in (ROOT/f'shared/blueprint-examples/{name}.ndjson').read_text().splitlines():
            raw=json.loads(line); checked=v.check(raw,world)
            assert checked and checked[-1]['op']==raw['op'], (name,raw,checked)


def test_blueprint_rule_capability_guard_and_generated_spawn():
    from imagine.rules import validate_rule
    cat=Catalog.load(ROOT/'shared/catalog.json'); world=WorldState(); v=Validator(cat,allow_blueprints=True)
    v.check(recipe(),world)
    r={'op':'rule','id':'spawn.tool','selector':{'asset':'flag'},'event':'interval','filters':{'duration':1},'actions':[{'action':'spawn','asset':'new_tool'}]}
    assert v.check(r,world)[-1]['op']=='rule'
    r['actions']=[{'action':'motion_config','config':{'mode':'chase','airborne':True}}]
    assert validate_rule(r)
    assert Validator(cat).check(r,world)[-1]['op']=='note'


def test_extended_snapshot_validation():
    from imagine.rules import validate_snapshot
    raw={'generation':0,'sequence':1,'entities':[{'id':'e1','group':'tool','asset':'new_tool','position':[0,0,0],'tags':[],'revision':1,'parts':['handle','blade'],'actor_state':'default'}], 'rules':[], 'counters':{}, 'states':{}, 'player':[0,1,0], 'failures':[], 'blueprints':[recipe()], 'overrides':{'e1':recipe()}, 'player_health':100,'player_damage_enabled':False}
    assert validate_snapshot(raw)
    raw['blueprints'][0]['parts'][0]['parent']='blade'
    with pytest.raises(RuleError): validate_snapshot(raw)


def test_blueprint_paths_are_world_stored_and_user_relative_in_prompt():
    cat=Catalog.load(ROOT/'shared/catalog.json'); world=WorldState(); frame=[10,20,90]; v=Validator(cat,frame,allow_blueprints=True)
    r=recipe(); r['capabilities']['movement']={'mode':'path','path':[[0,0,0],[0,0,5]]}
    assert v.check(r,world)[-1]['capabilities']['movement']['path'][1]==[15,0,20]
    remembered=json.loads(world.render(frame).splitlines()[0]); assert remembered['capabilities']['movement']['path'][1]==[0,0,5]


def test_blueprint_capability_turn_and_runtime_sync(monkeypatch):
    from fastapi.testclient import TestClient
    from app.main import app
    from imagine import config, llm, main, stt
    from imagine.session import SessionStore
    monkeypatch.setattr(config,'PRODUCTION',False); monkeypatch.setattr(config,'OFFLINE',False); monkeypatch.setattr(config,'APP_TOKEN','test-token'); monkeypatch.setattr(stt,'warm_up',lambda:None)
    main.state.sessions=SessionStore(); prompts=[]
    async def model(prompt,content,model=None):
        prompts.append(prompt)
        yield json.dumps(recipe()); yield json.dumps({'op':'give','asset':'new_tool','id':'tool'})
    monkeypatch.setattr(llm,'stream_lines',model)
    headers={'X-App-Token':'test-token','X-Session-Id':'bp-test','X-Client-Capabilities':'runtime-rules-v1,runtime-blueprints-v1'}
    with TestClient(app) as api:
        assert 'runtime-blueprints-v1' in api.get('/v1/health').json()['capabilities']
        response=api.post('/v1/turn',headers=headers,json={'text':'give me an unfamiliar tool'})
        data=[json.loads(l) for l in response.text.splitlines() if l.strip()]
        assert [s['op'] for s in data if s['op'] in ('blueprint','give')]==['blueprint','give']
        assert 'BLUEPRINT CAPABILITIES' in prompts[-1]
        snap={'generation':0,'sequence':1,'entities':[],'rules':[],'counters':{},'states':{},'player':[0,1,0],'failures':[],'blueprints':[recipe()]}
        assert api.post('/v1/session/runtime',headers=headers,json=snap).status_code==200
        assert 'new_tool' in main.state.sessions.get('bp-test').world.blueprints
        assert api.post('/v1/session/runtime',headers=headers,json=snap).status_code==409


def test_missing_give_gets_blueprint_retry_without_duplicate(monkeypatch):
    import asyncio
    from imagine import config,llm,turn
    from imagine.session import Session
    monkeypatch.setattr(config,'OFFLINE',False); calls=[]
    async def model(prompt,content,model=None):
        calls.append(content)
        if len(calls)==1: yield json.dumps({'op':'give','asset':'new_tool','id':'tool'})
        else:
            yield json.dumps(recipe()); yield json.dumps({'op':'give','asset':'new_tool','id':'tool'})
    monkeypatch.setattr(llm,'stream_lines',model)
    async def collect():
        return [s async for s in turn._run_turn(Session('bp-test'),Catalog.load(ROOT/'shared/catalog.json'),'runtime-blueprints-v1',text='give me an unfamiliar tool',allow_rules=True,allow_blueprints=True)]
    out=asyncio.run(collect())
    assert len(calls)==2
    assert sum(s['op']=='give' for s in out)==1
    assert any(s['op']=='blueprint' for s in out)


def test_actions_reject_malformed_kind():
    from imagine.rules import validate_action
    with pytest.raises(RuleError): validate_action({'action':{}})
