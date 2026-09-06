# STRIDE threat model — money-transfer golden path (UC-01→02→04→12→11)

> Grounded in the M1 implementation (not just the thesis reference in
> `docs/context/06-threat-model-stride.md`). Several threats below are **present in the
> current code** and are called out as such — these feed directly into the M3 vulnerable
> pass (`docs/context/08-attack-demo-plan.md`) and the M4 hardening.

## Scope & DFD

The flow: a user authenticates via **Keycloak** (OIDC), the **Angular SPA** calls the
**Ocelot gateway** with a bearer JWT, the gateway validates the token + rate-limits + routes
to **Transfer-Service** (ASP.NET 8 + EF Core → MSSQL). Transfer books an internal transfer and
publishes `transfer.completed` to **Kafka/Redpanda**; **Notification-Service** (Web Push) and
**Audit-Service** (append-only hash-chained log) consume it.

```
                          TB1: client ↔ edge/gateway
   ┌─────────┐  OIDC   ┌──────────┐        │
   │  User   │────────▶│ Keycloak │        │
   └────┬────┘         └──────────┘        │
        │ JWT (bearer)                      │
   ┌────▼─────┐   HTTPS   ┌───────────────┐ │   TB2: gateway ↔ services
   │  SPA     │──────────▶│ Ocelot Gateway│─┼─────────┐
   │ (Angular)│           │ JWT+ratelimit │ │         │ HTTP (+JWT re-validated)
   └──────────┘           └───────────────┘ │         ▼
                                             │   ┌──────────────┐   validate owner/limits
                                             │   │Transfer-Svc  │──────▶ Account-Svc
                                             │   │ EF Core      │
                                             │   └──────┬───────┘
                          TB3: service ↔ data│          │ publish transfer.completed
        ┌────────────────────────────────────┼──────────▼────────────┐
        │  MSSQL (EF Core, 1433)     Kafka/Redpanda (9092)            │
        │                                  │        │                 │
        │                        Notification-Svc  Audit-Svc          │
        │                        (Web Push)        (hash-chained log) │
        └─────────────────────────────────────────────────────────────┘
```

Trust boundaries: **TB1** SPA/Keycloak ↔ gateway, **TB2** gateway ↔ services (and
service↔service), **TB3** services ↔ data stores / event bus.

## Threats

