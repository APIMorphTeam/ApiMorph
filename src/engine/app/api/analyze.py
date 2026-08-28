from fastapi import APIRouter, HTTPException

from app.models.contracts import AnalyzeRequest, AnalyzeResponse
from app.services.analyzer import analyze_repository

router = APIRouter(prefix="/v1")


@router.post("/analyze", response_model=AnalyzeResponse)
async def analyze(request: AnalyzeRequest) -> AnalyzeResponse:
    if request.contract_version != "1":
        raise HTTPException(status_code=400, detail="Unsupported contract version")

    if request.provider != "stripe":
        raise HTTPException(status_code=400, detail="Unsupported provider for MVP")

    if request.language != "csharp":
        raise HTTPException(status_code=400, detail="Unsupported language for MVP")

    return analyze_repository(request)
