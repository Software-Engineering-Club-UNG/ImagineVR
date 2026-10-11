"""Bounded procedural recipes. All model output is data, never executable code."""
from __future__ import annotations

import copy
import json
from . import config
from .rules import RuleError, strict, identifier, number, vector, selector

_bundled = config.SERVER_DIR / 'shared' / 'runtime-blueprints.json'
CONTRACT = json.loads((_bundled if _bundled.is_file() else config.REPO_DIR / 'shared' / 'runtime-blueprints.json').read_text(encoding='utf-8'))
CAPABILITY = CONTRACT['capability']


def boolean(v):
    if not isinstance(v, bool): raise RuleError('expected boolean')


def color(v):
    import re
    if not isinstance(v,str) or not re.fullmatch(r'#[0-9a-fA-F]{6}',v): raise RuleError('expected hex color')


def strings(v, maximum=16):
    if not isinstance(v,list) or len(v)>maximum: raise RuleError('identifier list limit')
    for name in v: identifier(name)


def movement(v):
    strict(v,('mode','airborne','speed','turn_speed','radius','altitude','target','path'),('mode',))
    if v['mode'] not in CONTRACT['movement_modes']: raise RuleError('unknown movement mode')
    if 'airborne' in v: boolean(v['airborne'])
    for k,limits in {'speed':(0,15),'turn_speed':(1,360),'radius':(.5,40),'altitude':(.5,40)}.items():
        if k in v: number(v[k],*limits)
    if 'target' in v and v['target']!='player': selector(v['target'])
    if v['mode']=='path' and 'path' not in v: raise RuleError('path required')
    if 'path' in v:
        if not isinstance(v['path'],list) or not 2<=len(v['path'])<=24: raise RuleError('invalid movement path')
        for point in v['path']: vector(point)


