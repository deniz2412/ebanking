# E-banking Demo (Phase 0 + Phase 1) — host: ebank.local

Ovaj repozitorij sadrži **repliciran** setup za Fazu 0 i Fazu 1 (Kubernetes @ Docker Desktop).
Cilj: brzo podići DMZ/SVC/DATA/security slojeve, Keycloak, MSSQL, Redpanda i placeholder API Gateway.

## Brzi start (TL;DR)

```bash
# 0) Kloniraj repo i u /etc/hosts dodaj:
# 127.0.0.1 ebank.local

# 1) Faza 1 — platforma
bash scripts/apply-phase1.sh

# 2) Provjera
bash scripts/diagnose-503.sh   # (opcionalno)
curl -k https://ebank.local/   # očekuj JSON iz echo gateway-a
```

Ako nemaš `helm`, instaliraj ga prije (https://helm.sh/docs/intro/install/).

---

## Struktura repozitorija

```
frontend/                     # Angular placeholder (kasnije zamijeni)
services/                     # .NET 8 mikroservisi (placeholders)
  account/ transfer/ payment/ notification/ audit/

infrastructure/
  k8s/
    base/namespaces.yaml
    network/
      default-deny.yaml
      allow-ingress-to-gateway.yaml
      # (po potrebi) allow-probes-gateway.yaml
    ingress/ebank-ingress.yaml
    cert-manager/cluster-issuer.yaml
    keycloak/keycloak.yaml
    mssql/{secret.yaml,mssql.yaml}
    kafka/redpanda.yaml
    gateway/api-gateway.yaml

ci/.github/workflows/ci.yml   # CI skeleton (build/test/sbom placeholder)
docs/
  c4/README.md                # C4 (Mermaid) placeholder
  threat-model/README.md      # STRIDE placeholder

scripts/
  apply-phase1.sh             # Sve za Fazu 1 (install + apply + rollout)
  diagnose-503.sh             # Brza dijagnostika (Ingress/Endpoints/Logs)
Makefile
```

---

## Faza 0 — Osnovna postavka

- Struktura repozitorija kao gore.
- CI skeleton u `ci/.github/workflows/ci.yml` (dotjerati u Fazi 3).
- C4 i STRIDE placeholderi u `docs/`.

---

## Faza 1 — Platforma i mrežne zone (Docker Desktop Kubernetes)

**Šta radi `scripts/apply-phase1.sh`:**
1. Kreira/validira namespaces: `dmz`, `svc`, `data`, `security`
2. Instalira **cert-manager** i primjenjuje **ClusterIssuer `dev-ca`**
3. Instalira **ingress-nginx** controller u `ingress-nginx` ns (`ingressClassName: nginx`)
4. Primjenjuje **API Gateway** (echo server) i **NetworkPolicy** koja dopušta promet iz controllera
5. Primjenjuje **Ingress** u `svc` ns za host **ebank.local** (TLS secret `ebank-tls`)
6. Čeka rollout controllera i gateway-a

**Dodatno primijeni (ručno)**:
- `infrastructure/k8s/mssql/*.yaml` — MSSQL 2022 (StatefulSet + PVC)
- `infrastructure/k8s/kafka/redpanda.yaml` — Redpanda (Kafka za dev)
- `infrastructure/k8s/keycloak/keycloak.yaml` — Keycloak (start-dev; admin/admin)
> Ove tri komponente možeš i uključiti u `apply-phase1.sh` (ostavljeno je ručno kako bi bilo transparentnije).

**Provjera:**
```bash
# controller
kubectl -n ingress-nginx get pods
# slojevi i endpointi
kubectl -n svc get deploy,svc,endpoints,ingress
kubectl -n data get statefulset,svc
kubectl -n security get deploy,svc
# gateway i cert
kubectl -n svc rollout status deploy/api-gateway
kubectl -n svc get certificate,secret | grep -i ebank-tls
curl -k https://ebank.local/
```

**Najčešći problemi:**
- 503 → Ingress ↔ Service nisu u istom ns, ili NetworkPolicy blokira promet iz `ingress-nginx` ns.
- Port mismatch → Service 80 → targetPort 80, container port 80.
- TLS secret nije nastao → provjeri cert-manager i `cluster-issuer` annotation.

---

## Sljedeće faze (teaser)
- Faza 2: YARP/Ocelot gateway (JWT validacija, CORS, rate-limit), Keycloak realm `ebanking`.
- Faza 3: .NET 8 servisi + EF Core + inicijalne migracije.
- Faza 6/7: ranjiva varijanta → eksploatacija → hardened varijanta (ASVS/STRIDE pokrivenost).
