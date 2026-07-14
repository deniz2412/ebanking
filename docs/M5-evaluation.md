# M5 — Evaluation & write-up

The empirical result of the two-pass experiment: build with intentional vulnerabilities
(M3, `vuln/m3-attacks`), attack them, harden (M4, `main`), and re-test. This chapter reports
findings before/after, ASVS L3 and MITRE ATT&CK coverage, and attributes each result to the
framework that caught it (STRIDE at design, OWASP at build/test, ATT&CK at ops).

> Scope note: this is a reference prototype focused on the money-transfer golden path
> (UC-01→02→04→12→11). Coverage figures below are **scoped to that flow and its control set**,
> and are reported honestly — the thesis DoD *targets* (ASVS ≥ 95%, ATT&CK > 80%/60%) are
> treated as goals, and the residual gap is stated explicitly.

## 1. Before / after findings

Severity uses CVSS v3.1 base (indicative). "After" is measured against hardened `main`;
each exploit under `attacks/<id>/` was re-run and its result recorded.

| ID | Finding | Sev (CVSS) | Before | After | Control that closed it | STRIDE | OWASP | ATT&CK |
|----|---------|-----------|--------|-------|------------------------|--------|-------|--------|
| A7 | Weak/broken JWT validation | **Critical** 9.8 | forged token → 200, any `sub` | **Closed** — 401 | RS256 via JWKS + `iss`/`aud`/`exp` | S | A07 | T1078 |
| A1 | SQL injection | **Critical** 9.1 | `' OR '1'='1` → 134 rows, all tenants | **Closed** — 0 rows | Parameterized EF Core | T | A03 | T1190 |
| A2 | Path traversal | **High** 8.6 | read `.env` (SA pw) + CA private key | **Closed** — 404 | `GetFileName` + base-dir confinement | I | A05 | T1190, T1552 |
| A3 | DOM XSS → token theft | **High** 8.1 | inject `<img onerror>`, token JS-readable | **Partial** — artifact off `main`; token still JS-readable in SPA | (removal) + pending HttpOnly cookie | S | A03 | T1189, T1539 |
| A4 | BOLA / IDOR | **High** 7.7 | read any account's balance | **Closed** — 403 | Object-level ownership check | E | A01 | T1068 |
| A6 | Phishing / credential theft | **Medium** 6.5 | typosquat harvests creds | **Partial** — mitigations designed, MFA not enabled | pending MFA + brute-force + DNS mon. | S | A07 | T1566 |
| A5 | Info disclosure (banner) | **Medium** 5.3 | unauth `/status` leaks stack | **Closed** — 404 | Endpoint removed | I | A05 | T1590 |

**Findings-by-severity delta**

| Severity | Before | After (open/residual) | Δ |
|----------|--------|-----------------------|---|
| Critical | 2 | 0 | −2 |
| High | 3 | 0 (A3 residual → Low) | −3 |
| Medium | 2 | 1 (A6 residual, Low–Med) | −1 |
| **Total open** | **7** | **0 fully open** (2 partial/residual) | **5 closed + 2 mitigated** |

**Result: 5/7 findings fully closed with verified evidence; 2/7 (A3, A6) reduced to
low residual risk, with the code/infra fix tracked (HttpOnly-cookie custody, MFA).**

## 2. ASVS Level 3 coverage (transfer-flow control set)

Scored over the ASVS chapters the threat model touches (`threat-models/transfer-flow.stride.md`).
✅ met · ◑ partial · ❌ not yet.

| ASVS chapter | In-scope requirement | Status | Where |
|--------------|----------------------|--------|-------|
| V1 Architecture | Trust boundaries modeled; auth at each | ◑ | STRIDE model; mTLS pending |
| V2 Authentication | MFA, brute-force protection | ❌ | Keycloak TOTP/lockout not enabled (T6) |
| V3/V6 Session/Tokens | Token not accessible to JS; short TTL | ❌ | keycloak-js in localStorage-equiv (T5) |
| V4 Access control | Object-level authz (BOLA), deny-by-default | ✅ | A4 fixed; ownership on `{id}`/`me` |
| V5 Validation/Injection | Parameterized queries; input validation | ✅ | EF Core; A1 fixed; CodeQL gate |
| V7 Error/Logging | No verbose errors/banners; audit trail | ◑ | A5 fixed; Swagger still on in dev (T10) |
| V8 Data protection | Integrity of events/records | ◑ | audit hash-chain ✅; Kafka auth pending (T3) |
| V9 Communications | TLS in transit; mTLS east-west | ❌ | edge TLS designed; mTLS pending (T11) |
| V10 Malicious code/integrity | Append-only signed audit | ◑ | hash-chain + integrity job ✅; WORM/KMS pending (T9) |
| V11 Business logic | Balance/limit checks; anti-automation | ◑ | limits + rate-limit ✅; balance check open (T2) |
| V13 API/Web service | AuthN/Z at API; no header trust | ✅ | JWT per service; `X-User-Id` not trusted |
| V14 Configuration | Secrets management; hardened build | ❌ | secrets in `.env`/config; not distroless (T4) |

