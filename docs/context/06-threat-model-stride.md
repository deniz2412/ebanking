# STRIDE threat model — money-transfer flow

Source: thesis §3.1.3 (worked e-banking example).

This is the reference threat model for the app's most critical data flow: the mobile/SPA
client → API Gateway path that moves money. New components should be modeled the same way
(see the `stride-threat-modeling` skill in `.claude/skills/`).

## Process (as applied)

1. **Context diagram** — actors, external systems, data flows. Trust boundaries: client/
   mobile apps, API gateway, backend services, core banking.
2. **Decompose** — services, databases, message queues → a level-1/2 DFD with trust
   boundaries marked at each node.
3. **Enumerate** — walk every flow/component through S,T,R,I,D,E.
4. **Rate** — STRIDE has no built-in risk metric; combine with OWASP Risk Rating or
   ISO/IEC 27005. In banking, criticality is driven by impact on financial-data
   confidentiality and regulatory context (PSD2, GDPR).
5. **Mitigate** — one control per high-risk scenario; feed into the design as requirements.
6. **Keep it live** — refresh the model on every architectural change; telemetry from
   WAF/SIEM/EDR maps back into STRIDE categories in production.

## Threats on the flow: mobile/SPA ↔ API Gateway

| STRIDE | Scenario | Mitigation | ASVS | ATT&CK |
|--------|----------|-----------|------|--------|
| **S** Spoofing | Steal auth tokens, impersonate identity | MFA; verify JWT `aud`/`exp`; token-binding | V2/V6 | T1078, T1110.003 |
| **T** Tampering | Alter the JSON `amount` or destination account field | TLS 1.3; sign request body (HMAC) | V8 | T1565.001 |
| **R** Repudiation | No record that the transaction was signed/authorized | Signed, append-only audit log (Audit-Service) | V7/V10 | T1070 |
| **I** Info Disclosure | Personal data leaks in transit | End-to-end encryption; minimal responses | V9 | T1041 |
| **D** Denial of Service | Flood the `/token` endpoint | Rate limiting; CAPTCHA on suspicion | V17 | T1499 |
| **E** Elevation of Privilege | BOLA on `PUT /accounts/{id}` | Ownership check + RBAC/OPA | V4/V5 | T1068 |

Each identified threat becomes a **security requirement with a Definition-of-Done check**
(e.g. "a user story is not done until tests confirm tampering is mitigated by HMAC and all
API calls are written to the immutable log").

## STRIDE across the SDLC (§3.1.4)

- **Requirements:** turn PSD2 SCA / GDPR into security tasks; initial STRIDE pass surfaces
  non-functional requirements (Spoofing risk ⇒ MFA is mandatory).
- **Design:** build DFDs with trust boundaries; full STRIDE pass per flow → risk-ranked
  threat catalog → architecture decisions (TLS choice, WAF, JSON schema).
- **Implementation:** link the catalog to DoD; enforce via CI/CD.
- **Test:** convert STRIDE scenarios into test cases (e.g. attempt login with a "stolen"
  token; probe for SQLi).
- **Operations:** map WAF/SIEM/EDR telemetry back to STRIDE; DevSecOps updates the model —
  closing the loop.

## Deliverable location

Store per-component models under [`/threat-models/`](../../threat-models/) as
`COMPONENT.stride.md` using the skill's template, plus a Threat Dragon `.json` if generated.
