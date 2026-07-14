# C4 Architecture Diagrams

## Context Diagram
This shows the high-level system context and external actors.

```mermaid
C4Context
title E-banking System - Context Diagram

Enterprise_Boundary(b0, "Bank") {
  Person(customer, "Bank Customer", "Uses e-banking services via web browser")
  Person(admin, "Bank Admin", "Manages system configuration and monitors")
  
  System(ebank, "E-banking System", "Provides online banking services")
  
  System_Ext(keycloak, "Keycloak", "Identity Provider (OIDC/OAuth2)")
  System_Ext(external_bank, "External Banks", "SEPA transfers")
  System_Ext(push_service, "Push Service", "Web Push notifications")
  System_Ext(email_service, "Email Service", "Email notifications")
}

Rel(customer, ebank, "Uses", "HTTPS")
Rel(admin, ebank, "Administers", "HTTPS")
Rel(ebank, keycloak, "Authenticates", "OIDC/OAuth2")
Rel(ebank, external_bank, "Transfers", "SEPA/ISO20022")
Rel(ebank, push_service, "Sends notifications", "Web Push API")
Rel(ebank, email_service, "Sends emails", "SMTP")

UpdateLayoutConfig($c4ShapeInRow="2", $c4BoundaryInRow="1")
```

## Container Diagram
This shows the high-level technical containers and their interactions.

```mermaid
C4Container
title E-banking System - Container Diagram

Person(customer, "Bank Customer", "Uses e-banking web application")

Container_Boundary(ebank, "E-banking System") {
  Container(spa, "Web Application", "Angular 20, TypeScript", "Single-page application providing banking UI")
  Container(gateway, "API Gateway", "ASP.NET Core, Ocelot", "Routes requests, handles JWT validation, CORS, rate limiting")
  
  Container_Boundary(microservices, "Microservices") {
    Container(account, "Account Service", "ASP.NET Core", "Manages account balances, statements, PDF generation")
    Container(transfer, "Transfer Service", "ASP.NET Core", "Handles internal/external transfers, standing orders")
    Container(payment, "Payment Service", "ASP.NET Core", "Processes payments, manages templates")
    Container(notification, "Notification Service", "ASP.NET Core", "Sends push notifications and emails")
    Container(audit, "Audit Service", "ASP.NET Core", "Immutable audit trail with hash chains")
  }
  
  ContainerDb(database, "Database", "SQL Server 2022", "Stores account data, transactions, audit logs")
  Container(eventbus, "Event Bus", "Kafka/Redpanda", "Handles domain events and integration")
  Container(vault, "Vault", "HashiCorp Vault", "Secrets management")
}

System_Ext(keycloak, "Keycloak", "Identity Provider")
System_Ext(external, "External Systems", "Banks, notification services")

Rel(customer, spa, "Uses", "HTTPS")
Rel(spa, gateway, "API calls", "HTTPS/JSON")
Rel(spa, keycloak, "Authentication", "OIDC PKCE")

Rel(gateway, account, "Routes to", "HTTP/JSON")
Rel(gateway, transfer, "Routes to", "HTTP/JSON") 
Rel(gateway, payment, "Routes to", "HTTP/JSON")
Rel(gateway, notification, "Routes to", "HTTP/JSON")
Rel(gateway, keycloak, "Validates JWT", "JWKS")

Rel(account, database, "Reads/Writes", "TDS")
Rel(transfer, database, "Reads/Writes", "TDS")
Rel(payment, database, "Reads/Writes", "TDS")
Rel(audit, database, "Writes only", "TDS")

Rel(transfer, eventbus, "Publishes events", "Kafka")
Rel(payment, eventbus, "Publishes events", "Kafka")
Rel(notification, eventbus, "Consumes events", "Kafka")
Rel(audit, eventbus, "Consumes events", "Kafka")

Rel(account, vault, "Gets secrets", "HTTP/API")
Rel(transfer, vault, "Gets secrets", "HTTP/API")
Rel(payment, vault, "Gets secrets", "HTTP/API")

Rel(notification, external, "Sends notifications", "HTTPS/SMTP")
Rel(transfer, external, "SEPA transfers", "ISO20022")

UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

## Component Diagram - Account Service
This shows the internal structure of the Account Service.

```mermaid
C4Component
title Account Service - Component Diagram

Container(gateway, "API Gateway", "Ocelot", "Routes and validates requests")

Container_Boundary(account_service, "Account Service") {
  Component(account_controller, "Account Controller", "ASP.NET Core", "REST API endpoints")
  Component(account_business, "Account Business Logic", "C#", "Domain logic and validation")
  Component(pdf_generator, "PDF Generator", "QuestPDF", "Generates account statements")
  Component(account_repository, "Account Repository", "EF Core", "Data access layer")
  Component(auth_handler, "Authorization Handler", "ASP.NET Core", "Resource ownership validation")
}

