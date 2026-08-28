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
| 3 | Next | OpenAPI diff + C# detection (no LLM) |
| 4 | Planned | Draft PR automation |
| 5 | Planned | LLM patch proposals (BYOK / Ollama) |

## License

Apache License 2.0 — see [LICENSE](LICENSE).

## Security

See [SECURITY.md](SECURITY.md). Do not open public issues for vulnerabilities.
