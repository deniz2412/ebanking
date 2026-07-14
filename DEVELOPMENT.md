# E-banking Development Guide

This guide helps developers get started with the E-banking application development.

## Quick Start

### Prerequisites
- .NET 8 SDK
- Node.js 20+
- Docker Desktop with Kubernetes enabled
- kubectl CLI
- helm CLI

### Development Setup

1. **Clone and setup the environment:**
```bash
git clone <repository>
cd ebanking

# Setup development environment
make dev-setup
```

2. **Add to your hosts file:**
```
127.0.0.1 ebank.local
```

3. **Start the platform (Kubernetes):**
```bash
# Deploy infrastructure (one-time setup)
make all

# Check status
make k8s-status
```

4. **Start development servers:**
```bash
# Start frontend dev server
make dev-up
```

The frontend will be available at http://localhost:4200

## Current Implementation Status

### ✅ Completed (E0 - Basic Setup)
- [x] Monorepo structure
- [x] CI/CD pipeline skeleton
- [x] C4 architecture diagrams
- [x] Angular 20 frontend skeleton
- [x] Ocelot API Gateway with JWT validation
- [x] Kubernetes infrastructure manifests
- [x] Development tooling (Makefile, .editorconfig)

### 🚧 In Progress 
You are here! Ready to start implementing the microservices.

### 📋 Next Steps (E1-E3)

#### Immediate Priority (E1 - Platform & Network)
1. **S1.1**: Complete Ingress + TLS setup
2. **S1.2**: Deploy and configure Keycloak with ebanking realm
3. **S1.3**: Setup Vault for secrets management
4. **S1.4**: Deploy Kafka/Redpanda for event streaming
5. **S1.5**: Deploy MSSQL with initial schema
6. **S1.6**: Implement NetworkPolicies

#### Medium Priority (E2 - Gateway & Auth)
1. **S2.1**: Enhance Gateway with proper JWT validation
2. **S2.2**: Implement CORS and security policies
3. **S2.3**: Add logout/token revocation
4. **S2.4**: Implement audit logging

#### Next Phase (E3 - Microservices)
1. **Account Service**: Balance, transactions, PDF statements
2. **Transfer Service**: Internal/external transfers, standing orders
3. **Payment Service**: Bill payments, templates
4. **Notification Service**: Web Push notifications
5. **Audit Service**: Immutable audit trail with hash chains

## Development Workflow

### Creating a New Microservice

1. **Create service structure:**
```bash
mkdir -p services/myservice
cd services/myservice
dotnet new webapi
```

2. **Add to CI pipeline:**
Update `.github/workflows/ci.yml` to include the new service in the matrix.

3. **Add Kubernetes manifests:**
Create deployment, service, and configmap files in `infrastructure/k8s/`.

4. **Update Gateway routing:**
Add routes in `services/gateway-ocelot/ocelot.json`.

### Code Standards

- Follow the `.editorconfig` settings
- Use the security analyzers (configured in `Directory.Build.props`)
- Write tests for all business logic
- Use structured logging with Serilog
- Implement OpenTelemetry tracing

### Security Guidelines

- Always validate resource ownership (prevent BOLA/IDOR)
- Use parameterized queries (prevent SQL injection)
- Implement proper input validation
- Add security headers
- Use HTTPS everywhere
- Implement rate limiting
- Log security events

## Architecture Decisions

### Authentication & Authorization
- **OIDC/OAuth2** with Keycloak as IdP
- **PKCE flow** for Angular SPA
- **JWT bearer tokens** for API authentication
- **Resource-based authorization** for ownership validation

### Data Architecture
- **Database per service** pattern
- **Entity Framework Core** for data access
- **Outbox pattern** for reliable event publishing
- **Event sourcing** for audit service

### Communication Patterns
- **Synchronous**: REST APIs through API Gateway
- **Asynchronous**: Domain events via Kafka
- **Request/Response**: Direct HTTP for queries
- **Fire-and-forget**: Events for notifications

### Deployment Strategy
- **Containerized** microservices
- **Kubernetes** for orchestration
- **GitOps** deployment with Helm
- **Blue-green** deployments (later phases)

## Testing Strategy

### Unit Tests
- Business logic testing
- Repository pattern testing
- Authorization policy testing

### Integration Tests
- API endpoint testing
- Database integration testing
- Event publishing/consuming testing

### End-to-End Tests
- Complete user journeys
- Authentication flows
- Critical business processes

### Security Tests
- OWASP ZAP baseline scans
- Penetration testing scenarios
- Vulnerability assessments

## Monitoring & Observability

### Logging
- Structured logging with Serilog
- Centralized logging with Loki/ELK
- Security event logging

### Metrics
- Application metrics with Prometheus
- Business metrics (transactions/min, etc.)
- Infrastructure metrics

### Tracing
- Distributed tracing with OpenTelemetry
- Correlation IDs across services
- Performance monitoring

### Alerting
- Error rate thresholds
- Performance degradation
- Security incidents
- Business KPI alerts

## Contributing

1. Create a feature branch from `main`
2. Implement changes following coding standards
3. Write/update tests
4. Update documentation
5. Submit pull request
6. Ensure CI passes
7. Get code review approval

## Troubleshooting

### Common Issues

**Gateway returns 503:**
```bash
# Check gateway status
kubectl -n svc get pods -l app=api-gateway
kubectl -n svc logs -l app=api-gateway

# Check network policies
kubectl -n svc describe networkpolicy
```

**Frontend can't reach API:**
```bash
# Check ingress configuration
kubectl -n svc get ingress
kubectl -n svc describe ingress ebank-ingress

# Verify TLS certificate
kubectl -n svc get certificate
```

**Database connection issues:**
```bash
# Check database pod
kubectl -n data get pods -l app=mssql
kubectl -n data logs -l app=mssql

# Check secrets
kubectl -n data get secrets
```

### Useful Commands

```bash
# Development
make dev-setup          # Setup development environment
make dev-up             # Start development servers
make build              # Build all projects
make test               # Run all tests

# Kubernetes
make k8s-status         # Check cluster status
make k8s-logs-gateway   # View gateway logs

# Platform
make all                # Deploy entire platform
make clean              # Clean build artifacts
```

## Resources

- [C4 Architecture Diagrams](./docs/c4/README.md)
- [Threat Model](./docs/threat-model/README.md)
- [Kubernetes Manifests](./infrastructure/k8s/)
- [API Gateway Configuration](./services/gateway-ocelot/)
- [CI/CD Pipeline](./.github/workflows/)

## Contact

For questions or issues, please create an issue in the repository or contact the development team.
