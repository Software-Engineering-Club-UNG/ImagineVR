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


PART_FIELDS=('id','parent','shape','asset','position','rotation','scale','color','color_top','emission','collider','visible','points','radius','vertices','triangles','bevel','taper','sides','mirror','repeat','step','profile','outline','thickness','rough','seed','anchor')
ANIMATION_FIELDS=('name','part','mode','property','amplitude','frequency','phase','duration','loop','enabled','frames','alternate')
EMITTER_FIELDS=('name','socket','asset','target','speed','lifetime','gravity','damage','cooldown','enabled','autofire','color','impact','sound')
IMPACTS=('confetti','sparkles','sparks','smoke','dust','splash','explosion')
MIN_COOLDOWN=.08


def pairs(value,least,most,message):
    if not isinstance(value,list) or not least<=len(value)<=most: raise RuleError(message)
    for p in value:
        if not isinstance(p,list) or len(p)!=2: raise RuleError(message)
        for n in p: number(n,-20,20)


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
        strict(part,PART_FIELDS,('id','shape'))
        identifier(part['id'])
        if part['id'] in by_id: raise RuleError('duplicate part')
        by_id[part['id']]=part
        if part['shape'] not in CONTRACT['shapes']: raise RuleError('unknown shape')
        if 'parent' in part: identifier(part['parent'])
        for key in ('position','rotation','scale'):
            if key in part:
                vector(part[key],720 if key=='rotation' else 20)
                if key=='scale':
                    for n in part[key]: number(n,.002,20)
        if 'color' in part: color(part['color'])
        if 'color_top' in part: color(part['color_top'])
        for key,bounds in {'bevel':(0,1),'sides':(3,24),'repeat':(1,12),'thickness':(.002,20),'rough':(0,.6),'seed':(0,99999)}.items():
            if key in part: number(part[key],*bounds)
        if 'step' in part: vector(part['step'],20)
        if 'taper' in part:
            for n in (part['taper'] if isinstance(part['taper'],list) and len(part['taper'])==2 else [part['taper']]): number(n,0,3)
        if 'mirror' in part and part['mirror']!='x': raise RuleError('mirror must be "x"')
        if 'anchor' in part and part['anchor'] not in ('top','bottom'): raise RuleError('anchor must be top or bottom')
        copies=int(part.get('repeat',1))*(2 if 'mirror' in part else 1)
        if 'emission' in part: number(part['emission'],0,3)
        if 'visible' in part: boolean(part['visible'])
        if 'collider' in part:
            if part['collider'] not in ('box','sphere','capsule'): raise RuleError('invalid collider')
            colliders+=2 if 'mirror' in part else 1
        cost={'cube':44,'plank':44,'sphere':320,'blob':80,'cylinder':48,'cone':24,'capsule':120,'torus':192,'disc':24,'wedge':8,'pyramid':6,'dome':96}.get(part['shape'],128)
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
        if part['shape']=='lathe':
            pairs(part.get('profile'),2,16,'lathe requires a profile of 2-16 [radius,height] pairs')
            if any(p[0]<0 for p in part['profile']): raise RuleError('lathe radius below zero')
            cost=len(part['profile'])*24
        if part['shape']=='slab':
            pairs(part.get('outline'),3,24,'slab requires an outline of 3-24 [forward,up] pairs'); cost=len(part['outline'])*4
        triangles+=cost*copies
    if triangles>CONTRACT['limits']['triangles'] or colliders>16: raise RuleError('geometry/collider budget exceeded')
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
    animation_names=named(animations,16,ANIMATION_FIELDS,('name','part','mode','property'))
    for a in animations:
        identifier(a['part'])
        if a['part'] not in by_id: raise RuleError('unknown animation part')
        if a['mode'] not in CONTRACT['animation_modes'] or a['property'] not in ('position','rotation','scale'): raise RuleError('unknown animation')
        if a['mode']=='spin' and a['property']!='rotation': raise RuleError('spin requires rotation')
        if a['mode']!='keyframes' and 'amplitude' not in a: raise RuleError('animation amplitude required')
        if 'amplitude' in a: vector(a['amplitude'],360 if a['property']=='rotation' else 20)
        for key,bounds in {'frequency':(.05,10),'phase':(-360,360),'duration':(.1,60)}.items():
            if key in a: number(a[key],*bounds)
        for key in ('loop','enabled','alternate'):
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
    emitter_names=named(emitters,8,EMITTER_FIELDS,('name','socket','asset'))
    for e in emitters:
        identifier(e['socket'])
        if e['socket'] not in socket_names: raise RuleError('unknown emitter socket')
        identifier(e['asset'])
        if 'target' in e and e['target'] not in ('player','forward'): selector(e['target'])
        for key,bounds in {'speed':(.1,40),'lifetime':(.1,10),'gravity':(-.5,2),'damage':(0,1000),'cooldown':(MIN_COOLDOWN,60)}.items():
            if key in e: number(e[key],*bounds)
        for key in ('enabled','autofire'):
            if key in e: boolean(e[key])
        if 'color' in e: color(e['color'])
        if 'impact' in e and e['impact'] not in IMPACTS: raise RuleError('unknown impact preset')
        if 'sound' in e and e['sound'] not in CONTRACT['sounds']: raise RuleError('unknown sound')
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
                for v in a[key]: number(v,.002,20)


