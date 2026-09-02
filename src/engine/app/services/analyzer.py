from pathlib import Path

from app.models.contracts import AnalyzeRequest, AnalyzeResponse, AnalyzeSummary, FilePatch, Finding
from app.services.csharp_scanner import scan_csharp_repository
from app.services.llm_adapter import is_llm_configured, load_llm_settings, propose_file_patch
from app.services.openapi_diff import diff_openapi_specs, load_openapi_spec
from app.services.patch_generator import generate_deterministic_patches

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

    patches: list[FilePatch] = []
    patch_mode = "detect-only"

    if not request.options.detect_only:
        patches = generate_deterministic_patches(repo_path, findings)
        patch_mode = "deterministic" if patches else "detect-only"

        if request.options.llm_enabled and is_llm_configured():
            patches = _apply_llm_patches(repo_path, findings, patches)
            patch_mode = _resolve_patch_mode(patches)

    return AnalyzeResponse(
        contract_version="1",
        findings=findings,
        patches=patches,
        summary=AnalyzeSummary(
            files_scanned=files_scanned,
            finding_count=len(findings),
            patch_count=len(patches),
            patch_mode=patch_mode,
        ),
    )


def _apply_llm_patches(
    repo_path: Path,
    findings: list[Finding],
    deterministic_patches: list[FilePatch],
) -> list[FilePatch]:
    settings = load_llm_settings()
    patched_by_path = {patch.file_path: patch for patch in deterministic_patches}
    file_paths = sorted({finding.file_path for finding in findings})

    for relative_path in file_paths:
        full_path = repo_path / relative_path
        if not full_path.is_file():
            continue

        file_findings = [finding for finding in findings if finding.file_path == relative_path]
        current_content = patched_by_path[relative_path].content if relative_path in patched_by_path else full_path.read_text(
            encoding="utf-8"
        )

        llm_content = propose_file_patch(
            relative_path=relative_path,
            file_content=current_content,
            findings=file_findings,
            settings=settings,
        )
        if not llm_content or llm_content == current_content:
            continue

        linked_rule_ids = sorted({finding.rule_id for finding in file_findings})
        patched_by_path[relative_path] = FilePatch(
            file_path=relative_path,
            patch_type="llm-assisted",
            description="LLM-assisted migration patch",
            content=llm_content,
            linked_rule_ids=linked_rule_ids,
        )

    return list(patched_by_path.values())


def _resolve_patch_mode(patches: list[FilePatch]) -> str:
    if not patches:
        return "detect-only"

    patch_types = {patch.patch_type for patch in patches}
    if patch_types == {"deterministic"}:
        return "deterministic"
    if patch_types == {"llm-assisted"}:
        return "llm-assisted"
    return "mixed"


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
