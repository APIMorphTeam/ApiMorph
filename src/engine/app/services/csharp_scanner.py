import re
from pathlib import Path

from app.models.internal import CodeMatch, OpenApiChange

# Static Stripe + C# rules independent of OpenAPI diff.
STATIC_STRIPE_CSHARP_RULES: list[tuple[str, re.Pattern[str], str, str, str]] = [
    (
        "stripe.api-version.deprecated",
        re.compile(r'StripeConfiguration\.ApiVersion\s*=\s*"(?P<version>201[0-9]-[0-9]{2}-[0-9]{2})"'),
        "Deprecated Stripe API version configured in code",
        "high",
        "Migrate to a supported Stripe API version. See https://stripe.com/docs/upgrades",
    ),
    (
        "stripe.charge.source-deprecated",
        re.compile(r"\bSource\s*="),
        "ChargeCreateOptions.Source is deprecated; use PaymentMethod instead",
        "medium",
        "Replace Source with PaymentMethod when creating charges",
    ),
]


def scan_csharp_repository(repo_path: Path, openapi_changes: list[OpenApiChange]) -> list[CodeMatch]:
    matches: list[CodeMatch] = []
    csharp_files = sorted(repo_path.rglob("*.cs"))

    for file_path in csharp_files:
        if _should_skip_file(file_path):
            continue

        try:
            content = file_path.read_text(encoding="utf-8")
        except OSError:
            continue

        relative_path = _relative_path(repo_path, file_path)
        lines = content.splitlines()

        matches.extend(_scan_static_rules(relative_path, lines))
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


def _scan_static_rules(relative_path: str, lines: list[str]) -> list[CodeMatch]:
    matches: list[CodeMatch] = []

    for rule_id, pattern, message, confidence, _ in STATIC_STRIPE_CSHARP_RULES:
        for line_number, line in enumerate(lines, start=1):
            if pattern.search(line):
                matches.append(
                    CodeMatch(
                        rule_id=rule_id,
                        file_path=relative_path,
                        line=line_number,
                        message=message,
                        confidence=confidence,
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
