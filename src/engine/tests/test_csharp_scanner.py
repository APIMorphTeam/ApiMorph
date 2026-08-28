from pathlib import Path

from app.services.csharp_scanner import scan_csharp_repository
from app.services.openapi_diff import diff_openapi_specs, load_openapi_spec

FIXTURES = Path(__file__).resolve().parents[1] / "fixtures"
OPENAPI = FIXTURES / "openapi"
REPO = FIXTURES / "repos" / "stripe-demo"


def test_scan_detects_deprecated_api_version_and_source() -> None:
    baseline = load_openapi_spec(OPENAPI / "stripe_baseline.json")
    target = load_openapi_spec(OPENAPI / "stripe_target.json")
    changes = diff_openapi_specs(baseline, target)

    matches = scan_csharp_repository(REPO, changes)
    rule_ids = {match.rule_id for match in matches}

    assert "stripe.api-version.deprecated" in rule_ids
    assert "stripe.charge.source-deprecated" in rule_ids


def test_scan_detects_removed_refund_operation_usage() -> None:
    baseline = load_openapi_spec(OPENAPI / "stripe_baseline.json")
    target = load_openapi_spec(OPENAPI / "stripe_target.json")
    changes = diff_openapi_specs(baseline, target)

    matches = scan_csharp_repository(REPO, changes)
    refund_matches = [m for m in matches if m.rule_id == "stripe.openapi.removed-operation"]

    assert len(refund_matches) >= 1
    assert any("RefundService" in m.evidence for m in refund_matches)
