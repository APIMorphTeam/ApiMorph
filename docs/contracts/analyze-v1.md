# Analyze API Contract v1

Versioned contract between **ApiMorph Orchestrator** (.NET 9) and **ApiMorph Engine** (Python).

**Base URL (internal):** `http://engine:8000`  
**Version header (optional):** `X-ApiMorph-Contract-Version: 1`

## `GET /health`

**Response `200`:**

```json
{
  "status": "ok"
}
```

## `POST /v1/analyze`

Analyze a local repository path for API contract impacts.

### Request

```json
{
  "contractVersion": "1",
  "provider": "stripe",
  "repositoryPath": "/workspace/demo",
  "language": "csharp",
  "options": {
    "detectOnly": true,
    "llmEnabled": false
  }
}
```

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `contractVersion` | string | yes | Must be `"1"` |
| `provider` | string | yes | API provider id (MVP: `stripe`) |
| `repositoryPath` | string | yes | Absolute path inside the engine container |
| `language` | string | yes | Source language (MVP: `csharp`) |
| `options.detectOnly` | bool | no | Default `true` — no patch generation |
| `options.llmEnabled` | bool | no | Default `false` |

### Response `200`

```json
{
  "contractVersion": "1",
  "findings": [
    {
      "ruleId": "stripe.openapi.removed-endpoint",
      "filePath": "src/Payments/StripeService.cs",
      "line": 42,
      "message": "Usage of removed endpoint detected",
      "confidence": "medium",
      "evidence": "StripeChargeService.CreateLegacy(...)"
    }
  ],
  "summary": {
    "filesScanned": 12,
    "findingCount": 1
  }
}
```

### Confidence values

`high` | `medium` | `low`

### Errors

| Status | Body |
| --- | --- |
| `400` | `{ "detail": "..." }` — invalid request |
| `422` | Pydantic validation error |
| `500` | `{ "detail": "..." }` — internal error |

## Stage 2 behavior

The engine returns an empty `findings` array with `filesScanned: 0`. Real detection is implemented in Stage 3.
