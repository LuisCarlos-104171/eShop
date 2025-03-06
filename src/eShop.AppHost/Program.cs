using eShop.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddForwardedHeaders();

var redis = builder.AddRedis("redis");
var rabbitMq = builder.AddRabbitMQ("eventbus")
    .WithLifetime(ContainerLifetime.Persistent);
var postgres = builder.AddPostgres("postgres")
    .WithImage("ankane/pgvector")
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent);

// Add Jaeger for distributed tracing with unique endpoint names
// Fixed container image reference and using different port mappings
var jaeger = builder.AddContainer("jaeger", "jaegertracing/jaeger", "2.3.0")
    .WithArgs("--config", "/etc/jaeger/config.yml")
    .WithBindMount("../../deploy/jaeger", "/etc/jaeger")
    .WithEndpoint(16687, 16686, name: "jaeger-ui")         // UI (changed host port)
    .WithEndpoint(14251, 14250, name: "jaeger-model")      // Model (changed host port)
    .WithEndpoint(14269, 14268, name: "jaeger-collector")  // Collector HTTP (changed host port)
    .WithEndpoint(6832, 6831, scheme: "udp", name: "jaeger-agent"); // Agent (changed host port)

// Add OpenTelemetry collector
var otel = builder.AddContainer("otel-collector", "otel/opentelemetry-collector-contrib", "0.120.0")
    .WithBindMount("../../deploy/otel-collector.yaml", "/etc/otel-collector.yaml")
    .WithArgs("--config", "/etc/otel-collector.yaml")
    .WithEndpoint(4319, 4317, name: "otlp-grpc")       // OTLP gRPC (changed host port)
    .WithEndpoint(4320, 4318, name: "otlp-http")       // OTLP HTTP (changed host port)
    .WithEndpoint(8890, 8889, name: "prometheus-port"); // Prometheus metrics (changed host port)

// Add Prometheus for metrics
var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "v2.47.2")
    .WithBindMount("../../deploy/prometheus", "/etc/prometheus")
    .WithEndpoint(9091, 9090, name: "prometheus-ui"); // Changed host port

// Add Grafana for visualization
var grafana = builder.AddContainer("grafana", "grafana/grafana-oss")
    .WithBindMount("../../deploy/grafana", "/etc/grafana/provisioning")
    .WithEndpoint(3001, 3000, name: "grafana-ui"); // Changed host port

var catalogDb = postgres.AddDatabase("catalogdb");
var identityDb = postgres.AddDatabase("identitydb");
var orderDb = postgres.AddDatabase("orderingdb");
var webhooksDb = postgres.AddDatabase("webhooksdb");

var launchProfileName = ShouldUseHttpForEndpoints() ? "http" : "https";

// Set OpenTelemetry environment variables - update to new port
var otelExporterOtlpEndpoint = "http://localhost:4319";

// Services
var identityApi = builder.AddProject<Projects.Identity_API>("identity-api", launchProfileName)
    .WithExternalHttpEndpoints()
    .WithEnvironment(callback => callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otelExporterOtlpEndpoint)
    .WithReference(identityDb);

var identityEndpoint = identityApi.GetEndpoint(launchProfileName);

var basketApi = builder.AddProject<Projects.Basket_API>("basket-api")
    .WithReference(redis)
    .WithEnvironment(callback => callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otelExporterOtlpEndpoint)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithEnvironment("Identity__Url", identityEndpoint);

var catalogApi = builder.AddProject<Projects.Catalog_API>("catalog-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithEnvironment(callback => callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otelExporterOtlpEndpoint)
    .WithReference(catalogDb);

var orderingApi = builder.AddProject<Projects.Ordering_API>("ordering-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(orderDb).WaitFor(orderDb)
    .WithEnvironment(callback => callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otelExporterOtlpEndpoint)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Identity__Url", identityEndpoint);

builder.AddProject<Projects.OrderProcessor>("order-processor")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(orderDb)
    .WithEnvironment(callback => callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otelExporterOtlpEndpoint)
    .WaitFor(orderingApi); // wait for the orderingApi to be ready because that contains the EF migrations

builder.AddProject<Projects.PaymentProcessor>("payment-processor")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithEnvironment(callback => callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otelExporterOtlpEndpoint);

var webHooksApi = builder.AddProject<Projects.Webhooks_API>("webhooks-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(webhooksDb)
    .WithEnvironment(callback => callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otelExporterOtlpEndpoint)
    .WithEnvironment("Identity__Url", identityEndpoint);

// Reverse proxies
builder.AddProject<Projects.Mobile_Bff_Shopping>("mobile-bff")
    .WithReference(catalogApi)
    .WithReference(orderingApi)
    .WithReference(basketApi)
    .WithReference(identityApi)
    .WithEnvironment(callback => callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otelExporterOtlpEndpoint);

// Apps
var webhooksClient = builder.AddProject<Projects.WebhookClient>("webhooksclient", launchProfileName)
    .WithReference(webHooksApi)
    .WithEnvironment(callback => callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otelExporterOtlpEndpoint)
    .WithEnvironment("IdentityUrl", identityEndpoint);

var webApp = builder.AddProject<Projects.WebApp>("webapp", launchProfileName)
    .WithExternalHttpEndpoints()
    .WithReference(basketApi)
    .WithReference(catalogApi)
    .WithReference(orderingApi)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithEnvironment(callback => callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otelExporterOtlpEndpoint)
    .WithEnvironment("IdentityUrl", identityEndpoint);

// set to true if you want to use OpenAI
bool useOpenAI = false;
if (useOpenAI)
{
    builder.AddOpenAI(catalogApi, webApp);
}

bool useOllama = false;
if (useOllama)
{
    builder.AddOllama(catalogApi, webApp);
}

// Wire up the callback urls (self referencing)
webApp.WithEnvironment("CallBackUrl", webApp.GetEndpoint(launchProfileName));
webhooksClient.WithEnvironment("CallBackUrl", webhooksClient.GetEndpoint(launchProfileName));

// Identity has a reference to all of the apps for callback urls, this is a cyclic reference
identityApi.WithEnvironment("BasketApiClient", basketApi.GetEndpoint("http"))
           .WithEnvironment("OrderingApiClient", orderingApi.GetEndpoint("http"))
           .WithEnvironment("WebhooksApiClient", webHooksApi.GetEndpoint("http"))
           .WithEnvironment("WebhooksWebClient", webhooksClient.GetEndpoint(launchProfileName))
           .WithEnvironment("WebAppClient", webApp.GetEndpoint(launchProfileName));

// Make services depend on observability stack using WaitFor instead of WithReference
// The telemetry services need to initialize in the correct order
otel.WaitFor(jaeger);
grafana.WaitFor(prometheus);
grafana.WaitFor(jaeger);

// Make application services wait for the telemetry collector to be ready
webApp.WaitFor(otel);
basketApi.WaitFor(otel);
orderingApi.WaitFor(otel);
catalogApi.WaitFor(otel);

builder.Build().Run();

// For test use only.
// Looks for an environment variable that forces the use of HTTP for all the endpoints. We
// are doing this for ease of running the Playwright tests in CI.
static bool ShouldUseHttpForEndpoints()
{
    const string EnvVarName = "ESHOP_USE_HTTP_ENDPOINTS";
    var envValue = Environment.GetEnvironmentVariable(EnvVarName);

    // Attempt to parse the environment variable value; return true if it's exactly "1".
    return int.TryParse(envValue, out int result) && result == 1;
}
