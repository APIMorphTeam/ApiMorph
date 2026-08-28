# ADR-0003: Python analysis engine

**Status:** accepted  
**Date:** 2026-08-28

## Context

ApiMorph must analyze source code (AST), diff OpenAPI contracts, and optionally call LLM providers. Python has strong ecosystems for parsing, ML/LLM adapters, and rapid iteration on analysis pipelines.

## Decision

Use **Python 3.12+ / FastAPI** as a separate microservice (`src/engine`) that:

- Exposes versioned HTTP endpoints (`/health`, `/v1/analyze`)
- Returns structured findings via Pydantic models
- Runs only on the internal Docker network in the reference deployment

## Consequences

- Clear separation: orchestrator owns persistence and GitHub; engine owns analysis.
- Requires a versioned JSON contract between services.
- Two runtimes in CI and Docker images; acceptable trade-off for MVP velocity.
