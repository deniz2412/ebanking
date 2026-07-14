# Attack demonstration plan (vulnerable → patched)

Source: thesis §3.6 (attack techniques, with concrete code) and §4.2–4.4.

The thesis mandates a **two-pass** experiment: build the app with intentional
vulnerabilities, attack it with **Burp Suite** and **OWASP ZAP**, then patch and re-test
with a before/after comparison.

> ⚠️ **Lab-only.** The vulnerable pass must run in an isolated environment with synthetic
> data. Never expose it publicly. Keep the vulnerable code on a separate branch/tag
> (e.g. `vuln/*`) so it can never be deployed by the normal pipeline.

## Attacks to build

Each has a planted vulnerability, an exploit, and a fix. Line references point to the
thesis code snippets. Track each as `attacks/<id>/` with a PoC and a ZAP/Burp script.

| # | Vulnerability | Where (thesis) | Exploit | Fix | ATT&CK / OWASP |
|---|---------------|----------------|---------|-----|----------------|
| A1 | **SQL Injection** | `InvoiceController` builds SQL by string concat (§3.6.2) | `GET /invoice/0 UNION ALL SELECT * FROM Users` | Parameterized queries / EF Core | T1190 / A03 |
| A2 | **Path traversal → RCE** | `UploadController` uses `Path.Combine` with unvalidated `path` (§3.6.3) | upload `../../wwwroot/backup/shell.aspx`, then GET it → shell as IIS | Validate/whitelist path; store outside webroot; no exec | T1190 / A05 |
| A3 | **DOM XSS → token theft** | unfiltered value into `innerHTML` (§3.6.3) | inject script that reads JWT from `localStorage` | Store token in HttpOnly/Secure cookie; sanitize DOM; CSP | T1189 / A03 |
| A4 | **BOLA / IDOR** | `PUT /accounts/{id}` no ownership check (§3.1.3, §3.6.4) | change `{id}` to another user's account | Ownership verification + RBAC via OPA | T1068 / A01 |
| A5 | **Info disclosure via banner** | `/status` endpoint returns OS + runtime version (§3.6.1) | banner grab profiles the target | Remove diagnostic banners/endpoints in prod | T1590 / A05 |
| A6 | **Phishing / credential theft** | typosquatted login page (§3.6.2) | harvest creds, replay | MFA; external-mail banners; DNS monitoring | T1566 / A07 |
| A7 | **Weak JWT / broken auth** | unsigned or weakly-validated tokens (§3.7.2) | forge/replay token | RS256/ES256 + `aud`/`iss`/`exp` checks; short TTL | T1078 / A07 |

Optional stretch demos from §3.6: SSRF (Capital One–style), C2 over HTTPS/DNS tunneling,
GraphQL over-fetch exfiltration (`maxDepth`/`maxComplexity` fix), log tampering
(Serilog `sharedLock`).

## Method (per attack)

1. **Vulnerable pass:** implement the flaw on the `vuln/*` branch; write the exploit
   (script under `attacks/<id>/exploit.*`); capture Burp/ZAP evidence.
2. **Detect:** confirm what the WAF (ModSecurity CRS), IDS (Suricata), and SIEM see — or
   don't. Note the gaps.
3. **Hardened pass:** apply the fix; note the code/infra/network changes.
4. **Re-test:** run the same exploit + a ZAP baseline/active scan; confirm it fails.
5. **Compare:** before/after table — finding, severity (CVSS), status, control that closed
   it, and the STRIDE/OWASP/ATT&CK IDs.

## Evaluation output (§4.4)

A before/after results section: number of findings by severity, ASVS coverage delta,
ATT&CK coverage index delta, and which framework caught each class of issue. This is the
empirical payoff of the thesis.
