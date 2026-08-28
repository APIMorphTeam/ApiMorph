# Contributing to ApiMorph

Thank you for your interest in contributing to ApiMorph!

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) (for local deployment)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Python 3.12+](https://www.python.org/downloads/)

## Getting started

```bash
git clone https://github.com/<your-org>/ApiMorph.git
cd ApiMorph

dotnet restore
dotnet build

cd src/engine
python -m venv .venv
source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements-dev.txt
pytest

cd ../../deploy
docker compose up --build
```

Verify the orchestrator is healthy:

```bash
curl http://127.0.0.1:8080/health
curl http://127.0.0.1:8080/api/v1/status
```

## Branch naming

- `feature/<short-description>` — new functionality
- `fix/<short-description>` — bug fixes
- `docs/<short-description>` — documentation only
- `chore/<short-description>` — tooling, CI, dependencies

## Commit messages

We use [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` — new feature
- `fix:` — bug fix
- `docs:` — documentation
- `chore:` — maintenance, CI, dependencies
- `test:` — tests only
- `refactor:` — code change without behavior change

Example: `feat: add engine health check to status endpoint`

## Pull requests

1. Open an issue or comment on an existing one before large changes.
2. Keep PRs focused — one logical change per PR when possible.
3. Update docs when behavior or architecture changes.
4. Ensure CI passes.
5. Do not commit secrets, `.env` files, or `*.db` database files.
6. Review security-sensitive changes against [docs/THREAT_MODEL.md](./docs/THREAT_MODEL.md).

## Code style

- Follow `.editorconfig` settings.
- C#: standard .NET conventions; keep controllers thin, logic in services/domain.
- Python: type hints on public functions; keep FastAPI routes thin.

## Architecture decisions

Significant design changes should include an ADR in `docs/adr/`. See [docs/adr/README.md](./docs/adr/README.md).

## Security

Do not open public issues for vulnerabilities. See [SECURITY.md](./SECURITY.md).

## Questions

Open a GitHub Discussion or Issue if something is unclear.
