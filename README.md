# eBanking Secure — Reference Implementation

Companion codebase for the bachelor thesis **"Sigurnost i zaštita veb aplikacija"**
(Security and Protection of Web Applications) by Deniz Hadžirušidović, VŠRI eMPIRICOM.

This repository turns **Chapter 4** of the thesis (the e-banking case study) into a
working, security-focused reference implementation. The goal, per the thesis, is a
**two-pass** demonstration:

1. **Vulnerable pass** — a realistic e-banking prototype with intentionally planted
   vulnerabilities, attacked with **Burp Suite** and **OWASP ZAP**.
2. **Hardened pass** — the same application with the vulnerabilities patched, plus a
   before/after evaluation of the security controls.

It ties three frameworks together across the SDLC — **STRIDE** (design), **OWASP**
(implementation & testing), and **MITRE ATT&CK** (operations) — the central thesis of
the work.

---

## Where to start

All the extracted architecture and design context lives in [`docs/context/`](docs/context/).
Read [`docs/context/00-index.md`](docs/context/00-index.md) first — it is the map of
everything else.

If you are an AI coding agent, read [`CLAUDE.md`](CLAUDE.md) before touching code.

## Repository layout

```
ebanking/                         # this repo — the single source-of-truth implementation
├── README.md                     # this file
├── CLAUDE.md                     # working context for AI coding agents
├── docs/
│   ├── context/                  # extracted architecture & design docs (start here)
│   ├── c4/                       # C4 diagrams
│   ├── threat-model/             # threat-model notes
│   └── quickstart-phase0-1.md    # operational quick-start (Phase 0/1)
├── .claude/
│   ├── agents/                   # project subagents (security review, service scaffolding)
│   └── skills/                   # project skills (STRIDE threat modeling)
├── services/                     # ASP.NET 8 microservices + Ocelot gateway
│   ├── account/  transfer/  payment/  notification/  audit/
│   ├── gateway-ocelot/           # Ocelot API gateway
│   └── shared/
├── frontend/web/                 # Angular SPA
├── infrastructure/               # Kubernetes manifests + Helm (the three zones)
├── threat-models/                # STRIDE models per component (DFD + matrices)
└── attacks/                      # attack scripts, ZAP/Burp configs, PoCs
```

> Earlier single/two-service prototypes (`AccountService`, `ebanking-backend`,
> `ebanking-project`) were superseded by this monorepo and moved to `../_archive/` for
> reference; they are not part of the build.

## Tech stack (summary)

Angular SPA · NGINX Ingress + WAF (ModSecurity CRS) · Ocelot API Gateway (.NET) ·
Keycloak (OAuth2/OIDC) · ASP.NET 8 + EF Core microservices · Apache Kafka ·
MS SQL Server 2022 · HashiCorp Vault · Kubernetes · GitHub Actions CI/CD.

See [`docs/context/04-tech-stack.md`](docs/context/04-tech-stack.md) for the full list
and versions.

## Status

Golden-path build in progress (roadmap M0–M1, see `docs/context/09-roadmap.md`). The
Ocelot gateway and the Account/Transfer/Payment/Notification/Audit services are scaffolded
on ASP.NET 8 + EF Core, the Angular SPA shell exists under `frontend/web`, and the
Kubernetes/Helm infrastructure for the three zones (Keycloak, MSSQL, Kafka/Redpanda, Vault,
cert-manager, NetworkPolicies, Ingress) is in `infrastructure/`. See `PROGRESS.md` for the
per-story breakdown. The STRIDE threat models (`threat-models/`) and the A1–A7 attack
demos (`attacks/`) are not yet populated.

> ⚠️ **This project deliberately contains and demonstrates vulnerabilities.** The
> vulnerable pass must only ever run in an isolated lab environment. Never expose it to
> a public network or use real personal/financial data. See
> [`docs/context/08-attack-demo-plan.md`](docs/context/08-attack-demo-plan.md).
