from pathlib import Path

from app.models.contracts import AnalyzeRequest, AnalyzeResponse, AnalyzeSummary


def analyze_repository(request: AnalyzeRequest) -> AnalyzeResponse:
    """Stage 2 stub: validate input and return an empty result set."""
    repo_path = Path(request.repository_path)
    files_scanned = 0

    if repo_path.exists() and repo_path.is_dir():
        files_scanned = sum(1 for path in repo_path.rglob("*") if path.is_file())

    return AnalyzeResponse(
        contract_version="1",
        findings=[],
        summary=AnalyzeSummary(files_scanned=files_scanned, finding_count=0),
    )