# ---------------------------------------------------------------------------------- repair
#
# The model writes a recipe of thirty parts in one breath, and one number a hair out of range
# used to throw the whole of it away: in the 20:45 headset session an AK-47 was refused twice
# over a single bound and arrived as a white box. So what can be mended is mended first (a
# number brought into range, a shape called by another name, a reference to nothing dropped),
# and only then is the recipe held to the strict contract above.

SHAPE_NAMES={'box':'cube','block':'cube','cuboid':'cube','rect':'cube','rectangle':'cube','brick':'cube','quad':'cube','ball':'sphere','orb':'sphere','ellipsoid':'sphere','ellipse':'sphere','egg':'sphere',
    'cyl':'cylinder','rod':'cylinder','pipe':'cylinder','barrel':'cylinder','ring':'torus','donut':'torus','doughnut':'torus','hoop':'torus','ramp':'wedge','triangle':'wedge','prism':'wedge',
    'hemisphere':'dome','half_sphere':'dome','halfsphere':'dome','rock':'blob','cloud':'blob','lump':'blob','boulder':'blob','revolve':'lathe','vase':'lathe','extrude':'slab','silhouette':'slab','cutout':'slab',
    'circle':'disc','disk':'disc','plate':'disc','plane':'plank','board':'plank','mesh':'surface','path':'tube','cable':'tube','pill':'capsule','spike':'cone','tetrahedron':'pyramid'}
SOUND_NAMES={'laser':'zap','ray':'zap','blaster':'zap','beam':'zap','shot':'gunshot','gun':'gunshot','bang':'gunshot','rifle':'gunshot','pistol':'gunshot','bullet':'gunshot','explosion':'boom','blast':'boom','cannon':'boom','shotgun':'boom',
    'magic':'sparkle','silent':'none','off':'none'}
MODE_NAMES={'rotate':'spin','rotation':'spin','turn':'spin','bob':'oscillate','swing':'oscillate','wave':'oscillate','sway':'oscillate','flap':'oscillate','pulse':'oscillate','sine':'oscillate','keyframe':'keyframes'}
# a thing the user is to hold that is plainly meant to shoot, and that the model forgot to give anything to shoot with
SHOOTS=('gun','rifle','pistol','blaster','shotgun','revolver','cannon','launcher','bazooka','ak47','ak_47','m16','uzi','smg','sniper','musket','raygun','laser','crossbow','slingshot','carbine','minigun','flamethrower','phaser')


def _number(value,low,high):
    if isinstance(value,str):
        try: value=float(value)
        except ValueError: return None
    if isinstance(value,bool) or not isinstance(value,(int,float)) or value!=value or value in (float('inf'),float('-inf')): return None
    return round(max(low,min(high,value)),4)


def _triple(value,bound,low=None):
    """Three numbers in range. One number alone is taken for all three, which is how a size is often written."""
    if isinstance(value,(int,float)) and not isinstance(value,bool) and low is not None: value=[value]*3
    if not isinstance(value,(list,tuple)) or len(value)!=3: return None
    out=[_number(v,-bound if low is None else low,bound) for v in value]
    return None if None in out else out