def validate_blueprint(raw, catalog=None):
    strict(raw,('op','id','revision','parts','sockets','animations','capabilities','states','initial_state','description'),('op','id','parts'))
    if raw['op']!='blueprint': raise RuleError('expected blueprint')
    identifier(raw['id'])
    if 'revision' in raw:
        number(raw['revision'],1,2**31-1)
        if not isinstance(raw['revision'],int): raise RuleError('integer revision required')
    if 'description' in raw and (not isinstance(raw['description'],str) or len(raw['description'])>200): raise RuleError('description limit')
    parts=raw['parts']
    if not isinstance(parts,list) or not 1<=len(parts)<=64: raise RuleError('part limit')
    by_id={}; triangles=0; colliders=0
    for part in parts:
        strict(part,('id','parent','shape','asset','position','rotation','scale','color','emission','collider','visible','points','radius','vertices','triangles'),('id','shape'))
        identifier(part['id'])
        if part['id'] in by_id: raise RuleError('duplicate part')
        by_id[part['id']]=part
        if part['shape'] not in CONTRACT['shapes']: raise RuleError('unknown shape')
        if 'parent' in part: identifier(part['parent'])
        for key in ('position','rotation','scale'):
            if key in part:
                vector(part[key],720 if key=='rotation' else 20)
                if key=='scale':
                    for n in part[key]: number(n,.01,20)
        if 'color' in part: color(part['color'])
        if 'emission' in part: number(part['emission'],0,3)
        if 'visible' in part: boolean(part['visible'])
        if 'collider' in part:
            if part['collider'] not in ('box','sphere','capsule'): raise RuleError('invalid collider')
            colliders+=1
        cost={'cube':12,'plank':12,'sphere':80,'cylinder':32,'cone':16,'capsule':80,'torus':144,'disc':24,'wedge':12,'pyramid':6}.get(part['shape'],128)
        if part['shape']=='asset':
            identifier(part.get('asset'))
            if catalog is not None and not catalog.has('asset',part['asset']): raise RuleError('unknown baked asset')
            cost=512  # final baked mesh triangle count is enforced by Unity before activation
        if part['shape']=='tube':
            points=part.get('points')
            if not isinstance(points,list) or not 2<=len(points)<=16: raise RuleError('tube requires 2-16 points')
            for p in points: vector(p,20)
            number(part.get('radius',.1),.01,3); cost=len(points)*24
        if part['shape']=='surface':
            vertices=part.get('vertices'); indices=part.get('triangles')
            if not isinstance(vertices,list) or not 3<=len(vertices)<=24: raise RuleError('surface vertex limit')
            for v in vertices: vector(v,20)
            if not isinstance(indices,list) or not 3<=len(indices)<=132 or len(indices)%3: raise RuleError('surface triangle list')
            for n in indices:
                if not isinstance(n,int) or isinstance(n,bool) or not 0<=n<len(vertices): raise RuleError('invalid triangle index')
            cost=len(indices)//3*2
        triangles+=cost
    if triangles>8000 or colliders>16: raise RuleError('geometry/collider budget exceeded')
    for part in parts:
        seen=set(); cursor=part; depth=0
        while cursor:
            if cursor['id'] in seen: raise RuleError('cyclic parts')
            seen.add(cursor['id']); depth+=1
            if depth>8: raise RuleError('hierarchy depth exceeded')
            parent=cursor.get('parent')
            if parent and parent not in by_id: raise RuleError('unknown parent')
            cursor=by_id.get(parent)

    def named(items,maximum,allowed,required):
        if not isinstance(items,list) or len(items)>maximum: raise RuleError('component limit')
        names=set()
        for item in items:
            strict(item,allowed,required); identifier(item['name'])
            if item['name'] in names: raise RuleError('duplicate component name')
            names.add(item['name'])
        return names

    sockets=raw.get('sockets',[])
    socket_names=named(sockets,16,('name','part','position','rotation'),('name','part'))
    for s in sockets:
        identifier(s['part'])
        if s['part'] not in by_id: raise RuleError('unknown socket part')
        for key in ('position','rotation'):
            if key in s: vector(s[key],720 if key=='rotation' else 20)
    animations=raw.get('animations',[])
    animation_names=named(animations,16,('name','part','mode','property','amplitude','frequency','phase','duration','loop','enabled','frames'),('name','part','mode','property'))
    for a in animations:
        identifier(a['part'])
        if a['part'] not in by_id: raise RuleError('unknown animation part')
        if a['mode'] not in CONTRACT['animation_modes'] or a['property'] not in ('position','rotation','scale'): raise RuleError('unknown animation')
        if a['mode']=='spin' and a['property']!='rotation': raise RuleError('spin requires rotation')
        if a['mode']!='keyframes' and 'amplitude' not in a: raise RuleError('animation amplitude required')
        if 'amplitude' in a: vector(a['amplitude'],360 if a['property']=='rotation' else 20)
        for key,bounds in {'frequency':(.05,10),'phase':(-360,360),'duration':(.1,60)}.items():
            if key in a: number(a[key],*bounds)
        for key in ('loop','enabled'):
            if key in a: boolean(a[key])
        if a['mode']=='keyframes':
            frames=a.get('frames')
            if not isinstance(frames,list) or not 2<=len(frames)<=16: raise RuleError('keyframe limit')
            previous=-1
            for frame in frames:
                strict(frame,('time','value'),('time','value')); number(frame['time'],0,60)
                if frame['time']<=previous: raise RuleError('unordered keyframes')
                previous=frame['time']; vector(frame['value'],720 if a['property']=='rotation' else 20)
            if frames[0]['time']!=0 or frames[-1]['time']<=0: raise RuleError('keyframes must start at zero')
    if len({(a['part'],a['property']) for a in animations})!=len(animations): raise RuleError('animation property has multiple owners')
    caps=raw.get('capabilities',{})
    strict(caps,('grabbable','physics','mass','gravity','health','durability','grip','movement','contacts','emitters'))
    for key in ('grabbable','physics'):
        if key in caps: boolean(caps[key])
    for key,bounds in {'mass':(.02,100),'gravity':(-.5,2),'health':(0,10000),'durability':(0,10000)}.items():
        if key in caps: number(caps[key],*bounds)
    if 'grip' in caps:
        strict(caps['grip'],('position','rotation'))
        for key,v in caps['grip'].items(): vector(v,720 if key=='rotation' else 20)
    if 'movement' in caps: movement(caps['movement'])
    contacts=caps.get('contacts',[])
    contact_names=named(contacts,8,('name','part','damage','cooldown','target','enabled'),('name','part','damage'))
    for c in contacts:
        identifier(c['part'])
        if c['part'] not in by_id: raise RuleError('unknown contact part')
        number(c['damage'],0,1000); number(c.get('cooldown',1),.1,60)
        if 'target' in c and c['target']!='player': selector(c['target'])
        if 'enabled' in c: boolean(c['enabled'])
    if colliders+len({c['part'] for c in contacts})>16: raise RuleError('contact collider budget exceeded')
    emitters=caps.get('emitters',[])
    emitter_names=named(emitters,8,('name','socket','asset','target','speed','lifetime','gravity','damage','cooldown','enabled','autofire','color','impact'),('name','socket','asset'))
    for e in emitters:
        identifier(e['socket'])
        if e['socket'] not in socket_names: raise RuleError('unknown emitter socket')
        identifier(e['asset'])
        if 'target' in e and e['target'] not in ('player','forward'): selector(e['target'])
        for key,bounds in {'speed':(.1,40),'lifetime':(.1,10),'gravity':(-.5,2),'damage':(0,1000),'cooldown':(.25,60)}.items():
            if key in e: number(e[key],*bounds)
        for key in ('enabled','autofire'):
            if key in e: boolean(e[key])
        if 'color' in e: color(e['color'])
        if 'impact' in e and e['impact'] not in ('confetti','sparkles','sparks','smoke','dust','splash','explosion'): raise RuleError('unknown impact preset')
    states=raw.get('states',{})
    if not isinstance(states,dict) or len(states)>8: raise RuleError('state limit')
    for name,state in states.items():
        identifier(name); strict(state,('movement','animations','emitters','contacts'))
        if 'movement' in state: movement(state['movement'])
        for key,known in (('animations',animation_names),('emitters',emitter_names),('contacts',contact_names)):
            if key in state:
                strings(state[key])
                if set(state[key])-known: raise RuleError('state references missing component')
    if 'initial_state' in raw:
        identifier(raw['initial_state'])
        if raw['initial_state'] not in states: raise RuleError('unknown initial state')
    if len(json.dumps(raw))>48000: raise RuleError('recipe too large')
    return copy.deepcopy(raw)


def validate_extension_action(a):
    kind=a.get('action') if isinstance(a,dict) else None
    if not isinstance(kind,str): raise RuleError('invalid blueprint action')
    schema=CONTRACT['actions'].get(kind)
    if schema is None: raise RuleError('unknown blueprint action')
    strict(a,('action','target','delay',*schema['required'],*schema['optional']),('action',*schema['required']))
    if isinstance(a.get('target'),dict): selector(a['target'])
    elif a.get('target','self') not in ('self','source'): raise RuleError('blueprint action requires entity target')
    number(a.get('delay',0),0,60)
    if kind=='motion_config': movement(a['config'])
    if 'name' in a: identifier(a['name'])
    if 'part' in a: identifier(a['part'])
    for key in ('enabled','visible'):
        if key in a: boolean(a[key])
    for key in ('position','rotation','scale'):
        if key in a:
            vector(a[key],720 if key=='rotation' else 20)
            if key=='scale':
                for v in a[key]: number(v,.01,20)
