# Security frameworks

Source: thesis Chapter 3 (Standardi i metode).

The thesis's core argument is that three complementary frameworks, applied at the right
SDLC phase, cover the whole application lifecycle:

| Framework | Primary focus | SDLC phase | Value |
|-----------|---------------|------------|-------|
| **STRIDE** | Threat categories on a data-flow diagram | Design | Catch logic/design flaws before code |
| **OWASP** (Top 10, ASVS, WSTG, SAMM) | Concrete requirements + test procedures | Implementation & test | Testable controls in CI/CD |
| **MITRE ATT&CK** | Real adversary tactics & techniques | Operations | Shared language for detection & simulation |

## STRIDE (§3.1)

Six threat categories, one per element of the CIA triad plus authenticity/non-repudiation:

| Letter | Threat | Typical control |
|--------|--------|-----------------|
| **S** | Spoofing | MFA; verify JWT `aud`/`exp` |
| **T** | Tampering | TLS 1.3; sign request bodies (HMAC) |
| **R** | Repudiation | Signed, immutable audit logs |
| **I** | Information Disclosure | Minimal responses; encryption at rest |
| **D** | Denial of Service | Rate limiting at gateway; CAPTCHA on suspicion |
| **E** | Elevation of Privilege | Ownership checks; RBAC |

STRIDE ↔ **ASVS v5.0.0** section mapping (§3.1.6): S→V2, T→V8, R→V7/V10, I→V9, D→V17,
E→V4/V5. Worked model for this app is in doc 06.

## OWASP (§3.3)

- **Top 10 (2021; 2025 in progress):** ranked risk list. A01 Broken Access Control is the
  most prevalent (present in ~94% of tested apps). See §3.3.2 Table 3-2 for A01–A10 with
  examples.
- **ASVS v5.0.0:** 345 requirements over 17 sections, 3 levels. Level 2 (sensitive data)
  targets **90% coverage**; Level 3 (critical/finance/health) targets **95%**. This
  banking app is **ASVS Level 3**.
- **WSTG:** 90+ test procedures across 12 categories — the operational "how to test".
- **SAMM v2.1:** maturity model over 5 business functions (governance, design,
  implementation, verification, operations).
- **Tooling in DevSecOps:** Amass, Dependency-Check, ZAP, DefectDojo, Juice Shop.

## MITRE ATT&CK (§3.2)

14 tactics, 300+ techniques. For a web app the **Enterprise** and **Cloud** matrices matter
most. Techniques this project will map to:

- **T1190** Exploit Public-Facing Application (the SQLi / upload RCE demos)
- **T1189** Drive-by Compromise
- **T1078** Valid Accounts / **T1110.003** Password Spraying
- **T1068 / T1098** Privilege escalation (BOLA chain)
- **T1565.001** Stored Data Manipulation (Tampering)
- **T1566** Phishing
- **T1041 / T1030 / T1048** Exfiltration
- **T1070** Indicator Removal / log tampering
- Coverage index formula (§3.2.6): `(detected + prevented techniques) / relevant techniques × 100%`.
  Targets: >80% for high-risk techniques, >60% for medium.

## The unified mapping (§3.5.2, Table 3-5)

| Control area | STRIDE | OWASP | ATT&CK |
|--------------|--------|-------|--------|
| Auth & access control | Spoofing, Elevation of Privilege | Top 10 A07; ASVS V6, V8 | T1078 |
| Input validation & injection | Tampering | Top 10 A03; ASVS V1, V2 | T1190 |
| Crypto & integrity | Tampering, Info Disclosure | Top 10 A02; ASVS V11, V14 | T1573 |
| Logging & incident response | Repudiation, DoS | Top 10 A09; ASVS V16 | T1070 |

## Standards & regulation (§3.4) — this is a bank, so all apply

- **ISO/IEC 27001:2022** — ISMS; Annex A (93 controls).
- **PCI DSS v4.0** — card data protection.
- **GDPR** — personal data; Art. 32 technical measures; 72h breach notification.
- **PSD2 + RTS** — strong customer authentication (≥2 of 3 factors), dynamic linking.
- **SWIFT CSCF** — controls for SWIFT network participants.
- **DORA** — ICT resilience; TIBER-EU pen-testing; 4h reporting for high-criticality
  incidents.

These are *compliance overlays* on top of the technical controls above — cheap to satisfy
once STRIDE/OWASP/ATT&CK controls exist.