def _name(value,fallback=None):
    import re
    if not isinstance(value,(str,int)) or isinstance(value,bool): return fallback
    cleaned=re.sub(r'[^a-zA-Z0-9_.-]+','_',str(value).strip()).strip('_')[:80]
    return cleaned or fallback


def _unique(name,taken):
    candidate,n=name,1
    while candidate in taken:
        n+=1; candidate=f'{name}_{n}'
    taken.add(candidate); return candidate


def _flag(value):
    if isinstance(value,bool): return value
    if isinstance(value,str) and value.lower() in ('true','false','yes','no'): return value.lower() in ('true','yes')
    return None


def _selector(value):
    if isinstance(value,dict) and len(value)==1:
        key,name=next(iter(value.items()))
        if key in ('instance','group','asset','tag') and _name(name): return {key:_name(name)}
    return None


def _target(value,words):
    if isinstance(value,str) and value.lower() in words: return value.lower()
    return _selector(value)


def _pairs(value,least,most,bound,floor=None):
    if not isinstance(value,list): return None
    out=[]
    for p in value[:most]:
        if not isinstance(p,(list,tuple)) or len(p)<2: continue
        a,b=_number(p[0],-bound if floor is None else floor,bound),_number(p[1],-bound,bound)
        if a is not None and b is not None: out.append([a,b])
    return out if len(out)>=least else None


def _put(out,key,value):
    if value is not None: out[key]=value


def _movement(raw):
    if not isinstance(raw,dict): return None
    mode=raw.get('mode')
    if not isinstance(mode,str) or mode.lower() not in CONTRACT['movement_modes']: return None
    out={'mode':mode.lower()}
    _put(out,'airborne',_flag(raw.get('airborne')))
    for key,(low,high) in {'speed':(0,15),'turn_speed':(1,360),'radius':(.5,40),'altitude':(.5,40)}.items():
        if key in raw: _put(out,key,_number(raw[key],low,high))
    if 'target' in raw: _put(out,'target',_target(raw['target'],('player',)))
    if isinstance(raw.get('path'),list):
        points=[p for p in (_triple(v,120) for v in raw['path'][:24]) if p]
        if len(points)>=2: out['path']=points
    if out['mode']=='path' and 'path' not in out: out['mode']='wander'
    return out


def _part(raw,taken,catalog):
    from .script import to_color
    if not isinstance(raw,dict): return None
    out={'id':_unique(_name(raw.get('id'),'part'),taken)}
    shape=_name(raw.get('shape'),'cube').lower()
    shape=SHAPE_NAMES.get(shape,shape)
    if shape not in CONTRACT['shapes']: shape='cube'
    if shape=='asset':
        wanted=_name(raw.get('asset'),'')
        found=wanted if catalog is None or catalog.has('asset',wanted) else catalog.resolve('asset',wanted)[0]
        if found: out['asset']=found
        else: shape='cube'
    for key,bound,low in (('position',20,None),('rotation',720,None),('scale',20,.002),('step',20,None)):
        if key in raw: _put(out,key,_triple(raw[key],bound,low))
    for key in ('color','color_top'):
        if key in raw: _put(out,key,to_color(raw[key]))
    for key,(low,high) in {'emission':(0,3),'bevel':(0,1),'thickness':(.002,20),'rough':(0,.6),'radius':(.01,3)}.items():
        if key in raw: _put(out,key,_number(raw[key],low,high))
    for key,(low,high) in {'sides':(3,24),'seed':(0,99999),'repeat':(1,12)}.items():
        n=_number(raw.get(key),low,high) if key in raw else None
        if n is not None: out[key]=int(round(n))
    if out.get('repeat',1)<2 or 'step' not in out: out.pop('repeat',None); out.pop('step',None)
    if 'taper' in raw:
        t=raw['taper']
        if isinstance(t,(list,tuple)) and len(t)==2: t=[_number(v,0,3) for v in t]; t=None if None in t else t
        else: t=_number(t,0,3)
        _put(out,'taper',t)
    if raw.get('mirror') in ('x','X',True,'yes','true'): out['mirror']='x'
    if raw.get('anchor') in ('top','bottom'): out['anchor']=raw['anchor']
    _put(out,'visible',_flag(raw.get('visible')) if 'visible' in raw else None)
    if raw.get('collider'): out['collider']=raw['collider'] if raw['collider'] in ('box','sphere','capsule') else 'box'
    _put(out,'parent',_name(raw.get('parent')) if 'parent' in raw else None)
    if shape=='lathe':
        profile=_pairs(raw.get('profile'),2,16,20,floor=0)
        if profile and any(p[0]>0 for p in profile): out['profile']=profile
        else: shape='cylinder'
    if shape=='slab':
        outline=_pairs(raw.get('outline'),3,24,20)
        if outline: out['outline']=outline
        else: shape='cube'
    if shape=='tube':
        points=[p for p in (_triple(v,20) for v in raw.get('points',[])[:16]) if p] if isinstance(raw.get('points'),list) else []
        if len(points)>=2: out['points']=points
        else: shape='cylinder'
    if shape=='surface':
        vertices=[p for p in (_triple(v,20) for v in raw.get('vertices',[])[:24]) if p] if isinstance(raw.get('vertices'),list) else []
        corners=raw.get('triangles') if isinstance(raw.get('triangles'),list) else []
        faces=[corners[i:i+3] for i in range(0,len(corners)-2,3)][:44]
        faces=[f for f in faces if all(isinstance(n,int) and not isinstance(n,bool) and 0<=n<len(vertices) for n in f)]
        if len(vertices)>=3 and faces: out['vertices']=vertices; out['triangles']=[n for f in faces for n in f]
        else: shape='cube'
    out['shape']=shape
    return out


