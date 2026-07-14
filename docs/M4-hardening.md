# M4 — Hardened pass

`main` is the hardened/deployable branch. Every intentional M3 vulnerability is removed
(no `Vuln:*` flags, no vulnerable code paths), the exploits from `attacks/` were re-run and
all fail, and the doc-07 CI security gates are wired.

## Patches & closure (re-tested against hardened `main`)

| Attack | Patch | Closure result |
|--------|-------|----------------|
| A1 SQLi | `InvoiceController.Search` → parameterized `FromSqlInterpolated` only | injection → **0 rows** |
| A2 Path traversal | `InvoiceController.Download` → `Path.GetFileName` + base-dir confinement | traversal → **404** |
| A4 BOLA/IDOR | `AccountsController.GetByAccountNumber` → always enforces owner == `sub` | cross-user → **403** |
| A5 Info disclosure | `/status` banner removed from the gateway | `/status` → **404** |
| A7 Weak JWT | strict RS256 via JWKS + `iss`/`aud`/`exp`; weak-validation path deleted | forged token → **401** |
| A3 DOM XSS | live artifact off `main`; fix = HttpOnly-cookie token custody + sanitizer + CSP | see "deferred" |
| A6 Phishing | live artifact off `main`; mitigations = MFA + brute-force protection + DNS monitoring | see "deferred" |

Re-run any exploit against a hardened stack: each prints `[+] HARDENED`.

## Hardening baseline status (docs/context/04 §hardening + threat-model DoD)

| Control | Status | Notes / DoD |
|---------|--------|-------------|
| Parameterized SQL (EF Core) everywhere | ✅ applied | rule #5; CodeQL gate guards regressions (T12) |
| Object-level authorization (ownership) | ✅ applied | account `by-number`/`me` (T1/T8) |
| Strict JWT: RS256 + `iss`/`aud`/`exp`, per service | ✅ applied | gateway **and** every service re-validate (T5 partial/T7) |
| Gateway rate limiting | ✅ applied | per-route 15–60/min → 429 (T13) |
| Security headers + CSP (`script-src 'self'`) | ✅ applied | gateway + all services now `UseSecurityHeaders` |
| Append-only, hash-chained audit log + integrity job | ✅ applied | Repudiation control (T9 partial) |
| Idempotency on transfers | ✅ applied | replay protection |
| CI gates: secret-scan, SAST, SCA, IaC, fs-scan, SBOM | ✅ applied | `.github/workflows/ci.yml` + `dependabot.yml` |
| Balance/overdraft check on transfer | ⬜ **open** | T2 — transfer validates limits but not balance |
| Token in HttpOnly cookie (BFF), not `keycloak-js` | ⬜ **deferred** | T5 — closes A3 root cause; SPA change |
| mTLS between services | ⬜ **deferred** | T11 — infra (cert-manager/mesh) |
| Secrets from Vault (CSI), not `.env`/appsettings | ⬜ **deferred** | T4 — A2/A4 evidence; per-service DB users |
| Keycloak brute-force protection + required MFA | ⬜ **deferred** | T6 — realm `bruteForceProtected` + TOTP |
| Distroless image, read-only FS, seccomp/AppArmor | ⬜ **deferred** | containers currently `aspnet:8.0` |
| NetworkPolicy enforcement + OPA Gatekeeper | ⬜ **deferred** | manifests exist; not admission-enforced in-cluster |
| Image build+Trivy image scan, cosign signing | ⬜ **deferred** | CD/deploy stage (documented in ci.yml) |

## CI/CD security gates (halt on finding)

`.github/workflows/ci.yml`: **secret-scan** (Gitleaks) → **SAST** (CodeQL C#/JS,
security-extended) → **SCA** (`dotnet list --vulnerable` + `npm audit`) → **IaC**
(kube-linter + Trivy config) → build/test → **fs-scan** (Trivy vuln+secret+misconfig) →
**SBOM** (Syft/SPDX). Plus `.github/dependabot.yml` for ongoing SCA.

Deploy-stage gates (image scan, cosign signing, Gatekeeper admission, branch protection,
post-deploy rollback) are documented in `docs/context/07` and noted in the workflow; they
run in the CD/cluster layer, not this CI.

## For M5 (evaluation)

Open items above are the remaining path to **ASVS L3 ≥ 95%**. The before/after table
(`attacks/RESULTS.md`) + this status feed the M5 coverage report: findings-by-severity delta,
ASVS/ATT&CK coverage, and which framework (STRIDE/OWASP/ATT&CK) caught each class.
