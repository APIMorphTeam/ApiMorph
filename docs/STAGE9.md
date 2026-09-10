# Stage 9 — Pluggable provider packs

**Status:** In progress  
**Depends on:** Stages 0–8 (scan → PR → App → automation)

## Goal

Stripe remains the **reference pack**. Operators can add **custom/manual** providers (and optional auto sources) without rewriting the orchestrator. Diff + scan consume a normalized pack; how the pack was filled is separate.

## Pack layout

```text
providers/<id>/                 # shipped packs (repo root)
deploy/config/providers/<id>/   # operator / custom packs (preferred for self-host)

  manifest.json
  PROVIDER.md                   # human + AI agent instructions
  contract/
    baseline.json               # trusted “old” OpenAPI
    target.json                 # migrate toward this
  rules/
    csharp.json                 # optional static regex rules
```

### `manifest.json`

```json
{
  "id": "mock",
  "displayName": "Mock API",
  "languages": ["csharp"],
  "source": {
    "type": "manual",
    "openapiUrl": null,
    "gitUrl": null,
    "gitPath": null
  },
  "feed": {
    "enabled": false,
    "fingerprintFile": "contract/target.json"
  }
}
```

`source.type`: `manual` | `openapi_url` | `git` (URL/git refresh is assist/CLI; scan always uses local `contract/`).

## Engine resolution order

For provider `id`, baseline/target are resolved from the first hit:

1. Request overrides `openApiBaselinePath` / `openApiTargetPath`
2. `APIMORPH_PROVIDERS_PATH` / `/providers/<id>/`
3. `/config/providers/<id>/` (Docker: orchestrator config mount mirrored to engine)
4. Legacy fixtures `fixtures/openapi/<id>_baseline.json` (Stripe only, compatibility)

Static C# rules load from `rules/csharp.json` when present; otherwise Stripe built-in rules apply only for `id=stripe`.

OpenAPI removed-operation rule id: `{provider}.openapi.removed-operation`.

## CLI

```text
apimorph provider list
apimorph provider init <id> [--display-Name "..."] [--from-openapi baseline.json target.json]
apimorph provider validate [id]
```

`provider init` writes a custom pack under `deploy/config/providers/<id>/` plus `PROVIDER.md`.

## Feed (multi-provider)

When `provider_feed.enabled = true`, the poller fingerprints each id in `provider_feed.providers` using that pack’s `contract/target.json` (or legacy `feeds/stripe_target.json`). Manual packs with no file change are skipped.

## Definition of done

- [x] Pack format + loader; Stripe as a pack
- [x] Custom `provider init` + `PROVIDER.md`
- [x] Second pack (`mock`) → findings on demo fixture
- [x] Engine accepts any registered pack id (not stripe-only gate)
- [x] Feed fingerprints any pack with a resolvable target file
- [ ] Optional later: `provider refresh` fetching OpenAPI URL / git path

## Testing

Automated (Python):

```bash
python scripts/test_provider_packs.py
python scripts/test_provider_packs.py --checklist
```

Pytest only:

```bash
cd src/engine
python -m pytest tests/test_provider_pack.py tests/test_provider_validation.py -q
```

CLI wizard smoke:

```bash
dotnet run --project src/ApiMorph.Cli -- provider init acme --source manual
dotnet run --project src/ApiMorph.Cli -- provider init acme-gh --source github --git-url https://github.com/acme/api.git --git-path openapi.json
dotnet run --project src/ApiMorph.Cli -- provider init acme-html --source html_changelog --changelog-url https://example.com/changelog
dotnet run --project src/ApiMorph.Cli -- provider validate
```

Source types validated today (local contracts still required for scan):

| Type | Required manifest fields |
| --- | --- |
| `manual` | — |
| `openapi_url` | `source.openapiUrl` |
| `git` / `github` / `gitlab` | `source.gitUrl` (+ optional `gitPath`) |
| `html_changelog` | `source.changelogUrl` (assist-only warning) |
