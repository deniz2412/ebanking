# E1 Platform Deployment Script (PowerShell)
# Deploys all components for Epic 1: Platform & Network

param(
    [switch]$SkipTests = $false
)

Write-Host "🚀 Deploying E-banking Platform (Epic 1)..." -ForegroundColor Blue

function Write-Step {
    param($Message)
    Write-Host "==> $Message" -ForegroundColor Blue
}

function Write-Success {
    param($Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Warning {
    param($Message)
    Write-Host "⚠️ $Message" -ForegroundColor Yellow
}

function Write-Error {
    param($Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

# Check prerequisites
Write-Step "Checking prerequisites..."

if (-not (Get-Command kubectl -ErrorAction SilentlyContinue)) {
    Write-Error "kubectl is required but not installed"
    exit 1
}

if (-not (Get-Command helm -ErrorAction SilentlyContinue)) {
    Write-Error "helm is required but not installed"
    exit 1
}

# Check if Kubernetes is running
try {
    kubectl cluster-info | Out-Null
    Write-Success "Prerequisites check passed"
}
catch {
    Write-Error "Kubernetes cluster is not accessible"
    exit 1
}

# S1.1 - Namespaces + Ingress + TLS
Write-Step "S1.1 - Setting up namespaces, ingress, and TLS..."

Write-Host "Creating namespaces..."
kubectl apply -f infrastructure/k8s/base/namespaces.yaml

Write-Host "Installing cert-manager..."
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.15.1/cert-manager.crds.yaml
helm repo add jetstack https://charts.jetstack.io --force-update
helm upgrade --install cert-manager jetstack/cert-manager -n security --create-namespace --version v1.15.1 --wait

Write-Host "Applying cluster issuer..."
kubectl apply -f infrastructure/k8s/cert-manager/cluster-issuer.yaml

Write-Host "Installing ingress-nginx..."
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx --force-update
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx -n dmz --create-namespace --set controller.ingressClassResource.name=nginx --set controller.ingressClass=nginx --wait

Write-Host "Applying ingress configuration..."
kubectl apply -f infrastructure/k8s/ingress/ebank-ingress.yaml

Write-Success "S1.1 - Ingress and TLS setup completed"

# S1.2 - Keycloak Setup
Write-Step "S1.2 - Setting up Keycloak with ebanking realm..."

Write-Host "Creating Keycloak realm ConfigMap..."
kubectl apply -f infrastructure/k8s/keycloak/keycloak-realm.yaml

Write-Host "Deploying Keycloak..."
kubectl apply -f infrastructure/k8s/keycloak/keycloak.yaml

Write-Host "Applying Keycloak ingress..."
kubectl apply -f infrastructure/k8s/keycloak/keycloak-ingress.yaml

Write-Host "Waiting for Keycloak to be ready..."
kubectl wait --for=condition=available deployment/keycloak -n security --timeout=300s

Write-Success "S1.2 - Keycloak setup completed"

# S1.3 - Vault Setup + CSI Driver
Write-Step "S1.3 - Setting up Vault with CSI driver for secrets management..."

Write-Host "Installing Secrets Store CSI Driver..."
helm repo add secrets-store-csi-driver https://kubernetes-sigs.github.io/secrets-store-csi-driver/charts --force-update
helm upgrade --install csi-secrets-store secrets-store-csi-driver/secrets-store-csi-driver --namespace security --create-namespace --set syncSecret.enabled=true --set enableSecretRotation=true --wait

Write-Host "Installing Vault CSI Provider..."
helm repo add hashicorp https://helm.releases.hashicorp.com --force-update
helm upgrade --install vault-csi-provider hashicorp/vault-csi-provider --namespace security --wait

Write-Host "Deploying Vault configuration..."
kubectl apply -f infrastructure/k8s/vault/vault-config.yaml

Write-Host "Deploying Vault..."
kubectl apply -f infrastructure/k8s/vault/vault.yaml

Write-Host "Deploying Vault CSI driver configuration..."
kubectl apply -f infrastructure/k8s/vault/vault-csi-driver.yaml

Write-Host "Waiting for Vault to be ready..."
kubectl wait --for=condition=available deployment/vault -n security --timeout=300s

Write-Host "Deploying Secret Provider Classes..."
kubectl apply -f infrastructure/k8s/vault/secret-provider-classes.yaml

Write-Success "S1.3 - Vault with CSI driver setup completed"

# S1.4 - Kafka/Redpanda
Write-Step "S1.4 - Setting up Kafka/Redpanda for event streaming..."

Write-Host "Deploying Redpanda..."
kubectl apply -f infrastructure/k8s/kafka/redpanda.yaml

Write-Host "Waiting for Redpanda to be ready..."
kubectl wait --for=condition=available deployment/redpanda -n svc --timeout=120s

Write-Success "S1.4 - Kafka/Redpanda setup completed"

# S1.5 - MSSQL Database
Write-Step "S1.5 - Setting up MSSQL database..."

Write-Host "Creating MSSQL secrets..."
kubectl apply -f infrastructure/k8s/mssql/secret.yaml

Write-Host "Deploying MSSQL..."
kubectl apply -f infrastructure/k8s/mssql/mssql.yaml

Write-Host "Waiting for MSSQL to be ready..."
kubectl wait --for=condition=ready pod -l app=mssql -n data --timeout=300s

Write-Success "S1.5 - MSSQL setup completed"

# S1.6 - Network Policies
Write-Step "S1.6 - Implementing network policies..."

Write-Host "Applying default deny policy..."
kubectl apply -f infrastructure/k8s/network/default-deny.yaml

Write-Host "Applying DNS egress policies..."
kubectl apply -f infrastructure/k8s/network/allow-dns-egress.yaml

Write-Host "Applying ingress to gateway policy..."
kubectl apply -f infrastructure/k8s/network/allow-ingress-to-gateway.yaml

Write-Host "Applying ingress to keycloak policy..."
kubectl apply -f infrastructure/k8s/network/allow-ingress-to-keycloak.yaml

Write-Host "Applying DMZ to gateway policy..."
kubectl apply -f infrastructure/k8s/network/allow-dmz-to-gateway.yaml

Write-Host "Applying backend communication policies..."
kubectl apply -f infrastructure/k8s/network/allow-backend-communication.yaml

Write-Host "Applying backend to data access policies..."
kubectl apply -f infrastructure/k8s/network/allow-backend-to-data.yaml

Write-Host "Applying probes policy..."
kubectl apply -f infrastructure/k8s/network/allow-probes-gateway.yaml

Write-Success "S1.6 - Network policies implemented"

# Deploy API Gateway
Write-Step "Deploying API Gateway..."

Write-Host "Creating gateway ConfigMap..."
kubectl apply -f infrastructure/k8s/gateway/gateway-ocelot-configmap.yaml

Write-Host "Deploying API Gateway..."
kubectl apply -f infrastructure/k8s/gateway/api-gateway.yaml

Write-Host "Waiting for API Gateway to be ready..."
kubectl wait --for=condition=available deployment/api-gateway -n svc --timeout=120s

Write-Success "API Gateway deployed"

# Deploy Account Service
Write-Step "Deploying Account Service..."

Write-Host "Deploying Account Service..."
kubectl apply -f infrastructure/k8s/account/account-service.yaml

Write-Host "Deploying notification secrets..."
kubectl apply -f infrastructure/k8s/services/notification-secret.yaml

Write-Host "Waiting for Account Service to be ready..."
kubectl wait --for=condition=available deployment/account-service -n ebanking-backend --timeout=120s

Write-Success "Account Service deployed"

# Summary
Write-Host ""
Write-Host "🎉 E1 Platform Deployment Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Deployment Summary:" -ForegroundColor Cyan
Write-Host "✅ S1.1 - Namespaces, Ingress, TLS"
Write-Host "✅ S1.2 - Keycloak with ebanking realm"
Write-Host "✅ S1.3 - Vault with CSI driver integration"
Write-Host "✅ S1.4 - Kafka/Redpanda"
Write-Host "✅ S1.5 - MSSQL Database"
Write-Host "✅ S1.6 - Network Policies with DNS egress"
Write-Host "✅ API Gateway deployed"
Write-Host "✅ Account Service deployed"
Write-Host ""
Write-Host "🔗 Access Points:" -ForegroundColor Cyan
Write-Host "   - E-banking App: https://ebank.local"
Write-Host "   - Keycloak Admin: https://ebank.local/auth (admin/admin)"
Write-Host "   - API Gateway: https://ebank.local/api/"
Write-Host ""
Write-Host "🔧 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Add to hosts file: 127.0.0.1 ebank.local"
Write-Host "2. Run: .\scripts\init-databases.ps1"
Write-Host "3. Initialize Vault secrets: .\scripts\setup-vault.ps1"
Write-Host "4. Test: .\scripts\test-platform.ps1"
Write-Host ""
Write-Host "📋 Check status:" -ForegroundColor Cyan
Write-Host "   kubectl get pods -A"
Write-Host "   kubectl get ingress -A"
Write-Host "   kubectl get secretproviderclass -A"
