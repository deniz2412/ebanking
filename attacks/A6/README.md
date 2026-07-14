# A6 — Phishing / credential theft

| | |
|---|---|
| **OWASP** | A07:2021 – Identification and Authentication Failures |
| **MITRE ATT&CK** | T1566 – Phishing; T1078 – Valid Accounts (replay) |
| **STRIDE** | Spoofing (see `threat-models/transfer-flow.stride.md`, threat #6) |
| **Scope** | Social engineering artifact, vuln/* branch only |

## The attack

[`phishing-login.html`](phishing-login.html) is a typosquatted clone of the eBanking login
(imagine `ebank-login.com` vs the real `ebank.local`). It harvests whatever the victim types
and beacons it to the attacker's collector, then forwards to the real site so nothing looks
wrong. The attacker replays the captured username/password.

## Run the demo

```bash
python attacks/A3/collector.py     # attacker listener (:9099) — shared with A3
```

Open `attacks/A6/phishing-login.html`, type a username/password, submit. The collector prints:

```
[!] STOLEN TOKEN received: {"username":"victim","password":"hunter2"}
```

See [`evidence.txt`](evidence.txt).

## Why the hardened app resists it

Phishing can't be "patched" in code alone, but the design blunts it:

- **MFA (TOTP)** — a replayed password alone fails the second factor. The realm models this
  (`CONFIGURE_TOTP`); enable it as required for the transfer flow (DoD **T6**).
- **Keycloak brute-force protection** and anomaly detection on the real `/token` endpoint.
- **External-mail / unknown-origin banners** and user education; **DNS monitoring** for
  look-alike domains; registrar/brand takedown.
- Short-lived tokens + refresh rotation limit the value of any single capture.
