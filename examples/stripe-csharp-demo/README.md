# Stripe C# Demo

Minimal C# project with **intentionally outdated** Stripe.net patterns used by ApiMorph Stage 3 detection tests.

## Patterns included

| Rule ID | Pattern |
| --- | --- |
| `stripe.api-version.deprecated` | `StripeConfiguration.ApiVersion = "2019-12-03"` |
| `stripe.charge.source-deprecated` | `ChargeCreateOptions.Source` |
| `stripe.openapi.removed-operation` | `RefundService` usage (removed in target OpenAPI fixture) |

## Run locally

```bash
dotnet run --project StripeDemo/StripeDemo.csproj
```

## Scan with ApiMorph

```bash
curl -X POST http://127.0.0.1:8080/api/v1/scans \
  -H "Content-Type: application/json" \
  -d '{"repositoryPath":"/examples/stripe-csharp-demo/StripeDemo","provider":"stripe","language":"csharp"}'
```

When running via Docker Compose, the `examples` folder is mounted at `/examples`.
