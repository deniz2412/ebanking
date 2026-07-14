---
name: stride-threat-modeling
description: Produce a STRIDE threat model for a component, data flow, or feature in the eBanking Secure project, and map each threat to OWASP ASVS and MITRE ATT&CK. Use this whenever adding or changing a service, endpoint, or data flow, when the user says "threat model", "STRIDE", "what could go wrong here", or before writing security-sensitive code — even if they don't say "STRIDE" explicitly.
---

# STRIDE threat modeling

Turn a component or data flow into a risk-ranked threat model with concrete, testable
mitigations. This is the design-phase control in the project's framework (STRIDE for design,
OWASP for build/test, ATT&CK for ops). The goal: catch logic and design flaws *before* the
first line of code, and produce security requirements you can put a Definition-of-Done check
against.

## When to use
Any new/changed service, endpoint, message flow, or trust boundary. Do this **before**
scaffolding code, not after.

## Process

1. **Scope it.** Name the component and its data flows. Draw a small level-1 DFD: actors,
   processes, data stores, and the flows between them. Mark **trust boundaries** (client ↔
   gateway, gateway ↔ service, service ↔ data) — most threats live on a boundary crossing.
2. **Enumerate.** For each flow and element, ask "can S, T, R, I, D, E happen here?" Use the
   category prompts below. Write down concrete scenarios, not abstractions ("attacker changes
   the `amount` field", not "tampering possible").
3. **Rate.** STRIDE has no built-in score — rate impact × likelihood (OWASP Risk Rating or
   ISO 27005). In this app, weight by impact on financial-data confidentiality/integrity and
   regulatory exposure (PSD2, GDPR).
4. **Mitigate.** One control per high/medium threat. Map each to ASVS and ATT&CK. Turn
   high-risk items into security requirements with a testable DoD.
5. **Record.** Save the model (template below) to `threat-models/<component>.stride.md`.

## Category prompts (with default controls for this project)

- **S — Spoofing** (authenticity): stolen tokens/cookies/session IDs; impersonation.
  → MFA; verify JWT `aud`/`exp`/`iss`; token binding. ASVS V2/V6. ATT&CK T1078, T1110.003.
- **T — Tampering** (integrity): altering request body (`amount`, account), config, code.
  → TLS 1.3; HMAC-sign bodies; parameterized queries. ASVS V8. ATT&CK T1565.001.
- **R — Repudiation** (non-repudiation): no proof an action was authorized.
  → Signed, append-only audit log (Audit-Service). ASVS V7/V10. ATT&CK T1070.
- **I — Information Disclosure** (confidentiality): verbose responses, unencrypted stores,
  leaked versions/banners. → Minimal responses; encryption at rest/in transit; strip
  banners. ASVS V9. ATT&CK T1041.
- **D — Denial of Service** (availability): flooding endpoints, expensive queries.
  → Rate limiting; CAPTCHA on suspicion; query depth/complexity limits. ASVS V17.
  ATT&CK T1499.
- **E — Elevation of Privilege** (authorization): BOLA/IDOR on `{id}`, weak role checks.
  → Ownership verification + RBAC/OPA; least privilege. ASVS V4/V5. ATT&CK T1068.

## Output template

Always produce exactly this structure:

```markdown
# STRIDE threat model — <component / flow>

## Scope & DFD
<one-paragraph scope + a simple ASCII or described DFD with trust boundaries marked>

## Threats
| STRIDE | Scenario | Risk (H/M/L) | Mitigation | ASVS | ATT&CK |
|--------|----------|--------------|-----------|------|--------|
| S | ... | ... | ... | V_ | T____ |
| T | ... | ... | ... | V_ | T____ |
| R | ... | ... | ... | V_ | T____ |
| I | ... | ... | ... | V_ | T____ |
| D | ... | ... | ... | V_ | T____ |
| E | ... | ... | ... | V_ | T____ |

## Security requirements (Definition of Done)
- [ ] <requirement derived from each High/Medium threat, phrased as a testable check>

## Notes
<residual risk, assumptions, when to revisit>
```

## Rules
- Every High/Medium threat **must** yield at least one requirement with a DoD checkbox.
- Prefer the project's default controls above; only diverge with a reason.
- Keep it live: note when the model must be revisited (any architectural change).
- If a Threat Dragon or Microsoft Threat Modeling Tool file is generated, save the `.json`
  next to the markdown.
