"""Load and validate ApiMorph provider packs (OpenAPI contracts + optional C# rules)."""

from __future__ import annotations

import json
import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

ALLOWED_SOURCE_TYPES = frozenset(
    {
        "manual",
        "openapi_url",
        "git",
        "github",
        "gitlab",
        "html_changelog",
    }
)

MANDATORY_RELATIVE_FILES = (
    "manifest.json",
    "contract/baseline.json",
    "contract/target.json",
)

ID_PATTERN = re.compile(r"^[a-z][a-z0-9_-]{1,63}$")


@dataclass(frozen=True)
class StaticCsharpRule:
    rule_id: str
    pattern: re.Pattern[str]
    message: str
    confidence: str
    guidance: str


@dataclass(frozen=True)
class PackValidationIssue:
    code: str
    message: str
    path: str | None = None

    def __str__(self) -> str:
        loc = f" ({self.path})" if self.path else ""
        return f"[{self.code}]{loc} {self.message}"


@dataclass
class ProviderPack:
    id: str
    display_name: str
    root: Path
    baseline_path: Path
    target_path: Path
    languages: list[str] = field(default_factory=lambda: ["csharp"])
    source_type: str = "manual"
    openapi_url: str | None = None
    git_url: str | None = None
    git_path: str | None = None
    changelog_url: str | None = None
    csharp_rules: list[StaticCsharpRule] = field(default_factory=list)
    feed_enabled: bool = False
    fingerprint_file: str = "contract/target.json"

    def fingerprint_path(self) -> Path:
        return self.root / self.fingerprint_file


def providers_search_roots() -> list[Path]:
    roots: list[Path] = []
    env = os.environ.get("APIMORPH_PROVIDERS_PATH", "").strip()
    if env:
        roots.append(Path(env))

    roots.extend(
        [
            Path("/providers"),
            Path("/config/providers"),
            Path("/app/providers"),
            Path(__file__).resolve().parents[4] / "providers",
            Path(__file__).resolve().parents[2] / "providers",
            Path.cwd() / "providers",
            Path.cwd() / "deploy" / "config" / "providers",
            Path.cwd() / "config" / "providers",
        ]
    )

    seen: set[str] = set()
    unique: list[Path] = []
    for root in roots:
        key = str(root.resolve()) if root.exists() else str(root)
        if key in seen:
            continue
        seen.add(key)
        unique.append(root)
    return unique


def list_provider_ids() -> list[str]:
    ids: set[str] = set()
    for root in providers_search_roots():
        if not root.is_dir():
            continue
        for child in root.iterdir():
            if child.is_dir() and (child / "manifest.json").is_file():
                ids.add(child.name.lower())
    legacy = Path(__file__).resolve().parents[2] / "fixtures" / "openapi"
    if (legacy / "stripe_baseline.json").is_file() and (legacy / "stripe_target.json").is_file():
        ids.add("stripe")
    return sorted(ids)


def load_provider_pack(provider_id: str) -> ProviderPack:
    provider_id = provider_id.strip().lower()
    if not provider_id:
        raise ValueError("Provider id is required")

    for root in providers_search_roots():
        pack_dir = root / provider_id
        manifest_path = pack_dir / "manifest.json"
        if not manifest_path.is_file():
            continue
        issues = validate_provider_pack_dir(pack_dir)
        errors = [i for i in issues if i.code != "warning"]
        if errors:
            raise ValueError("; ".join(str(i) for i in errors))
        return _load_from_pack_dir(pack_dir, manifest_path)

    legacy = _try_legacy_fixtures(provider_id)
    if legacy is not None:
        return legacy

    known = ", ".join(list_provider_ids()) or "(none)"
    raise ValueError(f"Unknown provider '{provider_id}'. Known packs: {known}")


