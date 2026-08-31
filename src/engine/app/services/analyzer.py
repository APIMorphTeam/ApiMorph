from pathlib import Path

from app.models.contracts import AnalyzeRequest, AnalyzeResponse, AnalyzeSummary, Finding
from app.services.csharp_scanner import scan_csharp_repository
from app.services.openapi_diff import diff_openapi_specs, load_openapi_spec

_FIXTURES_DIR = Path(__file__).resolve().parents[2] / "fixtures" / "openapi"


def analyze_repository(request: AnalyzeRequest) -> AnalyzeResponse:
    repo_path = Path(request.repository_path)
    if not repo_path.exists() or not repo_path.is_dir():
        raise ValueError(f"Repository path does not exist: {request.repository_path}")

    baseline_path, target_path = _resolve_openapi_paths(request)
    baseline_spec = load_openapi_spec(baseline_path)
    target_spec = load_openapi_spec(target_path)
    openapi_changes = diff_openapi_specs(baseline_spec, target_spec)

    code_matches = scan_csharp_repository(repo_path, openapi_changes)
    files_scanned = sum(1 for path in repo_path.rglob("*.cs") if path.is_file() and "obj" not in path.parts)

    findings = [
        Finding(
            rule_id=match.rule_id,
            file_path=match.file_path,
            line=match.line,
            message=match.message,
            confidence=match.confidence,
            evidence=match.evidence,
        )
        for match in code_matches
    ]

    return AnalyzeResponse(
        contract_version="1",
        findings=findings,
        summary=AnalyzeSummary(files_scanned=files_scanned, finding_count=len(findings)),
    )


def _resolve_openapi_paths(request: AnalyzeRequest) -> tuple[Path, Path]:
    options = request.options

    if options.openapi_baseline_path and options.openapi_target_path:
        return Path(options.openapi_baseline_path), Path(options.openapi_target_path)

    if request.provider == "stripe":
        return (
            _FIXTURES_DIR / "stripe_baseline.json",
            _FIXTURES_DIR / "stripe_target.json",
        )

    raise ValueError(f"No OpenAPI fixtures configured for provider: {request.provider}")