| # | STRIDE | Boundary | Scenario | Risk | Mitigation | ASVS L3 | ATT&CK |
|---|--------|----------|----------|------|-----------|---------|--------|
| 1 | **E** Elevation | TB2 | **BOLA — transfer *from* another user's account.** `TransferService.CreateTransferAsync` accepts `fromAccountNumber` from the body; `ValidationService.ValidateAccountOwnershipAsync` is a **stub that returns true**. `fromAccountNumber` is never tied to the token `sub`. *Present in code.* | **High** | Verify `fromAccountNumber` belongs to the authenticated `sub` via Account-Service; enforce with OPA policy; deny on mismatch. | V4.1.1, V4.1.3, V4.2.1 | T1068 |
| 2 | **T** Tampering | TB2 | **No balance/overdraft check.** Transfer validates *limits* but not available balance; amount ≤ 0 is rejected by `ValidateTransferLimits`, but a valid-range amount exceeding balance still books. Money can be moved that doesn't exist. *Present in code.* | **High** | Server-side balance check via Account-Service before booking; atomic debit; reject `amount > balance`. | V5.1.1, V11.1.2 | T1565.001 |
| 3 | **S/T** Spoofing/Tampering | TB3 | **Forged Kafka events.** Redpanda runs PLAINTEXT with no producer auth. Anyone who can reach 9092 can publish a fake `transfer.completed`; Audit logs it as a real settled transfer and Notification pushes a bogus alert. Consumers do not authenticate the producer or verify event integrity. *Present in infra.* | **High** | Kafka SASL/mTLS + topic ACLs (only Transfer may produce); sign events (producer key) and verify signature in consumers; NetworkPolicy isolates the broker. | V1.9.1, V8.3.1 | T1565, T1078 |
| 4 | **I** Info Disclosure | TB3 | **Secrets in config/repo.** VAPID **private** key is committed in `services/notification/appsettings.Development.json`; DB SA password lives in `appsettings`/`.env`; single `sa` login used by every service. | **High** | All secrets from Vault (CSI driver); rotate; gitleaks/secret-scan gate in CI; per-service least-privilege DB users (not `sa`). | V6.4.1, V1.4.4, V2.10.4 | T1552.001 |
| 5 | **S** Spoofing | TB1 | **Access token reachable by JavaScript.** The SPA uses `keycloak-js` (token in JS memory + bearer header), **not** the mandated HttpOnly/Secure cookie. A DOM-XSS (see attack A3) can exfiltrate the token and replay it. Deviates from CLAUDE.md non-negotiable #4. | **High** | Move token custody to a BFF / HttpOnly+Secure+SameSite cookie; short access-token TTL + refresh rotation; strict CSP (script-src self+nonce); MFA. | V3.4.1, V3.4.2, V14.4.3 | T1539, T1189 |
| 6 | **S** Spoofing | TB1 | **Credential stuffing / brute force at Keycloak.** Realm `ebanking` has `bruteForceProtected: false`; no account lockout. | **Med** | Enable Keycloak brute-force protection + lockout; mandatory MFA (TOTP already modeled); edge rate-limit on `/token`. | V2.2.1, V2.2.3 | T1110.003 |
| 7 | **S/E** Spoofing/Elevation | TB2 | **Dev auth bypass reaching a deployed env.** `Auth:DevBypass` set to `true` installs a fake handler authenticating every request as `dev-user-id` with all scopes + `audit-admin`. Gated to `IsDevelopment()` today, but a misconfig (env=Development in a real deploy) fully disables auth. | **Med** | Fail-closed: refuse to start with DevBypass in non-Development; CI/admission check asserting `DevBypass` set to `false`; alert on the dev scheme in prod logs. | V1.14.6, V14.1.1 | T1078 |
| 8 | **E** Elevation | TB2 | **BOLA on Account-Service reads.** Balance/transactions/statements keyed by `{id}`; must resolve to the caller. Currently `me`-scoped with an `AccountOwnershipHandler`, but the handler/ownership must be enforced on *every* `{id}` path (statements, details). | **Med** | Ownership verification on every `{id}`; OPA RBAC→ABAC; deny-by-default. | V4.1.1, V4.1.2 | T1068 |
| 9 | **R** Repudiation | TB3 | **Audit tamper / re-chaining.** Log is append-only + hash-chained (integrity verify works), but it lives in one MSSQL DB; an attacker with DB write access *and* the hashing logic could recompute a consistent chain, or the producer of forged events (threat 3) creates "authentic-looking" records. | **Med** | Ship records to WORM/external store; sign chain heads with KMS; periodic + alerting integrity checks (job exists); separate audit DB creds. | V7.1.1, V10.2.2 | T1070.001 |
| 10 | **I** Info Disclosure | TB1/TB2 | **Verbose surface.** Swagger UI + detailed errors are on in Development; a `/health` and framework banners can profile the stack (attack A5). | **Med** | Disable Swagger + stack traces in prod; generic error bodies; strip `Server`/version headers; no diagnostic endpoints public. | V7.4.1, V14.3.2 | T1590, T1592 |
| 11 | **I/T** Disclosure/Tampering | TB2 | **East-west traffic in cleartext; downstream trusts `X-User-Id`.** Locally services talk plain HTTP; gateway injects `X-User-Id` from claims. If a service is reachable directly (bypassing the gateway), a caller could forge `X-User-Id` or sniff traffic. | **Med** | mTLS between services; NetworkPolicy so services accept traffic only from the gateway/mesh; services derive identity **only** from the validated JWT (they do), never trust `X-User-Id`. | V1.9.1, V9.1.1, V13.2.1 | T1040, T1557 |
| 12 | **T** Tampering | TB3 | **SQL injection.** All data access is EF Core / parameterized (no string-concatenated SQL). Residual risk if raw SQL is later added (attack A1 plants exactly this on a `vuln/*` branch). | **Low** | Keep EF Core / parameterized queries only (CLAUDE.md rule #5); SAST/CodeQL gate; reject raw SQL in review. | V5.3.4, V5.3.5 | T1190 |
| 13 | **D** Denial of Service | TB1/TB2 | **Flooding transfer/token endpoints; unbounded pagination.** | **Med** | Gateway per-route rate limits (implemented: 20–50/min); pageSize capped at 100 (implemented); query timeouts; edge WAF + Keycloak throttling. | V11.1.4, V13.4.2 | T1499 |
| 14 | **R** Repudiation | TB1 | **No proof the user authorized *this* transfer.** Beyond the audit log, there's no per-transaction signing / SCA challenge, so a disputed transfer rests on server logs alone (PSD2 SCA context). | **Med** | PSD2 SCA (step-up MFA) on transfer with a signed confirmation bound to amount+payee; record the signed assertion in the audit log. | V6.3.1, V10.2.5 | T1070 |

## Security requirements (Definition of Done)

Each High/Medium threat yields a testable check. A transfer-flow story is **not done** until:

- [ ] **(T1)** A transfer where `fromAccountNumber` is *not* owned by the token `sub` is rejected (403); an automated test asserts user A cannot debit user B's account. *(closes A4)*
- [ ] **(T2)** A transfer for `amount > available balance` is rejected; test covers boundary (exactly balance succeeds, balance+0.01 fails) and negative/zero amounts.
- [ ] **(T3)** Consumers reject a `transfer.completed` event not produced by Transfer-Service (bad/absent signature); test publishes a forged event and asserts no notification and no audit record. Broker requires SASL/mTLS.
- [ ] **(T4)** No secret (VAPID private key, DB password, client secret) is present in source/config; secrets load from Vault; a gitleaks CI job fails the build on any committed secret. Per-service DB users replace `sa`.
- [ ] **(T5)** The access token is not readable by page JavaScript (HttpOnly cookie / BFF); a test confirms `document.cookie`/JS cannot read it and CSP blocks inline script. *(closes A3)*
- [ ] **(T6)** Keycloak brute-force protection is enabled (lockout after N failures) and MFA is required for the transfer flow; test asserts lockout and SCA challenge.
- [ ] **(T7)** Services fail to start (or refuse requests) when `Auth:DevBypass` set to `true` outside Development; CI asserts `DevBypass` set to `false` in all non-dev configs.
- [ ] **(T8)** Every Account-Service `{id}`/`me` path enforces ownership; test asserts cross-user access to balance, transactions, and statements returns 403/404. *(closes A4)*
- [ ] **(T9)** Audit records are shipped to append-only external storage and chain heads are signed; a tamper test (edit a row) is detected by the integrity job **and** alerts.
- [ ] **(T10)** Swagger, stack traces, and version/diagnostic banners are disabled in the prod profile; test asserts generic error bodies and no `Server` version header. *(closes A5)*
- [ ] **(T11)** Service-to-service calls use mTLS and NetworkPolicy restricts service ingress to the gateway/mesh; downstream identity is taken from the JWT, not `X-User-Id` (test forges `X-User-Id` and is ignored).
- [ ] **(T13)** Load test confirms rate limits return 429 past threshold and pagination is capped; no unbounded query.
- [ ] **(T14)** Transfer requires PSD2 SCA step-up; the signed confirmation (bound to amount+payee) is persisted in the audit log.

## Notes

- **Known deviations (accepted for M1, tracked for M4):** token custody is `keycloak-js`/bearer
  rather than the mandated HttpOnly cookie (threat 5); east-west is plain HTTP without mTLS
  (threat 11); secrets are in config not Vault (threat 4). These are the top hardening items.
- **Already mitigated in M1:** RS256 JWT with `iss`/`aud`/`exp` validated at the gateway *and*
  re-validated per service; per-route rate limiting; EF Core parameterized queries; append-only
  hash-chained audit log with a working integrity check; idempotency middleware on transfers.
- **Ties to other milestones:** threats 1, 5, 10, 12 are the planted vulnerabilities for M3
  (A4, A3, A5, A1). M4 hardening = the DoD checks above.
- **Revisit when:** external transfers / Payment-Service join the flow, the SPA token model
  changes, mTLS/Vault land, or any new topic/consumer is added.
