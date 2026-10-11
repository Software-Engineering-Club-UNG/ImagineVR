"""Strict data-only rules shared with the Unity runtime. Never execute model code."""
from __future__ import annotations

import copy
import json
import math
import re
from . import config

_bundled = config.SERVER_DIR / 'shared' / 'runtime-rules.json'
CAPABILITIES = json.loads((_bundled if _bundled.is_file() else config.REPO_DIR / 'shared' / 'runtime-rules.json').read_text(encoding='utf-8'))
CAPABILITY = CAPABILITIES['capability']
LIMITS = CAPABILITIES['limits']


class RuleError(ValueError):
    pass


def strict(value, allowed, required=()):
    if not isinstance(value, dict) or set(value) - set(allowed) or set(required) - set(value):
        raise RuleError('unknown or missing rule fields')


def identifier(value):
    if not isinstance(value, str) or not re.fullmatch(r'[a-zA-Z0-9_.-]{1,80}', value):
        raise RuleError('invalid identifier')
    return value


def number(value, low=-100000, high=100000):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or not low <= value <= high:
        raise RuleError('number outside bounds')
    return value


def vector(value, bound=120):
    if not isinstance(value, list) or len(value) != 3:
        raise RuleError('expected three coordinates')
    for item in value:
        number(item, -bound, bound)


def selector(value):
    strict(value, ('instance', 'group', 'asset', 'tag'))
    if len(value) != 1:
        raise RuleError('selector requires one kind')
    identifier(next(iter(value.values())))


def validate_action(action):
    kind = action.get('action') if isinstance(action, dict) else None
    if not isinstance(kind,str): raise RuleError('invalid action kind')
    from .blueprints import CONTRACT, validate_extension_action
    if kind in CONTRACT['actions']:
        validate_extension_action(action)
        return
    schema = CAPABILITIES['actions'].get(kind)
    if schema is None:
        raise RuleError('unsupported action')
    strict(action, ['action', 'target', 'delay', *schema['required'], *schema['optional']], ['action', *schema['required']])
    target = action.get('target', 'self')
    if isinstance(target, dict):
        selector(target)
    elif target not in ('self', 'source', 'world', 'player'):
        raise RuleError('unsupported action target')
    world_actions = {'counter_set','counter_add','state_set','goal_complete','rule_enable','rule_disable','timer_start','timer_cancel','sky','light','fog','weather','ambience'}
    player_actions = {'teleport_player','resize_player','mount','dismount','haptic'}
    if target == 'world' and kind not in world_actions | {'set_gravity','message','sound','particles','spawn'}:
        raise RuleError('action cannot target world')
    if target == 'player' and kind not in player_actions | {'message','sound','particles'}:
        raise RuleError('action cannot target player')
    for key, value in action.items():
        if key in ('action','target'):
            continue
        if key in ('delay', 'duration'):
            number(value, 0, 60)
        elif key in ('position','offset','rotation','vector'):
            vector(value, 720 if key == 'rotation' else 120)
        elif key in ('enabled','repeat') or key == 'loop' and kind == 'follow_path':
            if not isinstance(value, bool): raise RuleError('expected boolean')
        elif key == 'color':
            if not isinstance(value, str) or not re.fullmatch(r'#[0-9a-fA-F]{6}', value): raise RuleError('expected hex color')
        elif key == 'text':
            if not isinstance(value, str) or not 1 <= len(value) <= 200: raise RuleError('invalid message')
        elif key == 'path':
            if not isinstance(value, list) or not 2 <= len(value) <= 24: raise RuleError('invalid path')
            for point in value: vector(point)
        elif key == 'tags':
            if not isinstance(value, list) or len(value) > 8: raise RuleError('invalid tags')
            for tag in value: identifier(tag)
        elif key in ('count','volume','pitch','size','speed','strength','intensity','density','scale'):
            bounds = {'count':(1,200 if kind == 'particles' else 20),'volume':(0,1),'pitch':(.5,2),'size':(.01,3),'speed':(.1,40),'strength':(0,1),'intensity':(0,2),'density':(0,1),'scale':(.05,20)}
            number(value, *bounds[key])
        elif key == 'value':
            if kind == 'state_set':
                if not isinstance(value, (bool, str)) or isinstance(value, str) and len(value) > 80: raise RuleError('invalid state')
            else:
                bounds = (.05,20) if kind in ('scale','resize_player') else (-.5,2) if kind == 'set_gravity' else (0,10000) if kind in ('damage','heal','set_health','set_breakable') else (-100000,100000)
                number(value, *bounds)
        else:
            identifier(value)
    if kind == 'particles' and action['preset'] not in CAPABILITIES['particles']:
        raise RuleError('unknown particle preset')
    if kind == 'set_behavior' or 'behavior' in action:
        if action['behavior'] not in ('chase','wander','flee','still'): raise RuleError('unknown behavior')
    if kind == 'timer_start' and action['duration'] < .25:
        raise RuleError('timer too fast')


