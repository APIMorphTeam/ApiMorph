# ADR-0001: Record architecture decisions

**Status:** accepted  
**Date:** 2026-08-28

## Context

ApiMorph is a multi-service system (C# orchestrator, Python engine, Docker deployment) with planned evolution from self-hosted OSS to a hybrid SaaS model. We need a lightweight way to capture why major decisions were made.

## Decision

We will use Architecture Decision Records (ADRs) stored in `docs/adr/` as plain markdown files.

## Consequences

- Decisions are discoverable in git history and PR reviews.
- Low ceremony — no external tooling required.
- ADRs should be updated or superseded rather than silently contradicted.
