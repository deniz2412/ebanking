#!/bin/bash

# E1 Platform Deployment Script
# Deploys all components for Epic 1: Platform & Network

set -e

echo "🚀 Deploying E-banking Platform (Epic 1)..."

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

print_step() {
    echo -e "${BLUE}==>${NC} $1"
}

print_success() {
    echo -e "${GREEN}✅${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠️${NC} $1"
}

print_error() {
    echo -e "${RED}❌${NC} $1"
}

# Check prerequisites
print_step "Checking prerequisites..."

if ! command -v kubectl &> /dev/null; then
    print_error "kubectl is required but not installed"
    exit 1
fi

if ! command -v helm &> /dev/null; then
    print_error "helm is required but not installed"
    exit 1
fi

# Check if Kubernetes is running
if ! kubectl cluster-info &> /dev/null; then
    print_error "Kubernetes cluster is not accessible"
    exit 1
fi

print_success "Prerequisites check passed"

# S1.1 - Namespaces + Ingress + TLS
print_step "S1.1 - Setting up namespaces, ingress, and TLS..."

echo "Creating namespaces..."
kubectl apply -f infrastructure/k8s/base/namespaces.yaml

echo "Installing cert-manager..."
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.15.1/cert-manager.crds.yaml
helm repo add jetstack https://charts.jetstack.io --force-update
helm upgrade --install cert-manager jetstack/cert-manager \
    -n security \
    --create-namespace \
    --version v1.15.1 \
    --wait

echo "Applying cluster issuer..."
kubectl apply -f infrastructure/k8s/cert-manager/cluster-issuer.yaml

echo "Installing ingress-nginx..."
kubectl create ns ingress-nginx || true
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx --force-update
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx \
    -n ingress-nginx \
    --set controller.ingressClassResource.name=nginx \
    --set controller.ingressClass=nginx \
    --wait

echo "Applying ingress configuration..."
kubectl apply -f infrastructure/k8s/ingress/ebank-ingress.yaml

print_success "S1.1 - Ingress and TLS setup completed"

# S1.2 - Keycloak Setup
print_step "S1.2 - Setting up Keycloak with ebanking realm..."

echo "Creating Keycloak realm ConfigMap..."
kubectl apply -f infrastructure/k8s/keycloak/keycloak-realm.yaml

echo "Deploying Keycloak..."
kubectl apply -f infrastructure/k8s/keycloak/keycloak.yaml

echo "Applying Keycloak ingress..."
kubectl apply -f infrastructure/k8s/keycloak/keycloak-ingress.yaml

echo "Waiting for Keycloak to be ready..."
kubectl wait --for=condition=available deployment/keycloak -n security --timeout=300s

print_success "S1.2 - Keycloak setup completed"

# S1.3 - Vault Setup
print_step "S1.3 - Setting up Vault for secrets management..."

echo "Deploying Vault..."
kubectl apply -f infrastructure/k8s/vault/vault.yaml

echo "Waiting for Vault to be ready..."
kubectl wait --for=condition=available deployment/vault -n security --timeout=120s

print_success "S1.3 - Vault deployment completed"
print_warning "Run 'bash scripts/setup-vault.sh' to initialize secrets"

# S1.4 - Kafka/Redpanda
print_step "S1.4 - Setting up Kafka/Redpanda for event streaming..."

echo "Deploying Redpanda..."
kubectl apply -f infrastructure/k8s/kafka/redpanda.yaml

echo "Waiting for Redpanda to be ready..."
kubectl wait --for=condition=available deployment/redpanda -n svc --timeout=120s

print_success "S1.4 - Kafka/Redpanda setup completed"

# S1.5 - MSSQL Database
print_step "S1.5 - Setting up MSSQL database..."

echo "Creating MSSQL secrets..."
kubectl apply -f infrastructure/k8s/mssql/secret.yaml

echo "Deploying MSSQL..."
kubectl apply -f infrastructure/k8s/mssql/mssql.yaml

echo "Waiting for MSSQL to be ready..."
kubectl wait --for=condition=ready pod -l app=mssql -n data --timeout=300s

print_success "S1.5 - MSSQL setup completed"

# S1.6 - Network Policies
print_step "S1.6 - Implementing network policies..."

echo "Applying default deny policy..."
kubectl apply -f infrastructure/k8s/network/default-deny.yaml

echo "Applying ingress to gateway policy..."
kubectl apply -f infrastructure/k8s/network/allow-ingress-to-gateway.yaml

echo "Applying ingress to keycloak policy..."
kubectl apply -f infrastructure/k8s/network/allow-ingress-to-keycloak.yaml

echo "Applying DMZ to gateway policy..."
kubectl apply -f infrastructure/k8s/network/allow-dmz-to-gateway.yaml

echo "Applying probes policy..."
kubectl apply -f infrastructure/k8s/network/allow-probes-gateway.yaml

print_success "S1.6 - Network policies implemented"

# Deploy API Gateway
print_step "Deploying API Gateway..."

echo "Creating gateway ConfigMap..."
kubectl apply -f infrastructure/k8s/gateway/gateway-ocelot-configmap.yaml

echo "Deploying API Gateway..."
kubectl apply -f infrastructure/k8s/gateway/api-gateway.yaml

echo "Waiting for API Gateway to be ready..."
kubectl wait --for=condition=available deployment/api-gateway -n svc --timeout=120s

print_success "API Gateway deployed"

# Deploy Account Service
print_step "Deploying Account Service..."

echo "Deploying Account Service..."
kubectl apply -f infrastructure/k8s/account/account-service.yaml

echo "Waiting for Account Service to be ready..."
kubectl wait --for=condition=available deployment/account-service -n svc --timeout=120s

print_success "Account Service deployed"

# Summary
echo ""
echo "🎉 E1 Platform Deployment Complete!"
echo ""
echo "📊 Deployment Summary:"
echo "✅ S1.1 - Namespaces, Ingress, TLS"
echo "✅ S1.2 - Keycloak with ebanking realm"
echo "✅ S1.3 - Vault (needs secret initialization)"
echo "✅ S1.4 - Kafka/Redpanda"
echo "✅ S1.5 - MSSQL Database"
echo "✅ S1.6 - Network Policies"
echo "✅ API Gateway deployed"
echo "✅ Account Service deployed"
echo ""
echo "🔗 Access Points:"
echo "   - E-banking App: https://ebank.local"
echo "   - Keycloak Admin: https://ebank.local/auth (admin/admin)"
echo "   - API Gateway: https://ebank.local/api/"
echo ""
echo "🔧 Next Steps:"
echo "1. Run: bash scripts/setup-vault.sh"
echo "2. Initialize databases with: bash scripts/init-databases.sh"
echo "3. Test the setup with: bash scripts/test-platform.sh"
echo "4. Deploy frontend: kubectl apply -f infrastructure/k8s/frontend/"
echo ""
echo "📋 Check status:"
echo "   kubectl get pods -A"
echo "   kubectl get ingress -A"
echo "   kubectl get networkpolicies -A"
