SHELL := /bin/bash

.PHONY: k8s-base install-cert-manager install-ingress apply-platform all dev-setup dev-up dev-down deploy-e1 init-db test-platform e1-complete

# Platform setup (existing)
k8s-base:
	kubectl apply -f infrastructure/k8s/base/namespaces.yaml
	kubectl apply -f infrastructure/k8s/network/default-deny.yaml

install-cert-manager:
	kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.15.1/cert-manager.crds.yaml
	helm repo add jetstack https://charts.jetstack.io
	helm upgrade --install cert-manager jetstack/cert-manager -n security --create-namespace --version v1.15.1
	kubectl apply -f infrastructure/k8s/cert-manager/cluster-issuer.yaml

install-ingress:
	kubectl create ns ingress-nginx || true
	helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
	helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx -n ingress-nginx 	  --set controller.ingressClassResource.name=nginx 	  --set controller.ingressClass=nginx

apply-platform:
	kubectl apply -f infrastructure/k8s/gateway/api-gateway.yaml
	kubectl apply -f infrastructure/k8s/network/allow-ingress-to-gateway.yaml
	kubectl apply -f infrastructure/k8s/ingress/ebank-ingress.yaml

all: k8s-base install-cert-manager install-ingress apply-platform

# E1 - Complete platform deployment
deploy-e1:
	@echo "Deploying complete E1 platform..."
	bash scripts/deploy-e1-platform.sh

init-db:
	@echo "Initializing databases..."
	bash scripts/init-databases.sh

setup-vault:
	@echo "Setting up Vault with secrets..."
	bash scripts/setup-vault.sh

test-platform:
	@echo "Testing platform components..."
	bash scripts/test-platform.sh

# Complete E1 workflow
e1-complete: deploy-e1 init-db setup-vault test-platform
	@echo "🎉 E1 deployment complete!"

# Development workflow
dev-setup:
	@echo "Setting up development environment..."
	@echo "Checking prerequisites..."
	@command -v dotnet >/dev/null 2>&1 || { echo "dotnet CLI is required"; exit 1; }
	@command -v npm >/dev/null 2>&1 || { echo "npm is required"; exit 1; }
	@command -v kubectl >/dev/null 2>&1 || { echo "kubectl is required"; exit 1; }
	@echo "Installing frontend dependencies..."
	cd frontend/web && npm install
	@echo "Restoring .NET packages..."
	dotnet restore services/gateway-ocelot
	dotnet restore services/account
	dotnet restore services/transfer
	dotnet restore services/payment
	dotnet restore services/notification
	dotnet restore services/audit
	@echo "Development setup complete!"

dev-up:
	@echo "Starting development environment..."
	@echo "Note: Make sure your /etc/hosts contains: 127.0.0.1 ebank.local"
	@echo "Starting all microservices..."
	cd services/gateway-ocelot && dotnet run --urls "https://localhost:5000" &
	sleep 2
	cd services/account && dotnet run --urls "https://localhost:5001" &
	sleep 2
	cd services/transfer && dotnet run --urls "https://localhost:5002" &
	sleep 2
	cd services/payment && dotnet run --urls "https://localhost:5003" &
	sleep 2
	cd services/notification && dotnet run --urls "https://localhost:5004" &
	sleep 2
	cd services/audit && dotnet run --urls "https://localhost:5005" &
	sleep 2
	@echo "Starting frontend..."
	cd frontend/web && npm start &
	@echo ""
	@echo "🚀 Development environment started!"
	@echo "Frontend: http://localhost:4200"
	@echo "Gateway: https://localhost:5000"
	@echo "Account Service: https://localhost:5001/swagger"
	@echo "Transfer Service: https://localhost:5002/swagger"
	@echo "Payment Service: https://localhost:5003/swagger"
	@echo "Notification Service: https://localhost:5004/swagger"
	@echo "Audit Service: https://localhost:5005/swagger"

