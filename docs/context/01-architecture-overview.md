# Architecture overview

Source: thesis §4.1.2 (Arhitektura rješenja), Diagram 4-1 (main flow), Diagram 4-2 (C4).

The system is a microservice e-banking application split into **three network zones**.
Isolation between zones is the backbone of the design: a failure in one zone must not
directly reach the next, and each zone is a natural trust boundary for threat modeling.

## The three zones

### 1. DMZ (Ingress zone)
- **NGINX Ingress + WAF** — the only public entry point.
- Terminates TLS, filters HTTP/S traffic against WAF rules (OWASP ModSecurity Core Rule
  Set), and applies edge rate limiting before anything reaches the gateway.

### 2. Service layer (Servisni sloj)
- **API Gateway (Ocelot, .NET)** — routing, JWT validation, and rate limiting. Everything
  from the SPA enters the service layer here.
- **Microservices (ASP.NET 8 + EF Core):** Account, Payment, Transfer, Notification, and a
  background **Audit** service for immutable log storage.
- **Apache Kafka broker** — the event bus. Transfer publishes transaction-status events;
  Notification and Audit consume them.
- Protected by **Kubernetes NetworkPolicy** and **mTLS** between every service.

### 3. Data layer (Podatkovni sloj)
- **MS SQL Server 2022** — the primary database (EF Core, port 1433).
- **HashiCorp Vault** — secrets and certificate management. Secrets are injected at pod
  start via the Kubernetes CSI driver and can rotate without rebuilding the app.

## Component / C4 view

```
                        ┌─────────────────── DMZ (Ingress zone) ───────────────────┐
   User (browser) ─────▶│  NGINX Ingress + WAF   (TLS termination, rate limit)      │
        ▲               └───────────────────────────┬───────────────────────────────┘
        │ Web Push (TLS 443)                         │ HTTP/HTTPS REST
        │                                            ▼
        │               ┌───────────────── Service layer (mTLS) ────────────────────┐
        │               │  API Gateway (Ocelot .NET) — routing, JWT check, rate limit │
        │               │        │            │            │              │           │
        │               │        ▼            ▼            ▼              ▼           │
        │               │  Account-Svc   Payment-Svc   Transfer-Svc   Notification-Svc │
        │               │        │            │            │  publishes    ▲ consumes  │
        │               │        │            │            ▼               │           │
        │               │        │            │      Apache Kafka  ────────┤           │
        │               │        │            │            │  consumes     ▼           │
        │               │        │            │            └──────────▶ Audit-Svc      │
        │◀──────────────┤                                                             │
        │ notifications  └──────────┬──────────────────────────┬─────────────────────┘
        │                           │ EF Core 1433              │ secrets
        │               ┌───────────▼───────── Data layer ──────▼─────────────────────┐
        │               │  MS SQL Server 2022 (RDBMS)      HashiCorp Vault (secrets)   │
                        └────────────────────────────────────────────────────────────┘

   Identity: Keycloak (OAuth2/OIDC) issues the JWTs the Gateway validates.
   (Replaces the custom Identity-Service shown in the thesis diagram — see 00-index.md.)
```

## Inter-service communication
- **North-south:** browser → Ingress → Gateway → service, all HTTPS/REST.
- **East-west:** service ↔ service over **REST with mTLS**.
- **Async:** Transfer → Kafka → {Notification, Audit}.
- **Data:** services → SQL Server over EF Core (1433).
- **Push:** Notification → browser Service Worker via **Web Push (TLS 443)**.

## Main flow — money transfer (Diagram 4-1)

1. User submits credentials → SPA → Gateway → **Keycloak**: `POST /login`.
2. Keycloak authenticates (incl. MFA) and issues a **short-lived JWT access token +
   refresh token**; Gateway returns `200 OK + JWT`.
3. User clicks *Transfer* → `POST /transfers` (JWT) → Gateway → **Transfer-Service**.
4. Transfer-Service checks limits/balance (via Account-Service), saves the transaction,
   then **publishes `TransferCompleteEvent`** to Kafka → `201 Created` to the client.
5. **Notification-Service** consumes the event and sends a **Web Push** notification to the
   client's Service Worker. **Audit-Service** consumes it and writes an immutable record.
6. Logout → `POST /logout` → token invalidated → `200 OK` (prevents replay).

## Trust boundaries

The full login → transfer → logout flow crosses **three trust boundaries** (SPA ↔ Gateway,
Gateway ↔ services, services ↔ data). These boundaries are exactly where the STRIDE model
(doc 06) enumerates threats.
