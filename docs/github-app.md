# GitHub App authentication (Stage 7)

ApiMorph prefers a **GitHub App** over a personal access token (PAT). PATs remain as a demo/migration fallback.

## Who configures secrets?

| Audience | How to configure | Needs `dotnet user-secrets`? |
| --- | --- | --- |
| **Self-hosted operators** (normal use) | `deploy/.env` + PEM file under `deploy/secrets/` | **No** |
| **Docker / Kubernetes** | Env vars + secret volume / K8s Secret mount | **No** |
| **Local SDK developers** (`dotnet run` without Docker) | Optional `dotnet user-secrets` | Optional only |

End users installing ApiMorph must **never** be required to run `dotnet user-secrets`. That command is a .NET developer convenience for machine-local secret storage during `dotnet run`. Production and Compose paths use env + file mounts — the same pattern as any self-hosted system.

## Create the GitHub App

1. GitHub → **Settings** → **Developer settings** → **GitHub Apps** → **New GitHub App**
2. Permissions:
   - **Repository permissions → Contents:** Read & write
   - **Repository permissions → Pull requests:** Read & write
   - **Repository permissions → Metadata:** Read-only
3. Uncheck webhooks for now (Stage 8), or set a webhook URL + secret for later
4. Create the app → note **App ID**
5. **Install** the app on the org/account that owns target repos → note **Installation ID** (from the install URL: `.../settings/installations/{id}` — use the number, or paste the full URL)
6. **Generate a private key** → download the `.pem` file

## Configure (Docker Compose — recommended)

1. Copy the PEM to a gitignored path:

```text
deploy/secrets/github-app.pem
```

2. Set in `deploy/.env`:

```env
GitHub__AppId=123456
GitHub__InstallationId=987654321
GitHub__AppPrivateKeyPath=/run/secrets/github-app.pem
GitHub__Token=
```

3. Uncomment the volume in `deploy/docker-compose.yml`:

```yaml
- ./secrets/github-app.pem:/run/secrets/github-app.pem:ro
```

4. Rebuild:

```bash
cd deploy
docker compose up --build -d
```

5. Verify:

```bash
dotnet run --project src/ApiMorph.Cli -- status
# GitHub auth: app
```

### Auth precedence

1. **App** — if `AppId` + `InstallationId` + private key (path or PEM content) are set  
2. **PAT** — if `GitHub__Token` is set  
3. **None** — GitHub PR features disabled  

## Optional: local `dotnet run` developers only

If you run the orchestrator with the .NET SDK (not Docker) and want secrets outside `appsettings.json`:

```bash
cd src/ApiMorph.Orchestrator
dotnet user-secrets set "GitHub:AppId" "123456"
dotnet user-secrets set "GitHub:InstallationId" "987654321"
dotnet user-secrets set "GitHub:AppPrivateKeyPath" "C:\\path\\to\\github-app.pem"
```

This is **not** part of the operator install path. Compose users ignore it.

## Security practices

- Never commit `*.pem`, `.env`, or private keys (`secrets/` and `*.pem` are gitignored)
- Prefer **file mounts** (`AppPrivateKeyPath`) over inline `AppPrivateKey` env PEMs (env vars appear in process listings)
- Prefer App over classic PATs; if using a PAT, use fine-scoped tokens limited to needed repos
- Installation tokens are minted on demand and cached until near expiry (~1 hour)
- Git HTTPS uses `x-access-token:{token}@github.com/...` (required for App installation tokens)
- `WebhookSecret` is accepted in config for Stage 8; do not log it

## Troubleshooting

| Symptom | Check |
| --- | --- |
| `githubAuthMode: none` | App fields incomplete or PEM missing at path |
| `App ID set but private key missing` (doctor) | Mount PEM / set `AppPrivateKeyPath` |
| Clone/push 401 | Installation must include the target repo; regenerate key if rotated |
| Still using PAT | Clear `GitHub__Token` once App works |
