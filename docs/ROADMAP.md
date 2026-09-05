# ApiMorph — Roadmap (Stages 8+)

**Status:** Stage 8 implemented (see [STAGE8.md](./STAGE8.md)); Stages 9+ planned  
**Date:** 2026-09-05  
**Depends on:** Stages 0–7 complete (detect → patch → draft PR → CLI → GitHub App)

This document captures corrected Stage 8+ plans: automation triggers, FreeRADIUS-style config, and local (not internet) install UX.

---

## Design principles (operator UX)

1. **Self-hosted first** — customer code and secrets stay on the customer network.
2. **Commented config, uncomment to enable** — FreeRADIUS-style `.conf` / settings files with dozens of documented parameters; defaults stay safe.
3. **Manual is always available** — CLI / API for emergencies; automation never replaces the kill-switch path.
4. **Local install UX, not public SaaS wizard** — optional local HTML/GUI on `127.0.0.1` only; no mandatory redirect over the public internet after GitHub App install.
5. **Two delivery shapes by OS** — terminal (headless server) vs desktop (local browser GUI), same engine underneath.

---

## Stage 8 — Automation: schedules, webhooks, provider feed, operator config

**Status:** Done (core)  
**Goal:** Continuous scans without relying on manual `curl`, while keeping full operator control.

### 8.1 Trigger matrix (all supported)

| Trigger | Default | Customization | Must have? |
| --- | --- | --- | --- |
| **Manual** | CLI `apimorph scan` + `POST /api/v1/scans` | Always on | **Yes** (emergencies, demos, CI) |
| **Cron / schedule** | Off until uncommented | Cron expression per repo or global | Yes |
| **Git push webhook** | Off until uncommented; branch filter default `main` | One or many branches / patterns | Yes |
| **Provider feed** | Off until uncommented | Poll OpenAPI/SDK version; enqueue matching repos | Yes (can land late in Stage 8) |

Manual remains the emergency path even when cron + webhooks + feed are enabled.

### 8.2 Push webhook — customizable branches

Not “always scan every push to every branch.”

| Setting | Default | Meaning |
| --- | --- | --- |
| `webhook.enabled` | `false` | Must uncomment / enable explicitly |
| `webhook.branches` | `main` | Comma-separated or list: `main`, `main,release/*`, etc. |
| `webhook.default_branch_only` | optional helper | If true, resolve GitHub default branch and treat as the only target |
| `webhook.path_filters` | empty (all paths) | Optional globs so docs-only pushes can be ignored |
| `webhook.secret` | from `GitHub__WebhookSecret` | HMAC (`X-Hub-Signature-256`) required when enabled |

**Flow:**

```text
GitHub push → POST /api/v1/webhooks/github
           → verify signature
           → if ref matches configured branch list
           → enqueue job (dedupe: owner/repo + commitSha)
           → ScanWorker → Engine → App token → draft PR
```

### 8.3 Cron / schedule

| Setting | Default | Meaning |
| --- | --- | --- |
| `schedule.enabled` | `false` | Off until uncommented |
| `schedule.cron` | `0 2 * * *` (example) | Global default |
| `schedule.timezone` | `UTC` | Explicit |
| Per-repo override | none | `repos/<name>.conf` can override cron |

### 8.4 Provider feed

| Setting | Default | Meaning |
| --- | --- | --- |
| `provider_feed.enabled` | `false` | Off until uncommented |
| `provider_feed.providers` | `stripe` | Which feeds to poll |
| `provider_feed.interval` | e.g. `6h` | Poll cadence |
| `provider_feed.on_change` | `enqueue_registered_repos` | Behavior when OpenAPI/SDK version changes |

When the feed detects a contract change, enqueue scans for registered repos that list that provider — still draft PR + human review.

### 8.5 FreeRADIUS-style configuration (easy + secure)

Replace “remember dozens of env vars” as the primary story with **documented config files**:

```text
deploy/config/
  apimorph.conf           # master file: includes + global toggles
  github.conf             # App / PAT / webhook secret path references
  triggers.conf           # manual (always), cron, webhook branches, provider feed
  scan.conf               # patch / LLM / provider / language defaults
  repos.d/
    example-org-payments.conf.example
```

**Style (FreeRADIUS-inspired):**

```conf
# triggers.conf — ApiMorph automation triggers
# All automation is OFF by default. Uncomment to enable.

# --- Manual (always available; do not disable in production without a reason) ---
# manual.enabled = true

# --- Cron ---
# schedule.enabled = false
# schedule.cron = "0 2 * * *"
# schedule.timezone = "UTC"

# --- Git push webhook ---
# webhook.enabled = false
# Default branch filter (comma-separated). Examples:
#   main
#   main,master,release/*
# webhook.branches = main
# webhook.path_filters =
# Webhook HMAC secret: prefer file mount, not inline
# webhook.secret_file = /run/secrets/github-webhook-secret

# --- Provider OpenAPI / SDK feed ---
# provider_feed.enabled = false
# provider_feed.providers = stripe
# provider_feed.interval = 6h
```

**Security rules for this model:**

- Secrets stay in **files under `deploy/secrets/`** or env overrides — never committed; `.conf` only holds **paths** to secrets.
- `.conf` files are readable by operators; PEM / tokens are `0600` mounts.
- Existing `deploy/.env` remains supported as an override layer (Docker Compose friendly), but **documented defaults live in `.conf`**.
- `apimorph init` / local GUI write both `.conf` uncommented lines and secret files — operators can still edit by hand.

### 8.6 Architecture (Stage 8)

```text
                    +------------------+
  Manual CLI/API -> |  Orchestrator    |
  Cron ----------> |  Job queue       | --> Engine --> GitHub App --> draft PR
  Webhook -------> |  Repo registry   |
  Provider feed -> +------------------+
```

