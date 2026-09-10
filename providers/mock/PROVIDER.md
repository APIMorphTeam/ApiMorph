# Provider pack: Mock API

Minimal OpenAPI-first pack used to prove Stage 9 without a real vendor.

## Contract

- `contract/baseline.json` — includes `POST /v1/widgets/{id}/legacy-activate`
- `contract/target.json` — that operation is **removed**

## Demo code

`examples/mock-csharp-demo` and `src/engine/fixtures/mock-demo` call `LegacyWidgetClient.ActivateAsync`.

## Scan

```bash
curl -X POST http://127.0.0.1:8080/api/v1/scans \
  -H "Content-Type: application/json" \
  -d '{"repositoryPath":"/examples/mock-csharp-demo","provider":"mock","language":"csharp","createPullRequest":false}'
```

Expected finding: `mock.openapi.removed-operation`.
