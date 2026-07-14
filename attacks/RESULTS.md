# M3 — Vulnerable-pass results (before / after)

All attacks from `docs/context/08-attack-demo-plan.md` implemented on the **`vuln/m3-attacks`**
branch, each gated behind an explicit `Vuln:*` build flag (default **off = hardened**), so the
same binary demonstrates both states. `main` never contains vulnerable code.

Run pattern: start the service(s) with the flag on for "vulnerable", off for "hardened", then
run the exploit under `attacks/<id>/`. Captured output in each `attacks/<id>/evidence.txt`.

| ID | Class | Flag | Vulnerable result | Hardened result | Exploit | OWASP / ATT&CK |
|----|-------|------|-------------------|-----------------|---------|----------------|
| **A1** | SQL Injection | `Vuln:A1` | `' OR '1'='1` dumps **134** txns across all accounts | payload is a literal → **0** rows | [A1/exploit.py](A1/exploit.py) | A03 / T1190 |
| **A2** | Path traversal | `Vuln:A2` | reads `.env` (SA pw) + `rootCA.key` (CA private key) | traversal stripped → **404** | [A2/exploit.py](A2/exploit.py) | A05 / T1190, T1552 |
| **A3** | DOM XSS → token theft | (frontend) | `msg` param injects live `<img onerror>`; token JS-readable from `localStorage` | HttpOnly cookie + sanitizer + CSP | [A3/](A3/README.md) | A03 / T1189, T1539 |
| **A4** | BOLA / IDOR | `Vuln:A4` | attacker reads a victim's account balance (**200**) | ownership enforced → **403** | [A4/exploit.py](A4/exploit.py) | A01 / T1068 |
| **A5** | Info disclosure | `Vuln:A5` | unauth `/status` leaks OS/.NET/host/user | endpoint **404** | [A5/exploit.py](A5/exploit.py) | A05 / T1590 |
| **A6** | Phishing | (artifact) | typosquat page harvests creds → forwards to real site | MFA + banners + DNS monitoring | [A6/](A6/README.md) | A07 / T1566 |
| **A7** | Weak JWT | `Vuln:A7` | forged token (attacker key), any `sub` accepted (**200**) | RS256+`iss`/`aud`/`exp` → **401** | [A7/exploit.py](A7/exploit.py) | A07 / T1078 |

## How this ties the milestones together

- Each attack maps to a threat in `threat-models/transfer-flow.stride.md`
  (A1→#12, A2→#4, A3→#5, A4→#1/#8, A5→#10, A6→#6, A7→#5/#7).
- The **hardened** column is the M1 default behavior (or the documented M4 fix). Every
  `attacks/<id>/README.md` records the fix + the threat-model DoD check it satisfies.
- M5 evaluation consumes this table: findings-by-severity before/after, and which framework
  (STRIDE design / OWASP build / ATT&CK ops) caught each class.

## Safety

Vulnerable code lives **only** on `vuln/*`, behind `Vuln:*` flags that default to off. Two
environment notes surfaced by the exploits and fixed by the hardening baseline: services
should not run from a directory containing secrets (A2), and secrets belong in Vault, not
`.env`/`appsettings` (A2/A4 evidence redacted before commit).