def validate_provider_pack_dir(pack_dir: Path) -> list[PackValidationIssue]:
    """Validate pack layout, manifest source rules, and OpenAPI JSON shape."""
    issues: list[PackValidationIssue] = []
    pack_dir = Path(pack_dir)

    if not pack_dir.is_dir():
        return [PackValidationIssue("missing_pack", f"Pack directory does not exist: {pack_dir}")]

    folder_id = pack_dir.name.lower()
    for relative in MANDATORY_RELATIVE_FILES:
        path = pack_dir / relative
        if not path.is_file():
            issues.append(
                PackValidationIssue(
                    "missing_file",
                    f"Mandatory file missing: {relative}",
                    path=relative,
                )
            )

    manifest_path = pack_dir / "manifest.json"
    manifest: dict[str, Any] | None = None
    if manifest_path.is_file():
        try:
            with manifest_path.open(encoding="utf-8") as handle:
                loaded = json.load(handle)
            if not isinstance(loaded, dict):
                issues.append(
                    PackValidationIssue("invalid_manifest", "manifest.json must be a JSON object", "manifest.json")
                )
            else:
                manifest = loaded
        except json.JSONDecodeError as exc:
            issues.append(
                PackValidationIssue("invalid_manifest", f"manifest.json is not valid JSON: {exc}", "manifest.json")
            )

    if manifest is not None:
        issues.extend(_validate_manifest(manifest, folder_id))

    for relative in ("contract/baseline.json", "contract/target.json"):
        path = pack_dir / relative
        if path.is_file():
            issues.extend(_validate_openapi_file(path, relative))

    rules_path = pack_dir / "rules" / "csharp.json"
    if rules_path.is_file():
        issues.extend(_validate_csharp_rules_file(rules_path))
    else:
        issues.append(
            PackValidationIssue(
                "warning",
                "Optional rules/csharp.json is missing (static C# rules will be empty)",
                "rules/csharp.json",
            )
        )

    provider_md = pack_dir / "PROVIDER.md"
    if not provider_md.is_file():
        issues.append(
            PackValidationIssue(
                "warning",
                "Optional PROVIDER.md is missing (recommended for humans/agents)",
                "PROVIDER.md",
            )
        )

    return issues


def validate_shipped_providers(providers_root: Path | None = None) -> dict[str, list[PackValidationIssue]]:
    """Validate every pack under a providers root (defaults to repo providers/)."""
    root = providers_root or Path(__file__).resolve().parents[4] / "providers"
    results: dict[str, list[PackValidationIssue]] = {}
    if not root.is_dir():
        return results

    for child in sorted(root.iterdir(), key=lambda p: p.name.lower()):
        if child.is_dir() and (child / "manifest.json").is_file():
            results[child.name] = validate_provider_pack_dir(child)
    return results


def _validate_manifest(manifest: dict[str, Any], folder_id: str) -> list[PackValidationIssue]:
    issues: list[PackValidationIssue] = []
    pack_id = str(manifest.get("id") or "").strip().lower()
    if not pack_id:
        issues.append(PackValidationIssue("invalid_manifest", "manifest.id is required", "manifest.json"))
    elif not ID_PATTERN.match(pack_id):
        issues.append(
            PackValidationIssue(
                "invalid_id",
                f"manifest.id '{pack_id}' must match ^[a-z][a-z0-9_-]{{1,63}}$",
                "manifest.json",
            )
        )
    elif pack_id != folder_id:
        issues.append(
            PackValidationIssue(
                "id_mismatch",
                f"manifest.id '{pack_id}' does not match folder name '{folder_id}'",
                "manifest.json",
            )
        )

    languages = manifest.get("languages")
    if languages is not None and (not isinstance(languages, list) or not languages):
        issues.append(
            PackValidationIssue("invalid_manifest", "manifest.languages must be a non-empty list", "manifest.json")
        )

    source = manifest.get("source") if isinstance(manifest.get("source"), dict) else {}
    source_type = str(source.get("type") or "manual").strip().lower()
    if source_type not in ALLOWED_SOURCE_TYPES:
        issues.append(
            PackValidationIssue(
                "invalid_source",
                f"source.type '{source_type}' is not allowed; use one of: {', '.join(sorted(ALLOWED_SOURCE_TYPES))}",
                "manifest.json",
            )
        )
    else:
        issues.extend(_validate_source_fields(source_type, source))

    feed = manifest.get("feed") if isinstance(manifest.get("feed"), dict) else {}
    fingerprint = str(feed.get("fingerprintFile") or "contract/target.json")
    if ".." in fingerprint.replace("\\", "/").split("/"):
        issues.append(
            PackValidationIssue(
                "invalid_feed",
                "feed.fingerprintFile must be a relative path without '..'",
                "manifest.json",
            )
        )

    return issues


