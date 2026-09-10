from fastapi import APIRouter, HTTPException

from app.models.contracts import AnalyzeRequest, AnalyzeResponse
from app.services.analyzer import analyze_repository
from app.services.provider_pack import load_provider_pack

router = APIRouter(prefix="/v1")


@router.post("/analyze", response_model=AnalyzeResponse)
async def analyze(request: AnalyzeRequest) -> AnalyzeResponse:
    if request.contract_version != "1":
        raise HTTPException(status_code=400, detail="Unsupported contract version")

    if request.language != "csharp":
        raise HTTPException(status_code=400, detail="Unsupported language for MVP")

    try:
        load_provider_pack(request.provider)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc

    try:
        return analyze_repository(request)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
