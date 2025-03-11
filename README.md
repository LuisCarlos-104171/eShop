# eShop Application

This README provides instructions for building, running, and monitoring the eShop application with OpenTelemetry integration and Grafana dashboards.

## Building and Running the eShop Environment

The eShop application can be built and run with a single command:

```bash
dotnet run --project src/eShop.AppHost/eShop.AppHost.csproj
```

This command will:
- Build all necessary projects
- Configure and start all microservices
- Set up the required infrastructure components
- Launch the application

## OpenTelemetry Configuration

The eShop application comes pre-configured with OpenTelemetry collectors and exporters to monitor application performance and behavior.

### OpenTelemetry Collectors/Exporters

The OpenTelemetry components are automatically launched when you start the application. No additional configuration is needed for basic usage.

If you need to modify the OpenTelemetry configuration:

1. The collector configurations can be found in the appropriate configuration files within the project
2. By default, telemetry data is exported to Jaeger for distributed tracing

## Grafana Dashboard

The eShop application includes a pre-configured Grafana dashboard for visualizing monitoring data.

### Accessing Grafana

Once the application is running:

1. Open your browser and navigate to: [http://localhost:3001](http://localhost:3001)
2. Log in with the following credentials:
   - Username: `admin`
   - Password: `admin`
3. The dashboard should be available in the Dashboards section

### Jaeger for Distributed Tracing

The application also includes Jaeger for distributed tracing:

1. Open your browser and navigate to: [http://localhost:16687](http://localhost:16687)
2. Use the interface to explore traces across the different microservices