def validate_rule(raw):
    strict(raw, ('op','id','selector','event','filters','conditions','actions','mode','enabled','cooldown','once'), ('op','id','selector','event','actions'))
    if raw['op'] != 'rule': raise RuleError('expected rule')
    identifier(raw['id'])
    selector(raw['selector'])
    from .blueprints import CONTRACT
    if raw['event'] not in CAPABILITIES['events'] + CONTRACT['events']: raise RuleError('unsupported event')
    if raw.get('mode','replace') not in ('replace','add'): raise RuleError('invalid rule mode')
    for key in ('once','enabled'):
        if key in raw and not isinstance(raw[key], bool): raise RuleError('expected boolean')
    number(raw.get('cooldown', .1), 0, 60)
    filters = raw.get('filters', {})
    strict(filters, ('source_kind','source_asset','source_tag','min_speed','radius','threshold','name','duration','part','line_of_sight'))
    for key, value in filters.items():
        if key == 'source_kind':
            if value not in ('projectile','thrown','melee','collision'): raise RuleError('unknown hit source')
        elif key in ('min_speed','radius','threshold','duration'):
            number(value, .25 if key == 'duration' else 0, 60 if key == 'duration' else 120)
        elif key == 'line_of_sight':
            if not isinstance(value,bool): raise RuleError('expected line_of_sight boolean')
        else: identifier(value)
    if raw['event'] in ('timer','interval') and 'duration' not in filters:
        raise RuleError('timer event requires duration')
    if raw['event'] in ('zone_enter','zone_exit','proximity_enter','proximity_exit') and 'radius' not in filters:
        raise RuleError('spatial event requires radius')
    if raw['event']=='part_contact' and 'part' not in filters: raise RuleError('part_contact requires part filter')
    conditions = raw.get('conditions', [])
    if not isinstance(conditions, list) or len(conditions) > 8: raise RuleError('too many conditions')
    for condition in conditions:
        strict(condition, ('field','operator','value','name'), ('field','operator','value'))
        if condition['field'] not in ('health','durability','speed','source_kind','source_asset','source_tag','counter','state','actor_state','chance'):
            raise RuleError('unknown condition field')
        if condition['operator'] not in ('eq','ne','lt','le','gt','ge'): raise RuleError('unknown comparison')
        if condition['field'] in ('counter','state'): identifier(condition.get('name'))
        value = condition['value']
        if condition['field'] in ('source_kind','source_asset','source_tag','state','actor_state'):
            if not isinstance(value, (str, bool)) or isinstance(value, str) and len(value) > 80: raise RuleError('invalid condition value')
        else: number(value, 0, 1) if condition['field'] == 'chance' else number(value)
    actions = raw['actions']
    if not isinstance(actions, list) or not 1 <= len(actions) <= 8: raise RuleError('invalid action count')
    for action in actions: validate_action(action)
    if sum(action.get('delay', 0) for action in actions) > 60:
        raise RuleError('combined delay exceeds 60 seconds')
    result = copy.deepcopy(raw)
    result.setdefault('mode', 'replace')
    result.setdefault('enabled', True)
    result.setdefault('cooldown', .1)
    result.setdefault('once', False)
    return result


