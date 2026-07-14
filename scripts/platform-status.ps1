# E-Banking Platform Status Check Script
# Provides comprehensive status of all platform components

param(
    [switch]$Detailed = $false,
    [switch]$Watch = $false
)

function Write-Header {
    param($Message)
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Blue
    Write-Host " $Message" -ForegroundColor Blue
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Blue
}

function Write-Section {
    param($Message)
    Write-Host ""
    Write-Host "▶ $Message" -ForegroundColor Cyan
    Write-Host "─────────────────────────────────────────" -ForegroundColor DarkGray
}

function Get-PodStatus {
    param($Namespace)
    kubectl get pods -n $Namespace --no-headers 2>$null | ForEach-Object {
        $fields = $_ -split '\s+'
        $name = $fields[0]
        $ready = $fields[1]
        $status = $fields[2]
        $restarts = $fields[3]
        $age = $fields[4]
        
        $color = "White"
        if ($status -eq "Running") { $color = "Green" }
        elseif ($status -like "*Error*" -or $status -like "*Failed*") { $color = "Red" }
        elseif ($status -like "*Pending*" -or $status -like "*Init*") { $color = "Yellow" }
        
        Write-Host "  $name" -NoNewline
        Write-Host " ($ready)" -NoNewline -ForegroundColor DarkGray
        Write-Host " [$status]" -ForegroundColor $color
        
        if ($Detailed -and $restarts -gt 0) {
            Write-Host "    ↳ Restarts: $restarts" -ForegroundColor Yellow
        }
    }
}

function Get-ServiceStatus {
    param($Namespace)
    kubectl get svc -n $Namespace --no-headers 2>$null | ForEach-Object {
        $fields = $_ -split '\s+'
        $name = $fields[0]
        $type = $fields[1]
        $clusterIP = $fields[2]
        $externalIP = $fields[3]
        $ports = $fields[4]
        
        Write-Host "  $name" -NoNewline
        Write-Host " ($type)" -NoNewline -ForegroundColor DarkGray
        Write-Host " - $clusterIP:$ports" -ForegroundColor White
    }
}

function Get-IngressStatus {
    kubectl get ingress -A --no-headers 2>$null | ForEach-Object {
        $fields = $_ -split '\s+'
        $namespace = $fields[0]
        $name = $fields[1]
        $class = $fields[2]
        $hosts = $fields[3]
        $address = $fields[4]
        $ports = $fields[5]
        
        $color = if ($address -eq "<none>") { "Yellow" } else { "Green" }
        
        Write-Host "  $name" -NoNewline
        Write-Host " ($namespace)" -NoNewline -ForegroundColor DarkGray
        Write-Host " - $hosts" -ForegroundColor $color
        if ($Detailed) {
            Write-Host "    ↳ Address: $address, Ports: $ports" -ForegroundColor DarkGray
        }
    }
}

function Check-HealthEndpoints {
    $endpoints = @(
        @{Name="API Gateway"; URL="https://ebank.local/api/health"},
        @{Name="Keycloak"; URL="https://ebank.local/auth/health"},
        @{Name="Frontend"; URL="https://ebank.local"}
    )
    
    foreach ($endpoint in $endpoints) {
        try {
            $response = Invoke-WebRequest -Uri $endpoint.URL -Method GET -SkipCertificateCheck -TimeoutSec 5 -ErrorAction Stop
            $status = if ($response.StatusCode -eq 200) { "✅ OK" } else { "⚠️ $($response.StatusCode)" }
            Write-Host "  $($endpoint.Name): $status" -ForegroundColor Green
        }
        catch {
            Write-Host "  $($endpoint.Name): ❌ Failed" -ForegroundColor Red
            if ($Detailed) {
                Write-Host "    ↳ Error: $($_.Exception.Message)" -ForegroundColor DarkRed
            }
        }
    }
}

