# A2 — Path traversal (arbitrary file read)

| | |
|---|---|
| **OWASP** | A05:2021 – Security Misconfiguration / A01 – Broken Access Control |
| **MITRE ATT&CK** | T1190 – Exploit Public-Facing Application; T1552.001 – Credentials in Files |
| **STRIDE** | Information Disclosure (see `threat-models/transfer-flow.stride.md`, threat #4) |
| **Flag** | `Vuln:A2` (env `Vuln__A2=true`) — vuln/* branch only |

## The vulnerability

`GET /api/invoice/download?file=…`
([`InvoiceController`](../../services/account/Controllers/InvoiceController.cs)) joins the
attacker-supplied name to a base directory with no confinement:

```csharp
var path = Path.GetFullPath(Path.Combine(baseDir, file));   // '../' escapes baseDir
return PhysicalFile(path, ...);
```

Because the service runs from the repo root, `../.env` and `../rootCA.key` are readable —
leaking the DB SA password and the CA private key.

## Run the exploit

```bash
python attacks/A2/exploit.py
```

- **Vulnerable:** `200` with the contents of `.env`, `rootCA.key`, `appsettings.json`.
- **Hardened:** `404` (path components stripped, access confined to the statements dir).

## Evidence

See [`evidence.txt`](evidence.txt) (secret values redacted).

## The fix (M4)

Never build file paths from raw input. Strip directory components (`Path.GetFileName`),
canonicalize, and assert the resolved path stays within the intended base directory
(the hardened branch does this). Store user files outside the app/webroot; don't run
services from a directory containing secrets; keep secrets in Vault, not on disk.
DoD check **T4** in the threat model.
