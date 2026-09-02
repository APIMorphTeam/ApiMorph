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

Analyze a local repository path for API contract impacts and optional migration patches.

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
| `options.detectOnly` | bool | no | Default `true` — findings only, no patches |
| `options.llmEnabled` | bool | no | Default `false` — use LLM for harder migrations when `detectOnly=false` |

### Response `200`

```json
{
  "contractVersion": "1",
  "findings": [
    {
      "ruleId": "stripe.api-version.deprecated",
      "filePath": "Services/PaymentService.cs",
      "line": 9,
      "message": "Deprecated Stripe API version configured in code",
      "confidence": "high",
      "evidence": "StripeConfiguration.ApiVersion = \"2019-12-03\";"
    }
  ],
  "patches": [
    {
      "filePath": "Services/PaymentService.cs",
      "patchType": "deterministic",
      "description": "Update Stripe API version to 2024-11-20.acacia",
      "content": "<full updated file contents>",
      "linkedRuleIds": ["stripe.api-version.deprecated"]
    }
  ],
  "summary": {
    "filesScanned": 2,
    "findingCount": 3,
    "patchCount": 1,
    "patchMode": "deterministic"
  }
}
```

### Patch types

| `patchType` | Description |
| --- | --- |
| `deterministic` | Rule-based codemod (no LLM) |
| `llm-assisted` | LLM-proposed change (BYOK or Ollama) |

### `patchMode` summary values

`detect-only` | `deterministic` | `llm-assisted` | `mixed`

### Confidence values

`high` | `medium` | `low`

### Errors

| Status | Body |
| --- | --- |
| `400` | `{ "detail": "..." }` — invalid request |
| `422` | Pydantic validation error |
| `500` | `{ "detail": "..." }` — internal error |
