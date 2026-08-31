# ApiMorph

[![CI](https://github.com/YOUR_ORG/ApiMorph/actions/workflows/ci.yml/badge.svg)](https://github.com/YOUR_ORG/ApiMorph/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

Self-hosted agent that maps API provider contract changes to your codebase and opens draft PRs with safe migrations.

> Dependabot for API logic. MVP: **Stripe** × **C#**.

## Why

API breaking changes ship quietly. Changelogs go unread. Useful features launch unnoticed. ApiMorph connects provider contract changes to customer repositories and proposes the fix as a reviewable PR.

## MVP scope

- **Provider:** Stripe (OpenAPI / API version + `Stripe.net` signals)
- **Language:** C#
- **Mode:** detect → report → draft PR (human review required)
- **Deploy:** Docker Compose, on-prem / self-hosted
- **Orchestrator:** C# / .NET 9
- **Engine:** Python / FastAPI
- **LLM:** optional BYOK or Ollama (detection works without LLM)

## Non-goals

Not a general coding agent. Not auto-merge. Not "every API". Customer source code does not leave the customer network.

## Prerequisites

- Docker
- .NET 9 SDK
- Python 3.12+ (for local engine development)

## Quick start

```bash
git clone https://github.com/YOUR_ORG/ApiMorph.git
cd ApiMorph

dotnet build

cd deploy
docker compose up --build
```

Verify:

```bash
curl http://127.0.0.1:8080/health
curl http://127.0.0.1:8080/api/v1/status
```

## Documentation

- [Architecture](docs/architecture.md)
- [Stage 0 decisions](docs/STAGE0.md)
- [Threat model (sketch)](docs/THREAT_MODEL.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)

## Roadmap

| Stage | Status | Description |
| --- | --- | --- |
| 0 | Done | Product decisions, security docs |
| 1 | Done | OSS foundation, CI, policies |
| 2 | Done | Solution skeleton, Docker Compose, health checks |
| 3 | Done | OpenAPI diff + C# detection + scan API + Markdown report |
| 4 | Done | GitHub draft PR automation (PAT, idempotent branch reuse) |
| 5 | Next | LLM patch proposals (BYOK / Ollama) |
| 6 | Planned | Interactive CLI installer |

## Scan API (Stage 3)

```bash
# Run scan (pretty-printed JSON in Development)
curl -X POST http://127.0.0.1:8080/api/v1/scans \
  -H "Content-Type: application/json" \
  -d '{"repositoryPath":"/examples/stripe-csharp-demo/StripeDemo","provider":"stripe","language":"csharp"}'

# Human-readable Markdown report (recommended)
curl "http://127.0.0.1:8080/api/v1/scans/{scanJobId}/report?format=markdown"

# Or use the .md shortcut
curl "http://127.0.0.1:8080/api/v1/scans/{scanJobId}/report.md"

# Structured findings only
curl "http://127.0.0.1:8080/api/v1/scans/{scanJobId}/findings"
```

PowerShell pretty-print:
```powershell
$r = Invoke-RestMethod -Method POST -Uri "http://127.0.0.1:8080/api/v1/scans" `
  -ContentType "application/json" `
  -Body '{"repositoryPath":"/examples/stripe-csharp-demo/StripeDemo","provider":"stripe","language":"csharp"}'
$r | ConvertTo-Json -Depth 6

Invoke-RestMethod "http://127.0.0.1:8080/api/v1/scans/$($r.id)/report?format=markdown"
```

## Draft PR (Stage 4)

Set `GitHub__Token` and request PR creation:

```bash
curl -X POST http://127.0.0.1:8080/api/v1/scans \
  -H "Content-Type: application/json" \
  -d '{"gitHubOwner":"your-org","gitHubRepo":"your-repo","createPullRequest":true,"provider":"stripe","language":"csharp"}'
```

## License

Apache License 2.0 — see [LICENSE](LICENSE).

## Security

See [SECURITY.md](SECURITY.md). Do not open public issues for vulnerabilities.
