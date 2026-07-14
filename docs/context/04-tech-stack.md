# Tech stack

Source: thesis §4 intro, §4.1.2, §3.7, Diagram 4-2.

| Layer | Technology | Notes |
|-------|-----------|-------|
| Frontend | **Angular** (SPA) | Served behind NGINX Ingress. Tokens kept in HttpOnly/Secure cookies, **not** localStorage. |
| Edge | **NGINX Ingress + WAF** | TLS 1.3 termination, OWASP **ModSecurity Core Rule Set**, edge rate limiting. |
| API Gateway | **Ocelot** (.NET) | Routing, JWT validation, rate limiting. |
| Identity | **Keycloak** | OAuth 2.0 / OIDC, MFA, JWT issuance. Replaces the custom Identity-Service (see 00-index). |
| Services | **ASP.NET 8 + EF Core** | Account, Payment, Transfer, Notification, Audit. |
| Event bus | **Apache Kafka** | `TransferCompleteEvent` etc.; consumed by Notification + Audit. Dev/local uses **Redpanda** (wire-compatible Kafka) — see `infrastructure/`. |
| Database | **MS SQL Server 2022** | EF Core over port 1433; encrypted at rest. |
| Secrets | **HashiCorp Vault** | K8s CSI driver injects at pod start; rotation without rebuild. |
| Orchestration | **Kubernetes** | Three network zones enforced with NetworkPolicy + mTLS. |
| CI/CD | **GitHub Actions** | Secure pipeline — see doc 07. |
| AuthZ policy | **OPA (Open Policy Agent)** | Policy-as-code; RBAC → ABAC → PBAC. |
| Security testing | **Burp Suite**, **OWASP ZAP** | Drive the attack demos (doc 08). |

## Supporting security tooling (from Chapter 3, adopt as needed)

- **SAST:** CodeQL or SonarQube.
- **SCA / dependency scanning:** OWASP Dependency-Check, Dependabot, Snyk; compared against
  the CVE database; build fails on CVSS > 7.
- **Secret scanning:** Gitleaks / GitGuardian on commits and diffs.
- **SBOM:** CycloneDX or SPDX, signed (GPG/KMS).
- **Container image scanning:** Trivy or Grype.
- **IaC scanning:** terrascan (for Terraform), plus Kubernetes manifest checks.
- **Runtime defense:** Suricata (IDS/IPS), a SIEM/SOAR for correlation, OPA/Gatekeeper for
  admission policy.
- **Threat modeling:** OWASP Threat Dragon or Microsoft Threat Modeling Tool (see the
  STRIDE skill in `.claude/skills/`).

## Hardening baseline (thesis §3.7.3) — bake into every service/container

- **Transport:** TLS 1.3 only; HSTS `max-age` ≥ 6 months (thesis example uses 2 years);
  OCSP stapling.
- **Browser headers:** CSP (script-src self + nonce), `X-Frame-Options: DENY`,
  `Referrer-Policy`.
- **Containers:** distroless/Alpine base, read-only filesystem, `noexec` on `/tmp`,
  `seccomp` + AppArmor/SELinux profiles.
- **Network:** mTLS everywhere, Kubernetes NetworkPolicy (e.g. DB only reachable from
  `role=api` pods on 5432 — note: SQL Server uses 1433, adjust the example accordingly),
  bastion for admin access.
- **JWT:** signed RS256/ES256 with `aud`/`iss`/`iat`/`exp`; rotating access + refresh
  tokens; stored client-side only in HttpOnly/Secure cookies.

Concrete config snippets (NGINX TLS/HSTS/CSP, Kubernetes NetworkPolicy, SAML assertion, JWT
claims) are given in thesis §3.7.5 and should be lifted into `src/` config as the baseline.
