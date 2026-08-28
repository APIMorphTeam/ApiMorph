# Scan API (Stage 3) & Draft PR (Stage 4)

## Scan endpoints

### `POST /api/v1/scans`

Creates and runs a scan job synchronously (MVP).

**Local path example:**

```json
{
  "repositoryPath": "/examples/stripe-csharp-demo/StripeDemo",
  "provider": "stripe",
  "language": "csharp",
  "createPullRequest": false
}
```

**GitHub repository example (Stage 4):**

```json
{
  "gitHubOwner": "your-org",
  "gitHubRepo": "your-repo",
  "provider": "stripe",
  "language": "csharp",
  "createPullRequest": true
}
```

### `GET /api/v1/scans/{scanJobId}`

Returns job status, finding count, and optional PR metadata.

### `GET /api/v1/scans/{scanJobId}/report`

Returns a Markdown report (`format: markdown`).

## Detection rules (MVP)

| Rule ID | Source |
| --- | --- |
| `stripe.openapi.removed-operation` | OpenAPI baseline vs target diff |
| `stripe.api-version.deprecated` | Static C# pattern |
| `stripe.charge.source-deprecated` | Static C# pattern |

## GitHub configuration

Set in environment or `appsettings.json`:

```json
{
  "GitHub": {
    "Token": "ghp_...",
    "AutoMerge": false,
    "BranchPrefix": "apimorph",
    "WorkspacePath": "/workspace"
  }
}
```

**Security notes:**

- Use a fine-scoped PAT with `contents` + `pull_requests` access to selected repos.
- `AutoMerge` must remain `false` (enforced in code).
- Draft PRs are idempotent per branch name (`apimorph/scan-{jobId}`); existing open PRs are reused.

## Report file in PR

When `createPullRequest=true`, ApiMorph commits:

`apimorph/reports/scan-{jobId}.md`

and opens a **draft** PR containing the migration report.
