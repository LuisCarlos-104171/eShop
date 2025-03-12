# eShop - Microservices Reference Application

eShop is a modern cloud-native reference application demonstrating a microservices architecture design pattern with .NET technologies.

## Architecture Overview

eShop is built as a container-based application leveraging microservices architectural patterns. It includes:

- Multiple backend microservices (Basket.API, Catalog.API, etc.)
- A cross-platform client application built with .NET MAUI
- Event-driven communication between services using RabbitMQ
- Container orchestration readiness
- Integrated observability with OpenTelemetry
- Data persistence with Redis (Basket) and PostgreSQL (Catalog)

## Building and Running the Application

To build and run the eShop application with all its components:

```bash
dotnet run --project src/eShop.AppHost/eShop.AppHost.csproj
```

This command will:
- Build all necessary projects
- Start all microservices
- Set up required infrastructure components
- Configure networking and dependencies

## Infrastructure Components

The application includes the following infrastructure components:

- **PostgreSQL**: For persistent data storage
- **Redis**: For distributed caching and basket storage
- **RabbitMQ**: For event-driven messaging between services
- **Jaeger**: For distributed tracing
- **Prometheus**: For metrics collection
- **Grafana**: For monitoring and visualization

## Monitoring and Observability

### OpenTelemetry Integration

The application comes with built-in OpenTelemetry instrumentation:

- Distributed tracing across all services
- Metrics collection for performance monitoring
- Logging integration

### Accessing Monitoring Tools

**Grafana Dashboard**:
- URL: [http://localhost:3001](http://localhost:3001)
- Credentials: 
  - Username: `admin` 
  - Password: `admin`

**Jaeger Distributed Tracing**:
- URL: [http://localhost:16687](http://localhost:16687)

## Microservices

The application consists of the following microservices:

- **Basket API**: Shopping basket management
- **Catalog API**: Product catalog management
- **ClientApp**: Cross-platform mobile and desktop client application

Each microservice is independently deployable and follows domain-driven design principles.

## Development Environment Setup

### Prerequisites

- .NET 9.0 SDK or later
- Docker Desktop
- Visual Studio 2022 or Visual Studio Code

### Running in Development Mode

For local development, you can also run individual services using:

```bash
dotnet run --project src/{ServiceName}/{ServiceName}.csproj
```

## API Documentation

API documentation is automatically generated using OpenAPI/Swagger. When running the application, you can access the Swagger UI for each service at:

```
http://{service-host}:{service-port}/swagger
```

## Contributing

Please see [CONTRIBUTING.md](CONTRIBUTING.md) for details on contributing to this project.

## License

This project is licensed under the terms specified in the [LICENSE](LICENSE) file.