| Component | Responsibility |
| --- | --- |
| **Repo registry** | owner, repo, providers[], branches[], schedule override, lastScanAt |
| **Job queue** | pending/running/failed; retry; dedupe |
| **ScanWorker** | Existing scan + PR path (Stage 3–7) |
| **Webhook controller** | Signature verify + branch filter + enqueue |
| **Scheduler** | Cron from `.conf` / registry |
| **Provider feed poller** | Compare OpenAPI/SDK fingerprints; enqueue |

### 8.7 Definition of done (Stage 8)

- [ ] Manual scan still works (CLI + API) with automation off  
- [ ] Enabling cron via uncommented `.conf` runs scheduled scans  
- [ ] Push webhook fires only for configured branches (default `main`)  
- [ ] Invalid webhook signatures rejected  
- [ ] Provider feed can enqueue on contract change (at least Stripe fixtures/URL)  
- [ ] Secrets referenced by path; sample `.conf` fully commented with descriptions  
- [ ] `apimorph doctor` validates webhook secret present when webhook.enabled  

---

## Stage 8b / Stage 10-install — Local operator UI & dual releases

*(Can start as a thin slice in late Stage 8; full polish in packaging stage.)*

### Problem

GitHub’s “redirect after App install to a public Setup URL” implies something reachable on the internet. That fights ApiMorph’s on-prem model.

### Solution

| Path | Audience | Behavior |
| --- | --- | --- |
| **Terminal release** | Headless Linux servers | `apimorph init` / edit `.conf`; no browser required |
| **Desktop / GUI release** | Windows / macOS / Linux desktop | Local static HTML (or light embedded UI) on `http://127.0.0.1:<port>` only |

**Local HTML install / settings pages** (shipped in-repo under e.g. `src/ApiMorph.OperatorUi/` or `deploy/www/`):

- Guided `.conf` toggles (enable webhook, set branches, cron)
- Paste App ID / Installation ID; upload PEM to secrets dir (never to a cloud)
- Show webhook URL to paste into GitHub App settings: `http://<server>:8080/api/v1/webhooks/github` (operator’s own host — their inbound choice)
- Link “open GitHub App install” in a new tab; **completion is local** (“I’ve installed — continue”) rather than mandatory OAuth redirect back from GitHub over the public internet

**Explicit non-goals for install UX:**

- No ApiMorph-operated cloud callback  
- No requiring public DNS for the install wizard  
- Binding operator UI to `127.0.0.1` by default; LAN bind is an uncommented advanced option with a warning  

### Dual release artifacts (later packaging)

| Artifact | Contents |
| --- | --- |
| `apimorph-server` | Compose + engine + orchestrator + CLI + `.conf` templates (no GUI) |
| `apimorph-desktop` | Same + local operator UI + “open browser to 127.0.0.1” helper |

Same scan/PR engine; different operator surface.

---

## Stage 9 — Pluggable OpenAPI-first providers

**Status:** Planned  
**Goal:** Stripe remains the reference provider; architecture supports adding others without rewriting the orchestrator.

| Layer | Plan |
| --- | --- |
| **Provider pack** | `providers/<id>/` — OpenAPI baseline/target or feed URL, rules, optional deterministic patches |
| **Registry** | `IApiProvider` + `.conf` `scan.providers = stripe` (uncomment more later) |
| **Languages** | `ILanguageScanner` — C# first; stubs for TS/Python later |
| **Branches** | `apimorph/{provider}-migration` (already Stage 4/5 pattern) |
| **Feed** | Stage 8 provider feed becomes multi-provider |

**Definition of done:** Second provider (Twilio or mock OpenAPI pack) → findings + draft PR on a demo repo.

---

## Stage 10 — Platform, packaging, scale

| Area | Plan |
| --- | --- |
| **PostgreSQL** | Multi-repo / long history (ADR already notes SQLite→Postgres path) |
| **GitHub Checks** | PR check run summarizing findings/patches |
| **Multi-installation map** | Many orgs → many App installation IDs in registry |
| **Global `dotnet tool` / packages** | Versioned `apimorph` CLI; optional NuGet/GitHub Releases |
| **Dual release channels** | `server` (headless) + `desktop` (local GUI) as above |
| **Hybrid SaaS (optional)** | Cloud **rule feed only**; source never leaves customer network |
| **More languages** | TS, Python, Java, Go scanners |
| **Auto-merge** | Opt-in only, policy-gated — never default |

---

## Corrected sequence

```text
Stage 8   Automation (manual + cron + branch-filtered webhooks + provider feed)
          + FreeRADIUS-style .conf
          + optional thin local settings HTML (127.0.0.1)

Stage 9   Pluggable providers (second provider pack)

Stage 10  Packaging (server vs desktop), Postgres, Checks, multi-install, tool publish
```

### Why this order

1. Stage 8 makes the GitHub App investment pay off (events + schedules + feed).  
2. Branch-filtered webhooks avoid noisy PRs from feature branches.  
3. `.conf` + local UI keeps servers operable without teaching every env var.  
4. Stage 9 proves generality once automation exists.  
5. Dual releases and public `dotnet tool` wait until the operator story is stable.

---

## Stage 8 implementation slices (when coding starts)

1. **Config loader** — parse `deploy/config/*.conf` (comment-aware) + env override  
2. **Repo registry + job queue + worker**  
3. **Cron** from `triggers.conf`  
4. **Webhook** with signature + `webhook.branches` (default `main`)  
5. **Provider feed** poller for Stripe  
6. **CLI** — `apimorph config validate`, `repos add`, keep `scan` / `doctor`  
7. **Optional** — local `127.0.0.1` settings pages that edit `.conf` / secrets  

Manual API/CLI remains slice 0 and never regresses.
