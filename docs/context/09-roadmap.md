# Roadmap

A suggested build order. Milestones are sized so each one produces something demonstrable
for the thesis. Nothing here is fixed — reorder as needed, but keep the security work
*inside* each milestone rather than bolted on at the end (that's the "shift-left" point of
the whole thesis).

## M0 — Foundations
- [ ] Repo + branch protection (signed commits, required review).
- [ ] Kubernetes dev cluster (kind/minikube ok) with the three namespaces = the three zones.
- [ ] Skeleton GitHub Actions pipeline (doc 07) — gates can be no-ops first, then filled in.
- [ ] Keycloak up; realm, one client for the SPA, one test user with MFA.

## M1 — Golden path, happy case
- [ ] Angular SPA shell: login (via Keycloak/OIDC), balance view, transfer form, logout.
- [ ] Ocelot gateway: routing + JWT validation + rate limit.
- [ ] Account-Service + Transfer-Service on ASP.NET 8 + EF Core against SQL Server 2022.
- [ ] Kafka broker; Transfer publishes `TransferCompleteEvent`.
- [ ] Notification-Service consumes → Web Push. Audit-Service consumes → immutable log.
- [ ] mTLS between services; secrets from Vault.
- **Demo:** UC-01 → UC-02 → UC-04 → UC-12 → UC-11 end to end.

## M2 — STRIDE threat model
- [ ] Build the DFD + STRIDE matrix for the transfer flow (doc 06 + the skill).
- [ ] Turn high-risk threats into tracked security requirements / DoD checks.
- [ ] Store models in `/threat-models/`.

## M3 — Vulnerable pass
- [ ] Implement attacks A1–A7 (doc 08) on `vuln/*` branch(es).
- [ ] Write exploits + Burp/ZAP scripts under `/attacks/`.
- [ ] Capture what WAF/IDS/SIEM detect (and miss).

## M4 — Hardened pass
- [ ] Patch each vulnerability; record code/infra/network changes.
- [ ] Fill in every CI/CD gate for real (SAST, SCA, secret scan, image scan, IaC, signing,
      Gatekeeper).
- [ ] Add hardening baseline (doc 04 §hardening) to services, containers, ingress.
- [ ] Re-run all exploits + ZAP scans; confirm closure.

## M5 — Evaluation & write-up
- [ ] Before/after comparison table (doc 08).
- [ ] ASVS Level 3 coverage report; ATT&CK coverage index.
- [ ] Map results back to STRIDE/OWASP/ATT&CK for the thesis conclusion (Chapter 5).

## Definition of done (whole project)
- ASVS Level 3 coverage ≥ 95%; SCA coverage ≈ 100% with no SBOM drift.
- ATT&CK coverage > 80% high-risk / > 60% medium.
- Every A1–A7 exploit that worked in M3 fails in M4, with evidence.
