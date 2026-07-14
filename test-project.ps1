# E-Banking Test Script
# Tests the complete project functionality

Write-Host "🧪 E-Banking Project Testing..." -ForegroundColor Green

# Test 1: Check if all services are running
Write-Host "`n📡 Testing Service Health..." -ForegroundColor Yellow

$services = @(
    @{Name="Frontend"; Url="http://localhost:4200"},
    @{Name="API Gateway"; Url="https://localhost:5000/health"},
    @{Name="Account Service"; Url="https://localhost:5001/health"},
    @{Name="Transfer Service"; Url="https://localhost:5002/health"},
    @{Name="Payment Service"; Url="https://localhost:5003/health"},
    @{Name="Notification Service"; Url="https://localhost:5004/health"},
    @{Name="Audit Service"; Url="https://localhost:5005/health"}
)

foreach ($service in $services) {
    try {
        $response = Invoke-WebRequest -Uri $service.Url -TimeoutSec 5 -SkipCertificateCheck -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "✅ $($service.Name) - OK" -ForegroundColor Green
        } else {
            Write-Host "⚠️  $($service.Name) - Status: $($response.StatusCode)" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "❌ $($service.Name) - Not responding" -ForegroundColor Red
    }
}

# Test 2: Authentication Flow Test
Write-Host "`n🔐 Testing Authentication Flow..." -ForegroundColor Yellow

try {
    # Test if Keycloak is accessible (if running in K8s mode)
    $keycloakResponse = Invoke-WebRequest -Uri "http://localhost:8081" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "✅ Keycloak accessible" -ForegroundColor Green
}
catch {
    Write-Host "⚠️  Keycloak not accessible (may be running in dev mode)" -ForegroundColor Yellow
}

# Test 3: Frontend Build Test
Write-Host "`n🏗️  Testing Frontend Build..." -ForegroundColor Yellow
cd "Z:\ebanking\frontend\web"

try {
    $buildResult = npm run build 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Frontend builds successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Frontend build failed" -ForegroundColor Red
        Write-Host $buildResult
    }
}
catch {
    Write-Host "❌ Frontend build error" -ForegroundColor Red
}

# Test 4: API Gateway Routes Test
Write-Host "`n🛣️  Testing API Gateway Routes..." -ForegroundColor Yellow

$routes = @(
    "/api/account/health",
    "/api/transfer/health", 
    "/api/payment/health",
    "/api/notification/health",
    "/api/audit/health"
)

foreach ($route in $routes) {
    try {
        $response = Invoke-WebRequest -Uri "https://localhost:5000$route" -TimeoutSec 5 -SkipCertificateCheck -ErrorAction Stop
        Write-Host "✅ Route $route - OK" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Route $route - Failed" -ForegroundColor Red
    }
}

# Test 5: Security Headers Test
Write-Host "`n🛡️  Testing Security Headers..." -ForegroundColor Yellow

try {
    $response = Invoke-WebRequest -Uri "http://localhost:4200" -TimeoutSec 5 -ErrorAction Stop
    $headers = $response.Headers
    
    if ($headers.'Content-Security-Policy') {
        Write-Host "✅ CSP header present" -ForegroundColor Green
    } else {
        Write-Host "⚠️  CSP header missing" -ForegroundColor Yellow
    }
    
    if ($headers.'X-Frame-Options') {
        Write-Host "✅ X-Frame-Options header present" -ForegroundColor Green
    } else {
        Write-Host "⚠️  X-Frame-Options header missing" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "❌ Could not test frontend security headers" -ForegroundColor Red
}

Write-Host "`n📊 Test Summary Complete!" -ForegroundColor Green
Write-Host "Check the results above for any issues." -ForegroundColor Cyan

# Interactive testing menu
Write-Host "`n🎯 Manual Testing Options:"
Write-Host "1. Open Frontend in Browser"
Write-Host "2. Open API Gateway Swagger"
Write-Host "3. Open Keycloak Admin"
Write-Host "4. View Service Logs"
Write-Host "5. Exit"

$choice = Read-Host "Enter choice (1-5)"

switch ($choice) {
    "1" { Start-Process "http://localhost:4200" }
    "2" { Start-Process "https://localhost:5000/swagger" }
    "3" { Start-Process "http://localhost:8081" }
    "4" { 
        Write-Host "Check the PowerShell windows running the services for logs."
        Write-Host "Or run: docker logs [container_name] if using containers."
    }
    "5" { Write-Host "Goodbye!" -ForegroundColor Green }
    default { Write-Host "Invalid choice." -ForegroundColor Red }
}
