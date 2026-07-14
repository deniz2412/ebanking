# EBanking Docker Development Environment

This Docker Compose setup provides a complete local development environment for the EBanking microservices application.

## Services Included

### Infrastructure Services
- **MSSQL Server 2022** - Main database server (Port: 1433)
- **RedPanda** - Kafka-compatible message broker (Port: 9092)
- **RedPanda Console** - Kafka management UI (Port: 8080)
- **Keycloak** - Identity and access management (Port: 8180)

### Available Ports
- `1433` - MSSQL Server
- `8080` - RedPanda Console (Kafka UI)
- `8180` - Keycloak Admin Console
- `9092` - Kafka Bootstrap Server
- `5000-5010` - Reserved for microservices

## Quick Start

1. **Copy environment file:**
   ```bash
   cp .env.example .env
   ```

2. **Start infrastructure services:**
   ```bash
   docker-compose up -d
   ```

3. **Verify services are running:**
   ```bash
   docker-compose ps
   ```

4. **Access management interfaces:**
   - Kafka Console: http://localhost:8080
   - Keycloak Admin: http://localhost:8180 (admin/admin123)

## Adding Your Services

To add your microservices to the compose file:

1. **Uncomment the example service sections** in `docker-compose.yml`
2. **Create a Dockerfile** in your service directory
3. **Update the service configuration** as needed

### Example Service Configuration

```yaml
account-service:
  build:
    context: ./services/account
    dockerfile: Dockerfile
  container_name: ebanking-account-service
  environment:
    ASPNETCORE_ENVIRONMENT: Development
    ASPNETCORE_URLS: http://+:80
    ConnectionStrings__DefaultConnection: Server=mssql,1433;Database=EBanking;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=true;
    Kafka__BootstrapServers: redpanda:29092
    Keycloak__Authority: http://keycloak:8080/realms/ebanking
  ports:
    - "5001:80"
  networks:
    - ebanking-network
  depends_on:
    - mssql
    - redpanda
    - keycloak
```

## Database Setup

The consolidated database `EBanking` will be automatically created. To run migrations:

```bash
# From your service directory
dotnet ef database update --connection "Server=localhost,1433;Database=EBanking;User Id=sa;Password=EBanking123!;TrustServerCertificate=true;"
```

## Keycloak Setup

1. Access Keycloak at http://localhost:8180
2. Login with admin/admin123
3. Import the realm configuration from `infrastructure/keycloak/ebanking-realm.json`

## Useful Commands

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f [service-name]

# Stop all services
docker-compose down

# Stop and remove volumes (WARNING: This will delete all data)
docker-compose down -v

# Rebuild services
docker-compose build

# Scale a service
docker-compose up -d --scale account-service=2
```

## Development Workflow

1. **Infrastructure First:** Start with `docker-compose up -d mssql redpanda keycloak`
2. **Add Services Gradually:** Uncomment and configure services as needed
3. **Local Development:** Services can run locally while connecting to containerized infrastructure
4. **Testing:** Use the full containerized environment for integration testing

## Troubleshooting

### MSSQL Connection Issues
- Ensure SQL Server is fully started (check logs: `docker-compose logs mssql`)
- Verify connection string uses `TrustServerCertificate=true`

### Keycloak Startup Issues
- Keycloak takes time to initialize on first startup
- Check if MSSQL is running before starting Keycloak

### Kafka Connection Issues
- Use `redpanda:29092` for internal communication
- Use `localhost:9092` for external connections

### Port Conflicts
- Check if ports are already in use: `netstat -an | findstr "1433\|8080\|8180\|9092"`
- Modify ports in docker-compose.yml if needed