import json
from pathlib import Path
from typing import Any

from app.models.internal import OpenApiChange


def _iter_operations(spec: dict[str, Any]) -> dict[tuple[str, str], dict[str, Any]]:
    operations: dict[tuple[str, str], dict[str, Any]] = {}
    paths = spec.get("paths", {})
    if not isinstance(paths, dict):
        return operations

    for path, path_item in paths.items():
        if not isinstance(path_item, dict):
            continue
        for method in ("get", "post", "put", "patch", "delete", "head", "options"):
            operation = path_item.get(method)
            if isinstance(operation, dict):
                operations[(path.lower(), method.upper())] = operation

    return operations


def _operation_id(operation: dict[str, Any], path: str, method: str) -> str:
    return str(operation.get("operationId") or f"{method}:{path}")


def diff_openapi_specs(
    baseline: dict[str, Any],
    target: dict[str, Any],
    provider_id: str = "stripe",
) -> list[OpenApiChange]:
    """Return breaking changes when moving from baseline (old) to target (new) API."""
    baseline_ops = _iter_operations(baseline)
    target_ops = _iter_operations(target)
    changes: list[OpenApiChange] = []
    rule_id = f"{provider_id}.openapi.removed-operation"

    for key, operation in baseline_ops.items():
        path, method = key
        if key not in target_ops:
            operation_id = _operation_id(operation, path, method)
            changes.append(
                OpenApiChange(
                    change_type="removed_operation",
                    rule_id=rule_id,
                    path=path,
                    method=method,
                    message=f"Operation removed from API contract: {method} {path}",
                    csharp_patterns=_patterns_for_removed_operation(path, method, operation_id),
                )
            )

    return changes


def _patterns_for_removed_operation(path: str, method: str, operation_id: str) -> list[str]:
    patterns = [rf"\b{operation_id}\b"]
    path_lower = path.lower()

    # Generic: last path segment often matches client method names
    segment = path_lower.rstrip("/").split("/")[-1]
    if segment and "{" not in segment:
        # e.g. legacy-activate → LegacyActivate / legacy_activate
        camel = "".join(part.title() for part in segment.replace("_", "-").split("-"))
        patterns.append(rf"\b{camel}\b")
        patterns.append(rf"\b{segment.replace('-', '_')}\b")

    if "refund" in path_lower:
        patterns.extend(
            [
                r"\bRefundService\b",
                r"\.CreateAsync\s*\(",
                r"new\s+RefundCreateOptions\b",
            ]
        )

    if "/charges" in path_lower and method == "POST":
        patterns.extend([r"\bChargeService\b", r"ChargeCreateOptions"])

    if "legacy-activate" in path_lower or "legacyactivate" in path_lower.replace("-", ""):
        patterns.extend(
            [
                r"\bLegacyWidgetClient\b",
                r"\bLegacyActivateWidget\b",
                r"\.ActivateAsync\s*\(",
            ]
        )

    return patterns


def load_openapi_spec(path: Path) -> dict[str, Any]:
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)
