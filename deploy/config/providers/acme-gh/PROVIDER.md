# How to add a custom ApiMorph provider pack

Use this file as instructions for a human **or an AI agent**. Goal: create a pack under `deploy/config/providers/acme-gh/` so scans work without shipping a built-in vendor.

## Layout

```text
deploy/config/providers/acme-gh/
  manifest.json
  PROVIDER.md
  contract/
    baseline.json      # OpenAPI 3.x — the old/trusted contract
  contract/
    target.json        # OpenAPI 3.x — migrate toward this
  rules/
    csharp.json        # optional static regex rules
```

## Steps

1. Choose a lowercase id (`acme`, `internal-billing`). No spaces.
2. Obtain or author OpenAPI JSON for **baseline** (current/old) and **target** (desired/new).
   - Prefer official OpenAPI / Swagger URL, or a GitHub/GitLab path to the spec.
   - If you only have docs, draft a minimal OpenAPI covering the endpoints your code calls.
3. Put those files in `contract/baseline.json` and `contract/target.json`.
4. Optionally add `rules/csharp.json` entries:

```json
[
  {
    "ruleId": "acme-gh.something.deprecated",
    "pattern": "\\\\bOldClient\\\\b",
    "message": "OldClient is deprecated",
    "confidence": "high",
    "guidance": "Use NewClient instead"
  }
]
```

5. Set `manifest.json` `source.type` to `manual` (default). Later you may set `openapi_url` / `git` metadata for refresh tooling.
6. Register the provider on a repo: `repo.providers = stripe,acme-gh` in `deploy/config/repos.d/*.conf`.
7. Validate: `dotnet run --project src/ApiMorph.Cli -- provider validate acme-gh`
8. Scan: `apimorph scan --owner ORG --repo NAME --provider acme-gh`

## What ApiMorph does with the pack

- Diffs baseline → target (removed operations → `{id}.openapi.removed-operation`)
- Applies optional static C# rules
- Opens draft PR on `apimorph/acme-gh-migration` when PR creation is enabled

## Non-goals for a first pack

- Perfect coverage of the entire vendor API
- Scraping HTML changelogs (optional assist later — always review)
- Deterministic patches (optional; Stripe has special-cased ones)

## Agent checklist

- [ ] `manifest.json` id matches folder name
- [ ] Both OpenAPI files parse as JSON
- [ ] At least one path differs between baseline and target **or** a csharp rule exists
- [ ] Demo / target repo contains a string the openapi patterns or rules can match
- [ ] `provider validate` passes
