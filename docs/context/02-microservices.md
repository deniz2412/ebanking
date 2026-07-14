# Microservices

Source: thesis §4.1.1–4.1.2, Table 4-1, Diagram 4-2.

Every microservice is **ASP.NET 8 + EF Core**, runs in Kubernetes, talks REST over mTLS,
and reads its secrets from Vault. Each owns its slice of the SQL Server database.

## Identity — **Keycloak** (not a custom service)

> Per the author's note, the custom `Identity-Service` from the diagram is **replaced by
> Keycloak**. Keycloak is the OAuth 2.0 / OIDC provider that authenticates users (with
> MFA), issues the JWT access + refresh tokens, and handles logout/token invalidation.
> The Gateway and every downstream service trust tokens signed by Keycloak.

Responsibilities: login, MFA, JWT issuance (RS256/ES256), refresh, password change, logout.
Backs: UC-01, UC-10, UC-11.

## Account-Service
- **Owns:** accounts, balances, transaction history, PDF statements.
- **Responsibilities:** list accounts and current balances for the logged-in user; serve
  transaction history; generate PDF statements. Ownership checks on every `{id}` access
  (this is the BOLA-sensitive surface — see doc 08).
- Backs: UC-02, UC-03, UC-09.

## Transfer-Service
- **Owns:** internal transfers, standing orders.
- **Responsibilities:** validate limits and available balance (calls Account-Service),
  book the transaction, then **publish `TransferCompleteEvent`** to Kafka. Handles standing
  orders. For external transfers it cooperates with Payment-Service.
- Backs: UC-04, UC-05 (with Payment), UC-07.

## Payment-Service
- **Owns:** bill payments, saved payees/templates, external payment rails.
- **Responsibilities:** process bill payments (manual entry, transaction stays `pending`
  until user confirms); manage payment templates; drive external transfers.
- Backs: UC-05 (with Transfer), UC-06, UC-08.

## Notification-Service
- **Owns:** user notification delivery.
- **Responsibilities:** consume Kafka events and push near-real-time notifications to the
  client's registered Service Worker via the **Web Push API** (TLS 443).
- Backs: UC-12.

## Audit-Service
- **Owns:** the immutable audit log.
- **Responsibilities:** consume Kafka events and persist **append-only, tamper-evident**
  records. This is the control that answers *Repudiation* threats (STRIDE R) — records must
  be signed and shipped to storage faster than an attacker could alter them (see doc 06/08).
- Backs: UC-03 (history/audit trail).

## Cross-cutting rules (apply to every service)
- **AuthN/Z:** validate the JWT on every request — signature, then `aud`/`iss`/`exp`, then
  `scope` vs. the requested action. Authorization follows RBAC → ABAC → PBAC; policy is
  evaluated as code via **OPA (Open Policy Agent)**.
- **Least privilege:** each service gets the **minimum OAuth scopes**. Target metric from
  the thesis: **< 5% of service accounts with an admin role**.
- **Data:** all queries parameterized / via EF Core (never string-concatenated SQL). Data
  encrypted at rest.
- **Secrets:** only from Vault (CSI driver), never in code, config, or images.
- **Transport:** mTLS between services; TLS 1.3 externally.
