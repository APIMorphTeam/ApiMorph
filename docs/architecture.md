# Architecture

High-level overview of ApiMorph components, data flow, and trust boundaries.

## Components

| Component | Technology | Responsibility |
| --- | --- | --- |
| **Orchestrator** | C# / .NET 9 | Business logic, GitHub integration, job scheduling, EF Core + SQLite |
| **Engine** | Python / FastAPI | Code analysis, OpenAPI diff, optional LLM patch proposals |
| **CLI** | C# / .NET 9 | Interactive installer and local diagnostics (`doctor`) |
| **Deploy** | Docker Compose | Self-hosted runtime; outbound HTTPS only |

## MVP data flow

```text
Trigger (manual / webhook / schedule)
        |
        v
+------------------+
|  Orchestrator    |
|  (.NET 9)        |
+--------+---------+
         | POST /v1/analyze
         v
+------------------+
|  Engine          |
|  (Python)        |
+--------+---------+
         |
         | findings[]
         v
+------------------+
|  SQLite          |
|  (jobs, findings)|
+------------------+
         |
         | (Stage 4+) draft PR
         v
      GitHub
```

## Network model

- **Default:** no inbound internet ports on the host.
- Orchestrator binds to `127.0.0.1:8080` via Docker port mapping.
- Engine is internal to the Compose network (not exposed on the host).
- Outbound HTTPS (443) only: GitHub API, Stripe OpenAPI, optional BYOK LLM.

See [THREAT_MODEL.md](./THREAT_MODEL.md) for trust zones and abuse cases.

## MVP wedge

- **Provider:** Stripe
- **Language:** C#
- **Mode:** detect → report → draft PR (human review required)
- **LLM:** optional; detection must work with `LLM_ENABLED=false`

## Internal API contract

Orchestrator ↔ Engine communication uses a versioned JSON contract:

- [contracts/analyze-v1.md](./contracts/analyze-v1.md)

## Repository layout

```text
src/
  ApiMorph.Orchestrator/   # .NET 9 Web API
  ApiMorph.Cli/            # .NET 9 console
  engine/                  # Python FastAPI
deploy/
  docker-compose.yml
  Dockerfile.*
examples/
  stripe-csharp-demo/      # Stage 3 demo app
tests/
  ApiMorph.Orchestrator.Tests/
```

## Evolution path

| Phase | Data store | Deployment |
| --- | --- | --- |
| MVP (now) | SQLite | Self-hosted Compose |
| SaaS (later) | PostgreSQL | Hybrid: cloud control plane + on-prem agent |

See ADRs in [adr/](./adr/) for decision rationale.

## Related docs

- [STAGE0.md](./STAGE0.md) — product decisions
- [THREAT_MODEL.md](./THREAT_MODEL.md) — security sketch
- [../SECURITY.md](../SECURITY.md) — security policy
- [../CONTRIBUTING.md](../CONTRIBUTING.md) — contributor guide
