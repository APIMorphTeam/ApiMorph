from pydantic import BaseModel, Field


class AnalyzeOptions(BaseModel):
    detect_only: bool = Field(default=True, alias="detectOnly")
    llm_enabled: bool = Field(default=False, alias="llmEnabled")
    openapi_baseline_path: str | None = Field(default=None, alias="openApiBaselinePath")
    openapi_target_path: str | None = Field(default=None, alias="openApiTargetPath")

    model_config = {"populate_by_name": True}


class AnalyzeRequest(BaseModel):
    contract_version: str = Field(alias="contractVersion")
    provider: str
    repository_path: str = Field(alias="repositoryPath")
    language: str
    options: AnalyzeOptions = Field(default_factory=AnalyzeOptions)

    model_config = {"populate_by_name": True}


class Finding(BaseModel):
    rule_id: str = Field(alias="ruleId")
    file_path: str = Field(alias="filePath")
    line: int
    message: str
    confidence: str
    evidence: str | None = None

    model_config = {"populate_by_name": True}


class AnalyzeSummary(BaseModel):
    files_scanned: int = Field(alias="filesScanned")
    finding_count: int = Field(alias="findingCount")

    model_config = {"populate_by_name": True}


class AnalyzeResponse(BaseModel):
    contract_version: str = Field(alias="contractVersion")
    findings: list[Finding] = Field(default_factory=list)
    summary: AnalyzeSummary

    model_config = {"populate_by_name": True}


class HealthResponse(BaseModel):
    status: str