def _front(parts):
    """Where the far end of the thing is along +Z, for a muzzle the model did not mark."""
    best=None
    for p in parts:
        if 'parent' in p: continue
        position=p.get('position',[0,0,0]); size=p.get('scale',[1,1,1])
        reach=position[2]+max(size)/2 if p.get('rotation') and any(p['rotation']) else position[2]+size[2]/2
        if best is None or reach>best[2]: best=[position[0],position[1],reach]
    return [round(v,4) for v in best or [0,0,0]]


def repair_blueprint(raw,catalog=None,known=()):
    """A recipe as near to what the model wrote as the contract allows. Raises only when there is nothing to build."""
    from .script import to_color
    if not isinstance(raw,dict) or not isinstance(raw.get('parts'),list): raise RuleError('a blueprint needs a list of parts')
    out={'op':'blueprint','id':_name(raw.get('id'),'made_thing')}
    if isinstance(raw.get('description'),str): out['description']=raw['description'][:200]
    taken=set(); parts=[p for p in (_part(r,taken,catalog) for r in raw['parts'][:CONTRACT['limits']['parts']]) if p]
    if not parts: raise RuleError('a blueprint needs at least one part')
    by_id={p['id']:p for p in parts}; colliders=0
    for p in parts:
        # a parent that is not there, or a chain that loops or runs too deep, is cut loose
        seen={p['id']}; cursor=p
        while 'parent' in cursor:
            parent=by_id.get(cursor['parent'])
            if parent is None or parent['id'] in seen or len(seen)>=CONTRACT['limits']['depth']: p.pop('parent',None) if cursor is p else cursor.pop('parent',None); break
            seen.add(parent['id']); cursor=parent
        if 'collider' in p:
            colliders+=2 if 'mirror' in p else 1
            if colliders>CONTRACT['limits']['colliders']: del p['collider']
    out['parts']=parts

    sockets=[]; socket_names=set()
    for raw_socket in raw.get('sockets',[])[:16] if isinstance(raw.get('sockets'),list) else []:
        if not isinstance(raw_socket,dict): continue
        socket={'name':_unique(_name(raw_socket.get('name'),'socket'),socket_names),'part':_name(raw_socket.get('part'))}
        if socket['part'] not in by_id: socket['part']=parts[0]['id']
        for key,bound in (('position',20),('rotation',720)):
            if key in raw_socket: _put(socket,key,_triple(raw_socket[key],bound))
        sockets.append(socket)

    animations=[]; animation_names=set(); owners=set()
    for a in raw.get('animations',[]) if isinstance(raw.get('animations'),list) else []:
        if not isinstance(a,dict) or _name(a.get('part')) not in by_id or len(animations)>=16: continue
        mode=str(a.get('mode','oscillate')).lower(); mode=MODE_NAMES.get(mode,mode)
        if mode not in CONTRACT['animation_modes']: mode='oscillate'
        prop=a.get('property') if a.get('property') in ('position','rotation','scale') else 'rotation'
        if mode=='spin': prop='rotation'
        if (a['part'],prop) in owners: continue
        bound=360 if prop=='rotation' else 20
        item={'part':_name(a['part']),'mode':mode,'property':prop}
        if mode=='keyframes':
            frames={}
            for f in a.get('frames',[]) if isinstance(a.get('frames'),list) else []:
                if isinstance(f,dict):
                    at,value=_number(f.get('time'),0,60),_triple(f.get('value'),720 if prop=='rotation' else 20)
                    if at is not None and value: frames[at]=value
            ordered=sorted(frames.items())[:16]
            if not ordered or ordered[0][0]!=0: ordered=[(0,[0,0,0])]+ordered[:15]
            if len(ordered)<2: continue
            item['frames']=[{'time':t,'value':v} for t,v in ordered]
        else:
            amplitude=_triple(a.get('amplitude'),bound)
            if not amplitude: continue
            item['amplitude']=amplitude
        for key,(low,high) in {'frequency':(.05,10),'phase':(-360,360),'duration':(.1,60)}.items():
            if key in a: _put(item,key,_number(a[key],low,high))
        for key in ('loop','enabled','alternate'):
            if key in a: _put(item,key,_flag(a[key]))
        item['name']=_unique(_name(a.get('name'),'move'),animation_names); owners.add((item['part'],prop)); animations.append(item)

    given=raw.get('capabilities') if isinstance(raw.get('capabilities'),dict) else {}
    caps={}
    for key in ('grabbable','physics'):
        if key in given: _put(caps,key,_flag(given[key]))
    for key,(low,high) in {'mass':(.02,100),'gravity':(-.5,2),'health':(0,10000),'durability':(0,10000)}.items():
        if key in given: _put(caps,key,_number(given[key],low,high))
    if isinstance(given.get('grip'),dict):
        grip={key:value for key,value in ((k,_triple(given['grip'].get(k),720 if k=='rotation' else 20)) for k in ('position','rotation')) if value}
        caps['grip']=grip
    _put(caps,'movement',_movement(given.get('movement')))

    contacts=[]; contact_names=set()
    for c in given.get('contacts',[])[:8] if isinstance(given.get('contacts'),list) else []:
        if not isinstance(c,dict) or _name(c.get('part')) not in by_id: continue
        item={'name':_unique(_name(c.get('name'),'touch'),contact_names),'part':_name(c['part']),'damage':_number(c.get('damage',10),0,1000) or 0}
        if 'cooldown' in c: _put(item,'cooldown',_number(c['cooldown'],.1,60))
        if 'target' in c: _put(item,'target',_target(c['target'],('player',)))
        if 'enabled' in c: _put(item,'enabled',_flag(c['enabled']))
        contacts.append(item)
    while contacts and colliders+len({c['part'] for c in contacts})>CONTRACT['limits']['colliders']: contacts.pop()

    def muzzle():
        """The socket a shot leaves by when none was named: the far end of the thing, pointing along it."""
        for socket in sockets:
            if socket['name'] in ('muzzle','barrel','tip','mouth','nozzle'): return socket['name']
        if sockets: return sockets[0]['name']
        point={'id':_unique('muzzle_point',taken),'shape':'cube','position':_front(parts),'scale':[.01,.01,.01],'visible':False}
        parts.append(point); by_id[point['id']]=point
        sockets.append({'name':_unique('muzzle',socket_names),'part':point['id']})
        return sockets[-1]['name']

    emitters=[]; emitter_names=set()
    for e in given.get('emitters',[])[:8] if isinstance(given.get('emitters'),list) else []:
        if not isinstance(e,dict): continue
        item={'name':_unique(_name(e.get('name'),'shot'),emitter_names)}
        socket=_name(e.get('socket'))
        item['socket']=socket if socket in socket_names else muzzle()
        asset=_name(e.get('asset'),'energy_ball')
        item['asset']=asset if asset in known or asset==out['id'] or catalog is None or catalog.has('asset',asset) else 'energy_ball'
        if 'target' in e: _put(item,'target',_target(e['target'],('player','forward')))
        for key,(low,high) in {'speed':(.1,40),'lifetime':(.1,10),'gravity':(-.5,2),'damage':(0,1000),'cooldown':(MIN_COOLDOWN,60)}.items():
            if key in e: _put(item,key,_number(e[key],low,high))
        for key in ('enabled','autofire'):
            if key in e: _put(item,key,_flag(e[key]))
        if 'color' in e: _put(item,'color',to_color(e['color']))
        if e.get('impact') in IMPACTS: item['impact']=e['impact']
        if isinstance(e.get('sound'),str):
            sound=SOUND_NAMES.get(e['sound'].lower(),e['sound'].lower())
            if sound in CONTRACT['sounds']: item['sound']=sound
        emitters.append(item)
    words=(out['id']+' '+out.get('description','')).lower().replace('-','_').replace(' ','_')
    if not emitters and caps.get('grabbable') and any(word in words.split('_') or word in out['id'].lower() for word in SHOOTS):
        emitters.append({'name':'shot','socket':muzzle(),'asset':'energy_ball','target':'forward','speed':30,'lifetime':2,'cooldown':.18,'damage':25,'enabled':True})
        emitter_names.add('shot')
    if contacts: caps['contacts']=contacts
    if emitters: caps['emitters']=emitters
    if sockets: out['sockets']=sockets
    if animations: out['animations']=animations
    if caps: out['capabilities']=caps

    states={}
    for name,state in list(raw['states'].items())[:8] if isinstance(raw.get('states'),dict) else []:
        if not _name(name) or not isinstance(state,dict): continue
        item={}
        _put(item,'movement',_movement(state.get('movement')))
        for key,known_names in (('animations',animation_names),('emitters',emitter_names),('contacts',contact_names)):
            if isinstance(state.get(key),list): item[key]=[n for n in state[key] if isinstance(n,str) and n in known_names][:16]
        states[_name(name)]=item
    if states:
        out['states']=states
        if _name(raw.get('initial_state')) in states: out['initial_state']=_name(raw['initial_state'])
    return out


