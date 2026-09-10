"""Solid validation coverage for provider packs and source types."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from app.services.provider_pack import (
    ALLOWED_SOURCE_TYPES,
    MANDATORY_RELATIVE_FILES,
    load_provider_pack,
    validate_provider_pack_dir,
    validate_shipped_providers,
)

FIXTURES = Path(__file__).resolve().parent / "fixtures" / "provider_packs"
REPO_PROVIDERS = Path(__file__).resolve().parents[3] / "providers"


def _errors(issues):
    return [i for i in issues if i.code != "warning"]


def _codes(issues):
    return {i.code for i in issues}


def test_allowed_source_types_documented() -> None:
    assert ALLOWED_SOURCE_TYPES == {
        "manual",
        "openapi_url",
        "git",
        "github",
        "gitlab",
        "html_changelog",
    }


def test_mandatory_files_constant() -> None:
    assert "manifest.json" in MANDATORY_RELATIVE_FILES
    assert "contract/baseline.json" in MANDATORY_RELATIVE_FILES
    assert "contract/target.json" in MANDATORY_RELATIVE_FILES


@pytest.mark.parametrize(
    "pack_id,source_type",
    [
        ("src_manual", "manual"),
        ("src_openapi_url", "openapi_url"),
        ("src_github", "github"),
        ("src_gitlab", "gitlab"),
        ("src_html", "html_changelog"),
    ],
)
def test_valid_source_type_packs_pass(pack_id: str, source_type: str) -> None:
    issues = validate_provider_pack_dir(FIXTURES / pack_id)
    assert _errors(issues) == [], [str(i) for i in issues]
    # load via direct dir validation path used by tests: set env search
    # We validate shape only here; load uses search roots.


def test_html_changelog_emits_assist_warning() -> None:
    issues = validate_provider_pack_dir(FIXTURES / "src_html")
    assert any(i.code == "warning" and "assist-only" in i.message for i in issues)


def test_missing_contract_fails() -> None:
    issues = validate_provider_pack_dir(FIXTURES / "bad_missing_contract")
    codes = _codes(_errors(issues))
    assert "missing_file" in codes


def test_openapi_url_requires_url() -> None:
    issues = validate_provider_pack_dir(FIXTURES / "bad_openapi_url")
    assert any(i.code == "invalid_source" and "openapiUrl" in i.message for i in _errors(issues))


def test_id_mismatch_fails() -> None:
    issues = validate_provider_pack_dir(FIXTURES / "bad_id_mismatch")
    assert any(i.code == "id_mismatch" for i in _errors(issues))


def test_unknown_source_type_fails() -> None:
    issues = validate_provider_pack_dir(FIXTURES / "bad_source_type")
    assert any(i.code == "invalid_source" for i in _errors(issues))


def test_openapi_shape_fails() -> None:
    issues = validate_provider_pack_dir(FIXTURES / "bad_openapi_shape")
    assert any(i.code == "invalid_openapi" for i in _errors(issues))


def test_shipped_stripe_and_mock_have_mandatory_files() -> None:
    results = validate_shipped_providers(REPO_PROVIDERS)
    assert "stripe" in results
    assert "mock" in results

    for name in ("stripe", "mock"):
        errors = _errors(results[name])
        assert errors == [], f"{name}: {[str(e) for e in errors]}"

        pack_dir = REPO_PROVIDERS / name
        for relative in MANDATORY_RELATIVE_FILES:
            assert (pack_dir / relative).is_file(), f"{name} missing {relative}"


def test_shipped_stripe_rules_compile() -> None:
    rules_path = REPO_PROVIDERS / "stripe" / "rules" / "csharp.json"
    rules = json.loads(rules_path.read_text(encoding="utf-8"))
    assert isinstance(rules, list) and len(rules) >= 2
    pack = load_provider_pack("stripe")
    assert pack.source_type in ALLOWED_SOURCE_TYPES
    assert len(pack.csharp_rules) >= 2


def test_shipped_mock_source_is_manual() -> None:
    pack = load_provider_pack("mock")
    assert pack.source_type == "manual"
    assert pack.baseline_path.is_file()
    assert pack.target_path.is_file()


def test_validate_rejects_broken_rules_regex(tmp_path: Path) -> None:
    pack = tmp_path / "bad_rules"
    (pack / "contract").mkdir(parents=True)
    (pack / "rules").mkdir()
    (pack / "manifest.json").write_text(
        json.dumps(
            {
                "id": "bad_rules",
                "displayName": "Bad rules",
                "languages": ["csharp"],
                "source": {"type": "manual"},
            }
        ),
        encoding="utf-8",
    )
    openapi = {
        "openapi": "3.0.0",
        "info": {"title": "t", "version": "1"},
        "paths": {"/a": {"get": {"responses": {"200": {"description": "ok"}}}}},
    }
    (pack / "contract" / "baseline.json").write_text(json.dumps(openapi), encoding="utf-8")
    (pack / "contract" / "target.json").write_text(json.dumps(openapi), encoding="utf-8")
    (pack / "rules" / "csharp.json").write_text(
        json.dumps([{"ruleId": "x.bad", "pattern": "(unclosed"}]),
        encoding="utf-8",
    )

    issues = validate_provider_pack_dir(pack)
    assert any(i.code == "invalid_rules" for i in _errors(issues))


def test_github_source_requires_git_url(tmp_path: Path) -> None:
    pack = tmp_path / "need_git"
    (pack / "contract").mkdir(parents=True)
    (pack / "manifest.json").write_text(
        json.dumps(
            {
                "id": "need_git",
                "displayName": "Need git",
                "languages": ["csharp"],
                "source": {"type": "github", "gitPath": "openapi.json"},
            }
        ),
        encoding="utf-8",
    )
    openapi = {
        "openapi": "3.0.0",
        "info": {"title": "t", "version": "1"},
        "paths": {"/a": {"get": {"responses": {"200": {"description": "ok"}}}}},
    }
    (pack / "contract" / "baseline.json").write_text(json.dumps(openapi), encoding="utf-8")
    (pack / "contract" / "target.json").write_text(json.dumps(openapi), encoding="utf-8")

    issues = validate_provider_pack_dir(pack)
    assert any("gitUrl" in i.message for i in _errors(issues))


def test_load_fails_for_invalid_pack_on_search_path(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    pack = tmp_path / "broken_load"
    pack.mkdir()
    (pack / "manifest.json").write_text(
        json.dumps({"id": "broken_load", "source": {"type": "manual"}, "languages": ["csharp"]}),
        encoding="utf-8",
    )
    monkeypatch.setenv("APIMORPH_PROVIDERS_PATH", str(tmp_path))

    with pytest.raises(ValueError, match="missing_file|Mandatory"):
        load_provider_pack("broken_load")