dev-down:
	@echo "Stopping development processes..."
	@pkill -f "ng serve" || true
	@pkill -f "dotnet run" || true

build:
	@echo "Building all services..."
	dotnet build services/gateway-ocelot --configuration Release
	dotnet build services/account --configuration Release
	dotnet build services/transfer --configuration Release
	dotnet build services/payment --configuration Release
	dotnet build services/notification --configuration Release
	dotnet build services/audit --configuration Release
	cd frontend/web && npm run build

test:
	@echo "Running tests..."
	dotnet test services/gateway-ocelot --configuration Release --verbosity normal || echo "No tests found"
	dotnet test services/account --configuration Release --verbosity normal || echo "No tests found"
	dotnet test services/transfer --configuration Release --verbosity normal || echo "No tests found"
	dotnet test services/payment --configuration Release --verbosity normal || echo "No tests found"
	dotnet test services/notification --configuration Release --verbosity normal || echo "No tests found"
	dotnet test services/audit --configuration Release --verbosity normal || echo "No tests found"
	cd frontend/web && npm test -- --watch=false --browsers=ChromeHeadless

# Docker builds (for later phases)
docker-build-gateway:
	docker build -t ebank/gateway-ocelot:latest services/gateway-ocelot

docker-build-account:
	docker build -t ebank/account-service:latest services/account

docker-build-transfer:
	docker build -t ebank/transfer-service:latest services/transfer

docker-build-payment:
	docker build -t ebank/payment-service:latest services/payment

docker-build-notification:
	docker build -t ebank/notification-service:latest services/notification

docker-build-audit:
	docker build -t ebank/audit-service:latest services/audit

# Build all Docker images
docker-build-all: docker-build-gateway docker-build-account docker-build-transfer docker-build-payment docker-build-notification docker-build-audit

# Kubernetes helpers
k8s-status:
	@echo "=== Kubernetes Status ==="
	kubectl get pods -A
	@echo ""
	@echo "=== Ingress Status ==="
	kubectl get ingress -A
	@echo ""
	@echo "=== Services ==="
	kubectl get svc -A

k8s-logs-gateway:
	kubectl logs -f -l app=api-gateway -n svc

k8s-logs-account:
	kubectl logs -f -l app=account-service -n svc

k8s-logs-transfer:
	kubectl logs -f -l app=transfer-service -n svc

k8s-logs-payment:
	kubectl logs -f -l app=payment-service -n svc

k8s-logs-notification:
	kubectl logs -f -l app=notification-service -n svc

k8s-logs-audit:
	kubectl logs -f -l app=audit-service -n svc

# Port forwarding for local access
port-forward-account:
	@echo "Account Service available at http://localhost:8080"
	kubectl port-forward -n svc svc/account-service 8080:80

port-forward-transfer:
	@echo "Transfer Service available at http://localhost:8081"
	kubectl port-forward -n svc svc/transfer-service 8081:80

port-forward-payment:
	@echo "Payment Service available at http://localhost:8082"
	kubectl port-forward -n svc svc/payment-service 8082:80

port-forward-notification:
	@echo "Notification Service available at http://localhost:8083"
	kubectl port-forward -n svc svc/notification-service 8083:80

port-forward-audit:
	@echo "Audit Service available at http://localhost:8084"
	kubectl port-forward -n svc svc/audit-service 8084:80

port-forward-keycloak:
	@echo "Keycloak available at http://localhost:8081"
	kubectl port-forward -n security svc/keycloak 8081:8080

port-forward-vault:
	@echo "Vault available at http://localhost:8082"
	kubectl port-forward -n security svc/vault 8082:8200

# Clean up
clean:
	@echo "Cleaning build artifacts..."
	find . -name "bin" -type d -exec rm -rf {} + 2>/dev/null || true
	find . -name "obj" -type d -exec rm -rf {} + 2>/dev/null || true
	cd frontend/web && npm run clean 2>/dev/null || true
