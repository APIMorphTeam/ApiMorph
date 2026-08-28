# ADR-0002: .NET 9 orchestrator

**Status:** accepted  
**Date:** 2026-08-28

## Context

ApiMorph needs a central component for business logic, GitHub integration, job orchestration, and persistence. The author is most productive in C# and the project targets Enterprise-friendly deployments.

## Decision

Use **C# / .NET 9** (ASP.NET Core) as the orchestrator with:

- Entity Framework Core for data access
- `HttpClient` for engine communication
- A separate `ApiMorph.Cli` project for installation and diagnostics

## Consequences

- Strong typing and mature tooling for GitHub/API integrations.
- Dual-language repo increases CI complexity (mitigated by Docker Compose).
- Python remains responsible for AST/analysis workloads (see ADR-0003).
