import re
from pathlib import Path

from app.models.internal import CodeMatch, OpenApiChange
from app.services.provider_pack import StaticCsharpRule

# Fallback when a pack has no rules/csharp.json (legacy Stripe-only).
STATIC_STRIPE_CSHARP_RULES: list[StaticCsharpRule] = [
    StaticCsharpRule(
        rule_id="stripe.api-version.deprecated",
        pattern=re.compile(r'StripeConfiguration\.ApiVersion\s*=\s*"(?P<version>201[0-9]-[0-9]{2}-[0-9]{2})"'),
        message="Deprecated Stripe API version configured in code",
        confidence="high",
        guidance="Migrate to a supported Stripe API version. See https://stripe.com/docs/upgrades",
    ),
    StaticCsharpRule(
        rule_id="stripe.charge.source-deprecated",
        pattern=re.compile(r"\bSource\s*="),
        message="ChargeCreateOptions.Source is deprecated; use PaymentMethod instead",
        confidence="medium",
        guidance="Replace Source with PaymentMethod when creating charges",
    ),
]


def scan_csharp_repository(
    repo_path: Path,
    openapi_changes: list[OpenApiChange],
    static_rules: list[StaticCsharpRule] | None = None,
) -> list[CodeMatch]:
    matches: list[CodeMatch] = []
    csharp_files = sorted(repo_path.rglob("*.cs"))
    rules = static_rules if static_rules is not None else STATIC_STRIPE_CSHARP_RULES

    for file_path in csharp_files:
        if _should_skip_file(file_path):
            continue

        try:
            content = file_path.read_text(encoding="utf-8")
        except OSError:
            continue

        relative_path = _relative_path(repo_path, file_path)
        lines = content.splitlines()

        matches.extend(_scan_static_rules(relative_path, lines, rules))
        matches.extend(_scan_openapi_rules(relative_path, lines, openapi_changes))

    return _deduplicate_matches(matches)


def _should_skip_file(file_path: Path) -> bool:
    parts = {part.lower() for part in file_path.parts}
    return bool(parts.intersection({"bin", "obj", ".git", "node_modules"}))


def _relative_path(repo_path: Path, file_path: Path) -> str:
    try:
        return file_path.relative_to(repo_path).as_posix()
    except ValueError:
        return file_path.as_posix()


def _scan_static_rules(
    relative_path: str,
    lines: list[str],
    rules: list[StaticCsharpRule],
) -> list[CodeMatch]:
    matches: list[CodeMatch] = []

    for rule in rules:
        for line_number, line in enumerate(lines, start=1):
            if rule.pattern.search(line):
                matches.append(
                    CodeMatch(
                        rule_id=rule.rule_id,
                        file_path=relative_path,
                        line=line_number,
                        message=rule.message,
                        confidence=rule.confidence,
                        evidence=line.strip(),
                    )
                )

    return matches


def _scan_openapi_rules(
    relative_path: str,
    lines: list[str],
    openapi_changes: list[OpenApiChange],
) -> list[CodeMatch]:
    matches: list[CodeMatch] = []

    for change in openapi_changes:
        if not change.csharp_patterns:
            continue

        compiled = [re.compile(pattern) for pattern in change.csharp_patterns]

        for line_number, line in enumerate(lines, start=1):
            if any(pattern.search(line) for pattern in compiled):
                matches.append(
                    CodeMatch(
                        rule_id=change.rule_id,
                        file_path=relative_path,
                        line=line_number,
                        message=change.message,
                        confidence="medium",
                        evidence=line.strip(),
                    )
                )

    return matches


def _deduplicate_matches(matches: list[CodeMatch]) -> list[CodeMatch]:
    seen: set[tuple[str, str, int, str]] = set()
    unique: list[CodeMatch] = []

    for match in matches:
        key = (match.rule_id, match.file_path, match.line, match.message)
        if key in seen:
            continue
        seen.add(key)
        unique.append(match)

    return unique
