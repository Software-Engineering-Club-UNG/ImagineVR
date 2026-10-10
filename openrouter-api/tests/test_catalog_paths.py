from imagine import config


def test_bundled_release_catalog(monkeypatch, tmp_path):
    monkeypatch.delenv('IMAGINE_CATALOG', raising=False)
    release = tmp_path / 'releases' / 'revision'
    catalog = release / 'shared' / 'catalog.json'
    catalog.parent.mkdir(parents=True)
    catalog.write_text('{}')
    assert config.catalog_path(release) == catalog


def test_repository_layout(monkeypatch, tmp_path):
    monkeypatch.delenv('IMAGINE_CATALOG', raising=False)
    assert config.catalog_path(tmp_path / 'openrouter-api') == tmp_path / 'shared' / 'catalog.json'


def test_explicit_catalog_override(monkeypatch, tmp_path):
    override = tmp_path / 'exported.json'
    monkeypatch.setenv('IMAGINE_CATALOG', str(override))
    assert config.catalog_path(tmp_path / 'release') == override
