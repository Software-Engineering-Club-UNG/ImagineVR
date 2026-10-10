import asyncio
from types import SimpleNamespace

import numpy as np
import pytest
from fastapi.testclient import TestClient

from imagine import config, stt
from app.main import app


def test_transcription_is_local_and_does_not_require_provider_keys(monkeypatch):
    monkeypatch.setattr(config, 'STT', 'local')
    monkeypatch.setattr(config, 'OPENROUTER_API_KEY', '')
    monkeypatch.setattr(config, 'ELEVENLABS_API_KEY', '')
    monkeypatch.setattr(stt, '_transcribe_local', lambda _: 'Build a forest')
    assert asyncio.run(stt.transcribe(b'audio')) == 'Build a forest'


def test_hosted_stt_is_not_supported(monkeypatch):
    monkeypatch.setattr(config, 'STT', 'elevenlabs')
    with pytest.raises(RuntimeError, match='Only local'):
        asyncio.run(stt.transcribe(b'audio'))


def test_production_accepts_local_whisper_without_speech_credentials(monkeypatch):
    monkeypatch.setattr(config, 'PRODUCTION', True)
    monkeypatch.setattr(config, 'OFFLINE', False)
    monkeypatch.setattr(config, 'APP_TOKEN', 'strong-client-token')
    monkeypatch.setattr(config, 'STT', 'local')
    monkeypatch.setattr(config, 'ELEVENLABS_API_KEY', '')
    monkeypatch.setattr(stt, 'warm_up', lambda: None)
    with TestClient(app) as client:
        assert client.get('/v1/health').json()['stt'] == 'local:base.en'


def test_cached_model_uses_cpu_int8_and_no_network(monkeypatch, tmp_path):
    import faster_whisper
    captured = {}
    def model(path, **kwargs):
        captured.update(path=path, **kwargs)
        return object()
    monkeypatch.setattr(faster_whisper, 'WhisperModel', model)
    monkeypatch.setattr(stt, '_model', None)
    monkeypatch.setattr(config, 'STT_MODEL_PATH', str(tmp_path))
    stt._load_model()
    assert captured['path'] == str(tmp_path)
    assert captured['device'] == 'cpu' and captured['compute_type'] == 'int8'
    assert captured['cpu_threads'] == 4 and captured['local_files_only'] is True


def test_local_decoder_filters_silence_and_runs_vad(monkeypatch):
    captured = {}
    def transcribe(samples, **kwargs):
        captured.update(kwargs)
        return [SimpleNamespace(text=' build a maze ', no_speech_prob=0.1),
                SimpleNamespace(text='hallucination', no_speech_prob=0.9)], None
    monkeypatch.setattr(stt, '_load_model', lambda: SimpleNamespace(transcribe=transcribe))
    monkeypatch.setattr(stt, 'wav_to_samples', lambda _: np.zeros(16000, dtype=np.float32))
    assert stt._transcribe_local(b'audio') == 'build a maze'
    assert captured['vad_filter'] is True and captured['language'] == 'en'
