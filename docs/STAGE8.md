# Stage 8 — Automation operators guide

See also [ROADMAP.md](./ROADMAP.md) for product intent.

## Quick enable

1. Register a repo:

```bash
dotnet run --project src/ApiMorph.Cli -- repos add --owner APIMorphTeam --repo ApiMorph-test
```

2. Edit `deploy/config/triggers.conf` — uncomment what you need:

```conf
webhook.enabled = true
webhook.branches = main
schedule.enabled = true
schedule.cron = 0 2 * * *
provider_feed.enabled = true
provider_feed.interval = 6h
```

3. Set webhook secret in `deploy/.env`:

```env
GitHub__WebhookSecret=your-random-secret
```

4. Rebuild:

```bash
cd deploy
docker compose up --build -d
```

5. Validate:

```bash
dotnet run --project src/ApiMorph.Cli -- config validate
curl.exe http://127.0.0.1:8080/api/v1/automation/status
curl.exe http://127.0.0.1:8080/api/v1/repos
```

## Webhook URL (GitHub App)

In the GitHub App settings (Stage 8), set:

- **Webhook URL:** `http://<your-host>:8080/api/v1/webhooks/github`  
  (for local demos use a tunnel such as ngrok, or GitHub cannot reach `127.0.0.1`)
- **Secret:** same value as `GitHub__WebhookSecret`
- Subscribe to **push** events

Branch filter default is `main`. Customize with `webhook.branches = main,release/*`.

## Manual always works

```bash
dotnet run --project src/ApiMorph.Cli -- scan --owner APIMorphTeam --repo ApiMorph-test --pr
```

## Config layout

```text
deploy/config/
  apimorph.conf
  github.conf
  triggers.conf
  scan.conf
  repos.d/*.conf
```

Secrets stay in `deploy/secrets/` and `.env` — never commit them.
