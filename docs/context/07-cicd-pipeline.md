# CI/CD pipeline

Source: thesis §3.7.4 and Diagram 3-4. Orchestrated with **GitHub Actions**.

Every step from first commit to production carries a control that stops vulnerabilities or
unauthorized changes. Any failing gate **halts the pipeline** (thesis: "Obustavi tok").

## Pipeline stages (in order)

```
Commit
  │  (branch protection: code review required; commits GPG/gitsign-signed)
  ▼
Secret scan            Gitleaks — reject the merge if an API key/password appears in the diff
  ▼
SAST + SCA             CodeQL/SonarQube (injections, unsafe reflection, insecure calls)
  │                    + dependency scan vs CVE DB; FAIL if any CVSS > 7
  ▼  (SAST/SCA passed?) ── No ─▶ HALT
  ▼ Yes
IaC check              terrascan on Terraform; validate Kubernetes manifests, security groups
  ▼  (IaC passed?) ──── No ─▶ HALT
  ▼ Yes
Build + generate SBOM  multi-stage Docker build → minimal "distroless" image; emit CycloneDX/SPDX
  ▼
Image scan             Trivy — scan image layers & env for secrets/CVEs
  ▼  (Trivy passed?) ── No ─▶ HALT
  ▼ Yes
Sign image             sign the artifact (GPG/KMS); SBOM follows the artifact across envs
  ▼
Gatekeeper policy      OPA/Gatekeeper admission check (network policy, TLS cert, CSP hash)
  ▼  (Gatekeeper passed?) ─ No ─▶ HALT
  ▼ Yes
Deploy to production
  │
  ▼
Post-deploy monitoring rollback to previous stable image if anomalies appear
```

## Concrete gate config to implement

- **Branch protection:** required review + signed commits (GPG or gitsign).
- **Secret scan job:** Gitleaks action on PR diffs.
- **SAST job:** CodeQL workflow (GitHub-native) or SonarQube scanner.
- **SCA job:** OWASP Dependency-Check / Dependabot / Snyk; break build on CVSS > 7; also
  check license compatibility.
- **IaC job:** terrascan + `kubeconform`/`kube-linter`.
- **Build job:** multi-stage Dockerfile → distroless; `syft` (or build-native) SBOM.
- **Image scan job:** Trivy (fail on HIGH/CRITICAL).
- **Sign job:** cosign / KMS signing; attach SBOM as attestation.
- **Admission:** OPA Gatekeeper constraints in-cluster.
- **Post-deploy:** health/anomaly check with automated rollback.

## Coverage targets (from Chapter 3)

- **SCA coverage** ≈ 100%, no SBOM drift between declared and actual image contents.
- **ASVS coverage** ≥ 95% (this app is Level 3).
- **ATT&CK coverage** > 80% high-risk, > 60% medium-risk techniques.

These targets are the acceptance criteria for the "hardened pass" (doc 08 / doc 09).