do {
    if ($Watch) { Clear-Host }
    
    Write-Header "🏦 E-Banking Platform Status"
    Write-Host "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor DarkGray
    
    # Cluster Info
    Write-Section "🔧 Cluster Information"
    try {
        $clusterInfo = kubectl cluster-info 2>$null | Select-Object -First 1
        Write-Host "  $clusterInfo" -ForegroundColor Green
    }
    catch {
        Write-Host "  ❌ Cluster not accessible" -ForegroundColor Red
    }
    
    # Namespace Status
    Write-Section "📁 Namespaces"
    $namespaces = @("dmz", "svc", "ebanking-backend", "data", "security")
    foreach ($ns in $namespaces) {
        $exists = kubectl get namespace $ns 2>$null
        if ($exists) {
            Write-Host "  ✅ $ns" -ForegroundColor Green
        } else {
            Write-Host "  ❌ $ns (missing)" -ForegroundColor Red
        }
    }
    
    # Infrastructure Layer
    Write-Section "🏗️ Infrastructure Layer (DMZ)"
    Get-PodStatus -Namespace "dmz"
    
    # Application Layer
    Write-Section "🚀 Application Layer (SVC)"
    Get-PodStatus -Namespace "svc"
    
    # Backend Services
    Write-Section "⚙️ Backend Services"
    Get-PodStatus -Namespace "ebanking-backend"
    
    # Data Layer
    Write-Section "💾 Data Layer"
    Get-PodStatus -Namespace "data"
    
    # Security Layer
    Write-Section "🔐 Security Layer"
    Get-PodStatus -Namespace "security"
    
    # Services
    if ($Detailed) {
        Write-Section "🌐 Services"
        Write-Host "  SVC Namespace:"
        Get-ServiceStatus -Namespace "svc"
        Write-Host "  Backend Namespace:"
        Get-ServiceStatus -Namespace "ebanking-backend"
        Write-Host "  Data Namespace:"
        Get-ServiceStatus -Namespace "data"
        Write-Host "  Security Namespace:"
        Get-ServiceStatus -Namespace "security"
    }
    
    # Ingress Status
    Write-Section "🌍 Ingress Status"
    Get-IngressStatus
    
    # Vault Status
    Write-Section "🔒 Vault & Secrets"
    try {
        $vaultPods = kubectl get pods -n security -l app=vault --no-headers 2>$null
        if ($vaultPods) {
            Write-Host "  ✅ Vault pod running" -ForegroundColor Green
            
            # Check Secret Provider Classes
            $spcCount = (kubectl get secretproviderclass -A --no-headers 2>$null | Measure-Object).Count
            Write-Host "  📝 Secret Provider Classes: $spcCount" -ForegroundColor Cyan
        } else {
            Write-Host "  ❌ Vault not running" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "  ❌ Vault status unknown" -ForegroundColor Red
    }
    
    # Network Policies
    Write-Section "🛡️ Network Policies"
    try {
        $npCount = (kubectl get networkpolicy -A --no-headers 2>$null | Measure-Object).Count
        Write-Host "  📋 Active Network Policies: $npCount" -ForegroundColor Cyan
        
        if ($Detailed) {
            kubectl get networkpolicy -A --no-headers 2>$null | ForEach-Object {
                $fields = $_ -split '\s+'
                Write-Host "    • $($fields[1]) ($($fields[0]))" -ForegroundColor DarkGray
            }
        }
    }
    catch {
        Write-Host "  ❌ Network policies status unknown" -ForegroundColor Red
    }
    
    # Health Checks
    Write-Section "🩺 Health Checks"
    Check-HealthEndpoints
    
    # Helm Releases
    Write-Section "📦 Helm Releases"
    try {
        $releases = helm list -A --short 2>$null
        if ($releases) {
            $releases | ForEach-Object {
                Write-Host "  ✅ $_" -ForegroundColor Green
            }
        } else {
            Write-Host "  ℹ️ No Helm releases found" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "  ❌ Helm status unknown" -ForegroundColor Red
    }
    
    # Summary
    Write-Section "📊 Quick Summary"
    $totalPods = (kubectl get pods -A --no-headers 2>$null | Measure-Object).Count
    $runningPods = (kubectl get pods -A --no-headers 2>$null | Where-Object { $_ -match "Running" } | Measure-Object).Count
    $readyPercentage = if ($totalPods -gt 0) { [math]::Round(($runningPods / $totalPods) * 100, 1) } else { 0 }
    
    Write-Host "  Total Pods: $totalPods" -ForegroundColor Cyan
    Write-Host "  Running Pods: $runningPods" -ForegroundColor Green
    Write-Host "  Ready: $readyPercentage%" -ForegroundColor $(if ($readyPercentage -gt 80) { "Green" } elseif ($readyPercentage -gt 50) { "Yellow" } else { "Red" })
    
    if ($Watch) {
        Write-Host ""
        Write-Host "🔄 Auto-refreshing every 30 seconds... (Ctrl+C to stop)" -ForegroundColor DarkGray
        Start-Sleep -Seconds 30
    }
    
} while ($Watch)

Write-Host ""
Write-Host "💡 Tips:" -ForegroundColor Yellow
Write-Host "  • Use -Detailed for more information"
Write-Host "  • Use -Watch for auto-refresh"
Write-Host "  • Check individual pods: kubectl describe pod <name> -n <namespace>"
Write-Host "  • View logs: kubectl logs <pod-name> -n <namespace>"
