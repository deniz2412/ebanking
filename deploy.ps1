<#
  eBanking Secure - one deployment entry point (Docker Compose or Kubernetes).

    ./deploy.ps1 up [-Frontend]     start the stack with Docker Compose (build + run)
    ./deploy.ps1 down               stop the compose stack
    ./deploy.ps1 logs [service]     tail logs
    ./deploy.ps1 ps                 show status
    ./deploy.ps1 clean              stop and remove volumes (wipes data)
    ./deploy.ps1 k8s-up             build images + deploy to the current kube-context
    ./deploy.ps1 k8s-down           remove the Kubernetes resources

  Browser login prerequisite (once): add "127.0.0.1 keycloak" to
  C:\Windows\System32\drivers\etc\hosts so the browser and containers share one issuer.
#>
param(
  [Parameter(Position = 0)][string]$Command = "help",
  [Parameter(Position = 1)][string]$Arg,
  [switch]$Frontend
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

switch ($Command) {
  "up" {
    $profile = if ($Frontend) { @("--profile", "frontend") } else { @() }
    docker compose @profile up -d --build
    Write-Host "up - gateway http://localhost:5000  keycloak http://localhost:8180  kafka-ui http://localhost:8080"
    if ($Frontend) { Write-Host "     SPA http://localhost:4200 (login: testuser / password123)" }
    Write-Host "note: add '127.0.0.1 keycloak' to your hosts file for browser login."
  }
  "down"  { docker compose --profile frontend down }
  "logs"  { docker compose logs -f $Arg }
  "ps"    { docker compose ps }
  "status"{ docker compose ps }
  "clean" { docker compose --profile frontend down -v }

  "k8s-up" {
    Write-Host "Building service images..."
    docker build -f Dockerfile.gateway -t ebanking/api-gateway:latest .
    foreach ($s in "account", "transfer", "payment", "notification", "audit") {
      docker build -f "Dockerfile.$s" -t "ebanking/$s-service:latest" .
    }
    docker build -f frontend/web/Dockerfile -t ebanking/frontend:latest frontend/web
    if (Get-Command kind -ErrorAction SilentlyContinue) {
      foreach ($img in "api-gateway", "account-service", "transfer-service", "payment-service", "notification-service", "audit-service", "frontend") {
        kind load docker-image "ebanking/$img:latest" 2>$null
      }
    }
    Write-Host "Applying manifests..."
    kubectl apply -k infrastructure/k8s
    Write-Host "k8s-up - check: kubectl get pods -A | Select-String ebanking"
  }
  "k8s-down" { kubectl delete -k infrastructure/k8s --ignore-not-found }

  default { Get-Help $PSCommandPath -Detailed }
}