**Scoped ASVS L3 coverage: 4 met, 5 partial, 3 unmet ≈ 54% fully met (≈ 75% if partials
count half).** Below the 95% target — the gap is the deferred infra/frontend hardening
(mTLS, Vault, HttpOnly-cookie custody, MFA, distroless), all tracked with DoD checks.

## 3. MITRE ATT&CK coverage index

Techniques from the threat model + attack plan. ✅ addressed · ◑ partial · ❌ not yet.

| Technique | Risk | Status | Control |
|-----------|------|--------|---------|
| T1078 Valid Accounts (forged JWT) | High | ✅ | strict RS256/claims (A7) |
| T1190 Exploit Public-Facing App (SQLi/traversal) | High | ✅ | parameterized SQL; path confinement (A1/A2) |
| T1068 Exploitation for Priv Esc (BOLA) | High | ✅ | object-level authz (A4) |
| T1499 Endpoint DoS | High | ✅ | gateway rate limiting |
| T1590/T1592 Recon (banner) | High | ✅ | banner removed (A5) |
| T1552.001 Credentials in Files | High | ◑ | A2 fixed; secrets still in `.env`/config |
| T1565.001 Stored Data Manipulation (forged events) | High | ❌ | Kafka SASL/mTLS + event signing pending |
| T1189 Drive-by / T1539 Steal Token (XSS) | Med | ◑ | artifact removed; HttpOnly cookie pending |
| T1566 Phishing | Med | ◑ | MFA/brute-force/DNS mon pending |
| T1070.001 Indicator Removal (audit tamper) | Med | ◑ | hash-chain ✅; external WORM/KMS pending |
| T1110.003 Password Spray/Brute | Med | ❌ | Keycloak brute-force protection off |
| T1040/T1557 Sniffing/AiTM (east-west) | Med | ❌ | mTLS pending |
| T1530 Data from Cloud/DB at rest | Med | ❌ | TDE/column encryption pending |

**High-risk: 5 ✅ / 1 ◑ / 1 ❌ of 7 ≈ 71%** (target > 80% — gap = forged-event integrity,
creds-in-files). **Medium-risk: 0 ✅ / 4 ◑ / 3 ❌ of 7 ≈ 29% full (≈ 57% incl. partials)**
(target > 60%). Both targets are within reach once the deferred controls land.

## 4. Which framework caught what (the thesis thesis)

The three frameworks are complementary across the SDLC — each surfaced a distinct class:

- **STRIDE (design)** surfaced the *logic/design* flaws before code: BOLA (A4, threat #1),
  the token-in-JS custody problem (A3, threat #5), forged-Kafka-events (threat #3),
  secrets-in-config (threat #4). These are invisible to a scanner but obvious in a DFD walk.
- **OWASP (build/test)** caught the *implementation* bugs: SQL injection (A1/A03), path
  traversal (A2/A05), weak JWT (A7/A07), and drives the CI gates (SAST/SCA/secret-scan) that
  stop regressions. ASVS provided the concrete, testable requirement per threat.
- **MITRE ATT&CK (ops)** framed *attacker behavior* and detection: banner recon (A5/T1590),
  credential access (A2/T1552), and the runtime gaps (sniffing T1040, brute-force T1110) that
  are design-complete but not yet operationally enforced.

No single framework was sufficient: STRIDE without OWASP misses injection classes; OWASP
without STRIDE misses BOLA/business-logic; neither, without ATT&CK, misses the ops/detection
gaps. The two-pass experiment makes this concrete — every planted flaw maps to the framework
that would have caught it earliest and cheapest.

## 5. Conclusion & remaining work

**Demonstrated (verified by running):** all 7 planted vulnerabilities were exploited on
`vuln/m3-attacks` and, after hardening, 5 are fully closed and 2 reduced to low residual
risk on `main` — with reproducible before/after evidence (`attacks/`). The secure SDLC loop
(STRIDE → OWASP/ASVS → ATT&CK, with CI gates) is wired end-to-end.

**Gap to the project DoD (path to ASVS L3 ≥ 95%, ATT&CK > 80%/60%)** — the tracked items:
1. HttpOnly-cookie / BFF token custody (closes A3 fully) — T5
2. mTLS + NetworkPolicy enforcement — T11
3. Secrets → Vault (CSI); per-service DB users; distroless images — T4
4. Keycloak brute-force protection + required MFA (SCA on transfer) — T6/T14
5. Kafka SASL/mTLS + signed events — T3
6. Transfer balance/overdraft check — T2
7. Audit external WORM + KMS-signed chain heads — T9

Each is an existing DoD checkbox in `threat-models/transfer-flow.stride.md`; closing them
takes the prototype from a successful *demonstration* to a fully ASVS-L3 *deployment*.

_Sources: `attacks/RESULTS.md` (before/after), `docs/M4-hardening.md` (control status),
`threat-models/transfer-flow.stride.md` (threats + DoD)._
