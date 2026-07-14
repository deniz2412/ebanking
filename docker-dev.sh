#!/bin/bash

# EBanking Docker Development Helper Script

case "$1" in
    "start")
        echo "Starting EBanking development environment..."
        docker-compose up -d
        echo "Services started. Access points:"
        echo "- Kafka Console: http://localhost:8080"
        echo "- Keycloak Admin: http://localhost:8180"
        echo "- MSSQL Server: localhost:1433"
        ;;
    "stop")
        echo "Stopping EBanking development environment..."
        docker-compose down
        ;;
    "restart")
        echo "Restarting EBanking development environment..."
        docker-compose restart
        ;;
    "logs")
        if [ -z "$2" ]; then
            docker-compose logs -f
        else
            docker-compose logs -f "$2"
        fi
        ;;
    "status")
        docker-compose ps
        ;;
    "clean")
        echo "WARNING: This will remove all data!"
        read -p "Are you sure? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            docker-compose down -v
            docker system prune -f
        fi
        ;;
    "build")
        docker-compose build
        ;;
    *)
        echo "EBanking Docker Development Helper"
        echo ""
        echo "Usage: $0 {start|stop|restart|logs|status|clean|build}"
        echo ""
        echo "Commands:"
        echo "  start   - Start all services"
        echo "  stop    - Stop all services"
        echo "  restart - Restart all services"
        echo "  logs    - View logs (optionally specify service name)"
        echo "  status  - Show service status"
        echo "  clean   - Stop services and remove all data (WARNING!)"
        echo "  build   - Rebuild all services"
        echo ""
        echo "Examples:"
        echo "  $0 start"
        echo "  $0 logs mssql"
        echo "  $0 status"
        ;;
esac