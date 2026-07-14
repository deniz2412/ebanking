#!/usr/bin/env pwsh
# E-Banking Development Startup Script
# Run this script to start the development environment

Write-Host "🏦 E-Banking Development Environment Startup" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Check if we're in the correct directory
if (!(Test-Path ".\frontend\web\package.json")) {
    Write-Host "❌ Error: Please run this script from the ebanking root directory" -ForegroundColor Red
    exit 1
}

Write-Host "🔍 Checking prerequisites..." -ForegroundColor Yellow

# Check Node.js
try {
    $nodeVersion = node --version
    Write-Host "✅ Node.js: $nodeVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Node.js not found. Please install Node.js 18+" -ForegroundColor Red
    exit 1
}

# Check Angular CLI
try {
    $ngVersion = ng version --skip-confirmation 2>$null | Select-String "Angular CLI" | Select-Object -First 1
    Write-Host "✅ Angular CLI found" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Angular CLI not found. Installing..." -ForegroundColor Yellow
    npm install -g @angular/cli@latest
}

Write-Host "`n📦 Installing frontend dependencies..." -ForegroundColor Yellow
Set-Location "frontend\web"

if (!(Test-Path "node_modules")) {
    npm install
} else {
    Write-Host "✅ Dependencies already installed" -ForegroundColor Green
}

Write-Host "`n🚀 Starting Angular development server..." -ForegroundColor Yellow
Write-Host "🌐 Frontend will be available at: http://localhost:4200" -ForegroundColor Cyan
Write-Host "🔐 Development mode: Authentication will be simulated locally" -ForegroundColor Cyan
Write-Host "`nPress Ctrl+C to stop the server" -ForegroundColor Yellow

# Start the development server
ng serve --port 4200 --open

Write-Host "`n👋 Development server stopped" -ForegroundColor Yellow
Set-Location "..\..\"
