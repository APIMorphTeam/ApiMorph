from pathlib import Path

from app.services.patch_generator import (
    CURRENT_STRIPE_API_VERSION,
    generate_deterministic_patches,
)
from app.models.contracts import Finding


FIXTURES = Path(__file__).resolve().parents[1] / "fixtures"
REPO = FIXTURES / "stripe-demo"
PAYMENT_SERVICE = REPO / "Services" / "PaymentService.cs"


def _payment_findings() -> list[Finding]:
    return [
        Finding(
            rule_id="stripe.api-version.deprecated",
            file_path="Services/PaymentService.cs",
            line=9,
            message="Deprecated Stripe API version configured in code",
            confidence="high",
            evidence='StripeConfiguration.ApiVersion = "2019-12-03";',
        ),
        Finding(
            rule_id="stripe.charge.source-deprecated",
            file_path="Services/PaymentService.cs",
            line=16,
            message="ChargeCreateOptions.Source is deprecated; use PaymentMethod instead",
            confidence="medium",
            evidence="Source = token,",
        ),
        Finding(
            rule_id="stripe.openapi.removed-operation",
            file_path="Services/PaymentService.cs",
            line=22,
            message="Operation removed from API contract: POST /v1/charges/{charge}/refund",
            confidence="medium",
            evidence="var refundService = new RefundService();",
        ),
    ]


def test_generate_deterministic_patches_updates_payment_service() -> None:
    patches = generate_deterministic_patches(REPO, _payment_findings())

    assert len(patches) == 1
    patch = patches[0]
    assert patch.file_path == "Services/PaymentService.cs"
    assert patch.patch_type == "deterministic"
    assert CURRENT_STRIPE_API_VERSION in patch.content
    assert "PaymentMethod = token" in patch.content
    assert "ApiMorph: Migrate refunds to PaymentIntent API" in patch.content
    assert "Source = token" not in patch.content
