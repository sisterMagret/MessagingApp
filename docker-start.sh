#!/bin/bash

echo "Starting MessagingApp with SQL Server...."

# Function to check if Docker is running
check_docker() {
    if ! docker info > /dev/null 2>&1; then
        echo "Docker is not running. Please start Docker Desktop and try again."
        exit 1
    fi
    echo "Docker is running"
}

# Function to start services
start_services() {
    echo "Starting Docker Compose services..."
    
    # Start SQL Server first
    echo "Starting SQL Server..."
    docker-compose up -d sqlserver
    
    # Wait for SQL Server to be ready
    echo "Waiting for SQL Server to be ready..."
    sleep 30
    
    # Check SQL Server health
    docker-compose exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P MessagingApp123! -Q "SELECT 1" > /dev/null 2>&1
    
    if [ $? -eq 0 ]; then
        echo "SQL Server is ready!"
    else
        echo "SQL Server may still be starting up..."
    fi
    
    # Start the API
    echo "Starting MessagingApp API..."
    docker-compose up -d messagingapp-api
    
    echo ""
    echo "Services started successfully!"
    echo "SQL Server: localhost:1433"
    echo "API: http://localhost:5250"
    echo "Swagger UI: http://localhost:5250/swagger"
    echo ""
    echo "To view logs: docker-compose logs -f"
    echo "To stop services: docker-compose down"
}

# Function to stop services
stop_services() {
    echo "Stopping Docker Compose services..."
    docker-compose down
    echo "Services stopped"
}

# Function to rebuild and start
rebuild_services() {
    echo "Rebuilding and starting services..."
    docker-compose down
    docker-compose build --no-cache
    start_services
}

# Function to show logs
show_logs() {
    echo "Showing service logs..."
    docker-compose logs -f
}

# Main script logic
case "$1" in
    start)
        check_docker
        start_services
        ;;
    stop)
        stop_services
        ;;
    restart)
        check_docker
        stop_services
        start_services
        ;;
    rebuild)
        check_docker
        rebuild_services
        ;;
    logs)
        show_logs
        ;;
    status)
        docker-compose ps
        ;;
    sqlserver-only)
        check_docker
        echo "Starting SQL Server only..."
        docker-compose up -d sqlserver
        ;;
    *)
        echo "Usage: $0 {start|stop|restart|rebuild|logs|status|sqlserver-only}"
        echo ""
        echo "Commands:"
        echo "  start         - Start all services"
        echo "  stop          - Stop all services"
        echo "  restart       - Restart all services"
        echo "  rebuild       - Rebuild and start services"
        echo "  logs          - Show service logs"
        echo "  status        - Show service status"
        echo "  sqlserver-only - Start only SQL Server"
        exit 1
        ;;
esac