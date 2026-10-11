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
    bad=recipe(); bad['parts']=[]   # nothing to build: the one thing that cannot be mended
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


def _turn(monkeypatch, lines):
    import asyncio
    from imagine import config,llm,turn
    from imagine.session import Session
    monkeypatch.setattr(config,'OFFLINE',False)
    async def model(prompt,content,model=None):
        for line in lines: yield json.dumps(line)
    monkeypatch.setattr(llm,'stream_lines',model)
    async def collect():
        return [s async for s in turn._run_turn(Session('bp-test'),Catalog.load(ROOT/'shared/catalog.json'),'runtime-blueprints-v1',text='turn the grip round',allow_rules=True,allow_blueprints=True)]
    return asyncio.run(collect())


def test_edit_accepts_a_nested_recipe_without_its_op(monkeypatch):
    nested={k:v for k,v in recipe().items() if k!='op'}
    out=_turn(monkeypatch,[{'op':'blueprint_edit','selector':{'instance':'tool'},'blueprint':nested},{'op':'say','text':'Turned it.'}])
    edit=[s for s in out if s['op']=='blueprint_edit']
    assert len(edit)==1 and edit[0]['blueprint']['op']=='blueprint'
    assert [s['text'] for s in out if s['op']=='say']==['Turned it.']
    assert out[-1]=={'op':'done','outcome':'world_changed'}


def test_an_edit_that_names_its_target_by_id_is_understood(monkeypatch):
    out=_turn(monkeypatch,[{'op':'blueprint_edit','id':'tool','blueprint':recipe()},{'op':'say','text':"I've turned the grip."}])
    edit=[s for s in out if s['op']=='blueprint_edit']
    assert len(edit)==1 and edit[0]['selector']=={'instance':'tool'}
    assert out[-1]=={'op':'done','outcome':'world_changed'}


def test_a_rejected_change_is_not_reported_as_done(monkeypatch):
    from imagine import turn
    out=_turn(monkeypatch,[{'op':'blueprint_edit','selector':{'instance':'tool'},'blueprint':{'id':'new_tool','parts':[]}},{'op':'say','text':"I've turned the grip."},{'op':'say','text':'Hold it out again.'}])
    notes=[s['text'] for s in out if s['op']=='note']
    assert any('rejected' in n for n in notes)
    assert [s['text'] for s in out if s['op']=='say']==[turn.REFUSED]
    assert out[-1]=={'op':'done','outcome':'failed'}


def test_a_rejection_beside_a_real_change_keeps_the_reply(monkeypatch):
    out=_turn(monkeypatch,[{'op':'rule','id':'bad','event':'use_start'},recipe(),{'op':'give','asset':'new_tool','id':'tool'},{'op':'say','text':'Here it is.'}])
    assert [s['text'] for s in out if s['op']=='say']==['Here it is.']
    assert out[-1]['outcome']=='world_changed'


def test_the_prompt_example_of_a_trigger_fired_tool_is_valid():
    text=(ROOT/'openrouter-api/imagine/blueprints-prompt.md').read_text(encoding='utf-8')
    gun=json.loads(next(line for line in text.splitlines() if line.startswith('{"op":"blueprint","id":"flare_pistol"')))
    v=Validator(Catalog.load(ROOT/'shared/catalog.json'),allow_rules=True,allow_blueprints=True); w=WorldState()
    assert v.check(gun,w)[0]['op']=='blueprint'
    emitter=gun['capabilities']['emitters'][0]
    assert emitter['enabled'] is True and 'autofire' not in emitter


# ---- the 20:45 headset session: an AK-47 refused twice over one number, and delivered as a white box

def _checked(raw, rich=True):
    v=Validator(Catalog.load(ROOT/'shared/catalog.json'),allow_rules=True,allow_blueprints=True,rich_blueprints=rich); w=WorldState()
    return v.check(raw,w),v,w


def ak47():
    return {'op':'blueprint','id':'ak47','description':'AK-47 rifle','parts':[
        {'id':'receiver','shape':'box','position':[0,0,0],'scale':[.06,.09,.32],'color':'grey','collider':'box'},
        {'id':'barrel','shape':'cylinder','position':[0,.01,.36],'rotation':[90,0,0],'scale':[.025,.4,.025],'color':'#1E1E1E'},
        {'id':'stock','shape':'slab','outline':[[-.1,.05],[-.1,0],[-.38,-.03],[-.38,.05]],'thickness':.04,'color':'#7A5230'},
        {'id':'sight','shape':'cube','position':[0,.08,.5],'scale':.012,'weird':True}],
        'capabilities':{'grabbable':True,'grip':{'position':[0,-.05,-.05]},'emitters':[
            {'name':'shot','socket':'muzzle','asset':'bullet','target':'forward','speed':60,'cooldown':.1,'damage':5000,'enabled':True,'sound':'gunshot'}]}}


def test_a_recipe_with_numbers_out_of_range_is_mended_not_refused():
    out,_,world=_checked(ak47())
    assert [s['op'] for s in out]==['blueprint']
    made=out[0]; emitter=made['capabilities']['emitters'][0]
    assert emitter['speed']==40 and emitter['damage']==1000 and emitter['cooldown']==.1   # a machine gun's pace is allowed
    assert emitter['asset']=='energy_ball', 'a projectile that does not exist becomes one that does'
    assert emitter['socket'] in {s['name'] for s in made['sockets']}, 'a muzzle nobody defined is put at the far end'
    parts={p['id']:p for p in made['parts']}
    assert parts['receiver']['shape']=='cube' and parts['receiver']['color']=='#8B8D98'
    assert parts['sight']['scale']==[.012,.012,.012] and 'weird' not in parts['sight']
    assert parts[made['sockets'][0]['part']]['position'][2]>.5
    assert 'ak47' in world.blueprints


