"""Deterministic code patches for known Stripe + C# migration rules."""

from __future__ import annotations

import re
from pathlib import Path

from app.models.contracts import FilePatch, Finding

CURRENT_STRIPE_API_VERSION = "2024-11-20.acacia"

_API_VERSION_PATTERN = re.compile(
    r'(StripeConfiguration\.ApiVersion\s*=\s*")(?P<version>201[0-9]-[0-9]{2}-[0-9]{2})(")'
)
_SOURCE_PATTERN = re.compile(r"\bSource\s*=")
_REFUND_METHOD_PATTERN = re.compile(r"(    public async Task RefundChargeAsync)")

_APIMORPH_REFUND_MARKER = "    // ApiMorph: Migrate refunds to PaymentIntent API"


def generate_deterministic_patches(repo_path: Path, findings: list[Finding]) -> list[FilePatch]:
    """Return one patch per changed file with the full updated file content."""
    file_paths = sorted({finding.file_path for finding in findings})
    patches: list[FilePatch] = []

    for relative_path in file_paths:
        full_path = repo_path / relative_path
        if not full_path.is_file():
            continue

        original = full_path.read_text(encoding="utf-8")
        file_findings = [finding for finding in findings if finding.file_path == relative_path]
        updated, linked_rule_ids, descriptions = _apply_deterministic_rules(original, file_findings)

        if updated == original:
            continue

        patches.append(
            FilePatch(
                file_path=relative_path,
                patch_type="deterministic",
                description="; ".join(descriptions),
                content=updated,
                linked_rule_ids=linked_rule_ids,
            )
        )

    return patches


def _apply_deterministic_rules(
    content: str,
    findings: list[Finding],
) -> tuple[str, list[str], list[str]]:
    updated = content
    linked_rule_ids: list[str] = []
    descriptions: list[str] = []
    rule_ids = {finding.rule_id for finding in findings}

    if "stripe.api-version.deprecated" in rule_ids:
        patched, changed = _patch_api_version(updated)
        if changed:
            updated = patched
            linked_rule_ids.append("stripe.api-version.deprecated")
            descriptions.append(f"Update Stripe API version to {CURRENT_STRIPE_API_VERSION}")

    if "stripe.charge.source-deprecated" in rule_ids:
        patched, changed = _patch_charge_source(updated)
        if changed:
            updated = patched
            linked_rule_ids.append("stripe.charge.source-deprecated")
            descriptions.append("Replace ChargeCreateOptions.Source with PaymentMethod")

    if "stripe.openapi.removed-operation" in rule_ids:
        patched, changed = _patch_refund_operation_guidance(updated)
        if changed:
            updated = patched
            linked_rule_ids.append("stripe.openapi.removed-operation")
            descriptions.append("Add migration guidance for removed charge refund endpoint")

    return updated, linked_rule_ids, descriptions


def _patch_api_version(content: str) -> tuple[str, bool]:
    def replace(match: re.Match[str]) -> str:
        return f'{match.group(1)}{CURRENT_STRIPE_API_VERSION}{match.group(3)}'

    updated, count = _API_VERSION_PATTERN.subn(replace, content, count=1)
    return updated, count > 0


def _patch_charge_source(content: str) -> tuple[str, bool]:
    if "PaymentMethod =" in content and "Source =" not in content:
        return content, False

    updated, count = _SOURCE_PATTERN.subn("PaymentMethod =", content, count=1)
    return updated, count > 0


def _patch_refund_operation_guidance(content: str) -> tuple[str, bool]:
    if _APIMORPH_REFUND_MARKER in content:
        return content, False

    if "RefundService" not in content:
        return content, False

    updated, count = _REFUND_METHOD_PATTERN.subn(
        (
            f"{_APIMORPH_REFUND_MARKER}\n"
            "    // The charge refund endpoint was removed from the Stripe OpenAPI contract.\n"
            "    // See: https://stripe.com/docs/refunds\n"
            r"\1"
        ),
        content,
        count=1,
    )
    return updated, count > 0
