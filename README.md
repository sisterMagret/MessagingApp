# MessagingApp

A modern, feature-rich real-time messaging application built with .NET 9, SignalR, and SQL Server. This application supports group chat, file sharing, voice messages, subscription-based premium features, and payment processing.

## Features

### Core Messaging

- **Real-time Messaging**: Instant messaging using SignalR
- **Group Chat**: Create and manage group conversations with role-based permissions
- **Private Messages**: One-to-one messaging between users
- **Message History**: Persistent message storage with read status tracking
- **Message Types**: Text, file attachments, and voice messages

### User Management

- **JWT Authentication**: Secure token-based authentication
- **User Registration & Login**: Email-based account system
- **Profile Management**: User profile with activity tracking
- **Role-based Authorization**: Group owners, members with different permissions

### Premium Features (Subscription-based)

- **Voice Messages**: Record and send voice messages (Premium)
- **File Sharing**: Upload and share files with size limits (Premium)
- **Group Chat**: Create unlimited groups (Premium)
- **Email Alerts**: Receive notifications via email (Premium)

### Subscription System

- **Feature-based Subscriptions**: Individual feature subscriptions
- **Payment Integration**: Secure payment processing
- **Flexible Pricing**: Monthly subscription model
  - Voice Messages: $2.99/month
  - File Sharing: $4.99/month  
  - Group Chat: $9.99/month
  - Email Alerts: $1.99/month

### Technical Features

- **RESTful API**: Comprehensive REST API with Swagger documentation
- **Real-time Updates**: SignalR hubs for live messaging
- **Database Persistence**: SQL Server with Entity Framework Core
- **File Storage**: Local file system with URL-based access
- **Background Services**: Email alert processing
- **Docker Support**: Complete containerization with Docker Compose
- **Health Monitoring**: Container health checks and monitoring

## Architecture

### Clean Architecture Pattern

```
┌─────────────────────────────────────────────────────────┐
│                        API Layer                        │
│  Controllers, Hubs, Middleware, Configuration          │
├─────────────────────────────────────────────────────────┤
│                     Core Layer                          │
│     Entities, DTOs, Interfaces, Enums                  │
├─────────────────────────────────────────────────────────┤
│                Infrastructure Layer                     │
│   Services, Data Access, Email, Workers                │
├─────────────────────────────────────────────────────────┤
│                      Database                           │
│              SQL Server 2022                           │
└─────────────────────────────────────────────────────────┘
```

### Project Structure

```
MessagingApp/
├── src/
│   ├── Api/                          # API Layer
│   │   ├── Controllers/             # REST API Controllers
│   │   ├── Hubs/                    # SignalR Hubs
│   │   ├── Services/                # API-specific Services
│   │   ├── wwwroot/                 # Static Files & SPA
│   │   └── Program.cs               # Application Entry Point
│   │
│   ├── Core/                        # Domain Layer
│   │   ├── Entities/                # Domain Models
│   │   ├── Dtos/                    # Data Transfer Objects
│   │   ├── Interfaces/              # Service Contracts
│   │   ├── Enums/                   # Domain Enumerations
│   │   └── Contracts/               # Interface Definitions
│   │
│   ├── Infrastructure/              # Infrastructure Layer
│   │   ├── Data/                    # Database Context & Config
│   │   ├── Services/                # Business Logic Services
│   │   ├── Email/                   # Email & Background Services
│   │   └── Migrations/              # EF Core Migrations
│   │
│   └── Tests/                       # Test Projects
│       ├── Integration/             # Integration Tests
│       └── Services/                # Unit Tests
│
├── docker-compose.yml              # Container Orchestration
├── Dockerfile                      # API Container Definition
├── docker-start.sh                # Docker Management Script
└── README.md                      # Project Documentation
```

### Technology Stack

- **Backend**: .NET 9, ASP.NET Core, Entity Framework Core
- **Database**: Microsoft SQL Server 2022
- **Real-time**: SignalR for WebSocket connections
- **Authentication**: JWT Bearer tokens
- **API Documentation**: Swagger/OpenAPI 3.0
- **Containerization**: Docker & Docker Compose
- **Testing**: xUnit, Integration tests
- **Frontend**: Vanilla JavaScript SPA

## Prerequisites

### Development Environment

- **.NET 9 SDK** (for local development)
- **Docker Desktop** (for containerized deployment)
- **SQL Server** (if running without Docker)
- **Visual Studio Code** or **Visual Studio 2022**

### Platform Requirements

- **Windows**: Windows 10/11 with WSL2 for Docker
- **macOS**: macOS 10.15+ with Docker Desktop
- **Linux**: Ubuntu 18.04+ or equivalent with Docker

## Installation & Setup

### Option 1: Docker Deployment (Recommended)

#### 1. Clone the Repository

```bash
git clone https://github.com/sisterMagret/MessagingApp.git
cd MessagingApp
```

#### 2. Start with Docker Compose

```bash
# Start all services
./docker-start.sh start

# Or manually
docker-compose up -d
```

#### 3. Verify Installation

- **API**: <http://localhost:5250>
- **Swagger UI**: <http://localhost:5250/swagger>
- **SQL Server**: localhost:1433 (sa/MessagingApp123!)

