import tempfile
from pathlib import Path

import pytest
from httpx import ASGITransport, AsyncClient

from app.main import app


@pytest.mark.asyncio
async def test_health_returns_ok() -> None:
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


@pytest.mark.asyncio
async def test_analyze_returns_empty_findings_for_clean_repo() -> None:
    with tempfile.TemporaryDirectory() as temp_dir:
        Path(temp_dir, "Program.cs").write_text("Console.WriteLine(\"hello\");", encoding="utf-8")

        transport = ASGITransport(app=app)
        payload = {
            "contractVersion": "1",
            "provider": "stripe",
            "repositoryPath": temp_dir,
            "language": "csharp",
            "options": {"detectOnly": True, "llmEnabled": False},
        }

        async with AsyncClient(transport=transport, base_url="http://test") as client:
            response = await client.post("/v1/analyze", json=payload)

    assert response.status_code == 200
    body = response.json()
    assert body["contractVersion"] == "1"
    assert body["findings"] == []
    assert body["summary"]["findingCount"] == 0
