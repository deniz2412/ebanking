# E-Banking Authentication Flow Test Script
# Tests the complete authentication flow from frontend to backend

param(
    [string]$BaseUrl = "https://ebank.local",
    [switch]$Verbose = $false
)

Write-Host "🧪 Testing E-Banking Authentication Flow..." -ForegroundColor Blue
Write-Host "Base URL: $BaseUrl" -ForegroundColor Cyan

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [int]$ExpectedStatus = 200
    )
    
    try {
        Write-Host "Testing $Name..." -NoNewline
        
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            SkipCertificateCheck = $true
            TimeoutSec = 10
        }
        
        if ($Body) {
            $params.Body = $Body
            $params.ContentType = "application/json"
        }
        
        $response = Invoke-WebRequest @params -ErrorAction Stop
        
        if ($response.StatusCode -eq $ExpectedStatus) {
            Write-Host " ✅ OK ($($response.StatusCode))" -ForegroundColor Green
            if ($Verbose) {
                Write-Host "    Response: $($response.Content.Substring(0, [Math]::Min(100, $response.Content.Length)))" -ForegroundColor DarkGray
            }
            return $true
        } else {
            Write-Host " ❌ Unexpected status: $($response.StatusCode)" -ForegroundColor Red
            return $false
        }
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq $ExpectedStatus) {
            Write-Host " ✅ OK ($statusCode - Expected)" -ForegroundColor Green
            return $true
        } else {
            Write-Host " ❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
            if ($Verbose) {
                Write-Host "    Detail: $($_.Exception)" -ForegroundColor DarkRed
            }
            return $false
        }
    }
}

# Test 1: Infrastructure Connectivity
Write-Host ""
Write-Host "📡 Testing Infrastructure Connectivity" -ForegroundColor Yellow

$tests = @()

# Frontend
$tests += Test-Endpoint "Frontend Root" "$BaseUrl" -ExpectedStatus 200

# Keycloak
$tests += Test-Endpoint "Keycloak Realm" "$BaseUrl/auth/realms/ebanking" -ExpectedStatus 200

# API Gateway Health
$tests += Test-Endpoint "API Gateway Health" "$BaseUrl/api/healthz" -ExpectedStatus 200

# Test 2: Authentication Endpoints
Write-Host ""
Write-Host "🔐 Testing Authentication Endpoints" -ForegroundColor Yellow

# Keycloak OpenID Configuration
$tests += Test-Endpoint "Keycloak OpenID Config" "$BaseUrl/auth/realms/ebanking/.well-known/openid_configuration" -ExpectedStatus 200

# Unauthenticated API calls should be rejected
$tests += Test-Endpoint "Protected Account API (No Auth)" "$BaseUrl/api/accounts" -ExpectedStatus 401

$tests += Test-Endpoint "Protected Transfer API (No Auth)" "$BaseUrl/api/transfers" -ExpectedStatus 401

# Test 3: Service Discovery (Internal)
Write-Host ""
Write-Host "🔍 Testing Service Discovery" -ForegroundColor Yellow

try {
    # Check if services are running in the cluster
    Write-Host "Checking Kubernetes services..." -NoNewline
    
    $services = kubectl get services -A --no-headers 2>$null | Where-Object { $_ -match "account-service|transfer-service|notification-service" }
    
    if ($services.Count -ge 3) {
        Write-Host " ✅ Services found" -ForegroundColor Green
        $tests += $true
        
        if ($Verbose) {
            $services | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
        }
    } else {
        Write-Host " ❌ Missing services" -ForegroundColor Red
        $tests += $false
    }
}
catch {
    Write-Host " ❌ Kubectl not available" -ForegroundColor Red
    $tests += $false
}

# Test 4: Network Policies
Write-Host ""
Write-Host "🛡️ Testing Network Policies" -ForegroundColor Yellow

try {
    Write-Host "Checking network policies..." -NoNewline
    
    $policies = kubectl get networkpolicy -A --no-headers 2>$null
    $requiredPolicies = @("default-deny", "allow-dns-egress", "allow-backend-communication", "allow-backend-to-data")
    
    $foundPolicies = 0
    foreach ($required in $requiredPolicies) {
        if ($policies -match $required) {
            $foundPolicies++
        }
    }
    
    if ($foundPolicies -eq $requiredPolicies.Count) {
        Write-Host " ✅ All policies present" -ForegroundColor Green
        $tests += $true
    } else {
        Write-Host " ⚠️ Missing $($requiredPolicies.Count - $foundPolicies) policies" -ForegroundColor Yellow
        $tests += $false
    }
}
catch {
    Write-Host " ❌ Cannot check policies" -ForegroundColor Red
    $tests += $false
}

# Test 5: Vault Integration
Write-Host ""
Write-Host "🔒 Testing Vault Integration" -ForegroundColor Yellow

try {
    Write-Host "Checking Vault status..." -NoNewline
    
    $vaultPods = kubectl get pods -n security -l app=vault --no-headers 2>$null
    if ($vaultPods -and $vaultPods -match "Running") {
        Write-Host " ✅ Vault running" -ForegroundColor Green
        $tests += $true
        
        # Check Secret Provider Classes
        Write-Host "Checking secret provider classes..." -NoNewline
        $spcCount = (kubectl get secretproviderclass -A --no-headers 2>$null | Measure-Object).Count
        if ($spcCount -gt 0) {
            Write-Host " ✅ $spcCount SPCs configured" -ForegroundColor Green
            $tests += $true
        } else {
            Write-Host " ❌ No SPCs found" -ForegroundColor Red
            $tests += $false
        }
    } else {
        Write-Host " ❌ Vault not running" -ForegroundColor Red
        $tests += $false
    }
}
catch {
    Write-Host " ❌ Cannot check Vault" -ForegroundColor Red
    $tests += $false
}

# Test Results Summary
Write-Host ""
Write-Host "📊 Test Results Summary" -ForegroundColor Blue
Write-Host "────────────────────────" -ForegroundColor DarkGray

$passed = ($tests | Where-Object { $_ -eq $true }).Count
$total = $tests.Count
$percentage = [math]::Round(($passed / $total) * 100, 1)

Write-Host "Tests Passed: $passed/$total ($percentage%)" -ForegroundColor $(if ($percentage -gt 80) { "Green" } elseif ($percentage -gt 60) { "Yellow" } else { "Red" })

if ($percentage -eq 100) {
    Write-Host ""
    Write-Host "🎉 All tests passed! Authentication flow is ready." -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Open https://ebank.local in your browser"
    Write-Host "2. Click 'Login' to test the OIDC flow"
    Write-Host "3. Use test credentials: admin/admin"
    Write-Host "4. Verify JWT token is included in API calls"
} elseif ($percentage -gt 80) {
    Write-Host ""
    Write-Host "✅ Most tests passed. Minor issues detected." -ForegroundColor Yellow
    Write-Host "Review failed tests above and check logs." -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "❌ Multiple tests failed. Check deployment status." -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting commands:" -ForegroundColor Cyan
    Write-Host "  kubectl get pods -A"
    Write-Host "  kubectl get ingress -A"
    Write-Host "  kubectl logs -n svc deployment/api-gateway"
    Write-Host "  kubectl logs -n security deployment/keycloak"
}

Write-Host ""
Write-Host "💡 Tips:" -ForegroundColor Yellow
Write-Host "• Use -Verbose for detailed output"
Write-Host "• Check browser console for frontend authentication issues"
Write-Host "• Verify /etc/hosts contains: 127.0.0.1 ebank.local"
