# ApiMorph Threat Model (Sketch)

**Status:** draft / Stage 0  
**Method:** informal STRIDE-oriented sketch (not a full corporate TARA)  
**Primary deployment:** self-hosted Docker Compose on customer infrastructure

## 1. System overview

ApiMorph helps customers react to API provider contract changes (MVP: Stripe) by scanning customer C# repositories and opening draft GitHub PRs with findings and optional patches.

### Components (MVP direction)

- **Orchestrator:** C# / .NET 9 (business logic, GitHub integration, jobs, EF Core + SQLite)
- **Engine:** Python / FastAPI (AST/analysis, optional LLM refactor proposals)
- **CLI installer:** C# / .NET 9

### Trust zones

| Zone | Components | Trust level |
| --- | --- | --- |
| Z0 Customer private network | Orchestrator (.NET 9), Engine (Python), SQLite, optional Ollama, repo clones | Highest — contains source code |
| Z1 GitHub | Repos, PRs, Checks, App credential validation | High — necessary third party |
| Z2 Provider contract feeds | Stripe OpenAPI / docs / version metadata | Medium — integrity matters |
| Z3 LLM provider (optional BYOK) | OpenAI-compatible API | Low/Medium — may receive code snippets |

```text
[Developer] -> [GitHub] <-HTTPS outbound- [ApiMorph Orchestrator (.NET 9)]
                                            | local
                                            v
                                      [Engine / AST / LLM adapter]
                                            |
                                            +--> [Ollama local] (optional)
                                            +--> [BYOK LLM HTTPS] (optional)
                                            +--> [Stripe OpenAPI HTTPS]
```

## 2. Assets

1. Customer source code (private repos, clones on disk)
2. GitHub credentials (App private key / PAT / installation tokens)
3. LLM API keys (BYOK)
4. Finding history / job metadata (may reference paths and snippets)
5. Integrity of migration PRs (a poisoned PR is a supply-chain risk inside the customer SDLC)
6. Provider contract truth (tampered OpenAPI → wrong migrations)

## 3. Actors

- **Operator** — installs and configures ApiMorph (trusted-ish, powerful)
- **Developer** — reviews/merges PRs
- **Attacker (external)** — internet adversary without a network foothold
- **Attacker (supply chain)** — malicious dependency / compromised image
- **Attacker (malicious insider / compromised bot identity)** — abuses the GitHub App
- **Honest-but-wrong LLM** — not malicious, but harmful if trusted blindly

## 4. Entry points

- Local CLI / config files / `.env`
- Orchestrator API (should be localhost / internal only in MVP)
- Engine API (internal Docker network only)
- Outbound clients: GitHub API, OpenAPI fetch, LLM API
- GitHub PR surface (created by the bot user/app)

## 5. STRIDE sketch

### Spoofing

- **Risk:** stolen GitHub App key/PAT impersonates the ApiMorph bot.
- **Mitigations:** tight file permissions on secrets; secret mounts; short-lived installation tokens; key rotation; limit the App to selected repos; lock down the host environment.

### Tampering

- **Risk:** MITM on OpenAPI download → false breaking changes / malicious patch guidance.
- **Mitigations:** HTTPS only; pin known URLs; optionally pin checksums/versions; verify Git commits/PRs via normal branch protection.
- **Risk:** compromised container image.
- **Mitigations:** publish digests/signatures later; prefer official base images; dependency scanning in CI.

### Repudiation

- **Risk:** unclear who approved a bad migration.
- **Mitigations:** draft PRs; require CODEOWNERS/review; local audit log of jobs (who/what/when); no auto-merge by default.

### Information disclosure

- **Risk:** source code leaves the network via BYOK LLM prompts.
- **Mitigations:** detect-only without LLM; minimize snippets; best-effort secret redaction; Ollama/offline mode; document clearly in SECURITY.md.
- **Risk:** SQLite / logs contain paths and code fragments.
- **Mitigations:** protect the data volume; no telemetry by default; clear data-retention guidance.

### Denial of service

- **Risk:** huge monorepo scan exhausts CPU/disk; LLM cost runaway.
- **Mitigations:** repo allowlists; file globs; size limits; timeouts; rate limits; LLM only on filtered hits; job quotas.

### Elevation of privilege

- **Risk:** Orchestrator/Engine RCE → access to all mounted repos and secrets.
- **Mitigations:** treat both services as the trusted computing base; do not expose admin ports publicly; run as non-root in containers where practical; never turn untrusted model/provider input into shell commands; sandbox patch application carefully.

## 6. Critical abuse cases

### A. Poisoned migration PR

An attacker influences inputs (tampered contract feed or prompt injection via fetched docs) so ApiMorph opens a PR that looks helpful but introduces a backdoor.

**Controls:** human review required; show confidence + evidence; prefer deterministic rules for high-risk changes; never auto-merge; display exact sources used for the change.

### B. Secret exfiltration via LLM

Prompts include API keys accidentally present in code.

**Controls:** best-effort pre-prompt secret scanning/redaction; warn operators; recommend Ollama for sensitive estates; minimize context to hit fragments only.

### C. Overscoped GitHub credentials

A classic PAT with org-wide access used "just for demo" remains in production.

**Controls:** docs push GitHub App + least privilege; installer warns on classic PATs; example configs use least scopes.

### D. Internal API exposed accidentally

A user publishes the Orchestrator port to `0.0.0.0` on a public VM.

**Controls:** Compose binds to localhost by default; security docs emphasize outbound-only; no public ingress in the reference architecture.

## 7. Data flow classification

| Data | Stored where | Leaves customer network? |
| --- | --- | --- |
| Full repo clone | local volume | No |
| Findings / snippets | SQLite + PR body | PR body → GitHub (intended); LLM only if enabled |
| GitHub tokens | env/secret mount | To GitHub HTTPS only |
| LLM prompts | ephemeral | To BYOK endpoint only if enabled |
| OpenAPI specs | local cache | Fetched from provider |

## 8. MVP security non-negotiables

1. Default bind: localhost / internal Docker network only
2. Default PR mode: draft + no auto-merge
3. Detection must work with `LLM_ENABLED=false`
4. `.env`, keys, `*.db`, and clones are gitignored
5. Document BYOK code-snippet risk explicitly
6. Treat OpenAPI + LLM output as untrusted input
7. Outbound HTTPS only in the reference deployment story

## 9. Out of scope for this sketch

- Formal pen-test report
- FedRAMP / ISO mapping
- Multi-tenant SaaS isolation (a new threat model is required before SaaS)
- Formal verification of patch correctness

## 10. Next security milestones

- [ ] Enable GitHub Private Vulnerability Reporting
- [ ] Add a real `security@` contact
- [ ] Non-root containers + read-only rootfs where feasible
- [ ] Secret redaction tests in CI
- [ ] Image signing / provenance for releases
- [ ] Separate threat model update before any hybrid SaaS control plane
