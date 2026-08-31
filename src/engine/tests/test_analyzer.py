from pathlib import Path

import pytest
from httpx import ASGITransport, AsyncClient

from app.main import app

FIXTURES_REPO = Path(__file__).resolve().parents[1] / "fixtures" / "stripe-demo"


@pytest.mark.asyncio
async def test_analyze_returns_findings_for_demo_repo() -> None:
    transport = ASGITransport(app=app)
    payload = {
        "contractVersion": "1",
        "provider": "stripe",
        "repositoryPath": str(FIXTURES_REPO),
        "language": "csharp",
        "options": {"detectOnly": True, "llmEnabled": False},
    }

    async with AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.post("/v1/analyze", json=payload)

    assert response.status_code == 200
    body = response.json()
    assert body["summary"]["findingCount"] >= 3
    rule_ids = {finding["ruleId"] for finding in body["findings"]}
    assert "stripe.api-version.deprecated" in rule_ids
    assert "stripe.charge.source-deprecated" in rule_ids
    assert "stripe.openapi.removed-operation" in rule_ids


@pytest.mark.asyncio
async def test_analyze_returns_400_for_missing_repo() -> None:
    transport = ASGITransport(app=app)
    payload = {
        "contractVersion": "1",
        "provider": "stripe",
        "repositoryPath": "/does/not/exist",
        "language": "csharp",
    }

    async with AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.post("/v1/analyze", json=payload)

    assert response.status_code == 400
