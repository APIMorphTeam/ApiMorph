# ADR-0004: SQLite for MVP, PostgreSQL later

**Status:** accepted  
**Date:** 2026-08-28

## Context

MVP is self-hosted with zero cloud cost. Operators should not need to provision a database server. A future SaaS/hybrid model will require multi-tenant persistence and concurrent writers.

## Decision

- **MVP:** SQLite via EF Core (`Microsoft.EntityFrameworkCore.Sqlite`), file stored on a Docker volume.
- **Later:** Switch to PostgreSQL by changing the EF Core provider and connection string; domain model stays provider-agnostic.

## Consequences

- Simple local and on-prem deployments (single file database).
- SQLite is not ideal for high-concurrency multi-worker writes — acceptable for MVP single-orchestrator deployments.
- Migrations must be tested against both providers before SaaS launch.
