# E-Banking Platform Deployment Script (Consolidated)
# Deploys complete E-banking platform with Vault CSI integration

param(
    [switch]$SkipTests = $false,
    [switch]$DevMode = $true,
    [switch]$InitVault = $true,
    [switch]$DeployBackend = $true,
    [string]$Environment = "development"
)

Write-Host "🚀 Deploying E-banking Platform (Consolidated)..." -ForegroundColor Blue
Write-Host "Environment: $Environment" -ForegroundColor Cyan

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

function Wait-ForDeployment {
    param(
        [string]$Name,
        [string]$Namespace,
        [int]$TimeoutSeconds = 300
    )
    Write-Host "Waiting for $Name deployment to be ready..."
    kubectl wait --for=condition=available deployment/$Name -n $Namespace --timeout="${TimeoutSeconds}s"
}

function Wait-ForPod {
    param(
        [string]$Label,
        [string]$Namespace,
        [int]$TimeoutSeconds = 300
    )
    Write-Host "Waiting for pod with label $Label to be ready..."
    kubectl wait --for=condition=ready pod -l $Label -n $Namespace --timeout="${TimeoutSeconds}s"
}

# Check prerequisites
Write-Step "Checking prerequisites..."

$requiredTools = @("kubectl", "helm")
foreach ($tool in $requiredTools) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        Write-Error "$tool is required but not installed"
        exit 1
    }
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