def test_a_held_gun_written_without_an_emitter_still_shoots():
    raw=ak47(); raw['capabilities'].pop('emitters')
    made=_checked(raw)[0][0]
    emitter=made['capabilities']['emitters'][0]
    assert emitter['enabled'] is True and emitter['target']=='forward' and 'autofire' not in emitter
    # but nothing is armed that was not plainly meant to be
    tool=recipe(); assert 'emitters' not in _checked(tool)[0][0].get('capabilities',{})


def test_a_blueprint_named_after_a_library_asset_is_kept_under_another_id():
    raw=ak47(); raw['id']='rifle'
    out,v,world=_checked(raw)
    assert out[0]['op']=='blueprint' and out[0]['id']=='rifle_made'
    given=v.check({'op':'give','asset':'rifle','id':'my_rifle'},world)
    assert given[-1]['asset']=='rifle_made'


def test_an_older_app_is_sent_a_recipe_in_the_words_it_knows():
    made=_checked(ak47(),rich=False)[0][0]
    stock=next(p for p in made['parts'] if p['id']=='stock')
    assert stock['shape']=='cube' and 'outline' not in stock and stock['scale']==[.04,.08,.28]
    emitter=made['capabilities']['emitters'][0]
    assert emitter['cooldown']==.25 and 'sound' not in emitter


def test_the_new_part_vocabulary_is_valid():
    raw={'op':'blueprint','id':'vase_bird','parts':[
        {'id':'vase','shape':'lathe','profile':[[.1,0],[.2,.2],[.05,.5],[0,.5]],'sides':12,'color':'#336699','color_top':'#88BBEE'},
        {'id':'leg','shape':'cylinder','anchor':'top','position':[.1,.5,0],'scale':[.05,.5,.05],'taper':1.5,'mirror':'x'},
        {'id':'toe','shape':'dome','parent':'leg','position':[0,-.5,.03],'scale':[.08,.03,.1]},
        {'id':'fleece','shape':'blob','rough':.3,'seed':4},
        {'id':'rivets','shape':'sphere','scale':[.02,.02,.02],'repeat':6,'step':[0,.05,0]},
        {'id':'guard','shape':'torus','radius':.06,'bevel':.5}],
        'animations':[{'name':'walk','part':'leg','mode':'oscillate','property':'rotation','amplitude':[20,0,0],'alternate':True}]}
    made=_checked(raw)[0][0]
    assert [p['shape'] for p in made['parts']]==['lathe','cylinder','dome','blob','sphere','torus']
    assert made['animations'][0]['alternate'] is True and made['parts'][1]['mirror']=='x'


def test_every_blueprint_in_the_prompt_is_valid_as_written():
    text=(ROOT/'openrouter-api/imagine/blueprints-prompt.md').read_text(encoding='utf-8')
    recipes=[json.loads(line) for line in text.splitlines() if line.startswith('{"op":"blueprint"')]
    assert len(recipes)>=2
    for raw in recipes:
        made=_checked(raw)[0][0]
        assert made['op']=='blueprint' and len(made['parts'])==len(raw['parts']), raw['id']
        for a,b in zip(raw['parts'],made['parts']): assert {k:v for k,v in b.items()}=={k:v for k,v in a.items()}, (raw['id'],a['id'])
        assert made.get('animations',[])==raw.get('animations',[]) and made.get('capabilities')==raw.get('capabilities')
        assert len(raw['parts'])>=14, 'the examples must show the depth they ask for'


def test_each_app_is_told_one_way_to_make_what_the_library_lacks():
    from imagine.main import for_client
    text=(ROOT/'openrouter-api/imagine/prompt.md').read_text(encoding='utf-8')
    new,old=for_client(text,True),for_client(text,False)
    assert '<!--' not in new and '<!--' not in old
    assert 'Sculpting from shapes' in old and 'Sculpting from shapes' not in new
    assert 'batman.legs' in old and 'batman.legs' not in new
    assert 'Making what the catalog lacks' in new and 'When they are not happy with it' in new


def test_what_cannot_be_built_falls_back_to_the_nearest_library_asset_not_a_white_box(monkeypatch):
    import asyncio
    from imagine import config,llm,turn
    from imagine.session import Session
    monkeypatch.setattr(config,'OFFLINE',False); calls=[]
    async def model(prompt,content,model=None):
        calls.append(content)
        if len(calls)==1: yield json.dumps({'op':'give','asset':'laser_rifle','id':'gun'})
        else: yield json.dumps({'op':'blueprint','id':'laser_rifle','parts':[]})
    monkeypatch.setattr(llm,'stream_lines',model)
    async def collect():
        return [s async for s in turn._run_turn(Session('bp-test'),Catalog.load(ROOT/'shared/catalog.json'),'runtime-blueprints-v1',text='give me a laser rifle',allow_rules=True,allow_blueprints=True)]
    out=asyncio.run(collect())
    give=[s for s in out if s['op']=='give']
    assert len(give)==1 and not give[0].get('stand_in') and give[0]['asset'] in ('rifle','blaster')
