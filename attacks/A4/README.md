# A4 — Broken Object Level Authorization (BOLA / IDOR)

| | |
|---|---|
| **OWASP** | A01:2021 – Broken Access Control |
| **MITRE ATT&CK** | T1068 – Exploitation for Privilege Escalation |
| **STRIDE** | Elevation of Privilege (see `threat-models/transfer-flow.stride.md`, threat #1/#8) |
| **Flag** | `Vuln:A4` (env `Vuln__A4=true`) — vuln/* branch only |

## The vulnerability

`GET /api/accounts/by-number/{accountNumber}` in
[`AccountsController`](../../services/account/Controllers/AccountsController.cs) returns an
account by its number. In vulnerable mode it performs **no object-level ownership check**, so
any authenticated user can read any account's balance by iterating account numbers.

```csharp
var vulnerable = _config.GetValue<bool>("Vuln:A4");
if (!vulnerable && account.UserId != userId)
    return Forbid();          // hardened path — skipped when the flag is on
```

## Run the exploit

```bash
# 1. Start the stack (real JWT) with the account service flag on:
#    Vuln__A4=true  on the account service
# 2. Attack:
python attacks/A4/exploit.py
```

Attacker `testuser` (owns `1008888888`) reads victim accounts `1001234567`,
`1001234568`, `1009999999`.

- **Vulnerable:** each returns `200` with the victim's `balance` → data leak.
- **Hardened:** each returns `403 Forbidden`.

## Evidence

See [`evidence.txt`](evidence.txt) (captured run, vulnerable vs hardened).

## The fix (M4)

Enforce object-level authorization on every `{id}`/number-addressed resource: the resolved
account owner must equal the authenticated `sub`. Centralize via the project's
`AccountOwnershipHandler` / OPA policy. DoD check **T1**/**T8** in the threat model.
