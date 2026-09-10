from pathlib import Path

import pytest
from httpx import ASGITransport, AsyncClient

from app.main import app
from app.services.provider_pack import list_provider_ids, load_provider_pack

MOCK_REPO = Path(__file__).resolve().parents[1] / "fixtures" / "mock-demo"


def test_load_stripe_and_mock_packs() -> None:
    ids = list_provider_ids()
    assert "stripe" in ids
    assert "mock" in ids

    stripe = load_provider_pack("stripe")
    assert stripe.baseline_path.is_file()
    assert stripe.target_path.is_file()
    assert any(r.rule_id == "stripe.api-version.deprecated" for r in stripe.csharp_rules)

    mock = load_provider_pack("mock")
    assert mock.id == "mock"
    assert any(r.rule_id == "mock.widget.legacy-client" for r in mock.csharp_rules)


@pytest.mark.asyncio
async def test_analyze_mock_provider_findings() -> None:
    transport = ASGITransport(app=app)
    payload = {
        "contractVersion": "1",
        "provider": "mock",
        "repositoryPath": str(MOCK_REPO),
        "language": "csharp",
        "options": {"detectOnly": True, "llmEnabled": False},
    }

    async with AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.post("/v1/analyze", json=payload)

    assert response.status_code == 200, response.text
    body = response.json()
    assert body["summary"]["findingCount"] >= 1
    rule_ids = {finding["ruleId"] for finding in body["findings"]}
    assert "mock.widget.legacy-client" in rule_ids or "mock.openapi.removed-operation" in rule_ids


@pytest.mark.asyncio
async def test_analyze_unknown_provider_returns_400() -> None:
    transport = ASGITransport(app=app)
    payload = {
        "contractVersion": "1",
        "provider": "does-not-exist",
        "repositoryPath": str(MOCK_REPO),
        "language": "csharp",
    }

    async with AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.post("/v1/analyze", json=payload)

    assert response.status_code == 400
    assert "Unknown provider" in response.json()["detail"] or "Known packs" in response.json()["detail"]
