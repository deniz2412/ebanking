# E-Banking Platform Cleanup Script
# Removes all E-banking components from Kubernetes

param(
    [switch]$Force = $false,
    [switch]$KeepNamespaces = $false
)

Write-Host "🧹 Cleaning up E-banking Platform..." -ForegroundColor Yellow

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

if (-not $Force) {
    $confirm = Read-Host "This will delete ALL E-banking components. Continue? (y/N)"
    if ($confirm -ne "y" -and $confirm -ne "Y") {
        Write-Host "Cleanup cancelled."
        exit 0
    }
}

# Remove Helm releases
Write-Step "Removing Helm releases..."

$helmReleases = @(
    @{Name="ingress-nginx"; Namespace="dmz"},
    @{Name="cert-manager"; Namespace="security"},
    @{Name="csi-secrets-store"; Namespace="security"},
    @{Name="vault-csi-provider"; Namespace="security"}
)

foreach ($release in $helmReleases) {
    Write-Host "Removing Helm release: $($release.Name) from namespace: $($release.Namespace)"
    helm uninstall $release.Name -n $release.Namespace --ignore-not-found
}

# Remove custom resources
Write-Step "Removing custom resources..."

Write-Host "Removing ingress resources..."
kubectl delete ingress --all -A --ignore-not-found

Write-Host "Removing certificates..."
kubectl delete certificate --all -A --ignore-not-found

Write-Host "Removing secret provider classes..."
kubectl delete secretproviderclass --all -A --ignore-not-found

Write-Host "Removing network policies..."
kubectl delete networkpolicy --all -A --ignore-not-found

# Remove application deployments
Write-Step "Removing application deployments..."

$namespaces = @("svc", "ebanking-backend", "data", "security", "dmz")

foreach ($ns in $namespaces) {
    Write-Host "Removing all resources from namespace: $ns"
    kubectl delete all --all -n $ns --ignore-not-found
    kubectl delete configmap --all -n $ns --ignore-not-found
    kubectl delete secret --all -n $ns --ignore-not-found
    kubectl delete pvc --all -n $ns --ignore-not-found
    kubectl delete serviceaccount --all -n $ns --ignore-not-found
}

# Remove cert-manager CRDs
Write-Step "Removing cert-manager CRDs..."
kubectl delete -f https://github.com/cert-manager/cert-manager/releases/download/v1.15.1/cert-manager.crds.yaml --ignore-not-found

# Remove namespaces (optional)
if (-not $KeepNamespaces) {
    Write-Step "Removing namespaces..."
    foreach ($ns in $namespaces) {
        Write-Host "Removing namespace: $ns"
        kubectl delete namespace $ns --ignore-not-found
    }
}

# Clean up any remaining finalizers
Write-Step "Cleaning up finalizers..."
kubectl patch namespace security -p '{"metadata":{"finalizers":[]}}' --type=merge --ignore-not-found
kubectl patch namespace svc -p '{"metadata":{"finalizers":[]}}' --type=merge --ignore-not-found
kubectl patch namespace ebanking-backend -p '{"metadata":{"finalizers":[]}}' --type=merge --ignore-not-found
kubectl patch namespace data -p '{"metadata":{"finalizers":[]}}' --type=merge --ignore-not-found
kubectl patch namespace dmz -p '{"metadata":{"finalizers":[]}}' --type=merge --ignore-not-found

Write-Success "E-banking platform cleanup completed!"
Write-Host ""
Write-Host "📋 Verify cleanup:" -ForegroundColor Cyan
Write-Host "   kubectl get pods -A"
Write-Host "   kubectl get namespaces"
Write-Host "   helm list -A"
