# Attacks (vulnerable → patched)

One folder per planned attack, `attacks/<id>/`, each with the exploit script
(`exploit.*`), Burp/ZAP evidence, and the documented fix. See the full plan and the
A1–A7 table in `docs/context/08-attack-demo-plan.md`.

> ⚠️ Lab-only. Intentional vulnerabilities live **only** on `vuln/*` branches or behind
> an explicit build flag — never on `main` or any deployable branch. Use synthetic data
> in an isolated environment.
