# Security Policy

## Supported versions

ApiMorph is in early development. Security fixes are applied on a best-effort basis to the default branch (`main`).

| Version | Supported |
| --- | --- |
| `main` (pre-1.0) | :white_check_mark: |
| pre-release tags | :white_check_mark: (latest only) |
| obsolete forks / unmodified old commits | :x: |

## Product security principles

ApiMorph is designed for **self-hosted / on-prem** use:

1. **Source code privacy:** Customer repositories are cloned/scanned inside the customer environment. ApiMorph does not require uploading application source to an ApiMorph-operated cloud.
2. **Outbound-only networking:** The intended deployment exposes no inbound internet ports. Egress is HTTPS (443) to explicit dependencies (e.g. GitHub, provider OpenAPI endpoints, optional user-configured LLM endpoints).
3. **Least privilege:** GitHub credentials must be scoped to the minimum permissions needed (contents/read, pull requests/write, metadata/read). Avoid classic PATs with org-wide access in production.
4. **Human-in-the-loop:** Default behavior creates **draft** PRs only. Auto-merge is off unless explicitly enabled by the operator.
5. **Secrets stay local:** GitHub App private keys, PATs, LLM API keys, and database files remain on customer infrastructure. Prefer env files / secret mounts that are not committed to git.
6. **Optional offline LLM:** Operators may use a local model endpoint (e.g. Ollama) so prompt traffic never leaves the network. Detection itself must not require an LLM.
7. **No silent code exfiltration:** Telemetry, if ever added, must be opt-in and must not include source code, secrets, or file contents by default.

## Report a vulnerability

Please **do not** open a public GitHub Issue for security vulnerabilities.

Instead, report privately via one of:

- GitHub **Private Vulnerability Reporting** (Security advisory) on this repository, if enabled
- Email: `security@apimorph.dev` *(replace with your real contact before publishing)*

### Include in your report

- Affected version / commit hash
- Description of the issue and impact
- Reproduction steps or proof-of-concept
- Any known mitigations

### Our commitment

- We will acknowledge valid reports within **7 days**
- We will keep you informed about remediation progress
- We will credit reporters in the advisory unless you request anonymity
- We ask for reasonable time before public disclosure (coordinated disclosure)

## Sensitive capabilities (operators)

Operators should treat the following as high-risk configuration:

- GitHub App installation with write access to private repositories
- LLM BYOK keys (prompts may include **code snippets** from findings)
- Volume mounts containing clones of private repositories
- SQLite / data directories with job history and finding details

### Hardening checklist

- [ ] Run via Docker Compose on a dedicated host/VM
- [ ] Restrict host access (SSH, disk encryption where appropriate)
- [ ] Use a GitHub App with least privilege, limited to selected repos
- [ ] Keep `AUTO_MERGE=false` and prefer draft PRs
- [ ] If using cloud LLMs, understand that **selected code fragments** may leave the network; use Ollama/air-gapped mode for sensitive repos
- [ ] Do not commit `.env`, keys, or `*.db` files
- [ ] Rotate GitHub and LLM credentials periodically
- [ ] Review generated PRs before merge — ApiMorph can be wrong

## Upstream / dependency security

- Keep base images and dependencies updated (Dependabot/Renovate recommended)
- Pin versions in release artifacts where practical
- Treat provider OpenAPI feeds and LLM outputs as **untrusted input**

## Scope exclusions

The following are outside ApiMorph's security boundary unless explicitly stated otherwise:

- Security of the customer's GitHub org configuration
- Correctness of third-party API provider changes
- Model provider handling of BYOK prompt data
- Compromised developer workstations used to review/merge PRs