def _validate_source_fields(source_type: str, source: dict[str, Any]) -> list[PackValidationIssue]:
    issues: list[PackValidationIssue] = []

    def require_url(field: str, code: str = "invalid_source") -> None:
        value = source.get(field)
        if not isinstance(value, str) or not value.strip():
            issues.append(
                PackValidationIssue(
                    code,
                    f"source.type '{source_type}' requires non-empty source.{field}",
                    "manifest.json",
                )
            )
        elif not value.strip().lower().startswith(("http://", "https://", "git@")):
            issues.append(
                PackValidationIssue(
                    "invalid_source",
                    f"source.{field} must look like an http(s) or git URL",
                    "manifest.json",
                )
            )

    if source_type == "manual":
        return issues

    if source_type == "openapi_url":
        require_url("openapiUrl")
        return issues

    if source_type in {"git", "github", "gitlab"}:
        require_url("gitUrl")
        git_path = source.get("gitPath")
        if git_path is not None and not isinstance(git_path, str):
            issues.append(
                PackValidationIssue("invalid_source", "source.gitPath must be a string when set", "manifest.json")
            )
        return issues

    if source_type == "html_changelog":
        # Assist-only: local contracts remain mandatory; URL points at docs/changelog to review.
        changelog = source.get("changelogUrl") or source.get("htmlUrl")
        if not isinstance(changelog, str) or not changelog.strip():
            issues.append(
                PackValidationIssue(
                    "invalid_source",
                    "source.type 'html_changelog' requires source.changelogUrl (or htmlUrl)",
                    "manifest.json",
                )
            )
        elif not changelog.strip().lower().startswith(("http://", "https://")):
            issues.append(
                PackValidationIssue(
                    "invalid_source",
                    "source.changelogUrl must be an http(s) URL",
                    "manifest.json",
                )
            )
        issues.append(
            PackValidationIssue(
                "warning",
                "html_changelog is assist-only: contracts must still be filled manually after review",
                "manifest.json",
            )
        )

    return issues


def _validate_openapi_file(path: Path, relative: str) -> list[PackValidationIssue]:
    issues: list[PackValidationIssue] = []
    try:
        with path.open(encoding="utf-8") as handle:
            data = json.load(handle)
    except json.JSONDecodeError as exc:
        return [PackValidationIssue("invalid_openapi", f"Invalid JSON: {exc}", relative)]

    if not isinstance(data, dict):
        return [PackValidationIssue("invalid_openapi", "OpenAPI document must be a JSON object", relative)]

    if "openapi" not in data and "swagger" not in data:
        issues.append(
            PackValidationIssue(
                "invalid_openapi",
                "OpenAPI document should include 'openapi' (3.x) or 'swagger' (2.x)",
                relative,
            )
        )

    paths = data.get("paths")
    if not isinstance(paths, dict):
        issues.append(
            PackValidationIssue("invalid_openapi", "OpenAPI document must include a 'paths' object", relative)
        )
    elif len(paths) == 0:
        issues.append(PackValidationIssue("warning", "OpenAPI 'paths' object is empty", relative))

    return issues


def _validate_csharp_rules_file(path: Path) -> list[PackValidationIssue]:
    issues: list[PackValidationIssue] = []
    relative = "rules/csharp.json"
    try:
        with path.open(encoding="utf-8") as handle:
            raw = json.load(handle)
    except json.JSONDecodeError as exc:
        return [PackValidationIssue("invalid_rules", f"Invalid JSON: {exc}", relative)]

    if not isinstance(raw, list):
        return [PackValidationIssue("invalid_rules", "rules/csharp.json must be a JSON array", relative)]

    for index, item in enumerate(raw):
        if not isinstance(item, dict):
            issues.append(PackValidationIssue("invalid_rules", f"Rule #{index} must be an object", relative))
            continue
        if not item.get("ruleId") or not item.get("pattern"):
            issues.append(
                PackValidationIssue(
                    "invalid_rules",
                    f"Rule #{index} requires ruleId and pattern",
                    relative,
                )
            )
            continue
        try:
            re.compile(str(item["pattern"]))
        except re.error as exc:
            issues.append(
                PackValidationIssue(
                    "invalid_rules",
                    f"Rule #{index} pattern is not valid regex: {exc}",
                    relative,
                )
            )

    return issues


