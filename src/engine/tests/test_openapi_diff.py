from pathlib import Path

import pytest

from app.services.openapi_diff import diff_openapi_specs, load_openapi_spec

FIXTURES = Path(__file__).resolve().parents[1] / "fixtures" / "openapi"


def test_diff_detects_removed_refund_operation() -> None:
    baseline = load_openapi_spec(FIXTURES / "stripe_baseline.json")
    target = load_openapi_spec(FIXTURES / "stripe_target.json")

    changes = diff_openapi_specs(baseline, target)

    assert len(changes) == 1
    assert changes[0].rule_id == "stripe.openapi.removed-operation"
    assert changes[0].path == "/v1/charges/{charge}/refund"
    assert changes[0].method == "POST"


def test_diff_no_changes_when_specs_match() -> None:
    baseline = load_openapi_spec(FIXTURES / "stripe_target.json")
    target = load_openapi_spec(FIXTURES / "stripe_target.json")

    assert diff_openapi_specs(baseline, target) == []
