# Architecture Decision Records

We use lightweight Architecture Decision Records (ADRs) to document significant technical decisions.

## Format

Each ADR is a markdown file named `NNNN-short-title.md` with:

1. **Status** — proposed | accepted | deprecated | superseded
2. **Context** — what problem or constraint drove the decision
3. **Decision** — what we chose
4. **Consequences** — trade-offs, follow-up work

## Index

| ADR | Title | Status |
| --- | --- | --- |
| [0001](./0001-record-architecture-decisions.md) | Record architecture decisions | accepted |
| [0002](./0002-dotnet9-orchestrator.md) | .NET 9 orchestrator | accepted |
| [0003](./0003-python-engine.md) | Python analysis engine | accepted |
| [0004](./0004-sqlite-to-postgres.md) | SQLite for MVP, Postgres later | accepted |

## When to add an ADR

- New service boundaries or communication patterns
- Database or deployment model changes
- Security architecture changes
- Replacing a core technology choice

Small implementation details do not need an ADR.
