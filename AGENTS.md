# AGENTS.md — working context

You are helping build **eBanking Secure**, the reference implementation for a thesis on
web application security. Read this before writing code.

## What this project is
A microservice e-banking app built to demonstrate secure development across the SDLC using
three frameworks: **STRIDE** (design), **OWASP** (build/test), **MITRE ATT&CK** (ops). It is
built in two passes: an intentionally **vulnerable** pass, then a **hardened** pass, with a
before/after evaluation.

## Read first
- `docs/context/00-index.md` — map of all context docs. **Start there.**
- `docs/context/01-architecture-overview.md` — the three zones and main flow.
- `docs/context/04-tech-stack.md` — exact technologies.

## Non-negotiable decisions (don't silently change these)
1. **Identity = Keycloak** (OAuth2/OIDC). There is **no** custom Identity-Service, despite
   the thesis diagram. See `docs/context/00-index.md`.
2. **API Gateway = Ocelot (.NET).**
3. Services = **ASP.NET 8 + EF Core**; DB = **MS SQL Server 2022**; events = **Kafka**;
   secrets = **HashiCorp Vault**; orchestration = **Kubernetes**; CI/CD = **GitHub Actions**.
4. **JWT lives in HttpOnly/Secure cookies, never localStorage.**
5. **No string-concatenated SQL, ever.** Parameterized queries / EF Core only.
6. **Secrets only from Vault** — never in code, config files, or committed anywhere.

## The two passes — critical
- Intentional vulnerabilities live **only** on `vuln/*` branches or behind an explicit
  build flag, and only for attacks listed in `docs/context/08-attack-demo-plan.md`.
- **Never** introduce a vulnerability on `main` or any deployable branch.
- When asked to "add the vulnerable version", implement exactly the planned flaw, document
  it in `attacks/<id>/`, and keep the fix documented alongside so the hardened pass is easy.
- On `main`, apply the hardening baseline in `docs/context/04-tech-stack.md`.

## Working style
- Before building a new component, model it with STRIDE — use the `stride-threat-modeling`
  skill and drop the result in `threat-models/`.
- Every feature is done only when its STRIDE-derived controls are tested (Definition of
  Done, see `docs/context/06-threat-model-stride.md`).
- Keep the context docs accurate: if a decision changes, update `docs/context/` in the same
  change and note it in `00-index.md`.
- Prefer the config snippets in thesis §3.7.5 (NGINX TLS/HSTS/CSP, K8s NetworkPolicy, JWT
  claims) as the baseline rather than inventing new ones.

## Subagents available (`.Codex/agents/`)
- `security-reviewer` — reviews diffs against STRIDE/OWASP/ATT&CK and the project rules.
- `dotnet-microservice` — scaffolds a new ASP.NET 8 + EF Core service to the project's
  security baseline.

## Safety
This repo demonstrates real attacks. All offensive work is lab-only, against this app,
with synthetic data. Do not build exploits targeting third-party systems.
