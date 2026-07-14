# A5 — Information disclosure via diagnostic banner

| | |
|---|---|
| **OWASP** | A05:2021 – Security Misconfiguration |
| **MITRE ATT&CK** | T1590 – Gather Victim Network Information / T1592 – Gather Host Information |
| **STRIDE** | Information Disclosure (see `threat-models/transfer-flow.stride.md`, threat #10) |
| **Flag** | `Vuln:A5` (env `Vuln__A5=true`) — vuln/* branch only |

## The vulnerability

The gateway maps an **unauthenticated** `GET /status`
([`Program.cs`](../../services/gateway-ocelot/Program.cs)) that returns OS description,
.NET runtime version, CLR version, machine name, OS user, PID and assembly version. This
lets an attacker fingerprint the exact stack and look up matching CVEs — all with no
credentials.

## Run the exploit

```bash
python attacks/A5/exploit.py
```

- **Vulnerable:** `200` with the full banner.
- **Hardened:** `404`.

## Evidence

See [`evidence.txt`](evidence.txt).

## The fix (M4)

Remove diagnostic/verbose endpoints from production; if a health probe is needed, keep it
minimal (`{status}` only) and network-restricted. Disable Swagger and stack traces in prod;
strip `Server`/version response headers. DoD check **T10** in the threat model.