ContainerDb(database, "Database", "SQL Server", "Account data storage")
Container(vault, "Vault", "HashiCorp Vault", "Database credentials")

Rel(gateway, account_controller, "HTTP requests", "JSON/HTTPS")
Rel(account_controller, auth_handler, "Validates ownership")
Rel(account_controller, account_business, "Delegates to")
Rel(account_business, account_repository, "Uses")
Rel(account_business, pdf_generator, "Generates statements")
Rel(account_repository, database, "Queries", "EF Core/TDS")
Rel(account_repository, vault, "Gets connection string", "HTTPS")

UpdateLayoutConfig($c4ShapeInRow="2", $c4BoundaryInRow="1")
```

## Deployment Diagram
This shows how the system is deployed in Kubernetes.

```mermaid
C4Deployment
title E-banking System - Deployment Diagram

Deployment_Node(k8s, "Kubernetes Cluster", "Docker Desktop") {
  
  Deployment_Node(dmz, "DMZ Namespace", "External-facing components") {
    Deployment_Node(ingress_pod, "Ingress Controller", "NGINX") {
      Container(ingress, "NGINX Ingress", "nginx", "TLS termination, routing")
    }
  }
  
  Deployment_Node(svc, "SVC Namespace", "Application services") {
    Deployment_Node(gateway_pod, "Gateway Pod", "Container") {
      Container(gateway_container, "API Gateway", "Ocelot", "Authentication, routing")
    }
    
    Deployment_Node(account_pod, "Account Pod", "Container") {
      Container(account_container, "Account Service", "ASP.NET Core", "Account management")
    }
    
    Deployment_Node(transfer_pod, "Transfer Pod", "Container") {
      Container(transfer_container, "Transfer Service", "ASP.NET Core", "Money transfers")
    }
    
    Deployment_Node(frontend_pod, "Frontend Pod", "Container") {
      Container(spa_container, "Angular SPA", "nginx", "Web application")
    }
  }
  
  Deployment_Node(data, "DATA Namespace", "Data persistence") {
    Deployment_Node(db_pod, "Database Pod", "StatefulSet") {
      ContainerDb(db_container, "SQL Server", "mssql:2022", "Primary database")
    }
    
    Deployment_Node(kafka_pod, "Kafka Pod", "StatefulSet") {
      Container(kafka_container, "Redpanda", "Kafka", "Event streaming")
    }
  }
  
  Deployment_Node(security, "Security Namespace", "Security services") {
    Deployment_Node(keycloak_pod, "Keycloak Pod", "Deployment") {
      Container(keycloak_container, "Keycloak", "JBoss", "Identity provider")
    }
    
    Deployment_Node(vault_pod, "Vault Pod", "StatefulSet") {
      Container(vault_container, "HashiCorp Vault", "Vault", "Secrets management")
    }
  }
}

Rel(ingress, gateway_container, "Routes", "HTTP")
Rel(gateway_container, account_container, "API calls", "HTTP")
Rel(gateway_container, transfer_container, "API calls", "HTTP")
Rel(account_container, db_container, "Queries", "TDS:1433")
Rel(transfer_container, kafka_container, "Events", "Kafka:9092")
Rel(gateway_container, keycloak_container, "JWT validation", "HTTP")

UpdateLayoutConfig($c4ShapeInRow="2", $c4BoundaryInRow="2")
```

## Security Boundaries

### Trust Zones
1. **DMZ (Demilitarized Zone)**: Ingress controller, exposed to internet
2. **SVC (Service Zone)**: Application services, internal communication
3. **DATA**: Databases and persistent storage
4. **Security**: Identity and secrets management

### Network Policies
- Default deny all traffic
- Explicit allow rules for required communication paths
- Ingress can only reach Gateway
- Services can only reach databases they need
- All services can reach Vault and Keycloak

### Data Flow Security
1. **External → DMZ**: HTTPS with TLS certificates
2. **DMZ → SVC**: HTTP over private network with JWT validation
3. **SVC → DATA**: Encrypted connections with service accounts
4. **SVC → Security**: mTLS for vault, HTTPS for Keycloak

## Technology Choices

### Frontend
- **Angular 20**: Modern SPA framework with excellent TypeScript support
- **PKCE Flow**: Secure authentication for SPAs
- **Service Worker**: For push notifications and offline capability

### Backend
- **ASP.NET Core 8**: High-performance, secure web framework
- **Minimal APIs**: Lightweight API development
- **Entity Framework Core**: Type-safe data access
- **Ocelot**: Feature-rich API gateway

### Infrastructure
- **Kubernetes**: Container orchestration
- **SQL Server 2022**: Enterprise-grade relational database
- **Kafka/Redpanda**: Event streaming and messaging
- **Keycloak**: Open-source identity management
- **HashiCorp Vault**: Secrets management

### Security
- **JWT**: Stateless authentication
- **HTTPS Everywhere**: TLS for all communications
- **Network Policies**: Microsegmentation
- **RBAC**: Role-based access control
