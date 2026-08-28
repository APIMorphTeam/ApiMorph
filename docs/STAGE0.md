# ApiMorph — Stage 0 (Product Decisions)

**Status:** accepted  
**Date:** 2026-08-27  
**License:** Apache License 2.0

## Name

**ApiMorph** — an API contract migration agent: detects provider breaking changes and proposes safe updates in customer codebases via pull requests.

## One-liner

ApiMorph is a self-hosted "Dependabot for API logic": it tracks contract changes (OpenAPI/SDK), finds impacted usages in a repository, and opens a draft PR with a migration.

## Problem

API changelogs are ignored, breaking changes ship with little warning, and useful features launch unnoticed. The result is downtime, manual toil, and delayed adoption. Agentic code-change infrastructure already exists — what is missing is the application layer that connects **a provider change** to **a customer's concrete codebase**.

## Solution (MVP)

1. Fetch / diff the API contract (OpenAPI) and SDK version signals.
2. Scan the customer repo (C#) and locate impacted call sites.
3. Produce findings and (optionally) a patch proposal.
4. Open a **draft Pull Request** with explanation, impact map, and confidence level.
5. A human reviews; auto-merge is disabled by default.

## MVP wedge (narrow vertical slice)

| Dimension | MVP decision | Later |
| --- | --- | --- |
| Vendor | **Stripe** | Pluggable providers (OpenAPI-first) |
| Scanned language | **C#** (.NET) | TS/JS, Python, Java, Go |
| Detection | OpenAPI diff + heuristics/AST (tree-sitter) + `Stripe.net` version | Rules-as-code per breaking change |
| Patching | **Detect-only** + report first; then draft PR; LLM only on confirmed hits | Higher automation at high confidence |
| Runtime | Self-hosted (Docker Compose), outbound HTTPS | Hybrid SaaS (cloud rule feed) |
| LLM | BYOK (OpenAI-compatible) or Ollama (offline) | Optional hosted proxy (never customer source hosting) |
| Data | SQLite (EF Core) | PostgreSQL / Azure SQL |
| Orchestrator | **C# / .NET 9** | Same |
| Engine | Python / FastAPI | Same |

## Non-goals (explicitly out of MVP)

- A general-purpose coding agent / Cursor/Devin replacement.
- Supporting "every API in the world" on day one.
- Auto-merge to `main` without review.
- Uploading customer source code to ApiMorph-operated servers.
- Scraping blog posts as the sole source of truth for breaking changes.
- Multi-tenant cloud control plane, billing, SSO (architectural door left open — not MVP scope).
- Full formal verification of migration correctness.

## Success metrics (Stage 0 → demo Definition of Done)

In `examples/stripe-csharp-demo` (intentionally outdated usages), ApiMorph:

- detects ≥1 real breaking change / impacted usage,
- generates a Markdown report with file locations,
- opens a **draft PR** on GitHub in < 10 minutes from trigger (local),
- by default **does not** merge and **does not** require an LLM for detection alone.

Smoke: `docker compose up` + one happy-path job passes in CI.

## PR quality bar (PR Definition of Done)

Every PR must include:

- list of detected contract changes (with source: OpenAPI diff / rule id),
- call-site map (file:line),
- confidence level (`high|medium|low`),
- risk notes and links to Stripe docs,
- whether the patch was deterministic, LLM-assisted, or detect-only.

## Trust model (short)

- Customer code stays on the customer network (self-hosted).
- External communication: outbound HTTPS (443) — GitHub, OpenAPI/docs feeds, optional BYOK LLM.
- No required inbound internet ports.
- Secrets (GitHub App/PAT, LLM keys) remain local to the customer.

## Stack direction (not full implementation in Stage 0)

- Orchestrator: C# / **.NET 9**, EF Core, SQLite
- Engine: Python / FastAPI (analysis, AST, LLM)
- CLI installer: C# / **.NET 9**
- Deploy: Docker Compose
- License: Apache-2.0

## Open decisions (do not block Stage 0)

- GitHub App vs fine-scoped PAT for the first demo (preference: GitHub App ASAP).
- Whether the first patch path is fully deterministic (`Stripe.net` bump + known renames) before enabling LLM.
- GitHub org name / branding.

## One-liner (README)

> ApiMorph is a self-hosted agent that turns API contract changes into draft PRs in your repos — Dependabot for API logic, starting with Stripe and C#.
