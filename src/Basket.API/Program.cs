using eShop.Basket.API.Extensions;
using eShop.Basket.API.Grpc;
using eShop.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults including OpenTelemetry
builder.AddServiceDefaults();

// Add application services
builder.AddApplicationServices();

// Add gRPC services
builder.Services.AddGrpc();

var app = builder.Build();

// Add default endpoints (health checks, etc.)
app.MapDefaultEndpoints();

// Map gRPC service
app.MapGrpcService<BasketService>();

app.Run();
