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


class FilePatch(BaseModel):
    file_path: str = Field(alias="filePath")
    patch_type: str = Field(alias="patchType")
    description: str
    content: str
    linked_rule_ids: list[str] = Field(default_factory=list, alias="linkedRuleIds")

    model_config = {"populate_by_name": True}


class AnalyzeSummary(BaseModel):
    files_scanned: int = Field(alias="filesScanned")
    finding_count: int = Field(alias="findingCount")
    patch_count: int = Field(default=0, alias="patchCount")
    patch_mode: str = Field(default="detect-only", alias="patchMode")

    model_config = {"populate_by_name": True}


class AnalyzeResponse(BaseModel):
    contract_version: str = Field(alias="contractVersion")
    findings: list[Finding] = Field(default_factory=list)
    patches: list[FilePatch] = Field(default_factory=list)
    summary: AnalyzeSummary

    model_config = {"populate_by_name": True}


class HealthResponse(BaseModel):
    status: str
