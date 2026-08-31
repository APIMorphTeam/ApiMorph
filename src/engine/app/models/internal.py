"""Internal models used by the analysis engine (not part of the public API contract)."""

from dataclasses import dataclass, field


@dataclass(frozen=True)
class OpenApiChange:
    change_type: str
    rule_id: str
    path: str
    method: str
    message: str
    csharp_patterns: list[str] = field(default_factory=list)


@dataclass(frozen=True)
class CodeMatch:
    rule_id: str
    file_path: str
    line: int
    message: str
    confidence: str
    evidence: str