USER_STATE = 'user-state-v1'


def validate_user(raw):
    """What the user has in their hands, in reach, lying dropped, and is riding: asset ids only."""
    strict(raw, ('holding', 'in_reach', 'dropped', 'riding'))
    for key in ('holding', 'in_reach', 'dropped'):
        items = raw.get(key, [])
        if not isinstance(items, list) or len(items) > 8: raise RuleError('invalid user state')
        for item in items: identifier(item)
    if raw.get('riding') is not None: identifier(raw['riding'])


def validate_snapshot(raw):
    strict(raw, ('generation','sequence','entities','rules','counters','states','player','failures','blueprints','overrides','player_health','player_damage_enabled','user'), ('generation','sequence','entities','rules','counters','states','player','failures'))
    if 'user' in raw: validate_user(raw['user'])
    for key in ('generation','sequence'):
        number(raw[key], 0, 2**31-1)
        if not isinstance(raw[key], int): raise RuleError('expected integer sequence')
    vector(raw['player'], 1000)
    entities = raw['entities']
    if not isinstance(entities, list) or len(entities) > LIMITS['snapshot_entities']: raise RuleError('too many entities')
    ids = set()
    for entity in entities:
        strict(entity, ('id','group','asset','position','tags','health','durability','revision','parts','actor_state','movement','animations','emitters','contacts'), ('id','group','asset','position','tags'))
        for key in ('id','group','asset'): identifier(entity[key])
        if entity['id'] in ids: raise RuleError('duplicate instance id')
        ids.add(entity['id'])
        vector(entity['position'], 1000)
        if not isinstance(entity['tags'],list) or len(entity['tags']) > 8: raise RuleError('invalid tags')
        for tag in entity['tags']: identifier(tag)
        for key in ('health','durability'):
            if key in entity: number(entity[key],0,10000)
        if 'revision' in entity: number(entity['revision'],1,2**31-1)
        if 'parts' in entity:
            from .blueprints import strings
            strings(entity['parts'],64)
        if 'actor_state' in entity: identifier(entity['actor_state'])
        if 'movement' in entity:
            from .blueprints import movement
            movement(entity['movement'])
        for key in ('animations','emitters','contacts'):
            if key in entity:
                from .blueprints import strings
                strings(entity[key])
    if not isinstance(raw['rules'], list) or len(raw['rules']) > 64: raise RuleError('too many rules')
    validated_rules = [validate_rule(rule) for rule in raw['rules']]
    if len({r['id'] for r in validated_rules}) != len(validated_rules): raise RuleError('duplicate rules')
    for key in ('counters','states'):
        if not isinstance(raw[key],dict) or len(raw[key]) > 64: raise RuleError('too many state values')
        for name, value in raw[key].items():
            identifier(name)
            if key == 'counters': number(value)
            elif not isinstance(value,(str,bool)) or isinstance(value,str) and len(value)>80: raise RuleError('invalid state')
    if not isinstance(raw['failures'],list) or len(raw['failures'])>32 or any(not isinstance(v,str) or len(v)>200 for v in raw['failures']): raise RuleError('invalid failures')
    from .blueprints import validate_blueprint
    definitions=raw.get('blueprints',[])
    if not isinstance(definitions,list) or len(definitions)>64: raise RuleError('blueprint registry limit')
    for b in definitions: validate_blueprint(b)
    if len({b['id'] for b in definitions})!=len(definitions): raise RuleError('duplicate blueprint')
    if len(json.dumps(definitions))>256000: raise RuleError('registry byte budget')
    overrides=raw.get('overrides',{})
    if not isinstance(overrides,dict) or len(overrides)>64: raise RuleError('override limit')
    for key,b in overrides.items(): identifier(key); validate_blueprint(b)
    if len(json.dumps(overrides))>128000: raise RuleError('override byte budget')
    if 'player_health' in raw: number(raw['player_health'],0,100)
    if 'player_damage_enabled' in raw and not isinstance(raw['player_damage_enabled'],bool): raise RuleError('invalid player damage permission')
    return copy.deepcopy(raw)