### Option 2: Local Development Setup

#### 1. Install Dependencies

```bash
# Restore NuGet packages
dotnet restore

# Install Entity Framework tools
dotnet tool install --global dotnet-ef
```

#### 2. Configure Database

```bash
# Update connection string in src/Api/appsettings.json
# Run migrations
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

#### 3. Run Application

```bash
# Development mode
dotnet run --project src/Api

# Or with hot reload
dotnet watch --project src/Api
```

## Docker Commands

### Quick Start Script

```bash
# Start all services
./docker-start.sh start

# Stop all services
./docker-start.sh stop

# View logs
./docker-start.sh logs

# Rebuild and restart
./docker-start.sh rebuild

# Check status
./docker-start.sh status

# SQL Server only
./docker-start.sh sqlserver-only
```

### Manual Docker Commands

```bash
# Build and start
docker-compose up -d

# Stop services
docker-compose down

# View logs
docker-compose logs -f

# Rebuild
docker-compose build --no-cache
```

## Configuration

### Environment Variables

```bash
# .env.development
ASPNETCORE_ENVIRONMENT=Development
SA_PASSWORD=MessagingApp123!
DB_SERVER=sqlserver
DB_PORT=1433
DB_NAME=MessagingAppDb
```

### Application Settings

Key configuration files:

- `src/Api/appsettings.json` - Main configuration
- `src/Api/appsettings.Development.json` - Development overrides
- `docker-compose.yml` - Container configuration

### Database Connection

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MessagingAppDb;User Id=sa;Password=MessagingApp123!;TrustServerCertificate=True;Encrypt=False;Connection Timeout=30;"
  }
}
```

## Testing

### Demo Users

The application seeds with demo data:

- **Alice**: <alice@example.com> / Demo@123 (Group Chat subscription)
- **Bob**: <bob@example.com> / Demo@123 (File Sharing subscription)  
- **Charlie**: <charlie@example.com> / Demo@123 (No subscriptions)
- **Sister Magret**: <sistermagret@gmail.com> / adminpassword (Admin user)

### API Testing

```bash
# Login test
curl -X POST -H "Content-Type: application/json" \
  -d '{"email":"alice@example.com","password":"Demo@123"}' \
  http://localhost:5250/api/auth/login

# Send message
curl -X POST -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{"receiverId":2,"content":"Hello Bob!"}' \
  http://localhost:5250/api/messages

# Upload file (Premium feature)
curl -X POST -H "Authorization: Bearer YOUR_TOKEN" \
  -F "file=@test.txt" \
  http://localhost:5250/api/files/upload
```

### Running Unit Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test src/Tests/
```

## API Documentation

### Authentication Endpoints

- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - User login

### Messaging Endpoints

- `GET /api/messages` - Get user messages
- `POST /api/messages` - Send message
- `PUT /api/messages/{id}/read` - Mark as read

### Group Chat Endpoints

- `GET /api/groups` - Get user groups
- `POST /api/groups` - Create group (Premium)
- `POST /api/groups/{id}/members` - Add member
- `DELETE /api/groups/{id}/members/{userId}` - Remove member (Owner only)
- `DELETE /api/groups/{id}` - Delete group (Owner only)

### File Upload Endpoints

- `POST /api/files/upload` - Upload file (Premium)
- `GET /files/{filename}` - Access uploaded file

### Payment Endpoints

- `GET /api/payments/features` - Get available features
- `POST /api/payments/purchase` - Purchase subscription

### Subscription Endpoints

- `GET /api/subscriptions` - Get user subscriptions
- `POST /api/subscriptions/check` - Check feature access

## SignalR Hubs

### Message Hub (`/messageHub`)

- `SendMessage` - Send real-time message
- `JoinGroup` - Join group for notifications
- `LeaveGroup` - Leave group notifications

### Events

- `ReceiveMessage` - New message received
- `MessageRead` - Message read notification
- `UserJoined` - User joined group
- `UserLeft` - User left group

## Database Schema

### Core Tables

- **Users**: User accounts and profiles
- **Messages**: All messages (private and group)
- **Groups**: Group chat information
- **GroupMembers**: Group membership with roles
- **Subscriptions**: User feature subscriptions

### Key Relationships

- Users can have multiple Messages (sender/receiver)
- Groups have multiple Members with roles (Owner/Member)
- Users can have multiple Subscriptions for different features
- Messages can belong to Groups or be private between Users

## Security Features

### Authentication & Authorization

- JWT Bearer token authentication
- Role-based access control for groups
- Subscription-based feature gating
- Secure password hashing (ASP.NET Identity)

### Data Protection

- SQL injection prevention (Entity Framework)
- XSS protection (built-in ASP.NET Core)
- CORS configuration for API access
- File upload validation and limits

### Container Security

- Non-root container execution
- Isolated container networking
- Secure environment variable handling
- Regular security updates for base images


### Environment-Specific Configurations

```bash
# Production
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection="Production connection string"

# Staging  
ASPNETCORE_ENVIRONMENT=Staging

# Development
ASPNETCORE_ENVIRONMENT=Development
```