def _load_from_pack_dir(pack_dir: Path, manifest_path: Path) -> ProviderPack:
    with manifest_path.open(encoding="utf-8") as handle:
        manifest: dict[str, Any] = json.load(handle)

    pack_id = str(manifest.get("id") or pack_dir.name).lower()
    baseline = pack_dir / "contract" / "baseline.json"
    target = pack_dir / "contract" / "target.json"
    if not baseline.is_file() or not target.is_file():
        raise ValueError(f"Provider pack '{pack_id}' missing contract/baseline.json or contract/target.json")

    source = manifest.get("source") if isinstance(manifest.get("source"), dict) else {}
    feed = manifest.get("feed") if isinstance(manifest.get("feed"), dict) else {}
    languages = manifest.get("languages") if isinstance(manifest.get("languages"), list) else ["csharp"]
    changelog = source.get("changelogUrl") or source.get("htmlUrl")

    return ProviderPack(
        id=pack_id,
        display_name=str(manifest.get("displayName") or pack_id),
        root=pack_dir,
        baseline_path=baseline,
        target_path=target,
        languages=[str(x) for x in languages],
        source_type=str(source.get("type") or "manual").lower(),
        openapi_url=_optional_str(source.get("openapiUrl")),
        git_url=_optional_str(source.get("gitUrl")),
        git_path=_optional_str(source.get("gitPath")),
        changelog_url=_optional_str(changelog),
        csharp_rules=_load_csharp_rules(pack_dir / "rules" / "csharp.json"),
        feed_enabled=bool(feed.get("enabled", False)),
        fingerprint_file=str(feed.get("fingerprintFile") or "contract/target.json"),
    )


def _optional_str(value: Any) -> str | None:
    if isinstance(value, str) and value.strip():
        return value.strip()
    return None


def _try_legacy_fixtures(provider_id: str) -> ProviderPack | None:
    if provider_id != "stripe":
        return None

    fixtures = Path(__file__).resolve().parents[2] / "fixtures" / "openapi"
    baseline = fixtures / "stripe_baseline.json"
    target = fixtures / "stripe_target.json"
    if not baseline.is_file() or not target.is_file():
        return None

    rules = [
        StaticCsharpRule(
            rule_id="stripe.api-version.deprecated",
            pattern=re.compile(
                r'StripeConfiguration\.ApiVersion\s*=\s*"(?P<version>201[0-9]-[0-9]{2}-[0-9]{2})"'
            ),
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

    return ProviderPack(
        id="stripe",
        display_name="Stripe",
        root=fixtures,
        baseline_path=baseline,
        target_path=target,
        csharp_rules=rules,
        feed_enabled=True,
        fingerprint_file="stripe_target.json",
    )


def _load_csharp_rules(path: Path) -> list[StaticCsharpRule]:
    if not path.is_file():
        return []

    with path.open(encoding="utf-8") as handle:
        raw = json.load(handle)

    if not isinstance(raw, list):
        raise ValueError(f"Expected list in {path}")

    rules: list[StaticCsharpRule] = []
    for item in raw:
        if not isinstance(item, dict):
            continue
        rule_id = str(item.get("ruleId") or "")
        pattern = str(item.get("pattern") or "")
        if not rule_id or not pattern:
            continue
        rules.append(
            StaticCsharpRule(
                rule_id=rule_id,
                pattern=re.compile(pattern),
                message=str(item.get("message") or rule_id),
                confidence=str(item.get("confidence") or "medium"),
                guidance=str(item.get("guidance") or ""),
            )
        )
    return rules