PLAIN_SHAPES={'dome':'sphere','blob':'sphere','lathe':'cylinder','slab':'cube'}


def plain_blueprint(recipe):
    """The same recipe in the words an app built before the richer parts understands."""
    out=copy.deepcopy(recipe)
    for part in out['parts']:
        size=part.get('scale',[1,1,1]); at=part.get('position',[0,0,0])
        if part['shape'] in ('slab','lathe'):
            # these carry their size in their points; a plain box or drum of the same extent stands in
            points=part['outline'] if part['shape']=='slab' else part['profile']
            low,high=min(p[1] for p in points),max(p[1] for p in points)
            if part['shape']=='slab':
                back,front=min(p[0] for p in points),max(p[0] for p in points)
                part['scale']=[max(.01,part.get('thickness',.05)*size[0]),max(.01,(high-low)*size[1]),max(.01,(front-back)*size[2])]
                part['position']=[at[0],round(at[1]+(low+high)/2*size[1],4),round(at[2]+(back+front)/2*size[2],4)]
            else:
                wide=2*max(p[0] for p in points)
                part['scale']=[max(.01,wide*size[0]),max(.01,(high-low)*size[1]),max(.01,wide*size[2])]
                part['position']=[at[0],round(at[1]+(low+high)/2*size[1],4),at[2]]
        if part.get('anchor') and part['shape'] not in ('slab','lathe','dome') and not any(part.get('rotation',[0,0,0])):
            part['position']=[at[0],round(at[1]+(size[1]/2 if part['anchor']=='bottom' else -size[1]/2),4),at[2]]
        part['shape']=PLAIN_SHAPES.get(part['shape'],part['shape'])
        for key in ('anchor','color_top','bevel','taper','sides','mirror','repeat','step','profile','outline','thickness','rough','seed'): part.pop(key,None)
        if part['shape'] not in ('tube',): part.pop('radius',None)
    for a in out.get('animations',[]): a.pop('alternate',None)
    for e in out.get('capabilities',{}).get('emitters',[]):
        e.pop('sound',None)
        if 'cooldown' in e: e['cooldown']=max(.25,e['cooldown'])
    return out
