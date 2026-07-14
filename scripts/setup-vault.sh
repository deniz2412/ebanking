#!/bin/bash

# Vault initialization script for E-banking
# This script sets up Vault with initial secrets for development

set -e

echo "🔐 Setting up HashiCorp Vault for E-banking..."

# Wait for Vault to be ready
echo "Waiting for Vault to be ready..."
kubectl wait --for=condition=ready pod -l app=vault -n security --timeout=120s

# Port forward to access Vault
echo "Setting up port forward to Vault..."
kubectl port-forward -n security svc/vault 8200:8200 &
VAULT_PID=$!

# Wait a moment for port forward
sleep 5

# Set Vault environment
export VAULT_ADDR="http://localhost:8200"
export VAULT_TOKEN="dev-root-token"

# Install vault CLI if not present (for local development)
if ! command -v vault &> /dev/null; then
    echo "Vault CLI not found. Please install it from: https://www.vaultproject.io/downloads"
    echo "Or use kubectl exec for vault commands"
    kill $VAULT_PID 2>/dev/null || true
    exit 1
fi

echo "📝 Creating initial secrets..."

# Enable KV secrets engine
vault secrets enable -path=secret kv-v2

# Account Service secrets
vault kv put secret/account-service \
    database-connection-string="Server=mssql.data.svc.cluster.local,1433;Database=AccountService;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;" \
    jwt-signing-key="your-super-secret-jwt-key-change-in-production" \
    encryption-key="your-encryption-key-32-chars-long"

# Transfer Service secrets
vault kv put secret/transfer-service \
    database-connection-string="Server=mssql.data.svc.cluster.local,1433;Database=TransferService;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;" \
    sepa-endpoint="https://api.sepa.example.com" \
    sepa-api-key="your-sepa-api-key"

# Payment Service secrets
vault kv put secret/payment-service \
    database-connection-string="Server=mssql.data.svc.cluster.local,1433;Database=PaymentService;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;" \
    payment-gateway-endpoint="https://api.payment.example.com" \
    payment-gateway-key="your-payment-gateway-key"

# Notification Service secrets
vault kv put secret/notification-service \
    database-connection-string="Server=mssql.data.svc.cluster.local,1433;Database=NotificationService;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;" \
    smtp-host="smtp.example.com" \
    smtp-username="notifications@ebank.local" \
    smtp-password="your-smtp-password" \
    vapid-public-key="your-vapid-public-key" \
    vapid-private-key="your-vapid-private-key"

# API Gateway secrets
vault kv put secret/api-gateway \
    jwt-signing-key="your-super-secret-jwt-key-change-in-production" \
    rate-limit-redis="redis://redis.svc.svc.cluster.local:6379" \
    client-id="api-gateway" \
    client-secret="your-api-gateway-client-secret"

echo "✅ Secrets created successfully!"

# Create auth policy for services
vault policy write ebanking-services - <<EOF
path "secret/data/account-service/*" {
  capabilities = ["read"]
}
path "secret/data/transfer-service/*" {
  capabilities = ["read"]
}
path "secret/data/payment-service/*" {
  capabilities = ["read"]
}
path "secret/data/notification-service/*" {
  capabilities = ["read"]
}
path "secret/data/api-gateway/*" {
  capabilities = ["read"]
}
EOF

echo "✅ Vault setup completed!"
echo ""
echo "📋 Next steps:"
echo "1. In production, replace dev mode with proper Vault deployment"
echo "2. Configure proper authentication (Kubernetes auth method)"
echo "3. Use CSI driver to inject secrets into pods"
echo "4. Rotate the dev-root-token"
echo ""
echo "🔗 Vault UI: http://localhost:8200 (token: dev-root-token)"

# Clean up port forward
kill $VAULT_PID 2>/dev/null || true
