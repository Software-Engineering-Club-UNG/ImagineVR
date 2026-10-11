"""Timeout regressions use a simulated clock: no provider calls or long sleeps."""

import asyncio
from contextlib import asynccontextmanager
import json
from pathlib import Path
import runpy
from types import SimpleNamespace

import dotenv
import pytest

from imagine import config, llm

ROOT = Path(__file__).resolve().parents[2]


def settings(monkeypatch, first=None, stall=None):
    monkeypatch.setattr(dotenv, 'load_dotenv', lambda *args: None)
    for key, value in [('IMAGINE_FIRST_TOKEN_SECONDS', first), ('IMAGINE_STALL_SECONDS', stall)]:
        monkeypatch.delenv(key, raising=False)
        if value is not None:
            monkeypatch.setenv(key, str(value))
    return runpy.run_path(str(ROOT / 'openrouter-api/imagine/config.py'))


def test_timeout_defaults_and_environment_overrides(monkeypatch):
    default = settings(monkeypatch)
    assert default['FIRST_TOKEN_SECONDS'] == 30
    assert default['STALL_SECONDS'] == 15
    override = settings(monkeypatch, 42, 20)
    assert override['FIRST_TOKEN_SECONDS'] == 42
    assert override['STALL_SECONDS'] == 20


def test_unity_timeout_defaults_match_the_shipped_settings():
    code = (ROOT / 'client/Assets/ImagineVR/Scripts/Net/ImagineSettings.cs').read_text()
    asset = (ROOT / 'client/Assets/ImagineVR/Resources/ImagineSettings.asset').read_text()
    assert 'LineTimeout = 15f;' in code and 'LineTimeout: 15' in asset
    assert 'TotalTimeout = 90f;' in code and 'TotalTimeout: 90' in asset


def stream(monkeypatch, frames):
    now = [0.0]
    delays = iter(delay for delay, _ in frames)
    monkeypatch.setattr(llm, 'time', SimpleNamespace(monotonic=lambda: now[0]))

    async def wait_for(awaitable, timeout):
        delay = next(delays)
        if delay > timeout:
            now[0] += timeout
            awaitable.close()
            raise asyncio.TimeoutError
        now[0] += delay
        return await awaitable

    monkeypatch.setattr(asyncio, 'wait_for', wait_for)

    async def lines():
        for _, frame in frames:
            yield frame

    @asynccontextmanager
    async def response(*args, **kwargs):
        yield SimpleNamespace(status_code=200, aiter_lines=lines)

    monkeypatch.setattr(llm, '_client', SimpleNamespace(stream=response))
    default = settings(monkeypatch)
    monkeypatch.setattr(config, 'FIRST_TOKEN_SECONDS', default['FIRST_TOKEN_SECONDS'])
    monkeypatch.setattr(config, 'STALL_SECONDS', default['STALL_SECONDS'])

    async def collect():
        return [line async for line in llm.stream_lines('system', 'user')]

    return asyncio.run(collect())


def content(text):
    return 'data: ' + json.dumps({'choices': [{'delta': {'content': text}}]})


def test_slow_first_text_and_content_gap_now_succeed(monkeypatch):
    result = stream(monkeypatch, [(12, content('{"op":"say",')),
                                  (10, content('"text":"hello"}\n')),
                                  (0, 'data: [DONE]')])
    assert result == ['{"op":"say","text":"hello"}']


@pytest.mark.parametrize('frames,message', [
    ([(31, content('hello'))], 'before answering'),
    ([(0, content('hello')), (16, content('world'))], 'partway through'),
    ([(9, ': processing')] * 4, 'before answering'),
])
def test_stalled_streams_still_terminate(monkeypatch, frames, message):
    with pytest.raises(llm.LLMError, match=message):
        stream(monkeypatch, frames)


def test_the_model_is_asked_not_to_think_before_it_writes_unless_told_to(monkeypatch):
    """A helicopter took over 30 silent seconds and was given up on; the default is to start writing."""
    import importlib
    from imagine import config
    monkeypatch.delenv("IMAGINE_REASONING", raising=False)
    monkeypatch.setattr("dotenv.load_dotenv", lambda *a, **k: None)
    try:
        assert importlib.reload(config).REASONING == "off"
    finally:
        monkeypatch.undo()
        importlib.reload(config)
