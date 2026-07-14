# E-Banking Project Execution Script
# This script runs the complete project for testing

Write-Host "🏦 Starting E-Banking Project..." -ForegroundColor Green

# Check prerequisites
Write-Host "`n📋 Checking Prerequisites..." -ForegroundColor Yellow

if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "❌ .NET SDK is not installed!" -ForegroundColor Red
    Write-Host "Please install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
}

if (!(Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Node.js is not installed!" -ForegroundColor Red
    exit 1
}

if (!(Get-Command kubectl -ErrorAction SilentlyContinue)) {
    Write-Host "❌ kubectl is not installed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ All prerequisites are installed" -ForegroundColor Green

# Option 1: Development Mode (Local)
Write-Host "`n🚀 Choose execution mode:"
Write-Host "1. Development Mode (Local services + Frontend)"
Write-Host "2. Kubernetes Mode (Full infrastructure)"
Write-Host "3. Frontend Only"

$choice = Read-Host "Enter choice (1-3)"

switch ($choice) {
    "1" {
        Write-Host "`n🔧 Starting Development Mode..." -ForegroundColor Yellow
        
        # Start services in background
        Write-Host "Starting API Gateway..."
        Start-Process powershell -ArgumentList "-Command", "cd 'Z:\ebanking\services\gateway-ocelot'; dotnet run --urls 'https://localhost:5000'" -WindowStyle Minimized
        
        Start-Sleep 3
        
        Write-Host "Starting Account Service..."
        Start-Process powershell -ArgumentList "-Command", "cd 'Z:\ebanking\services\account'; dotnet run --urls 'https://localhost:5001'" -WindowStyle Minimized
        
        Start-Sleep 2
        
        Write-Host "Starting Transfer Service..."
        Start-Process powershell -ArgumentList "-Command", "cd 'Z:\ebanking\services\transfer'; dotnet run --urls 'https://localhost:5002'" -WindowStyle Minimized
        
        Start-Sleep 2
        
        Write-Host "Starting Payment Service..."
        Start-Process powershell -ArgumentList "-Command", "cd 'Z:\ebanking\services\payment'; dotnet run --urls 'https://localhost:5003'" -WindowStyle Minimized
        
        Start-Sleep 2
        
        Write-Host "Starting Notification Service..."
        Start-Process powershell -ArgumentList "-Command", "cd 'Z:\ebanking\services\notification'; dotnet run --urls 'https://localhost:5004'" -WindowStyle Minimized
        
        Start-Sleep 2
        
        Write-Host "Starting Audit Service..."
        Start-Process powershell -ArgumentList "-Command", "cd 'Z:\ebanking\services\audit'; dotnet run --urls 'https://localhost:5005'" -WindowStyle Minimized
        
        Start-Sleep 3
        
        Write-Host "Starting Angular Frontend..."
        Start-Process powershell -ArgumentList "-Command", "cd 'Z:\ebanking\frontend\web'; npm start" -WindowStyle Minimized
        
        Write-Host "`n🎉 Development environment started!" -ForegroundColor Green
        Write-Host "`n📍 Access Points:"
        Write-Host "Frontend: http://localhost:4200" -ForegroundColor Cyan
        Write-Host "API Gateway: https://localhost:5000" -ForegroundColor Cyan
        Write-Host "Account Service: https://localhost:5001/swagger" -ForegroundColor Cyan
        Write-Host "Transfer Service: https://localhost:5002/swagger" -ForegroundColor Cyan
        Write-Host "Payment Service: https://localhost:5003/swagger" -ForegroundColor Cyan
        Write-Host "Notification Service: https://localhost:5004/swagger" -ForegroundColor Cyan
        Write-Host "Audit Service: https://localhost:5005/swagger" -ForegroundColor Cyan
        
        Write-Host "`n⚠️  Note: Services starting in background windows. Check for any errors."
        Write-Host "Press any key to stop all services..." -ForegroundColor Yellow
        Read-Host
        
        # Stop all processes
        Write-Host "Stopping all services..." -ForegroundColor Yellow
        Get-Process | Where-Object {$_.ProcessName -eq "dotnet" -or $_.ProcessName -eq "node"} | Stop-Process -Force
    }
    
    "2" {
        Write-Host "`n☸️  Starting Kubernetes Mode..." -ForegroundColor Yellow
        Write-Host "This will deploy the full infrastructure to Kubernetes cluster."
        
        # Check if kubectl is working
        kubectl get namespaces 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Kubernetes cluster not available!" -ForegroundColor Red
            Write-Host "Please ensure Docker Desktop Kubernetes is running."
            exit 1
        }
        
        Write-Host "Deploying infrastructure..."
        kubectl apply -f "Z:\ebanking\infrastructure\k8s\base\namespaces.yaml"
        kubectl apply -f "Z:\ebanking\infrastructure\k8s\keycloak\keycloak.yaml"
        kubectl apply -f "Z:\ebanking\infrastructure\k8s\mssql\mssql.yaml"
        kubectl apply -f "Z:\ebanking\infrastructure\k8s\kafka\redpanda.yaml"
        kubectl apply -f "Z:\ebanking\infrastructure\k8s\vault\"
        kubectl apply -f "Z:\ebanking\infrastructure\k8s\gateway\api-gateway.yaml"
        
        Write-Host "Waiting for services to start..."
        Start-Sleep 30
        
        Write-Host "Checking pod status..."
        kubectl get pods -A
        
        Write-Host "`n🎯 Port forwarding for local access..."
        Write-Host "Starting port forwards in background..."
        
        Start-Process powershell -ArgumentList "-Command", "kubectl port-forward -n security svc/keycloak 8081:8080" -WindowStyle Minimized
        Start-Process powershell -ArgumentList "-Command", "kubectl port-forward -n svc svc/api-gateway 8080:80" -WindowStyle Minimized
        
        Write-Host "`n📍 Access Points:"
        Write-Host "Keycloak: http://localhost:8081" -ForegroundColor Cyan
        Write-Host "API Gateway: http://localhost:8080" -ForegroundColor Cyan
        
        # Still start frontend locally
        Write-Host "Starting Angular Frontend..."
        Start-Process powershell -ArgumentList "-Command", "cd 'Z:\ebanking\frontend\web'; npm start" -WindowStyle Minimized
        Write-Host "Frontend: http://localhost:4200" -ForegroundColor Cyan
    }
    
    "3" {
        Write-Host "`n🌐 Starting Frontend Only..." -ForegroundColor Yellow
        cd "Z:\ebanking\frontend\web"
        npm start
    }
    
    default {
        Write-Host "Invalid choice. Exiting." -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n✨ Project execution complete!" -ForegroundColor Green
