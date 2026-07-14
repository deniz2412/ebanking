# Context index

This folder is the single source of truth for **what we are building and why**, extracted
from the thesis. Read in order, or jump to what you need.

| # | Doc | What it covers |
|---|-----|----------------|
| 01 | [Architecture overview](01-architecture-overview.md) | The three network zones, C4 view, main request flow, trust boundaries |
| 02 | [Microservices](02-microservices.md) | Each service, its responsibility, data, and interfaces |
| 03 | [Use cases](03-use-cases.md) | UC-01…UC-12 and how they map to services |
| 04 | [Tech stack](04-tech-stack.md) | Concrete technologies, versions, and why each was chosen |
| 05 | [Security frameworks](05-security-frameworks.md) | STRIDE + OWASP + MITRE ATT&CK, and the standards (ISO/PCI/GDPR/PSD2/DORA) |
| 06 | [STRIDE threat model](06-threat-model-stride.md) | The worked threat model for the main money-transfer flow |
| 07 | [CI/CD pipeline](07-cicd-pipeline.md) | The secure GitHub Actions pipeline, gate by gate |
| 08 | [Attack demo plan](08-attack-demo-plan.md) | The vulnerable→patched attacks to build and demonstrate |
| 09 | [Roadmap](09-roadmap.md) | Suggested build order / milestones |

## ⚠️ Known discrepancies in the source thesis

The thesis diagrams and the author's inline notes disagree in a few places. These docs
follow the author's **latest written intent** and flag each divergence so nobody
re-introduces the older version.

1. **Identity service → Keycloak.** Diagram 4-2 shows a custom `Identity-Service`
   (ASP.NET 8). The author's note underneath states *"Nema Identity Service vec koristimo
   keycloak"* ("There is no Identity Service — we use Keycloak"). **Decision: Keycloak is
   the identity provider.** The custom Identity-Service is dropped. See docs 02 and 04.
2. **API Gateway.** Prose in §4.1.1 uses a generic "API gateway"; Diagram 4-2 names it
   **Ocelot (.NET)**. **Decision: Ocelot.**
3. **Section numbering.** Some thesis figure/table numbers are inconsistent (e.g. two
   "Tabela 3-3"). Not relevant to the build; ignore.

If you change any of these decisions, update this list and the affected docs in the same
commit.