# Phase 1: Core Infrastructure
Write-Step "Phase 1: Core Infrastructure Setup"

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
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx -n dmz --create-namespace `
    --set controller.ingressClassResource.name=nginx `
    --set controller.ingressClass=nginx `
    --wait

Write-Success "Phase 1: Core Infrastructure completed"

# Phase 2: Secrets Management (Vault + CSI Driver)
Write-Step "Phase 2: Vault & Secrets Management"

Write-Host "Installing Secrets Store CSI Driver..."
helm repo add secrets-store-csi-driver https://kubernetes-sigs.github.io/secrets-store-csi-driver/charts --force-update
helm upgrade --install csi-secrets-store secrets-store-csi-driver/secrets-store-csi-driver `
    --namespace security --create-namespace `
    --set syncSecret.enabled=true `
    --set enableSecretRotation=true `
    --wait

Write-Host "Installing Vault CSI Provider..."
helm repo add hashicorp https://helm.releases.hashicorp.com --force-update
helm upgrade --install vault-csi-provider hashicorp/vault-csi-provider `
    --namespace security `
    --wait

Write-Host "Deploying Vault configuration..."
kubectl apply -f infrastructure/k8s/vault/vault-config.yaml

Write-Host "Deploying Vault..."
kubectl apply -f infrastructure/k8s/vault/vault.yaml

Write-Host "Deploying Vault CSI driver configuration..."
kubectl apply -f infrastructure/k8s/vault/vault-csi-driver.yaml

Wait-ForDeployment -Name "vault" -Namespace "security"

if ($InitVault) {
    Write-Host "Initializing Vault with secrets..."
    
    # Wait a bit more for Vault to be fully ready
    Start-Sleep -Seconds 10
    
    # Port forward to access Vault
    Write-Host "Setting up port forward to Vault..."
    $vaultPort = Start-Process kubectl -ArgumentList "port-forward -n security svc/vault 8200:8200" -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 5
    
    try {
        $env:VAULT_ADDR = "http://localhost:8200"
        
        # Initialize Vault (dev mode)
        if ($DevMode) {
            Write-Host "Vault running in development mode with auto-initialization"
            $env:VAULT_TOKEN = "dev-root-token"
        } else {
            Write-Warning "Production mode: Manual Vault initialization required"
        }
        
        # Check if vault CLI is available
        if (Get-Command vault -ErrorAction SilentlyContinue) {
            Write-Host "Setting up Vault secrets..."
            
            # Enable KV secrets engine
            vault secrets enable -path=secret kv-v2
            
            # Enable Kubernetes auth
            vault auth enable kubernetes
            
            # Configure Kubernetes auth
            $k8sHost = kubectl config view --raw --minify --flatten --output='jsonpath={.clusters[].cluster.server}'
            $k8sCert = kubectl config view --raw --minify --flatten --output='jsonpath={.clusters[].cluster.certificate-authority-data}'
            
            vault write auth/kubernetes/config `
                token_reviewer_jwt="$(kubectl get secret -n security $(kubectl get serviceaccount vault -n security -o jsonpath='{.secrets[0].name}') -o jsonpath='{.data.token}' | base64 -d)" `
                kubernetes_host="$k8sHost" `
                kubernetes_ca_cert="$(echo $k8sCert | base64 -d)"
            
            # Create role for ebanking services
            vault write auth/kubernetes/role/ebanking-role `
                bound_service_account_names=ebanking-vault-auth `
                bound_service_account_namespaces=ebanking-backend,svc `
                policies=ebanking-services `
                ttl=24h
            
            # Create secrets
            vault kv put secret/account-service `
                database-connection-string="Server=mssql.data.svc.cluster.local,1433;Database=AccountService;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;" `
                jwt-signing-key="your-super-secret-jwt-key-change-in-production" `
                encryption-key="your-encryption-key-32-chars-long"
            
            vault kv put secret/transfer-service `
                database-connection-string="Server=mssql.data.svc.cluster.local,1433;Database=TransferService;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;" `
                sepa-endpoint="https://api.sepa.example.com" `
                sepa-api-key="your-sepa-api-key"
            
            vault kv put secret/payment-service `
                database-connection-string="Server=mssql.data.svc.cluster.local,1433;Database=PaymentService;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;" `
                payment-gateway-endpoint="https://api.payment.example.com" `
                payment-gateway-key="your-payment-gateway-key"
            
            vault kv put secret/notification-service `
                database-connection-string="Server=mssql.data.svc.cluster.local,1433;Database=NotificationService;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;" `
                smtp-host="smtp.example.com" `
                smtp-username="notifications@ebank.local" `
                smtp-password="your-smtp-password" `
                vapid-public-key="your-vapid-public-key" `
                vapid-private-key="your-vapid-private-key"
            
            vault kv put secret/api-gateway `
                jwt-signing-key="your-super-secret-jwt-key-change-in-production" `
                rate-limit-redis="redis://redis.svc.svc.cluster.local:6379"
            
            # Create policy
            @"
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
"@ | vault policy write ebanking-services -
            
            Write-Success "Vault secrets initialized successfully"
        } else {
            Write-Warning "Vault CLI not found. Secrets need to be initialized manually."
        }
    }
    finally {
        # Clean up port forward
        if ($vaultPort) {
            Stop-Process $vaultPort -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host "Deploying Secret Provider Classes..."
kubectl apply -f infrastructure/k8s/vault/secret-provider-classes.yaml

Write-Success "Phase 2: Vault & Secrets Management completed"

# Phase 3: Data Layer
Write-Step "Phase 3: Data Layer Setup"

Write-Host "Deploying MSSQL secrets..."
kubectl apply -f infrastructure/k8s/mssql/secret.yaml

Write-Host "Deploying MSSQL database..."
kubectl apply -f infrastructure/k8s/mssql/mssql.yaml

Write-Host "Deploying Kafka/Redpanda..."
kubectl apply -f infrastructure/k8s/kafka/redpanda.yaml

Wait-ForPod -Label "app=mssql" -Namespace "data"
Wait-ForDeployment -Name "redpanda" -Namespace "svc"

Write-Success "Phase 3: Data Layer completed"

# Phase 4: Security Layer
Write-Step "Phase 4: Security & Authentication"

Write-Host "Creating Keycloak realm ConfigMap..."
kubectl apply -f infrastructure/k8s/keycloak/keycloak-realm.yaml

Write-Host "Deploying Keycloak..."
kubectl apply -f infrastructure/k8s/keycloak/keycloak.yaml

Wait-ForDeployment -Name "keycloak" -Namespace "security"

Write-Success "Phase 4: Security & Authentication completed"

# Phase 5: Application Layer
Write-Step "Phase 5: Application Services"

Write-Host "Creating gateway ConfigMap..."
kubectl apply -f infrastructure/k8s/gateway/gateway-ocelot-configmap.yaml

Write-Host "Deploying API Gateway..."
kubectl apply -f infrastructure/k8s/gateway/api-gateway.yaml

Write-Host "Deploying Frontend..."
kubectl apply -f infrastructure/k8s/frontend/frontend.yaml

Wait-ForDeployment -Name "api-gateway" -Namespace "svc"
Wait-ForDeployment -Name "frontend" -Namespace "svc"

if ($DeployBackend) {
    Write-Step "Deploying Backend Microservices"
    
    Write-Host "Deploying Account Service..."
    kubectl apply -f infrastructure/k8s/account/account-service.yaml
    
    Write-Host "Deploying notification secrets..."
    kubectl apply -f infrastructure/k8s/services/notification-secret.yaml
    
    Write-Host "Deploying Transfer Service..."
    kubectl apply -f infrastructure/k8s/services/transfer-service.yaml
    
    Write-Host "Deploying Payment Service..."
    kubectl apply -f infrastructure/k8s/services/payment-service.yaml
    
    Write-Host "Deploying Notification Service..."
    kubectl apply -f infrastructure/k8s/services/notification-service.yaml
    
    Write-Host "Deploying Audit Service..."
    kubectl apply -f infrastructure/k8s/services/audit-service.yaml
    
    Write-Host "Waiting for backend services to be ready..."
    Wait-ForDeployment -Name "account-service" -Namespace "ebanking-backend"
    Wait-ForDeployment -Name "transfer-service" -Namespace "ebanking-backend"
    Wait-ForDeployment -Name "payment-service" -Namespace "ebanking-backend"
    Wait-ForDeployment -Name "notification-service" -Namespace "ebanking-backend"
    Wait-ForDeployment -Name "audit-service" -Namespace "ebanking-backend"
    
    Write-Success "Backend Microservices deployed"
}

Write-Success "Phase 5: Application Services completed"

# Phase 6: Network Policies
Write-Step "Phase 6: Network Security Policies"

Write-Host "Applying default deny policy..."
kubectl apply -f infrastructure/k8s/network/default-deny.yaml

Write-Host "Applying DNS egress policies..."
kubectl apply -f infrastructure/k8s/network/allow-dns-egress.yaml

Write-Host "Applying ingress communication policies..."
kubectl apply -f infrastructure/k8s/network/allow-ingress-to-gateway.yaml
kubectl apply -f infrastructure/k8s/network/allow-ingress-to-keycloak.yaml

Write-Host "Applying DMZ policies..."
kubectl apply -f infrastructure/k8s/network/allow-dmz-to-gateway.yaml
kubectl apply -f infrastructure/k8s/network/allow-probes-gateway.yaml

Write-Host "Applying backend communication policies..."
kubectl apply -f infrastructure/k8s/network/allow-backend-communication.yaml

Write-Host "Applying backend to data access policies..."
kubectl apply -f infrastructure/k8s/network/allow-backend-to-data.yaml

Write-Success "Phase 6: Network Security Policies completed"

# Phase 7: Ingress Configuration
Write-Step "Phase 7: Ingress & TLS Setup"

Write-Host "Deploying main ingress..."
kubectl apply -f infrastructure/k8s/ingress/ebank-ingress.yaml

Write-Host "Deploying Keycloak ingress..."
kubectl apply -f infrastructure/k8s/ingress/keycloak-ingress.yaml

Write-Success "Phase 7: Ingress & TLS Setup completed"

# Final Status Check
Write-Step "Final Status Check"

Write-Host "Checking deployment status..."
kubectl get pods -A | Where-Object { $_ -notmatch "Running|Completed" }

Write-Host "Checking ingress status..."
kubectl get ingress -A

# Summary
Write-Host ""
Write-Host "🎉 E-Banking Platform Deployment Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Deployment Summary:" -ForegroundColor Cyan
Write-Host "✅ Core Infrastructure (namespaces, ingress, TLS)"
Write-Host "✅ Vault with CSI Driver integration"
Write-Host "✅ Data Layer (MSSQL, Kafka/Redpanda)"
Write-Host "✅ Security Layer (Keycloak, certificates)"
Write-Host "✅ Application Layer (API Gateway, Frontend)"
if ($DeployBackend) {
    Write-Host "✅ Backend Microservices (Account, Transfer, Payment, Notification, Audit)"
}
Write-Host "✅ Network Security Policies"
Write-Host "✅ Ingress & TLS Configuration"
Write-Host ""
Write-Host "🔗 Access Points:" -ForegroundColor Cyan
Write-Host "   - E-banking App: https://ebank.local"
Write-Host "   - Keycloak Admin: https://ebank.local/auth (admin/admin)"
Write-Host "   - API Gateway: https://ebank.local/api/"
if ($DevMode) {
    Write-Host "   - Vault UI: http://localhost:8200 (token: dev-root-token)"
}
Write-Host ""
Write-Host "🔧 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Add to hosts file: 127.0.0.1 ebank.local"
if (-not $DevMode) {
    Write-Host "2. Initialize Vault manually for production"
}
Write-Host "2. Test connectivity: curl -k https://ebank.local/api/health"
Write-Host "3. Run integration tests"
Write-Host ""
Write-Host "📋 Check status:" -ForegroundColor Cyan
Write-Host "   kubectl get pods -A"
Write-Host "   kubectl get ingress -A"
Write-Host "   kubectl get secretproviderclass -A"
