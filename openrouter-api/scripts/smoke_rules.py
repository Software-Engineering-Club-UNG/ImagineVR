#!/usr/bin/env python3
"""Live model acceptance for rules + headset fixes (isolated session, no Unity execution).

IMAGINE_URL and IMAGINE_APP_TOKEN configure the endpoint. Alternatively pass
--settings /path/to/imagine.json (never prints the token). Optional --audio accepts
a 16 kHz mono PCM WAV saying 'give me a sword'. Requires an online backend.
"""
import argparse
import json
import os
from pathlib import Path
import uuid

import httpx


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--settings', type=Path)
    parser.add_argument('--audio', type=Path)
    args = parser.parse_args()
    settings = json.loads(args.settings.read_text()) if args.settings else {}
    base = settings.get('server_url', os.getenv('IMAGINE_URL', 'http://127.0.0.1:8080')).rstrip('/')
    token = settings.get('app_token', os.getenv('IMAGINE_APP_TOKEN', 'dev-token'))
    headers = {'X-App-Token': token, 'X-Session-Id': 'acceptance-' + uuid.uuid4().hex,
               'X-Client-Capabilities': 'runtime-rules-v1', 'X-User-Pose': 'home'}
    with httpx.Client(base_url=base, headers=headers, timeout=120) as client:
        health = client.get('/v1/health').raise_for_status().json()
        assert health['model'] != 'offline', 'Online model required'
        assert 'runtime-rules-v1' in health.get('capabilities', []), 'Rules backend not deployed'
        print('Online backend:', health['model'], flush=True)
        snapshot = {'generation': 0, 'sequence': 1, 'entities': [], 'rules': [],
                    'counters': {}, 'states': {}, 'player': [0, 1.6, 0], 'failures': []}

        def turn(text=None, audio=None):
            options = {'json': {'text': text}} if audio is None else {
                'content': audio.read_bytes(), 'headers': {'Content-Type': 'audio/wav'}}
            response = client.post('/v1/turn', **options).raise_for_status()
            lines = [json.loads(line) for line in response.text.splitlines() if line.strip()]
            assert lines and lines[-1]['op'] == 'done' and lines[-1]['outcome'] != 'failed', lines
            assert not any(s['op'] == 'error' or 'fallback' in s.get('text', '').lower() for s in lines), lines
            print('Reply:', json.dumps(lines, separators=(',', ':')), flush=True)
            return lines

        try:
            client.post('/v1/session/runtime', json=snapshot).raise_for_status()
            lines = turn('Put two sheep in front of me and give me a blaster. When I shoot a sheep, '
                         'make only that sheep burst into confetti and disappear. Use a projectile hit rule.')
            rules = [s for s in lines if s['op'] == 'rule' and s['event'] == 'hit']
            assert rules, 'Missing hit rule'
            rule = rules[0]
            assert rule.get('filters', {}).get('source_kind') == 'projectile', rule
            assert any(a['action'] == 'particles' and a['preset'] == 'confetti' for a in rule['actions']), rule
            assert any(a['action'] == 'remove' and a.get('target', 'self') == 'self' for a in rule['actions']), rule
            assert any(s['op'] == 'give' and s['asset'] == 'blaster' for s in lines), lines
            sheep = [s for s in lines if s['op'] in ('spawn', 'scatter') and s.get('asset') == 'sheep']
            assert sheep, 'Missing sheep'
            # Simulate the client's authoritative snapshot in this test session only.
            snapshot.update(sequence=2, rules=rules, entities=[
                {'id': f'test-sheep-{i}', 'group': sheep[0]['id'], 'asset': 'sheep',
                 'position': [i * 2 - 1, 0, 5], 'tags': []} for i in range(2)])
            client.post('/v1/session/runtime', json=snapshot).raise_for_status()
            lines = turn('Change that same sheep hit rule: shooting a sheep should turn only that sheep blue '
                         'and keep it alive. No confetti or removal. Keep the sheep and my blaster.')
            replacement = next(s for s in lines if s['op'] == 'rule' and s['id'] == rule['id'])
            assert any(a['action'] == 'tint' for a in replacement['actions']), replacement
            assert not any(a['action'] in ('remove', 'particles') for a in replacement['actions']), replacement
            print('PASS: rule creation and replacement with runtime synchronization', flush=True)

            client.headers['X-User-Pose'] = '30,0,90'
            lines = turn('Make Batman appear four metres directly in front of me. Keep the existing world.')
            spawns = [s for s in lines if s['op'] == 'spawn']
            assert spawns and all(not s.get('stand_in') for s in spawns), lines
            assert all(s.get('frame') == [30, 0, 90] for s in spawns), spawns
            print('PASS: Batman composition and placement after moving', flush=True)

            lines = turn('Reset everything, including the blaster in my hand and all the rules. Back to the empty room.')
            assert any(s['op'] == 'clear' and s.get('everything') for s in lines), lines
            print('PASS: full world clear', flush=True)
            if args.audio:
                client.headers['X-User-Pose'] = 'home'
                lines = turn(audio=args.audio)
                assert any(s['op'] == 'transcript' and 'sword' in s['text'].lower() for s in lines), lines
                assert any(s['op'] == 'give' and s['asset'] == 'sword' for s in lines), lines
                print('PASS: WAV -> Whisper -> real model -> give sword', flush=True)
        finally:
            client.post('/v1/session/reset').raise_for_status()


if __name__ == '__main__':
    main()
