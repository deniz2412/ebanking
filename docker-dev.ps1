param(
    [Parameter(Position=0)]
    [string]$Command,
    [Parameter(Position=1)]
    [string]$Service
)

# EBanking Docker Development Helper Script

switch ($Command) {
    "start" {
        Write-Host "Starting EBanking development environment..." -ForegroundColor Green
        docker-compose up -d
        Write-Host "`nServices started. Access points:" -ForegroundColor Green
        Write-Host "- Kafka Console: http://localhost:8080" -ForegroundColor Yellow
        Write-Host "- Keycloak Admin: http://localhost:8180" -ForegroundColor Yellow
        Write-Host "- MSSQL Server: localhost:1433" -ForegroundColor Yellow
    }
    "stop" {
        Write-Host "Stopping EBanking development environment..." -ForegroundColor Yellow
        docker-compose down
    }
    "restart" {
        Write-Host "Restarting EBanking development environment..." -ForegroundColor Yellow
        docker-compose restart
    }
    "logs" {
        if ([string]::IsNullOrEmpty($Service)) {
            docker-compose logs -f
        } else {
            docker-compose logs -f $Service
        }
    }
    "status" {
        docker-compose ps
    }
    "clean" {
        Write-Host "WARNING: This will remove all data!" -ForegroundColor Red
        $confirmation = Read-Host "Are you sure? (y/N)"
        if ($confirmation -eq 'y' -or $confirmation -eq 'Y') {
            docker-compose down -v
            docker system prune -f
        }
    }
    "build" {
        docker-compose build
    }
    default {
        Write-Host "EBanking Docker Development Helper" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Usage: .\docker-dev.ps1 {start|stop|restart|logs|status|clean|build}" -ForegroundColor White
        Write-Host ""
        Write-Host "Commands:" -ForegroundColor Green
        Write-Host "  start   - Start all services"
        Write-Host "  stop    - Stop all services"
        Write-Host "  restart - Restart all services"
        Write-Host "  logs    - View logs (optionally specify service name)"
        Write-Host "  status  - Show service status"
        Write-Host "  clean   - Stop services and remove all data (WARNING!)"
        Write-Host "  build   - Rebuild all services"
        Write-Host ""
        Write-Host "Examples:" -ForegroundColor Yellow
        Write-Host "  .\docker-dev.ps1 start"
        Write-Host "  .\docker-dev.ps1 logs mssql"
        Write-Host "  .\docker-dev.ps1 status"
    }
}