---
name: dotnet-microservice
description: Scaffolds a new ASP.NET 8 + EF Core microservice for the eBanking Secure project, wired to the project's security baseline (JWT validation, mTLS, Vault secrets, Kafka, EF Core against SQL Server, and the hardening headers). Use when adding a new service (e.g. Account, Payment, Transfer, Notification, Audit) or a new endpoint on an existing one.
---

# .NET microservice scaffolder

You scaffold ASP.NET 8 + EF Core services that match this project's conventions. Do not
invent a different stack — read `docs/context/02-microservices.md` and `04-tech-stack.md`
first, and confirm which service you're building and which use cases it backs (doc 03).

## Every service gets, by default

**Project shape**
- ASP.NET 8 minimal API (or controllers — match existing services), EF Core with a
  `DbContext` scoped to that service's own tables (no shared schema ownership).
- `Dockerfile`: multi-stage → distroless final image, non-root user, read-only rootfs.
- Kubernetes manifests: `Deployment`, `Service`, `NetworkPolicy` (deny-all + explicit
  allows), Vault CSI `SecretProviderClass`.

**Security baseline (non-negotiable)**
- JWT auth: validate signature → `aud`/`iss`/`exp` → scope. Trust Keycloak's signing keys.
- Authorization via policies (RBAC) delegating to OPA where practical; **ownership checks
  on every `{id}` resource**.
- mTLS for inter-service calls (client cert from Vault).
- Config/secrets from Vault only — connection strings, keys, certs. Nothing in
  `appsettings.json` that's sensitive.
- Parameterized EF Core queries only.
- Response hardening: HSTS, CSP, `X-Frame-Options`, `Referrer-Policy` (or confirm the
  ingress sets them and don't duplicate).
- Structured logging to the audit path for security-relevant actions; no secrets/PII in
  logs.

**Integration**
- If the service publishes/consumes domain events, wire a Kafka producer/consumer
  (Transfer publishes `TransferCompleteEvent`; Notification + Audit consume).
- Health endpoint that does **not** leak OS/runtime versions (contrast the deliberately
  vulnerable `/status` in the attack plan).

## Steps
1. Confirm service name, owned tables, use cases, and events.
2. Model it with STRIDE first (use the `stride-threat-modeling` skill) → save to
   `threat-models/<service>.stride.md`.
3. Scaffold the project + Docker + K8s + the baseline above.
4. Add tests that assert the STRIDE-derived controls (auth required, ownership enforced,
   injection blocked).
5. Hand off to `security-reviewer` before merge.

## Note on the vulnerable pass
If explicitly asked for the vulnerable version of an endpoint, implement the exact planted
flaw from `docs/context/08-attack-demo-plan.md`, keep it on a `vuln/*` branch, and document
it under `attacks/<id>/`. Never weaken the baseline on `main`.
