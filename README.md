# eBanking Secure — Reference Implementation

Companion codebase for the bachelor thesis **"Sigurnost i zaštita veb aplikacija"**
(Security and Protection of Web Applications) by Deniz Hadžirušidović, VŠRI eMPIRICOM.

It turns **Chapter 4** of the thesis (the e-banking case study) into a working,
security-focused reference implementation, built as a **two-pass** experiment:

1. **Vulnerable pass** (`vuln/m3-attacks` branch) — a realistic e-banking app with seven
   intentionally planted vulnerabilities (A1–A7), attacked with reproducible scripts.
2. **Hardened pass** (`main`) — the same app with the vulnerabilities patched, plus a
   before/after evaluation.

Three frameworks are tied together across the SDLC — **STRIDE** (design), **OWASP/ASVS**
(build & test), **MITRE ATT&CK** (operations) — which is the central thesis of the work.

> ⚠️ **This project deliberately demonstrates real attacks.** The vulnerable pass is
> lab-only, on synthetic data — never expose it publicly. See
> [`docs/context/08-attack-demo-plan.md`](docs/context/08-attack-demo-plan.md).

---

## Quick start

**Prerequisites:** Docker (Compose v2). For browser login, add the Keycloak hostname to your
hosts file once so the browser and containers resolve the same token issuer:

```
127.0.0.1 keycloak      # /etc/hosts  (Windows: C:\Windows\System32\drivers\etc\hosts)
```

### Run with Docker Compose

```bash
./deploy.sh up                 # infra + all 6 services   (deploy.ps1 on Windows)
./deploy.sh up --frontend      # + the Angular SPA on http://localhost:4200
./deploy.sh logs gateway       # tail a service
./deploy.sh down               # stop        ./deploy.sh clean   # stop + wipe data
```

| Surface | URL |
|---|---|
| Ocelot gateway | http://localhost:5000 |
| SPA (with `--frontend`) | http://localhost:4200 · login `testuser` / `password123` |
| Keycloak | http://localhost:8180 (`admin` / `admin123`) |
| Kafka console | http://localhost:8080 |

Try the golden path (view balance → transfer → notification):

```bash
TOKEN=$(curl -s -d client_id=ebanking-frontend -d grant_type=password \
  -d username=testuser -d password=password123 \
  -d 'scope=openid read:accounts write:transfers read:notifications' \
  http://localhost:8180/realms/ebanking/protocol/openid-connect/token | jq -r .access_token)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/accounts/me/balance
```

### Deploy to Kubernetes

```bash
./deploy.sh k8s-up             # build images, load into kind/minikube, apply manifests
./deploy.sh k8s-down
```

Applies [`infrastructure/k8s`](infrastructure/k8s) (namespaces = the three zones, MSSQL,
Redpanda, Keycloak, the services, gateway, frontend, NetworkPolicies, Ingress). Needs an
ingress controller; Vault and cert-manager are optional add-ons (apply `infrastructure/k8s/vault`
and `infrastructure/k8s/cert-manager` separately if your cluster has their operators).

### Local development (without containers)

Run infra in Compose and the services with `dotnet run` — see
[`DEVELOPMENT.md`](DEVELOPMENT.md).

## Repository layout

```
services/            ASP.NET 8 microservices (account, transfer, payment, notification,
                     audit) + Ocelot gateway + shared library
frontend/web/        Angular SPA (Dockerfile serves it via nginx, proxies /api → gateway)
infrastructure/k8s/  Kubernetes manifests (kustomization.yaml) for the three zones
docs/context/        extracted architecture & design docs — start at 00-index.md
docs/M4-hardening.md, docs/M5-evaluation.md   hardening status + before/after evaluation
threat-models/       STRIDE models (DFD + matrices → Definition-of-Done checks)
attacks/             A1–A7 exploits, evidence, and RESULTS.md (the before/after record)
deploy.sh / deploy.ps1   single deployment entry point (compose + k8s)
docker-compose.yml   full local stack
```

## Status — roadmap M1–M5 complete

| Milestone | State |
|---|---|
| M1 Golden path | ✅ login (Keycloak OIDC) → balance → transfer → Kafka event → notification + immutable audit, through the Ocelot gateway |
| M2 STRIDE model | ✅ [`threat-models/transfer-flow.stride.md`](threat-models/transfer-flow.stride.md) |
| M3 Vulnerable pass | ✅ A1–A7 on `vuln/m3-attacks`, exploits + evidence in [`attacks/`](attacks/) |
| M4 Hardened pass | ✅ patched on `main`, CI security gates, [`docs/M4-hardening.md`](docs/M4-hardening.md) |
| M5 Evaluation | ✅ [`docs/M5-evaluation.md`](docs/M5-evaluation.md) |

This is a **reference prototype**: the two-pass demonstration is complete (5/7 vulnerabilities
fully closed, 2 reduced to low residual), and the remaining path to full ASVS L3 (HttpOnly-cookie
token custody, mTLS, Vault, MFA) is tracked in the threat-model DoD and `docs/M4-hardening.md`.

## Tech stack

Angular SPA · NGINX Ingress + WAF · Ocelot gateway (.NET) · Keycloak (OAuth2/OIDC) ·
ASP.NET 8 + EF Core services · Apache Kafka (Redpanda locally) · MS SQL Server 2022 ·
HashiCorp Vault · Kubernetes · GitHub Actions. Full list:
[`docs/context/04-tech-stack.md`](docs/context/04-tech-stack.md).

If you are an AI coding agent, read [`CLAUDE.md`](CLAUDE.md) first.
