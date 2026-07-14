# A7 — Weak / broken JWT validation

| | |
|---|---|
| **OWASP** | A07:2021 – Identification and Authentication Failures |
| **MITRE ATT&CK** | T1078 – Valid Accounts (forged) |
| **STRIDE** | Spoofing (see `threat-models/transfer-flow.stride.md`, threats #5/#7) |
| **Flag** | `Vuln:A7` (env `Vuln__A7=true`) — vuln/* branch only |

## The vulnerability

In vulnerable mode, `AddSharedAuthentication`
([`AuthenticationExtensions.cs`](../../services/shared/Extensions/AuthenticationExtensions.cs))
accepts JWTs **without verifying the signature** and ignores `iss`/`aud`/`exp`:

```csharp
SignatureValidator = (token, _) => new JsonWebToken(token),   // no signature check
ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = false,
```

So an attacker forges a token signed with *their own* key, sets `sub` to any victim, and is
authenticated — no Keycloak login, full impersonation.

## Run the exploit

Run the account service (and gateway) with `Vuln__A7=true`, then:

```bash
python attacks/A7/exploit.py
```

- **Vulnerable:** forged token (attacker key, `sub=dev-user-id`) → `200` + victim's balance.
- **Hardened:** `401` (RS256 signature via Keycloak JWKS + `iss`/`aud`/`exp` all enforced).

## Evidence

See [`evidence.txt`](evidence.txt).

## The fix (M4)

Validate RS256/ES256 signatures against Keycloak's JWKS; enforce `iss`, `aud`, `exp` with
minimal clock skew; reject `alg:none` and symmetric algorithms. This is the M1 hardened
default. DoD checks **T5**/**T6** in the threat model; ties to defense-in-depth (every
service re-validates, not just the gateway).
